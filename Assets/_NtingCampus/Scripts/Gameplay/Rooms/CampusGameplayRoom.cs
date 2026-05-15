using System;
using System.Collections.Generic;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    [Serializable]
    public sealed class CampusGameplayRoom
    {
        [Serializable]
        public sealed class FacilityRecord
        {
            [SerializeField] private string facilityId = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private CampusFacilityType facilityType;
            [SerializeField] private Vector3Int cell;
            [SerializeField] private CampusPlacedObject placedObject;

            public string FacilityId => facilityId;
            public string DisplayName => displayName;
            public CampusFacilityType FacilityType => facilityType;
            public Vector3Int Cell => cell;
            public CampusPlacedObject PlacedObject => placedObject;

            internal void Bind(CampusPlacedObject source, CampusFacilityType type)
            {
                placedObject = source;
                facilityType = type;
                facilityId = source != null && !string.IsNullOrWhiteSpace(source.ObjectId)
                    ? source.ObjectId.Trim()
                    : string.Empty;
                displayName = source != null ? source.DisplayName : string.Empty;
                cell = source != null ? source.Cell : default;
            }

            internal void BindExplicit(string explicitDisplayName, CampusFacilityType type, Vector3Int targetCell)
            {
                placedObject = null;
                facilityType = type;
                facilityId = string.Empty;
                displayName = string.IsNullOrWhiteSpace(explicitDisplayName) ? type.ToString() : explicitDisplayName.Trim();
                cell = targetCell;
            }
        }

        [SerializeField] private string roomId = string.Empty;
        [SerializeField] private string sourceRoomName = string.Empty;
        [SerializeField] private CampusRoomType roomType;
        [SerializeField] private int floorIndex = 1;
        [SerializeField] private BoundsInt markerBounds;
        [SerializeField] private Vector3 worldCenter;
        [SerializeField] private int markerCount;
        [SerializeField] private bool hasExplicitGameplayMarker;
        [SerializeField] private bool isValid;
        [SerializeField] private bool isUsableForGameplay;
        [SerializeField] private string validationSummary = string.Empty;
        [SerializeField] private List<CampusRuntimeRoomMarker> markers = new List<CampusRuntimeRoomMarker>();
        [SerializeField] private List<CampusGameplayRoomMarker> gameplayMarkers = new List<CampusGameplayRoomMarker>();
        [SerializeField] private List<FacilityRecord> facilities = new List<FacilityRecord>();

        public string RoomId => roomId;
        public string SourceRoomName => sourceRoomName;
        public CampusRoomType RoomType => roomType;
        public int FloorIndex => floorIndex;
        public BoundsInt MarkerBounds => markerBounds;
        public Vector3 WorldCenter => worldCenter;
        public int MarkerCount => markerCount;
        public bool HasExplicitGameplayMarker => hasExplicitGameplayMarker;
        public bool IsValid => isValid;
        public bool IsUsableForGameplay => isUsableForGameplay;
        public string ValidationSummary => validationSummary;
        public IReadOnlyList<CampusRuntimeRoomMarker> Markers => markers;
        public IReadOnlyList<CampusGameplayRoomMarker> GameplayMarkers => gameplayMarkers;
        public IReadOnlyList<FacilityRecord> Facilities => facilities;

        internal void Bind(
            string id,
            string roomName,
            CampusRoomType type,
            int targetFloorIndex,
            BoundsInt bounds,
            Vector3 center,
            List<CampusRuntimeRoomMarker> sourceMarkers)
        {
            roomId = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            sourceRoomName = string.IsNullOrWhiteSpace(roomName) ? string.Empty : roomName.Trim();
            roomType = type;
            floorIndex = Mathf.Max(1, targetFloorIndex);
            markerBounds = bounds;
            worldCenter = center;
            hasExplicitGameplayMarker = false;
            isValid = false;
            isUsableForGameplay = false;
            validationSummary = string.Empty;
            markers = sourceMarkers ?? new List<CampusRuntimeRoomMarker>();
            gameplayMarkers = gameplayMarkers ?? new List<CampusGameplayRoomMarker>();
            gameplayMarkers.Clear();
            markerCount = markers.Count;
            facilities = facilities ?? new List<FacilityRecord>();
            facilities.Clear();
        }

        internal void BindFromGameplayMarker(
            string id,
            string roomName,
            CampusRoomType type,
            int targetFloorIndex,
            BoundsInt bounds,
            Vector3 center,
            CampusGameplayRoomMarker gameplayRoomMarker)
        {
            Bind(id, roomName, type, targetFloorIndex, bounds, center, new List<CampusRuntimeRoomMarker>());
            hasExplicitGameplayMarker = gameplayRoomMarker != null;
            gameplayMarkers = gameplayMarkers ?? new List<CampusGameplayRoomMarker>();
            gameplayMarkers.Clear();
            if (gameplayRoomMarker != null)
            {
                gameplayMarkers.Add(gameplayRoomMarker);
            }

            markerCount = Mathf.Max(1, bounds.size.x * bounds.size.y);
        }

        internal void AddFacility(CampusPlacedObject placedObject, CampusFacilityType type)
        {
            if (placedObject == null)
            {
                return;
            }

            FacilityRecord record = new FacilityRecord();
            record.Bind(placedObject, type);
            facilities.Add(record);
        }

        internal void AddExplicitFacility(string displayName, CampusFacilityType type, Vector3Int targetCell)
        {
            FacilityRecord record = new FacilityRecord();
            record.BindExplicit(displayName, type, targetCell);
            facilities.Add(record);
        }

        public bool ContainsCell(Vector3Int cell)
        {
            return markerBounds.size.x > 0 &&
                   markerBounds.size.y > 0 &&
                   markerBounds.Contains(cell);
        }

        public int GetFacilityCount(CampusFacilityType facilityType)
        {
            int count = 0;
            for (int i = 0; i < facilities.Count; i++)
            {
                if (facilities[i] != null && facilities[i].FacilityType == facilityType)
                {
                    count++;
                }
            }

            return count;
        }

        internal void ApplyValidationState(bool valid, bool usableForGameplay, string summary)
        {
            isValid = valid;
            isUsableForGameplay = usableForGameplay;
            validationSummary = string.IsNullOrWhiteSpace(summary) ? string.Empty : summary.Trim();
        }
    }
}
