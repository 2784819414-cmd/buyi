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

            if (string.Equals(
                    station.AvailabilityRuleId,
                    CampusServiceStationPresetCatalog.AvailabilityCanteenOperatorMealPeak,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return CampusCanteenServiceWindowAvailability.IsAvailable(station);
            }

            return true;
        }
    }
}
