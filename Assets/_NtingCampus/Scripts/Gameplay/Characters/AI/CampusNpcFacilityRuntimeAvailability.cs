using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Rooms;

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

            switch (record.FacilityType)
            {
                case CampusFacilityType.ServiceWindow:
                    isAvailable = CampusCanteenServiceWindowAvailability.IsAvailable(record.PlacedObject);
                    return true;
                default:
                    return false;
            }
        }
    }
}
