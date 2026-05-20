using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public class CampusInteractionPromptSource : MonoBehaviour, ICampusInteractionPromptProvider, ICampusInteractionAvailability
    {
        public string PromptText = string.Empty;
        public CampusLocalizedText LocalizedPromptText;
        public string KeyOverride;
        public Sprite Icon;
        public Transform Anchor;
        public Vector3 WorldOffset = new Vector3(0f, 0.85f, 0f);
        public Color AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
        public int Priority;
        public bool IsAvailable = true;
        public string UnavailableText;
        public CampusLocalizedText LocalizedUnavailableText;
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
            prompt.UnavailableText = ResolveUnavailableText();
            return IsAvailable || !HideWhenUnavailable;
        }

        public virtual bool CanInteract(GameObject actor, out string unavailableReason)
        {
            unavailableReason = ResolveUnavailableText();
            return IsAvailable;
        }

        protected virtual string ResolvePromptText(GameObject actor)
        {
            string configuredPrompt = ResolveConfiguredPromptText();
            if (!string.IsNullOrWhiteSpace(configuredPrompt))
            {
                return configuredPrompt;
            }

            CampusPlacedObject placedObject = GetComponentInParent<CampusPlacedObject>();
            if (placedObject != null && !string.IsNullOrWhiteSpace(placedObject.ObjectId))
            {
                return CampusInteractionTextCatalog.Format(CampusInteractionTextId.InteractWith, placedObject.DisplayName);
            }

            return CampusInteractionTextCatalog.Format(
                CampusInteractionTextId.InteractWith,
                CampusObjectNames.GetDisplayName(gameObject.name));
        }

        protected string ResolveConfiguredPromptText()
        {
            if (LocalizedPromptText.HasAnyText)
            {
                return LocalizedPromptText.Current(PromptText);
            }

            return IsLegacyDefaultPrompt(PromptText) ? string.Empty : PromptText;
        }

        protected string ResolveUnavailableText()
        {
            return LocalizedUnavailableText.HasAnyText
                ? LocalizedUnavailableText.Current(UnavailableText)
                : UnavailableText;
        }

        protected static bool IsLegacyDefaultPrompt(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Trim() == "\u4ea4\u4e92";
        }
    }
}
