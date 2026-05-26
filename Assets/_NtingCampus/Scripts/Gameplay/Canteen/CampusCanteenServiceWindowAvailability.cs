using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Canteen
{
    internal static class CampusCanteenServiceWindowAvailability
    {
        public static bool IsAvailable(CampusPlacedObject window)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            return worldService != null &&
                   worldService.ServiceStations.TryResolveByPlacedObject(
                       worldService,
                       window,
                       out CampusServiceStation station) &&
                   CampusServiceStationRuntimeAvailability.CanServeNow(
                       station,
                       worldService,
                       bootstrap != null ? bootstrap.RosterService : null,
                       bootstrap != null ? bootstrap.TimeController : null);
        }

        public static bool IsAvailable(CampusServiceStation station)
        {
            return CampusServiceStationRuntimeAvailability.CanServeNow(station);
        }

        public static bool TryResolveAssignedOperator(
            CampusServiceStation station,
            out CampusCharacterRuntime runtime)
        {
            runtime = null;
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            return worldService != null &&
                   worldService.ServiceStations.TryResolvePresentOperator(
                       station,
                       bootstrap != null ? bootstrap.RosterService : null,
                       out runtime);
        }
    }
}
