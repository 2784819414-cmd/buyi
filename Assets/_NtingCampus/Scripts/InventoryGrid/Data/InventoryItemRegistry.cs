using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.InventoryGrid
{
    [CreateAssetMenu(menuName = "Nting Campus/Inventory Grid/Item Registry", fileName = "InventoryItemRegistry")]
    public sealed class InventoryItemRegistry : ScriptableObject
    {
        public List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

        private readonly Dictionary<string, ItemDefinition> lookup = new Dictionary<string, ItemDefinition>();
        private bool lookupDirty = true;

        public ItemDefinition FindItemDefinition(string itemDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(itemDefinitionId))
            {
                Debug.LogWarning("Inventory item lookup failed: itemDefinitionId is empty.");
                return null;
            }

            RebuildLookupIfNeeded();
            if (lookup.TryGetValue(itemDefinitionId, out ItemDefinition definition))
            {
                return definition;
            }

            Debug.LogWarning("Inventory item lookup failed: missing definition '" + itemDefinitionId + "'.");
            return null;
        }

        public void Register(ItemDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.itemId))
            {
                return;
            }

            if (!itemDefinitions.Contains(definition))
            {
                itemDefinitions.Add(definition);
            }

            lookupDirty = true;
        }

        private void OnValidate()
        {
            lookupDirty = true;
        }

        private void RebuildLookupIfNeeded()
        {
            if (!lookupDirty)
            {
                return;
            }

            lookup.Clear();
            for (int i = 0; i < itemDefinitions.Count; i++)
            {
                ItemDefinition definition = itemDefinitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.itemId))
                {
                    continue;
                }

                lookup[definition.itemId] = definition;
            }

            lookupDirty = false;
        }
    }
}
