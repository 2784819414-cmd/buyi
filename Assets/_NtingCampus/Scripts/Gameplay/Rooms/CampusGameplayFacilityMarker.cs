using NtingCampusMapEditor;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    public sealed class CampusGameplayFacilityMarker : MonoBehaviour
    {
        [SerializeField] private string facilityId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CampusLocalizedText localizedDisplayName = default;
        [SerializeField] private CampusFacilityType facilityType = CampusFacilityType.Unknown;
        [SerializeField, Min(1)] private int floorIndex = 1;
        [SerializeField] private Vector3Int cell;
        [SerializeField] private bool countsAsCoreFacility = true;
        [SerializeField] private string ownerFacilityId = string.Empty;
        [SerializeField] private CampusPlacedObject linkedPlacedObject;

        public string FacilityId => facilityId;
        public string DisplayName => displayName;
        public CampusLocalizedText LocalizedDisplayName => localizedDisplayName;
        public CampusFacilityType FacilityType => facilityType;
        public int FloorIndex => floorIndex;
        public Vector3Int Cell => cell;
        public bool CountsAsCoreFacility => countsAsCoreFacility;
        public string OwnerFacilityId => ownerFacilityId;
        public CampusPlacedObject LinkedPlacedObject => linkedPlacedObject;

        public void Configure(
            string targetDisplayName,
            CampusFacilityType type,
            int targetFloorIndex,
            Vector3Int targetCell,
            bool coreFacility,
            CampusPlacedObject placedObject)
        {
            Configure(
                string.Empty,
                targetDisplayName,
                default,
                type,
                targetFloorIndex,
                targetCell,
                coreFacility,
                placedObject);
        }

        public void Configure(
            string targetFacilityId,
            string targetDisplayName,
            CampusFacilityType type,
            int targetFloorIndex,
            Vector3Int targetCell,
            bool coreFacility,
            CampusPlacedObject placedObject)
        {
            Configure(
                targetFacilityId,
                targetDisplayName,
                string.IsNullOrWhiteSpace(targetDisplayName)
                    ? default
                    : new CampusLocalizedText(targetDisplayName.Trim(), string.Empty),
                type,
                targetFloorIndex,
                targetCell,
                coreFacility,
                placedObject);
        }

        public void Configure(
            string targetFacilityId,
            string targetDisplayName,
            CampusFacilityType type,
            int targetFloorIndex,
            Vector3Int targetCell,
            bool coreFacility,
            string targetOwnerFacilityId,
            CampusPlacedObject placedObject)
        {
            Configure(
                targetFacilityId,
                targetDisplayName,
                string.IsNullOrWhiteSpace(targetDisplayName)
                    ? default
                    : new CampusLocalizedText(targetDisplayName.Trim(), string.Empty),
                type,
                targetFloorIndex,
                targetCell,
                coreFacility,
                targetOwnerFacilityId,
                placedObject);
        }

        public void Configure(
            string targetFacilityId,
            string targetDisplayName,
            CampusLocalizedText targetLocalizedDisplayName,
            CampusFacilityType type,
            int targetFloorIndex,
            Vector3Int targetCell,
            bool coreFacility,
            CampusPlacedObject placedObject)
        {
            Configure(
                targetFacilityId,
                targetDisplayName,
                targetLocalizedDisplayName,
                type,
                targetFloorIndex,
                targetCell,
                coreFacility,
                string.Empty,
                placedObject);
        }

        public void Configure(
            string targetFacilityId,
            string targetDisplayName,
            CampusLocalizedText targetLocalizedDisplayName,
            CampusFacilityType type,
            int targetFloorIndex,
            Vector3Int targetCell,
            bool coreFacility,
            string targetOwnerFacilityId,
            CampusPlacedObject placedObject)
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? string.Empty : targetDisplayName.Trim();
            localizedDisplayName = targetLocalizedDisplayName;
            facilityType = type;
            floorIndex = Mathf.Max(1, targetFloorIndex);
            cell = targetCell;
            countsAsCoreFacility = coreFacility;
            ownerFacilityId = NormalizeFacilityId(targetOwnerFacilityId);
            linkedPlacedObject = placedObject;
            facilityId = NormalizeFacilityId(targetFacilityId);
            if (string.IsNullOrEmpty(facilityId))
            {
                facilityId = BuildStableFacilityId(floorIndex, facilityType, cell);
            }
        }

        public static string BuildStableFacilityId(int floorIndex, CampusFacilityType type, Vector3Int targetCell)
        {
            return "facility_f" +
                   Mathf.Max(1, floorIndex) +
                   "_" +
                   type +
                   "_" +
                   targetCell.x +
                   "_" +
                   targetCell.y;
        }

        public static string NormalizeFacilityId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
