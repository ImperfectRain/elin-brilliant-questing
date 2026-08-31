using BrilliantQuesting.Threads;

namespace BrilliantQuesting.Integration
{
    public static class EvidenceReconciliationPolicy
    {
        public static bool MayRecreateMissingPhysicalEvidence(NarrativeThread thread)
        {
            return thread != null && thread.GenerationCauses.Count == 0;
        }
    }
}
