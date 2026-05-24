using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcInteractable : MonoBehaviour, ICampusInteractable, ICampusInteractionActionHandler, ICampusInteractionAvailability, ICampusInteractionPromptProvider
    {
        private ICampusNpcTalkSource talkSource;

        public void Bind(ICampusNpcTalkSource targetTalkSource)
        {
            talkSource = targetTalkSource;
        }

        public void Interact(GameObject actor)
        {
            TryTalk(actor);
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            string normalizedActionId = CampusInteractionActionIds.Normalize(actionId);
            if (!string.IsNullOrEmpty(normalizedActionId) &&
                !CampusInteractionActionIds.Equals(normalizedActionId, CampusInteractionActionIds.NpcTalk))
            {
                return false;
            }

            return TryTalk(actor);
        }

        public bool CanInteract(GameObject actor, out string unavailableReason)
        {
            unavailableReason = string.Empty;
            return talkSource != null && talkSource.IsTalkAvailable;
        }

        public bool TryGetInteractionPrompt(GameObject actor, out CampusInteractionPromptData prompt)
        {
            string promptText = talkSource != null
                ? talkSource.ResolveInteractionPrompt(actor)
                : CampusCharacterTextCatalog.FormatTalkPrompt(
                    CampusLanguageState.CurrentLanguage,
                    CampusInteractionTextCatalog.Get(CampusInteractionTextId.UnknownActor));
            prompt = CampusInteractionPromptData.Create(promptText);
            prompt.Anchor = transform;
            prompt.WorldOffset = new Vector3(0f, 0.84f, 0f);
            prompt.Priority = 55;
            prompt.IsAvailable = talkSource != null && talkSource.IsTalkAvailable;
            return true;
        }

        private bool TryTalk(GameObject actor)
        {
            return talkSource != null && talkSource.TryTalk(actor, out _);
        }
    }
}

