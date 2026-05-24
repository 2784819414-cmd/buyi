using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    [Serializable]
    public sealed class CampusProtectedStockEntry
    {
        public string ItemDefinitionId = string.Empty;
        [Min(1)] public int StockCount = 1;
    }

    [DisallowMultipleComponent]
    public sealed class CampusProtectedStockContainer : MonoBehaviour
    {
        public string ContainerId = string.Empty;
        public string OwnerId = string.Empty;
        public string OwnerRole = "Campus";
        public bool AllowTakingContents = true;
        [Min(0)] public int SuspicionRisk = 4;
        public bool AutoRestock = true;
        public List<CampusProtectedStockEntry> StockItems = new List<CampusProtectedStockEntry>();

        public bool ConfigureContainer(
            StorageMemory memory,
            CampusPlacedObject placedObject,
            StorageContainerModel container)
        {
            if (memory == null || placedObject == null || container == null)
            {
                return false;
            }

            CampusGameplayRoom room = ResolveRoom(placedObject);
            container.AccessPolicy = StorageContainerAccessPolicy.ProtectedTransfer;
            container.OwnerId = ResolveOwnerId(room);
            container.OwnerRole = ResolveOwnerRole();
            container.RoomId = room != null ? room.RoomId : string.Empty;
            container.AllowTakingContents = AllowTakingContents;
            container.IsPlayerCarried = false;
            container.SuspicionRisk = Mathf.Max(0, SuspicionRisk);

            SyncStock(memory, container, ResolveStableContainerId(placedObject));
            return true;
        }

        public string ResolveStableContainerId(CampusPlacedObject placedObject)
        {
            string baseId = !string.IsNullOrWhiteSpace(ContainerId)
                ? ContainerId.Trim()
                : placedObject != null && !string.IsNullOrWhiteSpace(placedObject.ObjectId)
                    ? placedObject.ObjectId.Trim()
                    : gameObject.name;

            if (placedObject == null)
            {
                return "protected_stock_" + SanitizeId(baseId);
            }

            Vector3Int cell = placedObject.Cell;
            return string.Format(
                "protected_stock_{0}_f{1}_c{2}_{3}_{4}",
                SanitizeId(baseId),
                placedObject.FloorIndex,
                cell.x,
                cell.y,
                cell.z);
        }

        public void Normalize()
        {
            ContainerId = NormalizeId(ContainerId);
            OwnerId = NormalizeId(OwnerId);
            OwnerRole = string.IsNullOrWhiteSpace(OwnerRole) ? "Campus" : OwnerRole.Trim();
            SuspicionRisk = Mathf.Max(0, SuspicionRisk);
            if (StockItems == null)
            {
                StockItems = new List<CampusProtectedStockEntry>();
            }

            for (int i = StockItems.Count - 1; i >= 0; i--)
            {
                CampusProtectedStockEntry entry = StockItems[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemDefinitionId))
                {
                    StockItems.RemoveAt(i);
                    continue;
                }

                entry.ItemDefinitionId = entry.ItemDefinitionId.Trim();
                entry.StockCount = Mathf.Max(1, entry.StockCount);
            }
        }

        private void SyncStock(StorageMemory memory, StorageContainerModel container, string stableContainerId)
        {
            Normalize();
            if (StockItems.Count == 0)
            {
                return;
            }

            string seedFlag = "protected_stock_seeded." + stableContainerId;
            bool alreadySeeded = memory.IsSessionFlagSet(seedFlag);
            if (!AutoRestock && alreadySeeded)
            {
                ApplyPendingState(container);
                return;
            }

            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            if (registry == null)
            {
                return;
            }

            for (int entryIndex = 0; entryIndex < StockItems.Count; entryIndex++)
            {
                CampusProtectedStockEntry entry = StockItems[entryIndex];
                int existingCount = CountMatchingItems(container, entry.ItemDefinitionId);
                for (int itemIndex = existingCount; itemIndex < entry.StockCount; itemIndex++)
                {
                    StorageItemModel item = registry.CreateItem(
                        entry.ItemDefinitionId,
                        BuildInstanceId(stableContainerId, entry.ItemDefinitionId, itemIndex));
                    if (item == null || !container.FindFirstFit(item, out Vector2Int position))
                    {
                        break;
                    }

                    CampusProtectedTransferState.BeginPendingTransfer(item, container);
                    container.PlaceItem(item, position.x, position.y);
                }
            }

            ApplyPendingState(container);
            memory.SetSessionFlag(seedFlag);
        }

        private void ApplyPendingState(StorageContainerModel container)
        {
            if (container == null || container.Items == null)
            {
                return;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null && ContainsConfiguredItem(item.DefinitionId))
                {
                    CampusProtectedTransferState.BeginPendingTransfer(item, container);
                }
            }
        }

        private bool ContainsConfiguredItem(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || StockItems == null)
            {
                return false;
            }

            string normalizedId = definitionId.Trim();
            for (int i = 0; i < StockItems.Count; i++)
            {
                CampusProtectedStockEntry entry = StockItems[i];
                if (entry != null &&
                    string.Equals(entry.ItemDefinitionId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountMatchingItems(StorageContainerModel container, string definitionId)
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

        private string ResolveOwnerId(CampusGameplayRoom room)
        {
            if (!string.IsNullOrWhiteSpace(OwnerId))
            {
                return OwnerId.Trim();
            }

            return room != null ? room.RoomId : string.Empty;
        }

        private string ResolveOwnerRole()
        {
            return string.IsNullOrWhiteSpace(OwnerRole) ? "Campus" : OwnerRole.Trim();
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

        private static string BuildInstanceId(string containerId, string definitionId, int index)
        {
            return containerId + "." + SanitizeId(definitionId) + "." + index + "." + Guid.NewGuid().ToString("N");
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            string result = new string(chars).Trim('_');
            return string.IsNullOrEmpty(result) ? "unnamed" : result;
        }
    }
}
