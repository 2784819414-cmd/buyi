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
                case CampusRoomType.Canteen:
                    return new Color(0.22f, 0.72f, 0.46f, 1f);
                case CampusRoomType.Store:
                    return new Color(0.88f, 0.36f, 0.72f, 1f);
                case CampusRoomType.Library:
                    return new Color(0.16f, 0.72f, 0.72f, 1f);
                case CampusRoomType.CommonActivityZone:
                    return new Color(0.88f, 0.62f, 0.24f, 1f);
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
            switch (facilityType)
            {
                case CampusFacilityType.Door:
                    return new Color(0.72f, 0.72f, 0.72f, 1f);
                case CampusFacilityType.Blackboard:
                    return new Color(0.12f, 0.46f, 0.36f, 1f);
                case CampusFacilityType.StudentDesk:
                    return new Color(0.26f, 0.56f, 0.96f, 1f);
                case CampusFacilityType.Podium:
                    return new Color(0.18f, 0.38f, 0.86f, 1f);
                case CampusFacilityType.OfficeDesk:
                    return new Color(0.72f, 0.48f, 0.28f, 1f);
                case CampusFacilityType.Bed:
                    return new Color(0.56f, 0.42f, 0.88f, 1f);
                case CampusFacilityType.BulletinBoard:
                    return new Color(0.88f, 0.62f, 0.24f, 1f);
                case CampusFacilityType.Recruitment:
                    return new Color(0.74f, 0.36f, 0.88f, 1f);
                case CampusFacilityType.Sink:
                    return new Color(0.22f, 0.68f, 0.92f, 1f);
                case CampusFacilityType.Storage:
                    return new Color(0.62f, 0.56f, 0.46f, 1f);
                case CampusFacilityType.CanteenCounter:
                    return new Color(0.18f, 0.68f, 0.72f, 1f);
                case CampusFacilityType.CanteenServingWindow:
                    return new Color(0.16f, 0.78f, 0.78f, 1f);
                case CampusFacilityType.CanteenClerkStandPoint:
                    return new Color(0.12f, 0.58f, 0.68f, 1f);
                case CampusFacilityType.CanteenCustomerPickupPoint:
                    return new Color(0.48f, 0.82f, 0.62f, 1f);
                case CampusFacilityType.CanteenQueuePoint:
                    return new Color(0.95f, 0.76f, 0.28f, 1f);
                case CampusFacilityType.CanteenFoodTray:
                    return new Color(0.96f, 0.64f, 0.24f, 1f);
                case CampusFacilityType.CanteenFoodBox:
                    return new Color(0.94f, 0.56f, 0.18f, 1f);
                case CampusFacilityType.DeliveryDropPoint:
                    return new Color(0.32f, 0.54f, 0.98f, 1f);
                case CampusFacilityType.DiningTable:
                    return new Color(0.55f, 0.76f, 0.28f, 1f);
                case CampusFacilityType.StoreShelf:
                    return new Color(0.88f, 0.36f, 0.72f, 1f);
                case CampusFacilityType.StoreQueuePoint:
                    return new Color(0.94f, 0.48f, 0.82f, 1f);
                case CampusFacilityType.StoreCheckout:
                    return new Color(0.76f, 0.24f, 0.64f, 1f);
                default:
                    return new Color(0.78f, 0.78f, 0.78f, 1f);
            }
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
                    if ((actor.StaffDuty & CampusStaffDuty.CanteenClerk) != 0)
                    {
                        return new Color(0.65f, 0.54f, 0.32f, 1f);
                    }

                    if ((actor.StaffDuty & CampusStaffDuty.StoreOwner) != 0 ||
                        (actor.StaffDuty & CampusStaffDuty.BookstoreOwner) != 0)
                    {
                        return new Color(0.72f, 0.46f, 0.32f, 1f);
                    }

                    return new Color(0.32f, 0.54f, 0.98f, 1f);
                default:
                    return new Color(0.38f, 0.56f, 0.83f, 1f);
            }
        }

        internal static Color ResolveRoomNameColor(string roomName)
        {
            string key = string.IsNullOrWhiteSpace(roomName) ? "Unnamed Room" : roomName.Trim().ToLowerInvariant();
            if (ContainsRoomNameToken(key, "\u6559\u5ba4", "class"))
            {
                return new Color(0.25f, 0.55f, 0.98f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u8d70\u5eca", "\u8fc7\u9053", "corridor", "hall"))
            {
                return new Color(0.96f, 0.66f, 0.22f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u529e\u516c\u5ba4", "\u6559\u5e08", "office", "teacher"))
            {
                return new Color(0.72f, 0.48f, 0.28f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u5bbf\u820d", "dorm"))
            {
                return new Color(0.56f, 0.42f, 0.88f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u536b\u751f\u95f4", "\u5395\u6240", "\u6d17\u624b\u95f4", "restroom", "toilet", "bath"))
            {
                return new Color(0.22f, 0.68f, 0.92f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u98df\u5802", "\u9910\u5385", "canteen", "dining"))
            {
                return new Color(0.18f, 0.70f, 0.42f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u5916\u5356", "delivery"))
            {
                return new Color(0.35f, 0.55f, 1f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u8d85\u5e02", "\u5546\u5e97", "\u5c0f\u5356", "shop", "store", "market"))
            {
                return new Color(0.88f, 0.36f, 0.72f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u4e66\u5e97", "bookstore", "bookshop"))
            {
                return new Color(0.58f, 0.42f, 0.94f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u56fe\u4e66\u9986", "library"))
            {
                return new Color(0.16f, 0.72f, 0.72f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u516c\u5171", "\u6d3b\u52a8", "common", "activity"))
            {
                return new Color(0.88f, 0.62f, 0.24f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u697c\u68af", "stair"))
            {
                return new Color(0.62f, 0.62f, 0.62f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u4eba\u4e8b", "humanresources", "hr"))
            {
                return new Color(0.74f, 0.36f, 0.88f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u795e\u9f9b", "shrine"))
            {
                return new Color(0.92f, 0.38f, 0.45f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u6821\u5916", "\u5ba4\u5916", "\u64cd\u573a", "outdoor", "outside"))
            {
                return new Color(0.88f, 0.48f, 0.24f, 1f);
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
