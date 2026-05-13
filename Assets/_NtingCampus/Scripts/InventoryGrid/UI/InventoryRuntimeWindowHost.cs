using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace NtingCampus.InventoryGrid
{
    [DisallowMultipleComponent]
    public sealed class InventoryRuntimeWindowHost : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject root;
        private InventoryWindowController windowController;

        public static InventoryRuntimeWindowHost Instance { get; private set; }

        public static void Open(InventoryContainerRuntime left, InventoryContainerRuntime right)
        {
            if (left == null || right == null)
            {
                Debug.LogWarning("Inventory window open failed: container runtime is null.");
                return;
            }

            InventoryRuntimeWindowHost host = GetOrCreate();
            try
            {
                host.EnsureWindow();
                host.canvas.gameObject.SetActive(true);
                host.root.SetActive(true);
                host.root.transform.SetAsLastSibling();
                host.windowController.OpenContainers(left, right);
                Debug.Log("Inventory window opened: " + GetContainerLabel(left) + " + " + GetContainerLabel(right) + ".");
            }
            catch (Exception exception)
            {
                if (host.root != null)
                {
                    host.root.SetActive(false);
                }

                Debug.LogException(exception);
            }
        }

        public static void Close()
        {
            if (Instance != null && Instance.root != null)
            {
                Instance.root.SetActive(false);
            }
        }

        private static InventoryRuntimeWindowHost GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            InventoryRuntimeWindowHost existing = FindFirstObjectByType<InventoryRuntimeWindowHost>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject hostObject = new GameObject("InventoryRuntimeWindowHost");
            DontDestroyOnLoad(hostObject);
            Instance = hostObject.AddComponent<InventoryRuntimeWindowHost>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (root != null && root.activeSelf && WasClosePressed())
            {
                root.SetActive(false);
            }
        }

        private void EnsureWindow()
        {
            if (root != null && windowController != null)
            {
                EnsureEventSystem();
                canvas.gameObject.SetActive(true);
                root.transform.SetAsLastSibling();
                return;
            }

            if (root != null && windowController == null)
            {
                Destroy(root);
                root = null;
            }

            if (canvas == null)
            {
                canvas = CreateCanvas();
            }
            else
            {
                canvas.gameObject.SetActive(true);
            }

            EnsureEventSystem();

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            root = CreateRectObject("InventoryRuntimeWindow", canvasRect);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image background = root.AddComponent<Image>();
            background.color = new Color(0.02f, 0.025f, 0.03f, 0.82f);

            Font font = InventoryUIFont.DefaultFont;

            RectTransform frameShadow = CreateBox(rootRect, "WindowShadow", new Vector2(0f, -6f), new Vector2(920f, 532f),
                new Color(0f, 0f, 0f, 0.38f), Color.clear, 18f, 0f, true);
            frameShadow.SetAsFirstSibling();

            RectTransform frame = CreateBox(rootRect, "WindowFrame", Vector2.zero, new Vector2(920f, 532f),
                new Color(0.075f, 0.09f, 0.105f, 0.98f), new Color(0.36f, 0.48f, 0.55f, 0.95f), 18f, 1.4f, true);

            RectTransform titleBand = CreateBox(frame, "TitleBand", new Vector2(0f, -8f), new Vector2(884f, 58f),
                new Color(0.11f, 0.135f, 0.155f, 0.94f), new Color(0.22f, 0.3f, 0.36f, 0.9f), 12f, 1f, false);
            titleBand.anchorMin = new Vector2(0.5f, 1f);
            titleBand.anchorMax = new Vector2(0.5f, 1f);
            titleBand.pivot = new Vector2(0.5f, 1f);

            Text title = CreateText(titleBand, "Title", "储物空间", font, TextAnchor.MiddleLeft, 24);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.offsetMin = new Vector2(22f, 0f);
            titleRect.offsetMax = new Vector2(-120f, 0f);
            title.color = new Color(0.92f, 0.96f, 1f, 1f);

            Text subtitle = CreateText(titleBand, "Subtitle", "拖拽摆放  ·  右键旋转  ·  双击转移", font, TextAnchor.MiddleRight, 13);
            RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
            subtitleRect.offsetMin = new Vector2(420f, 0f);
            subtitleRect.offsetMax = new Vector2(-100f, 0f);
            subtitle.color = new Color(0.58f, 0.69f, 0.76f, 1f);

            CreateButton(titleBand, "关闭", new Vector2(-18f, -12f), new Vector2(72f, 34f), Close, font, true);

            GameObject controllerObject = CreateRectObject("InventoryWindowController", frame);
            windowController = controllerObject.AddComponent<InventoryWindowController>();

            CreateGridPanel(frame, "LeftContainer", new Vector2(36f, -92f), out InventoryGridView leftGrid, out Text leftTitle, font);
            CreateGridPanel(frame, "RightContainer", new Vector2(472f, -92f), out InventoryGridView rightGrid, out Text rightTitle, font);

            windowController.leftGrid = leftGrid;
            windowController.rightGrid = rightGrid;
            windowController.leftTitle = leftTitle;
            windowController.rightTitle = rightTitle;
            leftGrid.SetWindowController(windowController);
            rightGrid.SetWindowController(windowController);
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("InventoryRuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);

            Canvas newCanvas = canvasObject.GetComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.overrideSorting = true;
            newCanvas.sortingOrder = 32760;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            return newCanvas;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                DontDestroyOnLoad(eventSystemObject);
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
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
            Font font,
            bool anchorRight)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(InventoryRoundedBoxGraphic), typeof(Button));
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rectTransform.anchorMax = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rectTransform.pivot = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            InventoryRoundedBoxGraphic image = buttonObject.GetComponent<InventoryRoundedBoxGraphic>();
            image.FillColor = new Color(0.17f, 0.22f, 0.25f, 1f);
            image.BorderColor = new Color(0.43f, 0.58f, 0.65f, 0.95f);
            image.BorderWidth = 1.2f;
            image.CornerRadius = 8f;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
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

        private static bool WasClosePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.escapeKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private static string GetContainerLabel(InventoryContainerRuntime runtime)
        {
            if (runtime == null || runtime.definition == null)
            {
                return "null";
            }

            InventoryContainerDefinition definition = runtime.definition;
            return string.IsNullOrWhiteSpace(definition.displayName) ? definition.containerId : definition.displayName;
        }
    }
}
