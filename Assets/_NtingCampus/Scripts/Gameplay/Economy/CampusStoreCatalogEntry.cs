using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    [Serializable]
    public sealed class CampusStoreCatalogEntry
    {
        public string CategoryId = "general";
        public string DefinitionId = "snack";
        public string DisplayNameOverride = string.Empty;
        public CampusLocalizedText LocalizedDisplayNameOverride;
        [Min(0)] public int Price = 1;

        public string ResolveCategoryId()
        {
            return string.IsNullOrWhiteSpace(CategoryId) ? "general" : CategoryId.Trim();
        }

        public string ResolveDefinitionId()
        {
            return string.IsNullOrWhiteSpace(DefinitionId) ? string.Empty : DefinitionId.Trim();
        }

        public bool HasDisplayNameOverride()
        {
            return LocalizedDisplayNameOverride.HasAnyText || !string.IsNullOrWhiteSpace(DisplayNameOverride);
        }

        public string ResolveDisplayNameOverride(CampusDisplayLanguage language)
        {
            return LocalizedDisplayNameOverride.Get(language, DisplayNameOverride);
        }

        public bool MatchesCategory(string categoryId)
        {
            string requestedCategory = string.IsNullOrWhiteSpace(categoryId) ? "general" : categoryId.Trim();
            return string.Equals(requestedCategory, "general", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ResolveCategoryId(), requestedCategory, StringComparison.OrdinalIgnoreCase);
        }

        public void Normalize()
        {
            CategoryId = ResolveCategoryId();
            DefinitionId = ResolveDefinitionId();
            Price = Mathf.Max(0, Price);
            DisplayNameOverride = string.IsNullOrWhiteSpace(DisplayNameOverride) ? string.Empty : DisplayNameOverride.Trim();
        }

        public static CampusStoreCatalogEntry CreateLoose(string categoryId, string definitionId)
        {
            return new CampusStoreCatalogEntry
            {
                CategoryId = string.IsNullOrWhiteSpace(categoryId) ? "general" : categoryId.Trim(),
                DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? string.Empty : definitionId.Trim(),
                Price = 0
            };
        }

        public static List<CampusStoreCatalogEntry> CreateDefaultCatalog()
        {
            return new List<CampusStoreCatalogEntry>
            {
                new CampusStoreCatalogEntry { CategoryId = "snack", DefinitionId = "snack", Price = 6 },
                new CampusStoreCatalogEntry { CategoryId = "snack", DefinitionId = "lunch_box", Price = 12 },
                new CampusStoreCatalogEntry { CategoryId = "stationery", DefinitionId = "note", Price = 1 },
                new CampusStoreCatalogEntry { CategoryId = "stationery", DefinitionId = "pencil_case", Price = 8 },
                new CampusStoreCatalogEntry { CategoryId = "stationery", DefinitionId = "workbook", Price = 10 },
                new CampusStoreCatalogEntry { CategoryId = "book", DefinitionId = "textbook", Price = 18 },
                new CampusStoreCatalogEntry { CategoryId = "book", DefinitionId = "workbook", Price = 10 },
                new CampusStoreCatalogEntry { CategoryId = "electronics", DefinitionId = "phone", Price = 120 }
            };
        }
    }
}
