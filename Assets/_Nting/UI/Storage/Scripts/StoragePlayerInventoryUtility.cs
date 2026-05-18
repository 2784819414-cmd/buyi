using NtingCampus.Gameplay.Inventory;
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

            StorageContainerModel leftHand = memory.GetOrCreateContainer(LeftHandContainerId, "\u5de6\u624b", 2, 2, 3f);
            StorageContainerModel rightHand = memory.GetOrCreateContainer(RightHandContainerId, "\u53f3\u624b", 2, 2, 3f);
            ConfigurePlayerCarriedContainer(leftHand);
            ConfigurePlayerCarriedContainer(rightHand);

            return new[]
            {
                leftHand,
                rightHand
            };
        }

        public static StorageContainerModel[] GetOrCreatePocketContainers(StorageMemory memory)
        {
            if (memory == null)
            {
                return new StorageContainerModel[4];
            }

            StorageContainerModel leftChest = memory.GetOrCreateContainer(LeftChestPocketContainerId, "\u5de6\u80f8\u888b", 2, 3, 1.5f);
            StorageContainerModel rightChest = memory.GetOrCreateContainer(RightChestPocketContainerId, "\u53f3\u80f8\u888b", 2, 3, 1.5f);
            StorageContainerModel leftPants = memory.GetOrCreateContainer(LeftPantsPocketContainerId, "\u5de6\u88e4\u888b", 2, 3, 2f);
            StorageContainerModel rightPants = memory.GetOrCreateContainer(RightPantsPocketContainerId, "\u53f3\u88e4\u888b", 2, 3, 2f);
            ConfigurePlayerCarriedContainer(leftChest);
            ConfigurePlayerCarriedContainer(rightChest);
            ConfigurePlayerCarriedContainer(leftPants);
            ConfigurePlayerCarriedContainer(rightPants);

            return new[]
            {
                leftChest,
                rightChest,
                leftPants,
                rightPants
            };
        }

        public static StorageContainerModel GetOrCreateBackpack(StorageMemory memory)
        {
            StorageContainerModel backpack = memory != null
                ? memory.GetOrCreateContainer(BackpackContainerId, "\u5b66\u751f\u4e66\u5305", 5, 6, 20f)
                : null;
            ConfigurePlayerCarriedContainer(backpack);
            return backpack;
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

            CampusInventoryTransferService service = CampusInventoryTransferService.Resolve();
            StorageTransferContext context = new StorageTransferContext
            {
                Reason = StorageTransferReason.Pickup
            };
            if (service.TryPlaceInCarriedStorage(memory, item, context, out StorageTransferResult result))
            {
                return true;
            }

            errorMessage = result.Message;
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

        private static void ConfigurePlayerCarriedContainer(StorageContainerModel container)
        {
            if (container == null)
            {
                return;
            }

            container.AccessPolicy = StorageContainerAccessPolicy.PlayerCarried;
            container.OwnerId = "player";
            container.OwnerRole = "Player";
            container.AllowTakingContents = true;
            container.IsPlayerCarried = true;
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
