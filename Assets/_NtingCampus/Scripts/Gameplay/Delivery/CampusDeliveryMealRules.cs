using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;

namespace NtingCampus.Gameplay.Delivery
{
    internal static class CampusDeliveryMealRules
    {
        public static bool CanPlaceDeliveryOrder(CampusCharacterRuntime actor)
        {
            CampusDeliveryService service = ResolveService();
            return service != null && service.CanPlaceOrder(actor);
        }

        public static bool HasActiveDeliveryOrder(CampusCharacterRuntime actor)
        {
            CampusDeliveryService service = ResolveService();
            return service != null && service.HasActiveOrder(actor);
        }

        public static bool HasDeliveredDeliveryOrder(CampusCharacterRuntime actor)
        {
            CampusDeliveryService service = ResolveService();
            return service != null && service.HasDeliveredOrder(actor);
        }

        public static bool ShouldSkipCanteen(CampusCharacterRuntime actor)
        {
            CampusDeliveryService service = ResolveService();
            return service != null && service.ShouldSkipCanteen(actor);
        }

        private static CampusDeliveryService ResolveService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null ? bootstrap.DeliveryService : null;
        }
    }
}
