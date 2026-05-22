using System;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private const string PresetFileName = "NpcEcologyPresets.json";
        private const float TargetRetryHoldSeconds = 5f;

        private static EcologyPresetData cachedData;

        public static bool EnableSelectionDebug => Data.EnableSelectionDebug;

        public static CampusFacilityType[] GetFacilityGroup(string groupId)
        {
            string normalizedId = NormalizeId(groupId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return Array.Empty<CampusFacilityType>();
            }

            return Data.FacilityGroups.TryGetValue(normalizedId, out CampusFacilityType[] facilityTypes)
                ? facilityTypes
                : Array.Empty<CampusFacilityType>();
        }

        private static EcologyPresetData Data
        {
            get
            {
                if (cachedData == null)
                {
                    cachedData = LoadData();
                }

                return cachedData;
            }
        }
    }
}
