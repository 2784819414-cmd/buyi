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
        public const string Log = "log";
        public const string OpenStorage = "open_storage";
        public const string ToggleDoor = "toggle_door";
        public const string InteractTarget = "interact_target";

        public static bool Equals(string actionId, string expected)
        {
            return string.Equals(Normalize(actionId), expected, System.StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string actionId)
        {
            return string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
        }
    }
}
