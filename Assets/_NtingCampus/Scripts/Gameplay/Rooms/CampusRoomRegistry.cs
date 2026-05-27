using System;
using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    public sealed class CampusRoomRegistry : MonoBehaviour
    {
        [SerializeField] private CampusMapRoot mapRoot;
        [SerializeField] private bool logValidationIssues = true;
        [SerializeField] private List<CampusGameplayRoom> rooms = new List<CampusGameplayRoom>();

        private readonly Dictionary<string, CampusGameplayRoom> roomsById =
            new Dictionary<string, CampusGameplayRoom>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<CampusPlacedObject, CampusGameplayRoom> roomsByPlacedObject =
            new Dictionary<CampusPlacedObject, CampusGameplayRoom>();

        private readonly Dictionary<int, Dictionary<Vector3Int, RoomCellIndexEntry>> roomsByCellByFloor =
            new Dictionary<int, Dictionary<Vector3Int, RoomCellIndexEntry>>();

        private readonly List<CampusRoomValidator.ValidationIssue> validationIssues =
            new List<CampusRoomValidator.ValidationIssue>();

        public IReadOnlyList<CampusGameplayRoom> Rooms => rooms;
        public IReadOnlyList<CampusRoomValidator.ValidationIssue> ValidationIssues => validationIssues;

        public void RebuildRegistry()
        {
            mapRoot = ResolveMapRoot();
            rooms.Clear();
            roomsById.Clear();
            roomsByPlacedObject.Clear();
            roomsByCellByFloor.Clear();
            validationIssues.Clear();

            if (mapRoot == null)
            {
                validationIssues.Add(new CampusRoomValidator.ValidationIssue(
                    CampusRoomValidator.Severity.Error,
                    string.Empty,
                    CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.MapRootMissing)));
                MaybeLogValidationIssues();
                return;
            }

            mapRoot.RebuildFloorReferences();
            CampusRuntimeGameplayOverlayLoader overlayLoader = CampusRuntimeGameplayOverlayLoader.Instance;
            rooms.AddRange(CampusGameplayRoomCollector.CollectRooms(mapRoot, overlayLoader));
            IndexRoomsById();
            BuildRoomCellIndex();
            CampusGameplayFacilityCollector.AssignFacilities(mapRoot, overlayLoader, FindRoomByCell);
            IndexRoomsByPlacedObject();
            CampusGameplayServiceStationCollector.AssignServiceStations(
                overlayLoader,
                roomId => TryGetRoom(roomId, out CampusGameplayRoom room) ? room : null);
            validationIssues.AddRange(CampusRoomValidator.Validate(rooms));
            ApplyValidationState();
            MaybeLogValidationIssues();
            LogRegistrationSummary();
        }

        public bool TryGetRoom(string roomId, out CampusGameplayRoom room)
        {
            return roomsById.TryGetValue(CampusGameplayRoomCollector.NormalizeRoomKey(roomId), out room);
        }

        public bool TryFindRoomForPlacedObject(CampusPlacedObject placedObject, out CampusGameplayRoom room)
        {
            room = null;
            return placedObject != null &&
                   roomsByPlacedObject.TryGetValue(placedObject, out room);
        }

        public CampusGameplayRoom FindRoomByCell(int floorIndex, Vector3Int cell)
        {
            if (roomsByCellByFloor.Count == 0 && rooms.Count > 0)
            {
                BuildRoomCellIndex();
            }

            cell.z = 0;
            if (!roomsByCellByFloor.TryGetValue(floorIndex, out Dictionary<Vector3Int, RoomCellIndexEntry> floorCells))
            {
                return null;
            }

            return floorCells.TryGetValue(cell, out RoomCellIndexEntry entry) ? entry.Room : null;
        }

        private CampusMapRoot ResolveMapRoot()
        {
            if (mapRoot != null)
            {
                return mapRoot;
            }

            mapRoot = FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            return mapRoot;
        }

        private void IndexRoomsById()
        {
            roomsById.Clear();
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
                {
                    continue;
                }

                if (!roomsById.ContainsKey(room.RoomId))
                {
                    roomsById.Add(room.RoomId, room);
                }
            }
        }

        private void IndexRoomsByPlacedObject()
        {
            roomsByPlacedObject.Clear();
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null || room.Facilities == null)
                {
                    continue;
                }

                for (int facilityIndex = 0; facilityIndex < room.Facilities.Count; facilityIndex++)
                {
                    CampusGameplayRoom.FacilityRecord facility = room.Facilities[facilityIndex];
                    CampusPlacedObject placedObject = facility != null ? facility.PlacedObject : null;
                    if (placedObject != null && !roomsByPlacedObject.ContainsKey(placedObject))
                    {
                        roomsByPlacedObject.Add(placedObject, room);
                    }
                }
            }
        }

        private void BuildRoomCellIndex()
        {
            roomsByCellByFloor.Clear();
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                BoundsInt bounds = room.MarkerBounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0)
                {
                    continue;
                }

                int area = Mathf.Max(1, bounds.size.x * bounds.size.y);
                Dictionary<Vector3Int, RoomCellIndexEntry> floorCells = GetOrCreateFloorCellIndex(room.FloorIndex);
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    for (int x = bounds.xMin; x < bounds.xMax; x++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (!floorCells.TryGetValue(cell, out RoomCellIndexEntry existing) || area < existing.Area)
                        {
                            floorCells[cell] = new RoomCellIndexEntry(room, area);
                        }
                    }
                }
            }
        }

        private Dictionary<Vector3Int, RoomCellIndexEntry> GetOrCreateFloorCellIndex(int floorIndex)
        {
            if (!roomsByCellByFloor.TryGetValue(floorIndex, out Dictionary<Vector3Int, RoomCellIndexEntry> floorCells))
            {
                floorCells = new Dictionary<Vector3Int, RoomCellIndexEntry>();
                roomsByCellByFloor[floorIndex] = floorCells;
            }

            return floorCells;
        }

        private void ApplyValidationState()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                CampusRoomValidator.ValidationSummary summary = CampusRoomValidator.Summarize(room);
                room.ApplyValidationState(summary.IsValid, summary.IsUsableForGameplay, summary.Message);
            }
        }

        private void MaybeLogValidationIssues()
        {
            if (logValidationIssues)
            {
                CampusRoomValidator.LogIssues(validationIssues);
            }
        }

        private void LogRegistrationSummary()
        {
            Debug.Log(CampusRoomValidationTextCatalog.Format(
                CampusRoomValidationTextId.RegistrationSummary,
                rooms.Count));
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                Debug.Log(CampusRoomValidationTextCatalog.Format(
                    CampusRoomValidationTextId.RoomRegistrationSummary,
                    room.RoomId,
                    room.RoomType,
                    room.MarkerCount,
                    room.Facilities.Count,
                    room.IsValid,
                    room.IsUsableForGameplay,
                    room.ValidationSummary));
            }
        }

        private readonly struct RoomCellIndexEntry
        {
            public RoomCellIndexEntry(CampusGameplayRoom room, int area)
            {
                Room = room;
                Area = area;
            }

            public CampusGameplayRoom Room { get; }
            public int Area { get; }
        }
    }
}


