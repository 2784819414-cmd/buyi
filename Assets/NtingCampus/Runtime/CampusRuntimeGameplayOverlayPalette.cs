using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeGameplayOverlayPalette
    {
        internal static Color ResolveRoomTypeColor(CampusRoomType roomType)
        {
            switch (roomType)
            {
                case CampusRoomType.Classroom:
                    return new Color(0.25f, 0.55f, 0.98f, 1f);
                case CampusRoomType.Corridor:
                    return new Color(0.96f, 0.66f, 0.22f, 1f);
                case CampusRoomType.Office:
                    return new Color(0.72f, 0.48f, 0.28f, 1f);
                case CampusRoomType.Dormitory:
                    return new Color(0.56f, 0.42f, 0.88f, 1f);
                case CampusRoomType.Restroom:
                    return new Color(0.22f, 0.68f, 0.92f, 1f);
                case CampusRoomType.Library:
                    return new Color(0.16f, 0.72f, 0.72f, 1f);
                case CampusRoomType.ServiceArea:
                    return new Color(0.18f, 0.70f, 0.42f, 1f);
                case CampusRoomType.RetailArea:
                    return new Color(0.88f, 0.36f, 0.72f, 1f);
                case CampusRoomType.CommonActivityZone:
                    return new Color(0.88f, 0.62f, 0.24f, 1f);
                case CampusRoomType.Stairwell:
                    return new Color(0.62f, 0.62f, 0.62f, 1f);
                case CampusRoomType.Outdoor:
                    return new Color(0.22f, 0.58f, 0.95f, 1f);
                case CampusRoomType.HumanResources:
                    return new Color(0.74f, 0.36f, 0.88f, 1f);
                case CampusRoomType.ShrineRoom:
                    return new Color(0.92f, 0.38f, 0.45f, 1f);
                default:
                    return new Color(0.42f, 0.82f, 0.95f, 1f);
            }
        }

        internal static Color ResolveFacilityColor(CampusFacilityType facilityType)
        {
            if (CampusRuntimeGameplayMarkerPresetCatalog.TryGetPreset(
                    facilityType,
                    out CampusRuntimeGameplayMarkerPreset preset) &&
                preset != null)
            {
                return preset.Color;
            }

            return new Color(0.78f, 0.78f, 0.78f, 1f);
        }

        internal static Color ResolveActorColor(CampusRuntimeGameplayActorSnapshot actor)
        {
            if (actor == null)
            {
                return new Color(0.95f, 0.78f, 0.32f, 1f);
            }

            switch (actor.Role)
            {
                case CampusCharacterRole.Teacher:
                    return new Color(0.48f, 0.48f, 0.52f, 1f);
                case CampusCharacterRole.Staff:
                    return new Color(0.65f, 0.54f, 0.32f, 1f);
                default:
                    return new Color(0.38f, 0.56f, 0.83f, 1f);
            }
        }

        internal static Color ResolveRoomNameColor(string roomName)
        {
            string key = string.IsNullOrWhiteSpace(roomName) ? "Unnamed Room" : roomName.Trim().ToLowerInvariant();
            if (ContainsRoomNameToken(key, "\u670d\u52a1\u533a", "servicearea", "service_area"))
            {
                return ResolveRoomTypeColor(CampusRoomType.ServiceArea);
            }

            if (ContainsRoomNameToken(key, "\u96f6\u552e\u533a", "retailarea", "retail_area"))
            {
                return ResolveRoomTypeColor(CampusRoomType.RetailArea);
            }

            if (ContainsRoomNameToken(key, "\u6559\u5ba4", "class"))
            {
                return ResolveRoomTypeColor(CampusRoomType.Classroom);
            }

            if (ContainsRoomNameToken(key, "\u8d70\u5eca", "\u8fc7\u9053", "corridor", "hall"))
            {
                return ResolveRoomTypeColor(CampusRoomType.Corridor);
            }

            if (ContainsRoomNameToken(key, "\u529e\u516c\u5ba4", "\u6559\u5e08", "office", "teacher"))
            {
                return ResolveRoomTypeColor(CampusRoomType.Office);
            }

            if (ContainsRoomNameToken(key, "\u5bbf\u820d", "dorm"))
            {
                return ResolveRoomTypeColor(CampusRoomType.Dormitory);
            }

            if (ContainsRoomNameToken(key, "\u536b\u751f\u95f4", "\u5395\u6240", "\u6d17\u624b\u95f4", "restroom", "toilet", "bath"))
            {
                return ResolveRoomTypeColor(CampusRoomType.Restroom);
            }

            if (ContainsRoomNameToken(key, "\u56fe\u4e66\u9986", "library"))
            {
                return ResolveRoomTypeColor(CampusRoomType.Library);
            }

            if (ContainsRoomNameToken(key, "\u516c\u5171", "\u6d3b\u52a8", "common", "activity"))
            {
                return ResolveRoomTypeColor(CampusRoomType.CommonActivityZone);
            }

            if (ContainsRoomNameToken(key, "\u697c\u68af", "stair"))
            {
                return ResolveRoomTypeColor(CampusRoomType.Stairwell);
            }

            if (ContainsRoomNameToken(key, "\u4eba\u4e8b", "humanresources", "hr"))
            {
                return ResolveRoomTypeColor(CampusRoomType.HumanResources);
            }

            if (ContainsRoomNameToken(key, "\u795e\u9f9b", "shrine"))
            {
                return ResolveRoomTypeColor(CampusRoomType.ShrineRoom);
            }

            if (ContainsRoomNameToken(key, "\u6821\u5916", "\u5ba4\u5916", "\u64cd\u573a", "outdoor", "outside"))
            {
                return ResolveRoomTypeColor(CampusRoomType.Outdoor);
            }

            Color[] palette =
            {
                new Color(0.28f, 0.68f, 0.95f, 1f),
                new Color(0.88f, 0.52f, 0.28f, 1f),
                new Color(0.54f, 0.72f, 0.24f, 1f),
                new Color(0.78f, 0.42f, 0.88f, 1f),
                new Color(0.22f, 0.72f, 0.58f, 1f),
                new Color(0.92f, 0.38f, 0.45f, 1f),
                new Color(0.42f, 0.58f, 0.92f, 1f),
                new Color(0.72f, 0.62f, 0.22f, 1f)
            };

            int hash = 23;
            for (int i = 0; i < key.Length; i++)
            {
                hash = unchecked(hash * 37 + key[i]);
            }

            int index = Mathf.Abs(hash == int.MinValue ? 0 : hash) % palette.Length;
            return palette[index];
        }

        private static bool ContainsRoomNameToken(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && value.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
