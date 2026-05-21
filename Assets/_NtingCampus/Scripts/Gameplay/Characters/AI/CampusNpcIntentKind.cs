namespace NtingCampus.Gameplay.Characters
{
    public enum CampusNpcIntentKind
    {
        Idle = 0,
        AttendAssignedDesk = 10,
        DozeInClass = 11,
        TeachAssignedClass = 20,
        ReturnToOfficeDesk = 21,
        AttendPrimaryWorkstation = 30,
        RestInDorm = 70,
        Roam = 90
    }
}
