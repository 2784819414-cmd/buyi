using UnityEngine;

namespace Nting.Storage
{
    public static class StoragePlayerInventoryUtility
    {
        public const string LeftHandContainerId = "hand_left";
        public const string RightHandContainerId = "hand_right";
        public const string LeftChestPocketContainerId = "pocket_left_chest";
        public const string RightChestPocketContainerId = "pocket_right_chest";
        public const string LeftPantsPocketContainerId = "pocket_left_pants";
        public const string RightPantsPocketContainerId = "pocket_right_pants";
        public const string BackpackContainerId = "school_backpack";

        private const string StarterItemsSeedFlag = "storage_player_starter_items_seeded";

        public static StorageItemRegistry EnsureRegistry(StorageMemory memory)
        {
            if (memory == null)
            {
                return null;
            }

            StorageItemRegistry registry = Resources.Load<StorageItemRegistry>("StorageItemRegistry");
            if (registry == null)
            {
                registry = StorageItemRegistry.CreateDemoRegistry();
            }

            memory.SetRegistry(registry);
            return registry;
        }

        public static StorageContainerModel[] GetOrCreateHandContainers(StorageMemory memory)
        {
            if (memory == null)
            {
                return new StorageContainerModel[2];
            }

            return new[]
            {
                memory.GetOrCreateContainer(LeftHandContainerId, "\u5de6\u624b", 2, 2, 3f),
                memory.GetOrCreateContainer(RightHandContainerId, "\u53f3\u624b", 2, 2, 3f)
            };
        }

        public static StorageContainerModel[] GetOrCreatePocketContainers(StorageMemory memory)
        {
            if (memory == null)
            {
                return new StorageContainerModel[4];
            }

            return new[]
            {
                memory.GetOrCreateContainer(LeftChestPocketContainerId, "\u5de6\u80f8\u888b", 2, 3, 1.5f),
                memory.GetOrCreateContainer(RightChestPocketContainerId, "\u53f3\u80f8\u888b", 2, 3, 1.5f),
                memory.GetOrCreateContainer(LeftPantsPocketContainerId, "\u5de6\u88e4\u888b", 2, 3, 2f),
                memory.GetOrCreateContainer(RightPantsPocketContainerId, "\u53f3\u88e4\u888b", 2, 3, 2f)
            };
        }

        public static StorageContainerModel GetOrCreateBackpack(StorageMemory memory)
        {
            return memory != null
                ? memory.GetOrCreateContainer(BackpackContainerId, "\u5b66\u751f\u4e66\u5305", 5, 6, 20f)
                : null;
        }

        public static void EnsureStarterItems(StorageMemory memory)
        {
            if (memory == null || memory.IsSessionFlagSet(StarterItemsSeedFlag))
            {
                return;
            }

            TrySeedItem(memory, LeftChestPocketContainerId, "phone", "phone_player_001", 0, 0);
            TrySeedItem(memory, LeftChestPocketContainerId, "note", "note_player_001", 1, 0);
            TrySeedItem(memory, RightChestPocketContainerId, "key", "key_player_001", 0, 0);
            TrySeedItem(memory, LeftPantsPocketContainerId, "snack", "snack_player_001", 0, 1);

            TrySeedItem(memory, BackpackContainerId, "textbook", "textbook_player_001", 0, 0);
            TrySeedItem(memory, BackpackContainerId, "workbook", "workbook_player_001", 2, 0);
            TrySeedItem(memory, BackpackContainerId, "pencil_case", "pencil_case_player_001", 2, 2);
            TrySeedItem(memory, BackpackContainerId, "lunch_box", "lunch_box_player_001", 0, 3);

            memory.SetSessionFlag(StarterItemsSeedFlag);
        }

        public static bool TryPlaceInCarriedStorage(StorageMemory memory, StorageItemModel item, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (memory == null)
            {
                errorMessage = "Storage memory is unavailable.";
                return false;
            }

            if (item == null)
            {
                errorMessage = "Item is unavailable.";
                return false;
            }

            GetOrCreateHandContainers(memory);
            StorageContainerModel backpack = GetOrCreateBackpack(memory);
            StorageContainerModel[] pockets = GetOrCreatePocketContainers(memory);

            if (TryPlaceInContainer(backpack, item))
            {
                return true;
            }

            for (int i = 0; i < pockets.Length; i++)
            {
                if (TryPlaceInContainer(pockets[i], item))
                {
                    return true;
                }
            }

            errorMessage = "No backpack or pocket space for " + item.DisplayName + ".";
            return false;
        }

        public static bool IsHandContainerId(string containerId)
        {
            return string.Equals(containerId, LeftHandContainerId, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(containerId, RightHandContainerId, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHandGrid(StorageGridUI grid)
        {
            return grid != null && grid.Container != null && IsHandContainerId(grid.Container.Id);
        }

        private static bool TryPlaceInContainer(StorageContainerModel container, StorageItemModel item)
        {
            if (container == null || item == null)
            {
                return false;
            }

            return container.FindFirstFit(item, out Vector2Int position) &&
                   container.PlaceItem(item, position.x, position.y);
        }

        private static void TrySeedItem(StorageMemory memory, string containerId, string definitionId, string instanceId, int x, int y)
        {
            if (memory == null || memory.ItemRegistry == null || string.IsNullOrWhiteSpace(containerId))
            {
                return;
            }

            memory.TryPlaceNewItem(containerId, definitionId, instanceId, x, y);
        }
    }
}
