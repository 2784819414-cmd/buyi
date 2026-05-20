using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    internal readonly struct CampusStoreAuditResult
    {
        public CampusStoreAuditResult(int unpaidItemCount, int theftCount)
        {
            UnpaidItemCount = Mathf.Max(0, unpaidItemCount);
            TheftCount = Mathf.Max(0, theftCount);
        }

        public int UnpaidItemCount { get; }
        public int TheftCount { get; }
    }

    internal sealed class CampusStoreAuditService
    {
        private readonly CampusStoreStockService stock;
        private readonly Func<int> resolveDefaultStoreSuspicionRisk;
        private readonly Action<string> writeLog;

        private CampusRosterService rosterService;
        private CampusWorldService worldService;
        private CampusGameplayEventHub gameplayEventHub;

        public CampusStoreAuditService(
            CampusStoreStockService stock,
            Func<int> resolveDefaultStoreSuspicionRisk,
            Action<string> writeLog)
        {
            this.stock = stock;
            this.resolveDefaultStoreSuspicionRisk = resolveDefaultStoreSuspicionRisk;
            this.writeLog = writeLog;
        }

        public void SetContext(
            CampusRosterService targetRosterService,
            CampusWorldService targetWorldService,
            CampusGameplayEventHub targetGameplayEventHub)
        {
            rosterService = targetRosterService;
            worldService = targetWorldService;
            gameplayEventHub = targetGameplayEventHub;
        }

        public bool IsUnpaidStoreItem(StorageItemModel item)
        {
            return stock != null && stock.IsUnpaidStoreItem(item);
        }

        public bool ActorHasUnpaidStoreItems(CampusCharacterRuntime actor)
        {
            if (actor == null)
            {
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            return CountUnpaidItems(inventory, null, null) > 0;
        }

        public int CountUnpaidItems(
            CampusCharacterInventory inventory,
            CampusGameplayRoom storeRoom,
            List<StorageItemModel> destination)
        {
            int totalPrice = 0;
            if (inventory == null || stock == null)
            {
                return 0;
            }

            StorageContainerModel[] containers = BuildCarryTargets(inventory);
            for (int containerIndex = 0; containerIndex < containers.Length; containerIndex++)
            {
                StorageContainerModel container = containers[containerIndex];
                if (container == null || container.Items == null)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < container.Items.Count; itemIndex++)
                {
                    StorageItemModel item = container.Items[itemIndex];
                    if (!stock.IsUnpaidStoreItem(item) || !stock.MatchesStoreRoom(item, storeRoom))
                    {
                        continue;
                    }

                    destination?.Add(item);
                    totalPrice += stock.ResolveItemPrice(item);
                }
            }

            return totalPrice;
        }

        public CampusStoreAuditResult AuditUnpaidItems()
        {
            if (rosterService == null)
            {
                return new CampusStoreAuditResult(0, 0);
            }

            int unpaidCount = 0;
            int theftCount = 0;
            IReadOnlyList<CampusCharacterRuntime> runtimes = rosterService.Runtimes;
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime == null)
                {
                    continue;
                }

                CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(runtime, false);
                CampusGameplayRoom currentRoom = worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
                AuditInventoryForUnpaidStoreItems(runtime, inventory, currentRoom, ref unpaidCount, ref theftCount);
            }

            return new CampusStoreAuditResult(unpaidCount, theftCount);
        }

        private void AuditInventoryForUnpaidStoreItems(
            CampusCharacterRuntime actor,
            CampusCharacterInventory inventory,
            CampusGameplayRoom currentRoom,
            ref int unpaidCount,
            ref int theftCount)
        {
            if (inventory == null || stock == null)
            {
                return;
            }

            StorageContainerModel[] containers = BuildCarryTargets(inventory);
            for (int i = 0; i < containers.Length; i++)
            {
                StorageContainerModel container = containers[i];
                if (container == null || container.Items == null)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < container.Items.Count; itemIndex++)
                {
                    StorageItemModel item = container.Items[itemIndex];
                    if (!stock.IsUnpaidStoreItem(item))
                    {
                        continue;
                    }

                    unpaidCount++;
                    if (stock.IsStillInsideSourceStore(item, currentRoom))
                    {
                        continue;
                    }

                    if (MarkUnpaidItemAsStolen(actor, item, currentRoom))
                    {
                        theftCount++;
                    }
                }
            }
        }

        private bool MarkUnpaidItemAsStolen(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            CampusGameplayRoom currentRoom)
        {
            if (actor == null || item == null || item.LegalState == StorageItemLegalState.Stolen)
            {
                return false;
            }

            string ownerId = item.OwnerId;
            string sourceRoomId = item.SourceRoomId;
            string sourceContainerId = item.SourceContainerId;
            string targetContainerId = item.CurrentContainerId;
            int risk = Mathf.Max(ResolveDefaultStoreSuspicionRisk(), item.SuspicionRisk);
            item.EnsureEvidence().MarkAsStolen(null, sourceRoomId, ownerId, item.SourceLocation, risk);
            gameplayEventHub?.PublishProtectedItemMoved(new CampusProtectedItemMovedEvent(
                0,
                actor.CharacterId,
                ownerId,
                CampusInventoryEventPublisher.ResolveInstanceId(item),
                item.DefinitionId,
                ResolveItemName(item),
                sourceContainerId,
                targetContainerId,
                currentRoom != null ? currentRoom.RoomId : string.Empty,
                actor.transform.position,
                StorageTransferReason.Pickup,
                risk));
            writeLog?.Invoke(CampusCommerceTextCatalog.Format(
                CampusCommerceTextId.LeftWithUnpaidLog,
                FormatName(actor),
                ResolveItemName(item)));
            return true;
        }

        private int ResolveDefaultStoreSuspicionRisk()
        {
            return Mathf.Max(0, resolveDefaultStoreSuspicionRisk != null
                ? resolveDefaultStoreSuspicionRisk()
                : 0);
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

        private static string FormatName(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return CampusCommerceTextCatalog.Get(CampusCommerceTextId.UnknownActor);
            }

            return runtime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage);
        }

        private static string ResolveItemName(StorageItemModel item)
        {
            return CampusInventoryEventPublisher.ResolveItemName(item);
        }
    }
}
