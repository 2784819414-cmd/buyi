using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Skeleton;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public sealed class CampusSceneToolResident : MonoBehaviour
    {
        public const string ResidentHostName = "Campus Runtime Map Editor";

        [SerializeField] private bool ensureOnEnable = true;
        [SerializeField] private bool ensureOnStart = true;
        [SerializeField] private bool keepRuntimeMapEditor = true;
        [SerializeField] private bool keepLighting = true;
        [SerializeField] private bool keepCustomShadowSystem = true;
        [SerializeField] private bool keepGameplayBootstrap = true;
        [SerializeField] private bool keepMischiefSkeleton = true;
        [SerializeField] private bool keepTestPlayerInScene = true;
        [SerializeField] private bool generateDebugAssetsInEditor = true;
        [SerializeField] private bool fixValidationIssuesInEditor = true;
        [SerializeField] private bool rebuildWallVisualsInEditor = true;
        [SerializeField] private CampusMapRoot mapRoot;
        [SerializeField] private CampusRuntimeMapEditor runtimeMapEditor;

        private bool isEnsuring;

        public bool KeepTestPlayerInScene => keepTestPlayerInScene;
        public bool GenerateDebugAssetsInEditor => generateDebugAssetsInEditor;
        public bool FixValidationIssuesInEditor => fixValidationIssuesInEditor;
        public bool RebuildWallVisualsInEditor => rebuildWallVisualsInEditor;
        public CampusMapRoot MapRoot => mapRoot;
        public CampusRuntimeMapEditor RuntimeMapEditor => runtimeMapEditor;

        private void OnEnable()
        {
            if (Application.isPlaying && ensureOnEnable)
            {
                EnsureRuntimeServices();
            }
        }

        private void Start()
        {
            if (ensureOnStart)
            {
                EnsureRuntimeServices();
            }
        }

        [ContextMenu("Ensure Runtime Services")]
        public void EnsureRuntimeServices()
        {
            if (isEnsuring)
            {
                return;
            }

            isEnsuring = true;
            try
            {
                RefreshSceneReferences();

                if (keepRuntimeMapEditor)
                {
                    EnsureRuntimeMapEditor();
                }

                if (keepLighting)
                {
                    CampusDayNightController.EnsureSceneController(mapRoot);
                }

                if (keepCustomShadowSystem)
                {
                    NtingCustomShadowSystem.EnsureSceneSystem();
                }

                CampusGameBootstrap bootstrap = null;
                if (keepGameplayBootstrap)
                {
                    bootstrap = CampusGameBootstrap.EnsureSceneBootstrap();
                }

                if (keepMischiefSkeleton)
                {
                    EnsureMischiefSkeleton(bootstrap);
                }

                RefreshSceneReferences();
            }
            finally
            {
                isEnsuring = false;
            }
        }

        public void RefreshSceneReferences()
        {
            mapRoot = FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            runtimeMapEditor = FindFirstObjectByType<CampusRuntimeMapEditor>(FindObjectsInactive.Include);

            if (mapRoot != null)
            {
                mapRoot.RebuildFloorReferences();
            }
        }

        private CampusRuntimeMapEditor EnsureRuntimeMapEditor()
        {
            if (runtimeMapEditor != null)
            {
                return runtimeMapEditor;
            }

            runtimeMapEditor = GetComponent<CampusRuntimeMapEditor>();
            if (runtimeMapEditor != null)
            {
                return runtimeMapEditor;
            }

            runtimeMapEditor = FindFirstObjectByType<CampusRuntimeMapEditor>(FindObjectsInactive.Include);
            if (runtimeMapEditor != null)
            {
                return runtimeMapEditor;
            }

            GameObject host = gameObject != null && gameObject.name == ResidentHostName
                ? gameObject
                : new GameObject(ResidentHostName);
            runtimeMapEditor = host.AddComponent<CampusRuntimeMapEditor>();
            return runtimeMapEditor;
        }

        private static void EnsureMischiefSkeleton(CampusGameBootstrap bootstrap)
        {
            if (bootstrap == null || HasSceneMischiefSkeleton())
            {
                return;
            }

            CampusMischiefAnchorBootstrap.RebuildSkeleton(bootstrap);
        }

        private static bool HasSceneMischiefSkeleton()
        {
            if (FindFirstObjectByType<CampusMischiefAnchorBootstrap>(FindObjectsInactive.Include) != null)
            {
                return true;
            }

            GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < sceneObjects.Length; i++)
            {
                GameObject sceneObject = sceneObjects[i];
                if (sceneObject == null || !sceneObject.scene.IsValid() || !sceneObject.scene.isLoaded)
                {
                    continue;
                }

                if (sceneObject.name == CampusMischiefAnchorBootstrap.RootName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
