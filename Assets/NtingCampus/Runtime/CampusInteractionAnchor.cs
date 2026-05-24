using System.Reflection;
using NtingCampus.Gameplay.Core;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CampusInteractionAnchor : MonoBehaviour, ICampusInteractable, ICampusInteractionPromptProvider, ICampusInteractionAvailability
    {
        public MonoBehaviour InteractionTarget;
        public string ActionId;
        public string Payload;
        public Transform PromptAnchor;
        public string PromptText = string.Empty;
        public CampusLocalizedText LocalizedPromptText;
        public string KeyOverride;
        public Sprite Icon;
        public Color AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
        public int Priority = 100;
        public bool IsAvailable = true;
        public string UnavailableText;
        public CampusLocalizedText LocalizedUnavailableText;
        public bool HideWhenUnavailable;
        public bool UseTargetDoorStatePrompt;
        public bool LogInteraction = true;
        public string InteractionLogMessage;
        public CampusLocalizedText LocalizedInteractionLogMessage;

        private void Reset()
        {
            ConfigureCollider();
        }

        private void OnValidate()
        {
            ConfigureCollider();
        }

        public bool TryGetInteractionPrompt(GameObject actor, out CampusInteractionPromptData prompt)
        {
            if (InteractionTarget is ICampusInteractionPromptProvider targetPromptProvider &&
                !ReferenceEquals(targetPromptProvider, this) &&
                targetPromptProvider.TryGetInteractionPrompt(actor, out prompt))
            {
                if (prompt.Anchor == null)
                {
                    prompt.Anchor = PromptAnchor != null ? PromptAnchor : transform;
                }

                if (string.IsNullOrWhiteSpace(prompt.KeyText))
                {
                    prompt.KeyText = KeyOverride;
                }

                if (prompt.Icon == null)
                {
                    prompt.Icon = Icon;
                }

                return true;
            }

            prompt = CampusInteractionPromptData.Create(ResolvePromptText());
            prompt.KeyText = KeyOverride;
            prompt.Icon = Icon;
            prompt.Anchor = PromptAnchor != null ? PromptAnchor : transform;
            prompt.WorldOffset = Vector3.zero;
            prompt.AccentColor = AccentColor;
            prompt.Priority = Priority;
            prompt.IsAvailable = IsAvailable;
            prompt.UnavailableText = ResolveUnavailableText();
            return IsAvailable || !HideWhenUnavailable;
        }

        public bool CanInteract(GameObject actor, out string unavailableReason)
        {
            unavailableReason = ResolveUnavailableText();
            if (!IsAvailable)
            {
                return false;
            }

            if (InteractionTarget is ICampusInteractionAvailability targetAvailability &&
                !targetAvailability.CanInteract(actor, out unavailableReason))
            {
                return false;
            }

            return true;
        }

        public void Interact(GameObject actor)
        {
            if (TryHandleAction(actor))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(ActionId))
            {
                return;
            }

            if (InteractionTarget is ICampusInteractable target && !ReferenceEquals(target, this))
            {
                target.Interact(actor);
                return;
            }

            if (!LogInteraction)
            {
                return;
            }

            string message = LocalizedInteractionLogMessage.HasAnyText
                ? LocalizedInteractionLogMessage.Current(InteractionLogMessage)
                : !string.IsNullOrWhiteSpace(InteractionLogMessage)
                ? InteractionLogMessage
                : ResolvePromptText();
            Debug.Log(message, this);
        }

        private bool TryHandleAction(GameObject actor)
        {
            if (string.IsNullOrWhiteSpace(ActionId))
            {
                return false;
            }

            return CampusGameplayActionService.TryExecuteInteraction(this, actor);
        }

        private string ResolvePromptText()
        {
            if (UseTargetDoorStatePrompt &&
                InteractionTarget is Component targetComponent &&
                TryReadIsOpen(targetComponent, out bool isOpen))
            {
                return CampusInteractionTextCatalog.Get(isOpen
                    ? CampusInteractionTextId.CloseDoor
                    : CampusInteractionTextId.OpenDoor);
            }

            if (LocalizedPromptText.HasAnyText)
            {
                return LocalizedPromptText.Current(PromptText);
            }

            return IsLegacyDefaultPrompt(PromptText)
                ? CampusInteractionTextCatalog.Get(CampusInteractionTextId.Interact)
                : PromptText.Trim();
        }

        private string ResolveUnavailableText()
        {
            return LocalizedUnavailableText.HasAnyText
                ? LocalizedUnavailableText.Current(UnavailableText)
                : UnavailableText;
        }

        private static bool IsLegacyDefaultPrompt(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Trim() == "\u4ea4\u4e92";
        }

        private void ConfigureCollider()
        {
            Collider2D anchorCollider = GetComponent<Collider2D>();
            if (anchorCollider != null)
            {
                anchorCollider.isTrigger = true;
            }
        }

        private static bool TryReadIsOpen(Component component, out bool isOpen)
        {
            isOpen = false;
            if (component == null)
            {
                return false;
            }

            PropertyInfo property = component.GetType().GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(bool))
            {
                return false;
            }

            isOpen = (bool)property.GetValue(component, null);
            return true;
        }
    }
}
