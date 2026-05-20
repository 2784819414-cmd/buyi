using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    public static class StorageMemorySerializer
    {
        public static StorageMemorySaveData ToSaveData(IEnumerable<StorageContainerModel> containers)
        {
            StorageMemorySaveData data = new StorageMemorySaveData();
            if (containers == null)
            {
                return data;
            }

            foreach (StorageContainerModel container in containers)
            {
                if (container != null)
                {
                    data.Containers.Add(ToContainerSaveData(container));
                }
            }

            return data;
        }

        public static void LoadInto(StorageMemory memory, StorageMemorySaveData data)
        {
            if (memory == null)
            {
                return;
            }

            memory.ClearContainers();
            if (data == null || data.Containers == null)
            {
                return;
            }

            for (int i = 0; i < data.Containers.Count; i++)
            {
                LoadContainer(memory, data.Containers[i]);
            }
        }

        private static StorageContainerSaveData ToContainerSaveData(StorageContainerModel container)
        {
            StorageContainerSaveData data = new StorageContainerSaveData
            {
                Id = container.Id,
                DisplayName = container.DisplayName,
                LocalizedDisplayName = container.LocalizedDisplayName,
                Columns = container.Columns,
                Rows = container.Rows,
                MaxWeight = container.MaxWeight,
                AccessPolicy = container.AccessPolicy,
                OwnerId = container.OwnerId,
                OwnerRole = container.OwnerRole,
                RoomId = container.RoomId,
                AllowTakingContents = container.AllowTakingContents,
                IsPlayerCarried = container.IsPlayerCarried,
                SuspicionRisk = container.SuspicionRisk
            };

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null)
                {
                    data.Items.Add(ToItemSaveData(item));
                }
            }

            return data;
        }

        private static StorageItemSaveData ToItemSaveData(StorageItemModel item)
        {
            return new StorageItemSaveData
            {
                DefinitionId = item.DefinitionId,
                InstanceId = string.IsNullOrWhiteSpace(item.InstanceId) ? item.Id : item.InstanceId,
                DisplayName = item.DisplayName,
                LocalizedDisplayName = item.LocalizedDisplayName,
                Width = item.CurrentWidth,
                Height = item.CurrentHeight,
                Weight = item.Weight,
                Description = item.Description,
                LocalizedDescription = item.LocalizedDescription,
                X = item.X,
                Y = item.Y,
                Rotated = item.Rotated,
                ThemeColor = item.ThemeColor,
                IsUsable = item.IsUsable,
                UseActionId = item.UseActionId,
                ConsumeOnUse = item.ConsumeOnUse,
                UseText = item.UseText,
                LocalizedUseText = item.LocalizedUseText,
                LegalState = item.LegalState,
                OwnerId = item.OwnerId,
                SourceContainerId = item.SourceContainerId,
                SourceRoomId = item.SourceRoomId,
                SourceLocation = item.SourceLocation,
                AllowTaking = item.AllowTaking,
                StolenDuringSession = item.StolenDuringSession,
                SuspicionRisk = item.SuspicionRisk
            };
        }

        private static void LoadContainer(StorageMemory memory, StorageContainerSaveData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
            {
                return;
            }

            StorageContainerModel container = memory.GetOrCreateContainer(
                data.Id,
                data.DisplayName,
                data.LocalizedDisplayName,
                data.Columns,
                data.Rows,
                data.MaxWeight);
            container.AccessPolicy = data.AccessPolicy;
            container.OwnerId = data.OwnerId;
            container.OwnerRole = data.OwnerRole;
            container.RoomId = data.RoomId;
            container.AllowTakingContents = data.AllowTakingContents;
            container.IsPlayerCarried = data.IsPlayerCarried;
            container.SuspicionRisk = data.SuspicionRisk;
            container.Items.Clear();

            if (data.Items == null)
            {
                return;
            }

            for (int i = 0; i < data.Items.Count; i++)
            {
                StorageItemSaveData itemData = data.Items[i];
                StorageItemModel item = CreateItem(memory.ItemRegistry, itemData);
                if (item != null && !container.PlaceItem(item, itemData.X, itemData.Y))
                {
                    Debug.LogWarning("Storage memory load skipped item '" + item.DisplayName + "' because its saved position is invalid.");
                }
            }
        }

        private static StorageItemModel CreateItem(StorageItemRegistry registry, StorageItemSaveData data)
        {
            if (data == null)
            {
                return null;
            }

            StorageItemModel item = registry != null && !string.IsNullOrWhiteSpace(data.DefinitionId)
                ? registry.CreateItem(data.DefinitionId, data.InstanceId)
                : null;
            if (item == null)
            {
                item = new StorageItemModel
                {
                    Id = data.InstanceId,
                    InstanceId = data.InstanceId,
                    DefinitionId = data.DefinitionId
                };
            }

            item.DisplayName = data.DisplayName;
            item.LocalizedDisplayName = data.LocalizedDisplayName;
            item.Width = Mathf.Max(1, data.Width);
            item.Height = Mathf.Max(1, data.Height);
            item.Weight = Mathf.Max(0f, data.Weight);
            item.Description = data.Description;
            item.LocalizedDescription = data.LocalizedDescription;
            item.Rotated = data.Rotated;
            item.ThemeColor = data.ThemeColor;
            item.IsUsable = data.IsUsable;
            item.UseActionId = data.UseActionId;
            item.ConsumeOnUse = data.ConsumeOnUse;
            item.UseText = data.UseText;
            item.LocalizedUseText = data.LocalizedUseText;
            item.Icon = StorageItemIconUtility.Resolve(item.DefinitionId, item.Icon);
            item.LegalState = data.LegalState == StorageItemLegalState.Unknown
                ? StorageItemLegalState.Personal
                : data.LegalState;
            item.OwnerId = data.OwnerId;
            item.SourceContainerId = data.SourceContainerId;
            item.SourceRoomId = data.SourceRoomId;
            item.SourceLocation = data.SourceLocation;
            item.AllowTaking = data.LegalState == StorageItemLegalState.Unknown || data.AllowTaking;
            item.StolenDuringSession = data.StolenDuringSession;
            item.SuspicionRisk = data.SuspicionRisk;
            return item;
        }
    }
}
