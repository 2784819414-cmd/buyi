using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcFacilityGroups
    {
        public const string StudentDesks = "student_desks";
        public const string Podiums = "podiums";
        public const string OfficeDesks = "office_desks";
        public const string Workstations = "workstations";
        public const string ServiceWindows = "service_windows";
        public const string WorkerStands = "worker_stands";

        public static CampusFacilityType[] Get(string groupId)
        {
            return CampusNpcEcologyPresetCatalog.GetFacilityGroup(groupId);
        }
    }
}
