using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NtingCampusMapEditor
{
    internal static class CampusTestCharacterPrefabRepairer
    {
        private const string CharacterSpritePath = "Assets/NtingCampus/Tiles/Source/Characters/Campus_TestPlayer_32.png";
        private const string CharacterPrefabFolder = "Assets/NtingCampus/Prefabs/Player";
        private const string CharacterPrefabPath = CharacterPrefabFolder + "/" + CampusObjectNames.TestPlayer + ".prefab";

        [MenuItem("Tools/Nting Campus/Repair Test Player Prefab")]
        private static void RepairFromMenu()
        {
            Repair(true);
        }

        [MenuItem("Tools/Nting Campus/Create Test Player In Scene")]
        private static void CreateInScene()
        {
            EnsureInScene(true);
        }

        public static GameObject EnsureInScene(bool selectPlayer)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return null;
            }

            Repair(false);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Cannot create test player because the prefab is missing.");
                return null;
            }

            GameObject instance = FindExistingScenePlayer(prefab);
            bool created = instance == null;
            if (created)
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            }

            if (instance == null)
            {
                return null;
            }

            bool changed = created;
            if (created)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Create Campus Test Player");
            }
            else if (instance.name != CampusObjectNames.TestPlayer)
            {
                Undo.RecordObject(instance, "Rename Campus Test Player");
                instance.name = CampusObjectNames.TestPlayer;
                EditorUtility.SetDirty(instance);
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
                CampusMapEditorUtility.MarkSceneDirty();
            }

            return instance;
        }

        private static GameObject FindExistingScenePlayer(GameObject prefab)
        {
            CampusTestPlayerController[] controllers = Object.FindObjectsByType<CampusTestPlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject fallback = null;
            for (int i = 0; i < controllers.Length; i++)
            {
                CampusTestPlayerController controller = controllers[i];
                if (controller == null || !IsSceneObject(controller.gameObject))
                {
                    continue;
                }

                if (CampusObjectNames.MatchesAny(controller.gameObject.name, CampusObjectNames.TestPlayer, CampusObjectNames.LegacyTestPlayer) ||
                    IsPrefabInstanceOf(controller.gameObject, prefab))
                {
                    return controller.gameObject;
                }

                if (fallback == null)
                {
                    fallback = controller.gameObject;
                }
            }

            return fallback;
        }

        private static bool IsSceneObject(GameObject target)
        {
            return target != null && target.scene.IsValid();
        }

        private static bool IsPrefabInstanceOf(GameObject target, GameObject prefab)
        {
            if (target == null || prefab == null)
            {
                return false;
            }

            return PrefabUtility.GetCorrespondingObjectFromSource(target) == prefab;
        }

        private static void Repair(bool logResult)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsurePrefabFolder();
            Sprite sprite = LoadSprite(CharacterSpritePath);
            if (sprite == null)
            {
                if (logResult)
                {
                    Debug.LogWarning("[NtingCampusMapEditor] Test player prefab repair skipped because the character sprite is missing.");
                }

                return;
            }

            bool loadedExistingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath) != null;
            GameObject root = loadedExistingPrefab
                ? PrefabUtility.LoadPrefabContents(CharacterPrefabPath)
                : new GameObject(CampusObjectNames.TestPlayer);

            ConfigurePrefab(root, sprite);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefabPath);

            if (loadedExistingPrefab)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            if (logResult && savedPrefab != null)
            {
                Debug.Log("[NtingCampusMapEditor] Repaired test player prefab.");
            }
        }

        private static void ConfigurePrefab(GameObject root, Sprite sprite)
        {
            root.name = CampusObjectNames.TestPlayer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            EnsurePlayerTag();
            TrySetTag(root, "Player");

            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
            renderer.sprite = sprite;
            renderer.sortingOrder = CampusRenderSortingUtility.SharedWallObjectOffset;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            SortingGroup sortingGroup = GetOrAdd<SortingGroup>(root);
            sortingGroup.sortingOrder = 1000 + CampusRenderSortingUtility.SharedWallObjectOffset;

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CapsuleCollider2D collider = GetOrAdd<CapsuleCollider2D>(root);
            collider.isTrigger = false;
            collider.direction = CapsuleDirection2D.Vertical;
            collider.offset = new Vector2(0f, -0.08f);
            collider.size = new Vector2(0.45f, 0.72f);

            CampusTestPlayerController controller = GetOrAdd<CampusTestPlayerController>(root);
            controller.MoveSpeed = 3.5f;
            controller.FloorIndex = 1;
            controller.InteractKey = KeyCode.E;
            controller.InteractionForwardOffset = 0.45f;
            controller.InteractionRadius = 0.55f;
            controller.InteractionMask = Physics2D.AllLayers;

            CampusInteractionController interactionController = GetOrAdd<CampusInteractionController>(root);
            interactionController.InteractKey = controller.InteractKey;
            interactionController.PollInput = false;
            interactionController.RefreshEveryFrame = false;
            interactionController.AutoCreatePromptView = true;

            CampusInteractionSensor sensor = interactionController.GetOrCreateSensor();
            sensor.ScanOrigin = root.transform;
            sensor.ForwardOffset = controller.InteractionForwardOffset;
            sensor.Radius = controller.InteractionRadius;
            sensor.InteractionMask = controller.InteractionMask;
        }

        private static Sprite LoadSprite(string path)
        {
            EnsureSpriteImporter(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureSpriteImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, 32f) ||
                           importer.mipmapEnabled ||
                           importer.filterMode != FilterMode.Point ||
                           importer.wrapMode != TextureWrapMode.Clamp ||
                           !importer.alphaIsTransparency ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Center || settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                changed = true;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/NtingCampus/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/NtingCampus", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder(CharacterPrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/NtingCampus/Prefabs", "Player");
            }
        }

        private static void EnsurePlayerTag()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == "Player")
                {
                    return;
                }
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "Player";
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static void TrySetTag(GameObject target, string tag)
        {
            try
            {
                target.tag = tag;
            }
            catch (UnityException)
            {
                target.tag = "Untagged";
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
