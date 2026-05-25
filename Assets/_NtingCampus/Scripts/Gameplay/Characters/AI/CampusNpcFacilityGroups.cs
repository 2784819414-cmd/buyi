using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcFacilityTypeSets
    {
        public const string StudentDesks = "student_desks";
        public const string Podiums = "podiums";
        public const string OfficeDesks = "office_desks";
        public const string Workstations = "workstations";
        public const string ServiceWindows = "service_windows";
        public const string WorkerStands = "worker_stands";

        public static CampusFacilityType[] Get(string groupId)
        {
            switch (NormalizeId(groupId))
            {
                case StudentDesks:
                    return new[] { CampusFacilityType.StudentDesk };
                case Podiums:
                    return new[] { CampusFacilityType.Podium, CampusFacilityType.Blackboard };
                case OfficeDesks:
                    return new[] { CampusFacilityType.OfficeDesk, CampusFacilityType.Desk };
                case Workstations:
                    return new[] { CampusFacilityType.OfficeDesk, CampusFacilityType.Desk, CampusFacilityType.Storage };
                case ServiceWindows:
                    return new[] { CampusFacilityType.ServiceWindow };
                case WorkerStands:
                    return new[] { CampusFacilityType.WorkerStandPoint };
                default:
                    return System.Array.Empty<CampusFacilityType>();
            }
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
