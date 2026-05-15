using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    [CreateAssetMenu(menuName = "Nting/Storage/Item Registry", fileName = "StorageItemRegistry")]
    public sealed class StorageItemRegistry : ScriptableObject
    {
        public List<StorageItemDefinition> Items = new List<StorageItemDefinition>();

        private readonly Dictionary<string, StorageItemDefinition> cache = new Dictionary<string, StorageItemDefinition>();
        private bool cacheDirty = true;

        public bool TryGetDefinition(string definitionId, out StorageItemDefinition definition)
        {
            EnsureCache();
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                definition = null;
                return false;
            }

            return cache.TryGetValue(definitionId, out definition) && definition != null;
        }

        public StorageItemModel CreateItem(string definitionId, string instanceId = null)
        {
            if (!TryGetDefinition(definitionId, out StorageItemDefinition definition))
            {
                Debug.LogWarning("Storage item registry failed: missing item definition '" + definitionId + "'.");
                return null;
            }

            return definition.CreateItem(instanceId);
        }

        public void RegisterRuntimeDefinition(StorageItemDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            if (!Items.Contains(definition))
            {
                Items.Add(definition);
            }

            cacheDirty = true;
        }

        public static StorageItemRegistry CreateDemoRegistry()
        {
            StorageItemRegistry registry = CreateInstance<StorageItemRegistry>();
            registry.hideFlags = HideFlags.DontSave;
            registry.RegisterRuntimeDefinition(CreateRuntimeDefinition("phone", "手机", 1, 2, 0.2f, "屏幕有裂痕的旧手机。", new Color(0.25f, 0.38f, 0.47f, 1f)));
            registry.RegisterRuntimeDefinition(CreateRuntimeDefinition("key", "钥匙", 1, 1, 0.05f, "一把没有挂饰的小钥匙。", new Color(0.58f, 0.52f, 0.36f, 1f)));
            registry.RegisterRuntimeDefinition(CreateRuntimeDefinition("note", "纸条", 1, 1, 0.01f, "折起来的纸条，内容暂时看不清。", new Color(0.48f, 0.48f, 0.42f, 1f)));
            registry.RegisterRuntimeDefinition(CreateRuntimeDefinition("snack", "辣条", 2, 1, 0.16f, "气味明显，最好别放在太显眼的位置。", new Color(0.52f, 0.34f, 0.28f, 1f)));
            registry.RegisterRuntimeDefinition(CreateRuntimeDefinition("textbook", "教材", 2, 3, 1.2f, "厚重的主科教材。", new Color(0.34f, 0.43f, 0.5f, 1f)));
            registry.RegisterRuntimeDefinition(CreateRuntimeDefinition("lunch_box", "饭盒", 2, 2, 0.8f, "塑料饭盒，盖子扣得不算紧。", new Color(0.45f, 0.42f, 0.34f, 1f)));
            registry.RegisterRuntimeDefinition(CreateRuntimeDefinition("workbook", "练习册", 2, 1, 0.45f, "边角卷起的练习册。", new Color(0.42f, 0.5f, 0.47f, 1f)));
            registry.RegisterRuntimeDefinition(CreateRuntimeDefinition("pencil_case", "笔袋", 2, 1, 0.25f, "普通布面笔袋。", new Color(0.38f, 0.35f, 0.44f, 1f)));
            return registry;
        }

        private static StorageItemDefinition CreateRuntimeDefinition(
            string id,
            string displayName,
            int width,
            int height,
            float weight,
            string description,
            Color themeColor)
        {
            StorageItemDefinition definition = CreateInstance<StorageItemDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Id = id;
            definition.DisplayName = displayName;
            definition.Width = width;
            definition.Height = height;
            definition.Weight = weight;
            definition.Description = description;
            definition.ThemeColor = themeColor;
            return definition;
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
                StorageItemDefinition definition = Items[i];
                if (definition == null)
                {
                    continue;
                }

                string key = string.IsNullOrWhiteSpace(definition.Id) ? definition.name : definition.Id;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                cache[key] = definition;
            }

            cacheDirty = false;
        }
    }
}
