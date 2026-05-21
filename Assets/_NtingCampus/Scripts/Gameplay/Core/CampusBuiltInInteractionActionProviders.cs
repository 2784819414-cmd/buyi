using NtingCampus.Gameplay.Canteen;

namespace NtingCampus.Gameplay.Core
{
    internal static class CampusBuiltInInteractionActionProviders
    {
        public static void Install()
        {
            CampusInteractionActionRegistry.Register(CampusCanteenInteractionProvider.Instance);
        }
    }
}
