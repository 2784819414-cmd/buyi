using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    [CreateAssetMenu(menuName = "Nting/Canteen/Menu Profile", fileName = "CampusCanteenMenuProfile")]
    public sealed class CampusCanteenMenuProfile : ScriptableObject
    {
        public List<CampusCanteenDishDefinition> Dishes = new List<CampusCanteenDishDefinition>();

        public IReadOnlyList<CampusCanteenDishDefinition> Items => Dishes;

        public bool TryGetDish(string dishId, out CampusCanteenDishDefinition dish)
        {
            dish = null;
            string normalized = NormalizeId(dishId);
            if (string.IsNullOrEmpty(normalized) || Dishes == null)
            {
                return false;
            }

            for (int i = 0; i < Dishes.Count; i++)
            {
                CampusCanteenDishDefinition candidate = Dishes[i];
                if (candidate != null && NormalizeId(candidate.ResolveDishId()) == normalized)
                {
                    dish = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetDishForWindow(
            string dishId,
            string windowTypeId,
            out CampusCanteenDishDefinition dish)
        {
            if (!TryGetDish(dishId, out dish))
            {
                return false;
            }

            return CanServeDishAtWindow(dish, windowTypeId);
        }

        public bool CanServeDishAtWindow(CampusCanteenDishDefinition dish, string windowTypeId)
        {
            if (dish == null)
            {
                return false;
            }

            string dishWindow = NormalizeId(dish.ResolveWindowTypeId());
            string stationWindow = NormalizeId(windowTypeId);
            return string.IsNullOrEmpty(dishWindow) ||
                   dishWindow == "generic" ||
                   string.IsNullOrEmpty(stationWindow) ||
                   stationWindow == "generic" ||
                   dishWindow == stationWindow;
        }

        public CampusCanteenDishDefinition FirstDish()
        {
            if (Dishes == null)
            {
                return null;
            }

            for (int i = 0; i < Dishes.Count; i++)
            {
                if (Dishes[i] != null)
                {
                    return Dishes[i];
                }
            }

            return null;
        }

        public static CampusCanteenMenuProfile CreateFallback()
        {
            CampusCanteenMenuProfile profile = CreateInstance<CampusCanteenMenuProfile>();
            profile.hideFlags = HideFlags.DontSave;
            profile.Dishes.Add(CampusCanteenDishDefinition.CreateRuntime(
                "canteen_fried_chicken",
                "炸鸡",
                "Fried Chicken",
                2,
                1,
                0.42f,
                12,
                2.4f,
                18,
                new Color(0.63f, 0.42f, 0.22f, 1f)));
            profile.Dishes.Add(CampusCanteenDishDefinition.CreateRuntime(
                "canteen_burger",
                "汉堡",
                "Burger",
                2,
                1,
                0.36f,
                10,
                2.0f,
                15,
                new Color(0.48f, 0.34f, 0.22f, 1f)));
            profile.Dishes.Add(CampusCanteenDishDefinition.CreateRuntime(
                "canteen_oden",
                "关东煮",
                "Oden",
                1,
                2,
                0.5f,
                9,
                2.8f,
                14,
                new Color(0.42f, 0.49f, 0.35f, 1f)));
            return profile;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
