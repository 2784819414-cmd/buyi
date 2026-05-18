namespace NtingCampus.Gameplay.Characters
{
    public enum CampusNpcIntentKind
    {
        Idle = 0,
        AttendAssignedDesk = 10,
        TeachAssignedClass = 20,
        ReturnToOfficeDesk = 21,
        WorkCanteenCounter = 30,
        CoverCanteenWindows = 31,
        WorkStoreCheckout = 40,
        AuditStoreShelves = 41,
        WatchDeliveryPoint = 50,
        UsePhoneForDelivery = 60,
        PickupDelivery = 61,
        RestInDorm = 70,
        InvestigateEvent = 80,
        WatchEvent = 81,
        Roam = 90
    }
}
