using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NtingCampus.InventoryGrid.Editor
{
    public static class InventoryGridDemoSceneBuilder
    {
        private const string ScenePath = "Assets/_NtingCampus/Scenes/InventoryGridDemo.unity";
        private const string ItemFolder = "Assets/_NtingCampus/ScriptableObjects/InventoryItems";
        private const string ContainerFolder = "Assets/_NtingCampus/ScriptableObjects/InventoryContainers";
        private const string PrefabFolder = "Assets/_NtingCampus/Prefabs/UI/InventoryGrid";

        [MenuItem("Nting Campus/Inventory Grid/Rebuild Demo Scene")]
        public static void RebuildDemoScene()
        {
            EnsureFolders();
            DemoAssets assets = CreateOrUpdateDemoAssets();
            CreateOrUpdateDemoPrefabs();
            CreateDemoScene(assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Inventory Grid demo scene rebuilt at " + ScenePath + ".");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "_NtingCampus");
            EnsureFolder("Assets/_NtingCampus", "Scenes");
            EnsureFolder("Assets/_NtingCampus", "ScriptableObjects");
            EnsureFolder("Assets/_NtingCampus/ScriptableObjects", "InventoryItems");
            EnsureFolder("Assets/_NtingCampus/ScriptableObjects", "InventoryContainers");
            EnsureFolder("Assets/_NtingCampus", "Prefabs");
            EnsureFolder("Assets/_NtingCampus/Prefabs", "UI");
            EnsureFolder("Assets/_NtingCampus/Prefabs/UI", "InventoryGrid");
            EnsureFolder("Assets/_NtingCampus", "Docs");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static DemoAssets CreateOrUpdateDemoAssets()
        {
            DemoAssets assets = new DemoAssets
            {
                pocket = CreateOrUpdateContainer(
                    "Pocket_Default.asset",
                    "pocket_default",
                    "口袋",
                    2,
                    3,
                    InventoryContainerType.Pocket,
                    true,
                    1.3f,
                    1.5f,
                    3f),
                schoolbag = CreateOrUpdateContainer(
                    "Schoolbag_Default.asset",
                    "schoolbag_default",
                    "书包",
                    5,
                    6,
                    InventoryContainerType.Schoolbag,
                    true,
                    1f,
                    1f,
                    20f),
                deskCavity = CreateOrUpdateContainer(
                    "DeskCavity_Default.asset",
                    "desk_cavity_default",
                    "桌肚",
                    6,
                    3,
                    InventoryContainerType.DeskCavity,
                    false,
                    0.9f,
                    0.8f,
                    15f),
                locker = CreateOrUpdateContainer(
                    "Locker_Default.asset",
                    "locker_default",
                    "储物柜",
                    6,
                    6,
                    InventoryContainerType.Locker,
                    false,
                    0.6f,
                    0.5f,
                    40f),
                testBox = CreateOrUpdateContainer(
                    "TestBox_Default.asset",
                    "test_box_default",
                    "测试箱",
                    4,
                    4,
                    InventoryContainerType.Custom,
                    false,
                    0.75f,
                    0.9f,
                    12f)
            };

            assets.items.Add(CreateOrUpdateItem("Eraser.asset", "eraser", "橡皮", 1, 1, false, 0.1f, 0f, 0f, 0f));
            assets.items.Add(CreateOrUpdateItem("SpicyStrip.asset", "spicy_strip", "辣条", 1, 2, true, 0.2f, 0.3f, 0.5f, 0.1f, "snack"));
            assets.items.Add(CreateOrUpdateItem("HomeworkBook.asset", "homework_book", "作业本", 2, 2, true, 0.8f, 0.1f, 0f, 0.1f));
            assets.items.Add(CreateOrUpdateItem("TakeoutBox.asset", "takeout_box", "外卖盒", 2, 2, false, 1.2f, 0.8f, 0.9f, 0.2f, "food", "smell_source"));
            assets.items.Add(CreateOrUpdateItem("Umbrella.asset", "umbrella", "雨伞", 1, 3, true, 0.7f, 0.1f, 0f, 0.3f));
            assets.items.Add(CreateOrUpdateItem("ExamPaperBag.asset", "exam_paper_bag", "试卷袋", 2, 1, true, 0.2f, 0.7f, 0f, 0.1f, "evidence"));

            assets.registry = LoadOrCreateAsset<InventoryItemRegistry>(ItemFolder + "/InventoryItemRegistry.asset");
            assets.registry.itemDefinitions = assets.items;
            EditorUtility.SetDirty(assets.registry);
            return assets;
        }

        private static InventoryContainerDefinition CreateOrUpdateContainer(
            string assetName,
            string id,
            string displayName,
            int width,
            int height,
            InventoryContainerType type,
            bool isPortable,
            float searchExposure,
            float accessSpeed,
            float maxWeight)
        {
            InventoryContainerDefinition definition = LoadOrCreateAsset<InventoryContainerDefinition>(ContainerFolder + "/" + assetName);
            definition.containerId = id;
            definition.displayName = displayName;
            definition.width = width;
            definition.height = height;
            definition.containerType = type;
            definition.isPortable = isPortable;
            definition.searchExposure = searchExposure;
            definition.accessSpeed = accessSpeed;
            definition.maxWeight = maxWeight;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ItemDefinition CreateOrUpdateItem(
            string assetName,
            string id,
            string displayName,
            int width,
            int height,
            bool canRotate,
            float weight,
            float suspicion,
            float smell,
            float noise,
            params string[] forbiddenTags)
        {
            ItemDefinition definition = LoadOrCreateAsset<ItemDefinition>(ItemFolder + "/" + assetName);
            definition.itemId = id;
            definition.displayName = displayName;
            definition.width = width;
            definition.height = height;
            definition.canRotate = canRotate;
            definition.weight = weight;
            definition.suspicion = suspicion;
            definition.smell = smell;
            definition.noise = noise;
            definition.stackable = false;
            definition.maxStack = 1;
            definition.forbiddenTags = new List<string>(forbiddenTags ?? new string[0]);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void CreateOrUpdateDemoPrefabs()
        {
            InventoryCellView cellPrefab = CreateCellPrefab();
            InventoryItemView itemPrefab = CreateItemPrefab();
            CreateGridPrefab(cellPrefab, itemPrefab);
        }

        private static InventoryCellView CreateCellPrefab()
        {
            GameObject root = new GameObject("InventoryCellView", typeof(RectTransform), typeof(InventoryRoundedBoxGraphic), typeof(InventoryCellView));
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(48f, 48f);
            InventoryCellView cellView = root.GetComponent<InventoryCellView>();
            cellView.background = root.GetComponent<InventoryRoundedBoxGraphic>();
            cellView.SetNormal();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/InventoryCellView.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<InventoryCellView>();
        }

        private static InventoryItemView CreateItemPrefab()
        {
            GameObject root = new GameObject("InventoryItemView", typeof(RectTransform), typeof(InventoryRoundedBoxGraphic), typeof(CanvasGroup), typeof(InventoryItemView));
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(48f, 48f);

            InventoryItemView itemView = root.GetComponent<InventoryItemView>();
            itemView.backgroundGraphic = root.GetComponent<InventoryRoundedBoxGraphic>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/InventoryItemView.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<InventoryItemView>();
        }

        private static void CreateGridPrefab(InventoryCellView cellPrefab, InventoryItemView itemPrefab)
        {
            GameObject root = new GameObject("InventoryGridView", typeof(RectTransform), typeof(InventoryGridView));
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(300f, 300f);
            InventoryGridView gridView = root.GetComponent<InventoryGridView>();
            gridView.gridRoot = rectTransform;
            gridView.cellPrefab = cellPrefab;
            gridView.itemPrefab = itemPrefab;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/InventoryGridView.prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreateDemoScene(DemoAssets assets)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f, 1f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            GameObject bootstrapObject = new GameObject("InventoryGridDemoBootstrap", typeof(InventoryGridDemoBootstrap));
            InventoryGridDemoBootstrap bootstrap = bootstrapObject.GetComponent<InventoryGridDemoBootstrap>();
            bootstrap.pocketDefinition = assets.pocket;
            bootstrap.schoolbagDefinition = assets.schoolbag;
            bootstrap.deskCavityDefinition = assets.deskCavity;
            bootstrap.lockerDefinition = assets.locker;
            bootstrap.testBoxDefinition = assets.testBox;
            bootstrap.itemRegistry = assets.registry;
            bootstrap.fallbackItems = assets.items;
            bootstrap.RebuildDemoUi();
            EditorUtility.SetDirty(bootstrap);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private sealed class DemoAssets
        {
            public InventoryContainerDefinition pocket;
            public InventoryContainerDefinition schoolbag;
            public InventoryContainerDefinition deskCavity;
            public InventoryContainerDefinition locker;
            public InventoryContainerDefinition testBox;
            public InventoryItemRegistry registry;
            public List<ItemDefinition> items = new List<ItemDefinition>();
        }
    }
}
