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
        public const string BackpackEquipmentContainerId = "backpack_slot";
        public const string BackpackContainerId = "school_backpack";
        public const string BackpackItemDefinitionId = "school_backpack";

        private const string StarterItemsSeedFlag = "storage_player_starter_items_seeded";
        private const string RegistryResourcePath = "StorageItemRegistry";
        private const string PlayerProfileResourcePath = "StorageInventoryProfiles/PlayerInventoryProfile";

        private static StorageInventoryProfile fallbackProfile;

        public static StorageItemRegistry EnsureRegistry(StorageMemory memory)
        {
            if (memory == null)
            {
                return null;
            }

            StorageItemRegistry registry = Resources.Load<StorageItemRegistry>(RegistryResourcePath) ??
                                           StorageItemRegistry.CreateFallbackRegistry();
            memory.SetRegistry(registry);
            return registry;
        }

        public static StorageInventoryProfile EnsurePlayerProfile()
        {
            StorageInventoryProfile profile = Resources.Load<StorageInventoryProfile>(PlayerProfileResourcePath);
            if (profile != null)
            {
                return profile;
            }

            if (fallbackProfile == null)
            {
                fallbackProfile = StorageInventoryProfile.CreateDefaultPlayerProfile();
            }

            return fallbackProfile;
        }

        public static StorageContainerModel[] GetOrCreateHandContainers(StorageMemory memory)
        {
            StorageInventoryProfile profile = EnsurePlayerProfile();
            return profile.GetContainers(memory, profile.HandContainerIds);
        }

        public static StorageContainerModel[] GetOrCreatePocketContainers(StorageMemory memory)
        {
            StorageInventoryProfile profile = EnsurePlayerProfile();
            return profile.GetContainers(memory, profile.PocketContainerIds);
        }

        public static StorageContainerModel GetOrCreateBackpack(StorageMemory memory)
        {
            return EnsurePlayerProfile().GetBackpack(memory);
        }

        public static void EnsureStarterItems(StorageMemory memory)
        {
            EnsurePlayerProfile().EnsureStarterItems(memory, StarterItemsSeedFlag);
        }

        public static bool IsHandContainerId(string containerId)
        {
            return IsTemplateHandContainerId(containerId);
        }

        public static bool IsTemplateHandContainerId(string containerId)
        {
            StorageInventoryProfile profile = EnsurePlayerProfile();
            if (profile.HandContainerIds == null || string.IsNullOrWhiteSpace(containerId))
            {
                return false;
            }

            string normalizedContainerId = ExtractTemplateContainerId(containerId);
            for (int i = 0; i < profile.HandContainerIds.Count; i++)
            {
                if (string.Equals(profile.HandContainerIds[i], normalizedContainerId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractTemplateContainerId(string containerId)
        {
            string trimmed = containerId.Trim();
            int separator = trimmed.LastIndexOf('.');
            return separator >= 0 && separator + 1 < trimmed.Length
                ? trimmed.Substring(separator + 1)
                : trimmed;
        }

        public static bool IsHandGrid(StorageGridUI grid)
        {
            return grid != null && grid.Container != null && IsHandContainerId(grid.Container.Id);
        }
    }
}
