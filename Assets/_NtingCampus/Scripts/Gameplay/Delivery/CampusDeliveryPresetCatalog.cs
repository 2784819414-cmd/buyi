using System;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Delivery
{
    internal sealed class CampusDeliveryRules
    {
        public string ItemDefinitionId = "delivery_meal_box";
        public string SourceLocationId = "outside_delivery_spot";
        public string SourceContainerPrefix = "delivery_order";
        public string DeliverySpotObjectId = "CampusPrankSpot_StealDelivery";
        public string DeliverySpotInteractionPresetEid = "object.v005.outdoor_delivery_spot";
        public int OrderChancePercent = 35;
        public int MinDeliveryMinutes = 20;
        public int MaxDeliveryMinutes = 60;
        public int PickupScore = 66;
        public int WaitScore = 58;

        public void Normalize()
        {
            ItemDefinitionId = Clean(ItemDefinitionId, "delivery_meal_box");
            SourceLocationId = Clean(SourceLocationId, "outside_delivery_spot");
            SourceContainerPrefix = Clean(SourceContainerPrefix, "delivery_order");
            DeliverySpotObjectId = Clean(DeliverySpotObjectId, "CampusPrankSpot_StealDelivery");
            DeliverySpotInteractionPresetEid = Clean(DeliverySpotInteractionPresetEid, "object.v005.outdoor_delivery_spot");
            OrderChancePercent = Mathf.Clamp(OrderChancePercent, 0, 100);
            MinDeliveryMinutes = Mathf.Clamp(MinDeliveryMinutes, 1, 24 * 60);
            MaxDeliveryMinutes = Mathf.Clamp(MaxDeliveryMinutes, MinDeliveryMinutes, 24 * 60);
            PickupScore = Mathf.Max(0, PickupScore);
            WaitScore = Mathf.Max(0, WaitScore);
        }

        private static string Clean(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }

    internal static class CampusDeliveryPresetCatalog
    {
        private const string PresetFileName = "DeliveryPresets.json";
        private static CampusDeliveryRules cachedRules;

        public static CampusDeliveryRules Rules
        {
            get
            {
                if (cachedRules == null)
                {
                    cachedRules = LoadRules();
                }

                return cachedRules;
            }
        }

        public static CampusDeliveryMealId ResolveUpcomingMeal(int currentMinute)
        {
            int normalizedMinute = CampusTimeSchedule.NormalizeMinuteOfDay(currentMinute);
            if (IsWithinRange(normalizedMinute, 6 * 60 + 30, 7 * 60))
            {
                return CampusDeliveryMealId.Breakfast;
            }

            if (IsWithinRange(normalizedMinute, 10 * 60 + 50, 11 * 60 + 30))
            {
                return CampusDeliveryMealId.Lunch;
            }

            if (IsWithinRange(normalizedMinute, 16 * 60 + 35, 17 * 60 + 15))
            {
                return CampusDeliveryMealId.Dinner;
            }

            return CampusDeliveryMealId.None;
        }

        public static bool IsMealPeakForOrder(CampusDeliveryMealId mealId, CampusTimeSegment segment)
        {
            switch (mealId)
            {
                case CampusDeliveryMealId.Breakfast:
                    return segment == CampusTimeSegment.BreakfastPrep;
                case CampusDeliveryMealId.Lunch:
                    return segment == CampusTimeSegment.LunchBreak;
                case CampusDeliveryMealId.Dinner:
                    return segment == CampusTimeSegment.DinnerBreak;
                default:
                    return false;
            }
        }

        private static CampusDeliveryRules LoadRules()
        {
            CampusDeliveryRules rules = new CampusDeliveryRules();
            if (CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                try
                {
                    CampusDeliveryRules loaded = JsonUtility.FromJson<CampusDeliveryRules>(json);
                    if (loaded != null)
                    {
                        rules = loaded;
                    }
                }
                catch (ArgumentException)
                {
                    rules = new CampusDeliveryRules();
                }
            }

            rules.Normalize();
            return rules;
        }

        private static bool IsWithinRange(int minute, int start, int end)
        {
            return start <= end
                ? minute >= start && minute < end
                : minute >= start || minute < end;
        }
    }
}
