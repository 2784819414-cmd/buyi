using System;
using System.Collections.Generic;
using UnityEngine;
using NtingCampus.Gameplay.UI;

namespace Nting.Storage
{
    [Serializable]
    public sealed class StorageContainerDefinition
    {
        public string Id;
        public string DisplayName;
        public CampusLocalizedText LocalizedDisplayName;
        public int Columns = 1;
        public int Rows = 1;
        public float MaxWeight = 10f;
        public StorageContainerAccessPolicy AccessPolicy = StorageContainerAccessPolicy.Open;
        public string OwnerRole;
        public bool AllowTakingContents = true;
        public bool IsPlayerCarried;
        public int SuspicionRisk;

        public StorageContainerModel GetOrCreate(StorageMemory memory, string ownerId, string roomId)
        {
            if (memory == null || string.IsNullOrWhiteSpace(Id))
            {
                return null;
            }

            StorageContainerModel container = memory.GetOrCreateContainer(
                Id.Trim(),
                string.IsNullOrWhiteSpace(DisplayName) ? Id.Trim() : DisplayName.Trim(),
                LocalizedDisplayName,
                Columns,
                Rows,
                MaxWeight);
            ApplyRuntimeSettings(container, ownerId, roomId);
            return container;
        }

        public void ApplyRuntimeSettings(StorageContainerModel container, string ownerId, string roomId)
        {
            if (container == null)
            {
                return;
            }

            container.AccessPolicy = AccessPolicy;
            container.OwnerId = ownerId;
            container.OwnerRole = OwnerRole;
            container.RoomId = roomId;
            container.AllowTakingContents = AllowTakingContents;
            container.IsPlayerCarried = IsPlayerCarried;
            container.SuspicionRisk = Mathf.Max(0, SuspicionRisk);
        }
    }

    [Serializable]
    public sealed class StorageStarterItemDefinition
    {
        public string ContainerId;
        public string DefinitionId;
        public string InstanceId;
        public int X;
        public int Y;

        public void TrySeed(StorageMemory memory)
        {
            if (memory == null ||
                string.IsNullOrWhiteSpace(ContainerId) ||
                string.IsNullOrWhiteSpace(DefinitionId))
            {
                return;
            }

            memory.TryPlaceNewItem(ContainerId, DefinitionId, InstanceId, X, Y);
        }
    }

    [CreateAssetMenu(menuName = "Nting/Storage/Inventory Profile", fileName = "StorageInventoryProfile")]
    public sealed class StorageInventoryProfile : ScriptableObject
    {
        public string OwnerId = "player";
        public string RoomId;
        public List<StorageContainerDefinition> Containers = new List<StorageContainerDefinition>();
        public List<string> HandContainerIds = new List<string>();
        public List<string> PocketContainerIds = new List<string>();
        public string BackpackContainerId;
        public List<StorageStarterItemDefinition> StarterItems = new List<StorageStarterItemDefinition>();

        public void EnsureContainers(StorageMemory memory)
        {
            if (memory == null)
            {
                return;
            }

            for (int i = 0; i < Containers.Count; i++)
            {
                Containers[i]?.GetOrCreate(memory, OwnerId, RoomId);
            }
        }

        public StorageContainerModel[] GetContainers(StorageMemory memory, List<string> ids)
        {
            EnsureContainers(memory);
            if (memory == null || ids == null || ids.Count == 0)
            {
                return Array.Empty<StorageContainerModel>();
            }

            StorageContainerModel[] result = new StorageContainerModel[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(ids[i]))
                {
                    memory.TryGetContainer(ids[i], out result[i]);
                }
            }

            return result;
        }

        public StorageContainerModel GetBackpack(StorageMemory memory)
        {
            EnsureContainers(memory);
            if (memory == null || string.IsNullOrWhiteSpace(BackpackContainerId))
            {
                return null;
            }

            memory.TryGetContainer(BackpackContainerId, out StorageContainerModel backpack);
            return backpack;
        }

