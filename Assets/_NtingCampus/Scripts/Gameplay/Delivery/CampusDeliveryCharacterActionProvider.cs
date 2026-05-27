using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;

namespace NtingCampus.Gameplay.Delivery
{
    internal static class CampusDeliveryActionIds
    {
        public const string PlaceOrder = "campus.delivery.place_order";
        public const string WaitForOrder = "campus.delivery.wait_for_order";
    }

    internal sealed class CampusDeliveryCharacterActionProvider : ICampusCharacterActionProvider
    {
        public static readonly CampusDeliveryCharacterActionProvider Instance =
            new CampusDeliveryCharacterActionProvider();

        public string ProviderId => "campus_delivery_character_actions";

        public bool TryExecute(CampusCharacterActionContext context, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (context.Actor == null)
            {
                return false;
            }

            CampusDeliveryService service = ResolveService();
            if (service == null)
            {
                return false;
            }

            if (CampusCharacterActionUtility.IdEquals(context.ActionId, CampusDeliveryActionIds.PlaceOrder))
            {
                bool ordered = service.TryPlaceOrder(context.Actor);
                result = ordered
                    ? CampusCharacterActionUtility.Success()
                    : StorageTransferResult.Fail(string.Empty);
                return ordered;
            }

            if (CampusCharacterActionUtility.IdEquals(context.ActionId, CampusDeliveryActionIds.WaitForOrder))
            {
                result = CampusCharacterActionUtility.Success();
                return true;
            }

            return false;
        }

        private static CampusDeliveryService ResolveService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null ? bootstrap.DeliveryService : null;
        }
    }
}
