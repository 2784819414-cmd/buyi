using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Retail
{
    public enum CampusRetailShelfMode
    {
        Container = 0,
        DirectPickupDisplay = 1
    }

    [DisallowMultipleComponent]
    public sealed class CampusRetailShelf : MonoBehaviour
    {
        private readonly struct DisplayShelfState
        {
            public DisplayShelfState(
                string shelfId,
                string roomId,
                string storeId,
                string sourceLocation,
                int suspicionRisk)
            {
                ShelfId = shelfId;
                RoomId = roomId;
                StoreId = storeId;
                SourceLocation = sourceLocation;
                SuspicionRisk = suspicionRisk;
            }

            public string ShelfId { get; }
            public string RoomId { get; }
            public string StoreId { get; }
            public string SourceLocation { get; }
            public int SuspicionRisk { get; }
        }

        private const float DisplaySyncIntervalSeconds = 0.9f;

        public string ShelfId = string.Empty;
        public string ItemDefinitionId = string.Empty;
        public CampusRetailShelfMode ShelfMode = CampusRetailShelfMode.Container;
        [Min(1)] public int StockCount = 8;
        public bool AutoRestock = true;
        [Min(1)] public int DisplaySlotCount = 4;
        public Vector2 DisplaySpread = new Vector2(0.9f, 0.18f);
        public float DisplayHeight = 0.38f;

        private float nextDisplaySyncTime;

        private void OnEnable()
        {
            nextDisplaySyncTime = 0f;
        }

        private void Update()
        {
            if (!Application.isPlaying ||
                ShelfMode != CampusRetailShelfMode.DirectPickupDisplay ||
                !AutoRestock ||
                Time.time < nextDisplaySyncTime)
            {
                return;
            }

            nextDisplaySyncTime = Time.time + DisplaySyncIntervalSeconds;
            TrySyncDisplayItems();
        }

        public bool ConfigureContainer(StorageMemory memory, CampusPlacedObject placedObject, StorageContainerModel container)
        {
            if (memory == null || placedObject == null || container == null)
            {
                return false;
            }

            CampusGameplayRoom room = ResolveRoom(placedObject);
            container.AccessPolicy = StorageContainerAccessPolicy.ProtectedTransfer;
            container.OwnerId = ResolveStoreId(room);
            container.OwnerRole = "Retail";
            container.RoomId = room != null ? room.RoomId : string.Empty;
            container.AllowTakingContents = true;
            container.IsPlayerCarried = false;
            container.SuspicionRisk = ResolveSuspicionRisk(room);

            if (ShelfMode == CampusRetailShelfMode.Container)
            {
                SyncContainerStock(memory, container, ResolveShelfId(placedObject));
            }

            return true;
        }

        public bool TryTakeOneForActor(CampusCharacterRuntime actor, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (actor == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(ItemDefinitionId))
            {
                result = StorageTransferResult.Fail(CampusRetailTextCatalog.Get(CampusRetailTextId.ShelfUnconfigured));
                return false;
            }

            if (ShelfMode == CampusRetailShelfMode.DirectPickupDisplay)
            {
                TrySyncDisplayItems();
                CampusDroppedStorageItem droppedItem = FindAvailableDisplayItem();
                if (droppedItem == null)
                {
                    result = StorageTransferResult.Fail(CampusRetailTextCatalog.Get(CampusRetailTextId.ShelfEmpty));
                    return false;
                }

                return droppedItem.TryPickup(actor, out result);
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (memory == null || placedObject == null)
            {
                return false;
            }

            StorageContainerModel container = memory.GetOrCreateContainer(
                BuildContainerId(placedObject),
                placedObject.DisplayName,
                placedObject.LocalizedDisplayNameOverride,
                placedObject.NormalizedStorageSize.x,
                placedObject.NormalizedStorageSize.y,
                placedObject.NormalizedStorageMaxWeight);
            ConfigureContainer(memory, placedObject, container);

            StorageItemModel item = FindFirstShelfItem(container);
            if (item == null)
            {
                result = StorageTransferResult.Fail(CampusRetailTextCatalog.Get(CampusRetailTextId.ShelfEmpty));
                return false;
            }

            StorageTransferContext context = StorageTransferContext.ForActor(actor.gameObject, StorageTransferReason.ScriptedTake);
            bool moved = CampusInventoryTransferService.Resolve().TryPickUpIntoHands(
                memory,
                item,
                context,
                out result);
            if (moved && actor.Data != null)
            {
                actor.Data.AddMemory(CampusCharacterMemoryId.SelectedProtectedGoods);
            }

            return moved;
        }

        public string ResolveShelfId(CampusPlacedObject placedObject)
        {
            string baseId = ResolveShelfBaseId(placedObject);
            if (placedObject == null)
            {
                return baseId;
            }

            return string.Format(
                "{0}@F{1}_{2}_{3}",
                baseId,
                placedObject.FloorIndex,
                placedObject.Cell.x,
                placedObject.Cell.y);
        }

        public void RefreshAfterPlacement()
        {
            nextDisplaySyncTime = Time.time + DisplaySyncIntervalSeconds;
            if (ShelfMode != CampusRetailShelfMode.DirectPickupDisplay)
            {
                return;
            }

            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (placedObject == null)
            {
                return;
            }

            DestroyDisplayItems(ResolveShelfId(placedObject));
            TrySyncDisplayItems();
        }

        private void SyncContainerStock(
            StorageMemory memory,
            StorageContainerModel container,
            string shelfId)
        {
            if (memory == null ||
                container == null ||
                string.IsNullOrWhiteSpace(ItemDefinitionId))
            {
                return;
            }

            string seedFlag = "retail_shelf_seeded." + shelfId;
            bool alreadySeeded = memory.IsSessionFlagSet(seedFlag);
            if (!AutoRestock && alreadySeeded)
            {
                return;
            }

            int existingCount = CountMatchingItems(container, ItemDefinitionId);
            int desiredCount = Mathf.Max(1, StockCount);
            for (int i = existingCount; i < desiredCount; i++)
            {
                StorageItemModel item = CreateRetailItem(memory, container, shelfId, i);
                if (item == null || !container.FindFirstFit(item, out Vector2Int position))
                {
                    break;
                }

                container.PlaceItem(item, position.x, position.y);
            }

            memory.SetSessionFlag(seedFlag);
        }

        private void TrySyncDisplayItems()
        {
            if (!Application.isPlaying ||
                ShelfMode != CampusRetailShelfMode.DirectPickupDisplay ||
                string.IsNullOrWhiteSpace(ItemDefinitionId))
            {
                return;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (memory == null || placedObject == null)
            {
                return;
            }

            if (!TryBuildDisplayShelfState(placedObject, out DisplayShelfState state))
            {
                return;
            }

            int desiredCount = Mathf.Max(1, DisplaySlotCount);
            List<CampusDroppedStorageItem> existingItems = NormalizeDisplayItems(
                CollectDisplayItems(state.ShelfId),
                placedObject,
                state,
                desiredCount);
            if (!AutoRestock && existingItems.Count > 0)
            {
                return;
            }

            for (int i = existingItems.Count; i < desiredCount; i++)
            {
                StorageItemModel item = CreateRetailItem(memory, null, state.ShelfId, i);
                if (item == null)
                {
                    return;
                }

                ApplyDisplayItemPendingState(item, state);

                CampusStorageGroundItemUtility.TryPlaceItemAtWorldPosition(
                    gameObject,
                    item,
                    ResolveDisplayWorldPosition(placedObject, i, desiredCount),
                    out _,
                    out CampusDroppedStorageItem droppedItem);
                if (droppedItem != null)
                {
                    ApplyDisplayItemPendingState(droppedItem, state);
                    existingItems.Add(droppedItem);
                }
            }
        }

        private CampusDroppedStorageItem FindAvailableDisplayItem()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            string shelfId = ResolveShelfId(placedObject);
            List<CampusDroppedStorageItem> displayItems = CollectDisplayItems(shelfId);
            return displayItems.Count > 0 ? displayItems[0] : null;
        }

        private List<CampusDroppedStorageItem> CollectDisplayItems(string shelfId)
        {
            List<CampusDroppedStorageItem> items = new List<CampusDroppedStorageItem>();
            CampusDroppedStorageItemRegistry.CollectBySourceContainer(shelfId, items);
            return items;
        }

        private void DestroyDisplayItems(string shelfId)
        {
            List<CampusDroppedStorageItem> items = CollectDisplayItems(shelfId);
            for (int i = 0; i < items.Count; i++)
            {
                CampusDroppedStorageItem item = items[i];
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
        }

        private StorageItemModel FindFirstShelfItem(StorageContainerModel container)
        {
            if (container == null || container.Items == null)
            {
                return null;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null &&
                    string.Equals(item.DefinitionId, ItemDefinitionId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private int CountMatchingItems(StorageContainerModel container, string definitionId)
        {
            if (container == null || container.Items == null || string.IsNullOrWhiteSpace(definitionId))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null &&
                    string.Equals(item.DefinitionId, definitionId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private StorageItemModel CreateRetailItem(
            StorageMemory memory,
            StorageContainerModel source,
            string shelfId,
            int index)
        {
            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            StorageItemModel item = registry != null
                ? registry.CreateItem(ItemDefinitionId.Trim(), BuildInstanceId(shelfId, index))
                : null;
            if (item == null)
            {
                return null;
            }

            if (source != null)
            {
                CampusProtectedTransferState.BeginPendingTransfer(item, source);
            }

            return item;
        }

        private bool TryBuildDisplayShelfState(
            CampusPlacedObject placedObject,
            out DisplayShelfState state)
        {
            state = default;
            if (placedObject == null)
            {
                return false;
            }

            CampusGameplayRoom room = ResolveRoom(placedObject);
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                return false;
            }

            state = new DisplayShelfState(
                ResolveShelfId(placedObject),
                room.RoomId.Trim(),
                ResolveStoreId(room),
                placedObject.DisplayName,
                ResolveSuspicionRisk(room));
            return true;
        }

        private List<CampusDroppedStorageItem> NormalizeDisplayItems(
            List<CampusDroppedStorageItem> existingItems,
            CampusPlacedObject placedObject,
            DisplayShelfState state,
            int desiredCount)
        {
            List<CampusDroppedStorageItem> normalizedItems = new List<CampusDroppedStorageItem>();
            if (existingItems == null)
            {
                return normalizedItems;
            }

            for (int i = 0; i < existingItems.Count; i++)
            {
                CampusDroppedStorageItem droppedItem = existingItems[i];
                if (droppedItem == null)
                {
                    continue;
                }

                if (!string.Equals(
                        droppedItem.DefinitionId,
                        ItemDefinitionId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    Destroy(droppedItem.gameObject);
                    continue;
                }

                ApplyDisplayItemPendingState(droppedItem, state);
                droppedItem.transform.position = ResolveDisplayWorldPosition(
                    placedObject,
                    normalizedItems.Count,
                    desiredCount);
                normalizedItems.Add(droppedItem);
            }

            return normalizedItems;
        }

        private static void ApplyDisplayItemPendingState(
            StorageItemModel item,
            DisplayShelfState state)
        {
            CampusProtectedTransferState.BeginPendingTransfer(
                item,
                state.ShelfId,
                state.RoomId,
                state.StoreId,
                state.SourceLocation,
                state.SuspicionRisk);
        }

        private static void ApplyDisplayItemPendingState(
            CampusDroppedStorageItem droppedItem,
            DisplayShelfState state)
        {
            CampusProtectedTransferState.BeginPendingTransfer(
                droppedItem,
                state.ShelfId,
                state.RoomId,
                state.StoreId,
                state.SourceLocation,
                state.SuspicionRisk);
        }

        private string BuildContainerId(CampusPlacedObject placedObject)
        {
            return "retail_shelf_" + ResolveShelfId(placedObject).Replace(' ', '_');
        }

        private string BuildInstanceId(string shelfId, int index)
        {
            return shelfId + ".retail_stock." + index + "." + Guid.NewGuid().ToString("N");
        }

        private CampusGameplayRoom ResolveRoom(CampusPlacedObject placedObject)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            if (placedObject == null || worldService == null)
            {
                return null;
            }

            return worldService.FindRoomForPosition(
                placedObject.FloorIndex,
                placedObject.transform.position);
        }

        private string ResolveStoreId(CampusGameplayRoom room)
        {
            return room != null ? room.RoomId : string.Empty;
        }

        private int ResolveSuspicionRisk(CampusGameplayRoom room)
        {
            return room != null && room.RoomType == CampusRoomType.RetailArea ? 3 : 4;
        }

        private string ResolveShelfBaseId(CampusPlacedObject placedObject)
        {
            if (!string.IsNullOrWhiteSpace(ShelfId))
            {
                return ShelfId.Trim();
            }

            if (placedObject != null && !string.IsNullOrWhiteSpace(placedObject.ObjectId))
            {
                return placedObject.ObjectId.Trim();
            }

            return gameObject.name;
        }

        private Vector3 ResolveDisplayWorldPosition(
            CampusPlacedObject placedObject,
            int slotIndex,
            int slotCount)
        {
            float normalized = slotCount <= 1 ? 0.5f : slotIndex / (float)(slotCount - 1);
            float x = Mathf.Lerp(-DisplaySpread.x * 0.5f, DisplaySpread.x * 0.5f, normalized);
            float y = Mathf.Sin(normalized * Mathf.PI) * DisplaySpread.y;
            Vector3 localOffset = new Vector3(x, DisplayHeight + y, 0f);
            return placedObject != null
                ? placedObject.transform.TransformPoint(localOffset)
                : transform.TransformPoint(localOffset);
        }
    }
}
