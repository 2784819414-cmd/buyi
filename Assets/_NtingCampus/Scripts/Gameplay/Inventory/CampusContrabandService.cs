using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public static class CampusContrabandService
    {
        public const string ConfiscatedContainerId = "campus_confiscated_items";

        public static bool TryFindCarriedContraband(out StorageItemModel item, out StorageContainerModel container)
        {
            return TryFindCarriedContraband(ResolveCurrentPlayerRuntime(), out item, out container);
        }

        public static bool TryFindCarriedContraband(
            CampusCharacterRuntime targetRuntime,
            out StorageItemModel item,
            out StorageContainerModel container)
        {
            item = null;
            container = null;

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(targetRuntime, false);
            if (TryFindInContainers(inventory.Hands, out item, out container))
            {
                return true;
            }

            if (TryFindInContainers(inventory.Pockets, out item, out container))
            {
                return true;
            }

            return TryFindInContainer(inventory.Backpack, out item, out container);
        }

        public static int CountConfiscatedItems()
        {
            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null ||
                !memory.TryGetContainer(ConfiscatedContainerId, out StorageContainerModel container) ||
                container == null ||
                container.Items == null)
            {
                return 0;
            }

            return container.Items.Count;
        }

        public static StorageContainerModel GetOrCreateConfiscatedContainer(
            StorageMemory memory,
            CampusGameplayRoom room)
        {
            if (memory == null)
            {
                return null;
            }

            StorageContainerModel container = memory.GetOrCreateContainer(
                ConfiscatedContainerId,
                "Confiscated Evidence",
                8,
                12,
                200f);
            if (container == null)
            {
                return null;
            }

            container.AccessPolicy = StorageContainerAccessPolicy.StaffOnly;
            container.OwnerId = "campus_office";
            container.OwnerRole = "Staff";
            container.RoomId = room != null ? room.RoomId : string.Empty;
            container.AllowTakingContents = false;
            container.IsPlayerCarried = false;
            container.SuspicionRisk = 0;
            return container;
        }

        public static bool TryFindConfiscationSpace(
            StorageContainerModel container,
            StorageItemModel item,
            out Vector2Int targetPosition)
        {
            targetPosition = default;
            if (container == null || item == null)
            {
                return false;
            }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (container.FindFirstFit(item, out targetPosition))
                {
                    return true;
                }

                container.Rows += Mathf.Max(2, item.CurrentHeight);
            }

            return false;
        }

        private static bool TryFindInContainers(
            StorageContainerModel[] containers,
            out StorageItemModel item,
            out StorageContainerModel container)
        {
            item = null;
            container = null;
            if (containers == null)
            {
                return false;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                if (TryFindInContainer(containers[i], out item, out container))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindInContainer(
            StorageContainerModel candidate,
            out StorageItemModel item,
            out StorageContainerModel container)
        {
            item = null;
            container = null;
            if (candidate == null || candidate.Items == null)
            {
                return false;
            }

            for (int i = 0; i < candidate.Items.Count; i++)
            {
                StorageItemModel candidateItem = candidate.Items[i];
                if (candidateItem != null && candidateItem.IsStolenEvidence)
                {
                    item = candidateItem;
                    container = candidate;
                    return true;
                }
            }

            return false;
        }

        private static CampusCharacterRuntime ResolveCurrentPlayerRuntime()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
        }
    }
}
