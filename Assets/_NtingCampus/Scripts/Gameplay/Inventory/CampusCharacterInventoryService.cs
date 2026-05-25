using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;

namespace NtingCampus.Gameplay.Inventory
{
    public static class CampusCharacterInventoryService
    {
        public static StorageItemRegistry EnsureRegistry(StorageMemory memory)
        {
            return StoragePlayerInventoryUtility.EnsureRegistry(memory);
        }

        public static CampusCharacterInventory GetOrCreateCurrentPlayerInventory(bool seedStarterItems)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusCharacterRuntime runtime = bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
            return GetOrCreateInventory(runtime, seedStarterItems);
        }

        public static CampusCharacterInventory GetOrCreateInventory(
            CampusCharacterRuntime runtime,
            bool seedStarterItems)
        {
            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null || !HasInventoryOwner(runtime))
            {
                return CreateEmptyInventory(runtime);
            }

            EnsureRegistry(memory);
            StorageInventoryProfile profile = ResolveSharedCarryProfile();
            string ownerId = runtime.CharacterId.Trim();
            string ownerRole = ResolveOwnerRole(runtime);
            string roomId = ResolveContainerRoomId(runtime);

            EnsureContainers(memory, profile, ownerId, ownerRole, roomId);
            if (seedStarterItems)
            {
                EnsureStarterItems(memory, profile, ownerId);
            }

