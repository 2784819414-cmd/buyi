using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    public readonly struct CampusInteractionActionContext
    {
        public CampusInteractionActionContext(
            CampusSimpleInteractable sourceInteractable,
            CampusInteractionAnchor anchor,
            string actionId,
            string payload,
            GameObject actor)
            : this(
                sourceInteractable,
                anchor,
                actionId,
                payload,
                actor,
                sourceInteractable,
                sourceInteractable != null ? sourceInteractable.GetComponent<CampusPlacedObject>() : null)
        {
        }

        public CampusInteractionActionContext(
            CampusSimpleInteractable sourceInteractable,
            CampusInteractionAnchor anchor,
            string actionId,
            string payload,
            GameObject actor,
            Component directTarget,
            CampusPlacedObject sourceObject)
        {
            SourceInteractable = sourceInteractable;
            Anchor = anchor;
            ActionId = CampusInteractionActionIds.Normalize(actionId);
            Payload = payload ?? string.Empty;
            Actor = actor;
            DirectTarget = directTarget;
            SourceObject = sourceObject != null ? sourceObject : ResolveSourceObject(sourceInteractable, anchor, directTarget);
            FacilityType = CampusFacilityTypeResolver.Resolve(SourceObject);
        }

        public CampusSimpleInteractable SourceInteractable { get; }
        public CampusInteractionAnchor Anchor { get; }
        public string ActionId { get; }
        public string Payload { get; }
        public GameObject Actor { get; }
        public Component DirectTarget { get; }
        public CampusPlacedObject SourceObject { get; }
        public CampusFacilityType FacilityType { get; }

        private static CampusPlacedObject ResolveSourceObject(
            CampusSimpleInteractable sourceInteractable,
            CampusInteractionAnchor anchor,
            Component directTarget)
        {
            if (sourceInteractable != null &&
                sourceInteractable.TryGetComponent(out CampusPlacedObject sourceObject))
            {
                return sourceObject;
            }

            if (directTarget != null &&
                directTarget.TryGetComponent(out CampusPlacedObject directPlacedObject))
            {
                return directPlacedObject;
            }

            if (directTarget != null)
            {
                CampusPlacedObject parentPlacedObject = directTarget.GetComponentInParent<CampusPlacedObject>();
                if (parentPlacedObject != null)
                {
                    return parentPlacedObject;
                }
            }

            return anchor != null ? anchor.GetComponentInParent<CampusPlacedObject>() : null;
        }
    }
}
