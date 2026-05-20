using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal sealed class CampusRuntimeGameplayMarkerPreset
    {
        public readonly string ChineseLabel;
        public readonly string EnglishLabel;
        public readonly string ChineseDisplayName;
        public readonly string EnglishDisplayName;
        public readonly CampusRoomType RoomType;
        public readonly CampusFacilityType FacilityType;
        public readonly string PrankPayload;
        public readonly CampusPrankSpotVisualKind VisualKind;
        public readonly Color Color;

        private CampusRuntimeGameplayMarkerPreset(
            string chineseLabel,
            string englishLabel,
            string chineseDisplayName,
            string englishDisplayName,
            CampusRoomType roomType,
            CampusFacilityType facilityType,
            string prankPayload,
            CampusPrankSpotVisualKind visualKind,
            Color color)
        {
            ChineseLabel = chineseLabel;
            EnglishLabel = englishLabel;
            ChineseDisplayName = chineseDisplayName;
            EnglishDisplayName = englishDisplayName;
            RoomType = roomType;
            FacilityType = facilityType;
            PrankPayload = prankPayload;
            VisualKind = visualKind;
            Color = color;
        }

        public bool UsesInteractionSpot => !string.IsNullOrWhiteSpace(PrankPayload);

        public static CampusRuntimeGameplayMarkerPreset FacilityPoint(
            string chineseLabel,
            string englishLabel,
            CampusFacilityType facilityType,
            Color color)
        {
            return new CampusRuntimeGameplayMarkerPreset(
                chineseLabel,
                englishLabel,
                chineseLabel,
                englishLabel,
                CampusRoomType.Unknown,
                facilityType,
                string.Empty,
                CampusPrankSpotVisualKind.Envelope,
                color);
        }

        public static CampusRuntimeGameplayMarkerPreset InteractionFacilityPoint(
            string chineseLabel,
            string englishLabel,
            string payload,
            CampusRoomType requiredRoomType,
            CampusPrankSpotVisualKind visualKind,
            Color color)
        {
            return new CampusRuntimeGameplayMarkerPreset(
                chineseLabel,
                englishLabel,
                chineseLabel,
                englishLabel,
                requiredRoomType,
                CampusFacilityType.Unknown,
                payload,
                visualKind,
                color);
        }
    }

    internal static class CampusRuntimeGameplayMarkerPresetCatalog
    {
        private static CampusRuntimeGameplayMarkerPreset[] cachedPresets;

        internal static CampusRuntimeGameplayMarkerPreset[] Presets
        {
            get
            {
                if (cachedPresets == null)
                {
                    cachedPresets = LoadPresets();
                }

                return cachedPresets;
            }
        }

        private static readonly CampusRuntimeGameplayMarkerPreset[] BuiltInPresets =
        {
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u95e8",
                "Door",
                CampusFacilityType.Door,
                new Color(0.72f, 0.72f, 0.72f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u9ed1\u677f",
                "Blackboard",
                CampusFacilityType.Blackboard,
                new Color(0.12f, 0.46f, 0.36f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8bfe\u684c",
                "Student Desk",
                CampusFacilityType.StudentDesk,
                new Color(0.26f, 0.56f, 0.96f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8bb2\u53f0",
                "Podium",
                CampusFacilityType.Podium,
                new Color(0.18f, 0.38f, 0.86f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u529e\u516c\u684c",
                "Office Desk",
                CampusFacilityType.OfficeDesk,
                new Color(0.72f, 0.48f, 0.28f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5e8a",
                "Bed",
                CampusFacilityType.Bed,
                new Color(0.56f, 0.42f, 0.88f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u516c\u544a\u680f",
                "Bulletin Board",
                CampusFacilityType.BulletinBoard,
                new Color(0.88f, 0.62f, 0.24f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u62db\u52df\u70b9",
                "Recruitment",
                CampusFacilityType.Recruitment,
                new Color(0.74f, 0.36f, 0.88f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u6d17\u624b\u6c60",
                "Sink",
                CampusFacilityType.Sink,
                new Color(0.22f, 0.68f, 0.92f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u50a8\u7269\u70b9",
                "Storage",
                CampusFacilityType.Storage,
                new Color(0.62f, 0.56f, 0.46f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u98df\u5802\u67dc\u53f0",
                "Canteen Counter",
                CampusFacilityType.CanteenCounter,
                new Color(0.18f, 0.68f, 0.72f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u6253\u996d\u7a97\u53e3",
                "Serving Window",
                CampusFacilityType.CanteenServingWindow,
                new Color(0.16f, 0.78f, 0.78f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5e97\u5458\u540e\u53f0\u7ad9\u4f4d",
                "Clerk Back Stand",
                CampusFacilityType.CanteenClerkStandPoint,
                new Color(0.12f, 0.58f, 0.68f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u987e\u5ba2\u53d6\u9910\u70b9",
                "Customer Pickup",
                CampusFacilityType.CanteenCustomerPickupPoint,
                new Color(0.48f, 0.82f, 0.62f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u98df\u5802\u6392\u961f\u70b9",
                "Canteen Queue",
                CampusFacilityType.CanteenQueuePoint,
                new Color(0.95f, 0.76f, 0.28f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u6253\u996d\u6258\u76d8",
                "Food Tray",
                CampusFacilityType.CanteenFoodTray,
                new Color(0.96f, 0.64f, 0.24f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u73b0\u6210\u98df\u7269\u7bb1",
                "Ready Food Box",
                CampusFacilityType.CanteenFoodBox,
                new Color(0.94f, 0.56f, 0.18f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5403\u996d\u533a\u5ea7\u4f4d",
                "Dining Table",
                CampusFacilityType.DiningTable,
                new Color(0.55f, 0.76f, 0.28f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5916\u5356\u653e\u7f6e\u70b9",
                "Delivery Drop",
                CampusFacilityType.DeliveryDropPoint,
                new Color(0.32f, 0.54f, 0.98f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8d85\u5e02\u8d27\u67b6",
                "Store Shelf",
                CampusFacilityType.StoreShelf,
                new Color(0.88f, 0.36f, 0.72f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8d85\u5e02\u6392\u961f\u70b9",
                "Store Queue",
                CampusFacilityType.StoreQueuePoint,
                new Color(0.94f, 0.48f, 0.82f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8d85\u5e02\u6536\u94f6\u53f0",
                "Store Checkout",
                CampusFacilityType.StoreCheckout,
                new Color(0.76f, 0.24f, 0.64f, 1f)),
            CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                "\u5077\u70b8\u9e21",
                "Steal Chicken",
                CampusPrankPayloadIds.StealFriedChicken,
                CampusRoomType.Canteen,
                CampusPrankSpotVisualKind.Snack,
                new Color(0.95f, 0.52f, 0.22f, 1f)),
            CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                "\u5077\u6c49\u5821",
                "Steal Burger",
                CampusPrankPayloadIds.StealBurger,
                CampusRoomType.Canteen,
                CampusPrankSpotVisualKind.Snack,
                new Color(0.88f, 0.64f, 0.24f, 1f)),
            CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                "\u5077\u5173\u4e1c\u716e",
                "Steal Oden",
                CampusPrankPayloadIds.StealOden,
                CampusRoomType.Canteen,
                CampusPrankSpotVisualKind.Snack,
                new Color(0.78f, 0.48f, 0.9f, 1f)),
            CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                "\u5077\u5916\u5356",
                "Steal Delivery",
                CampusPrankPayloadIds.StealDelivery,
                CampusRoomType.Outdoor,
                CampusPrankSpotVisualKind.DeliveryBox,
                new Color(0.28f, 0.66f, 0.98f, 1f))
        };

        private static CampusRuntimeGameplayMarkerPreset[] LoadPresets()
        {
            if (!CampusRuntimeModPresetStore.TryReadJson("GameplayMarkerPresets.json", out string json))
            {
                return BuiltInPresets;
            }

            try
            {
                GameplayMarkerPresetFile file = JsonUtility.FromJson<GameplayMarkerPresetFile>(json);
                if (file == null || file.Presets == null || file.Presets.Count == 0)
                {
                    return BuiltInPresets;
                }

                List<CampusRuntimeGameplayMarkerPreset> loaded = new List<CampusRuntimeGameplayMarkerPreset>();
                for (int i = 0; i < file.Presets.Count; i++)
                {
                    GameplayMarkerPresetRecord record = file.Presets[i];
                    if (record == null)
                    {
                        continue;
                    }

                    string chinese = string.IsNullOrWhiteSpace(record.ChineseLabel) ? record.EnglishLabel : record.ChineseLabel;
                    string english = string.IsNullOrWhiteSpace(record.EnglishLabel) ? record.ChineseLabel : record.EnglishLabel;
                    if (string.IsNullOrWhiteSpace(chinese) && string.IsNullOrWhiteSpace(english))
                    {
                        continue;
                    }

                    Color color = CampusRuntimeModPresetStore.ParseColor(record.Color, new Color(0.5f, 0.65f, 0.9f, 1f));
                    if (!string.IsNullOrWhiteSpace(record.PrankPayload))
                    {
                        loaded.Add(CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                            chinese,
                            english,
                            record.PrankPayload.Trim(),
                            ParseEnum(record.RequiredRoomType, CampusRoomType.Unknown),
                            ParseEnum(record.VisualKind, CampusPrankSpotVisualKind.Envelope),
                            color));
                        continue;
                    }

                    CampusFacilityType facilityType = ParseEnum(record.FacilityType, CampusFacilityType.Unknown);
                    if (facilityType == CampusFacilityType.Unknown)
                    {
                        continue;
                    }

                    loaded.Add(CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                        chinese,
                        english,
                        facilityType,
                        color));
                }

                return loaded.Count > 0 ? loaded.ToArray() : BuiltInPresets;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusRuntimeGameplayMarkerPresetCatalog] Failed to load GameplayMarkerPresets.json: " + exception.Message);
                return BuiltInPresets;
            }
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Enum.TryParse(value.Trim(), true, out TEnum parsed)
                ? parsed
                : fallback;
        }

        [Serializable]
        private sealed class GameplayMarkerPresetFile
        {
            public List<GameplayMarkerPresetRecord> Presets = new List<GameplayMarkerPresetRecord>();
        }

        [Serializable]
        private sealed class GameplayMarkerPresetRecord
        {
            public string ChineseLabel = string.Empty;
            public string EnglishLabel = string.Empty;
            public string FacilityType = string.Empty;
            public string PrankPayload = string.Empty;
            public string RequiredRoomType = string.Empty;
            public string VisualKind = string.Empty;
            public string Color = string.Empty;
        }
    }
}
