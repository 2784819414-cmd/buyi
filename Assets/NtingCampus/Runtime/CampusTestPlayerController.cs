using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NtingCampusMapEditor
{
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class CampusTestPlayerController : MonoBehaviour
    {
        public float MoveSpeed = 3.5f;
        public int FloorIndex = 1;
        public KeyCode InteractKey = KeyCode.E;
        public float InteractionForwardOffset = 0.45f;
        public float InteractionRadius = 0.55f;
        public LayerMask InteractionMask = Physics2D.AllLayers;

        private Rigidbody2D body;
        private SortingGroup sortingGroup;
        private CampusInteractionController interactionController;
        private Vector2 moveInput;
        private Vector2 facingDirection = Vector2.down;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            sortingGroup = GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
            }

            ConfigureBody();
            ConfigureInteractionController();
            RefreshSorting();
        }

        private void Update()
        {
            moveInput = ReadMoveInput();
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                facingDirection = moveInput.normalized;
            }

            ConfigureInteractionController();
            interactionController.SetFacingDirection(facingDirection);
            interactionController.RefreshTarget();
            if (CampusInteractionInput.WasKeyPressed(InteractKey))
            {
                interactionController.TryInteractCurrent();
            }
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            Vector2 delta = moveInput.normalized * (MoveSpeed * Time.fixedDeltaTime);
            body.MovePosition(body.position + delta);
        }

        private void LateUpdate()
        {
            RefreshSorting();
        }

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            sortingGroup = GetComponent<SortingGroup>();
            ConfigureBody();
            ConfigureInteractionController();
        }

        private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void ConfigureInteractionController()
        {
            if (interactionController == null)
            {
                interactionController = GetComponent<CampusInteractionController>();
            }

            if (interactionController == null)
            {
                interactionController = gameObject.AddComponent<CampusInteractionController>();
            }

            interactionController.InteractKey = InteractKey;
            interactionController.PollInput = false;
            interactionController.RefreshEveryFrame = false;

            CampusInteractionSensor sensor = interactionController.GetOrCreateSensor();
            sensor.ScanOrigin = transform;
            sensor.ForwardOffset = InteractionForwardOffset;
            sensor.Radius = InteractionRadius;
            sensor.InteractionMask = InteractionMask;
            sensor.SetFacingDirection(facingDirection);
        }

        private static Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (TryReadMoveInputFromInputSystem(out Vector2 inputSystemMove))
            {
                return inputSystemMove;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return ReadMoveInputFromLegacyInputManager();
#else
            return Vector2.zero;
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static Vector2 ReadMoveInputFromLegacyInputManager()
        {
            float x = 0f;
            float y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                x -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                x += 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                y -= 1f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                y += 1f;
            }

            return new Vector2(x, y);
        }
#endif

#if ENABLE_INPUT_SYSTEM
        private static bool TryReadMoveInputFromInputSystem(out Vector2 move)
        {
            move = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                y += 1f;
            }

            move = new Vector2(x, y);
            return true;
        }
#endif

        private void RefreshSorting()
        {
            if (sortingGroup == null)
            {
                return;
            }

            CampusRenderSortingUtility.ConfigureTopDownTransparencySort();
            CampusMapRoot root = FindFirstObjectByType<CampusMapRoot>();
            int step = root != null ? root.SortingOrderStepPerFloor : 1000;
            sortingGroup.sortingOrder = Mathf.Max(1, FloorIndex) * step + CampusRenderSortingUtility.SharedWallObjectOffset;
            sortingGroup.sortingLayerID = SortingLayer.NameToID("Default");
        }
    }
}
