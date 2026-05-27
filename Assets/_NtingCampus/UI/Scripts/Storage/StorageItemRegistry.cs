using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace Nting.Storage
{
    [CreateAssetMenu(menuName = "Nting/Storage/Item Registry", fileName = "StorageItemRegistry")]
    public sealed class StorageItemRegistry : ScriptableObject
    {
        private const string ItemResourcePath = "StorageItems";

        public List<StorageItemDefinition> Items = new();

        private readonly Dictionary<string, StorageItemDefinition> cache = new();
        private bool cacheDirty = true;

        public bool TryGetDefinition(string definitionId, out StorageItemDefinition definition)
        {
            EnsureCache();
            definition = null;
            return !string.IsNullOrWhiteSpace(definitionId) &&
                   cache.TryGetValue(definitionId.Trim(), out definition) &&
                   definition != null;
        }

        public StorageItemModel CreateItem(string definitionId, string instanceId = null)
        {
            if (!TryGetDefinition(definitionId, out StorageItemDefinition definition))
            {
                Debug.LogWarning(StorageTextCatalog.Format(
                    StorageTextId.ItemRegistryMissingDefinition,
                    definitionId));
                return null;
            }

            return definition.CreateItem(instanceId);
        }

        public void RegisterRuntimeDefinition(StorageItemDefinition definition)
        {
            if (definition == null || Items.Contains(definition))
            {
                return;
            }

            Items.Add(definition);
            cacheDirty = true;
        }

        public static StorageItemRegistry CreateDemoRegistry()
        {
            return CreateFallbackRegistry();
        }

        public static StorageItemRegistry CreateFallbackRegistry()
        {
            StorageItemRegistry registry = CreateInstance<StorageItemRegistry>();
            registry.hideFlags = HideFlags.DontSave;
            registry.RegisterRuntimeDefinition(RuntimeDefinition("phone", "手机", "Phone", 1, 2, 0.2f, "屏幕有裂痕的旧手机。", "An old phone with a cracked screen.", new Color(0.25f, 0.38f, 0.47f, 1f)));
            registry.RegisterRuntimeDefinition(RuntimeDefinition("key", "钥匙", "Key", 1, 1, 0.05f, "一把没有挂饰的小钥匙。", "A small key with no keychain.", new Color(0.58f, 0.52f, 0.36f, 1f)));
            registry.RegisterRuntimeDefinition(RuntimeDefinition("note", "纸条", "Note", 1, 1, 0.01f, "折起来的纸条，内容暂时看不清。", "A folded note; its contents are hard to read for now.", new Color(0.48f, 0.48f, 0.42f, 1f)));
            registry.RegisterRuntimeDefinition(RuntimeDefinition("snack", "辣条", "Spicy Snack", 2, 1, 0.16f, "气味明显，最好别放在太显眼的位置。", "Strong-smelling spicy strips. Better not leave them somewhere obvious.", new Color(0.52f, 0.34f, 0.28f, 1f)));
            registry.RegisterRuntimeDefinition(RuntimeDefinition("textbook", "教材", "Textbook", 2, 3, 1.2f, "厚重的主科教材。", "A heavy main-subject textbook.", new Color(0.34f, 0.43f, 0.5f, 1f), "textbook", 9));
            registry.RegisterRuntimeDefinition(RuntimeDefinition("workbook", "练习册", "Workbook", 2, 1, 0.45f, "边角卷起的练习册。", "A workbook with curled corners.", new Color(0.42f, 0.5f, 0.47f, 1f), "workbook", 9));
            registry.RegisterRuntimeDefinition(RuntimeDefinition("pencil_case", "笔袋", "Pencil Case", 2, 1, 0.25f, "普通布面笔袋。", "An ordinary cloth pencil case.", new Color(0.38f, 0.35f, 0.44f, 1f)));
            registry.RegisterRuntimeDefinition(RuntimeDefinition("lunch_box", "饭盒", "Lunch Box", 2, 2, 0.8f, "塑料饭盒，盖子扣得不算紧。", "A plastic lunch box with a loose lid.", new Color(0.45f, 0.42f, 0.34f, 1f)));
            registry.RegisterRuntimeDefinition(RuntimeDefinition("school_backpack", "学生背包", "School Backpack", 1, 1, 0.8f, "可以携带书本和随身物品的学生背包。", "A student backpack for carrying books and personal items.", new Color(0.24f, 0.36f, 0.48f, 1f)));
            return registry;
        }

        private void OnValidate()
        {
            cacheDirty = true;
        }

        private void EnsureCache()
        {
            if (!cacheDirty)
            {
                return;
            }

            cache.Clear();
            for (int i = 0; i < Items.Count; i++)
            {
                AddDefinitionToCache(Items[i]);
            }

            StorageItemDefinition[] resourceDefinitions = Resources.LoadAll<StorageItemDefinition>(ItemResourcePath);
            for (int i = 0; i < resourceDefinitions.Length; i++)
            {
                AddDefinitionToCache(resourceDefinitions[i]);
            }

            cacheDirty = false;
        }

        private void AddDefinitionToCache(StorageItemDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            string key = definition.ResolveId();
            if (!string.IsNullOrWhiteSpace(key))
            {
                cache[key] = definition;
            }
        }

        private static StorageItemDefinition RuntimeDefinition(
            string id,
            string chineseDisplayName,
            string englishDisplayName,
            int width,
            int height,
            float weight,
            string chineseDescription,
            string englishDescription,
            Color themeColor,
            string stackGroupId = "",
            int maxStackSize = 1)
        {
            StorageItemDefinition definition = CreateInstance<StorageItemDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Id = id;
            definition.DisplayName = chineseDisplayName;
            definition.LocalizedDisplayName = new CampusLocalizedText(chineseDisplayName, englishDisplayName);
            definition.Width = width;
            definition.Height = height;
            definition.StackGroupId = stackGroupId;
            definition.MaxStackSize = Mathf.Clamp(maxStackSize, 1, StorageItemStackingService.MaxSupportedStackSize);
            definition.Weight = weight;
            definition.Description = chineseDescription;
            definition.LocalizedDescription = new CampusLocalizedText(chineseDescription, englishDescription);
            definition.ThemeColor = themeColor;
            definition.Icon = StorageItemIconUtility.Resolve(id, width, height);
            return definition;
        }
    }
}
