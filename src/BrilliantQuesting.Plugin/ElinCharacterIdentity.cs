using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Asks Elin who somebody is, and carries the answer across the seam without interpreting it.
    ///
    /// The identity counterpart of <see cref="ElinHomeState"/>. Six facets, read from six
    /// different places in the game, each on its own: the `SourceChara` row is the character
    /// archetype, `idRace` and the race row are what they are, the source `job` column is what
    /// they do for a living, the `hobbies` column is what they do otherwise, the trait subclass
    /// says what can be bought from them, and the trait subclass plus `faction` say what they are
    /// entitled to do and on whose behalf.
    ///
    /// Every read is wrapped on its own, which is the whole point of the shape. A member this
    /// build stopped exposing costs its own facet and nothing else: race going unreadable after a
    /// patch must not take work, hobby and institutional standing with it. A facet that could not
    /// be read is unknown - never "", never "local", never a default job - and unknown grants
    /// nothing.
    ///
    /// Nothing here decides what an answer *means*. A `job` of `farmer` is carried through as
    /// `farmer`; what a farmer plausibly knows, is asked about or is eligible for is derived
    /// above this seam and deliberately nowhere near it. Nothing here writes, registers or
    /// materialises anybody either: the whole file is reads.
    /// </summary>
    internal static class ElinCharacterIdentity
    {
        /// <summary>
        /// Elin's conventional "no row" markers in an id column. The one normalisation in this
        /// file, and it only ever removes a claim: a hobby id of `0` is the sheet saying nothing
        /// rather than a hobby called zero, and carrying it through would invent an interest the
        /// character does not have.
        /// </summary>
        private static readonly string[] EmptyIdMarkers = { "0", "-1" };

        /// <summary>
        /// Trait subclasses whose name says the character sells or performs something. Matched on
        /// the type name because the shipped trait hierarchy is wide and mostly unverified here;
        /// what crosses the seam is that type name verbatim, so an unrecognised shop trait is
        /// carried through as itself rather than mapped onto a service BQ happens to know.
        /// </summary>
        private static readonly string[] ServiceTraitMarkers =
        {
            "Shop", "Trade", "Merchant", "Vendor", "Store", "Inn", "Tavern", "Trainer", "Heal"
        };

        /// <summary>
        /// Whether the shop is open for business now. Unverified on a running game, so a build
        /// with none of these members reports <see cref="ServiceAvailability.Unknown"/> - which
        /// says a service exists and this build cannot say whether it is being offered, rather
        /// than telling the player a trade is closed on the strength of a missing member.
        /// </summary>
        private static readonly string[] ServiceOpenNames = { "IsOpen", "isOpen", "CanTrade", "ShopOpen" };

        private static bool _reportedShape;

        /// <summary>
        /// Who this character is, as far as this build can tell. Never throws and never returns
        /// null: an unresolvable character is somebody every facet is unknown about.
        /// </summary>
        internal static CharacterIdentity Read(Chara chara, EntityId actor, ManualLogSource log)
        {
            CharacterIdentityBuilder builder = new CharacterIdentityBuilder(actor);
            if (chara == null)
            {
                return builder.Build();
            }

            object source = TryRead(() => VanillaApiReflection.ReadObject(chara, "source"));

            ReadCharacterArchetype(builder, source);
            ReadRace(builder, chara, source);
            ReadWork(builder, chara, source);
            ReadHobbies(builder, source);
            ReadService(builder, chara);
            ReadInstitutions(builder, chara);

            Report(log);
            return builder.Build();
        }

        /// <summary>
        /// What kind of character this is - the `SourceChara` row itself. Its id is the handle;
        /// its name or aka is what the game calls that kind, which is not this character's own
        /// name and is never read from the instance.
        /// </summary>
        private static void ReadCharacterArchetype(CharacterIdentityBuilder builder, object source)
        {
            string id = TryRead(() => VanillaApiReflection.ReadText(source, "id", "Id"));
            if (IsAnswer(id))
            {
                builder.WithCharacterArchetype(id, TryRead(() => VanillaApiReflection.ReadText(source, "aka", "name", "Name")));
            }
        }

        private static void ReadRace(CharacterIdentityBuilder builder, Chara chara, object source)
        {
            string id = TryRead(() => VanillaApiReflection.ReadText(chara, "idRace"));
            if (!IsAnswer(id))
            {
                id = TryRead(() => TextOf(VanillaApiReflection.ReadObject(source, "race")));
            }

            if (!IsAnswer(id))
            {
                return;
            }

            object row = TryRead(() => VanillaApiReflection.ReadObject(chara, "race")
                                       ?? VanillaApiReflection.ReadObject(source, "race"));
            builder.WithRace(id, TryRead(() => VanillaApiReflection.ReadText(row, "name", "Name")));
        }

        /// <summary>
        /// What they do for a living. The source `job` column only: the Home branch's own work
        /// assignment is a second answer to the same question, it is read through
        /// <see cref="ElinHomeState"/> for the settlement that has one, and reconciling the two is
        /// not this read's business.
        /// </summary>
        private static void ReadWork(CharacterIdentityBuilder builder, Chara chara, object source)
        {
            string id = TryRead(() => VanillaApiReflection.ReadText(source, "job", "idJob"));
            if (!IsAnswer(id))
            {
                return;
            }

            object row = TryRead(() => VanillaApiReflection.ReadObject(chara, "job"));
            builder.WithWork(id, TryRead(() => VanillaApiReflection.ReadText(row, "name", "Name")));
        }

        /// <summary>
        /// What they do when they are not working. Zero or more, and the weakest of the six: a
        /// column that is present and lists nothing is a fact, and a column this build does not
        /// have is not - so only the first of those marks the facet read.
        /// </summary>
        private static void ReadHobbies(CharacterIdentityBuilder builder, object source)
        {
            if (source == null
                || VanillaApiReflection.ResolveReadableMember(source.GetType(), "hobbies") == null)
            {
                return;
            }

            object hobbies = TryRead(() => VanillaApiReflection.ReadObject(source, "hobbies"));
            builder.WithHobbiesRead();
            if (hobbies == null)
            {
                return;
            }

            if (hobbies is string single)
            {
                AddHobbies(builder, single);
                return;
            }

            if (hobbies is IEnumerable list)
            {
                foreach (object entry in list)
                {
                    AddHobbies(builder, TextOf(entry));
                }
            }
            else
            {
                AddHobbies(builder, TextOf(hobbies));
            }
        }

        /// <summary>One entry, or a comma-separated column of them, added verbatim.</summary>
        private static void AddHobbies(CharacterIdentityBuilder builder, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] entries = text.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string id = entries[i].Trim();
                if (IsAnswer(id))
                {
                    builder.AddHobby(id);
                }
            }
        }

        /// <summary>
        /// What can be got from this person, and whether it is on offer now. The trait subclass is
        /// the evidence and the trait's own type name is the id, so a shop trait this project has
        /// never heard of still crosses the seam as a service.
        /// </summary>
        private static void ReadService(CharacterIdentityBuilder builder, Chara chara)
        {
            Trait trait = TryRead(() => chara.trait);
            if (trait == null)
            {
                return;
            }

            string name = trait.GetType().Name;
            if (!LooksLikeService(name))
            {
                return;
            }

            builder.WithService(name, null, Availability(trait));
        }

        private static ServiceAvailability Availability(Trait trait)
        {
            for (int i = 0; i < ServiceOpenNames.Length; i++)
            {
                if (VanillaApiReflection.ResolveReadableMember(trait.GetType(), ServiceOpenNames[i]) == null)
                {
                    continue;
                }

                return VanillaApiReflection.HasTrueFlag(trait, ServiceOpenNames[i])
                    ? ServiceAvailability.Offered
                    : ServiceAvailability.NotOffered;
            }

            return ServiceAvailability.Unknown;
        }

        /// <summary>
        /// What they are entitled to do, and on whose behalf. Two markers, read together because
        /// they answer halves of one question: the trait subclass is the office - `TraitGuard` for
        /// the watch, the guild personnel traits for the clerks and doormen who staff a guild -
        /// and `faction` is the body it is held on behalf of.
        ///
        /// This is the read that used to live in <see cref="ElinAuthorityRoles"/> and write
        /// straight into the world model. It is an observation now, and the standing the authority
        /// policy grants is derived from it - one trait read, one place it is interpreted.
        ///
        /// Whether a member of the public holds a rank inside their faction is not readable per
        /// character on this build: `FactionRelation.rank` is the player's own standing.
        /// So a rank is reported only where the faction object itself answers one, and is
        /// otherwise unknown rather than zero.
        /// </summary>
        private static void ReadInstitutions(CharacterIdentityBuilder builder, Chara chara)
        {
            if (VanillaApiReflection.ResolveReadableMember(chara.GetType(), "trait") == null)
            {
                return;
            }

            builder.WithInstitutionsRead();

            IdentityFacet body = FactionOf(chara);
            Trait trait = TryRead(() => chara.trait);
            string office = OfficeOf(trait);

            if (office != null)
            {
                builder.AddInstitution(new InstitutionalRole(body, IdentityFacet.FromVanilla(office)));
                return;
            }

            if (body.IsKnown)
            {
                // Belonging to a faction is not an office. It crosses the seam because "on whose
                // behalf" is half of the facet, and it grants nothing on its own.
                builder.AddInstitution(new InstitutionalRole(body, IdentityFacet.Unknown));
            }
        }

        /// <summary>
        /// The office the trait marks, as the game's own type name, or null for somebody who holds
        /// none. Typed checks rather than name matching, because these three are the markers this
        /// project has actually verified and a guard is the one identity read that decides whether
        /// a crime can be reported at all.
        /// </summary>
        internal static string OfficeOf(Trait trait)
        {
            if (trait == null)
            {
                return null;
            }

            if (trait is TraitGuard || trait is TraitGuildPersonnel || trait is TraitGuildDoorman)
            {
                return trait.GetType().Name;
            }

            return null;
        }

        private static IdentityFacet FactionOf(Chara chara)
        {
            object faction = TryRead(() => VanillaApiReflection.ReadObject(chara, "faction"));
            if (faction == null)
            {
                return IdentityFacet.Unknown;
            }

            string id = TryRead(() => VanillaApiReflection.ReadText(faction, "id", "Id", "uid"));
            string name = TryRead(() => VanillaApiReflection.ReadText(faction, "Name", "name"));
            if (!IsAnswer(id))
            {
                id = name;
                name = null;
            }

            return IsAnswer(id) ? IdentityFacet.FromVanilla(id, name) : IdentityFacet.Unknown;
        }

        private static bool LooksLikeService(string traitName)
        {
            for (int i = 0; i < ServiceTraitMarkers.Length; i++)
            {
                if (traitName.IndexOf(ServiceTraitMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string TextOf(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string text = value.ToString();
            return text == null ? string.Empty : text.Trim();
        }

        /// <summary>
        /// Whether this is an answer at all. Empty is nothing, and so are the sheet's own "no row"
        /// markers - carrying either through would turn a silence into a claim.
        /// </summary>
        private static bool IsAnswer(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0; i < EmptyIdMarkers.Length; i++)
            {
                if (id == EmptyIdMarkers[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// One facet's read, and only that facet's. A member this build renamed, a source row that
        /// is not there and a getter that threw all cost the datum they were being asked for and
        /// leave the other five alone.
        /// </summary>
        private static T TryRead<T>(Func<T> read)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return default;
            }
        }

        private static void Report(ManualLogSource log)
        {
            if (_reportedShape || log == null)
            {
                return;
            }

            _reportedShape = true;
            log.LogInfo("BQ character identity: reading SourceChara id/aka, Chara.idRace and the race row, "
                        + "source job, source hobbies, service trait subclasses and TraitGuard/"
                        + "TraitGuildPersonnel/TraitGuildDoorman plus Chara.faction. Each facet fails on its "
                        + "own; an unread facet is unknown and grants nothing.");
        }
    }
}
