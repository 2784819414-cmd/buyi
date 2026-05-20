using Nting.Storage;
using NtingCampus.Gameplay.Events;

namespace NtingCampus.Gameplay.Inventory
{
    public sealed class CampusInventoryEventPublisher
    {
        private readonly CampusGameplayEventHub eventHub;
        private readonly CampusInventoryContextResolver contextResolver;

        public CampusInventoryEventPublisher(
            CampusGameplayEventHub eventHub,
            CampusInventoryContextResolver contextResolver)
        {
            this.eventHub = eventHub;
            this.contextResolver = contextResolver;
        }

        public void PublishTransfer(
            StorageItemModel item,
            StorageContainerModel source,
            string targetContainerId,
            StorageTransferContext context,
            bool illegal,
            bool observed)
        {
            if (eventHub == null || item == null)
            {
                return;
            }

            eventHub.PublishItemTransferred(new CampusItemTransferredEvent(
                contextResolver.ResolveActorId(context),
                ResolveInstanceId(item),
                item.DefinitionId,
                ResolveItemName(item),
                source != null ? source.Id : string.Empty,
                targetContainerId,
                contextResolver.ResolveRoomId(context, source),
                context.Reason,
                illegal,
                observed));
        }

        public void PublishProtectedItemMoved(
            StorageItemModel item,
            StorageContainerModel source,
            string targetContainerId,
            StorageTransferContext context,
            string ownerId,
            int suspicionRisk)
        {
            if (eventHub == null || item == null)
            {
                return;
            }

            eventHub.PublishProtectedItemMoved(new CampusProtectedItemMovedEvent(
                0,
                contextResolver.ResolveActorId(context),
                ownerId,
                ResolveInstanceId(item),
                item.DefinitionId,
                ResolveItemName(item),
                source != null ? source.Id : string.Empty,
                targetContainerId,
                contextResolver.ResolveRoomId(context, source),
                contextResolver.ResolveActorWorldPosition(context),
                context.Reason,
                suspicionRisk));
        }

        public static string ResolveInstanceId(StorageItemModel item)
        {
            return item == null
                ? string.Empty
                : !string.IsNullOrWhiteSpace(item.InstanceId)
                    ? item.InstanceId
                    : item.Id;
        }

        public static string ResolveItemName(StorageItemModel item)
        {
            if (item == null)
            {
                return StorageTextCatalog.Get(StorageTextId.ItemFallback);
            }

            return !string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.GetDisplayName()
                : !string.IsNullOrWhiteSpace(item.DefinitionId)
                    ? item.DefinitionId
                    : StorageTextCatalog.Get(StorageTextId.ItemFallback);
        }
    }
}
