using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Delivery;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Schedule;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusBuiltInNpcOpportunityProviders
    {
        public static void Install()
        {
            CampusNpcOpportunityRegistry.Register(CampusClassroomNpcOpportunityProvider.Instance);
            CampusNpcOpportunityRegistry.Register(CampusCanteenNpcOpportunityProvider.Instance);
            CampusNpcOpportunityRegistry.Register(CampusDeliveryNpcOpportunityProvider.Instance);
            CampusNpcOpportunityRegistry.Register(CampusInspectionNpcOpportunityProvider.Instance);
            CampusNpcOpportunityRegistry.Register(CampusPrankNpcOpportunityProvider.Instance);
            CampusNpcOpportunityRegistry.Register(CampusStoreNpcOpportunityProvider.Instance);
        }
    }
}
