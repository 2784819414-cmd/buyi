using System;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Core
{
    public static class CampusInteractionActionExecutor
    {
        public static bool TryExecute(
            CampusGameplayActionRequest request,
            Func<CampusGameplayActionRequest, bool> executeGlobalAction)
        {
            if (string.IsNullOrWhiteSpace(request.ActionId))
            {
                return TryExecuteTargetFallback(request);
            }

            if (TryExecuteTargetActionHandler(request, out bool targetOwnsAction))
            {
                return true;
            }

            if (targetOwnsAction)
            {
                return false;
            }

            if (TryExecuteParentActionHandlers(request, out _))
            {
                return true;
            }

            if (CampusInteractionActionRegistry.TryHandle(request, out _))
            {
                return true;
            }

            return executeGlobalAction != null && executeGlobalAction(request);
        }

        private static bool TryExecuteTargetActionHandler(CampusGameplayActionRequest request, out bool targetOwnsAction)
        {
            targetOwnsAction = request.DirectTarget is ICampusInteractionActionHandler;
            if (!targetOwnsAction)
            {
                return false;
            }

            ICampusInteractionActionHandler handler = (ICampusInteractionActionHandler)request.DirectTarget;
            return handler.TryHandleInteractionAction(request.Anchor, request.ActionId, request.Payload, request.Actor);
        }

        private static bool TryExecuteParentActionHandlers(CampusGameplayActionRequest request, out bool parentOwnsAction)
        {
            parentOwnsAction = false;
            if (request.Anchor == null)
            {
                return false;
            }

            ICampusInteractionActionHandler[] handlers =
                request.Anchor.GetComponentsInParent<ICampusInteractionActionHandler>(true);
            for (int i = 0; i < handlers.Length; i++)
            {
                ICampusInteractionActionHandler handler = handlers[i];
                if (handler == null || ReferenceEquals(handler, request.DirectTarget))
                {
                    continue;
                }

                parentOwnsAction = true;
                if (handler.TryHandleInteractionAction(request.Anchor, request.ActionId, request.Payload, request.Actor))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryExecuteTargetFallback(CampusGameplayActionRequest request)
        {
            if (request.DirectTarget is ICampusInteractable interactable &&
                !ReferenceEquals(interactable, request.Anchor))
            {
                interactable.Interact(request.Actor);
                return true;
            }

            return false;
        }
    }
}
