using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenMenuItem
    {
        public readonly string Id;
        public readonly string ItemDefinitionId;
        public readonly int Price;
        public readonly CampusLocalizedText DisplayName;
        public readonly CampusLocalizedText PromptText;
        public readonly CampusLocalizedText OrderedLogText;

        public CampusCanteenMenuItem(
            string id,
            string itemDefinitionId,
            int price,
            CampusLocalizedText displayName,
            CampusLocalizedText promptText,
            CampusLocalizedText orderedLogText)
        {
            Id = NormalizeId(id);
            ItemDefinitionId = NormalizeId(itemDefinitionId);
            Price = price;
            DisplayName = displayName;
            PromptText = promptText;
            OrderedLogText = orderedLogText;
        }

        public string ResolveDisplayName(StorageItemDefinition definition)
        {
            return DisplayName.Current(
                definition != null ? definition.ResolveDisplayName(CampusLanguageState.CurrentLanguage) : string.Empty,
                Id,
                ItemDefinitionId);
        }

        public string ResolvePrompt()
        {
            return PromptText.Current(Id, ItemDefinitionId);
        }

        public string ResolveOrderedLog(StorageItemModel item)
        {
            string itemName = item != null ? item.GetDisplayName() : ItemDefinitionId;
            string template = OrderedLogText.Current();
            return string.IsNullOrWhiteSpace(template)
                ? CampusCanteenTextCatalog.Format(CampusCanteenTextId.OrderedMenuItemLog, itemName)
                : string.Format(template, itemName);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal static class CampusCanteenMenuCatalog
    {
        private const string PresetFileName = "CanteenMenuPresets.json";
        private const string MigrationDefaultMenuItemId = "legacy_lunch_box";
        private const string MigrationDefaultItemDefinitionId = "lunch_box";

        private static MenuData data;

        public static bool TryResolve(string menuItemId, out CampusCanteenMenuItem menuItem)
        {
            EnsureLoaded();
            string normalizedId = NormalizeId(menuItemId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                normalizedId = data.DefaultItemId;
            }

            if (!string.IsNullOrEmpty(normalizedId) &&
                data.Items.TryGetValue(normalizedId, out menuItem))
            {
                return true;
            }

            menuItem = null;
            return false;
        }

        public static CampusCanteenMenuItem ResolveDefault()
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(data.DefaultItemId) &&
                data.Items.TryGetValue(data.DefaultItemId, out CampusCanteenMenuItem menuItem))
            {
                return menuItem;
            }

            return CreateMigrationDefault();
        }

        public static CampusCanteenMenuItem[] GetMenuItems()
        {
            EnsureLoaded();
            List<CampusCanteenMenuItem> items = new List<CampusCanteenMenuItem>();
            for (int i = 0; i < data.OrderedIds.Count; i++)
            {
                string id = data.OrderedIds[i];
                if (data.Items.TryGetValue(id, out CampusCanteenMenuItem item) && item != null)
                {
                    items.Add(item);
                }
            }

            return items.ToArray();
        }

        private static void EnsureLoaded()
        {
            if (data != null)
            {
                return;
            }

            data = LoadData();
        }

        private static MenuData LoadData()
        {
            MenuData loadedData = new MenuData();
            if (!CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                Debug.LogWarning("[CampusCanteenMenuCatalog] Missing menu preset file: " + PresetFileName);
                loadedData.Add(CreateMigrationDefault());
                loadedData.DefaultItemId = MigrationDefaultMenuItemId;
                return loadedData;
            }

            try
            {
                CanteenMenuPresetFile file = JsonUtility.FromJson<CanteenMenuPresetFile>(json);
                ParseFile(file, loadedData);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusCanteenMenuCatalog] Failed to parse " + PresetFileName + ": " + exception.Message);
            }

            if (loadedData.Items.Count == 0)
            {
                loadedData.Add(CreateMigrationDefault());
                loadedData.DefaultItemId = MigrationDefaultMenuItemId;
            }

            if (string.IsNullOrEmpty(loadedData.DefaultItemId) ||
                !loadedData.Items.ContainsKey(loadedData.DefaultItemId))
            {
                foreach (string key in loadedData.Items.Keys)
                {
                    loadedData.DefaultItemId = key;
                    break;
                }
            }

            return loadedData;
        }

        private static void ParseFile(CanteenMenuPresetFile file, MenuData target)
        {
            if (file == null || target == null)
            {
                return;
            }

            target.DefaultItemId = NormalizeId(file.DefaultItemId);
            if (file.MenuItems == null)
            {
                return;
            }

            for (int i = 0; i < file.MenuItems.Count; i++)
            {
                CampusCanteenMenuItem item = ParseItem(file.MenuItems[i]);
                if (item != null)
                {
                    target.Add(item);
                }
            }
        }

        private static CampusCanteenMenuItem ParseItem(CanteenMenuItemRecord record)
        {
            string id = NormalizeId(record != null ? record.Id : string.Empty);
            string itemDefinitionId = NormalizeId(record != null ? record.ItemDefinitionId : string.Empty);
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(itemDefinitionId))
            {
                return null;
            }

            return new CampusCanteenMenuItem(
                id,
                itemDefinitionId,
                record.Price,
                ToLocalizedText(record.DisplayName),
                ToLocalizedText(record.PromptText),
                ToLocalizedText(record.OrderedLogText));
        }

        private static CampusLocalizedText ToLocalizedText(LocalizedTextRecord record)
        {
            return record == null
                ? new CampusLocalizedText(string.Empty, string.Empty)
                : new CampusLocalizedText(record.Chinese, record.English);
        }

        private static CampusCanteenMenuItem CreateMigrationDefault()
        {
            return new CampusCanteenMenuItem(
                MigrationDefaultMenuItemId,
                MigrationDefaultItemDefinitionId,
                8,
                new CampusLocalizedText("\u4fbf\u5f53", "Lunch Box"),
                new CampusLocalizedText("\u70b9\u9910", "Order Meal"),
                new CampusLocalizedText("\u5df2\u53d6\u5230\u9910\u54c1\u3002", "Meal received."));
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private sealed class MenuData
        {
            public string DefaultItemId = string.Empty;
            public readonly List<string> OrderedIds = new List<string>();
            public readonly Dictionary<string, CampusCanteenMenuItem> Items =
                new Dictionary<string, CampusCanteenMenuItem>(StringComparer.OrdinalIgnoreCase);

            public void Add(CampusCanteenMenuItem item)
            {
                if (item == null || string.IsNullOrEmpty(item.Id))
                {
                    return;
                }

                Items[item.Id] = item;
                if (!OrderedIds.Contains(item.Id))
                {
                    OrderedIds.Add(item.Id);
                }
            }
        }

        [Serializable]
        private sealed class CanteenMenuPresetFile
        {
            public string DefaultItemId = string.Empty;
            public List<CanteenMenuItemRecord> MenuItems = new List<CanteenMenuItemRecord>();
        }

        [Serializable]
        private sealed class CanteenMenuItemRecord
        {
            public string Id = string.Empty;
            public string ItemDefinitionId = string.Empty;
            public int Price = -1;
            public LocalizedTextRecord DisplayName = new LocalizedTextRecord();
            public LocalizedTextRecord PromptText = new LocalizedTextRecord();
            public LocalizedTextRecord OrderedLogText = new LocalizedTextRecord();
        }

        [Serializable]
        private sealed class LocalizedTextRecord
        {
            public string Chinese = string.Empty;
            public string English = string.Empty;
        }
    }
}
