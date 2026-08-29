namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// The verb set the headless laboratories run on.
    ///
    /// Twenty-five verbs, spanning six of the eight solution families. The target is roughly forty
    /// - but only once each verb has proven it can carry a situation on its own, since a verb that
    /// cannot be the whole answer to some problem is not pulling its weight.
    /// </summary>
    public static class StandardActions
    {
        public static ActionRegistry CreateRegistry()
        {
            return new ActionRegistry()
                .Register(new BuildRapportAction())
                .Register(new QuestionAction())
                .Register(new PersuadeAction())
                .Register(new LieAction())
                .Register(new IntimidateAction())
                .Register(new BribeAction())
                .Register(new PayDebtAction())
                .Register(new SearchForEvidenceAction())
                .Register(new InspectAction())
                .Register(new ExamineCorpseAction())
                .Register(new ReadDocumentAction())
                .Register(new TranslateDocumentAction())
                .Register(new IdentifySubstanceAction())
                .Register(new SearchRecordsAction())
                .Register(new TrackAction())
                .Register(new FollowAction())
                .Register(new EavesdropAction())
                .Register(new CompareTestimonyAction())
                .Register(new ExposeSecretAction())
                .Register(new ReportToAuthorityAction())
                .Register(new PickpocketAction())
                .Register(new PlantEvidenceAction())
                .Register(new ReturnItemAction())
                .Register(new KeepItemAction())
                .Register(new AttackAction());
        }
    }
}
