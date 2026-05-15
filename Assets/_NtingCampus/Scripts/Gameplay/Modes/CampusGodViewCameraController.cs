using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NtingCampus.Gameplay.Modes
{
    [DisallowMultipleComponent]
    public sealed class CampusGodViewCameraController : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform studentTarget;
        [SerializeField] private Vector3 studentViewOffset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float studentFollowLerp = 14f;
        [SerializeField, Min(0.1f)] private float godViewMoveSpeed = 14f;
        [SerializeField, Min(0.01f)] private float godViewZoomSpeed = 5f;
        [SerializeField, Min(0.1f)] private float minOrthographicSize = 3f;
        [SerializeField, Min(0.1f)] private float maxOrthographicSize = 80f;
        [SerializeField, Min(0.1f)] private float studentModeOrthographicSize = 2f;
        [SerializeField, Min(0.1f)] private float godViewOrthographicSize = 13.23f;

        private CampusGameMode currentMode = CampusGameMode.StudentBody;

        public void SetGameMode(CampusGameMode mode, Transform playerTarget)
        {
            CampusGameMode previousMode = currentMode;
            currentMode = mode;
            if (playerTarget != null)
            {
                studentTarget = playerTarget;
            }

            ResolveCamera();
            ApplyModeCameraState(previousMode, currentMode);
            if (currentMode == CampusGameMode.StudentBody)
            {
                SnapToStudentTarget();
            }
        }

        private void Awake()
        {
            ResolveCamera();
            ApplyCurrentModeCameraState();
            SnapToStudentTarget();
        }

        private void LateUpdate()
        {
            ResolveCamera();
            if (controlledCamera == null)
            {
                return;
            }

            if (currentMode == CampusGameMode.GodView)
            {
                UpdateGodViewCamera();
                return;
            }

            UpdateStudentCamera();
        }

        private void ResolveCamera()
        {
            if (controlledCamera != null)
            {
                return;
            }

            controlledCamera = Camera.main;
            if (controlledCamera == null)
            {
                controlledCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            }
        }

        private void SnapToStudentTarget()
        {
            if (controlledCamera == null || studentTarget == null)
            {
                return;
            }

            controlledCamera.transform.position = ResolveStudentCameraPosition();
        }

        private void UpdateStudentCamera()
        {
            if (studentTarget == null || IsBuildModeOpen())
            {
                return;
            }

            Vector3 targetPosition = ResolveStudentCameraPosition();
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, studentFollowLerp) * Time.unscaledDeltaTime);
            controlledCamera.transform.position = Vector3.Lerp(controlledCamera.transform.position, targetPosition, t);
        }

        private Vector3 ResolveStudentCameraPosition()
        {
            Vector3 targetPosition = studentTarget.position + studentViewOffset;
            targetPosition.z = studentViewOffset.z;
            return targetPosition;
        }

        private void UpdateGodViewCamera()
        {
            Vector2 moveInput = ReadMoveInput();
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                float moveScale = godViewMoveSpeed * Mathf.Max(0.1f, ResolveCameraZoomFactor());
                Vector3 delta = new Vector3(moveInput.x, moveInput.y, 0f) * (moveScale * Time.unscaledDeltaTime);
                controlledCamera.transform.position += delta;
            }

            float scroll = ReadScrollDelta();
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                ApplyZoom(scroll);
            }
        }

        private float ResolveCameraZoomFactor()
        {
            if (controlledCamera == null || !controlledCamera.orthographic)
            {
                return 1f;
            }

            return controlledCamera.orthographicSize / Mathf.Max(1f, minOrthographicSize);
        }

        private void ApplyZoom(float scrollDelta)
        {
            if (controlledCamera == null)
            {
                return;
            }

            if (controlledCamera.orthographic)
            {
                float nextSize = controlledCamera.orthographicSize - scrollDelta * godViewZoomSpeed;
                controlledCamera.orthographicSize = Mathf.Clamp(nextSize, minOrthographicSize, maxOrthographicSize);
                return;
            }

            float nextFov = controlledCamera.fieldOfView - scrollDelta * godViewZoomSpeed;
            controlledCamera.fieldOfView = Mathf.Clamp(nextFov, 20f, 90f);
        }

        private void ApplyModeCameraState(CampusGameMode previousMode, CampusGameMode nextMode)
        {
            if (controlledCamera == null)
            {
                return;
            }

            if (controlledCamera.orthographic)
            {
                if (nextMode == CampusGameMode.GodView)
                {
                    controlledCamera.orthographicSize = Mathf.Clamp(godViewOrthographicSize, minOrthographicSize, maxOrthographicSize);
                    return;
                }

                if (previousMode == CampusGameMode.GodView || nextMode == CampusGameMode.StudentBody)
                {
                    controlledCamera.orthographicSize = Mathf.Clamp(studentModeOrthographicSize, minOrthographicSize, maxOrthographicSize);
                }
            }
        }

        private void ApplyCurrentModeCameraState()
        {
            if (controlledCamera == null || !controlledCamera.orthographic)
            {
                return;
            }

            controlledCamera.orthographicSize = currentMode == CampusGameMode.GodView
                ? Mathf.Clamp(godViewOrthographicSize, minOrthographicSize, maxOrthographicSize)
                : Mathf.Clamp(studentModeOrthographicSize, minOrthographicSize, maxOrthographicSize);
        }

        private static bool IsBuildModeOpen()
        {
            CampusRuntimeMapEditor runtimeMapEditor = CampusRuntimeMapEditor.Instance;
            return runtimeMapEditor != null && runtimeMapEditor.IsOpen;
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
#else
            return Vector2.zero;
#endif
        }

        private static float ReadScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.scroll.ReadValue().y / 120f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

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
