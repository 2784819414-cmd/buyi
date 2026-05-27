using UnityEngine;
using NtingCampus.Gameplay.Characters;
using UnityEngine.EventSystems;

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
        public float InteractionForwardOffset = 0.28f;
        public float InteractionRadius = 0.82f;
        public float InteractionRefreshIntervalSeconds = 0.1f;
        public LayerMask InteractionMask = Physics2D.AllLayers;

        private CampusCharacterBodyController bodyController;
        private CampusCharacterStaminaController staminaController;
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
            moveInput = CampusGameplayInputBindings.ReadMoveInput();
            bool wantsSprint = CampusGameplayInputBindings.IsHeld(CampusGameplayInputActionId.Sprint);
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                facingDirection = moveInput.normalized;
            }

            ConfigureBodyController(ResolveMoveSpeed(moveInput, wantsSprint));
            bodyController.SetMovementInput(moveInput);
            UpdateHeldItemUseState();
            UpdateInteractionState();
        }

        private void UpdateHeldItemUseState()
        {
            if (!CampusInteractionInput.WasMouseButtonPressed(1) || IsPointerOverUi())
            {
                return;
            }

            CampusCharacterRuntime runtime = GetComponent<CampusCharacterRuntime>();
            CampusCharacterActionExecutor.TryExecute(runtime, CampusCharacterAction.UseHeldItem(), out _);
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void UpdateInteractionState()
        {
            ConfigureInteractionController();
            interactionController.enabled = true;
            interactionController.SetFacingDirection(facingDirection);
            bool interactPressed = CampusGameplayInputBindings.WasPressed(CampusGameplayInputActionId.Interact);
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
            ConfigureBodyController(MoveSpeed);
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
            EnsureStaminaController();
            ConfigureBodyController(MoveSpeed);
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

        private void EnsureStaminaController()
        {
            if (staminaController == null)
            {
                staminaController = GetComponent<CampusCharacterStaminaController>();
            }

            if (staminaController == null)
            {
                staminaController = gameObject.AddComponent<CampusCharacterStaminaController>();
            }

            staminaController.EnsureSetup();
        }

        private float ResolveMoveSpeed(Vector2 input, bool wantsSprint)
        {
            EnsureStaminaController();
            return staminaController.ResolveMoveSpeed(input, wantsSprint, Time.deltaTime);
        }

        private void ConfigureBodyController(float moveSpeed)
        {
            EnsureBodyController();
            if (bodyController == null)
            {
                return;
            }

            bodyController.MoveSpeed = moveSpeed;
            bodyController.FloorIndex = FloorIndex;
            bodyController.EnsureSetup();
        }

        private void ConfigureBodyController()
        {
            ConfigureBodyController(MoveSpeed);
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
    }
}
