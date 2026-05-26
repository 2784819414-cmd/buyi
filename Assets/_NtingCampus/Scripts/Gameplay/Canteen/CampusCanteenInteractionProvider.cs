using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenInteractionProvider :
        ICampusInteractionActionProvider,
        ICampusInteractionPromptOverrideProvider
    {
        public static readonly CampusCanteenInteractionProvider Instance =
            new CampusCanteenInteractionProvider();

        public string ProviderId => "campus_canteen_interactions";

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (!CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.ServiceWindowUse))
            {
                return false;
            }

            CampusCharacterRuntime actor = CampusCharacterActionUtility.ResolveActorRuntime(context.Actor);
            CampusPlacedObject window = context.SourceObject;
            if (actor == null || window == null)
            {
                return false;
            }

            if (!IsWindowActive(window))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowInactiveLog);
                WriteInteractionLog(message);
                return false;
            }

            if (actor.Data != null && actor.Data.IsPlayerControlled)
            {
                CampusCanteenOrderPanel.Open(actor, window);
                return true;
            }

            CampusCanteenMenuItem menuItem = CampusCanteenMenuCatalog.ResolveDefault();
            if (!CampusCanteenOrderService.TryPlaceOrder(actor, window, menuItem, out message))
            {
                WriteInteractionLog(message);
                return false;
            }

            return true;
        }

        public bool TryResolvePrompt(CampusInteractionActionContext context, out string prompt)
        {
            prompt = string.Empty;
            if (!CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.ServiceWindowUse))
            {
                return false;
            }

            if (!IsWindowActive(context.SourceObject))
            {
                prompt = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowInactivePrompt);
                return true;
            }

            prompt = CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderMealPrompt);
            return true;
        }

        private static bool IsWindowActive(CampusPlacedObject window)
        {
            return CampusCanteenServiceWindowAvailability.IsAvailable(window);
        }

        private static void WriteInteractionLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
            }
        }
    }
}
