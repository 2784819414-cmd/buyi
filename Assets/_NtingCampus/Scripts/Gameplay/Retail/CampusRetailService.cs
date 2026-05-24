using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Retail
{
    internal readonly struct CampusRetailCheckoutSummary
    {
        public CampusRetailCheckoutSummary(int pendingItemCount, int totalPrice)
        {
            PendingItemCount = Mathf.Max(0, pendingItemCount);
            TotalPrice = Mathf.Max(0, totalPrice);
        }

        public int PendingItemCount { get; }
        public int TotalPrice { get; }
        public bool HasPendingItems => PendingItemCount > 0;
    }

    internal static class CampusRetailService
    {
        public static CampusRetailCheckoutSummary BuildPendingSummary(CampusCharacterRuntime actor)
        {
            return BuildSummary(actor, string.Empty, false);
        }

        public static CampusRetailCheckoutSummary BuildCheckoutSummary(
            CampusCharacterRuntime actor,
            Component checkoutSource)
        {
            return BuildSummary(actor, ResolveStoreRoomId(actor, checkoutSource), true);
        }

        public static bool TryCheckoutActor(
            CampusCharacterRuntime actor,
            Component checkoutSource,
            out string message)
        {
            message = string.Empty;
            if (actor == null)
            {
                return false;
            }

            string storeRoomId = ResolveStoreRoomId(actor, checkoutSource);
            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            CampusRetailCheckoutSummary summary = BuildSummary(inventory, storeRoomId, true);
            if (!summary.HasPendingItems)
            {
                message = CampusRetailTextCatalog.Get(CampusRetailTextId.NoPendingItems);
                return false;
            }

            if (summary.TotalPrice > 0 && !TrySpendForCheckout(actor, summary.TotalPrice))
            {
                message = CampusRetailTextCatalog.Format(CampusRetailTextId.InsufficientFunds, summary.TotalPrice);
                return false;
            }

            ClearPendingItems(inventory.Hands, actor.CharacterId, storeRoomId);
            ClearPendingItems(inventory.Pockets, actor.CharacterId, storeRoomId);
            ClearPendingItem(inventory.Backpack, actor.CharacterId, storeRoomId);

            if (actor.Data != null)
            {
                actor.Data.AddMemory(CampusCharacterMemoryId.ClearedProtectedTransfer);
                actor.Data.AddMemory(CampusCharacterMemoryId.ReceivedClearedGoods);
            }

            message = CampusRetailTextCatalog.Format(CampusRetailTextId.CheckoutComplete, summary.TotalPrice);
            return true;
        }

        public static string ResolveStoreRoomId(
            CampusCharacterRuntime actor,
            Component source)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            CampusPlacedObject placedObject = source != null ? source.GetComponentInParent<CampusPlacedObject>() : null;
            if (worldService != null && placedObject != null)
            {
                CampusGameplayRoom sourceRoom = worldService.FindRoomForPosition(
                    placedObject.FloorIndex,
                    placedObject.transform.position);
                if (sourceRoom != null)
                {
                    return sourceRoom.RoomId;
                }
            }

            if (worldService != null && actor != null)
            {
                CampusGameplayRoom actorRoom = worldService.FindRoomForRuntime(actor);
                if (actorRoom != null)
                {
                    return actorRoom.RoomId;
                }
            }

            return actor != null && actor.Data != null ? actor.Data.CurrentRoomId : string.Empty;
        }

        private static CampusRetailCheckoutSummary BuildSummary(
            CampusCharacterRuntime actor,
            string storeRoomId,
            bool filterByStore)
        {
            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            return BuildSummary(inventory, storeRoomId, filterByStore);
        }

        private static CampusRetailCheckoutSummary BuildSummary(
            CampusCharacterInventory inventory,
            string storeRoomId,
            bool filterByStore)
        {
            if (inventory == null)
            {
                return default;
            }

            string normalizedStoreRoomId = NormalizeStoreRoomId(storeRoomId);
            int pendingItemCount = 0;
            int totalPrice = 0;
            AccumulatePendingSummary(
                inventory.Hands,
                normalizedStoreRoomId,
                filterByStore,
                ref pendingItemCount,
                ref totalPrice);
            AccumulatePendingSummary(
                inventory.Pockets,
                normalizedStoreRoomId,
                filterByStore,
                ref pendingItemCount,
                ref totalPrice);
            AccumulatePendingSummary(
                inventory.Backpack,
                normalizedStoreRoomId,
                filterByStore,
                ref pendingItemCount,
                ref totalPrice);
            return new CampusRetailCheckoutSummary(pendingItemCount, totalPrice);
        }

        private static bool TrySpendForCheckout(CampusCharacterRuntime actor, int totalPrice)
        {
            if (totalPrice <= 0)
            {
                return true;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusEconomyService economyService = bootstrap != null ? bootstrap.EconomyService : null;
            if (economyService != null)
            {
                return economyService.TrySpendMoney(actor, totalPrice);
            }

            return actor != null &&
                   actor.Data != null &&
                   actor.Data.TrySpendMoney(totalPrice);
        }

        private static void AccumulatePendingSummary(
            StorageContainerModel[] containers,
            string storeRoomId,
            bool filterByStore,
            ref int pendingItemCount,
            ref int totalPrice)
        {
            if (containers == null)
            {
                return;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                AccumulatePendingSummary(
                    containers[i],
                    storeRoomId,
                    filterByStore,
                    ref pendingItemCount,
                    ref totalPrice);
            }
        }

        private static void AccumulatePendingSummary(
            StorageContainerModel container,
            string storeRoomId,
            bool filterByStore,
            ref int pendingItemCount,
            ref int totalPrice)
        {
            if (container == null || container.Items == null)
            {
                return;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item == null ||
                    !item.IsPendingProtectedTransfer ||
                    !MatchesStore(item, storeRoomId, filterByStore))
                {
                    continue;
                }

                pendingItemCount++;
                totalPrice += Mathf.Max(0, item.Price);
            }
        }

        private static int ClearPendingItems(
            StorageContainerModel[] containers,
            string actorId,
            string storeRoomId)
        {
            int clearedCount = 0;
            if (containers == null)
            {
                return clearedCount;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                clearedCount += ClearPendingItem(containers[i], actorId, storeRoomId);
            }

            return clearedCount;
        }

        private static int ClearPendingItem(
            StorageContainerModel container,
            string actorId,
            string storeRoomId)
        {
            if (container == null || container.Items == null)
            {
                return 0;
            }

            int clearedCount = 0;
            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item == null ||
                    !item.IsPendingProtectedTransfer ||
                    !MatchesStore(item, storeRoomId, true))
                {
                    continue;
                }

                CampusProtectedTransferState.ClearPendingTransfer(item, actorId);
                clearedCount++;
            }

            return clearedCount;
        }

        private static bool MatchesStore(
            StorageItemModel item,
            string storeRoomId,
            bool filterByStore)
        {
            if (item == null)
            {
                return false;
            }

            if (!filterByStore || string.IsNullOrEmpty(storeRoomId))
            {
                return true;
            }

            return string.Equals(
                item.SourceRoomId ?? string.Empty,
                storeRoomId,
                System.StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStoreRoomId(string storeRoomId)
        {
            return string.IsNullOrWhiteSpace(storeRoomId) ? string.Empty : storeRoomId.Trim();
        }
    }
}
