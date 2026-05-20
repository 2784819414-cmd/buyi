using Nting.Storage;
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

        public bool TryUse(StorageItemModel item, StorageGridUI sourceGrid, out string statusMessage)
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
                return true;
            }

            CampusInventoryTransferService service = CampusInventoryTransferService.Resolve();
            StorageContainerModel source = sourceGrid != null ? sourceGrid.Container : item.CurrentContainer;
            StorageTransferContext context = new StorageTransferContext
            {
                Reason = StorageTransferReason.UseItem
            };
            if (service.TryConsumeItem(item, source, context, out StorageTransferResult result))
            {
                return true;
            }

            statusMessage = string.IsNullOrWhiteSpace(result.Message)
                ? StorageTextCatalog.Format(StorageTextId.CouldNotConsumeItem, ResolveItemName(item))
                : result.Message;
            return false;
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
