namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusBuiltInNpcOpportunityProviders
    {
        public static void Install()
        {
            CampusNpcOpportunityRegistry.Register(CampusConfigDrivenNpcOpportunityProvider.Instance);
        }
    }
}
