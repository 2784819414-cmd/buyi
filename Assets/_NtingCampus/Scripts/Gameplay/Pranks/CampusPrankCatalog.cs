using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Pranks
{
    public readonly struct CampusPrankDefinition
    {
        public CampusPrankDefinition(
            string objectId,
            string displayName,
            string payload,
            CampusRoomType requiredRoomType,
            string unsupportedReason)
        {
            ObjectId = objectId;
            DisplayName = displayName;
            Payload = payload;
            RequiredRoomType = requiredRoomType;
            UnsupportedReason = unsupportedReason;
        }

        public string ObjectId { get; }
        public string DisplayName { get; }
        public string Payload { get; }
        public CampusRoomType RequiredRoomType { get; }
        public string UnsupportedReason { get; }
    }

    public static class CampusPrankCatalog
    {
        public const string PassNoteObjectId = "CampusPrankSpot_PassNote";
        public const string ConfuseBooksObjectId = "CampusPrankSpot_ConfuseBooks";
        public const string StealDeliveryObjectId = "CampusPrankSpot_StealDelivery";
        public const string StealFriedChickenObjectId = "CampusPrankSpot_StealFriedChicken";
        public const string StealBurgerObjectId = "CampusPrankSpot_StealBurger";
        public const string StealOdenObjectId = "CampusPrankSpot_StealOden";
        public const string TwistBottleCapsObjectId = "CampusPrankSpot_TwistBottleCaps";

        private static readonly CampusPrankDefinition[] Definitions =
        {
            new CampusPrankDefinition(
                PassNoteObjectId,
                "传纸条",
                CampusPrankPayloadIds.PassNote,
                CampusRoomType.Classroom,
                "当前正式玩法只接通了课堂里的传纸条。"),
            new CampusPrankDefinition(
                ConfuseBooksObjectId,
                "乱整理书",
                CampusPrankPayloadIds.ConfuseBooks,
                CampusRoomType.Library,
                "乱整理书还没接入正式玩法。"),
            new CampusPrankDefinition(
                StealDeliveryObjectId,
                "偷外卖",
                CampusPrankPayloadIds.StealDelivery,
                CampusRoomType.Outdoor,
                "偷外卖还没接入正式玩法。"),
            new CampusPrankDefinition(
                StealFriedChickenObjectId,
                "偷炸鸡",
                CampusPrankPayloadIds.StealFriedChicken,
                CampusRoomType.Canteen,
                "偷炸鸡需要食堂、食堂店员和可偷食物点。"),
            new CampusPrankDefinition(
                StealBurgerObjectId,
                "偷汉堡",
                CampusPrankPayloadIds.StealBurger,
                CampusRoomType.Canteen,
                "偷汉堡需要食堂、食堂店员和可偷食物点。"),
            new CampusPrankDefinition(
                StealOdenObjectId,
                "偷关东煮",
                CampusPrankPayloadIds.StealOden,
                CampusRoomType.Canteen,
                "偷关东煮需要食堂、食堂店员和可偷食物点。"),
            new CampusPrankDefinition(
                TwistBottleCapsObjectId,
                "拧瓶盖",
                CampusPrankPayloadIds.TwistBottleCaps,
                CampusRoomType.Store,
                "拧瓶盖还没接入正式玩法。")
        };

        public static int Count => Definitions.Length;

        public static CampusPrankDefinition GetAt(int index)
        {
            return Definitions[index];
        }

        public static bool TryGetByObjectId(string objectId, out CampusPrankDefinition definition)
        {
            for (int index = 0; index < Definitions.Length; index++)
            {
                if (string.Equals(Definitions[index].ObjectId, objectId, System.StringComparison.OrdinalIgnoreCase))
                {
                    definition = Definitions[index];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        public static bool TryGetByPayload(string payload, out CampusPrankDefinition definition)
        {
            for (int index = 0; index < Definitions.Length; index++)
            {
                if (string.Equals(Definitions[index].Payload, payload, System.StringComparison.OrdinalIgnoreCase))
                {
                    definition = Definitions[index];
                    return true;
                }
            }

            definition = default;
            return false;
        }
    }
}
