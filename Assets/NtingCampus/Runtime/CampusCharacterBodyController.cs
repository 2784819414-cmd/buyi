using UnityEngine;
using UnityEngine.Rendering;

namespace NtingCampusMapEditor
{
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class CampusCharacterBodyController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private int floorIndex = 1;
        [SerializeField] private Vector2 colliderOffset = new Vector2(0f, -0.08f);
        [SerializeField] private Vector2 colliderSize = new Vector2(0.45f, 0.72f);
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private CapsuleCollider2D solidCollider;
        [SerializeField] private SortingGroup sortingGroup;

        private NtingShadowCasterProfile shadowCasterProfile;
        private Vector2 moveInput;
        private bool movementEnabled = true;

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

        private void Awake()
        {
            EnsureSetup();
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

            Vector2 velocity = movementEnabled ? moveInput.normalized * moveSpeed : Vector2.zero;
            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            RefreshSorting();
        }

        public void EnsureSetup()
        {
            body = body != null ? body : GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            solidCollider = solidCollider != null ? solidCollider : GetComponent<CapsuleCollider2D>();
            if (solidCollider == null)
            {
                solidCollider = gameObject.AddComponent<CapsuleCollider2D>();
            }

            sortingGroup = sortingGroup != null ? sortingGroup : GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
            }

            ConfigureBody();
            ConfigureCollider();
            EnsureShadowCasterProfile();
            RefreshSorting();
        }

        public void SetMovementInput(Vector2 input)
        {
            moveInput = movementEnabled ? input : Vector2.zero;
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (!movementEnabled)
            {
                moveInput = Vector2.zero;
            }
        }

        public void Teleport(Vector3 worldPosition)
        {
            if (body != null)
            {
                body.position = worldPosition;
            }

            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            RefreshSorting();
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
            CampusMapRoot root = FindFirstObjectByType<CampusMapRoot>();
            int step = root != null ? root.SortingOrderStepPerFloor : 1000;
            sortingGroup.sortingOrder = FloorIndex * step + CampusRenderSortingUtility.SharedWallObjectOffset;
            sortingGroup.sortingLayerID = SortingLayer.NameToID("Default");
        }
    }
}
