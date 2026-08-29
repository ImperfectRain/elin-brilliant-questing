using BrilliantQuesting.World;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// Turns a fact into the words a person would use for it.
    ///
    /// One renderer, because the journal and a bark have to say the same thing about the same
    /// claim. A player who reads "Kip stole the locket" in the journal and hears "Kip took the
    /// ring" in the market is being told about two matters, and the only difference between them
    /// was which file rendered the sentence.
    ///
    /// It renders the claim and nothing else - no confidence, no source, no ids. How sure the
    /// speaker is belongs to whoever is speaking, and is worded rather than numbered
    /// (<c>LW §3.1</c>).
    /// </summary>
    public static class FactPhrasing
    {
        /// <summary>The claim itself: "Kip stole the silver locket".</summary>
        public static string Claim(EntityRegistry registry, Fact fact)
        {
            if (fact == null)
            {
                return "something";
            }

            string subject = registry.NameOf(fact.Subject);
            string obj = registry.Npcs.ContainsKey(fact.Object)
                ? registry.NameOf(fact.Object)
                : !string.IsNullOrEmpty(fact.Value) ? fact.Value : fact.Object.Value;
            return (subject + " " + fact.Predicate.Replace('_', ' ') + " " + obj).Trim();
        }
    }
}