        public void EnsureStarterItems(StorageMemory memory, string seedFlag)
        {
            if (memory == null || memory.IsSessionFlagSet(seedFlag))
            {
                return;
            }

            for (int i = 0; i < StarterItems.Count; i++)
            {
                StarterItems[i]?.TrySeed(memory);
            }

            memory.SetSessionFlag(seedFlag);
        }

        public static StorageInventoryProfile CreateDefaultPlayerProfile()
        {
            StorageInventoryProfile profile = CreateInstance<StorageInventoryProfile>();
            profile.hideFlags = HideFlags.DontSave;
            profile.OwnerId = "player";
            profile.Containers.Add(Carried("hand_left", StorageTextId.LeftHand, 2, 2, 3f));
            profile.Containers.Add(Carried("hand_right", StorageTextId.RightHand, 2, 2, 3f));
            profile.Containers.Add(Carried("pocket_left_chest", StorageTextId.LeftChestPocket, 2, 3, 1.5f));
            profile.Containers.Add(Carried("pocket_right_chest", StorageTextId.RightChestPocket, 2, 3, 1.5f));
            profile.Containers.Add(Carried("pocket_left_pants", StorageTextId.LeftPantsPocket, 2, 3, 2f));
            profile.Containers.Add(Carried("pocket_right_pants", StorageTextId.RightPantsPocket, 2, 3, 2f));
            profile.Containers.Add(Carried("school_backpack", StorageTextId.StudentBackpack, 5, 6, 20f));
            profile.HandContainerIds.AddRange(new[] { "hand_left", "hand_right" });
            profile.PocketContainerIds.AddRange(new[]
            {
                "pocket_left_chest",
                "pocket_right_chest",
                "pocket_left_pants",
                "pocket_right_pants"
            });
            profile.BackpackContainerId = "school_backpack";
            profile.StarterItems.Add(Starter("pocket_left_chest", "phone", "phone_player_001", 0, 0));
            profile.StarterItems.Add(Starter("pocket_left_chest", "note", "note_player_001", 1, 0));
            profile.StarterItems.Add(Starter("pocket_right_chest", "key", "key_player_001", 0, 0));
            profile.StarterItems.Add(Starter("pocket_left_pants", "snack", "snack_player_001", 0, 1));
            profile.StarterItems.Add(Starter("school_backpack", "textbook", "textbook_player_001", 0, 0));
            profile.StarterItems.Add(Starter("school_backpack", "workbook", "workbook_player_001", 2, 0));
            profile.StarterItems.Add(Starter("school_backpack", "pencil_case", "pencil_case_player_001", 2, 2));
            profile.StarterItems.Add(Starter("school_backpack", "lunch_box", "lunch_box_player_001", 0, 3));
            return profile;
        }

        private static StorageContainerDefinition Carried(string id, StorageTextId nameId, int columns, int rows, float maxWeight)
        {
            CampusLocalizedText localizedName = BuildLocalizedText(nameId);
            return new StorageContainerDefinition
            {
                Id = id,
                DisplayName = localizedName.ResolvePrimary(id),
                LocalizedDisplayName = localizedName,
                Columns = columns,
                Rows = rows,
                MaxWeight = maxWeight,
                AccessPolicy = StorageContainerAccessPolicy.PlayerCarried,
                OwnerRole = "Player",
                AllowTakingContents = true,
                IsPlayerCarried = true
            };
        }

        private static CampusLocalizedText BuildLocalizedText(StorageTextId id)
        {
            return new CampusLocalizedText(
                StorageTextCatalog.Get(CampusDisplayLanguage.Chinese, id),
                StorageTextCatalog.Get(CampusDisplayLanguage.English, id));
        }

        private static StorageStarterItemDefinition Starter(
            string containerId,
            string definitionId,
            string instanceId,
            int x,
            int y)
        {
            return new StorageStarterItemDefinition
            {
                ContainerId = containerId,
                DefinitionId = definitionId,
                InstanceId = instanceId,
                X = x,
                Y = y
            };
        }
    }
}
