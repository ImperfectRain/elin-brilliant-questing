using BrilliantQuesting.Threads;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// BQ-124 recovery documentation for canonical archetype worst outcomes.
    /// Routes point back to existing verbs or vanilla-owned systems; they do not reopen history or
    /// promise that every attempt succeeds.
    /// </summary>
    internal static class ArchetypeRecoveryRoutes
    {
        public static void AddPettyTheft(NarrativeThread thread)
        {
            Add(thread, "feud after an unresolved or false accusation", "return_item / report",
                "recover or prove the real item/fact; may require theft, search, or authority standing",
                "the object or proof can be missing, hidden, or disbelieved",
                "the true ownership/theft record can be acted on again without erasing the feud");
        }

        public static void AddShortage(NarrativeThread thread)
        {
            Add(thread, "settlement deteriorates while demand remains open", "buy_supplies / cook / alchemy / repair / invest_in_supplier",
                "real goods, raw materials, or supplier investment",
                "quality checks, production inputs, and supplier cause reads can fail",
                "open demand facts are superseded and local pressure is relieved");
        }

        public static void AddHuntedWitness(NarrativeThread thread)
        {
            Add(thread, "witness is found or sanctuary leaks", "shelter / assign_protection / store_evidence / report",
                "Home capacity, Public Safety, stored proof, or authority access",
                "Home reads can be unavailable and protection checks can fail",
                "the exposure can be answered while the threat event remains history");
        }

        public static void AddFalseAccusation(NarrativeThread thread)
        {
            Add(thread, "innocent is institutionally blamed", "search / report",
                "recover surviving proof and take it to an authority",
                "proof may be hidden, destroyed, or rejected at the authority threshold",
                "the true fact remains recoverable and can supersede the false institutional judgment");
        }

        public static void AddDistressedBusiness(NarrativeThread thread)
        {
            Add(thread, "business_failed", "reopen_business",
                "three times the original debt in real orens",
                "an Investing recovery check can fail after the money is committed",
                "business continuity returns from Failed to Recovered while the failure stays in history");
        }

        public static void AddRecognizedViolence(NarrativeThread thread)
        {
            Add(thread, "death is judged as murder or unlawful violence", "report / invoke_authority",
                "credible proof, witnesses, or guild standing",
                "authorities may reject weak proof and guild asks can be refused",
                "standing trouble around the death can be answered without undoing the death");
        }

        public static void AddFestivalCompetition(NarrativeThread thread)
        {
            Add(thread, "player loses the contest", "perform / enter later competition",
                "time, materials, and another public attempt",
                "the contest check can still be lost to another NPC",
                "standing and local memory can change through a later public result");
        }

        public static void AddHomeResidentProblem(NarrativeThread thread)
        {
            Add(thread, "household pressure harms a resident", "provide_supplies / buy_supplies",
                "real food, logistics, or money",
                "Home capacity and supply checks can still fail",
                "the resident's open need is superseded and Home pressure is relieved");
        }

        private static void Add(NarrativeThread thread, string worstOutcome, string actionId, string price, string uncertainty, string restores)
        {
            if (thread == null)
            {
                return;
            }

            thread.RecoveryRoutes.Add(new RecoveryRoute(worstOutcome, actionId, price, uncertainty, restores));
        }
    }
}
