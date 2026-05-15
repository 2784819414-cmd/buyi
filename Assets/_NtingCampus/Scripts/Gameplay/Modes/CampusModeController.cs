using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Modes
{
    [DisallowMultipleComponent]
    public sealed class CampusModeController : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusTestPlayerController playerController;
        [SerializeField] private CampusGodViewCameraController cameraController;
        [SerializeField] private KeyCode toggleModeKey = KeyCode.Tab;
        [SerializeField] private KeyCode pauseKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode normalSpeedKey = KeyCode.Alpha2;
        [SerializeField] private KeyCode fastSpeedKey = KeyCode.Alpha3;
        [SerializeField] private KeyCode maxSpeedKey = KeyCode.Alpha4;
        [SerializeField, Min(2f)] private float maxDebugTimeScale = 200f;

        private bool isInitialized;
        private bool mapEditorForcedGodView;

        public CampusGameMode CurrentMode =>
            bootstrap != null && bootstrap.GameState != null
                ? bootstrap.GameState.CurrentMode
                : CampusGameMode.StudentBody;

        public void InitializeModes(CampusGameBootstrap targetBootstrap, bool writeInitialLog)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            ResolveReferences();
            ApplyMode(CurrentMode, true, writeInitialLog);
            isInitialized = true;
        }

        public void ToggleMode()
        {
            SetMode(CurrentMode == CampusGameMode.StudentBody ? CampusGameMode.GodView : CampusGameMode.StudentBody, true);
        }

        public void SetMode(CampusGameMode mode, bool writeLog)
        {
            ResolveReferences();
            ApplyMode(mode, false, writeLog);
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (!isInitialized)
            {
                InitializeModes(bootstrap != null ? bootstrap : CampusGameBootstrap.Instance, false);
            }
        }

        private void Update()
        {
            ResolveReferences();
            if (!Application.isPlaying || bootstrap == null || bootstrap.TimeController == null)
            {
                return;
            }

            SyncModeWithRuntimeMapEditor();

            if (!IsRuntimeMapEditorOpen() && CampusInteractionInput.WasKeyPressed(toggleModeKey))
            {
                ToggleMode();
            }

            if (CampusInteractionInput.WasKeyPressed(pauseKey))
            {
                bootstrap.TimeController.SetSpeedMode(CampusTimeSpeedMode.Paused);
            }

            if (CampusInteractionInput.WasKeyPressed(normalSpeedKey))
            {
                bootstrap.TimeController.SetSpeedMode(CampusTimeSpeedMode.Normal);
            }

            if (CampusInteractionInput.WasKeyPressed(fastSpeedKey))
            {
                bootstrap.TimeController.SetSpeedMode(CampusTimeSpeedMode.Fast);
            }

            if (CampusInteractionInput.WasKeyPressed(maxSpeedKey))
            {
                bootstrap.TimeController.SetCustomTimeScale(maxDebugTimeScale);
            }
        }

        private void SyncModeWithRuntimeMapEditor()
        {
            bool isRuntimeMapEditorOpen = IsRuntimeMapEditorOpen();
            if (isRuntimeMapEditorOpen)
            {
                if (CurrentMode != CampusGameMode.GodView)
                {
                    ApplyMode(CampusGameMode.GodView, false, false);
                }

                mapEditorForcedGodView = true;
                return;
            }

            if (!mapEditorForcedGodView)
            {
                return;
            }

            mapEditorForcedGodView = false;
            if (CurrentMode != CampusGameMode.StudentBody)
            {
                ApplyMode(CampusGameMode.StudentBody, false, false);
            }
        }

        private static bool IsRuntimeMapEditorOpen()
        {
            CampusRuntimeMapEditor runtimeMapEditor = CampusRuntimeMapEditor.Instance;
            return runtimeMapEditor != null && runtimeMapEditor.IsOpen;
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
            {
                bootstrap = GetComponent<CampusGameBootstrap>();
                if (bootstrap == null)
                {
                    bootstrap = CampusGameBootstrap.Instance;
                }
            }

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<CampusTestPlayerController>(FindObjectsInactive.Include);
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CampusGodViewCameraController>(FindObjectsInactive.Include);
                if (cameraController == null)
                {
                    GameObject host = Camera.main != null ? Camera.main.gameObject : gameObject;
                    cameraController = host.GetComponent<CampusGodViewCameraController>();
                    if (cameraController == null)
                    {
                        cameraController = host.AddComponent<CampusGodViewCameraController>();
                    }
                }
            }
        }

        private void ApplyMode(CampusGameMode mode, bool force, bool writeLog)
        {
            if (bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            CampusGameMode previousMode = bootstrap.GameState.CurrentMode;
            if (!force && previousMode == mode)
            {
                return;
            }

            bootstrap.GameState.SetMode(mode);

            bool enablePlayerGameplayInput = mode == CampusGameMode.StudentBody;
            if (playerController != null)
            {
                playerController.SetGameplayInputEnabled(enablePlayerGameplayInput);
            }

            if (cameraController != null)
            {
                cameraController.SetGameMode(mode, playerController != null ? playerController.transform : null);
            }

            if (writeLog && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(mode == CampusGameMode.StudentBody
                    ? "[System] Switched to student body mode."
                    : "[System] Switched to god view mode.");
            }
        }
    }
}
