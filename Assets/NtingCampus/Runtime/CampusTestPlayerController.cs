using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
            RefreshSorting();
        }

        private void Update()
        {
            moveInput = ReadMoveInput();
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                facingDirection = moveInput.normalized;
            }

            if (WasInteractPressed())
            {
                TryInteract();
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

        private bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (TryReadInteractPressedFromInputSystem(InteractKey, out bool inputSystemPressed))
            {
                return inputSystemPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(InteractKey);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryReadInteractPressedFromInputSystem(KeyCode keyCode, out bool pressed)
        {
            pressed = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            KeyControl key = GetKeyboardKey(keyboard, keyCode);
            if (key == null)
            {
                return false;
            }

            pressed = key.wasPressedThisFrame;
            return true;
        }

        private static KeyControl GetKeyboardKey(Keyboard keyboard, KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.B: return keyboard.bKey;
                case KeyCode.C: return keyboard.cKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.F: return keyboard.fKey;
                case KeyCode.G: return keyboard.gKey;
                case KeyCode.H: return keyboard.hKey;
                case KeyCode.I: return keyboard.iKey;
                case KeyCode.J: return keyboard.jKey;
                case KeyCode.K: return keyboard.kKey;
                case KeyCode.L: return keyboard.lKey;
                case KeyCode.M: return keyboard.mKey;
                case KeyCode.N: return keyboard.nKey;
                case KeyCode.O: return keyboard.oKey;
                case KeyCode.P: return keyboard.pKey;
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.T: return keyboard.tKey;
                case KeyCode.U: return keyboard.uKey;
                case KeyCode.V: return keyboard.vKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.X: return keyboard.xKey;
                case KeyCode.Y: return keyboard.yKey;
                case KeyCode.Z: return keyboard.zKey;
                case KeyCode.Alpha0: return keyboard.digit0Key;
                case KeyCode.Alpha1: return keyboard.digit1Key;
                case KeyCode.Alpha2: return keyboard.digit2Key;
                case KeyCode.Alpha3: return keyboard.digit3Key;
                case KeyCode.Alpha4: return keyboard.digit4Key;
                case KeyCode.Alpha5: return keyboard.digit5Key;
                case KeyCode.Alpha6: return keyboard.digit6Key;
                case KeyCode.Alpha7: return keyboard.digit7Key;
                case KeyCode.Alpha8: return keyboard.digit8Key;
                case KeyCode.Alpha9: return keyboard.digit9Key;
                case KeyCode.Keypad0: return keyboard.numpad0Key;
                case KeyCode.Keypad1: return keyboard.numpad1Key;
                case KeyCode.Keypad2: return keyboard.numpad2Key;
                case KeyCode.Keypad3: return keyboard.numpad3Key;
                case KeyCode.Keypad4: return keyboard.numpad4Key;
                case KeyCode.Keypad5: return keyboard.numpad5Key;
                case KeyCode.Keypad6: return keyboard.numpad6Key;
                case KeyCode.Keypad7: return keyboard.numpad7Key;
                case KeyCode.Keypad8: return keyboard.numpad8Key;
                case KeyCode.Keypad9: return keyboard.numpad9Key;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                case KeyCode.UpArrow: return keyboard.upArrowKey;
                case KeyCode.DownArrow: return keyboard.downArrowKey;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.Return: return keyboard.enterKey;
                case KeyCode.KeypadEnter: return keyboard.numpadEnterKey;
                case KeyCode.Escape: return keyboard.escapeKey;
                case KeyCode.Tab: return keyboard.tabKey;
                case KeyCode.Backspace: return keyboard.backspaceKey;
                case KeyCode.Delete: return keyboard.deleteKey;
                case KeyCode.LeftShift: return keyboard.leftShiftKey;
                case KeyCode.RightShift: return keyboard.rightShiftKey;
                case KeyCode.LeftControl: return keyboard.leftCtrlKey;
                case KeyCode.RightControl: return keyboard.rightCtrlKey;
                case KeyCode.LeftAlt: return keyboard.leftAltKey;
                case KeyCode.RightAlt: return keyboard.rightAltKey;
                default: return null;
            }
        }
#endif

        private void TryInteract()
        {
            Vector2 origin = body != null ? body.position : (Vector2)transform.position;
            Vector2 center = origin + facingDirection.normalized * InteractionForwardOffset;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, InteractionRadius, InteractionMask);
            ICampusInteractable best = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!TryGetInteractable(hit, out ICampusInteractable interactable))
                {
                    continue;
                }

                float distance = Vector2.Distance(origin, hit.ClosestPoint(origin));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = interactable;
                }
            }

            if (best != null)
            {
                best.Interact(gameObject);
            }
        }

        private static bool TryGetInteractable(Collider2D hit, out ICampusInteractable interactable)
        {
            RestroomStallDoor door = hit.GetComponentInParent<RestroomStallDoor>();
            if (door != null)
            {
                interactable = door;
                return true;
            }

            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICampusInteractable candidate)
                {
                    interactable = candidate;
                    return true;
                }
            }

            interactable = null;
            return false;
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
            sortingGroup.sortingOrder = Mathf.Max(1, FloorIndex) * step + CampusRenderSortingUtility.SharedWallObjectOffset;
            sortingGroup.sortingLayerID = SortingLayer.NameToID("Default");
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 direction = facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector2.down;
            Vector2 center = (Vector2)transform.position + direction * InteractionForwardOffset;
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);
            Gizmos.DrawSphere(center, InteractionRadius);
        }
    }
}
