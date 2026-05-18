using System.Collections;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Opens a restroom stall door around a hinge pivot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RestroomStallDoor : MonoBehaviour
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
        public bool AutoOpenForNearbyCharacters = true;
        public float AutoOpenRadius = 1.05f;
        public float AutoCloseDelay = 0.7f;
        public float AutoScanInterval = 0.15f;
        public bool RequiresPermission;
        public string RequiredPermissionId;
        public List<string> AllowedCharacterIds = new List<string>();
        public List<CampusCharacterRole> AllowedRoles = new List<CampusCharacterRole>();

        [SerializeField]
        private bool isOpen;

        private Coroutine animationRoutine;
        private float nextAutoScanTime;
        private float lastAllowedCharacterNearTime = -999f;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            CacheReferences();
            SetOpenImmediate(StartsOpen);
            DisableDoorInteractionAnchors();
        }

        private void Reset()
        {
            CacheReferences();
            SetOpen(StartsOpen);
            DisableDoorInteractionAnchors();
        }

        private void OnValidate()
        {
            CacheReferences();

            if (!Application.isPlaying)
            {
                SetOpenImmediate(StartsOpen);
            }

            DisableDoorInteractionAnchors();
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

        private IEnumerator AnimateDoor(bool open)
        {
            isOpen = open;
            ApplyBlockingState(open);

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
            ApplyBlockingState(open);
            animationRoutine = null;
        }

        private void Update()
        {
            if (!Application.isPlaying || !AutoOpenForNearbyCharacters || Time.time < nextAutoScanTime)
            {
                return;
            }

            nextAutoScanTime = Time.time + Mathf.Max(0.05f, AutoScanInterval);
            if (HasAllowedCharacterNearby())
            {
                lastAllowedCharacterNearTime = Time.time;
                if (!isOpen)
                {
                    Open();
                }

                return;
            }

            if (isOpen && Time.time - lastAllowedCharacterNearTime >= Mathf.Max(0f, AutoCloseDelay))
            {
                Close();
            }
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
            ApplyBlockingState(open);
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

        private void ApplyBlockingState(bool open)
        {
            bool blocks = RequiresPermission && !open;
            if (DoorCollider != null)
            {
                DoorCollider.enabled = blocks;
            }

            if (PlacedObject != null)
            {
                PlacedObject.BlocksMovement = blocks;
                PlacedObject.BlocksSight = blocks;
                PlacedObject.IsInteractable = false;
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

            if (DoorCollider == null && RequiresPermission)
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

            DisableDoorInteractionAnchors();
        }

        private bool HasAllowedCharacterNearby()
        {
            float radius = Mathf.Max(0.05f, AutoOpenRadius);
            float radiusSqr = radius * radius;
            Vector2 doorPosition = DoorPivot != null ? DoorPivot.position : transform.position;
            IReadOnlyList<CampusCharacterBodyController> bodies = CampusCharacterBodyController.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                CampusCharacterBodyController body = bodies[i];
                if (body == null || !body.isActiveAndEnabled)
                {
                    continue;
                }

                if (((Vector2)body.transform.position - doorPosition).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                if (CanCharacterOpen(body))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanCharacterOpen(CampusCharacterBodyController body)
        {
            if (!RequiresPermission)
            {
                return true;
            }

            CampusCharacterRuntime runtime = body != null
                ? body.GetComponent<CampusCharacterRuntime>() ?? body.GetComponentInParent<CampusCharacterRuntime>()
                : null;
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return false;
            }

            if (AllowedCharacterIds != null)
            {
                string characterId = runtime.CharacterId;
                for (int i = 0; i < AllowedCharacterIds.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(AllowedCharacterIds[i]) &&
                        string.Equals(AllowedCharacterIds[i].Trim(), characterId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (AllowedRoles != null)
            {
                for (int i = 0; i < AllowedRoles.Count; i++)
                {
                    if (AllowedRoles[i] == data.Role)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void DisableDoorInteractionAnchors()
        {
            CampusInteractionAnchor[] anchors = GetComponentsInChildren<CampusInteractionAnchor>(true);
            for (int i = 0; i < anchors.Length; i++)
            {
                CampusInteractionAnchor anchor = anchors[i];
                if (anchor == null)
                {
                    continue;
                }

                if (!ReferenceEquals(anchor.InteractionTarget, this) &&
                    !CampusInteractionActionIds.Equals(anchor.ActionId, CampusInteractionActionIds.ToggleDoor) &&
                    !anchor.UseTargetDoorStatePrompt)
                {
                    continue;
                }

                anchor.IsAvailable = false;
                anchor.HideWhenUnavailable = true;
                Collider2D anchorCollider = anchor.GetComponent<Collider2D>();
                if (anchorCollider != null)
                {
                    anchorCollider.enabled = false;
                }
            }
        }
    }
}
