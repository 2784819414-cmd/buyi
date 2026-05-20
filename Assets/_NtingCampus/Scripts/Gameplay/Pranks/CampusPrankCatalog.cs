using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Pranks
{
    public readonly struct CampusPrankDefinition
    {
        public CampusPrankDefinition(
            string objectId,
            string chineseDisplayName,
            string englishDisplayName,
            string payload,
            CampusRoomType requiredRoomType,
            string chineseUnsupportedReason,
            string englishUnsupportedReason)
        {
            ObjectId = objectId;
            LocalizedDisplayName = new CampusLocalizedText(chineseDisplayName, englishDisplayName);
            Payload = payload;
            RequiredRoomType = requiredRoomType;
            LocalizedUnsupportedReason = new CampusLocalizedText(chineseUnsupportedReason, englishUnsupportedReason);
        }

        public string ObjectId { get; }
        public string DisplayName => LocalizedDisplayName.Current(ObjectId);
        public CampusLocalizedText LocalizedDisplayName { get; }
        public string Payload { get; }
        public CampusRoomType RequiredRoomType { get; }
        public string UnsupportedReason => LocalizedUnsupportedReason.Current();
        public CampusLocalizedText LocalizedUnsupportedReason { get; }

        public string GetDisplayName(CampusDisplayLanguage language)
        {
            return LocalizedDisplayName.Get(language, ObjectId);
        }

        public string GetUnsupportedReason(CampusDisplayLanguage language)
        {
            return LocalizedUnsupportedReason.Get(language);
        }
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
                "Pass Note",
                CampusPrankPayloadIds.PassNote,
                CampusRoomType.Classroom,
                "当前正式玩法只接通了课堂里的传纸条。",
                "The current formal gameplay only wires pass notes in classrooms."),
            new CampusPrankDefinition(
                ConfuseBooksObjectId,
                "乱整理书",
                "Confuse Books",
                CampusPrankPayloadIds.ConfuseBooks,
                CampusRoomType.Library,
                "乱整理书还没接入正式玩法。",
                "Confuse Books is not wired into formal gameplay yet."),
            new CampusPrankDefinition(
                StealDeliveryObjectId,
                "偷外卖",
                "Steal Delivery",
                CampusPrankPayloadIds.StealDelivery,
                CampusRoomType.Outdoor,
                "偷外卖还没接入正式玩法。",
                "Steal Delivery is not wired into formal gameplay yet."),
            new CampusPrankDefinition(
                StealFriedChickenObjectId,
                "偷炸鸡",
                "Steal Fried Chicken",
                CampusPrankPayloadIds.StealFriedChicken,
                CampusRoomType.Canteen,
                "偷炸鸡需要食堂、食堂店员和可偷食物点。",
                "Stealing fried chicken requires a canteen, a canteen clerk, and a stealable food spot."),
            new CampusPrankDefinition(
                StealBurgerObjectId,
                "偷汉堡",
                "Steal Burger",
                CampusPrankPayloadIds.StealBurger,
                CampusRoomType.Canteen,
                "偷汉堡需要食堂、食堂店员和可偷食物点。",
                "Stealing a burger requires a canteen, a canteen clerk, and a stealable food spot."),
            new CampusPrankDefinition(
                StealOdenObjectId,
                "偷关东煮",
                "Steal Oden",
                CampusPrankPayloadIds.StealOden,
                CampusRoomType.Canteen,
                "偷关东煮需要食堂、食堂店员和可偷食物点。",
                "Stealing oden requires a canteen, a canteen clerk, and a stealable food spot."),
            new CampusPrankDefinition(
                TwistBottleCapsObjectId,
                "拧瓶盖",
                "Twist Bottle Caps",
                CampusPrankPayloadIds.TwistBottleCaps,
                CampusRoomType.Store,
                "拧瓶盖还没接入正式玩法。",
                "Twist Bottle Caps is not wired into formal gameplay yet.")
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
