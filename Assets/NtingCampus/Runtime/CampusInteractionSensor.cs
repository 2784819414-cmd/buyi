using System;
using System.Reflection;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [Serializable]
    public struct CampusInteractionTarget
    {
        public Collider2D Collider;
        public Component InteractableComponent;
        public CampusInteractionPromptData Prompt;
        public float Distance;
        public bool CanInteract;

        public ICampusInteractable Interactable => InteractableComponent as ICampusInteractable;

        public bool IsValid => Collider != null && InteractableComponent != null && Interactable != null;

        public Transform TargetTransform => InteractableComponent != null ? InteractableComponent.transform : null;

        public void Interact(GameObject actor)
        {
            if (!CanInteract)
            {
                return;
            }

            ICampusInteractable interactable = Interactable;
            if (interactable != null)
            {
                interactable.Interact(actor);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class CampusInteractionSensor : MonoBehaviour
    {
        private const int MinHitBufferSize = 128;
        private const int MaxHitBufferSize = 512;

        public Transform ScanOrigin;
        public Vector2 FacingDirection = Vector2.down;
        public float ForwardOffset = 0.45f;
        public float Radius = 0.55f;
        public LayerMask InteractionMask = Physics2D.AllLayers;
        public string DefaultKeyText = "E";
        public Vector3 DefaultPromptOffset = new Vector3(0f, 0.85f, 0f);
        public int MaxHits = MinHitBufferSize;

        private Collider2D[] hitBuffer;

        public Vector2 OriginPosition
        {
            get
            {
                Transform origin = ScanOrigin != null ? ScanOrigin : transform;
                return origin.position;
            }
        }

        public Vector2 ScanCenter
        {
            get
            {
                Vector2 direction = FacingDirection.sqrMagnitude > 0.0001f ? FacingDirection.normalized : Vector2.down;
                return OriginPosition + direction * ForwardOffset;
            }
        }

        private void Awake()
        {
            EnsureHitBuffer();
        }

        private void OnValidate()
        {
            ForwardOffset = Mathf.Max(0f, ForwardOffset);
            Radius = Mathf.Max(0.01f, Radius);
            MaxHits = Mathf.Clamp(MaxHits, MinHitBufferSize, MaxHitBufferSize);
        }

        public void SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                FacingDirection = direction.normalized;
            }
        }

        public bool TryGetBestTarget(GameObject actor, out CampusInteractionTarget target)
        {
            return TryGetBestTarget(actor, default, out target);
        }

        public bool TryGetBestTarget(GameObject actor, CampusInteractionTarget currentTarget, out CampusInteractionTarget target)
        {
            EnsureHitBuffer();

            target = default;
            Vector2 origin = OriginPosition;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(InteractionMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;
            int count = OverlapInteractionCircle(filter);
            bool hasTarget = false;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hitBuffer[i];
                if (!TryBuildTarget(hit, actor, origin, out CampusInteractionTarget candidate))
                {
                    continue;
                }

                if (!hasTarget || IsBetterCandidate(candidate, target))
                {
                    target = candidate;
                    hasTarget = true;
                }
            }

            return hasTarget;
        }

        private void EnsureHitBuffer()
        {
            int size = Mathf.Clamp(MaxHits, MinHitBufferSize, MaxHitBufferSize);
            if (hitBuffer == null || hitBuffer.Length != size)
            {
                hitBuffer = new Collider2D[size];
            }
        }

        private int OverlapInteractionCircle(ContactFilter2D filter)
        {
            int count = Physics2D.OverlapCircle(ScanCenter, Radius, filter, hitBuffer);
            while (count >= hitBuffer.Length && hitBuffer.Length < MaxHitBufferSize)
            {
                int nextSize = Mathf.Min(hitBuffer.Length * 2, MaxHitBufferSize);
                hitBuffer = new Collider2D[nextSize];
                count = Physics2D.OverlapCircle(ScanCenter, Radius, filter, hitBuffer);
            }

            return count;
        }

        private bool TryBuildTarget(Collider2D hit, GameObject actor, Vector2 origin, out CampusInteractionTarget target)
        {
            target = default;
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                return false;
            }

            if (ShouldIgnoreLegacyCollider(hit))
            {
                return false;
            }

            if (!TryResolveComponents(hit, out Component interactableComponent, out ICampusInteractionPromptProvider promptProvider, out ICampusInteractionAvailability[] availabilityChecks))
            {
                return false;
            }

            bool canInteract = ResolveAvailability(actor, availabilityChecks, out string unavailableReason);
            CampusInteractionPromptData prompt;
            if (promptProvider != null)
            {
                if (!promptProvider.TryGetInteractionPrompt(actor, out prompt))
                {
                    return false;
                }
            }
            else
            {
                prompt = CreateFallbackPrompt(hit, interactableComponent);
            }

            NormalizePrompt(ref prompt, interactableComponent, canInteract, unavailableReason);

            target = new CampusInteractionTarget
            {
                Collider = hit,
                InteractableComponent = interactableComponent,
                Prompt = prompt,
                Distance = Vector2.Distance(origin, ResolveTargetDistancePoint(hit, interactableComponent, prompt.Anchor)),
                CanInteract = canInteract && prompt.IsAvailable
            };
            return true;
        }

        private static Vector2 ResolveTargetDistancePoint(Collider2D hit, Component interactableComponent, Transform promptAnchor)
        {
            CampusInteractionAnchor anchor = hit != null ? hit.GetComponentInParent<CampusInteractionAnchor>(true) : null;
            if (anchor != null)
            {
                Transform anchorTransform = anchor.PromptAnchor != null ? anchor.PromptAnchor : anchor.transform;
                return anchorTransform.position;
            }

            if (promptAnchor != null)
            {
                return promptAnchor.position;
            }

            if (interactableComponent != null)
            {
                return interactableComponent.transform.position;
            }

            return hit != null ? hit.bounds.center : Vector2.zero;
        }

        private static bool ShouldIgnoreLegacyCollider(Collider2D hit)
        {
            if (hit == null || hit.GetComponentInParent<CampusInteractionAnchor>(true) != null)
            {
                return false;
            }

            CampusPlacedObject placedObject = hit.GetComponentInParent<CampusPlacedObject>(true);
            return placedObject != null && placedObject.GetComponentInChildren<CampusInteractionAnchor>(true) != null;
        }

        private static bool TryResolveComponents(
            Collider2D hit,
            out Component interactableComponent,
            out ICampusInteractionPromptProvider promptProvider,
            out ICampusInteractionAvailability[] availabilityChecks)
        {
            interactableComponent = null;
            promptProvider = null;

            CampusInteractionAnchor anchor = hit != null ? hit.GetComponentInParent<CampusInteractionAnchor>(true) : null;
            if (anchor != null && anchor.isActiveAndEnabled)
            {
                interactableComponent = anchor;
                promptProvider = anchor;
                availabilityChecks = new ICampusInteractionAvailability[] { anchor };
                return true;
            }

            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
            CampusInteractionAvailabilityCollector availabilityCollector = new CampusInteractionAvailabilityCollector(behaviours.Length);

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                if (interactableComponent == null && behaviour is ICampusInteractable)
                {
                    interactableComponent = behaviour;
                }

                if (promptProvider == null && behaviour is ICampusInteractionPromptProvider provider)
                {
                    promptProvider = provider;
                }

                if (behaviour is ICampusInteractionAvailability availability)
                {
                    availabilityCollector.Add(availability);
                }
            }

            availabilityChecks = availabilityCollector.ToArray();
            return interactableComponent != null;
        }

        private static bool ResolveAvailability(GameObject actor, ICampusInteractionAvailability[] availabilityChecks, out string unavailableReason)
        {
            unavailableReason = null;
            for (int i = 0; i < availabilityChecks.Length; i++)
            {
                ICampusInteractionAvailability availability = availabilityChecks[i];
                if (availability == null)
                {
                    continue;
                }

                if (!availability.CanInteract(actor, out unavailableReason))
                {
                    return false;
                }
            }

            return true;
        }

        private CampusInteractionPromptData CreateFallbackPrompt(Collider2D hit, Component interactableComponent)
        {
            CampusInteractionPromptData prompt = CampusInteractionPromptData.Create(ResolveFallbackPromptText(interactableComponent));
            prompt.Anchor = hit != null ? hit.transform : interactableComponent != null ? interactableComponent.transform : null;
            prompt.WorldOffset = ResolveFallbackPromptOffset(hit, prompt.Anchor);
            return prompt;
        }

        private Vector3 ResolveFallbackPromptOffset(Collider2D hit, Transform anchor)
        {
            if (hit == null || anchor == null)
            {
                return DefaultPromptOffset;
            }

            Bounds bounds = hit.bounds;
            Vector3 offset = bounds.center - anchor.position;
            float verticalPadding = Mathf.Max(0f, DefaultPromptOffset.y - 0.5f);
            offset.y += bounds.extents.y + verticalPadding;
            offset.z += DefaultPromptOffset.z;
            return offset;
        }

        private static string ResolveFallbackPromptText(Component interactableComponent)
        {
            if (interactableComponent == null)
            {
                return CampusInteractionTextCatalog.Get(CampusInteractionTextId.Interact);
            }

            string typeName = interactableComponent.GetType().Name;
            string objectName = CampusObjectNames.GetDisplayName(interactableComponent.gameObject.name);
            bool isDoor = typeName.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0 || objectName.Contains("门");
            if (isDoor)
            {
                return CampusInteractionTextCatalog.Get(
                    TryReadIsOpen(interactableComponent, out bool isOpen) && isOpen
                        ? CampusInteractionTextId.CloseDoor
                        : CampusInteractionTextId.OpenDoor);
            }

            CampusPlacedObject placedObject = interactableComponent.GetComponentInParent<CampusPlacedObject>();
            if (placedObject != null && !string.IsNullOrWhiteSpace(placedObject.ObjectId))
            {
                return CampusInteractionTextCatalog.Format(
                    CampusInteractionTextId.InteractWith,
                    CampusObjectNames.GetDisplayName(placedObject.ObjectId));
            }

            return CampusInteractionTextCatalog.Format(CampusInteractionTextId.InteractWith, objectName);
        }

        private static bool TryReadIsOpen(Component component, out bool isOpen)
        {
            isOpen = false;
            PropertyInfo property = component.GetType().GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(bool))
            {
                return false;
            }

            isOpen = (bool)property.GetValue(component, null);
            return true;
        }

        private void NormalizePrompt(ref CampusInteractionPromptData prompt, Component interactableComponent, bool canInteract, string unavailableReason)
        {
            if (string.IsNullOrWhiteSpace(prompt.KeyText))
            {
                prompt.KeyText = DefaultKeyText;
            }

            if (prompt.Anchor == null && interactableComponent != null)
            {
                prompt.Anchor = interactableComponent.transform;
            }

            if (prompt.AccentColor.a <= 0f)
            {
                prompt.AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
            }

            if (!canInteract)
            {
                prompt.IsAvailable = false;
                if (!string.IsNullOrWhiteSpace(unavailableReason))
                {
                    prompt.UnavailableText = unavailableReason;
                }
            }
        }

        private static bool IsBetterCandidate(CampusInteractionTarget candidate, CampusInteractionTarget current)
        {
            if (!Mathf.Approximately(candidate.Distance, current.Distance))
            {
                return candidate.Distance < current.Distance;
            }

            if (candidate.CanInteract != current.CanInteract)
            {
                return candidate.CanInteract;
            }

            return candidate.Prompt.Priority > current.Prompt.Priority;
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 center = ScanCenter;
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);
            Gizmos.DrawSphere(center, Radius);
        }

        private struct CampusInteractionAvailabilityCollector
        {
            private readonly ICampusInteractionAvailability[] items;
            private int count;

            public CampusInteractionAvailabilityCollector(int capacity)
            {
                items = new ICampusInteractionAvailability[Mathf.Max(0, capacity)];
                count = 0;
            }

            public void Add(ICampusInteractionAvailability item)
            {
                if (item == null || count >= items.Length)
                {
                    return;
                }

                items[count] = item;
                count++;
            }

            public ICampusInteractionAvailability[] ToArray()
            {
                if (count <= 0)
                {
                    return Array.Empty<ICampusInteractionAvailability>();
                }

                ICampusInteractionAvailability[] result = new ICampusInteractionAvailability[count];
                Array.Copy(items, result, count);
                return result;
            }
        }
    }
}
