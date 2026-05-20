using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Economy
{
    internal sealed class CampusStoreInteractionActionProvider :
        ICampusInteractionActionProvider,
        ICampusInteractionPromptOverrideProvider
    {
        public static CampusStoreInteractionActionProvider Instance { get; } =
            new CampusStoreInteractionActionProvider();

        public string ProviderId => "campus.store.interaction";

        private CampusStoreInteractionActionProvider()
        {
        }

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (context.FacilityType != CampusFacilityType.StoreCheckout ||
                !IsCheckoutAction(context.ActionId))
            {
                return false;
            }

            CampusCommerceService commerce = CampusCommerceService.Resolve();
            if (commerce == null || !commerce.TryCheckout(context.Actor, context.SourceObject, out message))
            {
                return true;
            }

            message = string.Empty;
            return true;
        }

        public bool TryResolvePrompt(CampusInteractionActionContext context, out string prompt)
        {
            prompt = string.Empty;
            if (context.FacilityType == CampusFacilityType.StoreShelf)
            {
                prompt = CampusInteractionTextCatalog.Get(CampusInteractionTextId.OpenShelf);
                return true;
            }

            if (context.FacilityType == CampusFacilityType.StoreCheckout)
            {
                prompt = CampusInteractionTextCatalog.Get(CampusInteractionTextId.Checkout);
                return true;
            }

            return false;
        }

        private static bool IsCheckoutAction(string actionId)
        {
            return CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.InteractTarget) ||
                   CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.Log);
        }
    }
}
