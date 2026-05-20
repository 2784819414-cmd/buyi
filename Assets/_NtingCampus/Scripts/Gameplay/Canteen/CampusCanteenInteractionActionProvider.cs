using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenInteractionActionProvider : ICampusInteractionActionProvider
    {
        public static CampusCanteenInteractionActionProvider Instance { get; } =
            new CampusCanteenInteractionActionProvider();

        public string ProviderId => "campus.canteen.serving_window";

        private CampusCanteenInteractionActionProvider()
        {
        }

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (context.FacilityType != CampusFacilityType.CanteenServingWindow ||
                !IsWindowUseAction(context.ActionId))
            {
                return false;
            }

            CampusCanteenService canteen = CampusCanteenService.Resolve();
            if (canteen == null || !canteen.InteractWithServingWindow(context.Actor, context.SourceObject, out message))
            {
                return true;
            }

            message = string.Empty;
            return true;
        }

        private static bool IsWindowUseAction(string actionId)
        {
            return CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.InteractTarget) ||
                   CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.Log);
        }
    }
}
