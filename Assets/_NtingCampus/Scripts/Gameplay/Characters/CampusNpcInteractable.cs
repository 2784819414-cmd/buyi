using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcInteractable : MonoBehaviour, ICampusInteractable, ICampusInteractionActionHandler, ICampusInteractionAvailability
    {
        [SerializeField] private CampusNpcAgent agent;

        public void Bind(CampusNpcAgent targetAgent)
        {
            agent = targetAgent;
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
            return agent != null && agent.enabled;
        }

        private bool TryTalk(GameObject actor)
        {
            return agent != null && agent.TryTalk(actor, out _);
        }
    }
}
