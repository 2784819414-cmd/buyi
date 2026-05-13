using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public class CampusInteractionPromptSource : MonoBehaviour, ICampusInteractionPromptProvider, ICampusInteractionAvailability
    {
        public string PromptText = "\u4ea4\u4e92";
        public string KeyOverride;
        public Sprite Icon;
        public Transform Anchor;
        public Vector3 WorldOffset = new Vector3(0f, 0.85f, 0f);
        public Color AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
        public int Priority;
        public bool IsAvailable = true;
        public string UnavailableText;
        public bool HideWhenUnavailable;

        public virtual bool TryGetInteractionPrompt(GameObject actor, out CampusInteractionPromptData prompt)
        {
            prompt = CampusInteractionPromptData.Create(ResolvePromptText(actor));
            prompt.KeyText = KeyOverride;
            prompt.Icon = Icon;
            prompt.Anchor = Anchor != null ? Anchor : transform;
            prompt.WorldOffset = WorldOffset;
            prompt.AccentColor = AccentColor;
            prompt.Priority = Priority;
            prompt.IsAvailable = IsAvailable;
            prompt.UnavailableText = UnavailableText;
            return IsAvailable || !HideWhenUnavailable;
        }

        public virtual bool CanInteract(GameObject actor, out string unavailableReason)
        {
            unavailableReason = UnavailableText;
            return IsAvailable;
        }

        protected virtual string ResolvePromptText(GameObject actor)
        {
            if (!string.IsNullOrWhiteSpace(PromptText))
            {
                return PromptText;
            }

            CampusPlacedObject placedObject = GetComponentInParent<CampusPlacedObject>();
            if (placedObject != null && !string.IsNullOrWhiteSpace(placedObject.ObjectId))
            {
                return "\u4ea4\u4e92 " + placedObject.DisplayName;
            }

            return "\u4ea4\u4e92 " + CampusObjectNames.GetDisplayName(gameObject.name);
        }
    }
}
