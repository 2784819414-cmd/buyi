using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    public sealed class CampusCanteenStationRegistry
    {
        private const float RefreshIntervalSeconds = 2.5f;

        private readonly List<CampusCanteenStation> stations = new List<CampusCanteenStation>();
        private readonly Dictionary<string, CampusCanteenStation> stationsById =
            new Dictionary<string, CampusCanteenStation>(StringComparer.OrdinalIgnoreCase);

        private CampusWorldService worldService;
        private float nextRefreshTime = -1f;

        public IReadOnlyList<CampusCanteenStation> Stations => stations;

        public CampusCanteenStationRegistry(CampusWorldService worldService)
        {
            this.worldService = worldService;
        }

        public void SetWorldService(CampusWorldService value)
        {
            worldService = value;
        }

        public void RefreshIfNeeded(bool force)
        {
            if (!force && Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + RefreshIntervalSeconds;
            Rebuild();
        }

        public bool TryGetStation(string stationId, out CampusCanteenStation station)
        {
            RefreshIfNeeded(false);
            return stationsById.TryGetValue(NormalizeId(stationId), out station) && station != null;
        }

        public bool TryFindStationForObject(UnityEngine.Object source, out CampusCanteenStation station)
        {
            RefreshIfNeeded(false);
            station = null;
            if (source == null)
            {
                return false;
            }

            for (int i = 0; i < stations.Count; i++)
            {
                CampusCanteenStation candidate = stations[i];
                if (candidate == null)
                {
                    continue;
                }

                if (ReferenceEquals(candidate.WindowObject, source) ||
                    ReferenceEquals(candidate.FoodBoxObject, source) ||
                    ReferenceEquals(candidate.StockObject, source))
                {
                    station = candidate;
                    return true;
                }
            }

            return false;
        }

        public CampusCanteenStation FindNearestOpenStation(Vector3 position)
        {
            RefreshIfNeeded(false);
            CampusCanteenStation best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < stations.Count; i++)
            {
                CampusCanteenStation station = stations[i];
                if (station == null || !station.HasServingWindow)
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude((Vector2)(station.CustomerPosition - position));
                if (distance < bestDistance)
                {
                    best = station;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void Rebuild()
        {
            stations.Clear();
            stationsById.Clear();
            if (worldService == null)
            {
                return;
            }

            List<CampusGameplayRoom> rooms = worldService.GetRoomsByType(CampusRoomType.Canteen, false);
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                AddRoomStations(rooms[roomIndex]);
            }
        }

        private void AddRoomStations(CampusGameplayRoom room)
        {
            if (room == null || room.Facilities == null)
            {
                return;
            }

            StationBuildContext context = new StationBuildContext(room);
            for (int i = 0; i < context.Windows.Count; i++)
            {
                CampusCanteenStation station = BuildStation(room, context, context.Windows[i], i);
                AddStation(station);
            }
        }

        private CampusCanteenStation BuildStation(
            CampusGameplayRoom room,
            StationBuildContext context,
            CampusGameplayRoom.FacilityRecord window,
            int index)
        {
            Vector3 windowPosition = FacilityPosition(window);
            CampusGameplayRoom.FacilityRecord counter = context.FindNearestCounter(windowPosition);
            CampusGameplayRoom.FacilityRecord clerk = context.TakeNearestClerk(windowPosition);
            CampusGameplayRoom.FacilityRecord pickup = context.TakeNearestPickup(windowPosition);
            CampusGameplayRoom.FacilityRecord foodBox = context.TakeNearestFoodBox(windowPosition);

            string stationId = ResolveStationId(room, window, index);
            string displayName = ResolveWindowDisplayName(window, index);
            Vector3 counterPosition = counter != null ? FacilityPosition(counter) : windowPosition;
            Vector3 mealDropPosition = new Vector3(windowPosition.x, counterPosition.y, counterPosition.z);

            CampusCanteenStation station = new CampusCanteenStation
            {
                StationId = stationId,
                RoomId = room.RoomId,
                DisplayName = displayName,
                WindowTypeId = ResolveWindowTypeId(window),
                WindowIndex = index,
                FloorIndex = room.FloorIndex,
                WindowFacilityId = FacilityId(window),
                CounterFacilityId = FacilityId(counter),
                ClerkStandFacilityId = FacilityId(clerk),
                CustomerPickupFacilityId = FacilityId(pickup),
                FoodBoxFacilityId = FacilityId(foodBox),
                WindowPosition = windowPosition,
                CounterPosition = counterPosition,
                CustomerPosition = pickup != null ? FacilityPosition(pickup) : windowPosition + Vector3.down * 0.75f,
                ClerkPosition = clerk != null ? FacilityPosition(clerk) : windowPosition + Vector3.up * 0.75f,
                MealDropPosition = mealDropPosition,
                FoodBoxPosition = foodBox != null ? FacilityPosition(foodBox) : windowPosition + Vector3.right * 0.85f,
                StockPosition = foodBox != null ? FacilityPosition(foodBox) : windowPosition + Vector3.right * 0.85f,
                WindowObject = window.PlacedObject,
                CounterObject = counter != null ? counter.PlacedObject : null,
                FoodBoxObject = foodBox != null ? foodBox.PlacedObject : null,
                StockObject = foodBox != null ? foodBox.PlacedObject : null,
                HasFoodBox = foodBox != null
            };
            return station;
        }

        private void AddStation(CampusCanteenStation station)
        {
            if (station == null || string.IsNullOrWhiteSpace(station.StationId))
            {
                return;
            }

            station.StationId = BuildUniqueStationId(station.StationId);
            station.CounterContainerId = CampusCanteenStation.BuildContainerId(station.StationId, "counter");
            station.FoodBoxContainerId = CampusCanteenStation.BuildContainerId(station.StationId, "food_box");
            station.StockContainerId = station.FoodBoxContainerId;
            stations.Add(station);
            stationsById.Add(station.StationId, station);
        }

        private string BuildUniqueStationId(string stationId)
        {
            string baseId = NormalizeId(stationId);
            if (string.IsNullOrEmpty(baseId))
            {
                baseId = "canteen_serving_window";
            }

            if (!stationsById.ContainsKey(baseId))
            {
                return baseId;
            }

            int suffix = 2;
            string candidate = baseId + "_" + suffix;
            while (stationsById.ContainsKey(candidate))
            {
                suffix++;
                candidate = baseId + "_" + suffix;
            }

            return candidate;
        }

        private static string ResolveStationId(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord window,
            int index)
        {
            if (window != null && !string.IsNullOrWhiteSpace(window.FacilityId))
            {
                return NormalizeId(window.FacilityId);
            }

            string roomId = room != null && !string.IsNullOrWhiteSpace(room.RoomId) ? room.RoomId : "canteen";
            return roomId + ".serving_window_" + (index + 1);
        }

        private static string ResolveWindowDisplayName(CampusGameplayRoom.FacilityRecord window, int index)
        {
            string displayName = window != null ? window.DisplayName : string.Empty;
            return IsGenericWindowName(displayName)
                ? CampusCanteenTextCatalog.Format(CampusCanteenTextId.GenericWindowName, index + 1)
                : displayName.Trim();
        }

        private static bool IsGenericWindowName(string displayName)
        {
            string key = NormalizeKey(displayName);
            return string.IsNullOrEmpty(key) ||
                   key == "canteenservingwindow" ||
                   key == "servingwindow" ||
                   key == "mealwindow" ||
                   key == "foodwindow" ||
                   key == NormalizeKey("\u6253\u996d\u7a97\u53e3") ||
                   key == NormalizeKey("\u98df\u5802\u7a97\u53e3");
        }

        private static string ResolveWindowTypeId(CampusGameplayRoom.FacilityRecord window)
        {
            string key = NormalizeKey((window != null ? window.FacilityId : string.Empty) + " " + (window != null ? window.DisplayName : string.Empty));
            if (ContainsAny(key, "malatang", "spicyhotpot", "\u9ebb\u8fa3\u70eb"))
            {
                return "malatang";
            }

            if (ContainsAny(key, "noodle", "mian", "\u9762"))
            {
                return "noodles";
            }

            if (ContainsAny(key, "rice", "gaifan", "\u76d6\u6d47\u996d", "\u7c73\u996d"))
            {
                return "rice_bowl";
            }

            if (ContainsAny(key, "burger", "chicken", "\u6c49\u5821", "\u70b8\u9e21"))
            {
                return "burger_fried_chicken";
            }

            return "generic";
        }

        private static List<CampusGameplayRoom.FacilityRecord> FindFacilities(
            CampusGameplayRoom room,
            CampusFacilityType type)
        {
            List<CampusGameplayRoom.FacilityRecord> result = new List<CampusGameplayRoom.FacilityRecord>();
            IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = room.Facilities;
            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = facilities[i];
                if (facility != null && facility.FacilityType == type)
                {
                    result.Add(facility);
                }
            }

            result.Sort(CompareFacilities);
            return result;
        }

        private static int CompareFacilities(
            CampusGameplayRoom.FacilityRecord left,
            CampusGameplayRoom.FacilityRecord right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int y = left.Cell.y.CompareTo(right.Cell.y);
            return y != 0 ? y : left.Cell.x.CompareTo(right.Cell.x);
        }

        private static Vector3 FacilityPosition(CampusGameplayRoom.FacilityRecord facility)
        {
            if (facility == null)
            {
                return Vector3.zero;
            }

            CampusPlacedObject placed = facility.PlacedObject;
            if (placed != null)
            {
                Vector2Int footprint = placed.RotatedFootprintSize;
                return new Vector3(
                    placed.Cell.x + Mathf.Max(1, footprint.x) * 0.5f,
                    placed.Cell.y + Mathf.Max(1, footprint.y) * 0.5f,
                    0f);
            }

            return CellCenter(facility.Cell);
        }

        private static string FacilityId(CampusGameplayRoom.FacilityRecord facility)
        {
            return facility != null ? NormalizeId(facility.FacilityId) : string.Empty;
        }

        private static Vector3 CellCenter(Vector3Int cell)
        {
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(tokens[i]) && value.Contains(NormalizeKey(tokens[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class StationBuildContext
        {
            private readonly List<CampusGameplayRoom.FacilityRecord> counters;
            private readonly List<CampusGameplayRoom.FacilityRecord> clerks;
            private readonly List<CampusGameplayRoom.FacilityRecord> pickups;
            private readonly List<CampusGameplayRoom.FacilityRecord> foodBoxes;
            private readonly bool[] usedClerks;
            private readonly bool[] usedPickups;
            private readonly bool[] usedFoodBoxes;

            public StationBuildContext(CampusGameplayRoom room)
            {
                Windows = FindFacilities(room, CampusFacilityType.CanteenServingWindow);
                KeepPlacedWindowsWhenPresent(Windows);
                counters = FindFacilities(room, CampusFacilityType.CanteenCounter);
                clerks = FindFacilities(room, CampusFacilityType.CanteenClerkStandPoint);
                pickups = FindFacilities(room, CampusFacilityType.CanteenCustomerPickupPoint);
                foodBoxes = FindFacilities(room, CampusFacilityType.CanteenFoodBox);
                AppendFacilities(foodBoxes, FindFacilities(room, CampusFacilityType.CanteenFoodTray));

                usedClerks = new bool[clerks.Count];
                usedPickups = new bool[pickups.Count];
                usedFoodBoxes = new bool[foodBoxes.Count];
            }

            public List<CampusGameplayRoom.FacilityRecord> Windows { get; }

            public CampusGameplayRoom.FacilityRecord FindNearestCounter(Vector3 origin)
            {
                return FindNearest(counters, null, origin);
            }

            public CampusGameplayRoom.FacilityRecord TakeNearestClerk(Vector3 origin)
            {
                return FindNearest(clerks, usedClerks, origin);
            }

            public CampusGameplayRoom.FacilityRecord TakeNearestPickup(Vector3 origin)
            {
                return FindNearest(pickups, usedPickups, origin);
            }

            public CampusGameplayRoom.FacilityRecord TakeNearestFoodBox(Vector3 origin)
            {
                return FindNearest(foodBoxes, usedFoodBoxes, origin);
            }

            private static CampusGameplayRoom.FacilityRecord FindNearest(
                List<CampusGameplayRoom.FacilityRecord> facilities,
                bool[] used,
                Vector3 origin)
            {
                CampusGameplayRoom.FacilityRecord best = null;
                int bestIndex = -1;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < facilities.Count; i++)
                {
                    if (used != null && used[i])
                    {
                        continue;
                    }

                    CampusGameplayRoom.FacilityRecord facility = facilities[i];
                    if (facility == null)
                    {
                        continue;
                    }

                    float distance = Vector2.SqrMagnitude((Vector2)(FacilityPosition(facility) - origin));
                    if (distance < bestDistance)
                    {
                        best = facility;
                        bestIndex = i;
                        bestDistance = distance;
                    }
                }

                if (bestIndex >= 0 && used != null)
                {
                    used[bestIndex] = true;
                }

                return best;
            }

            private static void AppendFacilities(
                List<CampusGameplayRoom.FacilityRecord> target,
                List<CampusGameplayRoom.FacilityRecord> additions)
            {
                for (int i = 0; i < additions.Count; i++)
                {
                    target.Add(additions[i]);
                }
            }

            private static void KeepPlacedWindowsWhenPresent(List<CampusGameplayRoom.FacilityRecord> windows)
            {
                bool hasPlacedWindow = false;
                for (int i = 0; i < windows.Count; i++)
                {
                    CampusGameplayRoom.FacilityRecord window = windows[i];
                    if (window != null && window.PlacedObject != null)
                    {
                        hasPlacedWindow = true;
                        break;
                    }
                }

                if (!hasPlacedWindow)
                {
                    return;
                }

                windows.RemoveAll(window => window == null || window.PlacedObject == null);
            }
        }
    }
}
