using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenNpcOpportunityProvider : ICampusNpcActionOpportunityProvider
    {
        public static CampusCanteenNpcOpportunityProvider Instance { get; } =
            new CampusCanteenNpcOpportunityProvider();

        public string ProviderId => "canteen";

        private CampusCanteenNpcOpportunityProvider()
        {
        }

        public bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query)
        {
            if (!npc.IsValid)
            {
                return false;
            }

            switch (query.Purpose)
            {
                case CampusNpcOpportunityPurpose.Duty:
                    return npc.Data.Role == CampusCharacterRole.Staff &&
                           (npc.Data.StaffDuty & CampusStaffDuty.CanteenClerk) != 0;
                case CampusNpcOpportunityPurpose.FreeMovement:
                    return npc.Data.Role == CampusCharacterRole.Student;
                default:
                    return false;
            }
        }

        public void CollectOpportunities(
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results)
        {
            if (results == null || !npc.IsValid)
            {
                return;
            }

            CampusCanteenService canteen = CampusCanteenService.Resolve(false);
            if (canteen == null || !canteen.IsServingOpenNow())
            {
                return;
            }

            switch (query.Purpose)
            {
                case CampusNpcOpportunityPurpose.Duty:
                    CollectClerkDuty(npc, canteen, results);
                    break;
                case CampusNpcOpportunityPurpose.FreeMovement:
                    CollectCustomerMeal(npc, canteen, results);
                    break;
            }
        }

        private static void CollectClerkDuty(
            CampusNpcOpportunityContext npc,
            CampusCanteenService canteen,
            List<CampusNpcActionOpportunity> results)
        {
            if (npc.Data.Role != CampusCharacterRole.Staff ||
                (npc.Data.StaffDuty & CampusStaffDuty.CanteenClerk) == 0 ||
                !CampusNpcScheduleFacts.IsCanteenShiftActive(npc.Segment) ||
                !canteen.TryFindDutyWindowForClerk(npc.Runtime, out CampusCanteenStation station) ||
                station == null ||
                station.WindowObject == null ||
                canteen.HasFoodAtStation(station))
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                "canteen_prepare_meal",
                CampusCharacterAction.PressInteract(station.WindowObject),
                station.ClerkPosition,
                station.RoomId,
                0.16f,
                88f,
                CampusNpcIntentKind.WorkCanteenCounter,
                "CanteenPrepare",
                actor => actor != null &&
                         canteen.IsServingOpenNow() &&
                         !canteen.HasFoodAtStation(station)));
        }

        private static void CollectCustomerMeal(
            CampusNpcOpportunityContext npc,
            CampusCanteenService canteen,
            List<CampusNpcActionOpportunity> results)
        {
            if (npc.Data.Role != CampusCharacterRole.Student ||
                !CampusNpcScheduleFacts.IsMealPeak(npc.Segment) ||
                canteen.HasReceivedMealToday(npc.Runtime) ||
                !canteen.TryFindWindowWithFoodForCustomer(npc.Runtime, out CampusCanteenStation station) ||
                station == null ||
                station.WindowObject == null)
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                "canteen_take_meal",
                CampusCharacterAction.PressInteract(station.WindowObject),
                station.CustomerPosition,
                station.RoomId,
                0.18f,
                70f,
                CampusNpcIntentKind.EatCanteenMeal,
                "CanteenMeal",
                actor => actor != null &&
                         canteen.IsServingOpenNow() &&
                         !canteen.HasReceivedMealToday(actor) &&
                         canteen.HasFoodAtStation(station)));
        }
    }
}
