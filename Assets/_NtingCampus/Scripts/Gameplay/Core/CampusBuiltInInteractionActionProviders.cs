using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Pranks;

namespace NtingCampus.Gameplay.Core
{
    internal static class CampusBuiltInInteractionActionProviders
    {
        public static void Install()
        {
            CampusInteractionActionRegistry.Register(CampusCanteenInteractionActionProvider.Instance);
            CampusInteractionActionRegistry.Register(CampusPrankInteractionActionProvider.Instance);
            CampusInteractionActionRegistry.Register(CampusStoreInteractionActionProvider.Instance);
        }
    }
}
