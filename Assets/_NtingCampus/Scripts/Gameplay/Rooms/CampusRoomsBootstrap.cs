using System;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public static class CampusRoomsBootstrap
    {
        private const string BootstrapRootName = "NtingCampus_GameplayRoomsRoot";

        private readonly struct RoomSeed
        {
            public readonly string Id;
            public readonly string Name;
            public readonly CampusRoomType Type;
            public readonly int FloorIndex;
            public readonly Vector3Int Cell;
            public readonly Vector2Int Size;
            public readonly bool UsableForGameplay;

            public RoomSeed(string id, string name, CampusRoomType type, int floorIndex, Vector3Int cell, Vector2Int size, bool usableForGameplay)
            {
                Id = id;
                Name = name;
                Type = type;
                FloorIndex = floorIndex;
                Cell = cell;
                Size = size;
                UsableForGameplay = usableForGameplay;
            }
        }

        private readonly struct FacilitySeed
        {
            public readonly string Name;
            public readonly CampusFacilityType Type;
            public readonly int FloorIndex;
            public readonly Vector3Int Cell;
            public readonly bool CountsAsCoreFacility;

            public FacilitySeed(string name, CampusFacilityType type, int floorIndex, Vector3Int cell, bool countsAsCoreFacility)
            {
                Name = name;
                Type = type;
                FloorIndex = floorIndex;
                Cell = cell;
                CountsAsCoreFacility = countsAsCoreFacility;
            }
        }

        private static readonly RoomSeed[] RoomSeeds =
        {
            new RoomSeed("room_classroom_main", "主教室", CampusRoomType.Classroom, 1, new Vector3Int(-4, 0, 0), new Vector2Int(4, 3), true),
            new RoomSeed("room_dormitory_main", "主宿舍", CampusRoomType.Dormitory, 1, new Vector3Int(4, 1, 0), new Vector2Int(3, 3), true),
            new RoomSeed("room_office_main", "教师办公室", CampusRoomType.Office, 1, new Vector3Int(4, -3, 0), new Vector2Int(3, 2), true),
            new RoomSeed("room_common_activity", "公共活动区", CampusRoomType.CommonActivityZone, 1, new Vector3Int(-1, -3, 0), new Vector2Int(3, 2), true),
            new RoomSeed("room_hr_department", "人事部", CampusRoomType.HumanResources, 1, new Vector3Int(0, 1, 0), new Vector2Int(3, 2), true),
            new RoomSeed("room_shrine_room", "神龛室", CampusRoomType.ShrineRoom, 1, new Vector3Int(-7, -3, 0), new Vector2Int(2, 2), true)
        };

        private static readonly FacilitySeed[] FacilitySeeds =
        {
            new FacilitySeed("主黑板", CampusFacilityType.Blackboard, 1, new Vector3Int(-4, 2, 0), true),
            new FacilitySeed("主讲台", CampusFacilityType.Podium, 1, new Vector3Int(-3, 2, 0), true),
            new FacilitySeed("第一排课桌", CampusFacilityType.StudentDesk, 1, new Vector3Int(-4, 1, 0), true),
            new FacilitySeed("第二排课桌", CampusFacilityType.StudentDesk, 1, new Vector3Int(-3, 1, 0), true),
            new FacilitySeed("宿舍床位A", CampusFacilityType.Bed, 1, new Vector3Int(4, 1, 0), true),
            new FacilitySeed("宿舍床位B", CampusFacilityType.Bed, 1, new Vector3Int(5, 1, 0), true),
            new FacilitySeed("办公桌", CampusFacilityType.OfficeDesk, 1, new Vector3Int(4, -3, 0), true),
            new FacilitySeed("公告栏", CampusFacilityType.BulletinBoard, 1, new Vector3Int(-1, -2, 0), true),
            new FacilitySeed("招募台", CampusFacilityType.Recruitment, 1, new Vector3Int(0, 1, 0), true)
        };

        public static void EnsureRoomsForScene()
        {
            CampusMapRoot mapRoot = UnityEngine.Object.FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            if (mapRoot == null)
            {
                return;
            }

            mapRoot.RebuildFloorReferences();

            GameObject bootstrapRoot = FindOrCreateRoot();
            EnsureSeededMarkers(mapRoot, bootstrapRoot.transform);
            EnsureRegistry(bootstrapRoot, mapRoot);
        }

        private static void EnsureSeededMarkers(CampusMapRoot mapRoot, Transform bootstrapRoot)
        {
            if (UnityEngine.Object.FindFirstObjectByType<CampusGameplayRoomMarker>(FindObjectsInactive.Include) == null)
            {
                for (int i = 0; i < RoomSeeds.Length; i++)
                {
                    CreateRoomMarker(mapRoot, bootstrapRoot, RoomSeeds[i]);
                }
            }

            if (UnityEngine.Object.FindFirstObjectByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include) == null)
            {
                for (int i = 0; i < FacilitySeeds.Length; i++)
                {
                    CreateFacilityMarker(mapRoot, bootstrapRoot, FacilitySeeds[i]);
                }
            }
        }

        private static void EnsureRegistry(GameObject bootstrapRoot, CampusMapRoot mapRoot)
        {
            CampusRoomRegistry registry = bootstrapRoot.GetComponent<CampusRoomRegistry>();
            if (registry == null)
            {
                registry = bootstrapRoot.AddComponent<CampusRoomRegistry>();
            }

            registry.RebuildRegistry();
        }

        private static GameObject FindOrCreateRoot()
        {
            GameObject existing = GameObject.Find(BootstrapRootName);
            if (existing != null)
            {
                return existing;
            }

            return new GameObject(BootstrapRootName);
        }

        private static void CreateRoomMarker(CampusMapRoot mapRoot, Transform parent, RoomSeed seed)
        {
            CampusFloorRoot floor = mapRoot.GetFloor(seed.FloorIndex);
            GameObject markerObject = new GameObject("GameplayRoom_" + seed.Id);
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.position = ResolveWorldPosition(floor, seed.Cell);

            CampusGameplayRoomMarker marker = markerObject.AddComponent<CampusGameplayRoomMarker>();
            marker.Configure(seed.Id, seed.Name, seed.Type, seed.FloorIndex, seed.Cell, seed.Size, seed.UsableForGameplay);
        }

        private static void CreateFacilityMarker(CampusMapRoot mapRoot, Transform parent, FacilitySeed seed)
        {
            CampusFloorRoot floor = mapRoot.GetFloor(seed.FloorIndex);
            GameObject markerObject = new GameObject("GameplayFacility_" + seed.Type + "_" + seed.Name);
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.position = ResolveWorldPosition(floor, seed.Cell);

            CampusGameplayFacilityMarker marker = markerObject.AddComponent<CampusGameplayFacilityMarker>();
            marker.Configure(seed.Name, seed.Type, seed.FloorIndex, seed.Cell, seed.CountsAsCoreFacility, null);
        }

        private static Vector3 ResolveWorldPosition(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor != null && floor.Grid != null)
            {
                return floor.Grid.GetCellCenterWorld(cell);
            }

            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        }
    }
}
