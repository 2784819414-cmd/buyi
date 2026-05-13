using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageWindowUI : MonoBehaviour
    {
        public Canvas Canvas;
        public CanvasGroup CanvasGroup;
        public RectTransform Root;
        public RectTransform WindowPanel;
        public RectTransform DragLayer;
        public StorageDragController DragController;

        public StorageGridUI[] PocketGrids = new StorageGridUI[4];
        public StorageGridUI BackpackGrid;
        public StorageGridUI ExternalGrid;

        public Text LeftTitleText;
        public Text LeftMetaText;
        public Text RightTitleText;
        public Text RightMetaText;
        public Text SelectedItemNameText;
        public Text SelectedItemMetaText;
        public Text StatusText;
        public Text BackpackTabLabel;
        public Text PocketTabLabel;
        public Button PocketTabButton;
        public Button BackpackTabButton;

        private readonly List<MonoBehaviour> suppressedMapEditors = new List<MonoBehaviour>();
        private readonly List<bool> suppressedMapEditorStates = new List<bool>();
        private const float DefaultGridCellSize = 52f;
        private const float MinimumGridCellSize = 28f;

        private StorageContainerModel[] pockets = new StorageContainerModel[4];
        private StorageContainerModel backpack;
        private StorageContainerModel externalContainer;
        private bool backpackEquipped;
        private bool showingBackpack;
        private GameObject pocketPage;
        private GameObject backpackPage;
        private GameObject noBackpackHint;
        private GameObject visibleRoot;
        private bool built;

        public bool IsOpen => visibleRoot != null && visibleRoot.activeSelf;

        private void Awake()
        {
            EnsureBuilt();
            HideImmediate();
        }

        private void Update()
        {
            if (IsOpen && WasEscapePressed())
            {
                Close();
            }
        }

        public void Open(
            StorageContainerModel[] pocketContainers,
            StorageContainerModel backpackContainer,
            bool hasBackpack,
            StorageContainerModel rightContainer)
        {
            EnsureBuilt();
            pockets = pocketContainers != null && pocketContainers.Length >= 4 ? pocketContainers : new StorageContainerModel[4];
            backpack = backpackContainer;
            backpackEquipped = hasBackpack && backpack != null;
            externalContainer = rightContainer;
            showingBackpack = false;

            visibleRoot.SetActive(true);
            DragLayer.gameObject.SetActive(true);
            CanvasGroup.alpha = 1f;
            SuppressMapEditorOverlay();
            RefreshPages();
            SelectItem(null);
            ShowStatus("拖拽物品、右键旋转、双击转移", false);
        }

        public void Close()
        {
            if (DragController != null && DragController.IsDragging)
            {
                DragController.CancelDrag();
            }

            HideImmediate();
            RestoreMapEditorOverlay();
        }

        public void HideImmediate()
        {
            if (visibleRoot != null)
            {
                visibleRoot.SetActive(false);
            }

            if (DragLayer != null)
            {
                DragLayer.gameObject.SetActive(false);
            }
        }

        public void ShowPocketPage()
        {
            showingBackpack = false;
            RefreshPages();
        }

        public void ShowBackpackPage()
        {
            if (!backpackEquipped)
            {
                ShowStatus("未装备背包", true);
                return;
            }

            showingBackpack = true;
            RefreshPages();
        }

        public void RefreshAllGrids()
        {
            if (pocketPage != null && pocketPage.activeSelf)
            {
                for (int i = 0; i < PocketGrids.Length; i++)
                {
                    if (PocketGrids[i] != null && i < pockets.Length)
                    {
                        PocketGrids[i].Bind(pockets[i], this);
                    }
                }
            }

            if (BackpackGrid != null && backpackEquipped)
            {
                BackpackGrid.Bind(backpack, this);
            }

            if (ExternalGrid != null)
            {
                ConfigureGridCellSize(ExternalGrid, externalContainer, 420f, 388f, DefaultGridCellSize);
                ExternalGrid.Bind(externalContainer, this);
            }

            RefreshHeaders();
        }

        public void SelectItem(StorageItemModel item)
        {
            if (SelectedItemNameText == null || SelectedItemMetaText == null)
            {
                return;
            }

            if (item == null)
            {
                SelectedItemNameText.text = "未选中物品";
                SelectedItemMetaText.text = "点击物品查看详情";
                return;
            }

            SelectedItemNameText.text = item.DisplayName;
            SelectedItemMetaText.text = item.CurrentWidth + "x" + item.CurrentHeight + "  " +
                                        item.Weight.ToString("0.#") + "kg  " + item.Description;
        }

        public void ShowStatus(string message, bool warning)
        {
            if (StatusText == null)
            {
                return;
            }

            StatusText.text = message;
            StatusText.color = warning ? new Color(0.95f, 0.38f, 0.32f, 1f) : StoragePalette.TextSecondary;
        }

        public bool TryRotateItem(StorageItemView view)
        {
            if (view == null || view.Item == null || view.OwnerGrid == null)
            {
                return false;
            }

            StorageItemModel item = view.Item;
            item.Rotate();
            if (view.OwnerGrid.CanPlace(item, item.X, item.Y))
            {
                RefreshAllGrids();
                SelectItem(item);
                ShowStatus("已旋转 " + item.DisplayName, false);
                return true;
            }

            item.Rotate();
            ShowStatus("旋转后空间不足", true);
            RefreshAllGrids();
            SelectItem(item);
            return false;
        }

        public bool TryQuickTransfer(StorageItemView view)
        {
            if (view == null || view.Item == null || view.OwnerGrid == null)
            {
                return false;
            }

            StorageItemModel item = view.Item;
            StorageGridUI sourceGrid = view.OwnerGrid;

            if (sourceGrid == ExternalGrid)
            {
                if (showingBackpack)
                {
                    if (!backpackEquipped || BackpackGrid == null)
                    {
                        ShowStatus("未装备背包", true);
                        return false;
                    }

                    return TryMoveToFirstFit(sourceGrid, new[] { BackpackGrid }, item);
                }

                return TryMoveToFirstFit(sourceGrid, PocketGrids, item);
            }

            return TryMoveToFirstFit(sourceGrid, new[] { ExternalGrid }, item);
        }

        private bool TryMoveToFirstFit(StorageGridUI sourceGrid, StorageGridUI[] targetGrids, StorageItemModel item)
        {
            for (int i = 0; i < targetGrids.Length; i++)
            {
                StorageGridUI targetGrid = targetGrids[i];
                if (targetGrid == null || targetGrid == sourceGrid || targetGrid.Container == null)
                {
                    continue;
                }

                if (!targetGrid.FindFirstFit(item, out Vector2Int position))
                {
                    continue;
                }

                if (!targetGrid.PlaceItem(item, position.x, position.y))
                {
                    continue;
                }

                if (sourceGrid.RemoveItem(item))
                {
                    RefreshAllGrids();
                    SelectItem(item);
                    ShowStatus("已转移 " + item.DisplayName, false);
                    return true;
                }

                targetGrid.RemoveItem(item);
                break;
            }

            ShowStatus("目标空间不足", true);
            RefreshAllGrids();
            SelectItem(item);
            return false;
        }

        private static void ConfigureGridCellSize(StorageGridUI grid, StorageContainerModel model, float maxWidth, float maxHeight, float defaultCellSize)
        {
            if (grid == null || model == null)
            {
                return;
            }

            float spacing = Mathf.Max(0f, grid.CellSpacing);
            float widthCellSize = model.Columns > 0
                ? (maxWidth - Mathf.Max(0, model.Columns - 1) * spacing) / model.Columns
                : defaultCellSize;
            float heightCellSize = model.Rows > 0
                ? (maxHeight - Mathf.Max(0, model.Rows - 1) * spacing) / model.Rows
                : defaultCellSize;
            grid.CellSize = Mathf.Clamp(Mathf.Min(defaultCellSize, widthCellSize, heightCellSize), MinimumGridCellSize, defaultCellSize);
        }

        private void RefreshPages()
        {
            if (pocketPage != null)
            {
                pocketPage.SetActive(!showingBackpack);
            }

            if (backpackPage != null)
            {
                backpackPage.SetActive(showingBackpack);
            }

            if (noBackpackHint != null)
            {
                noBackpackHint.SetActive(showingBackpack && !backpackEquipped);
            }

            if (BackpackGrid != null)
            {
                BackpackGrid.gameObject.SetActive(showingBackpack && backpackEquipped);
            }

            if (PocketTabButton != null)
            {
                PocketTabButton.interactable = showingBackpack;
            }

            if (BackpackTabButton != null)
            {
                BackpackTabButton.interactable = backpackEquipped && !showingBackpack;
            }

            if (BackpackTabLabel != null)
            {
                BackpackTabLabel.text = backpackEquipped ? "背包" : "背包  未装备";
                BackpackTabLabel.color = backpackEquipped ? StoragePalette.TextPrimary : StoragePalette.TextMuted;
            }

            RefreshAllGrids();
        }

        private void RefreshHeaders()
        {
            if (LeftTitleText != null)
            {
                LeftTitleText.text = showingBackpack ? "学生书包" : "衣服口袋";
            }

            if (LeftMetaText != null)
            {
                if (showingBackpack)
                {
                    LeftMetaText.text = backpackEquipped && backpack != null
                        ? backpack.Columns + "x" + backpack.Rows + "  " + backpack.CurrentWeight.ToString("0.#") + "/" + backpack.MaxWeight.ToString("0.#") + "kg"
                        : "未装备背包";
                }
                else
                {
                    LeftMetaText.text = "4 × 2x3";
                }
            }

            if (RightTitleText != null)
            {
                RightTitleText.text = externalContainer != null ? externalContainer.DisplayName : "外部容器";
            }

            if (RightMetaText != null)
            {
                RightMetaText.text = externalContainer != null
                    ? externalContainer.Columns + "x" + externalContainer.Rows + "  " +
                      externalContainer.CurrentWeight.ToString("0.#") + "/" + externalContainer.MaxWeight.ToString("0.#") + "kg"
                    : string.Empty;
            }
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            EnsureCanvas();
            Root = StorageUIUtility.CreateRectObject("StorageUIRoot", transform).GetComponent<RectTransform>();
            Root.anchorMin = Vector2.zero;
            Root.anchorMax = Vector2.one;
            Root.offsetMin = Vector2.zero;
            Root.offsetMax = Vector2.zero;
            visibleRoot = Root.gameObject;
            CanvasGroup = Root.gameObject.AddComponent<CanvasGroup>();

            GameObject dimmer = StorageUIUtility.CreateRectObject("BackgroundDimmer", Root).gameObject;
            RectTransform dimmerRect = dimmer.GetComponent<RectTransform>();
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;
            Image dimmerImage = dimmer.AddComponent<Image>();
            dimmerImage.color = StoragePalette.Dimmer;

            CreateWindow();
            CreateDragLayer();
            built = true;
        }

        private void EnsureCanvas()
        {
            Canvas = GetComponent<Canvas>();
            if (Canvas == null)
            {
                Canvas = gameObject.AddComponent<Canvas>();
            }

            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.overrideSorting = true;
            Canvas.sortingOrder = 32000;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            EnsureEventSystem();
        }

        private void CreateWindow()
        {
            RectTransform shadow = CreateBox(Root, "WindowShadow", new Vector2(12f, -10f), new Vector2(1510f, 830f),
                new Color(0f, 0f, 0f, 0.36f), Color.clear, 20f, 0f, true);

            WindowPanel = CreateBox(Root, "StorageWindow", Vector2.zero, new Vector2(1500f, 820f),
                StoragePalette.Window, StoragePalette.WindowBorder, 18f, 1.4f, true);
            shadow.SetSiblingIndex(1);
            WindowPanel.SetAsLastSibling();

            CreateHeader(WindowPanel);
            CreateBody(WindowPanel);
            CreateFooter(WindowPanel);
        }

        private void CreateHeader(RectTransform parent)
        {
            RectTransform header = CreateBox(parent, "Header", new Vector2(28f, -24f), new Vector2(1444f, 82f),
                new Color(0.08f, 0.1f, 0.116f, 0.96f), StoragePalette.PanelBorder, 12f, 1f, false);

            Text title = StorageUIUtility.CreateText("TitleText", header, "储物空间", 31, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(title.rectTransform, 24f, 7f, 480f, 42f);

            Text hint = StorageUIUtility.CreateText("HintText", header, "拖拽物品 · 右键旋转 · 双击转移 · Esc关闭", 15, TextAnchor.MiddleLeft, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(hint.rectTransform, 26f, 50f, 680f, 24f);

            Button close = StorageUIUtility.CreateButton("CloseButton", header, "关闭", Close,
                new Color(0.135f, 0.16f, 0.18f, 1f), new Color(0.34f, 0.43f, 0.48f, 0.95f));
            RectTransform closeRect = close.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(closeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            closeRect.anchoredPosition = new Vector2(-18f, -17f);
            closeRect.sizeDelta = new Vector2(86f, 42f);
        }

        private void CreateBody(RectTransform parent)
        {
            RectTransform leftPanel = CreateBox(parent, "LeftPanel_PlayerStorage", new Vector2(28f, -122f), new Vector2(662f, 552f),
                StoragePalette.Panel, StoragePalette.PanelBorder, 14f, 1.2f, false);
            RectTransform center = CreateBox(parent, "CenterTransferHint", new Vector2(710f, -122f), new Vector2(80f, 552f),
                new Color(0.045f, 0.055f, 0.064f, 0.38f), Color.clear, 10f, 0f, false);
            RectTransform rightPanel = CreateBox(parent, "RightPanel_Container", new Vector2(810f, -122f), new Vector2(662f, 552f),
                StoragePalette.Panel, StoragePalette.PanelBorder, 14f, 1.2f, false);

            Text arrow = StorageUIUtility.CreateText("TransferArrow", center, "⇄", 26, TextAnchor.MiddleCenter, StoragePalette.TextMuted);
            arrow.rectTransform.offsetMin = Vector2.zero;
            arrow.rectTransform.offsetMax = Vector2.zero;

            CreateLeftPanel(leftPanel);
            CreateRightPanel(rightPanel);
        }

        private void CreateLeftPanel(RectTransform panel)
        {
            LeftTitleText = StorageUIUtility.CreateText("LeftTitle", panel, "衣服口袋", 22, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(LeftTitleText.rectTransform, 22f, 12f, 240f, 34f);

            LeftMetaText = StorageUIUtility.CreateText("LeftMeta", panel, "4 × 2x3", 15, TextAnchor.MiddleRight, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(LeftMetaText.rectTransform, 360f, 16f, 280f, 28f);

            RectTransform tabs = StorageUIUtility.CreateRectObject("Tabs", panel).GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            tabs.anchoredPosition = new Vector2(22f, -58f);
            tabs.sizeDelta = new Vector2(260f, 36f);

            PocketTabButton = StorageUIUtility.CreateButton("PocketTab", tabs, "口袋", ShowPocketPage,
                new Color(0.145f, 0.18f, 0.2f, 1f), StoragePalette.PanelBorder);
            PocketTabButton.GetComponent<RectTransform>().sizeDelta = new Vector2(86f, 34f);
            PocketTabLabel = PocketTabButton.GetComponentInChildren<Text>();

            BackpackTabButton = StorageUIUtility.CreateButton("BackpackTab", tabs, "背包", ShowBackpackPage,
                new Color(0.11f, 0.132f, 0.15f, 1f), StoragePalette.PanelBorder);
            RectTransform backpackTabRect = BackpackTabButton.GetComponent<RectTransform>();
            backpackTabRect.anchoredPosition = new Vector2(94f, 0f);
            backpackTabRect.sizeDelta = new Vector2(132f, 34f);
            BackpackTabLabel = BackpackTabButton.GetComponentInChildren<Text>();

            pocketPage = StorageUIUtility.CreateRectObject("PocketPage", panel).gameObject;
            RectTransform pocketRect = pocketPage.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(pocketRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            pocketRect.anchoredPosition = new Vector2(0f, 0f);
            pocketRect.sizeDelta = panel.sizeDelta;

            CreatePocketGrid(pocketRect, 0, "左胸袋", new Vector2(22f, -108f));
            CreatePocketGrid(pocketRect, 1, "右胸袋", new Vector2(338f, -108f));
            CreatePocketGrid(pocketRect, 2, "左裤袋", new Vector2(22f, -336f));
            CreatePocketGrid(pocketRect, 3, "右裤袋", new Vector2(338f, -336f));

            backpackPage = StorageUIUtility.CreateRectObject("BackpackPage", panel).gameObject;
            RectTransform backpackPageRect = backpackPage.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(backpackPageRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            backpackPageRect.anchoredPosition = Vector2.zero;
            backpackPageRect.sizeDelta = panel.sizeDelta;

            BackpackGrid = CreateGrid(backpackPageRect, "BackpackGrid", new Vector2(193f, -138f), 52f, 4f);
            noBackpackHint = StorageUIUtility.CreateRectObject("NoBackpackHint", backpackPageRect).gameObject;
            Text hint = StorageUIUtility.CreateText("HintText", noBackpackHint.transform, "未装备背包", 20, TextAnchor.MiddleCenter, StoragePalette.TextMuted);
            RectTransform noBackpackRect = noBackpackHint.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(noBackpackRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            noBackpackRect.anchoredPosition = new Vector2(180f, -210f);
            noBackpackRect.sizeDelta = new Vector2(300f, 100f);
            hint.rectTransform.offsetMin = Vector2.zero;
            hint.rectTransform.offsetMax = Vector2.zero;
        }

        private void CreateRightPanel(RectTransform panel)
        {
            RightTitleText = StorageUIUtility.CreateText("RightTitle", panel, "测试箱", 22, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(RightTitleText.rectTransform, 22f, 12f, 260f, 34f);

            RightMetaText = StorageUIUtility.CreateText("RightMeta", panel, "4x4  0/12kg", 15, TextAnchor.MiddleRight, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(RightMetaText.rectTransform, 340f, 16f, 300f, 28f);

            ExternalGrid = CreateGrid(panel, "ContainerGrid", new Vector2(221f, -154f), 52f, 4f);
        }

        private void CreateFooter(RectTransform parent)
        {
            RectTransform footer = CreateBox(parent, "Footer", new Vector2(28f, -698f), new Vector2(1444f, 94f),
                new Color(0.073f, 0.087f, 0.1f, 0.96f), StoragePalette.PanelBorder, 12f, 1f, false);

            SelectedItemNameText = StorageUIUtility.CreateText("SelectedItemName", footer, "未选中物品", 18, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(SelectedItemNameText.rectTransform, 22f, 14f, 520f, 28f);

            SelectedItemMetaText = StorageUIUtility.CreateText("SelectedItemMeta", footer, "点击物品查看详情", 14, TextAnchor.MiddleLeft, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(SelectedItemMetaText.rectTransform, 22f, 48f, 900f, 24f);

            StatusText = StorageUIUtility.CreateText("StatusHint", footer, string.Empty, 15, TextAnchor.MiddleRight, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(StatusText.rectTransform, 860f, 20f, 560f, 54f);
        }

        private void CreateDragLayer()
        {
            DragLayer = StorageUIUtility.CreateRectObject("DragLayer", transform).GetComponent<RectTransform>();
            DragLayer.anchorMin = Vector2.zero;
            DragLayer.anchorMax = Vector2.one;
            DragLayer.offsetMin = Vector2.zero;
            DragLayer.offsetMax = Vector2.zero;
            DragLayer.SetAsLastSibling();

            DragController = gameObject.GetComponent<StorageDragController>();
            if (DragController == null)
            {
                DragController = gameObject.AddComponent<StorageDragController>();
            }

            DragController.Window = this;
            DragController.DragLayer = DragLayer;
            DragController.Canvas = Canvas;
        }

        private void CreatePocketGrid(RectTransform parent, int index, string title, Vector2 position)
        {
            RectTransform panel = CreateBox(parent, "PocketGrid_" + title, position, new Vector2(286f, 204f),
                StoragePalette.PanelRaised, StoragePalette.PanelBorder, 10f, 1f, false);
            Text label = StorageUIUtility.CreateText("PocketTitle", panel, title, 15, TextAnchor.MiddleLeft, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(label.rectTransform, 16f, 8f, 250f, 24f);

            PocketGrids[index] = CreateGrid(panel, "Grid", new Vector2(92f, -42f), 48f, 4f);
        }

        private StorageGridUI CreateGrid(Transform parent, string name, Vector2 position, float cellSize, float spacing)
        {
            GameObject gridObject = new GameObject(name, typeof(RectTransform), typeof(StorageGridUI));
            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.SetParent(parent, false);
            StorageUIUtility.SetAnchor(gridRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            gridRect.anchoredPosition = position;

            StorageGridUI grid = gridObject.GetComponent<StorageGridUI>();
            grid.CellSize = cellSize;
            grid.CellSpacing = spacing;
            return grid;
        }

        private RectTransform CreateBox(Transform parent, string name, Vector2 position, Vector2 size, Color fill, Color border, float radius, float borderWidth, bool centered)
        {
            GameObject boxObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(StorageBoxGraphic));
            RectTransform rect = boxObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            StorageUIUtility.SetAnchor(rect, centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f), centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f), centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f));
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            StorageBoxGraphic box = boxObject.GetComponent<StorageBoxGraphic>();
            box.raycastTarget = false;
            box.SetStyle(fill, border, borderWidth, radius);
            return rect;
        }

        private static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.UI.InputSystemUIInputModule inputModule = eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
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

        private void SuppressMapEditorOverlay()
        {
            RestoreMapEditorOverlay();
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == this || behaviour.GetType().Name != "CampusRuntimeMapEditor")
                {
                    continue;
                }

                suppressedMapEditors.Add(behaviour);
                suppressedMapEditorStates.Add(behaviour.enabled);
                behaviour.enabled = false;
            }
        }

        private void RestoreMapEditorOverlay()
        {
            for (int i = 0; i < suppressedMapEditors.Count; i++)
            {
                if (suppressedMapEditors[i] != null)
                {
                    suppressedMapEditors[i].enabled = suppressedMapEditorStates[i];
                }
            }

            suppressedMapEditors.Clear();
            suppressedMapEditorStates.Clear();
        }
    }
}
