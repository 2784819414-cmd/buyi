using System;
using Nting.Storage;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    public sealed class CampusCanteenDishFactory
    {
        public StorageItemModel CreateServedDish(
            StorageMemory memory,
            CampusCanteenDishDefinition dish,
            string ownerId,
            string sourceLocation)
        {
            StorageItemModel item = CreateDish(memory, dish);
            if (item == null)
            {
                return null;
            }

            item.OwnerId = string.IsNullOrWhiteSpace(ownerId) ? string.Empty : ownerId.Trim();
            item.SourceLocation = sourceLocation;
            item.LegalState = StorageItemLegalState.Personal;
            item.AllowTaking = true;
            return item;
        }

        public StorageItemModel CreateStockDish(
            StorageMemory memory,
            CampusCanteenDishDefinition dish,
            CampusCanteenStation station)
        {
            StorageItemModel item = CreateDish(memory, dish);
            if (item == null)
            {
                return null;
            }

            item.OwnerId = station != null ? station.StationId : "canteen";
            item.SourceLocation = station != null ? station.DisplayName : CampusCanteenTextCatalog.Get(CampusCanteenTextId.CanteenFallback);
            item.LegalState = StorageItemLegalState.Public;
            item.AllowTaking = false;
            item.SuspicionRisk = dish != null ? dish.SuspicionRisk : 12;
            return item;
        }

        private StorageItemModel CreateDish(StorageMemory memory, CampusCanteenDishDefinition dish)
        {
            if (memory == null || dish == null)
            {
                return null;
            }

            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            EnsureDefinition(registry, dish);
            string definitionId = dish.ResolveStorageDefinitionId();
            StorageItemModel item = registry.CreateItem(definitionId, definitionId + "_" + Guid.NewGuid().ToString("N"));
            if (item == null)
            {
                return null;
            }

            item.DisplayName = dish.ResolveDisplayName(CampusDisplayLanguage.Chinese);
            item.LocalizedDisplayName = dish.LocalizedDisplayName;
            item.Description = dish.ResolveDescription(CampusDisplayLanguage.Chinese);
            item.LocalizedDescription = dish.LocalizedDescription;
            item.Width = Mathf.Max(1, dish.Width);
            item.Height = Mathf.Max(1, dish.Height);
            item.Weight = Mathf.Max(0f, dish.Weight);
            item.ThemeColor = dish.ThemeColor;
            item.Icon = StorageItemIconUtility.Resolve(definitionId, dish.Icon);
            item.IsUsable = true;
            item.UseActionId = StorageItemUseUtility.ConsumeFoodActionId;
            item.ConsumeOnUse = true;
            item.UseText = FormatUseText(CampusDisplayLanguage.Chinese, dish);
            item.LocalizedUseText = new CampusLocalizedText(
                item.UseText,
                FormatUseText(CampusDisplayLanguage.English, dish));
            item.SuspicionRisk = Mathf.Max(0, dish.SuspicionRisk);
            return item;
        }

        private static void EnsureDefinition(StorageItemRegistry registry, CampusCanteenDishDefinition dish)
        {
            if (registry == null || dish == null)
            {
                return;
            }

            string definitionId = dish.ResolveStorageDefinitionId();
            if (registry.TryGetDefinition(definitionId, out _))
            {
                return;
            }

            StorageItemDefinition definition = ScriptableObject.CreateInstance<StorageItemDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Id = definitionId;
            definition.DisplayName = dish.ResolveDisplayName(CampusDisplayLanguage.Chinese);
            definition.LocalizedDisplayName = dish.LocalizedDisplayName;
            definition.Width = Mathf.Max(1, dish.Width);
            definition.Height = Mathf.Max(1, dish.Height);
            definition.Weight = Mathf.Max(0f, dish.Weight);
            definition.Description = dish.ResolveDescription(CampusDisplayLanguage.Chinese);
            definition.LocalizedDescription = dish.LocalizedDescription;
            definition.ThemeColor = dish.ThemeColor;
            definition.Icon = StorageItemIconUtility.Resolve(definitionId, dish.Icon);
            definition.IsUsable = true;
            definition.UseActionId = StorageItemUseUtility.ConsumeFoodActionId;
            definition.ConsumeOnUse = true;
            definition.UseText = FormatUseText(CampusDisplayLanguage.Chinese, dish);
            definition.LocalizedUseText = new CampusLocalizedText(
                definition.UseText,
                FormatUseText(CampusDisplayLanguage.English, dish));
            registry.RegisterRuntimeDefinition(definition);
        }

        private static string FormatUseText(CampusDisplayLanguage language, CampusCanteenDishDefinition dish)
        {
            string name = dish != null ? dish.ResolveDisplayName(language) : StorageTextCatalog.Get(language, StorageTextId.ItemFallback);
            return string.Format(
                CampusCanteenTextCatalog.Get(language, CampusCanteenTextId.DishUseText),
                name);
        }
    }
}
