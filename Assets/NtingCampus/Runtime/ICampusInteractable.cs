using UnityEngine;

namespace NtingCampusMapEditor
{
    public interface ICampusInteractable
    {
        void Interact(GameObject actor);
    }

    public interface ICampusInteractionActionHandler
    {
        bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor);
    }

    public static class CampusInteractionActionIds
    {
        public const string Log = "campus.debug.log";
        public const string OpenStorage = "campus.storage.open";
        public const string ToggleDoor = "campus.door.toggle";
        public const string InteractTarget = "campus.interact.target";
        public const string NpcTalk = "campus.npc.talk";
        public const string PickupStorageItem = "campus.storage.pickup";
        public const string ServiceWindowUse = "campus.facility.service_window.use";

        public static bool Equals(string actionId, string expected)
        {
            return string.Equals(Normalize(actionId), expected, System.StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return string.Empty;
            }

            return actionId.Trim();
        }
    }
}
