using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NtingCampusMapEditor
{
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class CampusCharacterBodyController : MonoBehaviour
    {
        private static readonly List<CampusCharacterBodyController> ActiveBodies =
            new List<CampusCharacterBodyController>();
        private const float SortingCacheRefreshSeconds = 1f;
        private static CampusMapRoot cachedMapRoot;
        private static float nextSortingCacheRefreshTime;
        private static int cachedSortingStep = 1000;
        private static int cachedSortingLayerId;
        private static bool sortingCacheInitialized;

        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private int floorIndex = 1;
        [SerializeField] private Vector2 colliderOffset = new Vector2(0f, -0.08f);
        [SerializeField] private Vector2 colliderSize = new Vector2(0.45f, 0.72f);
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private CapsuleCollider2D solidCollider;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private CampusCharacterFacingState facingState;

        private NtingShadowCasterProfile shadowCasterProfile;
        private Vector2 moveInput;
        private bool movementEnabled = true;
        private bool automaticSortingEnabled = true;
        private bool setupComplete;
        private int lastAppliedSortingOrder = int.MinValue;
        private int lastAppliedSortingLayerId = int.MinValue;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0.01f, value);
        }

        public int FloorIndex
        {
            get => Mathf.Max(1, floorIndex);
            set => floorIndex = Mathf.Max(1, value);
        }

        public Rigidbody2D Body => body;
        public CapsuleCollider2D SolidCollider => solidCollider;
        public static IReadOnlyList<CampusCharacterBodyController> Bodies => ActiveBodies;

        private void Awake()
        {
            EnsureSetup();
        }

        private void OnEnable()
        {
            EnsureSetup();
            RegisterBody(this);
        }

        private void OnDisable()
        {
            UnregisterBody(this);
        }

        private void OnDestroy()
        {
            UnregisterBody(this);
        }

        private void Reset()
        {
            EnsureSetup();
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            Vector2 normalizedInput = movementEnabled && moveInput.sqrMagnitude > 0.0001f
                ? moveInput.normalized
                : Vector2.zero;
            Vector2 velocity = normalizedInput * moveSpeed;
            if (velocity.sqrMagnitude <= 0.0001f)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            Vector2 desiredDelta = velocity * Time.fixedDeltaTime;
            if (desiredDelta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            body.MovePosition(body.position + desiredDelta);
        }

        private void LateUpdate()
        {
            if (automaticSortingEnabled)
            {
                RefreshSorting();
            }
        }

        public void EnsureSetup()
        {
            bool needsConfigure = !setupComplete;
            body = body != null ? body : GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
                needsConfigure = true;
            }

            solidCollider = solidCollider != null ? solidCollider : GetComponent<CapsuleCollider2D>();
            if (solidCollider == null)
            {
                solidCollider = gameObject.AddComponent<CapsuleCollider2D>();
                needsConfigure = true;
            }

            sortingGroup = sortingGroup != null ? sortingGroup : GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
                needsConfigure = true;
            }

            facingState = facingState != null ? facingState : GetComponent<CampusCharacterFacingState>();
            if (facingState == null)
            {
                facingState = gameObject.AddComponent<CampusCharacterFacingState>();
                needsConfigure = true;
            }

            if (!needsConfigure)
            {
                return;
            }

            ConfigureBody();
            ConfigureCollider();
            EnsureShadowCasterProfile();
            if (automaticSortingEnabled)
            {
                RefreshSorting();
            }

            RefreshCharacterCollisionIgnores();
            setupComplete = true;
        }

        public void SetMovementInput(Vector2 input)
        {
            moveInput = movementEnabled && input.sqrMagnitude > 0.0001f ? input.normalized : Vector2.zero;
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                facingState = facingState != null ? facingState : GetComponent<CampusCharacterFacingState>();
                facingState?.SetMovementDirection(moveInput);
            }
        }

        public void StopMovement()
        {
            moveInput = Vector2.zero;
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (!movementEnabled)
            {
                moveInput = Vector2.zero;
            }
        }

        public void SetAutomaticSortingEnabled(bool enabled)
        {
            automaticSortingEnabled = enabled;
            if (automaticSortingEnabled)
            {
                RefreshSorting();
            }
        }

        public void Teleport(Vector3 worldPosition)
        {
            if (body != null)
            {
                body.position = worldPosition;
            }

            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            if (automaticSortingEnabled)
            {
                RefreshSorting();
            }
        }

        private void ConfigureBody()
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearDamping = 16f;
        }

        private void ConfigureCollider()
        {
            solidCollider.isTrigger = false;
            solidCollider.direction = CapsuleDirection2D.Vertical;
            solidCollider.offset = colliderOffset;
            solidCollider.size = colliderSize;
        }

        private static void RegisterBody(CampusCharacterBodyController controller)
        {
            if (controller == null || ActiveBodies.Contains(controller))
            {
                return;
            }

            ActiveBodies.Add(controller);
            RefreshCharacterCollisionIgnores();
        }

        private static void UnregisterBody(CampusCharacterBodyController controller)
        {
            if (controller == null)
            {
                return;
            }

            ActiveBodies.Remove(controller);
            RefreshCharacterCollisionIgnores();
        }

        private static void RefreshCharacterCollisionIgnores()
        {
            for (int i = 0; i < ActiveBodies.Count; i++)
            {
                CampusCharacterBodyController left = ActiveBodies[i];
                if (left == null || left.solidCollider == null)
                {
                    continue;
                }

                for (int j = i + 1; j < ActiveBodies.Count; j++)
                {
                    CampusCharacterBodyController right = ActiveBodies[j];
                    if (right == null || right.solidCollider == null)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(left.solidCollider, right.solidCollider, true);
                }
            }
        }

        private void EnsureShadowCasterProfile()
        {
            shadowCasterProfile = NtingShadowCasterProfile.EnsureForObject(gameObject);
            if (shadowCasterProfile == null)
            {
                return;
            }

            shadowCasterProfile.ApplyCharacterDefaults();
            shadowCasterProfile.castCustomShadows = true;
            shadowCasterProfile.castPointLightShadows = true;
            shadowCasterProfile.castSunShadow = true;
        }

        private void RefreshSorting()
        {
            if (sortingGroup == null)
            {
                return;
            }

            CampusRenderSortingUtility.ConfigureTopDownTransparencySort();
            RefreshSortingCacheIfNeeded();
            int sortingOrder = FloorIndex * cachedSortingStep + CampusRenderSortingUtility.SharedWallObjectOffset;
            if (sortingOrder != lastAppliedSortingOrder)
            {
                sortingGroup.sortingOrder = sortingOrder;
                lastAppliedSortingOrder = sortingOrder;
            }

            if (cachedSortingLayerId != lastAppliedSortingLayerId)
            {
                sortingGroup.sortingLayerID = cachedSortingLayerId;
                lastAppliedSortingLayerId = cachedSortingLayerId;
            }
        }

        private static void RefreshSortingCacheIfNeeded()
        {
            if (sortingCacheInitialized &&
                (!Application.isPlaying || Time.unscaledTime < nextSortingCacheRefreshTime) &&
                cachedMapRoot != null)
            {
                return;
            }

            cachedMapRoot = cachedMapRoot != null
                ? cachedMapRoot
                : FindFirstObjectByType<CampusMapRoot>();
            cachedSortingStep = cachedMapRoot != null ? cachedMapRoot.SortingOrderStepPerFloor : 1000;
            cachedSortingLayerId = SortingLayer.NameToID("Default");
            sortingCacheInitialized = true;
            nextSortingCacheRefreshTime = Application.isPlaying
                ? Time.unscaledTime + SortingCacheRefreshSeconds
                : 0f;
        }
    }
}
