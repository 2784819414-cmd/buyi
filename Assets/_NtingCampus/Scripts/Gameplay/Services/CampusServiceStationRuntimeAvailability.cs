using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Services
{
    internal static class CampusServiceStationRuntimeAvailability
    {
        public static bool CanServeNow(CampusServiceStation station)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return CanServeNow(
                station,
                bootstrap != null ? bootstrap.WorldService : null,
                bootstrap != null ? bootstrap.RosterService : null,
                bootstrap != null ? bootstrap.TimeController : null);
        }

        public static bool CanServeNow(
            CampusServiceStation station,
            CampusWorldService worldService,
            CampusRosterService rosterService,
            CampusTimeController timeController)
        {
            if (!station.IsOperational || station.Availability == null)
            {
                return false;
            }

            if (!MatchesScheduleWindows(station.Availability, timeController))
            {
                return false;
            }

            switch (station.Availability.Mode)
            {
                case CampusServiceStationAvailabilityMode.Always:
                    return true;
                case CampusServiceStationAvailabilityMode.RequiresAssignedOperator:
                    return worldService != null &&
                           worldService.ServiceStations.TryResolvePresentOperator(
                               station,
                               rosterService,
                               out _);
                default:
                    return false;
            }
        }

        private static bool MatchesScheduleWindows(
            CampusServiceStationAvailabilityDefinition availability,
            CampusTimeController timeController)
        {
            if (availability == null ||
                availability.ScheduleWindows == null ||
                availability.ScheduleWindows.Length == 0)
            {
                return true;
            }

            if (timeController == null)
            {
                return false;
            }

            CampusTimeSegment segment = timeController.CurrentSegment;
            for (int i = 0; i < availability.ScheduleWindows.Length; i++)
            {
                if (MatchesScheduleWindow(availability.ScheduleWindows[i], segment))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesScheduleWindow(string scheduleWindowId, CampusTimeSegment segment)
        {
            if (string.Equals(scheduleWindowId, "Always", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(scheduleWindowId, "ClassSession", System.StringComparison.OrdinalIgnoreCase))
            {
                return CampusNpcScheduleFacts.IsClassSession(segment);
            }

            if (string.Equals(scheduleWindowId, "Break", System.StringComparison.OrdinalIgnoreCase))
            {
                return CampusNpcScheduleFacts.IsBreak(segment);
            }

            if (string.Equals(scheduleWindowId, "MealPeak", System.StringComparison.OrdinalIgnoreCase))
            {
                return CampusNpcScheduleFacts.IsMealPeak(segment);
            }

            if (string.Equals(scheduleWindowId, "StudentFreeMovement", System.StringComparison.OrdinalIgnoreCase))
            {
                return CampusNpcScheduleFacts.IsStudentFreeMovementWindow(segment);
            }

            if (string.Equals(scheduleWindowId, "DormWindow", System.StringComparison.OrdinalIgnoreCase))
            {
                return CampusNpcScheduleFacts.IsDormWindow(segment);
            }

            if (string.Equals(scheduleWindowId, "TeacherOfficeWindow", System.StringComparison.OrdinalIgnoreCase))
            {
                return CampusNpcScheduleFacts.IsTeacherOfficeWindow(segment);
            }

            if (string.Equals(scheduleWindowId, "StaffOffDuty", System.StringComparison.OrdinalIgnoreCase))
            {
                return CampusNpcScheduleFacts.IsStaffOffDuty(segment);
            }

            return false;
        }
    }
}
