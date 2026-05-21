using System;

namespace NtingCampus.Gameplay.Rooms
{
    internal static class CampusRoomTypeInference
    {
        public static CampusRoomType Infer(string roomName)
        {
            string normalizedName = NormalizeKey(roomName);

            if (ContainsAny(normalizedName, "servicearea", "service_area") ||
                ContainsAny(roomName, "\u670d\u52a1\u533a"))
            {
                return CampusRoomType.ServiceArea;
            }

            if (ContainsAny(normalizedName, "retailarea", "retail_area") ||
                ContainsAny(roomName, "\u96f6\u552e\u533a"))
            {
                return CampusRoomType.RetailArea;
            }

            if (ContainsAny(normalizedName, "classroom", "class") ||
                ContainsAny(roomName, "\u6559\u5ba4"))
            {
                return CampusRoomType.Classroom;
            }

            if (ContainsAny(normalizedName, "library") ||
                ContainsAny(roomName, "\u56fe\u4e66"))
            {
                return CampusRoomType.Library;
            }

            if (ContainsAny(normalizedName, "dormitory", "dorm") ||
                ContainsAny(roomName, "\u5bbf\u820d"))
            {
                return CampusRoomType.Dormitory;
            }

            if (ContainsAny(normalizedName, "restroom", "toilet", "bath") ||
                ContainsAny(roomName, "\u536b\u751f\u95f4", "\u5395\u6240", "\u6d17\u624b\u95f4"))
            {
                return CampusRoomType.Restroom;
            }

            if (ContainsAny(normalizedName, "office", "teacher") ||
                ContainsAny(roomName, "\u529e\u516c\u5ba4", "\u6559\u5e08"))
            {
                return CampusRoomType.Office;
            }

            if (ContainsAny(normalizedName, "humanresources", "human_resources") ||
                ContainsAny(roomName, "\u4eba\u4e8b"))
            {
                return CampusRoomType.HumanResources;
            }

            if (ContainsAny(normalizedName, "shrineroom", "shrine_room", "shrine") ||
                ContainsAny(roomName, "\u795e\u9f9b"))
            {
                return CampusRoomType.ShrineRoom;
            }

            if (ContainsAny(normalizedName, "commonactivityzone", "common_activity_zone", "activity", "common") ||
                ContainsAny(roomName, "\u516c\u5171\u6d3b\u52a8", "\u6d3b\u52a8\u533a"))
            {
                return CampusRoomType.CommonActivityZone;
            }

            if (ContainsAny(normalizedName, "corridor", "hall") ||
                ContainsAny(roomName, "\u8d70\u5eca", "\u8fc7\u9053"))
            {
                return CampusRoomType.Corridor;
            }

            if (ContainsAny(normalizedName, "stairwell", "stair") ||
                ContainsAny(roomName, "\u697c\u68af"))
            {
                return CampusRoomType.Stairwell;
            }

            if (ContainsAny(normalizedName, "outdoor", "outside") ||
                ContainsAny(roomName, "\u5ba4\u5916", "\u64cd\u573a", "\u6821\u5916"))
            {
                return CampusRoomType.Outdoor;
            }

            return CampusRoomType.Unknown;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrWhiteSpace(token) &&
                    value.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(' ', '_').ToLowerInvariant();
        }
    }
}
