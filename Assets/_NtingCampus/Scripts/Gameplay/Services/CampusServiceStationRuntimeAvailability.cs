using NtingCampus.Gameplay.Canteen;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Services
{
    internal static class CampusServiceStationRuntimeAvailability
    {
        public static bool CanServeNow(CampusServiceStation station)
        {
            if (!station.HasInteractionFacility)
            {
                return false;
            }

            if (CampusInteractionActionIds.Equals(
                    station.InteractionActionId,
                    CampusInteractionActionIds.ServiceWindowUse))
            {
                return CampusCanteenServiceWindowAvailability.IsAvailable(station);
            }

            return true;
        }
    }
}
