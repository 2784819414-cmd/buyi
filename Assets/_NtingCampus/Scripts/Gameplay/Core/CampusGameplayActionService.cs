using NtingCampus.Gameplay.Pranks;
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
        [SerializeField] private CampusPrankService prankService;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            prankService = bootstrap != null ? bootstrap.PrankService : null;
        }

        public bool TryExecute(CampusGameplayActionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ActionId))
            {
                return TryExecuteTargetFallback(request);
            }

            if (CampusInteractionActionIds.Equals(request.ActionId, CampusInteractionActionIds.PrankExecute) &&
                TryExecutePrank(request))
            {
                return true;
            }

            return TryExecuteLocalHandler(request);
        }

        public static bool TryExecuteShared(CampusGameplayActionRequest request)
        {
            CampusGameplayActionService service = ResolveService();
            if (service != null && service.TryExecute(request))
            {
                return true;
            }

            return TryExecuteStaticFallback(request);
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

        private bool TryExecutePrank(CampusGameplayActionRequest request)
        {
            if (prankService == null)
            {
                prankService = bootstrap != null ? bootstrap.PrankService : null;
            }

            if (prankService == null)
            {
                prankService = FindFirstObjectByType<CampusPrankService>(FindObjectsInactive.Include);
            }

            return prankService != null && prankService.TryExecutePayload(request.Payload, request.Actor);
        }

        private static bool TryExecuteStaticFallback(CampusGameplayActionRequest request)
        {
            if (CampusInteractionActionIds.Equals(request.ActionId, CampusInteractionActionIds.PrankExecute))
            {
                CampusPrankService prankService = CampusGameBootstrap.Instance != null
                    ? CampusGameBootstrap.Instance.PrankService
                    : Object.FindFirstObjectByType<CampusPrankService>(FindObjectsInactive.Include);
                if (prankService != null && prankService.TryExecutePayload(request.Payload, request.Actor))
                {
                    return true;
                }
            }

            return TryExecuteLocalHandler(request) || TryExecuteTargetFallback(request);
        }

        private static bool TryExecuteLocalHandler(CampusGameplayActionRequest request)
        {
            if (request.DirectTarget is ICampusInteractionActionHandler directHandler &&
                directHandler.TryHandleInteractionAction(request.Anchor, request.ActionId, request.Payload, request.Actor))
            {
                return true;
            }

            if (request.Anchor == null)
            {
                return false;
            }

            ICampusInteractionActionHandler[] handlers =
                request.Anchor.GetComponentsInParent<ICampusInteractionActionHandler>(true);
            for (int i = 0; i < handlers.Length; i++)
            {
                ICampusInteractionActionHandler handler = handlers[i];
                if (handler != null &&
                    handler.TryHandleInteractionAction(request.Anchor, request.ActionId, request.Payload, request.Actor))
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

        private static CampusGameplayActionService ResolveService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.ActionService != null)
            {
                return bootstrap.ActionService;
            }

            return Object.FindFirstObjectByType<CampusGameplayActionService>(FindObjectsInactive.Include);
        }
    }
}
