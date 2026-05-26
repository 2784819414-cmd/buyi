using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Services;
using UnityEngine;

namespace NtingCampus.Gameplay.Retail
{
    internal sealed class CampusRetailInteractionProvider :
        ICampusInteractionActionProvider,
        ICampusInteractionPromptOverrideProvider
    {
        public static readonly CampusRetailInteractionProvider Instance =
            new CampusRetailInteractionProvider();

        public string ProviderId => "campus_retail_interactions";

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (!CampusCharacterActionUtility.IdEquals(context.ActionId, CampusRetailActionIds.Checkout))
            {
                return false;
            }

            CampusCharacterRuntime actor = CampusCharacterActionUtility.ResolveActorRuntime(context.Actor);
            if (actor == null)
            {
                return false;
            }

            if (!CampusServiceStationRuntimeAvailability.TryRequireActionSourceAvailable(
                    context.ActionId,
                    context.SourceObject,
                    out message))
            {
                WriteInteractionLog(message);
                return false;
            }

            bool succeeded = CampusRetailService.TryCheckoutActor(actor, context.SourceObject, out message);
            if (!succeeded)
            {
                WriteInteractionLog(message);
            }

            return succeeded;
        }

        public bool TryResolvePrompt(CampusInteractionActionContext context, out string prompt)
        {
            prompt = string.Empty;
            if (!CampusCharacterActionUtility.IdEquals(context.ActionId, CampusRetailActionIds.Checkout))
            {
                return false;
            }

            if (!CampusServiceStationRuntimeAvailability.TryRequireActionSourceAvailable(
                    context.ActionId,
                    context.SourceObject,
                    out prompt))
            {
                return true;
            }

            prompt = CampusRetailTextCatalog.Get(CampusRetailTextId.CheckoutPrompt);
            return true;
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
