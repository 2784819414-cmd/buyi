using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    internal readonly struct CampusStoreStockRefreshResult
    {
        public CampusStoreStockRefreshResult(int knownShelfCount, int restockedItemCount)
        {
            KnownShelfCount = Mathf.Max(0, knownShelfCount);
            RestockedItemCount = Mathf.Max(0, restockedItemCount);
        }

        public int KnownShelfCount { get; }
        public int RestockedItemCount { get; }
    }

    internal sealed class CampusStoreStockService
    {
        private const string StoreOwnerPrefix = "store:";

        private readonly CampusStoreFacts facts;
        private readonly Func<IReadOnlyList<CampusStoreCatalogEntry>> resolveCatalog;
        private readonly Func<int> resolveDefaultShelfTargetItemCount;
        private readonly Func<int> resolveDefaultStoreSuspicionRisk;
        private readonly Action<string, string> warningSink;

        public CampusStoreStockService(
            CampusStoreFacts facts,
            Func<IReadOnlyList<CampusStoreCatalogEntry>> resolveCatalog,
            Func<int> resolveDefaultShelfTargetItemCount,
            Func<int> resolveDefaultStoreSuspicionRisk,
            Action<string, string> warningSink)
        {
            this.facts = facts;
            this.resolveCatalog = resolveCatalog;
            this.resolveDefaultShelfTargetItemCount = resolveDefaultShelfTargetItemCount;
            this.resolveDefaultStoreSuspicionRisk = resolveDefaultStoreSuspicionRisk;
            this.warningSink = warningSink;
        }

        public string ResolveShelfContainerId(CampusPlacedObject shelf)
        {
            if (shelf == null)
            {
                return "store_shelf_missing";
            }

            CampusGameplayRoom room = facts != null ? facts.ResolveRoomForObject(shelf) : null;
            string roomId = room != null ? room.RoomId : "room";
            string objectId = !string.IsNullOrWhiteSpace(shelf.ObjectId)
                ? shelf.ObjectId
                : !string.IsNullOrWhiteSpace(shelf.TypeId)
                    ? shelf.TypeId
                    : shelf.gameObject.name;
            Vector3Int cell = shelf.Cell;
            return "store_shelf_" +
                   SanitizeId(roomId) +
                   "_" +
                   SanitizeId(objectId) +
                   "_f" + shelf.FloorIndex +
                   "_c" + cell.x + "_" + cell.y + "_" + cell.z;
        }

        public CampusStoreStockRefreshResult RestockShelves(CampusWorldService worldService)
        {
            if (worldService == null)
            {
                return new CampusStoreStockRefreshResult(0, 0);
            }

            int knownShelfCount = 0;
            int restockedItemCount = 0;
            List<CampusGameplayRoom> storeRooms = worldService.GetRoomsByType(CampusRoomType.Store, false);
            for (int i = 0; i < storeRooms.Count; i++)
            {
                CampusGameplayRoom room = storeRooms[i];
                if (room == null || room.Facilities == null)
                {
                    continue;
                }

                for (int facilityIndex = 0; facilityIndex < room.Facilities.Count; facilityIndex++)
                {
                    CampusGameplayRoom.FacilityRecord facility = room.Facilities[facilityIndex];
                    if (facility == null ||
                        facility.FacilityType != CampusFacilityType.StoreShelf ||
                        facility.PlacedObject == null)
                    {
                        continue;
                    }

                    knownShelfCount++;
                    StorageContainerModel container = GetOrCreateShelfContainer(facility.PlacedObject, out int added);
                    restockedItemCount += added;
                    if (container == null)
                    {
                        warningSink?.Invoke(
                            "shelf.container." + facility.FacilityId,
                            "Store shelf failed to create a storage container: " + facility.DisplayName);
                    }
                }
            }

            return new CampusStoreStockRefreshResult(knownShelfCount, restockedItemCount);
        }

        public StorageContainerModel GetOrCreateShelfContainer(CampusPlacedObject shelf, out int added)
        {
            added = 0;
            if (shelf == null)
            {
                return null;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            CampusCharacterInventoryService.EnsureRegistry(memory);
            Vector2Int size = ResolveShelfStorageSize(shelf);
            StorageContainerModel container = memory.GetOrCreateContainer(
                ResolveShelfContainerId(shelf),
                shelf.DisplayName,
                size.x,
                size.y,
                ResolveShelfStorageMaxWeight(shelf));
            TryPrepareShelfStorage(shelf, container, out added, out _);
            return container;
        }

        public bool TryPrepareShelfStorage(
            CampusPlacedObject shelf,
            StorageContainerModel container,
            out int added,
            out string message)
        {
            added = 0;
            message = string.Empty;
            if (shelf == null || container == null)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.MissingShelfOrContainer);
                return false;
            }

            if (!CampusStoreFacts.IsStoreShelf(shelf))
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.NotStoreShelf);
                return false;
            }

            ConfigureShelfContainer(shelf, container);
            added = RestockShelf(shelf, container);
            message = added > 0
                ? CampusCommerceTextCatalog.Format(CampusCommerceTextId.Restocked, added)
                : CampusCommerceTextCatalog.Get(CampusCommerceTextId.ShelfAlreadyStocked);
            return true;
        }

        public bool IsUnpaidStoreItem(StorageItemModel item)
        {
            return item != null &&
                   item.LegalState == StorageItemLegalState.Public &&
                   IsStoreOwnerId(item.OwnerId) &&
                   !item.StolenDuringSession;
        }

        public bool MatchesStoreRoom(StorageItemModel item, CampusGameplayRoom storeRoom)
        {
            if (storeRoom == null || item == null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(item.SourceRoomId) &&
                string.Equals(item.SourceRoomId.Trim(), storeRoom.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(item.OwnerId, ResolveStoreOwnerId(storeRoom), StringComparison.OrdinalIgnoreCase);
        }

        public bool IsStillInsideSourceStore(StorageItemModel item, CampusGameplayRoom currentRoom)
        {
            if (item == null || currentRoom == null || currentRoom.RoomType != CampusRoomType.Store)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(item.SourceRoomId))
            {
                return string.Equals(item.SourceRoomId.Trim(), currentRoom.RoomId, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(item.OwnerId, ResolveStoreOwnerId(currentRoom), StringComparison.OrdinalIgnoreCase);
        }

        public int ResolveItemPrice(StorageItemModel item)
        {
            CampusStoreCatalogEntry entry = item != null ? FindCatalogEntryByDefinition(item.DefinitionId) : null;
            return entry != null ? entry.Price : 0;
        }

        private void ConfigureShelfContainer(CampusPlacedObject shelf, StorageContainerModel container)
        {
            CampusGameplayRoom room = facts != null ? facts.ResolveRoomForObject(shelf) : null;
            container.AccessPolicy = StorageContainerAccessPolicy.Commerce;
            container.OwnerId = ResolveStoreOwnerId(room);
            container.OwnerRole = "Store";
            container.RoomId = room != null ? room.RoomId : string.Empty;
            container.AllowTakingContents = true;
            container.IsPlayerCarried = false;
            container.SuspicionRisk = ResolveDefaultStoreSuspicionRisk();
        }

        private int RestockShelf(CampusPlacedObject shelf, StorageContainerModel container)
        {
            if (shelf == null || container == null)
            {
                return 0;
            }

            CampusStoreShelfDefinition shelfDefinition = shelf.GetComponent<CampusStoreShelfDefinition>();
            if (shelfDefinition != null && !shelfDefinition.AutoRestock)
            {
                return 0;
            }

            int targetItemCount = shelfDefinition != null
                ? shelfDefinition.ResolveTargetItemCount(ResolveDefaultShelfTargetItemCount())
                : ResolveDefaultShelfTargetItemCount();
            string categoryId = facts != null
                ? facts.ResolveShelfCategoryId(shelf, shelfDefinition)
                : CampusStoreFacts.GeneralCategoryId;
            List<CampusStoreCatalogEntry> shelfEntries = BuildShelfCatalog(shelfDefinition, categoryId);
            if (shelfEntries.Count == 0)
            {
                warningSink?.Invoke(
                    "shelf.catalog." + ResolveShelfContainerId(shelf),
                    "Store shelf has no merchandise catalog for category '" + categoryId + "'.");
                return 0;
            }

            int currentItemCount = CountShelfMerchandise(container);
            int addedCount = 0;
            while (currentItemCount < targetItemCount)
            {
                bool placed = false;
                for (int attempt = 0; attempt < shelfEntries.Count; attempt++)
                {
                    int entryIndex = PositiveModulo(currentItemCount + addedCount + attempt, shelfEntries.Count);
                    CampusStoreCatalogEntry entry = shelfEntries[entryIndex];
                    if (TryCreateAndPlaceStoreItem(entry, container))
                    {
                        currentItemCount++;
                        addedCount++;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    break;
                }
            }

            return addedCount;
        }

        private bool TryCreateAndPlaceStoreItem(
            CampusStoreCatalogEntry entry,
            StorageContainerModel container)
        {
            string definitionId = entry != null ? entry.ResolveDefinitionId() : string.Empty;
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            if (registry == null || !registry.TryGetDefinition(definitionId, out _))
            {
                warningSink?.Invoke(
                    "catalog.definition." + definitionId,
                    "Store catalog references missing item definition '" + definitionId + "'.");
                return false;
            }

            StorageItemModel item = registry.CreateItem(definitionId, BuildMerchandiseInstanceId(container, definitionId));
            if (item == null)
            {
                return false;
            }

            if (entry != null && entry.HasDisplayNameOverride())
            {
                item.DisplayName = entry.ResolveDisplayNameOverride(CampusLanguageState.CurrentLanguage);
                if (entry.LocalizedDisplayNameOverride.HasAnyText)
                {
                    item.LocalizedDisplayName = entry.LocalizedDisplayNameOverride;
                }
            }

            ApplyStoreOwnership(item, container);
            if (!container.FindFirstFit(item, out Vector2Int position))
            {
                return false;
            }

            return container.PlaceItem(item, position.x, position.y);
        }

        private List<CampusStoreCatalogEntry> BuildShelfCatalog(
            CampusStoreShelfDefinition shelfDefinition,
            string categoryId)
        {
            List<CampusStoreCatalogEntry> result = new List<CampusStoreCatalogEntry>();
            if (shelfDefinition != null && shelfDefinition.HasExplicitItemDefinitions)
            {
                IReadOnlyList<string> definitionIds = shelfDefinition.ItemDefinitionIds;
                for (int i = 0; i < definitionIds.Count; i++)
                {
                    string definitionId = NormalizeId(definitionIds[i]);
                    if (string.IsNullOrWhiteSpace(definitionId))
                    {
                        continue;
                    }

                    CampusStoreCatalogEntry entry = FindCatalogEntryByDefinition(definitionId);
                    result.Add(entry != null ? entry : CampusStoreCatalogEntry.CreateLoose(categoryId, definitionId));
                }

                return result;
            }

            IReadOnlyList<CampusStoreCatalogEntry> catalog = ResolveCatalog();
            for (int i = 0; i < catalog.Count; i++)
            {
                CampusStoreCatalogEntry entry = catalog[i];
                if (entry != null && entry.MatchesCategory(categoryId))
                {
                    result.Add(entry);
                }
            }

            if (result.Count > 0 || string.Equals(categoryId, CampusStoreFacts.GeneralCategoryId, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] != null && catalog[i].MatchesCategory(CampusStoreFacts.GeneralCategoryId))
                {
                    result.Add(catalog[i]);
                }
            }

            return result;
        }

        private CampusStoreCatalogEntry FindCatalogEntryByDefinition(string definitionId)
        {
            string normalizedId = NormalizeId(definitionId);
            IReadOnlyList<CampusStoreCatalogEntry> catalog = ResolveCatalog();
            if (string.IsNullOrWhiteSpace(normalizedId) || catalog == null)
            {
                return null;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                CampusStoreCatalogEntry entry = catalog[i];
                if (entry != null &&
                    string.Equals(entry.ResolveDefinitionId(), normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static void ApplyStoreOwnership(StorageItemModel item, StorageContainerModel container)
        {
            if (item == null || container == null)
            {
                return;
            }

            item.LegalState = StorageItemLegalState.Public;
            item.OwnerId = container.OwnerId;
            item.SourceContainerId = container.Id;
            item.SourceRoomId = container.RoomId;
            item.SourceLocation = ResolveContainerDisplayName(container);
            item.AllowTaking = true;
            item.StolenDuringSession = false;
            item.SuspicionRisk = container.SuspicionRisk;
        }

        private static int CountShelfMerchandise(StorageContainerModel container)
        {
            if (container == null || container.Items == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null &&
                    item.LegalState == StorageItemLegalState.Public &&
                    string.Equals(item.OwnerId, container.OwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static Vector2Int ResolveShelfStorageSize(CampusPlacedObject shelf)
        {
            if (shelf == null)
            {
                return CampusPlacedObject.DefaultStorageSize;
            }

            shelf.NormalizeStorageSettings();
            return shelf.NormalizedStorageSize;
        }

        private static float ResolveShelfStorageMaxWeight(CampusPlacedObject shelf)
        {
            if (shelf == null)
            {
                return CampusPlacedObject.DefaultStorageMaxWeight;
            }

            shelf.NormalizeStorageSettings();
            return shelf.NormalizedStorageMaxWeight;
        }

        private static bool IsStoreOwnerId(string ownerId)
        {
            return !string.IsNullOrWhiteSpace(ownerId) &&
                   ownerId.Trim().StartsWith(StoreOwnerPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveStoreOwnerId(CampusGameplayRoom room)
        {
            return StoreOwnerPrefix + (room != null && !string.IsNullOrWhiteSpace(room.RoomId)
                ? SanitizeId(room.RoomId)
                : "campus_store");
        }

        private string BuildMerchandiseInstanceId(StorageContainerModel container, string definitionId)
        {
            string containerId = container != null ? container.Id : "store_shelf";
            return SanitizeId(containerId) + "." + SanitizeId(definitionId) + "." + Guid.NewGuid().ToString("N");
        }

        private IReadOnlyList<CampusStoreCatalogEntry> ResolveCatalog()
        {
            return resolveCatalog != null
                ? resolveCatalog()
                : Array.Empty<CampusStoreCatalogEntry>();
        }

        private int ResolveDefaultShelfTargetItemCount()
        {
            return Mathf.Max(1, resolveDefaultShelfTargetItemCount != null
                ? resolveDefaultShelfTargetItemCount()
                : 1);
        }

        private int ResolveDefaultStoreSuspicionRisk()
        {
            return Mathf.Max(0, resolveDefaultStoreSuspicionRisk != null
                ? resolveDefaultStoreSuspicionRisk()
                : 0);
        }

        private static string ResolveContainerDisplayName(StorageContainerModel container)
        {
            return container != null
                ? container.GetDisplayName(CampusLanguageState.CurrentLanguage)
                : StorageTextCatalog.Get(StorageTextId.ExternalContainer);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "id";
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
            return string.IsNullOrWhiteSpace(result) ? "id" : result;
        }
    }
}
