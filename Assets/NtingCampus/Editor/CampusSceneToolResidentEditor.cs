using System.IO;
using NtingCampus.Gameplay.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NtingCampusMapEditor
{
    [CustomEditor(typeof(CampusSceneToolResident))]
    public sealed class CampusSceneToolResidentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Install / Refresh Stored Scene Tools"))
                {
                    CampusSceneToolResidentEditorUtility.RunStoredToolSet((CampusSceneToolResident)target, true);
                }

                if (GUILayout.Button("Create Test Player In Scene"))
                {
                    CampusSceneToolResidentEditorUtility.EnsureTestPlayerInScene(true);
                }
            }
        }
    }

    internal static class CampusSceneToolResidentEditorUtility
    {
        private const string DefaultScenePath = "Assets/Scenes/CampusMap.unity";
        private const string TestPlayerPrefabFolder = "Assets/NtingCampus/Prefabs/Player";

        [MenuItem("Tools/Nting Campus/Install Resident Scene Tools", false, 5)]
        public static void InstallResidentSceneToolsFromMenu()
        {
            CampusSceneToolResident resident = EnsureResidentComponent(true);
            RunStoredToolSet(resident, true);
        }

        [MenuItem("Tools/Nting Campus/Refresh Resident Scene Tools", false, 6)]
        public static void RefreshResidentSceneToolsFromMenu()
        {
            CampusSceneToolResident resident = EnsureResidentComponent(false);
            RunStoredToolSet(resident, true);
        }

        [MenuItem("Tools/Nting Campus/Bake Authoring Map Into CampusMap Scene", false, 7)]
        public static void BakeAuthoringMapIntoCampusMapScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Exit Play Mode before baking the authoring map into the scene.");
                return;
            }

            CampusRuntimeMapEditor.BakeRuntimeGeneratedContentIntoCampusMapScene();
        }

        public static CampusSceneToolResident EnsureResidentComponent(bool selectResident)
        {
            EnsureEditableSceneLoaded();

            CampusSceneToolResident resident = Object.FindFirstObjectByType<CampusSceneToolResident>(FindObjectsInactive.Include);
            if (resident == null)
            {
                CampusRuntimeMapEditor runtimeEditor = Object.FindFirstObjectByType<CampusRuntimeMapEditor>(FindObjectsInactive.Include);
                GameObject host = runtimeEditor != null ? runtimeEditor.gameObject : new GameObject(CampusSceneToolResident.ResidentHostName);
                if (runtimeEditor == null)
                {
                    Undo.RegisterCreatedObjectUndo(host, "Create Campus Resident Tool Host");
                    runtimeEditor = host.AddComponent<CampusRuntimeMapEditor>();
                    EditorUtility.SetDirty(runtimeEditor);
                }

                resident = host.GetComponent<CampusSceneToolResident>();
                if (resident == null)
                {
                    resident = Undo.AddComponent<CampusSceneToolResident>(host);
                }
            }

            resident.RefreshSceneReferences();
            EditorUtility.SetDirty(resident);

            if (selectResident)
            {
                Selection.activeObject = resident.gameObject;
            }

            return resident;
        }

        public static void RunStoredToolSet(CampusSceneToolResident resident, bool saveScene)
        {
            if (resident == null)
            {
                resident = EnsureResidentComponent(false);
            }

            if (resident == null)
            {
                return;
            }

            if (resident.GenerateDebugAssetsInEditor)
            {
                CampusMapEditorUtility.EnsureDebugAssets();
            }
            else
            {
                CampusMapEditorUtility.EnsureDirectories();
            }

            CampusMapRoot root = CampusMapEditorUtility.FindOrCreateCampusMapRoot();
            resident.EnsureRuntimeServices();
            CampusMapEditorUtility.EnsureMapLighting(root);

            CampusGameBootstrap bootstrap = CampusGameBootstrap.EnsureSceneBootstrap();
            if (bootstrap != null)
            {
                EditorUtility.SetDirty(bootstrap);
            }

            if (resident.FixValidationIssuesInEditor)
            {
                CampusMapEditorUtility.FixValidationIssues(
                    root,
                    CampusMapEditorUtility.LoadDefaultFloorPalette(),
                    CampusMapEditorUtility.LoadDefaultWallPalette(),
                    CampusMapEditorUtility.LoadDefaultPrefabPalette());
            }

            if (resident.RebuildWallVisualsInEditor)
            {
                CampusWallRenderProfile profile = CampusMapEditorUtility.LoadDefaultWallRenderProfile();
                if (root != null && profile != null)
                {
                    CampusMapEditorUtility.RebuildAllWallVisuals(root, profile);
                    CampusMapEditorUtility.ApplyWallDebugView(root, CampusWallDebugView.ShowFinalWallVisuals);
                }
            }

            if (resident.KeepTestPlayerInScene)
            {
                EnsureTestPlayerInScene(false);
            }

            resident.RefreshSceneReferences();
            EditorUtility.SetDirty(resident);
            MarkOpenSceneDirty();
            AssetDatabase.SaveAssets();

            if (saveScene)
            {
                Scene scene = resident.gameObject.scene;
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }

        public static GameObject EnsureTestPlayerInScene(bool selectPlayer)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return null;
            }

            string prefabPath = TestPlayerPrefabFolder + "/" + CampusObjectNames.TestPlayer + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Cannot create test player because the prefab is missing: " + prefabPath);
                return null;
            }

            GameObject instance = FindExistingScenePlayer(prefab);
            bool created = instance == null;
            if (created)
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    Undo.RegisterCreatedObjectUndo(instance, "Create Campus Test Player");
                }
            }

            if (instance == null)
            {
                return null;
            }

            bool changed = created;
            if (instance.name != CampusObjectNames.TestPlayer)
            {
                Undo.RecordObject(instance, "Rename Campus Test Player");
                instance.name = CampusObjectNames.TestPlayer;
                changed = true;
            }

            CampusFloorRoot floor = Object.FindFirstObjectByType<CampusFloorRoot>(FindObjectsInactive.Exclude);
            if (floor != null && floor.Grid != null)
            {
                if (created)
                {
                    instance.transform.position = floor.Grid.GetCellCenterWorld(Vector3Int.zero);
                }

                CampusTestPlayerController controller = instance.GetComponent<CampusTestPlayerController>();
                if (controller != null && controller.FloorIndex != floor.FloorIndex)
                {
                    Undo.RecordObject(controller, "Set Campus Test Player Floor");
                    controller.FloorIndex = floor.FloorIndex;
                    EditorUtility.SetDirty(controller);
                    changed = true;
                }
            }

            if (selectPlayer)
            {
                Selection.activeGameObject = instance;
            }

            if (changed)
            {
                EditorUtility.SetDirty(instance);
                CampusMapEditorUtility.MarkSceneDirty();
            }

            return instance;
        }

        private static GameObject FindExistingScenePlayer(GameObject prefab)
        {
            CampusTestPlayerController[] controllers =
                Object.FindObjectsByType<CampusTestPlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject fallback = null;
            for (int i = 0; i < controllers.Length; i++)
            {
                CampusTestPlayerController controller = controllers[i];
                if (controller == null || controller.gameObject == null || !controller.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (CampusObjectNames.MatchesAny(controller.gameObject.name, CampusObjectNames.TestPlayer, CampusObjectNames.LegacyTestPlayer) ||
                    PrefabUtility.GetCorrespondingObjectFromSource(controller.gameObject) == prefab)
                {
                    return controller.gameObject;
                }

                fallback ??= controller.gameObject;
            }

            return fallback;
        }

        private static void EnsureEditableSceneLoaded()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path))
            {
                return;
            }

            if (File.Exists(DefaultScenePath))
            {
                EditorSceneManager.OpenScene(DefaultScenePath, OpenSceneMode.Single);
            }
        }

        private static void MarkOpenSceneDirty()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                EditorUtility.SetDirty(roots[i]);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
