using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CampusPlacedObject))]
    public sealed class CampusPrankPlacedObject : MonoBehaviour, ICampusInteractionAvailability
    {
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CampusLocalizedText localizedDisplayName = CampusPrankTextCatalog.Localized(CampusPrankTextId.DefaultPrankSpot);
        [SerializeField] private string prankPayload = CampusPrankPayloadIds.PassNote;
        [SerializeField] private CampusRoomType requiredRoomType = CampusRoomType.Unknown;
        [SerializeField] private string unsupportedReason = string.Empty;
        [SerializeField] private CampusLocalizedText localizedUnsupportedReason = CampusPrankTextCatalog.Localized(CampusPrankTextId.DefaultUnsupportedPrankSpot);

        private CampusPrankService prankService;

        public string Payload => prankPayload;

        public void Configure(CampusPrankDefinition definition)
        {
            displayName = definition.DisplayName;
            localizedDisplayName = definition.LocalizedDisplayName;
            prankPayload = definition.Payload;
            requiredRoomType = definition.RequiredRoomType;
            unsupportedReason = definition.UnsupportedReason;
            localizedUnsupportedReason = definition.LocalizedUnsupportedReason;
        }

        public bool CanInteract(GameObject actor, out string unavailableReason)
        {
            prankService = prankService != null ? prankService : ResolvePrankService();
            if (prankService == null)
            {
                unavailableReason = CampusPrankTextCatalog.Get(CampusPrankTextId.MissingPrankService);
                return false;
            }

            return prankService.CanExecutePayload(prankPayload, actor, out unavailableReason);
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
