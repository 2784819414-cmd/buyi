namespace NtingCampus.Gameplay.Characters
{
    public enum CampusNpcIntentKind
    {
        Idle = 0,
        AttendAssignedDesk = 10,
        DozeInClass = 11,
        TeachAssignedClass = 20,
        ReturnToOfficeDesk = 21,
        WorkCanteenCounter = 30,
        CoverCanteenWindows = 31,
        WorkStoreCheckout = 40,
        AuditStoreShelves = 41,
        WatchDeliveryPoint = 50,
        UsePhoneForDelivery = 60,
        PickupDelivery = 61,
        EatCanteenMeal = 62,
        BrowseStoreShelf = 63,
        PayStoreCheckout = 64,
        RestInDorm = 70,
        Roam = 90
    }
}
