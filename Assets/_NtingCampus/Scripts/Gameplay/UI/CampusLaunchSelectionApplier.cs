using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class CampusLaunchSelectionApplier : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;

        private bool hasAppliedSelection;
        private bool pauseCaptured;
        private CampusGameplayPauseState pauseState;
        private CampusRuntimeGameplayOverlayLoader overlayLoader;

        private void Awake()
        {
            bootstrap = bootstrap != null ? bootstrap : GetComponent<CampusGameBootstrap>();
            overlayLoader = GetComponent<CampusRuntimeGameplayOverlayLoader>();
            if (!CampusLaunchConfigStore.HasPendingSelection)
            {
                return;
            }

            pauseState = CampusGameplayPauseUtility.Pause(bootstrap);
            pauseCaptured = true;
        }

        private void Start()
        {
            if (!CampusLaunchConfigStore.HasPendingSelection || hasAppliedSelection)
            {
                return;
            }

            StartCoroutine(ApplyPendingSelectionCoroutine());
        }

        private IEnumerator ApplyPendingSelectionCoroutine()
        {
            hasAppliedSelection = true;

            while (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
                if (bootstrap == null)
                {
                    yield return null;
                    continue;
                }
            }

            while (overlayLoader == null)
            {
                overlayLoader = bootstrap != null
                    ? bootstrap.GetComponent<CampusRuntimeGameplayOverlayLoader>()
                    : GetComponent<CampusRuntimeGameplayOverlayLoader>();
                if (overlayLoader == null && bootstrap != null)
                {
                    overlayLoader = bootstrap.gameObject.AddComponent<CampusRuntimeGameplayOverlayLoader>();
                }

                if (overlayLoader == null)
                {
                    yield return null;
                }
            }

            CampusRuntimeMapEditor runtimeMapEditor = null;
            while (runtimeMapEditor == null)
            {
                runtimeMapEditor = CampusRuntimeMapEditor.Instance;
                if (runtimeMapEditor == null)
                {
                    runtimeMapEditor = FindFirstObjectByType<CampusRuntimeMapEditor>(FindObjectsInactive.Include);
                }

                if (runtimeMapEditor == null)
                {
                    yield return null;
                }
            }

            try
            {
                if (CampusLaunchConfigStore.StartWithBlankMap)
                {
                    runtimeMapEditor.CreateBlankMap(false);
                    if (!string.IsNullOrWhiteSpace(CampusLaunchConfigStore.SelectedMapPath))
                    {
                        runtimeMapEditor.SaveNamedMap(
                            CampusLaunchConfigStore.SelectedMapPath,
                            CampusRuntimeMapLoadSource.AuthoringPackage,
                            false);
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(CampusLaunchConfigStore.SelectedMapPath))
                    {
                        LoadSnapshotFromPath(runtimeMapEditor, CampusLaunchConfigStore.SelectedMapPath, CampusLaunchConfigStore.SelectedMapSource);
                    }

                    if (!string.IsNullOrWhiteSpace(CampusLaunchConfigStore.SelectedSavePath))
                    {
                        LoadSnapshotFromPath(runtimeMapEditor, CampusLaunchConfigStore.SelectedSavePath, CampusLaunchConfigStore.SelectedSaveSource);
                    }
                }

                overlayLoader.ApplyLaunchSelection(
                    CampusLaunchConfigStore.SelectedMapPath,
                    CampusLaunchConfigStore.SelectedMapSource);
                RefreshGameplayBindings();
                WriteLaunchLog();
            }
            catch (Exception exception)
            {
                if (bootstrap != null && bootstrap.EventLog != null)
                {
                    bootstrap.EventLog.AddLog("[System] Startup load failed: " + exception.Message);
                }

                Debug.LogWarning("[CampusLaunchSelectionApplier] " + exception);
            }
            finally
            {
                CampusLaunchConfigStore.Clear();
                if (pauseCaptured)
                {
                    CampusGameplayPauseUtility.Resume(bootstrap, pauseState);
                    pauseCaptured = false;
                }
            }
        }

        private void LoadSnapshotFromPath(CampusRuntimeMapEditor runtimeMapEditor, string path, CampusRuntimeMapLoadSource source)
        {
            if (runtimeMapEditor == null)
            {
                throw new InvalidOperationException("CampusRuntimeMapEditor was not found.");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Snapshot file was not found.", path);
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            FieldInfo suppressField = runtimeMapEditor.GetType().GetField(
                "suppressPlayerSaveScheduling",
                BindingFlags.Instance | BindingFlags.NonPublic);
            bool previousSuppress = suppressField != null && (bool)suppressField.GetValue(runtimeMapEditor);

            try
            {
                suppressField?.SetValue(runtimeMapEditor, true);
                InvokePrivate(runtimeMapEditor, "LoadRuntimeResources");
                InvokePrivate(runtimeMapEditor, "LoadSnapshotJson", json);
                InvokePrivate(runtimeMapEditor, "RememberMapLoadSource", source, path);
            }
            finally
            {
                suppressField?.SetValue(runtimeMapEditor, previousSuppress);
            }
        }

        private void RefreshGameplayBindings()
        {
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.WorldService?.Initialize(bootstrap);
            bootstrap.RosterService?.RebuildRosterFromScene();
            bootstrap.ModeController?.InitializeModes(bootstrap, false);
            bootstrap.ScheduleService?.Initialize(bootstrap);
            bootstrap.GameplayEventHub?.Initialize(bootstrap);
            bootstrap.ClassroomLoopService?.Initialize(bootstrap);
            bootstrap.SanctionService?.Initialize(bootstrap);
            bootstrap.PrankService?.Initialize(bootstrap);
        }

        private void WriteLaunchLog()
        {
            if (bootstrap == null || bootstrap.EventLog == null)
            {
                return;
            }

            string mapLabel = string.IsNullOrWhiteSpace(CampusLaunchConfigStore.SelectedMapPath)
                ? "Scene Default"
                : Path.GetFileName(CampusLaunchConfigStore.SelectedMapPath);
            string saveLabel = string.IsNullOrWhiteSpace(CampusLaunchConfigStore.SelectedSavePath)
                ? "No Save"
                : Path.GetFileName(CampusLaunchConfigStore.SelectedSavePath);

            bootstrap.EventLog.AddLog("[System] Startup selection applied. Map=" + mapLabel + ", Save=" + saveLabel + ".");
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().Name, methodName);
            }

            method.Invoke(target, arguments);
        }
    }
}
