using System;

namespace NtingCampus.Gameplay.Characters
{
    [Flags]
    public enum CampusStaffDuty
    {
        None = 0,
        SupportStaff = 1 << 0,
        OperationsStaff = 1 << 1,
        RecordsStaff = 1 << 2,
        FacilityAssistant = 1 << 3,
        Registrar = 1 << 4,
        PatrolStaff = 1 << 5
    }
}
