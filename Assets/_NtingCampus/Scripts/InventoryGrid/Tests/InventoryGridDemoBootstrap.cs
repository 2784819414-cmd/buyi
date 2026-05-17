using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace NtingCampus.InventoryGrid
{
    [DisallowMultipleComponent]
    public sealed class InventoryGridDemoBootstrap : MonoBehaviour
    {
        [Header("Container Definitions")]
        public InventoryContainerDefinition pocketDefinition;
        public InventoryContainerDefinition schoolbagDefinition;
        public InventoryContainerDefinition deskCavityDefinition;
        public InventoryContainerDefinition lockerDefinition;
        public InventoryContainerDefinition testBoxDefinition;

        [Header("Item Definitions")]
        public InventoryItemRegistry itemRegistry;
        public List<ItemDefinition> fallbackItems = new List<ItemDefinition>();

        [Header("Generated UI")]
        public Canvas demoCanvas;
        public InventoryWindowController windowController;

        private InventoryContainerRuntime pocket;
        private InventoryContainerRuntime schoolbag;
        private InventoryContainerRuntime deskCavity;
        private InventoryContainerRuntime locker;
        private InventoryContainerRuntime testBox;
        private int itemSerial;

        private void Start()
        {
            EnsureDemoData();
            RebuildDemoUi();
            OpenPocketAndSchoolbag();
        }

        [ContextMenu("Rebuild Inventory Grid Demo UI")]
        public void RebuildDemoUi()
        {
            DestroyExistingDemoUi();

            demoCanvas = CreateCanvas();
            EnsureEventSystem();

            RectTransform canvasRect = demoCanvas.GetComponent<RectTransform>();
            GameObject root = CreateRectObject("InventoryWindowRoot", canvasRect);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image rootBackground = root.AddComponent<Image>();
            rootBackground.color = new Color(0.025f, 0.03f, 0.036f, 0.98f);

            Font font = InventoryUIFont.DefaultFont;
            CreateBox(rootRect, "Toolbar", new Vector2(18f, -16f), new Vector2(1002f, 52f),
                new Color(0.085f, 0.105f, 0.125f, 0.96f), new Color(0.28f, 0.38f, 0.45f, 0.92f), 12f, 1.2f, false);
            CreateButton(rootRect, "打开 口袋 + 书包", new Vector2(24f, -24f), new Vector2(150f, 34f), OpenPocketAndSchoolbag, font);
            CreateButton(rootRect, "打开 书包 + 桌肚", new Vector2(184f, -24f), new Vector2(150f, 34f), OpenSchoolbagAndDeskCavity, font);
            CreateButton(rootRect, "打开 书包 + 储物柜", new Vector2(344f, -24f), new Vector2(160f, 34f), OpenSchoolbagAndLocker, font);
            CreateButton(rootRect, "打开 书包 + 测试箱", new Vector2(514f, -24f), new Vector2(170f, 34f), OpenSchoolbagAndTestBox, font);
            CreateButton(rootRect, "添加测试物品到书包", new Vector2(694f, -24f), new Vector2(170f, 34f), AddTestItemsToSchoolbag, font);
            CreateButton(rootRect, "清空所有容器", new Vector2(874f, -24f), new Vector2(130f, 34f), ClearAllContainers, font);

            GameObject controllerObject = CreateRectObject("InventoryWindowController", rootRect);
            windowController = controllerObject.AddComponent<InventoryWindowController>();

            CreateGridPanel(rootRect, "LeftContainer", new Vector2(72f, -88f), out InventoryGridView leftGrid, out Text leftTitle, font);
            CreateGridPanel(rootRect, "RightContainer", new Vector2(460f, -88f), out InventoryGridView rightGrid, out Text rightTitle, font);

            windowController.leftGrid = leftGrid;
            windowController.rightGrid = rightGrid;
            windowController.leftTitle = leftTitle;
            windowController.rightTitle = rightTitle;
            leftGrid.SetWindowController(windowController);
            rightGrid.SetWindowController(windowController);
        }

        public void OpenPocketAndSchoolbag()
        {
            EnsureDemoData();
            if (windowController == null)
            {
                Debug.LogWarning("Inventory demo open failed: window controller is missing.");
                return;
            }

            windowController.OpenContainers(pocket, schoolbag);
        }

        public void OpenSchoolbagAndDeskCavity()
        {
            EnsureDemoData();
            if (windowController == null)
            {
                Debug.LogWarning("Inventory demo open failed: window controller is missing.");
                return;
            }

            windowController.OpenContainers(schoolbag, deskCavity);
        }

        public void OpenSchoolbagAndLocker()
        {
            EnsureDemoData();
            if (windowController == null)
            {
                Debug.LogWarning("Inventory demo open failed: window controller is missing.");
                return;
            }

            windowController.OpenContainers(schoolbag, locker);
        }

        public void OpenSchoolbagAndTestBox()
        {
            EnsureDemoData();
            if (windowController == null)
            {
                Debug.LogWarning("Inventory demo open failed: window controller is missing.");
                return;
            }

            windowController.OpenContainers(schoolbag, testBox);
        }

        public void AddTestItemsToSchoolbag()
        {
            EnsureDemoData();
            if (schoolbag == null)
            {
                Debug.LogWarning("Inventory demo add failed: schoolbag container is missing.");
                return;
            }

            List<ItemDefinition> items = ResolveItemDefinitions();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition itemDefinition = items[i];
                if (itemDefinition == null)
                {
                    continue;
                }

                ItemInstance item = new ItemInstance(itemDefinition, CreateInstanceId(itemDefinition));
                if (!schoolbag.TryAutoPlace(item))
                {
                    Debug.LogWarning("Inventory demo add failed: schoolbag has no room for '" + itemDefinition.displayName + "'.");
                }
            }

            RefreshWindow();
        }

        public void ClearAllContainers()
        {
            EnsureDemoData();
            ClearContainer(pocket);
            ClearContainer(schoolbag);
            ClearContainer(deskCavity);
            ClearContainer(locker);
            ClearContainer(testBox);
            RefreshWindow();
        }

        private void EnsureDemoData()
        {
            EnsureDefinitions();

            if (pocket == null || pocket.definition != pocketDefinition)
            {
                pocket = new InventoryContainerRuntime(pocketDefinition);
            }

            if (schoolbag == null || schoolbag.definition != schoolbagDefinition)
            {
                schoolbag = new InventoryContainerRuntime(schoolbagDefinition);
            }

            if (deskCavity == null || deskCavity.definition != deskCavityDefinition)
            {
                deskCavity = new InventoryContainerRuntime(deskCavityDefinition);
            }

            if (locker == null || locker.definition != lockerDefinition)
            {
                locker = new InventoryContainerRuntime(lockerDefinition);
            }

            if (testBox == null || testBox.definition != testBoxDefinition)
            {
                testBox = new InventoryContainerRuntime(testBoxDefinition);
            }
        }

        private void EnsureDefinitions()
        {
            if (pocketDefinition == null)
            {
                pocketDefinition = CreateContainerDefinition("pocket_default", "口袋", 2, 3, InventoryContainerType.Pocket, true, 1.3f, 1.5f, 3f);
            }

            if (schoolbagDefinition == null)
            {
                schoolbagDefinition = CreateContainerDefinition("schoolbag_default", "书包", 5, 6, InventoryContainerType.Schoolbag, true, 1f, 1f, 20f);
            }

            if (deskCavityDefinition == null)
            {
                deskCavityDefinition = CreateContainerDefinition("desk_cavity_default", "桌肚", 6, 3, InventoryContainerType.DeskCavity, false, 0.9f, 0.8f, 15f);
            }

            if (lockerDefinition == null)
            {
                lockerDefinition = CreateContainerDefinition("locker_default", "储物柜", 6, 6, InventoryContainerType.Locker, false, 0.6f, 0.5f, 40f);
            }

            if (testBoxDefinition == null)
            {
                testBoxDefinition = CreateContainerDefinition("test_box_default", "测试箱", 4, 4, InventoryContainerType.Custom, false, 0.75f, 0.9f, 12f);
            }

            if (fallbackItems == null || fallbackItems.Count == 0)
            {
                fallbackItems = CreateFallbackItems();
            }

            if (itemRegistry == null)
            {
                itemRegistry = ScriptableObject.CreateInstance<InventoryItemRegistry>();
                itemRegistry.name = "Runtime Inventory Demo Item Registry";
                for (int i = 0; i < fallbackItems.Count; i++)
                {
                    itemRegistry.Register(fallbackItems[i]);
                }
            }
        }

        private List<ItemDefinition> ResolveItemDefinitions()
        {
            if (itemRegistry != null && itemRegistry.itemDefinitions != null && itemRegistry.itemDefinitions.Count > 0)
            {
                return itemRegistry.itemDefinitions;
            }

            return fallbackItems;
        }

        private string CreateInstanceId(ItemDefinition definition)
        {
            itemSerial++;
            string itemId = definition != null && !string.IsNullOrWhiteSpace(definition.itemId) ? definition.itemId : "item";
            return "demo_" + itemId + "_" + itemSerial.ToString("0000");
        }

        private void RefreshWindow()
        {
            if (windowController != null)
            {
                windowController.RefreshAll();
            }
        }

        private static void ClearContainer(InventoryContainerRuntime container)
        {
            if (container != null && container.items != null)
            {
                container.items.Clear();
            }
        }

        private static InventoryContainerDefinition CreateContainerDefinition(
            string id,
            string displayName,
            int width,
            int height,
            InventoryContainerType type,
            bool portable,
            float searchExposure,
            float accessSpeed,
            float maxWeight)
        {
            InventoryContainerDefinition definition = ScriptableObject.CreateInstance<InventoryContainerDefinition>();
            definition.name = displayName;
            definition.containerId = id;
            definition.displayName = displayName;
            definition.width = width;
            definition.height = height;
            definition.containerType = type;
            definition.isPortable = portable;
            definition.searchExposure = searchExposure;
            definition.accessSpeed = accessSpeed;
            definition.maxWeight = maxWeight;
            return definition;
        }

        private static List<ItemDefinition> CreateFallbackItems()
        {
            return new List<ItemDefinition>
            {
                CreateItem("eraser", "橡皮", 1, 1, false, 0.1f, 0f, 0f, 0f),
                CreateItem("spicy_strip", "辣条", 1, 2, true, 0.2f, 0.3f, 0.5f, 0.1f, "snack"),
                CreateItem("homework_book", "作业本", 2, 2, true, 0.8f, 0.1f, 0f, 0.1f),
                CreateItem("takeout_box", "外卖盒", 2, 2, false, 1.2f, 0.8f, 0.9f, 0.2f, "food", "smell_source"),
                CreateItem("umbrella", "雨伞", 1, 3, true, 0.7f, 0.1f, 0f, 0.3f),
                CreateItem("exam_paper_bag", "试卷袋", 2, 1, true, 0.2f, 0.7f, 0f, 0.1f, "evidence")
            };
        }

        private static ItemDefinition CreateItem(
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
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = displayName;
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
            definition.forbiddenTags = new List<string>(forbiddenTags ?? Array.Empty<string>());
            return definition;
        }

        private Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("InventoryGridDemoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = FindFirstObjectByType<EventSystem>();
            if (existing == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                existing = eventSystemObject.GetComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputModule = existing.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = existing.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }
#else
            if (existing.GetComponent<StandaloneInputModule>() == null)
            {
                existing.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private void DestroyExistingDemoUi()
        {
            GameObject existingCanvas = GameObject.Find("InventoryGridDemoCanvas");
            if (existingCanvas != null)
            {
                DestroyForMode(existingCanvas);
            }

            demoCanvas = null;
            windowController = null;
        }

        private static GameObject CreateRectObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return gameObject;
        }

        private static Button CreateButton(
            RectTransform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            UnityEngine.Events.UnityAction action,
            Font font)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(InventoryRoundedBoxGraphic), typeof(Button));
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            InventoryRoundedBoxGraphic image = buttonObject.GetComponent<InventoryRoundedBoxGraphic>();
            image.FillColor = new Color(0.16f, 0.205f, 0.235f, 1f);
            image.BorderColor = new Color(0.4f, 0.54f, 0.62f, 0.96f);
            image.BorderWidth = 1.1f;
            image.CornerRadius = 8f;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.88f, 0.92f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text text = CreateText(buttonObject.transform, "Text", label, font, TextAnchor.MiddleCenter, 14);
            text.color = Color.white;
            text.raycastTarget = false;
            return button;
        }

        private static void CreateGridPanel(
            RectTransform parent,
            string panelName,
            Vector2 anchoredPosition,
            out InventoryGridView gridView,
            out Text titleText,
            Font font)
        {
            RectTransform shadowRect = CreateBox(parent, panelName + "Shadow", anchoredPosition + new Vector2(5f, -6f), new Vector2(388f, 398f),
                new Color(0f, 0f, 0f, 0.24f), Color.clear, 14f, 0f, false);
            shadowRect.SetAsFirstSibling();

            GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(InventoryRoundedBoxGraphic));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = new Vector2(388f, 398f);

            InventoryRoundedBoxGraphic panelImage = panel.GetComponent<InventoryRoundedBoxGraphic>();
            panelImage.FillColor = new Color(0.105f, 0.125f, 0.145f, 0.98f);
            panelImage.BorderColor = new Color(0.24f, 0.33f, 0.39f, 0.95f);
            panelImage.BorderWidth = 1.4f;
            panelImage.CornerRadius = 14f;

            titleText = CreateText(panel.transform, "Title", "未打开", font, TextAnchor.MiddleLeft, 16);
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.offsetMin = new Vector2(16f, -42f);
            titleRect.offsetMax = new Vector2(-16f, -12f);
            titleText.color = new Color(0.9f, 0.95f, 1f, 1f);
            CreateBox(panelRect, "HeaderLine", new Vector2(16f, -50f), new Vector2(356f, 2f),
                new Color(0.33f, 0.48f, 0.56f, 0.9f), Color.clear, 1f, 0f, false);

            GameObject gridObject = new GameObject("Grid", typeof(RectTransform), typeof(InventoryGridView));
            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.SetParent(panel.transform, false);
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(0f, 1f);
            gridRect.pivot = new Vector2(0f, 1f);
            gridRect.anchoredPosition = new Vector2(24f, -68f);
            gridRect.sizeDelta = new Vector2(300f, 300f);

            gridView = gridObject.GetComponent<InventoryGridView>();
            gridView.gridRoot = gridRect;
            gridView.cellSize = 48f;
            gridView.cellSpacing = 3f;
        }

        private static RectTransform CreateBox(
            Transform parent,
            string objectName,
            Vector2 anchoredPosition,
            Vector2 size,
            Color fillColor,
            Color borderColor,
            float cornerRadius,
            float borderWidth,
            bool centered)
        {
            GameObject boxObject = new GameObject(objectName, typeof(RectTransform), typeof(InventoryRoundedBoxGraphic));
            RectTransform rectTransform = boxObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            rectTransform.anchorMax = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            rectTransform.pivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            InventoryRoundedBoxGraphic graphic = boxObject.GetComponent<InventoryRoundedBoxGraphic>();
            graphic.FillColor = fillColor;
            graphic.BorderColor = borderColor;
            graphic.BorderWidth = borderWidth;
            graphic.CornerRadius = cornerRadius;
            graphic.raycastTarget = false;
            return rectTransform;
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            string text,
            Font font,
            TextAnchor alignment,
            int fontSize)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Text uiText = textObject.GetComponent<Text>();
            uiText.font = font;
            uiText.text = text;
            uiText.alignment = alignment;
            uiText.fontSize = fontSize;
            uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
            uiText.verticalOverflow = VerticalWrapMode.Truncate;
            return uiText;
        }

        private static void DestroyForMode(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
