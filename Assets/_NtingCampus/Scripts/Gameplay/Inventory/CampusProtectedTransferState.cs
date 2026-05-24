using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Inventory
{
    internal static class CampusProtectedTransferState
    {
        // Pending protected transfer means:
        // 1. the item can still be taken normally inside its source room
        // 2. its original source metadata must stay stable
        // 3. once it leaves the source room, it upgrades into stolen evidence
        public static bool IsPendingProtectedTransfer(StorageItemModel item)
        {
            return item != null &&
                   IsPendingProtectedTransfer(item.LegalState, item.StolenDuringSession);
        }

        public static bool IsPendingProtectedTransfer(StorageItemLegalState legalState, bool stolenDuringSession)
        {
            return legalState == StorageItemLegalState.PendingProtectedTransfer &&
                   !stolenDuringSession;
        }

        public static bool HasLeftPendingTransferSourceRoom(StorageItemModel item, string currentRoomId)
        {
            return item != null &&
                   HasLeftPendingTransferSourceRoom(
                       item.LegalState,
                       item.StolenDuringSession,
                       item.SourceRoomId,
                       currentRoomId);
        }

        public static bool HasLeftPendingTransferSourceRoom(
            StorageItemLegalState legalState,
            bool stolenDuringSession,
            string sourceRoomId,
            string currentRoomId)
        {
            if (!IsPendingProtectedTransfer(legalState, stolenDuringSession))
            {
                return false;
            }

            string normalizedCurrentRoomId = NormalizeId(currentRoomId);
            string normalizedSourceRoomId = NormalizeId(sourceRoomId);
            if (string.IsNullOrEmpty(normalizedCurrentRoomId) ||
                string.IsNullOrEmpty(normalizedSourceRoomId))
            {
                return false;
            }

            return !string.Equals(
                normalizedCurrentRoomId,
                normalizedSourceRoomId,
                StringComparison.OrdinalIgnoreCase);
        }

        public static void BeginPendingTransfer(StorageItemModel item, StorageContainerModel source)
        {
            if (item == null || source == null)
            {
                return;
            }

            BeginPendingTransfer(
                item,
                source.Id,
                source.RoomId,
                source.OwnerId,
                source.DisplayName,
                source.SuspicionRisk);
        }

        public static void BeginPendingTransfer(
            StorageItemModel item,
            string sourceContainerId,
            string sourceRoomId,
            string ownerId,
            string sourceLocation,
            int suspicionRisk)
        {
            if (item == null)
            {
                return;
            }

            item.EnsureEvidence().BeginProtectedTransferPending(
                sourceContainerId,
                sourceRoomId,
                ownerId,
                sourceLocation,
                suspicionRisk);
        }

        public static void BeginPendingTransfer(
            CampusDroppedStorageItem droppedItem,
            string sourceContainerId,
            string sourceRoomId,
            string ownerId,
            string sourceLocation,
            int suspicionRisk)
        {
            if (droppedItem == null)
            {
                return;
            }

            droppedItem.LegalState = StorageItemLegalState.PendingProtectedTransfer;
            droppedItem.OwnerId = NormalizeId(ownerId);
            droppedItem.SourceContainerId = NormalizeId(sourceContainerId);
            droppedItem.SourceRoomId = NormalizeId(sourceRoomId);
            droppedItem.SourceLocation = NormalizeDisplayText(sourceLocation);
            droppedItem.StolenDuringSession = false;
            droppedItem.SuspicionRisk = Math.Max(0, suspicionRisk);
        }

        public static void ClearPendingTransfer(StorageItemModel item, string ownerId)
        {
            if (item == null)
            {
                return;
            }

            item.EnsureEvidence().ClearProtectedTransferPending(ownerId);
        }

        public static bool TryPromotePendingTransferToEvidence(
            StorageItemModel item,
            string currentRoomId,
            int suspicionRisk)
        {
            if (!HasLeftPendingTransferSourceRoom(item, currentRoomId))
            {
                return false;
            }

            item.EnsureEvidence().PromoteProtectedTransferToStolenEvidence(
                item.OwnerId,
                Math.Max(item.SuspicionRisk, suspicionRisk));
            return true;
        }

        public static bool ShouldTreatDroppedPickupAsIllegal(
            StorageItemLegalState legalState,
            bool stolenDuringSession,
            string sourceRoomId,
            string ownerId,
            string actorCurrentRoomId,
            string actorId)
        {
            if (legalState == StorageItemLegalState.Stolen)
            {
                return false;
            }

            if (IsPendingProtectedTransfer(legalState, stolenDuringSession))
            {
                return HasLeftPendingTransferSourceRoom(
                    legalState,
                    stolenDuringSession,
                    sourceRoomId,
                    actorCurrentRoomId);
            }

            string normalizedOwnerId = NormalizeId(ownerId);
            string normalizedActorId = NormalizeId(actorId);
            if (string.IsNullOrEmpty(normalizedOwnerId) ||
                string.IsNullOrEmpty(normalizedActorId))
            {
                return false;
            }

            return !string.Equals(
                normalizedOwnerId,
                normalizedActorId,
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldDisplayPendingCheckout(
            StorageItemModel item,
            StorageContainerModel container)
        {
            return item != null &&
                   IsPendingProtectedTransfer(item) &&
                   container != null &&
                   container.IsCarriedInventory;
        }

        public static string ResolveActorCurrentRoomId(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   runtime.Data != null &&
                   !string.IsNullOrWhiteSpace(runtime.Data.CurrentRoomId)
                ? runtime.Data.CurrentRoomId.Trim()
                : string.Empty;
        }

        public static int PromotePendingTransfersForRoomTransition(
            CampusCharacterRuntime runtime,
            string previousRoomId,
            string currentRoomId)
        {
            if (runtime == null ||
                !CampusCharacterInventoryService.TryGetExistingInventory(runtime, out CampusCharacterInventory inventory))
            {
                return 0;
            }

            int promotedCount = 0;
            promotedCount += PromotePendingTransfers(inventory.Hands, previousRoomId, currentRoomId);
            promotedCount += PromotePendingTransfers(inventory.Pockets, previousRoomId, currentRoomId);
            promotedCount += PromotePendingTransfers(inventory.Backpack, previousRoomId, currentRoomId);
            return promotedCount;
        }

        private static int PromotePendingTransfers(
            StorageContainerModel[] containers,
            string previousRoomId,
            string currentRoomId)
        {
            if (containers == null || containers.Length == 0)
            {
                return 0;
            }

            int promotedCount = 0;
            for (int i = 0; i < containers.Length; i++)
            {
                promotedCount += PromotePendingTransfers(containers[i], previousRoomId, currentRoomId);
            }

            return promotedCount;
        }

        private static int PromotePendingTransfers(
            StorageContainerModel container,
            string previousRoomId,
            string currentRoomId)
        {
            if (container == null || container.Items == null)
            {
                return 0;
            }

            int promotedCount = 0;
            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null &&
                    ShouldPromoteForRoomTransition(item, previousRoomId, currentRoomId))
                {
                    item.EnsureEvidence().PromoteProtectedTransferToStolenEvidence(
                        item.OwnerId,
                        item.SuspicionRisk);
                    promotedCount++;
                }
            }

            return promotedCount;
        }

        private static bool ShouldPromoteForRoomTransition(
            StorageItemModel item,
            string previousRoomId,
            string currentRoomId)
        {
            if (!IsPendingProtectedTransfer(item))
            {
                return false;
            }

            string normalizedSourceRoomId = NormalizeId(item.SourceRoomId);
            string normalizedPreviousRoomId = NormalizeId(previousRoomId);
            if (string.IsNullOrEmpty(normalizedSourceRoomId) ||
                !string.Equals(
                    normalizedSourceRoomId,
                    normalizedPreviousRoomId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalizedCurrentRoomId = NormalizeId(currentRoomId);
            return !string.Equals(
                normalizedCurrentRoomId,
                normalizedSourceRoomId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeDisplayText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
