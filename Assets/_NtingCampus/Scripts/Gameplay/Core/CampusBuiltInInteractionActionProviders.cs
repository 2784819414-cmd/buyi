using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Inventory;

namespace NtingCampus.Gameplay.Core
{
    internal static class CampusBuiltInInteractionActionProviders
    {
        public static void Install()
        {
            CampusInteractionActionRegistry.Register(CampusCanteenInteractionProvider.Instance);
            CampusInteractionActionRegistry.Register(CampusProtectedTransferClearanceInteractionProvider.Instance);
            CampusInteractionActionRegistry.Register(CampusDomainInteractionActionProvider.Instance);
        }
    }

    internal sealed class CampusDomainInteractionActionProvider : ICampusInteractionActionProvider
    {
        public static readonly CampusDomainInteractionActionProvider Instance =
            new CampusDomainInteractionActionProvider();

        public string ProviderId => "campus_domain_interaction_actions";

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(context.ActionId) ||
                !CampusCharacterActionUtility.TryResolveActorRuntime(context.Actor, out CampusCharacterRuntime actor))
            {
                return false;
            }

            if (!CampusCharacterActionRegistry.TryExecute(
                    new CampusCharacterActionContext(
                        actor,
                        context.ActionId,
                        context.Payload,
                        context.SourceObject != null ? context.SourceObject : context.Anchor),
                    out StorageTransferResult result))
            {
                return false;
            }

            message = result.Message;
            return result.Succeeded;
        }
    }
}
