using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Presentation
{
    // Transient, immutable presentation values. No save state or native objects belong here.
    public sealed class JournalItem
    {
        public JournalItem(string text, string destinationPageId = null)
        {
            Text = text ?? string.Empty;
            DestinationPageId = destinationPageId;
        }
        public string Text { get; }
        public string DestinationPageId { get; }
    }

    public sealed class JournalSection
    {
        public JournalSection(string heading, IEnumerable<JournalItem> items, string emptyText)
        {
            Heading = heading;
            Items = Array.AsReadOnly(items.ToArray());
            EmptyText = emptyText;
        }
        public string Heading { get; }
        public IReadOnlyList<JournalItem> Items { get; }
        public string EmptyText { get; }
    }

    public sealed class JournalPageDescriptor
    {
        public JournalPageDescriptor(string id, string label,
            Func<NarrativeWorldState, IVanillaState, IReadOnlyList<JournalSection>> project,
            Func<NarrativeWorldState, IVanillaState, bool> isAvailable = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable page id is required.", nameof(id));
            Id = id;
            Label = label;
            Project = project ?? throw new ArgumentNullException(nameof(project));
            IsAvailable = isAvailable ?? ((world, vanilla) => true);
        }
        public string Id { get; }
        public string Label { get; }
        // Both callbacks must only read authorities; neither is an exposure/delivery event.
        public Func<NarrativeWorldState, IVanillaState, IReadOnlyList<JournalSection>> Project { get; }
        public Func<NarrativeWorldState, IVanillaState, bool> IsAvailable { get; }
    }

    public sealed class JournalPageRegistry
    {
        public JournalPageRegistry(IEnumerable<JournalPageDescriptor> pages)
        {
            JournalPageDescriptor[] ordered = pages.ToArray();
            if (ordered.Length == 0 || ordered.Any(p => p == null)
                || ordered.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
                throw new ArgumentException("Journal pages must be nonempty and have unique ids.", nameof(pages));
            Pages = Array.AsReadOnly(ordered);
        }
        public IReadOnlyList<JournalPageDescriptor> Pages { get; }
        public JournalPageDescriptor Resolve(string id) => Pages.FirstOrDefault(p => p.Id == id) ?? Pages[0];
        public IReadOnlyList<JournalPageDescriptor> Available(NarrativeWorldState world, IVanillaState vanilla) =>
            Array.AsReadOnly(Pages.Where(p => p.IsAvailable(world, vanilla)).ToArray());

        public static JournalPageRegistry CreateDefault() => new JournalPageRegistry(new[]
        {
            new JournalPageDescriptor("overview", "Overview", JournalPages.Overview),
            new JournalPageDescriptor("chronicle", "Chronicle", JournalPages.ChroniclePage)
        });
    }

    public static class JournalPages
    {
        private static IReadOnlyList<JournalSection> Unloaded() => Array.AsReadOnly(new[]
        {
            Section("Brilliant Questing", Array.Empty<string>(), "No Brilliant Questing state is loaded.")
        });

        public static IReadOnlyList<JournalSection> Overview(NarrativeWorldState world, IVanillaState vanilla)
        {
            if (world == null || vanilla == null || vanilla.PlayerId.IsNone) return Unloaded();
            var safeText = PlayerText(world);
            var claims = NarrativeJournal.Entries(world, vanilla.PlayerId);
            // ContentProjection decides visibility. Its diagnostic detail (including tension) is not wording.
            var matters = NarrativeContentProjection.Entries(world, vanilla.PlayerId)
                .Where(e => e.ContentClass == NarrativeContentClass.Situation)
                .Select(e => e.Title + " — unresolved");
            var people = claims.Select(e => world.Knowledge.GetFact(e.FactId)).Where(f => f != null)
                .SelectMany(f => new[] { f.Subject, f.Object }).Distinct()
                .Select(world.Registry.GetNpc).Where(n => n != null)
                .OrderBy(n => n.Name, StringComparer.Ordinal).ThenBy(n => n.Id.Value, StringComparer.Ordinal)
                .Select(n => string.IsNullOrWhiteSpace(n.Name) ? "Someone whose name is unknown" : n.Name);
            return Array.AsReadOnly(new[]
            {
                Section("Active matters", matters, "No active matters known to you."),
                Section("Standing", StandingSheet.Entries(world, vanilla)
                    .Select(e => safeText(e.Title + (e.Detail.Length == 0 ? "" : " — " + e.Detail))),
                    "Nothing earned yet."),
                Section("Known people", people, "No one tied to a known claim yet."),
                Section("Known claims", claims.Select(e => "[" + e.Tag + "] " + safeText(e.Text)
                    + (e.CanProve ? " — evidence available" : "")),
                    "Nothing known yet."),
                new JournalSection("Resolved history", new[] { new JournalItem("Read your Chronicle", "chronicle") }, "Nothing resolved yet.")
            });
        }

        public static IReadOnlyList<JournalSection> ChroniclePage(NarrativeWorldState world, IVanillaState vanilla)
        {
            if (world == null || vanilla == null || vanilla.PlayerId.IsNone) return Unloaded();
            // Reuse the complete existing reading, including WhatWasKnown and its epistemic tags.
            string text = PlayerText(world)(ChronicleNarrative.Export(world, vanilla.PlayerId, vanilla.Now));
            // The export already separates sections with blank lines and indents their items.
            // Preserve those sections as native headers without maintaining a second history formatter.
            var sections = new List<JournalSection>();
            string[] blocks = text.TrimEnd().Split(new[] { "\n\n" }, StringSplitOptions.None);
            var introduction = blocks[0].Split('\n').Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            if (introduction.Length > 0) sections.Add(Section("Chronicle", introduction, "Nothing to tell yet."));
            foreach (string block in blocks.Skip(1))
            {
                string[] lines = block.Split('\n');
                bool hasHeading = lines[0].Length > 0 && !char.IsWhiteSpace(lines[0][0]);
                sections.Add(Section(hasHeading ? lines[0] : "Chronicle",
                    (hasHeading ? lines.Skip(1) : lines).Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim()), "Nothing to tell yet."));
            }
            return sections.AsReadOnly();
        }

        private static JournalSection Section(string title, IEnumerable<string> lines, string empty) =>
            new JournalSection(title, lines.Select(line => new JournalItem(line)), empty);

        // Existing text projections intentionally fall back to ids for diagnostics. At the player
        // boundary replace those fallbacks; do not change the registry or the historical record.
        private static Func<string, string> PlayerText(NarrativeWorldState world)
        {
            var ids = world.Knowledge.Facts.Values.SelectMany(f => new[] { f.Id, f.Subject, f.Object })
                .Concat(world.Ledger.Events.SelectMany(e => new[] { e.Id, e.Actor, e.Target, e.Zone, e.ThreadId }))
                .Concat(world.Registry.Npcs.Keys).Concat(world.Registry.Sites.Keys)
                .Concat(world.Registry.Organizations.Keys).Where(id => !id.IsNone).Distinct()
                .OrderByDescending(id => id.Value.Length);
            var names = ids.ToDictionary(id => id.Value, id =>
            {
                string name = world.Registry.NameOf(id);
                return name == id.Value || string.IsNullOrWhiteSpace(name) ? "someone or something unnamed" : name;
            }, StringComparer.Ordinal);
            if (names.Count == 0) return text => text;
            var pattern = new System.Text.RegularExpressions.Regex(@"(?<![\w])(?:"
                + string.Join("|", names.Keys.Select(System.Text.RegularExpressions.Regex.Escape)) + @")(?![\w])");
            return text => pattern.Replace(text, match => names[match.Value]);
        }
    }
}
