using System.Collections;
using UnityEngine;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Opens a restroom stall door around a hinge pivot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RestroomStallDoor : MonoBehaviour, ICampusInteractable
    {
        public Transform DoorPivot;
        public SpriteRenderer DoorRenderer;
        public Sprite ClosedSprite;
        public Sprite OpenSprite;
        public Collider2D DoorCollider;
        public CampusPlacedObject PlacedObject;
        public bool StartsOpen;
        public float OpenAngle = 90f;
        public float AnimationDuration = 0.22f;

        [SerializeField]
        private bool isOpen;

        private Coroutine animationRoutine;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            CacheReferences();
            SetOpenImmediate(StartsOpen);
        }

        private void Reset()
        {
            CacheReferences();
            SetOpen(StartsOpen);
        }

        private void OnValidate()
        {
            CacheReferences();

            if (!Application.isPlaying)
            {
                SetOpenImmediate(StartsOpen);
            }
        }

        public void ToggleOpen()
        {
            SetOpen(!isOpen);
        }

        public void Open()
        {
            SetOpen(true);
        }

        public void Close()
        {
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            CacheReferences();
            if (!Application.isPlaying || AnimationDuration <= 0f)
            {
                SetOpenImmediate(open);
                return;
            }

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimateDoor(open));
        }

        public void Interact(GameObject actor)
        {
            ToggleOpen();
        }

        private IEnumerator AnimateDoor(bool open)
        {
            isOpen = open;
            ApplyBlockingState(open, open);

            float startAngle = DoorPivot != null ? DoorPivot.localEulerAngles.z : (isOpen ? OpenAngle : 0f);
            if (startAngle > 180f)
            {
                startAngle -= 360f;
            }

            float targetAngle = open ? OpenAngle : 0f;
            float elapsed = 0f;
            while (elapsed < AnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / AnimationDuration);
                t = t * t * (3f - 2f * t);
                ApplyVisualState(open, Mathf.Lerp(startAngle, targetAngle, t));
                yield return null;
            }

            ApplyVisualState(open, targetAngle);
            ApplyBlockingState(open, true);
            animationRoutine = null;
        }

        private void SetOpenImmediate(bool open)
        {
            if (animationRoutine != null && Application.isPlaying)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            isOpen = open;
            ApplyVisualState(open, open ? OpenAngle : 0f);
            ApplyBlockingState(open, true);
        }

        private void ApplyVisualState(bool open, float angle)
        {
            if (DoorRenderer != null)
            {
                Sprite sprite = open ? OpenSprite : ClosedSprite;
                if (sprite != null)
                {
                    DoorRenderer.sprite = sprite;
                }
            }

            if (DoorPivot != null)
            {
                DoorPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void ApplyBlockingState(bool open, bool finalState)
        {
            if (DoorCollider != null)
            {
                DoorCollider.enabled = !open || (!finalState && !open);
            }

            if (PlacedObject != null)
            {
                PlacedObject.BlocksMovement = !open;
                PlacedObject.BlocksSight = !open;
            }
        }

        private void CacheReferences()
        {
            if (DoorPivot == null)
            {
                DoorPivot = CampusObjectNames.FindDirectChild(transform, CampusObjectNames.DoorPivot, CampusObjectNames.LegacyDoorPivot);
            }

            if (DoorRenderer == null)
            {
                Transform doorVisual = DoorPivot != null
                    ? CampusObjectNames.FindDirectChild(DoorPivot, CampusObjectNames.DoorVisual, CampusObjectNames.LegacyDoorVisual)
                    : CampusObjectNames.FindDirectChild(transform, CampusObjectNames.DoorVisual, CampusObjectNames.LegacyDoorVisual);
                DoorRenderer = doorVisual != null ? doorVisual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
            }

            if (DoorCollider == null)
            {
                Transform doorCollider = DoorPivot != null
                    ? CampusObjectNames.FindDirectChild(DoorPivot, CampusObjectNames.DoorCollider, CampusObjectNames.LegacyDoorCollider)
                    : CampusObjectNames.FindDirectChild(transform, CampusObjectNames.DoorCollider, CampusObjectNames.LegacyDoorCollider);
                DoorCollider = doorCollider != null ? doorCollider.GetComponent<Collider2D>() : GetComponent<Collider2D>();
            }

            if (PlacedObject == null)
            {
                PlacedObject = GetComponent<CampusPlacedObject>();
            }
        }
    }
}
