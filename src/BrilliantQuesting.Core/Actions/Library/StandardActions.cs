namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// The verb set the three-NPC laboratory runs on.
    ///
    /// Twelve verbs, spanning six of the eight solution families. The roadmap's next milestone is
    /// thirty - but only once each of these has proven it can carry a situation on its own, since
    /// a verb that cannot be the whole answer to some problem is not pulling its weight.
    /// </summary>
    public static class StandardActions
    {
        public static ActionRegistry CreateRegistry()
        {
            return new ActionRegistry()
                .Register(new QuestionAction())
                .Register(new PersuadeAction())
                .Register(new LieAction())
                .Register(new IntimidateAction())
                .Register(new BribeAction())
                .Register(new SearchForEvidenceAction())
                .Register(new ExposeSecretAction())
                .Register(new PickpocketAction())
                .Register(new PlantEvidenceAction())
                .Register(new ReturnItemAction())
                .Register(new KeepItemAction())
                .Register(new AttackAction());
        }
    }
}
