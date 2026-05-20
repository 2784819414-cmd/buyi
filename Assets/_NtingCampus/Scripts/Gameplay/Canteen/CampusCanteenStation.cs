using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    public sealed class CampusCanteenStation
    {
        public string StationId;
        public string RoomId;
        public string DisplayName;
        public string WindowTypeId = "generic";
        public int WindowIndex;
        public int FloorIndex = 1;
        public string WindowFacilityId;
        public string CounterFacilityId;
        public string ClerkStandFacilityId;
        public string CustomerPickupFacilityId;
        public string FoodBoxFacilityId;
        public Vector3 WindowPosition;
        public Vector3 CounterPosition;
        public Vector3 CustomerPosition;
        public Vector3 ClerkPosition;
        public Vector3 MealDropPosition;
        public Vector3 FoodBoxPosition;
        public Vector3 StockPosition;
        public CampusPlacedObject WindowObject;
        public CampusPlacedObject CounterObject;
        public CampusPlacedObject FoodBoxObject;
        public CampusPlacedObject StockObject;
        public string CounterContainerId;
        public string FoodBoxContainerId;
        public string StockContainerId;
        public bool HasFoodBox;

        public bool HasServingWindow => !string.IsNullOrWhiteSpace(StationId);
        public bool HasCounter => HasServingWindow;

        public bool MatchesWorkstationId(string value)
        {
            string key = NormalizeId(value);
            return !string.IsNullOrEmpty(key) &&
                   (SameId(key, StationId) ||
                    SameId(key, WindowFacilityId) ||
                    SameId(key, ClerkStandFacilityId) ||
                    SameId(key, CounterFacilityId) ||
                    SameId(key, CustomerPickupFacilityId) ||
                    SameId(key, FoodBoxFacilityId));
        }

        public static string BuildContainerId(string stationId, string suffix)
        {
            string id = string.IsNullOrWhiteSpace(stationId) ? "canteen.station" : stationId.Trim();
            return id + "." + suffix;
        }

        private static bool SameId(string left, string right)
        {
            return string.Equals(NormalizeId(left), NormalizeId(right), System.StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
