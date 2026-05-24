using System;
using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public enum CampusRoomTypeSource
    {
        Unknown = 0,
        ExplicitGameplayMarker = 1,
        LegacyNameInference = 2
    }

    [Serializable]
    public sealed class CampusGameplayRoom
    {
        [Serializable]
        public sealed class FacilityRecord
        {
            [SerializeField] private string facilityId = string.Empty;
            [SerializeField] private string ownerFacilityId = string.Empty;
            [SerializeField] private string legacyServiceStationId = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private CampusFacilityType facilityType;
            [SerializeField] private CampusFacilityTypeSource facilityTypeSource;
            [SerializeField] private string facilityTypeDiagnostic = string.Empty;
            [SerializeField] private Vector3Int cell;
            [SerializeField] private CampusPlacedObject placedObject;

            public string FacilityId => facilityId;
            public string OwnerFacilityId => ownerFacilityId;
            public string LegacyServiceStationId => legacyServiceStationId;
            public string DisplayName => displayName;
            public CampusFacilityType FacilityType => facilityType;
            public CampusFacilityTypeSource FacilityTypeSource => facilityTypeSource;
            public string FacilityTypeDiagnostic => facilityTypeDiagnostic;
            public bool HasExplicitFacilityType =>
                facilityTypeSource == CampusFacilityTypeSource.ExplicitTypeId ||
                facilityTypeSource == CampusFacilityTypeSource.ExplicitMarker;
            public Vector3Int Cell => cell;
            public CampusPlacedObject PlacedObject => placedObject;

            internal void Bind(
                CampusPlacedObject source,
                CampusFacilityType type,
                string explicitFacilityId,
                string explicitOwnerFacilityId,
                string explicitLegacyServiceStationId)
            {
                Bind(
                    source,
                    new CampusFacilityTypeResolution(type, CampusFacilityTypeSource.Unknown, string.Empty),
                    explicitFacilityId,
                    explicitOwnerFacilityId,
                    explicitLegacyServiceStationId);
            }

            internal void Bind(
                CampusPlacedObject source,
                CampusFacilityTypeResolution resolution,
                string explicitFacilityId,
                string explicitOwnerFacilityId,
                string explicitLegacyServiceStationId)
            {
                string resolvedDisplayName = source != null ? source.DisplayName : string.Empty;
                Vector3Int resolvedCell = source != null ? source.Cell : default;
                Bind(
                    source,
                    resolution,
                    explicitFacilityId,
                    explicitOwnerFacilityId,
                    explicitLegacyServiceStationId,
                    resolvedDisplayName,
                    resolvedCell);
            }

            internal void Bind(
                CampusPlacedObject source,
                CampusFacilityTypeResolution resolution,
                string explicitFacilityId,
                string explicitOwnerFacilityId,
                string explicitLegacyServiceStationId,
                string explicitDisplayName,
                Vector3Int targetCell)
            {
                placedObject = source;
                facilityType = resolution.FacilityType;
                facilityTypeSource = resolution.Source;
                facilityTypeDiagnostic = resolution.Diagnostic;
                facilityId = CampusGameplayFacilityMarker.NormalizeFacilityId(explicitFacilityId);
                ownerFacilityId = CampusGameplayFacilityMarker.NormalizeOwnerFacilityId(explicitOwnerFacilityId);
                legacyServiceStationId =
                    CampusGameplayFacilityMarker.NormalizeLegacyServiceStationId(explicitLegacyServiceStationId);
                if (string.IsNullOrEmpty(facilityId) && source != null)
                {
                    facilityId = CampusGameplayFacilityMarker.BuildStableFacilityId(
                        source.FloorIndex,
                        facilityType,
                        targetCell);
                }

                displayName = string.IsNullOrWhiteSpace(explicitDisplayName)
                    ? source != null ? source.DisplayName : string.Empty
                    : explicitDisplayName.Trim();
                cell = targetCell;
            }

            internal void BindExplicit(
                string explicitFacilityId,
                string explicitOwnerFacilityId,
                string explicitLegacyServiceStationId,
                string explicitDisplayName,
                CampusFacilityType type,
                int targetFloorIndex,
                Vector3Int targetCell,
                CampusFacilityTypeSource source)
            {
                placedObject = null;
                facilityType = type;
                facilityTypeSource = source;
                facilityTypeDiagnostic = string.Empty;
                facilityId = CampusGameplayFacilityMarker.NormalizeFacilityId(explicitFacilityId);
                ownerFacilityId = CampusGameplayFacilityMarker.NormalizeOwnerFacilityId(explicitOwnerFacilityId);
                legacyServiceStationId =
                    CampusGameplayFacilityMarker.NormalizeLegacyServiceStationId(explicitLegacyServiceStationId);
                if (string.IsNullOrEmpty(facilityId))
                {
                    facilityId = CampusGameplayFacilityMarker.BuildStableFacilityId(targetFloorIndex, type, targetCell);
                }

                displayName = string.IsNullOrWhiteSpace(explicitDisplayName) ? type.ToString() : explicitDisplayName.Trim();
                cell = targetCell;
            }
        }

        [SerializeField] private string roomId = string.Empty;
        [SerializeField] private string sourceRoomName = string.Empty;
        [SerializeField] private CampusLocalizedText localizedDisplayName = default;
        [SerializeField] private CampusRoomType roomType;
        [SerializeField] private CampusRoomTypeSource roomTypeSource;
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
        public CampusLocalizedText LocalizedDisplayName => localizedDisplayName;
        public CampusRoomType RoomType => roomType;
        public CampusRoomTypeSource RoomTypeSource => roomTypeSource;
        public int FloorIndex => floorIndex;
        public BoundsInt MarkerBounds => markerBounds;
        public Vector3 WorldCenter => worldCenter;
        public int MarkerCount => markerCount;
        public bool HasExplicitGameplayMarker => hasExplicitGameplayMarker;
        public bool HasExplicitRoomType => roomTypeSource == CampusRoomTypeSource.ExplicitGameplayMarker;
        public bool IsValid => isValid;
        public bool IsUsableForGameplay => isUsableForGameplay;
        public string ValidationSummary => validationSummary;
        public IReadOnlyList<CampusRuntimeRoomMarker> Markers => markers;
        public IReadOnlyList<CampusGameplayRoomMarker> GameplayMarkers => gameplayMarkers;
        public IReadOnlyList<FacilityRecord> Facilities => facilities;
        public bool HasDisplayName => localizedDisplayName.HasAnyText || !string.IsNullOrWhiteSpace(sourceRoomName);

        public string GetDisplayName(CampusDisplayLanguage language)
        {
            if (localizedDisplayName.HasAnyText)
            {
                return localizedDisplayName.Get(language, ResolveFallbackDisplayName(language));
            }

            return ResolveFallbackDisplayName(language);
        }

        public string GetPrimaryDisplayName()
        {
            if (localizedDisplayName.HasAnyText)
            {
                return localizedDisplayName.ResolvePrimary(sourceRoomName, ResolveCatalogPrimaryDisplayName());
            }

            string fallback = ResolvePrimaryFallbackDisplayName();
            return string.IsNullOrWhiteSpace(fallback)
                ? CampusRoomTextCatalog.GetLocalizedText(CampusRoomType.Unknown).ResolvePrimary()
                : fallback;
        }

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
            localizedDisplayName = type == CampusRoomType.Unknown
                ? default
                : CampusRoomTextCatalog.GetLocalizedText(type);
            roomTypeSource = type == CampusRoomType.Unknown
                ? CampusRoomTypeSource.Unknown
                : CampusRoomTypeSource.LegacyNameInference;
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
                localizedDisplayName = gameplayRoomMarker.LocalizedDisplayName;
            }

            markerCount = Mathf.Max(1, bounds.size.x * bounds.size.y);
            roomTypeSource = CampusRoomTypeSource.ExplicitGameplayMarker;
        }

        internal void AddFacility(
            CampusPlacedObject placedObject,
            CampusFacilityType type,
            string explicitFacilityId = "",
            string explicitOwnerFacilityId = "",
            string explicitLegacyServiceStationId = "")
        {
            AddFacility(
                placedObject,
                new CampusFacilityTypeResolution(type, CampusFacilityTypeSource.Unknown, string.Empty),
                explicitFacilityId,
                explicitOwnerFacilityId,
                explicitLegacyServiceStationId);
        }

        internal void AddFacility(
            CampusPlacedObject placedObject,
            CampusFacilityTypeResolution resolution,
            string explicitFacilityId = "",
            string explicitOwnerFacilityId = "",
            string explicitLegacyServiceStationId = "")
        {
            if (placedObject == null)
            {
                return;
            }

            FacilityRecord record = new FacilityRecord();
            record.Bind(
                placedObject,
                resolution,
                explicitFacilityId,
                explicitOwnerFacilityId,
                explicitLegacyServiceStationId);
            facilities.Add(record);
        }

        internal void AddLinkedFacility(
            CampusPlacedObject placedObject,
            CampusFacilityTypeResolution resolution,
            string facilityId,
            string ownerFacilityId,
            string legacyServiceStationId,
            string displayName,
            Vector3Int targetCell)
        {
            if (placedObject == null)
            {
                return;
            }

            FacilityRecord record = new FacilityRecord();
            record.Bind(
                placedObject,
                resolution,
                facilityId,
                ownerFacilityId,
                legacyServiceStationId,
                displayName,
                targetCell);
            facilities.Add(record);
        }

        internal void AddExplicitFacility(
            string facilityId,
            string ownerFacilityId,
            string legacyServiceStationId,
            string displayName,
            CampusFacilityType type,
            Vector3Int targetCell)
        {
            FacilityRecord record = new FacilityRecord();
            record.BindExplicit(
                facilityId,
                ownerFacilityId,
                legacyServiceStationId,
                displayName,
                type,
                floorIndex,
                targetCell,
                CampusFacilityTypeSource.ExplicitMarker);
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

        private string ResolveFallbackDisplayName(CampusDisplayLanguage language)
        {
            if (roomTypeSource == CampusRoomTypeSource.ExplicitGameplayMarker &&
                !string.IsNullOrWhiteSpace(sourceRoomName))
            {
                return sourceRoomName.Trim();
            }

            if (roomType != CampusRoomType.Unknown)
            {
                return CampusRoomTextCatalog.Get(language, roomType);
            }

            if (!string.IsNullOrWhiteSpace(sourceRoomName))
            {
                return sourceRoomName.Trim();
            }

            return CampusRoomTextCatalog.Get(language, CampusRoomType.Unknown);
        }

        private string ResolveCatalogPrimaryDisplayName()
        {
            return CampusRoomTextCatalog.GetLocalizedText(roomType).ResolvePrimary();
        }

        private string ResolvePrimaryFallbackDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(sourceRoomName) &&
                roomTypeSource == CampusRoomTypeSource.ExplicitGameplayMarker)
            {
                return sourceRoomName.Trim();
            }

            if (roomType != CampusRoomType.Unknown)
            {
                return ResolveCatalogPrimaryDisplayName();
            }

            return string.IsNullOrWhiteSpace(sourceRoomName) ? string.Empty : sourceRoomName.Trim();
        }
    }
}
