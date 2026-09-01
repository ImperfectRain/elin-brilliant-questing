namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// The verb set the headless laboratories run on.
    ///
    /// Sixty-five verbs, spanning all eight solution families. The target was roughly forty - but
    /// only once each verb has proven it can carry a situation on its own, since a verb that
    /// cannot be the whole answer to some problem is not pulling its weight.
    /// </summary>
    public static class StandardActions
    {
        public static ActionRegistry CreateRegistry()
        {
            return new ActionRegistry()
                .Register(new BuildRapportAction())
                .Register(new QuestionAction())
                .Register(new PerformForCrowdAction())
                .Register(new PersuadeAction())
                .Register(new LieAction())
                .Register(new IntimidateAction())
                .Register(new BribeAction())
                .Register(new PayDebtAction())
                .Register(new DonateToMuseumAction())
                .Register(new BuyDistressedBusinessAction())
                .Register(new ReopenFailedBusinessAction())
                .Register(new BuySuppliesAction())
                .Register(new InvestInSupplierAction())
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
                .Register(new InvokeGuildAuthorityAction())
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
                .Register(new DeliverFishingHaulAction())
                .Register(new DeliverHarvestAction())
                .Register(new BrewAction())
                .Register(new AlchemyAction())
                .Register(new BuildAction())
                .Register(new CraftToPropertyAction())
                .Register(new RepairAction())
                .Register(new MakeOfferingAction())
                .Register(new InvokeBlessingAction())
                .Register(new ClearObstructionAction())
                .Register(new CarryAction())
                .Register(new RescueAction())
                .Register(new MineBypassAction())
                .Register(new BreakBarrierAction())
                .Register(new TransportAction())
                .Register(new CaptureAction())
                .Register(new RestrainAction())
                .Register(new EscortAction())
                .Register(new ShelterAction())
                .Register(new GiveBredAnimalAction())
                .Register(new HostAction())
                .Register(new RecruitSpecialistAction())
                .Register(new AssignProtectionAction())
                .Register(new ProvideSuppliesAction())
                .Register(new StoreEvidenceAction())
                .Register(new ReturnItemAction())
                .Register(new KeepItemAction())
                .Register(new AttackAction());
        }
    }
}