            return new CampusCharacterInventory(
                runtime,
                ResolveContainers(memory, profile.HandContainerIds, ownerId),
                ResolveContainers(memory, profile.PocketContainerIds, ownerId),
                ResolveContainer(memory, profile.BackpackEquipmentContainerId, ownerId),
                ResolveContainer(memory, profile.BackpackContainerId, ownerId));
        }

        public static bool TryGetExistingInventory(
            CampusCharacterRuntime runtime,
            out CampusCharacterInventory inventory)
        {
            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null || !HasInventoryOwner(runtime))
            {
                inventory = CreateEmptyInventory(runtime);
                return false;
            }

            StorageInventoryProfile profile = ResolveSharedCarryProfile();
            string ownerId = runtime.CharacterId.Trim();
            inventory = new CampusCharacterInventory(
                runtime,
                TryResolveExistingContainers(memory, profile.HandContainerIds, ownerId),
                TryResolveExistingContainers(memory, profile.PocketContainerIds, ownerId),
                TryResolveExistingContainer(memory, profile.BackpackEquipmentContainerId, ownerId),
                TryResolveExistingContainer(memory, profile.BackpackContainerId, ownerId));
            return HasAnyExistingContainer(inventory);
        }

        public static bool IsHandContainerId(string containerId)
        {
            return TrySplitTemplateContainerId(containerId, out string templateId) &&
                   StoragePlayerInventoryUtility.IsTemplateHandContainerId(templateId);
        }

        public static string ResolveContainerId(string ownerId, string templateContainerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId) ||
                string.IsNullOrWhiteSpace(templateContainerId))
            {
                return string.Empty;
            }

            return NormalizeOwnerId(ownerId) + "." + templateContainerId.Trim();
        }

        private static CampusCharacterInventory CreateEmptyInventory(CampusCharacterRuntime runtime)
        {
            return new CampusCharacterInventory(runtime, null, null, null, null);
        }

        private static bool HasInventoryOwner(CampusCharacterRuntime runtime)
        {
            return runtime != null && !string.IsNullOrWhiteSpace(runtime.CharacterId);
        }

        private static StorageInventoryProfile ResolveSharedCarryProfile()
        {
            return StoragePlayerInventoryUtility.EnsurePlayerProfile();
        }

        private static void EnsureContainers(
            StorageMemory memory,
            StorageInventoryProfile profile,
            string ownerId,
            string ownerRole,
            string roomId)
        {
            if (memory == null || profile == null || profile.Containers == null)
            {
                return;
            }

            for (int i = 0; i < profile.Containers.Count; i++)
            {
                StorageContainerDefinition definition = profile.Containers[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                string containerId = ResolveContainerId(ownerId, definition.Id);
                if (string.IsNullOrWhiteSpace(containerId))
                {
                    continue;
                }

                StorageContainerModel container = memory.GetOrCreateContainer(
                    containerId,
                    definition.DisplayName,
                    definition.LocalizedDisplayName,
                    definition.Columns,
                    definition.Rows,
                    definition.MaxWeight,
                    definition.IsSingleItemSlot);
                definition.ApplyRuntimeSettings(container, ownerId, roomId);
                container.OwnerRole = string.IsNullOrWhiteSpace(ownerRole)
                    ? definition.OwnerRole
                    : ownerRole;
            }
        }

        private static StorageContainerModel[] ResolveContainers(
            StorageMemory memory,
            List<string> templateIds,
            string ownerId)
        {
            if (memory == null || templateIds == null || templateIds.Count == 0)
            {
                return Array.Empty<StorageContainerModel>();
            }

            StorageContainerModel[] result = new StorageContainerModel[templateIds.Count];
            for (int i = 0; i < templateIds.Count; i++)
            {
                result[i] = ResolveContainer(memory, templateIds[i], ownerId);
            }

            return result;
        }

        private static StorageContainerModel[] TryResolveExistingContainers(
            StorageMemory memory,
            List<string> templateIds,
            string ownerId)
        {
            if (memory == null || templateIds == null || templateIds.Count == 0)
            {
                return Array.Empty<StorageContainerModel>();
            }

            StorageContainerModel[] result = new StorageContainerModel[templateIds.Count];
            for (int i = 0; i < templateIds.Count; i++)
            {
                result[i] = TryResolveExistingContainer(memory, templateIds[i], ownerId);
            }

            return result;
        }

        private static StorageContainerModel ResolveContainer(
            StorageMemory memory,
            string templateContainerId,
            string ownerId)
        {
            string containerId = ResolveContainerId(ownerId, templateContainerId);
            if (memory == null || string.IsNullOrWhiteSpace(containerId))
            {
                return null;
            }

            memory.TryGetContainer(containerId, out StorageContainerModel container);
            return container;
        }

        private static StorageContainerModel TryResolveExistingContainer(
            StorageMemory memory,
            string templateContainerId,
            string ownerId)
        {
            string containerId = ResolveContainerId(ownerId, templateContainerId);
            if (memory == null || string.IsNullOrWhiteSpace(containerId))
            {
                return null;
            }

            memory.TryGetContainer(containerId, out StorageContainerModel container);
            return container;
        }

        private static void EnsureStarterItems(
            StorageMemory memory,
            StorageInventoryProfile profile,
            string ownerId)
        {
            if (memory == null || profile == null || profile.StarterItems == null)
            {
                return;
            }

            string seedFlag = "storage_character_starter_items_seeded." + NormalizeOwnerId(ownerId);
            if (memory.IsSessionFlagSet(seedFlag))
            {
                return;
            }

            for (int i = 0; i < profile.StarterItems.Count; i++)
            {
                StorageStarterItemDefinition starter = profile.StarterItems[i];
                if (starter == null ||
                    string.IsNullOrWhiteSpace(starter.ContainerId) ||
                    string.IsNullOrWhiteSpace(starter.DefinitionId))
                {
                    continue;
                }

                string containerId = ResolveContainerId(ownerId, starter.ContainerId);
                if (string.IsNullOrWhiteSpace(containerId))
                {
                    continue;
                }

                memory.TryPlaceNewItem(
                    containerId,
                    starter.DefinitionId,
                    ResolveItemInstanceId(ownerId, starter.InstanceId, starter.DefinitionId, i),
                    starter.X,
                    starter.Y);
            }

            memory.SetSessionFlag(seedFlag);
        }

        private static bool TrySplitTemplateContainerId(string containerId, out string templateId)
        {
            templateId = string.Empty;
            if (string.IsNullOrWhiteSpace(containerId))
            {
                return false;
            }

            string trimmed = containerId.Trim();
            int separator = trimmed.LastIndexOf('.');
            templateId = separator >= 0 && separator + 1 < trimmed.Length
                ? trimmed.Substring(separator + 1)
                : trimmed;
            return !string.IsNullOrWhiteSpace(templateId);
        }

        private static string ResolveOwnerRole(CampusCharacterRuntime runtime)
        {
            return runtime != null && runtime.Data != null
                ? runtime.Data.Role.ToString()
                : "Character";
        }

        private static string ResolveContainerRoomId(CampusCharacterRuntime runtime)
        {
            return CampusProtectedTransferState.ResolveActorCurrentRoomId(runtime);
        }

        private static string ResolveItemInstanceId(
            string ownerId,
            string configuredInstanceId,
            string definitionId,
            int index)
        {
            string suffix = !string.IsNullOrWhiteSpace(configuredInstanceId)
                ? configuredInstanceId.Trim()
                : (definitionId ?? "item") + "_" + index;
            return NormalizeOwnerId(ownerId) + "." + suffix;
        }

        private static string NormalizeOwnerId(string ownerId)
        {
            string source = string.IsNullOrWhiteSpace(ownerId) ? "unknown_character" : ownerId.Trim();
            char[] chars = source.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static bool HasAnyExistingContainer(CampusCharacterInventory inventory)
        {
            return HasAnyContainer(inventory != null ? inventory.Hands : null) ||
                   HasAnyContainer(inventory != null ? inventory.Pockets : null) ||
                   (inventory != null && inventory.BackpackEquipmentSlot != null) ||
                   (inventory != null && inventory.Backpack != null);
        }

        private static bool HasAnyContainer(StorageContainerModel[] containers)
        {
            if (containers == null)
            {
                return false;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                if (containers[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
