namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private static float CalculateOpportunityScore(
            CampusNpcAiRuntime npc,
            ScheduleTemplateRecord template,
            ScheduleEntryRecord entry,
            ActionDefinitionRecord actionDefinition,
            CampusNpcActionOpportunity opportunity)
        {
            return entry != null ? entry.Score : 0f;
        }
    }
}
