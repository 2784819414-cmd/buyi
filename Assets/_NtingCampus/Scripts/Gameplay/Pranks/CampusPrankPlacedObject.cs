using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CampusPlacedObject))]
    public sealed class CampusPrankPlacedObject : MonoBehaviour, ICampusInteractionActionHandler, ICampusInteractionAvailability
    {
        [SerializeField] private string displayName = "缺德点";
        [SerializeField] private string prankPayload = CampusPrankPayloadIds.PassNote;
        [SerializeField] private CampusRoomType requiredRoomType = CampusRoomType.Unknown;
        [SerializeField] private string unsupportedReason = "该缺德点还没接入正式玩法。";

        private CampusPrankService prankService;

        public string Payload => prankPayload;

        public void Configure(CampusPrankDefinition definition)
        {
            displayName = definition.DisplayName;
            prankPayload = definition.Payload;
            requiredRoomType = definition.RequiredRoomType;
            unsupportedReason = definition.UnsupportedReason;
        }

        public bool CanInteract(GameObject actor, out string unavailableReason)
        {
            prankService = prankService != null ? prankService : ResolvePrankService();
            if (prankService == null)
            {
                unavailableReason = "Formal prank service is missing.";
                return false;
            }

            return prankService.CanExecutePayload(prankPayload, actor, out unavailableReason);
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            if (!CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.PrankExecute))
            {
                return false;
            }

            if (!string.Equals(payload, prankPayload, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            prankService = prankService != null ? prankService : ResolvePrankService();
            return prankService != null && prankService.TryExecutePayload(prankPayload, actor);
        }

        private CampusPrankService ResolvePrankService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.PrankService != null)
            {
                return bootstrap.PrankService;
            }

            return FindFirstObjectByType<CampusPrankService>(FindObjectsInactive.Include);
        }
    }
}
