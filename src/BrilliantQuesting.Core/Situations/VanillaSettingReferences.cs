using System;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    internal static class VanillaSettingReferences
    {
        private static readonly string[] Terms =
        {
            "Kumiromi",
            "Opatos",
            "Ehekatl",
            "Lulwy",
            "Mani",
            "Itzpalt",
            "Jure",
            "Yevan",
            "Horome",
            "Noyel",
            "Derphy",
            "Vernis",
            "Palmia",
            "Yowyn",
            "Mysilia",
            "Lumiest",
            "Olvina",
            "Nefia",
            "ether",
            "Fighters Guild",
            "Mages Guild",
            "Thieves Guild",
            "Merchants Guild"
        };

        public static void Attach(
            SituationCandidateBuilder builder,
            NarrativeWorldState world,
            LocalAffordanceProfile profile,
            ActorAffordances actor,
            ActorAffordances target,
            ItemDescriptor stake)
        {
            if (builder == null || world == null || profile == null)
            {
                return;
            }

            NarrativeSite site = world.Registry.GetSite(profile.ZoneId);
            if (site != null)
            {
                AddMatches(builder, site.Name, "site " + site.Name);
                AddMatches(builder, site.SiteType, "site type " + site.SiteType);
            }

            AddActorMatches(builder, actor);
            AddActorMatches(builder, target);
            if (stake != null)
            {
                AddMatches(builder, stake.Name, "stake " + stake.Name);
                AddMatches(builder, stake.CategoryTag, "stake category " + stake.CategoryTag);
            }
        }

        private static void AddActorMatches(SituationCandidateBuilder builder, ActorAffordances actor)
        {
            if (actor == null)
            {
                return;
            }

            AddMatches(builder, actor.Occupation, actor.Name + "'s occupation");
            foreach (string role in actor.Roles)
            {
                AddMatches(builder, role, actor.Name + "'s role");
            }
        }

        private static void AddMatches(SituationCandidateBuilder builder, string text, string source)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            for (int i = 0; i < Terms.Length; i++)
            {
                if (text.IndexOf(Terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    builder.SettingReference(Terms[i], source);
                }
            }
        }
    }
}
