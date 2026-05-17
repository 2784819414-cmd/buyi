using System;

namespace NtingCampus.Gameplay.Characters
{
    [Flags]
    public enum CampusStaffDuty
    {
        None = 0,
        CanteenClerk = 1 << 0,
        StoreOwner = 1 << 1,
        BookstoreOwner = 1 << 2,
        LibrarianAssistant = 1 << 3,
        LibraryRegistrar = 1 << 4,
        DeliveryWatcher = 1 << 5
    }
}
