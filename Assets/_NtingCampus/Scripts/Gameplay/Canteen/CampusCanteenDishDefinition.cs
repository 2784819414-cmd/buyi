using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    [CreateAssetMenu(menuName = "Nting/Canteen/Dish Definition", fileName = "CampusCanteenDishDefinition")]
    public sealed class CampusCanteenDishDefinition : ScriptableObject
    {
        public string DishId;
        public string StorageDefinitionId;
        public string DisplayName;
        public CampusLocalizedText LocalizedDisplayName;
        public string WindowTypeId = "generic";
        [TextArea]
        public string Description;
        public CampusLocalizedText LocalizedDescription;
        public int Width = 2;
        public int Height = 1;
        public float Weight = 0.35f;
        public int Price = 8;
        public float PrepareSeconds = 2.2f;
        public int SuspicionRisk = 14;
        public Color ThemeColor = new Color(0.55f, 0.42f, 0.27f, 1f);
        public Sprite Icon;
        public bool StockedInTray = true;

        public string ResolveDishId()
        {
            return string.IsNullOrWhiteSpace(DishId) ? name : DishId.Trim();
        }

        public string ResolveStorageDefinitionId()
        {
            return string.IsNullOrWhiteSpace(StorageDefinitionId) ? ResolveDishId() : StorageDefinitionId.Trim();
        }

        public string ResolveDisplayName()
        {
            return ResolveDisplayName(CampusLanguageState.CurrentLanguage);
        }

        public string ResolveDisplayName(CampusDisplayLanguage language)
        {
            string id = ResolveDishId();
            return LocalizedDisplayName.Get(language, DisplayName, id);
        }

        public string ResolveDescription(CampusDisplayLanguage language)
        {
            return LocalizedDescription.Get(language, Description);
        }

        public string ResolveWindowTypeId()
        {
            return string.IsNullOrWhiteSpace(WindowTypeId) ? "generic" : WindowTypeId.Trim();
        }

        public static CampusCanteenDishDefinition CreateRuntime(
            string dishId,
            string chineseDisplayName,
            string englishDisplayName,
            int width,
            int height,
            float weight,
            int price,
            float prepareSeconds,
            int suspicionRisk,
            Color themeColor,
            string windowTypeId = "generic")
        {
            CampusCanteenDishDefinition dish = CreateInstance<CampusCanteenDishDefinition>();
            dish.hideFlags = HideFlags.DontSave;
            dish.DishId = dishId;
            dish.StorageDefinitionId = dishId;
            dish.DisplayName = chineseDisplayName;
            dish.LocalizedDisplayName = new CampusLocalizedText(chineseDisplayName, englishDisplayName);
            dish.WindowTypeId = string.IsNullOrWhiteSpace(windowTypeId) ? "generic" : windowTypeId.Trim();
            dish.Description = string.Format(
                CampusCanteenTextCatalog.Get(CampusDisplayLanguage.Chinese, CampusCanteenTextId.DishDescription),
                chineseDisplayName);
            dish.LocalizedDescription = new CampusLocalizedText(
                dish.Description,
                string.Format(
                    CampusCanteenTextCatalog.Get(CampusDisplayLanguage.English, CampusCanteenTextId.DishDescription),
                    englishDisplayName));
            dish.Width = Mathf.Max(1, width);
            dish.Height = Mathf.Max(1, height);
            dish.Weight = Mathf.Max(0f, weight);
            dish.Price = Mathf.Max(0, price);
            dish.PrepareSeconds = Mathf.Max(0.1f, prepareSeconds);
            dish.SuspicionRisk = Mathf.Max(0, suspicionRisk);
            dish.ThemeColor = themeColor;
            dish.StockedInTray = true;
            return dish;
        }
    }
}
