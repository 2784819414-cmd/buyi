using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeRoomPrefabLibrary
    {
        internal const string Schema = "NtingCampusRuntimeRoomPrefab.v1";
        private const string DefaultRoomName = "Unnamed Room";

        internal static List<CampusRuntimeRoomPrefab> LoadAll(string folder, Action<string> logWarning)
        {
            List<CampusRuntimeRoomPrefab> roomPrefabs = new List<CampusRuntimeRoomPrefab>();
            Directory.CreateDirectory(folder);

            string[] files = Directory.GetFiles(folder, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    CampusRuntimeRoomPrefab roomPrefab =
                        JsonUtility.FromJson<CampusRuntimeRoomPrefab>(File.ReadAllText(files[i], Encoding.UTF8));
                    if (roomPrefab == null)
                    {
                        continue;
                    }

                    Normalize(roomPrefab, Path.GetFileNameWithoutExtension(files[i]));
                    roomPrefab.SourcePath = files[i];
                    if (!string.IsNullOrWhiteSpace(roomPrefab.RoomName))
                    {
                        roomPrefabs.Add(roomPrefab);
                    }
                }
                catch (Exception exception)
                {
                    if (logWarning != null)
                    {
                        logWarning("Failed to load room prefab '" + files[i] + "': " + exception.Message);
                    }
                }
            }

            return roomPrefabs;
        }

        internal static string Save(string folder, CampusRuntimeRoomPrefab roomPrefab)
        {
            if (roomPrefab == null)
            {
                return string.Empty;
            }

            Normalize(roomPrefab, roomPrefab.RoomName);
            Directory.CreateDirectory(folder);
            string filePath = ResolveFilePath(folder, roomPrefab);
            File.WriteAllText(filePath, JsonUtility.ToJson(roomPrefab, true), Encoding.UTF8);
            roomPrefab.SourcePath = filePath;
            return filePath;
        }

        internal static bool Delete(CampusRuntimeRoomPrefab roomPrefab, string folder)
        {
            if (roomPrefab == null)
            {
                return false;
            }

            string path = ResolveExistingPath(roomPrefab, folder);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        internal static Vector2Int NormalizeSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        internal static bool HasContent(CampusRuntimeRoomPrefab roomPrefab)
        {
            return roomPrefab != null &&
                   ((roomPrefab.FloorTiles != null && roomPrefab.FloorTiles.Count > 0) ||
                    (roomPrefab.WallTiles != null && roomPrefab.WallTiles.Count > 0) ||
                    (roomPrefab.Objects != null && roomPrefab.Objects.Count > 0) ||
                    (roomPrefab.RoomMarkers != null && roomPrefab.RoomMarkers.Count > 0) ||
                    (roomPrefab.GameplayRooms != null && roomPrefab.GameplayRooms.Count > 0) ||
                    (roomPrefab.GameplayFacilities != null && roomPrefab.GameplayFacilities.Count > 0) ||
                    (roomPrefab.GameplayPrankSpots != null && roomPrefab.GameplayPrankSpots.Count > 0) ||
                    (roomPrefab.Lights != null && roomPrefab.Lights.Count > 0));
        }

        internal static void Normalize(CampusRuntimeRoomPrefab roomPrefab, string fallbackName)
        {
            if (roomPrefab == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(roomPrefab.Schema))
            {
                roomPrefab.Schema = Schema;
            }

            roomPrefab.RoomName = ResolveRoomName(roomPrefab.RoomName, fallbackName);
            roomPrefab.Size = NormalizeSize(roomPrefab.Size);
            roomPrefab.FloorTiles = roomPrefab.FloorTiles ?? new List<CampusRuntimeTileSnapshot>();
            roomPrefab.WallTiles = roomPrefab.WallTiles ?? new List<CampusRuntimeTileSnapshot>();
            roomPrefab.Objects = roomPrefab.Objects ?? new List<CampusRuntimeObjectSnapshot>();
            roomPrefab.RoomMarkers = roomPrefab.RoomMarkers ?? new List<CampusRuntimeRoomSnapshot>();
            roomPrefab.GameplayRooms = roomPrefab.GameplayRooms ?? new List<CampusRuntimeGameplayRoomSnapshot>();
            roomPrefab.GameplayFacilities = roomPrefab.GameplayFacilities ?? new List<CampusRuntimeGameplayFacilitySnapshot>();
            roomPrefab.GameplayPrankSpots = roomPrefab.GameplayPrankSpots ?? new List<CampusRuntimeGameplayPrankSpotSnapshot>();
            roomPrefab.Lights = roomPrefab.Lights ?? new List<CampusRuntimeRoomLightSnapshot>();

            for (int i = 0; i < roomPrefab.GameplayRooms.Count; i++)
            {
                roomPrefab.GameplayRooms[i]?.Normalize();
            }

            for (int i = 0; i < roomPrefab.GameplayFacilities.Count; i++)
            {
                roomPrefab.GameplayFacilities[i]?.Normalize();
            }

            for (int i = 0; i < roomPrefab.GameplayPrankSpots.Count; i++)
            {
                roomPrefab.GameplayPrankSpots[i]?.Normalize();
            }
        }

        private static string ResolveRoomName(string roomName, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                return roomName.Trim();
            }

            return string.IsNullOrWhiteSpace(fallbackName) ? DefaultRoomName : fallbackName.Trim();
        }

        private static string ResolveFilePath(string folder, CampusRuntimeRoomPrefab roomPrefab)
        {
            return Path.Combine(folder, CampusRuntimeImportLibrary.SanitizeFileName(roomPrefab.RoomName) + ".json");
        }

        private static string ResolveExistingPath(CampusRuntimeRoomPrefab roomPrefab, string folder)
        {
            if (!string.IsNullOrWhiteSpace(roomPrefab.SourcePath))
            {
                return roomPrefab.SourcePath;
            }

            Normalize(roomPrefab, roomPrefab.RoomName);
            return ResolveFilePath(folder, roomPrefab);
        }
    }

    [Serializable]
    public sealed class CampusRuntimeRoomPrefab
    {
        public string Schema;
        public string RoomName;
        public string CreatedAtLocal;
        public Vector2Int Size = Vector2Int.one;
        public List<CampusRuntimeTileSnapshot> FloorTiles = new List<CampusRuntimeTileSnapshot>();
        public List<CampusRuntimeTileSnapshot> WallTiles = new List<CampusRuntimeTileSnapshot>();
        public List<CampusRuntimeObjectSnapshot> Objects = new List<CampusRuntimeObjectSnapshot>();
        public List<CampusRuntimeRoomSnapshot> RoomMarkers = new List<CampusRuntimeRoomSnapshot>();
        public List<CampusRuntimeGameplayRoomSnapshot> GameplayRooms = new List<CampusRuntimeGameplayRoomSnapshot>();
        public List<CampusRuntimeGameplayFacilitySnapshot> GameplayFacilities = new List<CampusRuntimeGameplayFacilitySnapshot>();
        public List<CampusRuntimeGameplayPrankSpotSnapshot> GameplayPrankSpots = new List<CampusRuntimeGameplayPrankSpotSnapshot>();
        public List<CampusRuntimeRoomLightSnapshot> Lights = new List<CampusRuntimeRoomLightSnapshot>();

        [NonSerialized] public string SourcePath;
    }

    [Serializable]
    public sealed class CampusRuntimeRoomLightSnapshot
    {
        public CampusRuntimeLightSnapshot Light = new CampusRuntimeLightSnapshot();
        public Vector3Int RelativeCell;
        public Vector3 RelativePosition;
        public bool HasRelativePosition;
    }
}
