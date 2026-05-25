namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private static float CalculateOpportunityScore(
            CampusNpcAiRuntime npc,
            NpcDecisionProfileRecord profile,
            ScheduleEntryRecord entry,
            ActionRecord action,
            CampusNpcActionOpportunity opportunity)
        {
            return entry != null ? entry.Score : 0f;
        }
    }
}
