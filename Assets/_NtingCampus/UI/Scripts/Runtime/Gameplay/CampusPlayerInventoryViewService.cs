using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public static class CampusPlayerInventoryViewService
    {
        public static bool TryOpen(
            CampusCharacterRuntime actor,
            StorageContainerModel externalContainer,
            GameObject groundDropContext,
            bool includeBackpack,
            out string message)
        {
            return TryOpen(
                actor,
                externalContainer,
                groundDropContext,
                includeBackpack,
                false,
                string.Empty,
                string.Empty,
                -1,
                null,
                out message);
        }

        public static bool TryOpen(
            CampusCharacterRuntime actor,
            StorageContainerModel externalContainer,
            GameObject groundDropContext,
            bool includeBackpack,
            bool forceIllegalExternalTake,
            string externalTakeSourceLocation,
            string externalTakeOwnerId,
            int externalTakeSuspicionRiskOverride,
            Func<bool> closeWhenExternalTakeUnavailable,
            out string message)
        {
            message = string.Empty;
            if (actor == null || actor.Data == null || !actor.Data.IsPlayerControlled)
            {
                message = StorageTextCatalog.Get(StorageTextId.MissingItemOrSource);
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, true);
            if (inventory == null)
            {
                message = StorageTextCatalog.Get(StorageTextId.MissingItemOrSource);
                return false;
            }

            StorageWindowUI window = UnityEngine.Object.FindFirstObjectByType<StorageWindowUI>(UnityEngine.FindObjectsInactive.Include);
            if (window == null)
            {
                GameObject windowObject = new GameObject("Canvas_Storage", typeof(RectTransform), typeof(StorageWindowUI));
                window = windowObject.GetComponent<StorageWindowUI>();
            }

            GameObject dropContext = groundDropContext != null ? groundDropContext : actor.gameObject;
            window.SetGroundDropContext(dropContext);
            window.SetActorContext(actor.gameObject);
            window.SetTransferHandler(CampusStorageTransferHandler.Instance);
            window.OpenPlayerStorage(
                inventory.Hands,
                inventory.Pockets,
                inventory.Backpack,
                includeBackpack && inventory.HasBackpack,
                externalContainer,
                includeBackpack);
            if (forceIllegalExternalTake)
            {
                window.SetExternalTakeIllegalContext(
                    externalTakeSourceLocation,
                    externalTakeOwnerId,
                    externalTakeSuspicionRiskOverride,
                    closeWhenExternalTakeUnavailable);
            }

            return true;
        }
    }
}
