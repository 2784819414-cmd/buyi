using Nting.Storage;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public static class CampusCharacterActionExecutor
    {
        public static bool TryExecute(
            CampusCharacterRuntime actor,
            CampusCharacterAction action,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (actor == null || action == null)
            {
                return false;
            }

            switch (action.Kind)
            {
                case CampusCharacterActionKind.NoOp:
                    result = new StorageTransferResult(true, false, false, string.Empty, string.Empty);
                    return true;

                case CampusCharacterActionKind.PressInteract:
                    if (TryPressInteract(actor, action.Target))
                    {
                        result = new StorageTransferResult(true, false, false, string.Empty, string.Empty);
                        return true;
                    }

                    return false;

                case CampusCharacterActionKind.PressInteractionAction:
                    return TryPressInteractionAction(
                        actor,
                        action.Target,
                        action.ActionId,
                        action.Payload,
                        out result);

                case CampusCharacterActionKind.PickUpDroppedItem:
                    return action.Target is CampusDroppedStorageItem droppedItem &&
                           TryPickUpDroppedItem(actor, droppedItem, out result);

                case CampusCharacterActionKind.TransferItem:
                    return CampusInventoryActionExecutor.TryTransferItem(
                        actor,
                        action.Item,
                        action.SourceContainer,
                        action.TargetContainer,
                        action.TargetX,
                        action.TargetY,
                        out result);

                case CampusCharacterActionKind.TransferItemToFirstFit:
                    return CampusInventoryActionExecutor.TryTransferItemToFirstFit(
                        actor,
                        action.Item,
                        action.SourceContainer,
                        action.TargetContainers,
                        out result);

                case CampusCharacterActionKind.DropItemToGround:
                    return CampusInventoryActionExecutor.TryDropItemToGround(
                        actor,
                        action.Item,
                        action.SourceContainer,
                        action.GroundDropContext,
                        out result);

                case CampusCharacterActionKind.OpenInventoryView:
                    bool opened = CampusInventoryActionExecutor.TryOpenInventoryView(
                        actor,
                        action.TargetContainer,
                        action.GroundDropContext,
                        action.IncludeBackpack,
                        out string message);
                    result = new StorageTransferResult(opened, false, false, message, string.Empty);
                    return opened;

                case CampusCharacterActionKind.DomainAction:
                    return CampusCharacterActionRegistry.TryExecute(
                        new CampusCharacterActionContext(
                            actor,
                            action.ActionId,
                            action.Payload,
                            action.Target),
                        out result);

                default:
                    return false;
            }
        }

        public static bool TryPressInteract(CampusCharacterRuntime actor, CampusInteractionTarget target)
        {
            if (!target.IsValid || !target.CanInteract)
            {
                return false;
            }

            return TryPressInteract(actor, target.InteractableComponent);
        }

        public static bool TryPressInteract(CampusCharacterRuntime actor, Object target)
        {
            if (actor == null ||
                target == null ||
                !TryFindInteractable(target, out ICampusInteractable interactable))
            {
                return false;
            }

            GameObject actorObject = actor.gameObject;
            if (interactable is ICampusInteractionAvailability availability &&
                !availability.CanInteract(actorObject, out _))
            {
                return false;
            }

            interactable.Interact(actorObject);
            return true;
        }

        public static bool TryPickUpDroppedItem(
            CampusCharacterRuntime actor,
            CampusDroppedStorageItem droppedItem,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            return actor != null &&
                   droppedItem != null &&
                   droppedItem.TryPickup(actor, out result);
        }

        private static bool TryPressInteractionAction(
            CampusCharacterRuntime actor,
            Object target,
            string actionId,
            string payload,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (actor == null || target == null || string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            if (!TryResolveInteractionTarget(target, out CampusInteractionAnchor anchor, out Component directTarget))
            {
                return false;
            }

            bool succeeded = CampusGameplayActionService.TryExecuteShared(new CampusGameplayActionRequest(
                actor.gameObject,
                actionId,
                payload,
                anchor,
                directTarget,
                "character_action"));
            result = new StorageTransferResult(succeeded, false, false, string.Empty, string.Empty);
            return succeeded;
        }

        private static bool TryResolveInteractionTarget(
            Object target,
            out CampusInteractionAnchor anchor,
            out Component directTarget)
        {
            anchor = null;
            directTarget = null;
            if (target is CampusInteractionAnchor directAnchor)
            {
                anchor = directAnchor;
                directTarget = directAnchor.InteractionTarget as Component;
                return directTarget != null || anchor != null;
            }

            if (target is Component component)
            {
                directTarget = component;
                return true;
            }

            if (target is GameObject gameObject)
            {
                anchor = gameObject.GetComponent<CampusInteractionAnchor>();
                directTarget = anchor != null
                    ? anchor.InteractionTarget as Component
                    : gameObject.GetComponent<Component>();
                return directTarget != null || anchor != null;
            }

            return false;
        }

        private static bool TryFindInteractable(Object target, out ICampusInteractable interactable)
        {
            interactable = null;
            if (target is ICampusInteractable directInteractable)
            {
                interactable = directInteractable;
                return true;
            }

            if (target is GameObject gameObject)
            {
                return TryFindInteractable(gameObject, out interactable);
            }

            if (target is Component component)
            {
                return TryFindInteractable(component.gameObject, out interactable);
            }

            return false;
        }

        private static bool TryFindInteractable(GameObject target, out ICampusInteractable interactable)
        {
            interactable = null;
            if (target == null)
            {
                return false;
            }

            CampusInteractionAnchor anchor = target.GetComponent<CampusInteractionAnchor>();
            if (anchor != null)
            {
                interactable = anchor;
                return true;
            }

            if (TryFindInBehaviours(target.GetComponents<MonoBehaviour>(), out interactable))
            {
                return true;
            }

            CampusInteractionAnchor childAnchor = target.GetComponentInChildren<CampusInteractionAnchor>(true);
            if (childAnchor != null)
            {
                interactable = childAnchor;
                return true;
            }

            if (TryFindInBehaviours(target.GetComponentsInChildren<MonoBehaviour>(true), out interactable))
            {
                return true;
            }

            CampusInteractionAnchor parentAnchor = target.GetComponentInParent<CampusInteractionAnchor>(true);
            if (parentAnchor != null)
            {
                interactable = parentAnchor;
                return true;
            }

            return TryFindInBehaviours(target.GetComponentsInParent<MonoBehaviour>(true), out interactable);
        }

        private static bool TryFindInBehaviours(MonoBehaviour[] behaviours, out ICampusInteractable interactable)
        {
            interactable = null;
            if (behaviours == null)
            {
                return false;
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICampusInteractable candidate)
                {
                    interactable = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
