using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Retail;

namespace NtingCampus.Gameplay.Core
{
    internal static class CampusBuiltInInteractionActionProviders
    {
        public static void Install()
        {
            CampusInteractionActionRegistry.Register(CampusCanteenInteractionProvider.Instance);
            CampusInteractionActionRegistry.Register(CampusRetailInteractionProvider.Instance);
        }
    }
}
