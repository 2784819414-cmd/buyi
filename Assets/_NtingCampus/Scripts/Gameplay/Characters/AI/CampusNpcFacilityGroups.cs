using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcFacilityGroups
    {
        public static readonly CampusFacilityType[] StudentDesks = { CampusFacilityType.StudentDesk };
        public static readonly CampusFacilityType[] Podiums = { CampusFacilityType.Podium, CampusFacilityType.Blackboard };
        public static readonly CampusFacilityType[] OfficeDesks = { CampusFacilityType.OfficeDesk, CampusFacilityType.Desk };
        public static readonly CampusFacilityType[] CanteenWorkstations =
        {
            CampusFacilityType.CanteenClerkStandPoint
        };

        public static readonly CampusFacilityType[] StoreWorkstations =
        {
            CampusFacilityType.StoreCheckout
        };

        public static readonly CampusFacilityType[] DeliveryPoints = { CampusFacilityType.DeliveryDropPoint };
        public static readonly CampusFacilityType[] Shelves =
        {
            CampusFacilityType.StoreShelf,
            CampusFacilityType.Storage,
            CampusFacilityType.CanteenFoodTray
        };
    }
}
