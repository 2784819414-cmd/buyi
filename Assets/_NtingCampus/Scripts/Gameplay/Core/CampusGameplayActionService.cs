using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    public readonly struct CampusGameplayActionRequest
    {
        public CampusGameplayActionRequest(
            GameObject actor,
            string actionId,
            string payload,
            CampusInteractionAnchor anchor,
            Component directTarget,
            string source)
        {
            Actor = actor;
            ActionId = CampusInteractionActionIds.Normalize(actionId);
            Payload = payload ?? string.Empty;
            Anchor = anchor;
            DirectTarget = directTarget;
            Source = source ?? string.Empty;
        }

        public GameObject Actor { get; }
        public string ActionId { get; }
        public string Payload { get; }
        public CampusInteractionAnchor Anchor { get; }
        public Component DirectTarget { get; }
        public string Source { get; }
    }

    [DisallowMultipleComponent]
    public sealed class CampusGameplayActionService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
        }

        public bool TryExecute(CampusGameplayActionRequest request)
        {
            return CampusInteractionActionExecutor.TryExecute(request, TryExecuteGlobalAction);
        }

        public static bool TryExecuteShared(CampusGameplayActionRequest request)
        {
            CampusGameplayActionService service = ResolveService();
            return service != null
                ? service.TryExecute(request)
                : CampusInteractionActionExecutor.TryExecute(request, TryExecuteStaticGlobalAction);
        }

        public static bool TryExecuteInteraction(CampusInteractionAnchor anchor, GameObject actor)
        {
            if (anchor == null)
            {
                return false;
            }

            Component target = anchor.InteractionTarget as Component;
            return TryExecuteShared(new CampusGameplayActionRequest(
                actor,
                anchor.ActionId,
                anchor.Payload,
                anchor,
                target,
                "interaction_anchor"));
        }

        public static bool TryExecuteCharacterAction(
            CampusCharacterRuntime actor,
            CampusCharacterAction action,
            out StorageTransferResult result)
        {
            return CampusCharacterActionExecutor.TryExecute(actor, action, out result);
        }

        public static bool TryPressInteract(CampusCharacterRuntime actor, CampusInteractionTarget target)
        {
            return CampusCharacterActionExecutor.TryPressInteract(actor, target);
        }

        public static bool TryPressInteract(CampusCharacterRuntime actor, Object target)
        {
            return CampusCharacterActionExecutor.TryPressInteract(actor, target);
        }

        public static bool TryPickUpDroppedItem(
            CampusCharacterRuntime actor,
            CampusDroppedStorageItem droppedItem,
            out StorageTransferResult result)
        {
            return CampusCharacterActionExecutor.TryPickUpDroppedItem(actor, droppedItem, out result);
        }

        public static bool TryTransferItem(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryTransferItem(actor, item, source, target, x, y, out result);
        }

        public static bool TryTransferItem(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryTransferItem(actor, item, source, target, x, y, context, out result);
        }

        public static bool TryTransferItemToFirstFit(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryTransferItemToFirstFit(actor, item, source, targets, out result);
        }

        public static bool TryTransferItemToFirstFit(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryTransferItemToFirstFit(actor, item, source, targets, context, out result);
        }

        public static bool TryDropItemToGround(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            GameObject groundDropContext,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryDropItemToGround(actor, item, source, groundDropContext, out result);
        }

        public static bool TryDropItemToGround(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            GameObject groundDropContext,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryDropItemToGround(actor, item, source, groundDropContext, context, out result);
        }

        private bool TryExecuteGlobalAction(CampusGameplayActionRequest request)
        {
            return false;
        }

        private static bool TryExecuteStaticGlobalAction(CampusGameplayActionRequest request)
        {
            return false;
        }

        private static CampusGameplayActionService ResolveService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.ActionService != null)
            {
                return bootstrap.ActionService;
            }

            return FindFirstObjectByType<CampusGameplayActionService>(FindObjectsInactive.Include);
        }
    }
}
