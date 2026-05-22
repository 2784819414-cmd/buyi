using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcRoomSelector
    {
        public static List<CampusGameplayRoom> GetRooms(CampusWorldService worldService, CampusRoomType roomType)
        {
            List<CampusGameplayRoom> rooms = worldService != null
                ? worldService.GetRoomsByType(roomType, true)
                : new List<CampusGameplayRoom>();
            if (rooms.Count == 0 && worldService != null)
            {
                rooms = worldService.GetRoomsByType(roomType, false);
            }

            rooms.Sort(CompareRooms);
            return rooms;
        }

        public static CampusGameplayRoom Choose(List<CampusGameplayRoom> rooms, string key, int salt)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return null;
            }

            int index = CampusNpcStableIds.PositiveModulo(CampusNpcStableIds.Hash(key) + salt, rooms.Count);
            return rooms[index];
        }

        public static CampusGameplayRoom ChooseNearest(
            List<CampusGameplayRoom> rooms,
            Vector3 origin,
            string preferredRoomId = "")
        {
            CampusGameplayRoom best = null;
            float bestDistanceSqr = float.MaxValue;
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(preferredRoomId) &&
                    string.Equals(room.RoomId, preferredRoomId, StringComparison.OrdinalIgnoreCase))
                {
                    return room;
                }

                float distanceSqr = (room.WorldCenter - origin).sqrMagnitude;
                if (best == null || distanceSqr < bestDistanceSqr)
                {
                    best = room;
                    bestDistanceSqr = distanceSqr;
                }
            }

            return best;
        }

        public static CampusGameplayRoom ResolveAssigned(
            CampusWorldService worldService,
            string roomId,
            CampusRoomType expectedType)
        {
            if (worldService == null || string.IsNullOrWhiteSpace(roomId))
            {
                return null;
            }

            CampusGameplayRoom room = worldService.FindRoomById(roomId.Trim());
            if (room == null)
            {
                return null;
            }

            return expectedType == CampusRoomType.Unknown || room.RoomType == expectedType
                ? room
                : null;
        }

        public static Vector3 PointNearCenter(CampusGameplayRoom room, int seed, float radius)
        {
            if (room == null)
            {
                return Vector3.zero;
            }

            float angle = CampusNpcStableIds.PositiveModulo(seed * 97, 360) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Mathf.Max(0f, radius);
            Vector3 result = room.WorldCenter + offset;
            result.z = 0f;
            return result;
        }

        public static bool SameRoom(CampusGameplayRoom left, CampusGameplayRoom right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.RoomId, right.RoomId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareRooms(CampusGameplayRoom left, CampusGameplayRoom right)
        {
            return string.Compare(RoomKey(left), RoomKey(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string RoomKey(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(room.RoomId)
                ? room.RoomId
                : room.RoomType + ":" + room.WorldCenter.x + ":" + room.WorldCenter.y;
        }
    }
}
