using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    internal sealed class CampusPrankInteractionActionProvider : ICampusInteractionActionProvider
    {
        public static CampusPrankInteractionActionProvider Instance { get; } =
            new CampusPrankInteractionActionProvider();

        public string ProviderId => "campus.prank.execute";

        private CampusPrankInteractionActionProvider()
        {
        }

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (!CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.PrankExecute))
            {
                return false;
            }

            CampusPrankService prankService = ResolvePrankService();
            if (prankService == null)
            {
                message = CampusPrankTextCatalog.Get(CampusPrankTextId.MissingPrankService);
                return true;
            }

            prankService.TryExecutePayload(ResolvePayload(context), context.Actor);
            return true;
        }

        private static CampusPrankService ResolvePrankService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.PrankService != null)
            {
                return bootstrap.PrankService;
            }

            return Object.FindFirstObjectByType<CampusPrankService>(FindObjectsInactive.Include);
        }

        private static string ResolvePayload(CampusInteractionActionContext context)
        {
            if (!string.IsNullOrWhiteSpace(context.Payload))
            {
                return context.Payload.Trim();
            }

            if (TryReadPayload(context.DirectTarget, out string directPayload))
            {
                return directPayload;
            }

            if (context.Anchor != null &&
                TryReadPayload(context.Anchor.InteractionTarget, out string anchorPayload))
            {
                return anchorPayload;
            }

            return string.Empty;
        }

        private static bool TryReadPayload(object source, out string payload)
        {
            payload = string.Empty;
            switch (source)
            {
                case CampusPrankPlacedObject placedObject:
                    payload = placedObject.Payload;
                    break;
                case CampusPrankInteractionSpot interactionSpot:
                    payload = interactionSpot.PrankPayload;
                    break;
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            payload = payload.Trim();
            return true;
        }
    }
}
