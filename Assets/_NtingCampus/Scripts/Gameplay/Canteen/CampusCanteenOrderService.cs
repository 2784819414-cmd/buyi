using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal static class CampusCanteenOrderService
    {
        public static bool TryPlaceOrder(
            CampusCharacterRuntime actor,
            CampusPlacedObject window,
            CampusCanteenMenuItem menuItem,
            out string message)
        {
            message = string.Empty;
            if (actor == null || window == null)
            {
                return false;
            }

            if (!CampusCanteenServiceWindowAvailability.IsAvailable(window))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowInactiveLog);
                return false;
            }

            if (menuItem == null || string.IsNullOrWhiteSpace(menuItem.ItemDefinitionId))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.MenuItemMissingLog);
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.MenuItemMissingLog);
                return false;
            }

            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            if (registry == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.MenuItemMissingLog);
                return false;
            }

            if (!registry.TryGetDefinition(menuItem.ItemDefinitionId, out StorageItemDefinition definition) ||
                definition == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.MenuItemMissingLog);
                return false;
            }

            int price = ResolvePrice(menuItem);
            CampusEconomyService economyService = ResolveEconomyService();
            if (economyService != null && economyService.GetBalance(actor) < price)
            {
                message = CampusCanteenTextCatalog.Format(CampusCanteenTextId.InsufficientFundsLog, price);
                return false;
            }

            StorageItemModel meal = registry.CreateItem(
                menuItem.ItemDefinitionId,
                actor.CharacterId + ".canteen_order." + menuItem.Id + "." + Guid.NewGuid().ToString("N"));
            if (meal == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.MenuItemMissingLog);
                return false;
            }

            meal.OwnerId = actor.CharacterId;
            meal.LegalState = StorageItemLegalState.Personal;
            meal.SourceLocation = CampusCanteenServedItemPlacement.SourceLocationId;
            meal.SourceRoomId = ResolveRoomId(window);

            if (economyService != null && !economyService.TrySpendMoney(actor, price))
            {
                message = CampusCanteenTextCatalog.Format(CampusCanteenTextId.InsufficientFundsLog, price);
                return false;
            }

            if (!CampusCanteenServedItemPlacement.TryPlace(
                    window,
                    meal,
                    out string spawnError,
                    out _))
            {
                if (economyService != null)
                {
                    economyService.AddMoney(actor, price);
                }

                message = string.IsNullOrWhiteSpace(spawnError)
                    ? CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderFailedLog)
                    : spawnError;
                return false;
            }

            message = menuItem.ResolveOrderedLog(meal);
            return true;
        }

        public static bool TryGetDefinition(
            CampusCanteenMenuItem menuItem,
            out StorageItemDefinition definition)
        {
            definition = null;
            if (menuItem == null || string.IsNullOrWhiteSpace(menuItem.ItemDefinitionId))
            {
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            return registry != null && registry.TryGetDefinition(menuItem.ItemDefinitionId, out definition);
        }

        public static int ResolvePrice(CampusCanteenMenuItem menuItem)
        {
            if (menuItem == null)
            {
                return 0;
            }

            if (menuItem.Price >= 0)
            {
                return Mathf.Max(0, menuItem.Price);
            }

            return TryGetDefinition(menuItem, out StorageItemDefinition definition) && definition != null
                ? Mathf.Max(0, definition.Price)
                : 0;
        }

        public static int ResolveBalance(CampusCharacterRuntime actor)
        {
            CampusEconomyService economyService = ResolveEconomyService();
            return economyService != null ? economyService.GetBalance(actor) : 0;
        }

        private static CampusEconomyService ResolveEconomyService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null ? bootstrap.EconomyService : null;
        }

        private static string ResolveRoomId(CampusPlacedObject placedObject)
        {
            CampusWorldService worldService = CampusGameBootstrap.Instance != null
                ? CampusGameBootstrap.Instance.WorldService
                : null;
            CampusGameplayRoom room = worldService != null && placedObject != null
                ? worldService.FindRoomForPosition(placedObject.FloorIndex, placedObject.transform.position)
                : null;
            return room != null ? room.RoomId : string.Empty;
        }
    }
}
