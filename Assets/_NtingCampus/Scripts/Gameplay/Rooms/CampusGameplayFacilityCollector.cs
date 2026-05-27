using System;
using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Rooms
{
    internal static class CampusGameplayFacilityCollector
    {
        public static void AssignFacilities(
            CampusMapRoot mapRoot,
            CampusRuntimeGameplayOverlayLoader overlayLoader,
            Func<int, UnityEngine.Vector3Int, CampusGameplayRoom> findRoomByCell)
        {
            if (findRoomByCell == null)
            {
                return;
            }

            HashSet<string> registeredFacilityIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<CampusPlacedObject> handledPlacedObjects = new HashSet<CampusPlacedObject>();

            AssignExplicitFacilities(mapRoot, overlayLoader, findRoomByCell, registeredFacilityIds, handledPlacedObjects);
            AssignPlacedObjectFacilities(mapRoot, findRoomByCell, registeredFacilityIds, handledPlacedObjects);
        }

        private static void AssignExplicitFacilities(
            CampusMapRoot mapRoot,
            CampusRuntimeGameplayOverlayLoader overlayLoader,
            Func<int, UnityEngine.Vector3Int, CampusGameplayRoom> findRoomByCell,
            HashSet<string> registeredFacilityIds,
            HashSet<CampusPlacedObject> handledPlacedObjects)
        {
            CampusGameplayFacilityMarker[] explicitFacilities =
                UnityEngine.Object.FindObjectsByType<CampusGameplayFacilityMarker>(
                    UnityEngine.FindObjectsInactive.Include,
                    UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < explicitFacilities.Length; i++)
            {
                CampusGameplayFacilityMarker facilityMarker = explicitFacilities[i];
                if (facilityMarker == null ||
                    (overlayLoader != null && !overlayLoader.ShouldIncludeExplicitMarker(facilityMarker)))
                {
                    continue;
                }

                CampusGameplayRoom room = findRoomByCell(facilityMarker.FloorIndex, facilityMarker.Cell);
                if (room == null)
                {
                    continue;
                }

                string facilityId = CampusGameplayFacilityMarker.NormalizeFacilityId(facilityMarker.FacilityId);
                if (string.IsNullOrEmpty(facilityId))
                {
                    facilityId = CampusGameplayFacilityMarker.BuildStableFacilityId(
                        facilityMarker.FloorIndex,
                        facilityMarker.FacilityType,
                        facilityMarker.Cell);
                }

                if (!registeredFacilityIds.Add(facilityId))
                {
                    continue;
                }

                if (facilityMarker.LinkedPlacedObject != null)
                {
                    handledPlacedObjects.Add(facilityMarker.LinkedPlacedObject);
                    room.AddLinkedFacility(
                        facilityMarker.LinkedPlacedObject,
                        CampusFacilityTypeResolution.ExplicitMarker(facilityMarker.FacilityType),
                        facilityId,
                        facilityMarker.DisplayName,
                        facilityMarker.LocalizedDisplayName,
                        facilityMarker.Cell);
                    continue;
                }

                if (TryResolvePlacedObject(mapRoot, facilityMarker, out CampusPlacedObject placedObject))
                {
                    handledPlacedObjects.Add(placedObject);
                    room.AddLinkedFacility(
                        placedObject,
                        CampusFacilityTypeResolution.ExplicitMarker(facilityMarker.FacilityType),
                        facilityId,
                        facilityMarker.DisplayName,
                        facilityMarker.LocalizedDisplayName,
                        facilityMarker.Cell);
                    continue;
                }

                room.AddExplicitFacility(
                    facilityId,
                    facilityMarker.DisplayName,
                    facilityMarker.LocalizedDisplayName,
                    facilityMarker.FacilityType,
                    facilityMarker.Cell);
            }
        }

        private static void AssignPlacedObjectFacilities(
            CampusMapRoot mapRoot,
            Func<int, UnityEngine.Vector3Int, CampusGameplayRoom> findRoomByCell,
            HashSet<string> registeredFacilityIds,
            HashSet<CampusPlacedObject> handledPlacedObjects)
        {
            if (mapRoot == null || mapRoot.Floors == null)
            {
                return;
            }

            for (int i = 0; i < mapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = mapRoot.Floors[i];
                if (floor == null || floor.PropsRoot == null)
                {
                    continue;
                }

                CampusPlacedObject[] placedObjects =
                    floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
                for (int objectIndex = 0; objectIndex < placedObjects.Length; objectIndex++)
                {
                    CampusPlacedObject placedObject = placedObjects[objectIndex];
                    if (placedObject == null || handledPlacedObjects.Contains(placedObject))
                    {
                        continue;
                    }

                    CampusGameplayRoom room = findRoomByCell(floor.FloorIndex, placedObject.Cell);
                    if (room == null)
                    {
                        continue;
                    }

                    if (!CampusPlacedObjectConceptResolver.TryResolveFacility(placedObject, out CampusFacilityTypeResolution resolution))
                    {
                        continue;
                    }

                    string facilityId = CampusGameplayFacilityMarker.BuildStableFacilityId(
                        placedObject.FloorIndex,
                        resolution.FacilityType,
                        placedObject.Cell);
                    if (!registeredFacilityIds.Add(facilityId))
                    {
                        continue;
                    }

                    room.AddFacility(placedObject, resolution);
                }
            }
        }

        private static bool TryResolvePlacedObject(
            CampusMapRoot mapRoot,
            CampusGameplayFacilityMarker facilityMarker,
            out CampusPlacedObject placedObject)
        {
            placedObject = null;
            if (mapRoot == null || facilityMarker == null)
            {
                return false;
            }

            CampusFloorRoot floor = mapRoot.GetFloor(facilityMarker.FloorIndex);
            if (floor == null || floor.PropsRoot == null)
            {
                return false;
            }

            CampusPlacedObject[] candidates = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                CampusPlacedObject candidate = candidates[i];
                if (candidate == null ||
                    candidate.FloorIndex != facilityMarker.FloorIndex ||
                    candidate.Cell != facilityMarker.Cell)
                {
                    continue;
                }

                placedObject = candidate;
                return true;
            }

            return false;
        }
    }
}

