using System;
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
            if (memory == null)
            {
                return new CampusCharacterInventory(runtime, null, null, null);
            }

            StoragePlayerInventoryUtility.EnsureRegistry(memory);
            StorageInventoryProfile profile = StoragePlayerInventoryUtility.EnsurePlayerProfile();
            string ownerId = ResolveOwnerId(runtime, profile);
            string ownerRole = ResolveOwnerRole(runtime);
            string roomId = ResolveRoomId(runtime, profile);

            EnsureContainers(memory, profile, ownerId, ownerRole, roomId);
            if (seedStarterItems)
            {
                EnsureStarterItems(memory, profile, ownerId);
            }

            return new CampusCharacterInventory(
                runtime,
                ResolveContainers(memory, profile.HandContainerIds, ownerId),
                ResolveContainers(memory, profile.PocketContainerIds, ownerId),
                ResolveContainer(memory, profile.BackpackContainerId, ownerId));
        }

        public static bool IsHandContainerId(string containerId)
        {
            return TrySplitTemplateContainerId(containerId, out string templateId) &&
                   StoragePlayerInventoryUtility.IsTemplateHandContainerId(templateId);
        }

        public static string ResolveContainerId(string ownerId, string templateContainerId)
        {
            if (string.IsNullOrWhiteSpace(templateContainerId))
            {
                return string.Empty;
            }

            return NormalizeOwnerId(ownerId) + "." + templateContainerId.Trim();
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

                StorageContainerModel container = memory.GetOrCreateContainer(
                    ResolveContainerId(ownerId, definition.Id),
                    definition.DisplayName,
                    definition.Columns,
                    definition.Rows,
                    definition.MaxWeight);
                definition.ApplyRuntimeSettings(container, ownerId, roomId);
                container.OwnerRole = string.IsNullOrWhiteSpace(ownerRole) ? definition.OwnerRole : ownerRole;
            }
        }

        private static StorageContainerModel[] ResolveContainers(
            StorageMemory memory,
            System.Collections.Generic.List<string> templateIds,
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

        private static StorageContainerModel ResolveContainer(
            StorageMemory memory,
            string templateContainerId,
            string ownerId)
        {
            if (memory == null || string.IsNullOrWhiteSpace(templateContainerId))
            {
                return null;
            }

            memory.TryGetContainer(ResolveContainerId(ownerId, templateContainerId), out StorageContainerModel container);
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

                memory.TryPlaceNewItem(
                    ResolveContainerId(ownerId, starter.ContainerId),
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

        private static string ResolveOwnerId(CampusCharacterRuntime runtime, StorageInventoryProfile profile)
        {
            if (runtime != null && !string.IsNullOrWhiteSpace(runtime.CharacterId))
            {
                return runtime.CharacterId.Trim();
            }

            return profile != null && !string.IsNullOrWhiteSpace(profile.OwnerId)
                ? profile.OwnerId.Trim()
                : "player";
        }

        private static string ResolveOwnerRole(CampusCharacterRuntime runtime)
        {
            return runtime != null && runtime.Data != null
                ? runtime.Data.Role.ToString()
                : "Player";
        }

        private static string ResolveRoomId(CampusCharacterRuntime runtime, StorageInventoryProfile profile)
        {
            if (runtime != null && runtime.Data != null && !string.IsNullOrWhiteSpace(runtime.Data.CurrentRoomId))
            {
                return runtime.Data.CurrentRoomId.Trim();
            }

            return profile != null ? profile.RoomId : string.Empty;
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
            string source = string.IsNullOrWhiteSpace(ownerId) ? "player" : ownerId.Trim();
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
    }
}
