using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    internal sealed class CampusStoreActions
    {
        private const float ShelfReachDistance = 0.82f;

        private readonly CampusStoreFacts facts;
        private readonly Func<CampusPlacedObject, StorageContainerModel> getOrCreateShelfContainer;
        private readonly Func<CampusCharacterInventory, CampusGameplayRoom, List<StorageItemModel>, int> countUnpaidItems;
        private readonly Action<CampusCharacterRuntime> addCheckedOutActor;
        private readonly Action<int> recordPaidItems;
        private readonly Func<int> currentDay;
        private readonly Action<string> writeLog;

        public CampusStoreActions(
            CampusStoreFacts facts,
            Func<CampusPlacedObject, StorageContainerModel> getOrCreateShelfContainer,
            Func<CampusCharacterInventory, CampusGameplayRoom, List<StorageItemModel>, int> countUnpaidItems,
            Action<CampusCharacterRuntime> addCheckedOutActor,
            Action<int> recordPaidItems,
            Func<int> currentDay,
            Action<string> writeLog)
        {
            this.facts = facts;
            this.getOrCreateShelfContainer = getOrCreateShelfContainer;
            this.countUnpaidItems = countUnpaidItems;
            this.addCheckedOutActor = addCheckedOutActor;
            this.recordPaidItems = recordPaidItems;
            this.currentDay = currentDay;
            this.writeLog = writeLog;
        }

        public bool TryCheckout(
            CampusCharacterRuntime actor,
            CampusPlacedObject checkout,
            out string message)
        {
            message = string.Empty;
            if (actor == null || checkout == null)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.MissingActorOrCheckout);
                return false;
            }

            if (!CampusStoreFacts.IsStoreCheckout(checkout))
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.NotStoreCheckout);
                return false;
            }

            if (facts == null ||
                !facts.TryResolveCheckout(
                    checkout,
                    out CampusGameplayRoom room,
                    out Vector3 checkoutPosition,
                    out string checkoutDisplayName))
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.CheckoutNotInsideStore);
                return false;
            }

            return TryCheckoutAtPosition(actor, room, checkoutPosition, checkoutDisplayName, out message);
        }

        public bool TryTakeOneItemFromShelf(
            CampusCharacterRuntime actor,
            CampusPlacedObject shelf,
            out string message)
        {
            message = string.Empty;
            if (actor == null || shelf == null)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.MissingActorOrShelf);
                return false;
            }

            if (!CampusStoreFacts.IsStoreShelf(shelf))
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.NotStoreShelf);
                return false;
            }

            if (facts == null ||
                !facts.TryResolveShelf(
                    shelf,
                    out CampusGameplayRoom room,
                    out CampusGameplayRoom.FacilityRecord shelfRecord,
                    out Vector3 shelfCustomerPosition))
            {
                CampusGameplayRoom resolvedRoom = facts != null ? facts.ResolveRoomForObject(shelf) : null;
                message = resolvedRoom == null || resolvedRoom.RoomType != CampusRoomType.Store
                    ? CampusCommerceTextCatalog.Get(CampusCommerceTextId.ShelfNotInsideStore)
                    : CampusCommerceTextCatalog.Get(CampusCommerceTextId.NoUsableShelf);
                return false;
            }

            if (Vector2.Distance(actor.transform.position, shelfCustomerPosition) > ShelfReachDistance)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.NotCloseEnoughToShelf);
                return false;
            }

            return TryTakeOneItemFromShelfRecord(actor, room, shelfRecord, out message);
        }

        private bool TryTakeOneItemFromShelfRecord(
            CampusCharacterRuntime actor,
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord shelfRecord,
            out string message)
        {
            message = string.Empty;
            if (actor == null || room == null || shelfRecord == null || shelfRecord.PlacedObject == null)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.MissingActorOrShelf);
                return false;
            }

            StorageContainerModel shelfContainer = getOrCreateShelfContainer?.Invoke(shelfRecord.PlacedObject);
            if (shelfContainer == null || shelfContainer.Items == null || shelfContainer.Items.Count == 0)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.ShelfNoMerchandise);
                return false;
            }

            StorageItemModel item = FindFirstAvailableShelfItem(shelfContainer);
            if (item == null)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.ShelfNoAvailableMerchandise);
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            StorageContainerModel[] targets = BuildCarryTargets(inventory);
            StorageTransferContext context = StorageTransferContext.ForActor(actor.gameObject, StorageTransferReason.Pickup);
            string shelfDisplayName = ResolveContainerDisplayName(shelfContainer);
            context.RoomId = room.RoomId;
            context.OwnerId = shelfContainer.OwnerId;
            context.SourceLocation = shelfDisplayName;

            if (!CampusInventoryActionExecutor.TryTransferItemToFirstFit(
                    actor,
                    item,
                    shelfContainer,
                    targets,
                    context,
                    out StorageTransferResult result))
            {
                message = result.Message;
                return false;
            }

            actor.Data?.AddMemory(CampusCharacterMemoryId.SelectedStoreItem);
            RefreshHeldItemVisual(actor);
            message = result.Message;
            writeLog?.Invoke(CampusCommerceTextCatalog.Format(
                CampusCommerceTextId.TookFromShelfLog,
                FormatName(actor),
                ResolveItemName(item),
                shelfDisplayName));
            return true;
        }

        private bool TryCheckoutAtPosition(
            CampusCharacterRuntime actor,
            CampusGameplayRoom room,
            Vector3 checkoutPosition,
            string checkoutDisplayName,
            out string message)
        {
            message = string.Empty;
            if (actor == null || room == null)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.MissingActorOrStoreRoom);
                return false;
            }

            if (facts == null || !facts.TryFindClerkAtCheckout(room, checkoutPosition, out CampusCharacterRuntime clerk))
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.NoClerkAtCheckout);
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            List<StorageItemModel> unpaidItems = new List<StorageItemModel>();
            int totalPrice = countUnpaidItems != null
                ? countUnpaidItems(inventory, room, unpaidItems)
                : 0;
            if (unpaidItems.Count == 0)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.NoUnpaidItem);
                return false;
            }

            for (int i = 0; i < unpaidItems.Count; i++)
            {
                StorageItemModel item = unpaidItems[i];
                MarkItemPaid(actor, item);
                actor.Data?.AddPossession(
                    item.DefinitionId,
                    ResolveItemName(item),
                    string.IsNullOrWhiteSpace(checkoutDisplayName)
                        ? CampusCommerceTextCatalog.Get(CampusCommerceTextId.StoreCheckout)
                        : checkoutDisplayName,
                    currentDay != null ? currentDay() : 0);
            }

            addCheckedOutActor?.Invoke(actor);
            actor.Data?.AddMemory(CampusCharacterMemoryId.PaidAtStoreCheckout);
            actor.Data?.AddMemory(CampusCharacterMemoryId.ReceivedStorePurchase);
            recordPaidItems?.Invoke(unpaidItems.Count);
            message = CampusCommerceTextCatalog.Format(CampusCommerceTextId.CheckedOut, unpaidItems.Count, totalPrice);
            writeLog?.Invoke(CampusCommerceTextCatalog.Format(
                CampusCommerceTextId.CheckedOutLog,
                FormatName(clerk),
                FormatName(actor),
                unpaidItems.Count,
                totalPrice));
            return true;
        }

        private static void MarkItemPaid(CampusCharacterRuntime actor, StorageItemModel item)
        {
            if (actor == null || item == null)
            {
                return;
            }

            item.LegalState = StorageItemLegalState.Personal;
            item.OwnerId = actor.CharacterId;
            item.SourceContainerId = string.Empty;
            item.SourceRoomId = string.Empty;
            item.SourceLocation = CampusCommerceTextCatalog.Get(CampusCommerceTextId.StoreCheckout);
            item.AllowTaking = true;
            item.StolenDuringSession = false;
            item.SuspicionRisk = 0;
        }

        private static StorageItemModel FindFirstAvailableShelfItem(StorageContainerModel container)
        {
            if (container == null || container.Items == null)
            {
                return null;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null && item.LegalState == StorageItemLegalState.Public)
                {
                    return item;
                }
            }

            return null;
        }

        private static StorageContainerModel[] BuildCarryTargets(CampusCharacterInventory inventory)
        {
            if (inventory == null)
            {
                return Array.Empty<StorageContainerModel>();
            }

            List<StorageContainerModel> containers = new List<StorageContainerModel>();
            AddContainers(containers, inventory.Hands);
            AddContainers(containers, inventory.Pockets);
            if (inventory.Backpack != null)
            {
                containers.Add(inventory.Backpack);
            }

            return containers.ToArray();
        }

        private static void AddContainers(
            List<StorageContainerModel> destination,
            StorageContainerModel[] containers)
        {
            if (destination == null || containers == null)
            {
                return;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                if (containers[i] != null)
                {
                    destination.Add(containers[i]);
                }
            }
        }

        private static void RefreshHeldItemVisual(CampusCharacterRuntime actor)
        {
            CampusHeldItemVisual heldItemVisual = actor != null ? actor.GetComponent<CampusHeldItemVisual>() : null;
            if (heldItemVisual != null)
            {
                heldItemVisual.RefreshImmediate();
            }
        }

        private static string FormatName(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return CampusCommerceTextCatalog.Get(CampusCommerceTextId.UnknownActor);
            }

            return runtime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage);
        }

        private static string ResolveContainerDisplayName(StorageContainerModel container)
        {
            return container != null
                ? container.GetDisplayName(CampusLanguageState.CurrentLanguage)
                : StorageTextCatalog.Get(StorageTextId.ExternalContainer);
        }

        private static string ResolveItemName(StorageItemModel item)
        {
            return CampusInventoryEventPublisher.ResolveItemName(item);
        }
    }
}
