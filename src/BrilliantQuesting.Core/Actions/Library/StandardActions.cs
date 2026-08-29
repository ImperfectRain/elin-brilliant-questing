namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// The verb set the headless laboratories run on.
    ///
    /// Forty-one verbs, spanning seven of the eight solution families. The eighth is
    /// Home/Community, which waits on being able to read a Home at all. The target is roughly
    /// forty - but only once each verb has proven it can carry a situation on its own, since a
    /// verb that cannot be the whole answer to some problem is not pulling its weight. That rule
    /// is why the faith family is two verbs and not five: asking a god and paying for the asking
    /// are the whole of what answers a matter in his gift, and a third would have been a count
    /// rather than a route.
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
                .Register(new TrespassAction())
                .Register(new DestroyEvidenceAction())
                .Register(new SabotageAction())
                .Register(new ExtortAction())
                .Register(new ImpersonateAction())
                .Register(new FenceGoodsAction())
                .Register(new ForgeAction())
                .Register(new SmuggleAction())
                .Register(new CookAction())
                .Register(new BrewAction())
                .Register(new AlchemyAction())
                .Register(new BuildAction())
                .Register(new CraftToPropertyAction())
                .Register(new RepairAction())
                .Register(new MakeOfferingAction())
                .Register(new InvokeBlessingAction())
                .Register(new ReturnItemAction())
                .Register(new KeepItemAction())
                .Register(new AttackAction());
        }
    }
}
