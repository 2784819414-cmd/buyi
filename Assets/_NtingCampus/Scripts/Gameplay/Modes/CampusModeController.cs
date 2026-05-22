using System.Reflection;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Modes
{
    [DisallowMultipleComponent]
    public sealed class CampusModeController : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusCharacterRuntime playerRuntime;
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
                bootstrap.TimeController.TogglePauseTime(true);
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

            if (bootstrap != null && bootstrap.RosterService != null && bootstrap.RosterService.PlayerRuntime != null)
            {
                playerRuntime = bootstrap.RosterService.PlayerRuntime;
            }

            if (playerRuntime == null)
            {
                CampusPlayerCharacter playerCharacter = CampusPlayerCharacter.FindCurrent();
                if (playerCharacter != null)
                {
                    playerRuntime = playerCharacter.CharacterRuntime;
                }
            }

            if (playerRuntime == null)
            {
                CampusCharacterRuntime[] runtimes =
                    FindObjectsByType<CampusCharacterRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < runtimes.Length; i++)
                {
                    CampusCharacterRuntime runtime = runtimes[i];
                    if (runtime != null && runtime.Data != null && runtime.Data.IsPlayerControlled)
                    {
                        playerRuntime = runtime;
                        break;
                    }
                }
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

            if (cameraController != null && CurrentMode == CampusGameMode.StudentBody && playerRuntime != null)
            {
                cameraController.SetGameMode(CampusGameMode.StudentBody, playerRuntime.transform);
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
            ApplyPlayerGameplayInput(enablePlayerGameplayInput);

            if (cameraController != null)
            {
                cameraController.SetGameMode(mode, playerRuntime != null ? playerRuntime.transform : null);
            }

            if (writeLog && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(mode == CampusGameMode.StudentBody
                    ? CampusCoreTextCatalog.Get(CampusCoreTextId.SwitchedStudentBodyMode)
                    : CampusCoreTextCatalog.Get(CampusCoreTextId.SwitchedGodViewMode));
            }
        }

        private void ApplyPlayerGameplayInput(bool enabled)
        {
            if (playerRuntime == null)
            {
                return;
            }

            MonoBehaviour[] components = playerRuntime.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                {
                    continue;
                }

                MethodInfo method = component.GetType().GetMethod(
                    "SetGameplayInputEnabled",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(bool) },
                    null);
                if (method == null)
                {
                    continue;
                }

                method.Invoke(component, new object[] { enabled });
            }
        }
    }
}
