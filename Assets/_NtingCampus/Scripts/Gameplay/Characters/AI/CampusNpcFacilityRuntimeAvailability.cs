using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcFacilityRuntimeAvailability
    {
        public static bool CanUseAsNpcTarget(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record)
        {
            if (room == null || record == null || record.PlacedObject == null)
            {
                return false;
            }

            return TryEvaluateRule(record, out bool isAvailable)
                ? isAvailable
                : true;
        }

        private static bool TryEvaluateRule(
            CampusGameplayRoom.FacilityRecord record,
            out bool isAvailable)
        {
            isAvailable = false;
            if (record == null)
            {
                return false;
            }

            if (record.FacilityType != CampusFacilityType.ServiceWindow)
            {
                return false;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            if (worldService == null ||
                !worldService.ServiceStations.TryResolveByPlacedObject(
                    record.PlacedObject,
                    out CampusServiceStation station))
            {
                return false;
            }

            isAvailable = CampusServiceStationRuntimeAvailability.CanServeNow(
                station,
                worldService,
                bootstrap != null ? bootstrap.RosterService : null,
                bootstrap != null ? bootstrap.TimeController : null);
            return true;
        }
    }
}
