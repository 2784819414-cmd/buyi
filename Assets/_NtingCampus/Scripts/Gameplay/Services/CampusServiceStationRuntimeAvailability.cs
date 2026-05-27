using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

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

        public static bool TryRequireActionSourceAvailable(
            string actionId,
            Object source,
            out string unavailableMessage)
        {
            unavailableMessage = string.Empty;
            if (!TryResolveActionStation(actionId, source, out CampusServiceStation station))
            {
                unavailableMessage = CampusServiceStationRuntimeTextCatalog.Get(
                    CampusServiceStationRuntimeTextId.StationMissing);
                return false;
            }

            if (CanServeNow(station))
            {
                return true;
            }

            unavailableMessage = ResolveUnavailableMessage(station);
            return false;
        }

        public static bool TryResolveActionStation(
            string actionId,
            Object source,
            out CampusServiceStation station)
        {
            station = default;
            if (string.IsNullOrWhiteSpace(actionId) ||
                !TryResolvePlacedObject(source, out CampusPlacedObject placedObject))
            {
                return false;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            return worldService != null &&
                   worldService.ServiceStations.TryResolveByPlacedObject(
                       placedObject,
                       actionId,
                       out station) &&
                   CampusInteractionActionIds.Equals(station.InteractionActionId, actionId);
        }

        public static string ResolveUnavailableMessage(CampusServiceStation station)
        {
            var text = station.Availability != null
                ? station.Availability.UnavailableText
                : default;
            return text.HasAnyText
                ? text.Current(CampusServiceStationRuntimeTextCatalog.Get(
                    CampusServiceStationRuntimeTextId.StationUnavailable))
                : CampusServiceStationRuntimeTextCatalog.Get(
                    CampusServiceStationRuntimeTextId.StationUnavailable);
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

        private static bool TryResolvePlacedObject(
            Object source,
            out CampusPlacedObject placedObject)
        {
            placedObject = null;
            if (source is CampusPlacedObject direct)
            {
                placedObject = direct;
                return true;
            }

            if (source is Component component)
            {
                placedObject = component.GetComponent<CampusPlacedObject>() ??
                               component.GetComponentInParent<CampusPlacedObject>();
                return placedObject != null;
            }

            if (source is GameObject gameObject)
            {
                placedObject = gameObject.GetComponent<CampusPlacedObject>() ??
                               gameObject.GetComponentInParent<CampusPlacedObject>();
                return placedObject != null;
            }

            return false;
        }
    }
}
