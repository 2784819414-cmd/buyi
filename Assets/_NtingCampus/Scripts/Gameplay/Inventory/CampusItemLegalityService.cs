using System;
using Nting.Storage;

namespace NtingCampus.Gameplay.Inventory
{
    public sealed class CampusItemLegalityService
    {
        public bool IsIllegalTake(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            StorageTransferContext context,
            string actorId)
        {
            if (IsExplicitlyAllowed(context))
            {
                return false;
            }

            if (IsExplicitlyIllegal(context))
            {
                return true;
            }

            if (IsKnownEvidence(item) || IsCarriedInventoryMove(source) || source == null)
            {
                return false;
            }

            if (IsAllowedProtectedTransferTake(source))
            {
                return false;
            }

            if (IsActorOwner(actorId, item, source))
            {
                return false;
            }

            return IsTakingProtectedItemAway(item, source, target);
        }

        public void MarkIllegalMove(
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            string roomId,
            string ownerId,
            int suspicionRisk)
        {
            if (item == null)
            {
                return;
            }

            item.EnsureEvidence().MarkAsStolen(
                source,
                roomId,
                ownerId,
                context != null ? context.SourceLocation : string.Empty,
                suspicionRisk);
        }

        public void PreservePersonalOwnership(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            string actorId)
        {
            if (item == null || string.IsNullOrWhiteSpace(actorId))
            {
                return;
            }

            if (IsAllowedProtectedTransferTake(source))
            {
                MarkUnclearedProtectedTransferItem(item, source);
                return;
            }

            if (!IsCarriedInventory(source) || target == null || IsCarriedInventory(target) || IsOpenTarget(target))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(item.OwnerId))
            {
                item.OwnerId = actorId.Trim();
            }

            if (item.LegalState == StorageItemLegalState.Unknown)
            {
                item.LegalState = StorageItemLegalState.Personal;
            }
        }

        private static bool IsExplicitlyAllowed(StorageTransferContext context)
        {
            return context != null && context.AllowProtectedTake;
        }

        private static bool IsExplicitlyIllegal(StorageTransferContext context)
        {
            return context != null && context.ForceIllegal;
        }

        private static bool IsKnownEvidence(StorageItemModel item)
        {
            return item != null && item.IsStolenEvidence;
        }

        private static bool IsCarriedInventoryMove(StorageContainerModel source)
        {
            return IsCarriedInventory(source);
        }

        private static bool IsTakingProtectedItemAway(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target)
        {
            if (!IsProtectedSource(source))
            {
                return item != null && !item.AllowTaking;
            }

            return IsTakingAway(target) || (item != null && !item.AllowTaking);
        }

        private static bool IsActorOwner(string actorId, StorageItemModel item, StorageContainerModel source)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return false;
            }

            string normalizedActorId = actorId.Trim();
            if (item != null &&
                !string.IsNullOrWhiteSpace(item.OwnerId) &&
                string.Equals(item.OwnerId.Trim(), normalizedActorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return source != null &&
                   !string.IsNullOrWhiteSpace(source.OwnerId) &&
                   string.Equals(source.OwnerId.Trim(), normalizedActorId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProtectedSource(StorageContainerModel source)
        {
            return source.AccessPolicy == StorageContainerAccessPolicy.OwnedPrivate ||
                   source.AccessPolicy == StorageContainerAccessPolicy.ProtectedPublic ||
                   source.AccessPolicy == StorageContainerAccessPolicy.StaffOnly ||
                   source.AccessPolicy == StorageContainerAccessPolicy.ProtectedTransfer ||
                   !source.AllowTakingContents;
        }

        private static bool IsAllowedProtectedTransferTake(StorageContainerModel source)
        {
            return source != null &&
                   source.AccessPolicy == StorageContainerAccessPolicy.ProtectedTransfer &&
                   source.AllowTakingContents;
        }

        private static void MarkUnclearedProtectedTransferItem(StorageItemModel item, StorageContainerModel source)
        {
            if (item == null || source == null)
            {
                return;
            }

            item.LegalState = StorageItemLegalState.Suspicious;
            item.OwnerId = source.OwnerId;
            item.SourceContainerId = source.Id;
            item.SourceRoomId = source.RoomId;
            item.SourceLocation = source.DisplayName;
            item.AllowTaking = false;
            item.StolenDuringSession = false;
            item.SuspicionRisk = source.SuspicionRisk;
        }

        private static bool IsTakingAway(StorageContainerModel target)
        {
            return target == null ||
                   target.IsPlayerCarried ||
                   target.AccessPolicy == StorageContainerAccessPolicy.PlayerCarried ||
                   target.AccessPolicy == StorageContainerAccessPolicy.Ground;
        }

        private static bool IsCarriedInventory(StorageContainerModel container)
        {
            return container != null &&
                   (container.IsPlayerCarried ||
                    container.AccessPolicy == StorageContainerAccessPolicy.PlayerCarried);
        }

        private static bool IsOpenTarget(StorageContainerModel container)
        {
            return container.AccessPolicy == StorageContainerAccessPolicy.Open ||
                   container.AccessPolicy == StorageContainerAccessPolicy.Ground;
        }
    }
}
