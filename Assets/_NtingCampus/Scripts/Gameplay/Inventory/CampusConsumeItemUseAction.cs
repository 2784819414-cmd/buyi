using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public sealed class CampusConsumeItemUseAction : IStorageItemUseAction
    {
        public string ActionId => StorageItemUseUtility.ConsumeFoodActionId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAction()
        {
            StorageItemUseActionRegistry.Register(new CampusConsumeItemUseAction());
        }

        public bool CanUse(StorageItemModel item, StorageGridUI sourceGrid, out string reason)
        {
            reason = string.Empty;
            if (item == null)
            {
                reason = StorageTextCatalog.Get(StorageTextId.ItemCannotBeUsed);
                return false;
            }

            if (!CampusCharacterInventoryService.IsHandContainerId(item.CurrentContainerId))
            {
                reason = StorageTextCatalog.Get(StorageTextId.MoveIntoHandFirst);
                return false;
            }

            return true;
        }

        public bool TryUse(
            StorageItemModel item,
            StorageGridUI sourceGrid,
            StorageItemUseContext context,
            out string statusMessage)
        {
            if (!CanUse(item, sourceGrid, out statusMessage))
            {
                return false;
            }

            statusMessage = string.IsNullOrWhiteSpace(item.GetUseText())
                ? StorageTextCatalog.Format(StorageTextId.UsedItem, ResolveItemName(item))
                : item.GetUseText();

            if (!item.ConsumeOnUse)
            {
                ApplyUseEffects(item, context);
                return true;
            }

            CampusInventoryTransferService service = CampusInventoryTransferService.Resolve();
            StorageContainerModel source = sourceGrid != null ? sourceGrid.Container : item.CurrentContainer;
            StorageTransferContext transferContext = BuildTransferContext(context);
            if (service.TryConsumeItem(item, source, transferContext, out StorageTransferResult result))
            {
                ApplyUseEffects(item, context);
                return true;
            }

            statusMessage = string.IsNullOrWhiteSpace(result.Message)
                ? StorageTextCatalog.Format(StorageTextId.CouldNotConsumeItem, ResolveItemName(item))
                : result.Message;
            return false;
        }

        private static StorageTransferContext BuildTransferContext(StorageItemUseContext context)
        {
            StorageTransferReason reason = context != null
                ? context.Reason
                : StorageTransferReason.UseItem;
            return StorageTransferContext.ForActor(context != null ? context.Actor : null, reason);
        }

        private static void ApplyUseEffects(StorageItemModel item, StorageItemUseContext context)
        {
            if (item == null || item.StaminaRestore <= 0f || context == null || context.Actor == null)
            {
                return;
            }

            CampusCharacterRuntime runtime = context.Actor.GetComponentInParent<CampusCharacterRuntime>();
            if (runtime == null)
            {
                return;
            }

            CampusCharacterStaminaController stamina = runtime.GetComponent<CampusCharacterStaminaController>();
            if (stamina == null)
            {
                stamina = runtime.gameObject.AddComponent<CampusCharacterStaminaController>();
            }

            stamina.RestoreStamina(item.StaminaRestore);
        }

        private static string ResolveItemName(StorageItemModel item)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.GetDisplayName()
                : item != null && !string.IsNullOrWhiteSpace(item.DefinitionId)
                    ? item.DefinitionId
                    : StorageTextCatalog.Get(StorageTextId.ItemFallback);
        }
    }
}
