using System;
using UnityEditor;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [InitializeOnLoad]
    public static class CampusMapLightingBootstrap
    {
        private const string ProjectOpenSetupSessionKey = "NtingCampusMapEditor.ProjectOpenSetupRan";

        private static bool isEnsuringLighting;

        static CampusMapLightingBootstrap()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            QueueProjectOpenSetup();
        }

        [MenuItem("Tools/Nting Campus/Ensure Lighting For Current Map")]
        private static void EnsureLightingFromMenu()
        {
            EnsureLightingForCurrentMap();
        }

        private static void QueueProjectOpenSetup()
        {
            EditorApplication.delayCall -= TryRunProjectOpenSetup;
            EditorApplication.delayCall += TryRunProjectOpenSetup;
        }

        private static void TryRunProjectOpenSetup()
        {
            if (Application.isBatchMode || SessionState.GetBool(ProjectOpenSetupSessionKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueProjectOpenSetup();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SessionState.SetBool(ProjectOpenSetupSessionKey, true);
            try
            {
                CampusTestCharacterPrefabRepairer.EnsureInScene(false);
                EnsureLightingForCurrentMap();
                CampusMapEditorUtility.EnsureMapLightingFromMenu();
                Debug.Log("[NtingCampusMapEditor] Project open setup completed.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Project open setup failed: " + exception.Message);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                EnsureLightingForCurrentMap();
            }
        }

        internal static void EnsureLightingForCurrentMap()
        {
            EnsureMapLightAndMaxShadows();
        }

        private static void EnsureMapLightAndMaxShadows()
        {
            if (isEnsuringLighting)
            {
                return;
            }

            isEnsuringLighting = true;
            try
            {
                CampusMapEditorUtility.EnsureMapLightingFromMenu();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Ensure Map Light And Max Shadows failed: " + exception.Message);
            }
            finally
            {
                isEnsuringLighting = false;
            }
        }
    }
}
