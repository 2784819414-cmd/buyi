using System.Reflection;
using NtingCampus.Gameplay.Core;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CampusInteractionAnchor : MonoBehaviour, ICampusInteractable, ICampusInteractionPromptProvider, ICampusInteractionAvailability
    {
        private const string DefaultPromptText = "\u4ea4\u4e92";

        public MonoBehaviour InteractionTarget;
        public string ActionId;
        public string Payload;
        public Transform PromptAnchor;
        public string PromptText = DefaultPromptText;
        public string KeyOverride;
        public Sprite Icon;
        public Color AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
        public int Priority = 100;
        public bool IsAvailable = true;
        public string UnavailableText;
        public bool HideWhenUnavailable;
        public bool UseTargetDoorStatePrompt;
        public bool LogInteraction = true;
        public string InteractionLogMessage;

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
            prompt = CampusInteractionPromptData.Create(ResolvePromptText());
            prompt.KeyText = KeyOverride;
            prompt.Icon = Icon;
            prompt.Anchor = PromptAnchor != null ? PromptAnchor : transform;
            prompt.WorldOffset = Vector3.zero;
            prompt.AccentColor = AccentColor;
            prompt.Priority = Priority;
            prompt.IsAvailable = IsAvailable;
            prompt.UnavailableText = UnavailableText;
            return IsAvailable || !HideWhenUnavailable;
        }

        public bool CanInteract(GameObject actor, out string unavailableReason)
        {
            unavailableReason = UnavailableText;
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

            string message = !string.IsNullOrWhiteSpace(InteractionLogMessage)
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
                return isOpen ? "\u5173\u95e8" : "\u5f00\u95e8";
            }

            return string.IsNullOrWhiteSpace(PromptText) ? DefaultPromptText : PromptText;
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
