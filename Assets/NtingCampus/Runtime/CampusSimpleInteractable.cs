using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public sealed class CampusSimpleInteractable : CampusInteractionPromptSource, ICampusInteractable, ICampusInteractionActionHandler
    {
        public bool LogInteraction = true;
        public string InteractionLogMessage;
        public CampusLocalizedText LocalizedInteractionLogMessage;

        [Header("Unified Action")]
        public string DefaultActionId;

        public void Interact(GameObject actor)
        {
            string actionId = ResolveDefaultActionId(null);
            if (!TryHandleInteractionAction(null, actionId, null, actor))
            {
                LogDefaultInteraction(actor);
            }
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            string resolvedActionId = CampusInteractionActionIds.Normalize(actionId);
            if (string.IsNullOrEmpty(resolvedActionId))
            {
                resolvedActionId = ResolveDefaultActionId(anchor);
            }

            if (CampusInteractionActionRegistry.TryHandle(
                    this,
                    anchor,
                    resolvedActionId,
                    payload,
                    actor,
                    out string handlerMessage))
            {
                WriteInteractionLog(handlerMessage);
                return true;
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.OpenStorage))
            {
                return CampusObjectStorageInteraction.TryOpenStorageView(this, actor, payload);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.InteractTarget))
            {
                return TryInteractTarget(anchor, actor);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.Log))
            {
                LogDefaultInteraction(actor);
                return true;
            }

            return false;
        }

        private string ResolveDefaultActionId(CampusInteractionAnchor anchor)
        {
            string configuredActionId = ResolveConfiguredDefaultActionId();
            if (!string.IsNullOrEmpty(configuredActionId))
            {
                return configuredActionId;
            }

            string placedObjectActionId = ResolvePlacedObjectDefaultActionId();
            if (!string.IsNullOrEmpty(placedObjectActionId))
            {
                return placedObjectActionId;
            }

            if (anchor != null && anchor.InteractionTarget is ICampusInteractable target && !ReferenceEquals(target, this))
            {
                return CampusInteractionActionIds.InteractTarget;
            }

            return CampusInteractionActionIds.Log;
        }

        private string ResolveConfiguredDefaultActionId()
        {
            string configuredActionId = CampusInteractionActionIds.Normalize(DefaultActionId);
            return !string.IsNullOrEmpty(configuredActionId) &&
                   !CampusInteractionActionIds.Equals(configuredActionId, CampusInteractionActionIds.Log)
                ? configuredActionId
                : string.Empty;
        }

        private string ResolvePlacedObjectDefaultActionId()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            string placedActionId = placedObject != null
                ? CampusPlacedObjectInteractionState.ResolveDefaultActionId(placedObject)
                : string.Empty;
            return CampusInteractionActionIds.Normalize(placedActionId);
        }

        protected override string ResolvePromptText(GameObject actor)
        {
            if (CampusInteractionActionRegistry.TryResolvePrompt(
                    this,
                    null,
                    ResolveDefaultActionId(null),
                    string.Empty,
                    actor,
                    out string providerPrompt))
            {
                return providerPrompt;
            }

            return base.ResolvePromptText(actor);
        }

        private bool TryInteractTarget(CampusInteractionAnchor anchor, GameObject actor)
        {
            if (anchor == null ||
                !(anchor.InteractionTarget is Object targetObject) ||
                ReferenceEquals(anchor.InteractionTarget, this))
            {
                return false;
            }

            return CampusCharacterActionExecutor.TryPressInteract(ResolveActorRuntime(actor), targetObject);
        }

        private static CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            return CampusCharacterActionUtility.ResolveActorRuntime(actor);
        }

        private static void WriteInteractionLog(string message)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog(message);
            }
        }

        private void LogDefaultInteraction(GameObject actor)
        {
            if (!LogInteraction)
            {
                return;
            }

            string actorName = actor != null ? actor.name : CampusInteractionTextCatalog.Get(CampusInteractionTextId.UnknownActor);
            string message = LocalizedInteractionLogMessage.HasAnyText
                ? LocalizedInteractionLogMessage.Current(InteractionLogMessage)
                : string.IsNullOrWhiteSpace(InteractionLogMessage)
                ? CampusInteractionTextCatalog.Format(
                    CampusInteractionTextId.InteractedWithLog,
                    actorName,
                    ResolveObjectDisplayName())
                : InteractionLogMessage;

            Debug.Log(message, this);
        }

        private string ResolveObjectDisplayName()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (placedObject != null)
            {
                return placedObject.DisplayName;
            }

            return CampusObjectNames.GetDisplayName(gameObject.name);
        }
    }
}
