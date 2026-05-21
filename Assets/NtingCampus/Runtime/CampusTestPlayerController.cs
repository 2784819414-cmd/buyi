using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using NtingCampus.Gameplay.Characters;

namespace NtingCampusMapEditor
{
    [RequireComponent(typeof(CampusCharacterBodyController))]
    [DisallowMultipleComponent]
    public sealed class CampusTestPlayerController : MonoBehaviour
    {
        private enum PlayerControlState
        {
            Disabled,
            FreeMove
        }

        public float MoveSpeed => CampusCharacterMovementTuning.ResolvePlayerMoveSpeed();
        public int FloorIndex = 1;
        public KeyCode InteractKey = KeyCode.E;
        public float InteractionForwardOffset = 0.28f;
        public float InteractionRadius = 0.82f;
        public float InteractionRefreshIntervalSeconds = 0.1f;
        public LayerMask InteractionMask = Physics2D.AllLayers;

        private CampusCharacterBodyController bodyController;
        private CampusInteractionController interactionController;
        private Vector2 moveInput;
        private Vector2 facingDirection = Vector2.down;
        [SerializeField] private PlayerControlState controlState = PlayerControlState.FreeMove;

        public bool GameplayInputEnabled { get; private set; } = true;
        public bool IsMoving => moveInput.sqrMagnitude > 0.0001f;
        public Vector2 FacingDirection => facingDirection;

        private void Awake()
        {
            EnsureBodyController();
            ConfigureBodyController();
            ConfigureInteractionController();
        }

        private void Update()
        {
            if (!GameplayInputEnabled)
            {
                EnterDisabledState();
                return;
            }

            UpdateFreeMoveState();
        }

        private void UpdateFreeMoveState()
        {
            controlState = PlayerControlState.FreeMove;
            moveInput = ReadMoveInput();
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                facingDirection = moveInput.normalized;
            }

            ConfigureBodyController();
            bodyController.SetMovementInput(moveInput);
            UpdateInteractionState();
        }

        private void UpdateInteractionState()
        {
            ConfigureInteractionController();
            interactionController.enabled = true;
            interactionController.SetFacingDirection(facingDirection);
            bool interactPressed = CampusInteractionInput.WasKeyPressed(InteractKey);
            if (interactPressed)
            {
                interactionController.TryInteractCurrent();
                return;
            }

            interactionController.RefreshTargetIfNeeded();
        }

        private void EnterDisabledState()
        {
            controlState = PlayerControlState.Disabled;
            moveInput = Vector2.zero;
            ConfigureBodyController();
            bodyController.StopMovement();
            ConfigureInteractionController();
            if (interactionController != null)
            {
                interactionController.enabled = false;
            }
        }

        private void Reset()
        {
            EnsureBodyController();
            ConfigureBodyController();
            ConfigureInteractionController();
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            if (enabled && !this.enabled)
            {
                this.enabled = true;
            }

            GameplayInputEnabled = enabled;
            moveInput = Vector2.zero;
            controlState = enabled ? PlayerControlState.FreeMove : PlayerControlState.Disabled;
            EnsureBodyController();
            ConfigureBodyController();
            bodyController.SetMovementEnabled(enabled);
            bodyController.StopMovement();

            ConfigureInteractionController();
            if (interactionController != null)
            {
                interactionController.enabled = enabled;
            }
        }

        private void EnsureBodyController()
        {
            if (bodyController == null)
            {
                bodyController = GetComponent<CampusCharacterBodyController>();
            }

            if (bodyController == null)
            {
                bodyController = gameObject.AddComponent<CampusCharacterBodyController>();
            }
        }

        private void ConfigureBodyController()
        {
            EnsureBodyController();
            if (bodyController == null)
            {
                return;
            }

            bodyController.MoveSpeed = MoveSpeed;
            bodyController.FloorIndex = FloorIndex;
            bodyController.EnsureSetup();
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
            interactionController.RefreshIntervalSeconds = InteractionRefreshIntervalSeconds;

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

    }
}
