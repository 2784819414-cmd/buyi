using System.Collections.Generic;
using DG.Tweening;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
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
        public Canvas DragCanvas;
        public StorageDragController DragController;

        public StorageGridUI[] PocketGrids = new StorageGridUI[4];
        public StorageGridUI[] HandGrids = new StorageGridUI[2];
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
        public Button UseItemButton;

        private Text windowEyebrowText;
        private Text windowTitleText;
        private Text windowHintText;
        private Text noBackpackTitleText;
        private Text noBackpackMetaText;
        private Text itemInfoLabelText;
        private Text statusLabelText;
        private readonly Text[] pocketTitleTexts = new Text[4];
        private const float DefaultGridCellSize = 52f;
        private const float MinimumGridCellSize = 28f;
        private const int WindowSortingOrder = 32000;
        private const int DragSortingOrder = 32760;

        private readonly List<MonoBehaviour> suppressedMapEditors = new List<MonoBehaviour>();
        private readonly List<bool> suppressedMapEditorStates = new List<bool>();
        private StorageContainerModel[] hands = new StorageContainerModel[2];
        private StorageContainerModel[] pockets = new StorageContainerModel[4];
        private StorageContainerModel backpack;
        private StorageContainerModel externalContainer;
        private StorageItemModel selectedItem;
        private StorageGridUI selectedItemGrid;
        private bool backpackEquipped;
        private bool showingBackpack;
        private GameObject pocketPage;
        private GameObject backpackPage;
        private GameObject noBackpackHint;
        private RectTransform backpackGridPad;
        private RectTransform externalGridPad;
        private GameObject visibleRoot;
        private bool built;
        private GameObject groundDropSource;
        private GameObject transferActor;
        private IStorageTransferHandler transferHandler = StorageDefaultTransferHandler.Instance;
        private Tween visibilityTween;

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

        private void OnDestroy()
        {
            if (DragLayer != null && DragLayer.transform.parent == null)
            {
                Destroy(DragLayer.gameObject);
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
            ShowAnimated();
            SuppressMapEditorOverlay();
            RefreshLocalizedText();
            RefreshPages();
            SelectItem(null);
            ShowStatus(StorageTextCatalog.Get(StorageTextId.WindowHint), false);
        }

        public void OpenPlayerStorage(
            StorageContainerModel[] handContainers,
            StorageContainerModel[] pocketContainers,
            StorageContainerModel backpackContainer,
            bool hasBackpack,
            StorageContainerModel rightContainer,
            bool startOnBackpack)
        {
            EnsureBuilt();
            hands = handContainers != null && handContainers.Length >= 2 ? handContainers : new StorageContainerModel[2];
            pockets = pocketContainers != null && pocketContainers.Length >= 4 ? pocketContainers : new StorageContainerModel[4];
            backpack = backpackContainer;
            backpackEquipped = hasBackpack && backpack != null;
            externalContainer = rightContainer;
            showingBackpack = startOnBackpack && backpackEquipped;

            visibleRoot.SetActive(true);
            DragLayer.gameObject.SetActive(true);
            ShowAnimated();
            SuppressMapEditorOverlay();
            AttachSharedHandGridsFromScene();
            RefreshLocalizedText();
            RefreshPages();
            SelectItem(null);
            ShowStatus(StorageTextCatalog.Get(StorageTextId.PlayerWindowHint), false);
        }

        public void SetGroundDropContext(GameObject groundDropContext)
        {
            groundDropSource = groundDropContext;
        }

        public void SetActorContext(GameObject actorContext)
        {
            transferActor = actorContext;
        }

        public void SetTransferHandler(IStorageTransferHandler handler)
        {
            transferHandler = handler ?? StorageDefaultTransferHandler.Instance;
        }

        public void SetSharedHandGrids(StorageGridUI[] grids)
        {
            if (HandGrids == null || HandGrids.Length != 2)
            {
                HandGrids = new StorageGridUI[2];
            }

            HandGrids[0] = grids != null && grids.Length > 0 ? grids[0] : null;
            HandGrids[1] = grids != null && grids.Length > 1 ? grids[1] : null;
        }

        private void AttachSharedHandGridsFromScene()
        {
            CampusGameplayHudView hudView = FindFirstObjectByType<CampusGameplayHudView>(FindObjectsInactive.Include);
            if (hudView != null)
            {
                hudView.AttachHandGridsToStorageWindow(this);
            }
        }

        public StorageTransferContext CreateTransferContext(StorageTransferReason reason)
        {
            return StorageTransferContext.ForActor(transferActor != null ? transferActor : groundDropSource, reason);
        }

        public bool TryMoveItem(
            StorageItemModel item,
            StorageGridUI sourceGrid,
            StorageGridUI targetGrid,
            int x,
            int y)
        {
            if (item == null || targetGrid == null)
            {
                return false;
            }

            bool moved = TransferHandler.TryMoveItem(
                item,
                sourceGrid != null ? sourceGrid.Container : null,
                targetGrid.Container,
                x,
                y,
                CreateTransferContext(StorageTransferReason.Move),
                out StorageTransferResult result);
            if (!moved && !string.IsNullOrWhiteSpace(result.Message))
            {
                ShowStatus(result.Message, true);
            }

            return moved;
        }

        public bool TryDropItemToGround(StorageItemModel item, StorageGridUI sourceGrid)
        {
            if (item == null || sourceGrid == null)
            {
                return false;
            }

            if (!TransferHandler.TryDropItemToGround(
                    groundDropSource,
                    item,
                    sourceGrid.Container,
                    CreateTransferContext(StorageTransferReason.DropToGround),
                    out StorageTransferResult result))
            {
                ShowStatus(string.IsNullOrWhiteSpace(result.Message) ? StorageTextCatalog.Get(StorageTextId.CouldNotDropToGround) : result.Message, true);
                return false;
            }

            ShowStatus(result.Message, result.Illegal && result.Observed);
            return true;
        }

        public void Close()
        {
            if (DragController != null && DragController.IsDragging)
            {
                DragController.CancelDrag();
            }

            HideAnimated();
            RestoreMapEditorOverlay();
        }

        public void HideImmediate()
        {
            KillTween();
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
                ShowStatus(StorageTextCatalog.Get(StorageTextId.NoBackpack), true);
                return;
            }

            showingBackpack = true;
            RefreshPages();
        }

        public void RefreshAllGrids()
        {
            RefreshActorCarriedEvidenceState();

            for (int handIndex = 0; handIndex < HandGrids.Length; handIndex++)
            {
                if (HandGrids[handIndex] != null)
                {
                    HandGrids[handIndex].Bind(handIndex < hands.Length ? hands[handIndex] : null, this);
                }
            }

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
                ConfigureGridCellSize(BackpackGrid, backpack, 420f, 388f, DefaultGridCellSize);
                BackpackGrid.Bind(backpack, this);
                CenterGridInArea(BackpackGrid, backpackGridPad, 54f, 226f, 554f, 300f, 34f);
            }

            if (ExternalGrid != null)
            {
                ConfigureGridCellSize(ExternalGrid, externalContainer, 420f, 388f, DefaultGridCellSize);
                ExternalGrid.Bind(externalContainer, this);
                CenterGridInArea(ExternalGrid, externalGridPad, 62f, 108f, 538f, 392f, 34f);
            }

            RefreshHeaders();
            RefreshUseButton();
        }

        public void SelectItem(StorageItemModel item)
        {
            SelectItem(item, null);
        }

        public void SelectItem(StorageItemModel item, StorageGridUI sourceGrid)
        {
            selectedItem = item;
            selectedItemGrid = sourceGrid;
            RefreshUseButton();

            if (SelectedItemNameText == null || SelectedItemMetaText == null)
            {
                return;
            }

            if (item == null)
            {
                SelectedItemNameText.text = StorageTextCatalog.Get(StorageTextId.NoItemSelected);
                SelectedItemMetaText.text = StorageTextCatalog.Get(StorageTextId.InspectItemHint);
                return;
            }

            SelectedItemNameText.text = item.GetDisplayName();
            SelectedItemMetaText.text = BuildSelectedItemMeta(
                item,
                sourceGrid != null ? sourceGrid.Container : null);
        }

        public void ShowStatus(string message, bool warning)
        {
            if (StatusText == null)
            {
                return;
            }

            StatusText.text = message;
            StatusText.color = warning ? StoragePalette.InvalidBorder : StoragePalette.TextSecondary;
        }

        public bool TryUseSelectedItem()
        {
            if (!StorageItemUseUtility.TryUse(selectedItem, selectedItemGrid, out string statusMessage))
            {
                ShowStatus(statusMessage, true);
                return false;
            }

            RefreshAllGrids();
            SelectItem(null);
            ShowStatus(statusMessage, false);
            return true;
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
                SelectItem(item, view.OwnerGrid);
                ShowStatus(StorageTextCatalog.Format(StorageTextId.Rotated, item.GetDisplayName()), false);
                return true;
            }

            item.Rotate();
            ShowStatus(StorageTextCatalog.Get(StorageTextId.RotationBlocked), true);
            RefreshAllGrids();
            SelectItem(item, view.OwnerGrid);
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

            if (externalContainer == null)
            {
                if (StoragePlayerInventoryUtility.IsHandGrid(sourceGrid))
                {
                    return showingBackpack
                        ? TryMoveToFirstFitContainers(sourceGrid, new[] { backpack }, item)
                        : TryMoveToFirstFitGrids(sourceGrid, PocketGrids, item);
                }

                return TryMoveToFirstFitContainers(sourceGrid, hands, item);
            }

            if (sourceGrid == ExternalGrid)
            {
                if (showingBackpack)
                {
                    if (!backpackEquipped || BackpackGrid == null)
                    {
                        ShowStatus(StorageTextCatalog.Get(StorageTextId.NoBackpack), true);
                        return false;
                    }

                    return TryMoveToFirstFitContainers(sourceGrid, new[] { backpack }, item);
                }

                return TryMoveToFirstFitGrids(sourceGrid, PocketGrids, item);
            }

            return TryMoveToFirstFitContainers(sourceGrid, new[] { externalContainer }, item);
        }

        private bool TryMoveToFirstFitGrids(StorageGridUI sourceGrid, StorageGridUI[] targetGrids, StorageItemModel item)
        {
            if (sourceGrid == null || sourceGrid.Container == null || targetGrids == null)
            {
                ShowStatus(StorageTextCatalog.Get(StorageTextId.TargetBlocked), true);
                return false;
            }

            List<StorageContainerModel> targetContainers = new List<StorageContainerModel>(targetGrids.Length);
            for (int targetIndex = 0; targetIndex < targetGrids.Length; targetIndex++)
            {
                StorageGridUI targetGrid = targetGrids[targetIndex];
                if (targetGrid != null && targetGrid != sourceGrid && targetGrid.Container != null)
                {
                    targetContainers.Add(targetGrid.Container);
                }
            }

            return TryMoveToFirstFitContainers(sourceGrid, targetContainers.ToArray(), item);
        }

        private bool TryMoveToFirstFitContainers(StorageGridUI sourceGrid, StorageContainerModel[] targetContainers, StorageItemModel item)
        {
            if (sourceGrid == null || sourceGrid.Container == null || targetContainers == null)
            {
                ShowStatus(StorageTextCatalog.Get(StorageTextId.TargetBlocked), true);
                return false;
            }

            List<StorageContainerModel> filteredTargets = new List<StorageContainerModel>(targetContainers.Length);
            for (int i = 0; i < targetContainers.Length; i++)
            {
                StorageContainerModel targetContainer = targetContainers[i];
                if (targetContainer != null && targetContainer != sourceGrid.Container)
                {
                    filteredTargets.Add(targetContainer);
                }
            }

            if (TransferHandler.TryMoveToFirstFit(
                    item,
                    sourceGrid.Container,
                    filteredTargets.ToArray(),
                    CreateTransferContext(StorageTransferReason.QuickTransfer),
                    out StorageTransferResult result))
            {
                RefreshAllGrids();
                SelectItem(item);
                ShowStatus(result.Message, result.Illegal && result.Observed);
                return true;
            }

            ShowStatus(string.IsNullOrWhiteSpace(result.Message) ? StorageTextCatalog.Get(StorageTextId.TargetBlocked) : result.Message, true);
            RefreshAllGrids();
            SelectItem(item);
            return false;
        }

        private IStorageTransferHandler TransferHandler => transferHandler ?? StorageDefaultTransferHandler.Instance;

        private void RefreshActorCarriedEvidenceState()
        {
            CampusCharacterRuntime runtime = ResolveTransferActorRuntime();
            CampusCharacterCurrentRoomTracker.SyncRuntime(runtime);
        }

        private CampusCharacterRuntime ResolveTransferActorRuntime()
        {
            return transferActor != null
                ? transferActor.GetComponentInParent<CampusCharacterRuntime>()
                : null;
        }

        private static string BuildSelectedItemMeta(
            StorageItemModel item,
            StorageContainerModel container)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string description = item.GetDescription();
            string meta = item.CurrentWidth + "x" + item.CurrentHeight + "  " +
                          item.Weight.ToString("0.#") + "kg";
            if (!string.IsNullOrWhiteSpace(description))
            {
                meta += "  " + description;
            }

            if (item.IsUsable)
            {
                meta += "  [" + StorageTextCatalog.Get(StorageTextId.UsableFromHand) + "]";
            }

            if (CampusProtectedTransferState.ShouldDisplayPendingCheckout(item, container))
            {
                meta += "  [" + StorageTextCatalog.Get(StorageTextId.PendingCheckout);
                if (!string.IsNullOrWhiteSpace(item.SourceLocation))
                {
                    meta += " " + StorageTextCatalog.Get(StorageTextId.From) + " " + item.SourceLocation;
                }

                meta += "]";
            }
            else if (item.IsStolenEvidence)
            {
                meta += "  [" + StorageTextCatalog.Get(StorageTextId.Stolen);
                if (!string.IsNullOrWhiteSpace(item.SourceLocation))
                {
                    meta += " " + StorageTextCatalog.Get(StorageTextId.From) + " " + item.SourceLocation;
                }

                if (item.SuspicionRisk > 0)
                {
                    meta += ", " + StorageTextCatalog.Get(StorageTextId.Risk) + " " + item.SuspicionRisk;
                }

                meta += "]";
            }

            return meta;
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

        private static void CenterGridInArea(StorageGridUI grid, RectTransform backing, float x, float y, float width, float height, float padding)
        {
            if (grid == null || grid.Container == null)
            {
                return;
            }

            Vector2 gridSize = grid.RectTransform.sizeDelta;
            float gridX = x + Mathf.Max(0f, width - gridSize.x) * 0.5f;
            float gridY = y + Mathf.Max(0f, height - gridSize.y) * 0.5f;
            grid.RectTransform.anchoredPosition = new Vector2(gridX, -gridY);

            if (backing == null)
            {
                return;
            }

            backing.sizeDelta = new Vector2(gridSize.x + padding, gridSize.y + padding);
            backing.anchoredPosition = new Vector2(gridX - padding * 0.5f, -(gridY - padding * 0.5f));
            backing.gameObject.SetActive(gridSize.x > 0f && gridSize.y > 0f);
        }

        private void RefreshLocalizedText()
        {
            SetText(windowEyebrowText, StorageTextCatalog.Get(StorageTextId.WindowEyebrow));
            SetText(windowTitleText, StorageTextCatalog.Get(StorageTextId.WindowTitle));
            SetText(windowHintText, StorageTextCatalog.Get(StorageTextId.WindowHint));
            SetText(noBackpackTitleText, StorageTextCatalog.Get(StorageTextId.NoBackpack));
            SetText(noBackpackMetaText, StorageTextCatalog.Get(StorageTextId.NoBackpackPage));
            SetText(itemInfoLabelText, StorageTextCatalog.Get(StorageTextId.ItemInfo));
            SetText(statusLabelText, StorageTextCatalog.Get(StorageTextId.Status));
            SetText(PocketTabLabel, StorageTextCatalog.Get(StorageTextId.PocketTab));
            if (UseItemButton != null)
            {
                SetText(UseItemButton.GetComponentInChildren<Text>(), StorageTextCatalog.Get(StorageTextId.Use));
            }

            SetText(pocketTitleTexts[0], StorageTextCatalog.Get(StorageTextId.LeftChestPocket));
            SetText(pocketTitleTexts[1], StorageTextCatalog.Get(StorageTextId.RightChestPocket));
            SetText(pocketTitleTexts[2], StorageTextCatalog.Get(StorageTextId.LeftPantsPocket));
            SetText(pocketTitleTexts[3], StorageTextCatalog.Get(StorageTextId.RightPantsPocket));
            RefreshHeaders();
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private void ShowAnimated()
        {
            if (visibleRoot != null)
            {
                visibleRoot.SetActive(true);
            }

            if (DragLayer != null)
            {
                DragLayer.gameObject.SetActive(true);
            }

            if (CanvasGroup == null || WindowPanel == null)
            {
                if (CanvasGroup != null)
                {
                    CanvasGroup.alpha = 1f;
                }

                return;
            }

            KillTween();
            CanvasGroup.alpha = 0f;
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
            WindowPanel.localScale = Vector3.one * 0.965f;
            visibilityTween = CampusUiTweenUtility.OpenPanel(CanvasGroup, WindowPanel, 0.24f, 0.965f);
        }

        private void HideAnimated()
        {
            if (CanvasGroup == null || WindowPanel == null)
            {
                HideImmediate();
                return;
            }

            KillTween();
            visibilityTween = CampusUiTweenUtility.ClosePanel(CanvasGroup, WindowPanel, 0.18f, 0.965f);
            visibilityTween.OnComplete(HideImmediate);
        }

        private void KillTween()
        {
            if (visibilityTween != null && visibilityTween.IsActive())
            {
                visibilityTween.Kill();
            }

            visibilityTween = null;
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
                PocketTabButton.interactable = true;
                ApplyTabVisual(PocketTabButton, !showingBackpack, true);
            }

            if (BackpackTabButton != null)
            {
                BackpackTabButton.interactable = backpackEquipped;
                ApplyTabVisual(BackpackTabButton, showingBackpack, backpackEquipped);
            }

            if (BackpackTabLabel != null)
            {
                BackpackTabLabel.text = backpackEquipped
                    ? StorageTextCatalog.Get(StorageTextId.BackpackTab)
                    : StorageTextCatalog.Get(StorageTextId.BackpackUnavailableTab);
            }

            RefreshAllGrids();
        }

        private void RefreshHeaders()
        {
            if (LeftTitleText != null)
            {
                LeftTitleText.text = showingBackpack
                    ? StorageTextCatalog.Get(StorageTextId.BackpackTab)
                    : StorageTextCatalog.Get(StorageTextId.PocketTab);
            }

            if (LeftMetaText != null)
            {
                if (showingBackpack)
                {
                    LeftMetaText.text = backpackEquipped && backpack != null
                        ? backpack.Columns + "x" + backpack.Rows + "  " + backpack.CurrentWeight.ToString("0.#") + "/" + backpack.MaxWeight.ToString("0.#") + "kg"
                        : StorageTextCatalog.Get(StorageTextId.NoBackpack);
                }
                else
                {
                    LeftMetaText.text = string.Empty;
                }
            }

            if (RightTitleText != null)
            {
                RightTitleText.text = externalContainer != null
                    ? externalContainer.GetDisplayName()
                    : StorageTextCatalog.Get(StorageTextId.ExternalContainer);
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

            ConfigureOverlayCanvas(Canvas, WindowSortingOrder);

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            ConfigureCanvasScaler(scaler);

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            EnsureEventSystem();
        }

        private static void ConfigureOverlayCanvas(Canvas targetCanvas, int sortingOrder)
        {
            if (targetCanvas == null)
            {
                return;
            }

            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = sortingOrder;
        }

        private static void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void CreateWindow()
        {
            RectTransform shadow = CreateBox(Root, "WindowShadow", new Vector2(12f, -10f), new Vector2(1510f, 830f),
                StoragePalette.WindowShadow, Color.clear, 18f, 0f, true);

            WindowPanel = CreateBox(Root, "StorageWindow", Vector2.zero, new Vector2(1500f, 820f),
                StoragePalette.Window, StoragePalette.WindowBorder, 18f, 1.6f, true);
            shadow.SetSiblingIndex(1);
            WindowPanel.SetAsLastSibling();

            CreateWindowTrim(WindowPanel);
            CreateHeader(WindowPanel);
            CreateBody(WindowPanel);
            CreateFooter(WindowPanel);
        }

        private void CreateWindowTrim(RectTransform parent)
        {
            CreateBox(parent, "WindowSoftVignette", new Vector2(18f, -18f), new Vector2(1464f, 784f),
                new Color(0.05f, 0.065f, 0.06f, 0.1f), Color.clear, 14f, 0f, false);
        }

        private void CreateHeader(RectTransform parent)
        {
            RectTransform header = CreateBox(parent, "Header", new Vector2(28f, -24f), new Vector2(1444f, 82f),
                new Color(StoragePalette.PanelRaised.r, StoragePalette.PanelRaised.g, StoragePalette.PanelRaised.b, 0.64f), StoragePalette.PanelBorder, 14f, 1f, false);

            CreateBox(header, "HeaderAccent", new Vector2(18f, -14f), new Vector2(4f, 54f),
                StoragePalette.Accent, Color.clear, 2f, 0f, false);

            windowEyebrowText = StorageUIUtility.CreateText("WindowLabel", header, StorageTextCatalog.Get(StorageTextId.WindowEyebrow), 12, TextAnchor.MiddleLeft, StoragePalette.Paper);
            StorageUIUtility.SetTopLeft(windowEyebrowText.rectTransform, 34f, 10f, 240f, 18f);

            windowTitleText = StorageUIUtility.CreateText("TitleText", header, StorageTextCatalog.Get(StorageTextId.WindowTitle), 30, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(windowTitleText.rectTransform, 34f, 29f, 340f, 38f);

            windowHintText = StorageUIUtility.CreateText("HintText", header, StorageTextCatalog.Get(StorageTextId.WindowHint), 15, TextAnchor.MiddleLeft, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(windowHintText.rectTransform, 420f, 42f, 730f, 24f);

            Button close = StorageUIUtility.CreateButton("CloseButton", header, "×", Close,
                StoragePalette.ButtonNormal, StoragePalette.PanelBorder);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(closeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            closeRect.anchoredPosition = new Vector2(-20f, -21f);
            closeRect.sizeDelta = new Vector2(42f, 34f);
            StorageUIUtility.StyleButton(close, StoragePalette.ButtonNormal, Color.clear, 0f, 9f, StoragePalette.TextSecondary);
        }

        private void CreateBody(RectTransform parent)
        {
            RectTransform leftPanel = CreateBox(parent, "LeftPanel_PlayerStorage", new Vector2(28f, -122f), new Vector2(662f, 552f),
                StoragePalette.Panel, StoragePalette.PanelBorder, 14f, 1f, false);
            RectTransform center = CreateBox(parent, "CenterTransferHint", new Vector2(710f, -122f), new Vector2(80f, 552f),
                new Color(0.12f, 0.145f, 0.14f, 0.18f), Color.clear, 14f, 0f, false);
            RectTransform rightPanel = CreateBox(parent, "RightPanel_Container", new Vector2(810f, -122f), new Vector2(662f, 552f),
                StoragePalette.Panel, StoragePalette.PanelBorder, 14f, 1f, false);

            CreateTransferColumn(center);
            CreateLeftPanel(leftPanel);
            CreateRightPanel(rightPanel);
        }

        private void CreateTransferColumn(RectTransform center)
        {
            RectTransform badge = CreateBox(center, "TransferBadge", new Vector2(17f, -248f), new Vector2(46f, 46f),
                new Color(0.16f, 0.19f, 0.18f, 0.46f), Color.clear, 16f, 0f, false);
            Text arrow = StorageUIUtility.CreateText("TransferArrow", badge, "⇄", 24, TextAnchor.MiddleCenter, StoragePalette.Accent);
            arrow.rectTransform.offsetMin = Vector2.zero;
            arrow.rectTransform.offsetMax = Vector2.zero;
        }

        private void CreateLeftPanel(RectTransform panel)
        {
            CreatePanelHeader(panel, "LeftTitleStrip");

            LeftTitleText = StorageUIUtility.CreateText("LeftTitle", panel, StorageTextCatalog.Get(StorageTextId.PocketTab), 22, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(LeftTitleText.rectTransform, 22f, 12f, 240f, 34f);

            LeftMetaText = StorageUIUtility.CreateText("LeftMeta", panel, string.Empty, 14, TextAnchor.MiddleRight, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(LeftMetaText.rectTransform, 360f, 16f, 280f, 28f);

            RectTransform tabs = StorageUIUtility.CreateRectObject("Tabs", panel).GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            tabs.anchoredPosition = new Vector2(22f, -58f);
            tabs.sizeDelta = new Vector2(260f, 36f);

            PocketTabButton = StorageUIUtility.CreateButton("PocketTab", tabs, StorageTextCatalog.Get(StorageTextId.PocketTab), ShowPocketPage,
                StoragePalette.TabSelected, StoragePalette.SlotHoverBorder);
            PocketTabButton.GetComponent<RectTransform>().sizeDelta = new Vector2(86f, 34f);
            PocketTabLabel = PocketTabButton.GetComponentInChildren<Text>();

            BackpackTabButton = StorageUIUtility.CreateButton("BackpackTab", tabs, StorageTextCatalog.Get(StorageTextId.BackpackTab), ShowBackpackPage,
                StoragePalette.TabNormal, StoragePalette.PanelBorder);
            RectTransform backpackTabRect = BackpackTabButton.GetComponent<RectTransform>();
            backpackTabRect.anchoredPosition = new Vector2(94f, 0f);
            backpackTabRect.sizeDelta = new Vector2(132f, 34f);
            BackpackTabLabel = BackpackTabButton.GetComponentInChildren<Text>();

            pocketPage = StorageUIUtility.CreateRectObject("PocketPage", panel).gameObject;
            RectTransform pocketRect = pocketPage.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(pocketRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            pocketRect.anchoredPosition = Vector2.zero;
            pocketRect.sizeDelta = panel.sizeDelta;

            CreatePocketGrid(pocketRect, 0, StorageTextCatalog.Get(StorageTextId.LeftChestPocket), new Vector2(22f, -98f));
            CreatePocketGrid(pocketRect, 1, StorageTextCatalog.Get(StorageTextId.RightChestPocket), new Vector2(338f, -98f));
            CreatePocketGrid(pocketRect, 2, StorageTextCatalog.Get(StorageTextId.LeftPantsPocket), new Vector2(22f, -256f));
            CreatePocketGrid(pocketRect, 3, StorageTextCatalog.Get(StorageTextId.RightPantsPocket), new Vector2(338f, -256f));

            backpackPage = StorageUIUtility.CreateRectObject("BackpackPage", panel).gameObject;
            RectTransform backpackPageRect = backpackPage.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(backpackPageRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            backpackPageRect.anchoredPosition = Vector2.zero;
            backpackPageRect.sizeDelta = panel.sizeDelta;

            backpackGridPad = CreateBox(backpackPageRect, "BackpackGridSoftPad", new Vector2(150f, -238f), new Vector2(360f, 300f),
                new Color(0.095f, 0.115f, 0.112f, 0.24f), Color.clear, 16f, 0f, false);
            BackpackGrid = CreateGrid(backpackPageRect, "BackpackGrid", new Vector2(193f, -250f), 52f, 4f);
            RectTransform hintPanel = CreateBox(backpackPageRect, "NoBackpackHint", new Vector2(180f, -280f), new Vector2(300f, 100f),
                StoragePalette.PanelRaised, StoragePalette.PanelBorder, 1f, 3f, false);
            noBackpackHint = hintPanel.gameObject;
            noBackpackTitleText = StorageUIUtility.CreateText("HintText", hintPanel, StorageTextCatalog.Get(StorageTextId.NoBackpack), 22, TextAnchor.MiddleCenter, StoragePalette.TextMuted);
            StorageUIUtility.SetTopLeft(noBackpackTitleText.rectTransform, 20f, 24f, 260f, 34f);
            noBackpackMetaText = StorageUIUtility.CreateText("HintMeta", hintPanel, StorageTextCatalog.Get(StorageTextId.NoBackpackPage), 14, TextAnchor.MiddleCenter, StoragePalette.TextMuted);
            StorageUIUtility.SetTopLeft(noBackpackMetaText.rectTransform, 20f, 61f, 260f, 24f);
        }

        private void CreateRightPanel(RectTransform panel)
        {
            CreatePanelHeader(panel, "RightTitleStrip");

            RightTitleText = StorageUIUtility.CreateText("RightTitle", panel, StorageTextCatalog.Get(StorageTextId.ExternalContainer), 22, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(RightTitleText.rectTransform, 22f, 12f, 260f, 34f);

            RightMetaText = StorageUIUtility.CreateText("RightMeta", panel, "4x4  0/12kg", 14, TextAnchor.MiddleRight, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(RightMetaText.rectTransform, 340f, 16f, 300f, 28f);

            externalGridPad = CreateBox(panel, "GridSoftPad", new Vector2(150f, -130f), new Vector2(360f, 300f),
                new Color(0.095f, 0.115f, 0.112f, 0.26f), Color.clear, 16f, 0f, false);
            ExternalGrid = CreateGrid(panel, "ContainerGrid", new Vector2(221f, -154f), 52f, 4f);
        }

        private void CreateFooter(RectTransform parent)
        {
            RectTransform footer = CreateBox(parent, "Footer", new Vector2(28f, -698f), new Vector2(1444f, 94f),
                StoragePalette.Panel, StoragePalette.PanelBorder, 14f, 1f, false);

            itemInfoLabelText = StorageUIUtility.CreateText("InfoLabel", footer, StorageTextCatalog.Get(StorageTextId.ItemInfo), 12, TextAnchor.MiddleLeft, StoragePalette.PaperDim);
            StorageUIUtility.SetTopLeft(itemInfoLabelText.rectTransform, 24f, 12f, 160f, 18f);

            SelectedItemNameText = StorageUIUtility.CreateText("SelectedItemName", footer, StorageTextCatalog.Get(StorageTextId.NoItemSelected), 18, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(SelectedItemNameText.rectTransform, 24f, 28f, 560f, 28f);

            SelectedItemMetaText = StorageUIUtility.CreateText("SelectedItemMeta", footer, StorageTextCatalog.Get(StorageTextId.InspectItemHint), 14, TextAnchor.MiddleLeft, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(SelectedItemMetaText.rectTransform, 24f, 55f, 760f, 24f);

            UseItemButton = StorageUIUtility.CreateButton("UseItemButton", footer, StorageTextCatalog.Get(StorageTextId.Use), () => TryUseSelectedItem(),
                StoragePalette.TabNormal, Color.clear);
            RectTransform useRect = UseItemButton.GetComponent<RectTransform>();
            StorageUIUtility.SetTopLeft(useRect, 960f, 31f, 100f, 36f);
            RefreshUseButton();

            statusLabelText = StorageUIUtility.CreateText("StatusLabel", footer, StorageTextCatalog.Get(StorageTextId.Status), 12, TextAnchor.MiddleRight, StoragePalette.PaperDim);
            StorageUIUtility.SetTopLeft(statusLabelText.rectTransform, 1080f, 12f, 340f, 18f);

            StatusText = StorageUIUtility.CreateText("StatusHint", footer, string.Empty, 15, TextAnchor.MiddleRight, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(StatusText.rectTransform, 1080f, 35f, 340f, 44f);
        }

        private void CreateDragLayer()
        {
            GameObject dragLayerObject = new GameObject("StorageDragOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            DragLayer = dragLayerObject.GetComponent<RectTransform>();
            DragLayer.anchorMin = Vector2.zero;
            DragLayer.anchorMax = Vector2.one;
            DragLayer.pivot = new Vector2(0.5f, 0.5f);
            DragLayer.anchoredPosition = Vector2.zero;
            DragLayer.offsetMin = Vector2.zero;
            DragLayer.offsetMax = Vector2.zero;

            DragCanvas = dragLayerObject.GetComponent<Canvas>();
            ConfigureOverlayCanvas(DragCanvas, DragSortingOrder);

            CanvasScaler dragScaler = dragLayerObject.GetComponent<CanvasScaler>();
            ConfigureCanvasScaler(dragScaler);

            DragController = gameObject.GetComponent<StorageDragController>();
            if (DragController == null)
            {
                DragController = gameObject.AddComponent<StorageDragController>();
            }

            DragController.Window = this;
            DragController.DragLayer = DragLayer;
            DragController.Canvas = DragCanvas != null ? DragCanvas : Canvas;
        }

        private void CreatePocketGrid(RectTransform parent, int index, string title, Vector2 position)
        {
            RectTransform panel = CreateBox(parent, "PocketGrid_" + title, position, new Vector2(286f, 148f),
                StoragePalette.PanelRaised, StoragePalette.PanelBorder, 12f, 0.8f, false);
            RectTransform strip = CreateBox(panel, "PocketTitleStrip", new Vector2(12f, -10f), new Vector2(112f, 26f),
                new Color(StoragePalette.PanelHeader.r, StoragePalette.PanelHeader.g, StoragePalette.PanelHeader.b, 0.72f), Color.clear, 8f, 0f, false);
            Text label = StorageUIUtility.CreateText("PocketTitle", strip, title, 15, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(label.rectTransform, 12f, 2f, 90f, 22f);
            if (index >= 0 && index < pocketTitleTexts.Length)
            {
                pocketTitleTexts[index] = label;
            }

            PocketGrids[index] = CreateGrid(panel, "Grid", new Vector2(105f, -28f), 36f, 4f);
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

        private void CreatePanelHeader(RectTransform panel, string name)
        {
            CreateBox(panel, name, new Vector2(18f, -18f), new Vector2(panel.sizeDelta.x - 36f, 2f),
                StoragePalette.Divider, Color.clear, 1f, 0f, false);
        }

        private void ApplyTabVisual(Button button, bool selected, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            Color fill = selected ? StoragePalette.TabSelected : StoragePalette.TabNormal;
            Color border = selected ? StoragePalette.SlotHoverBorder : StoragePalette.PanelBorder;
            Color text = enabled ? (selected ? StoragePalette.TextPrimary : StoragePalette.TextSecondary) : StoragePalette.TextMuted;
            StorageUIUtility.StyleButton(button, fill, selected ? border : Color.clear, selected ? 1f : 0f, 9f, text);
        }

        private void RefreshUseButton()
        {
            if (UseItemButton == null)
            {
                return;
            }

            bool canUse = StorageItemUseUtility.CanUse(selectedItem);
            UseItemButton.interactable = canUse;
            StorageUIUtility.StyleButton(
                UseItemButton,
                canUse ? StoragePalette.ButtonNormal : StoragePalette.TabNormal,
                canUse ? StoragePalette.SlotHoverBorder : Color.clear,
                canUse ? 1f : 0f,
                9f,
                canUse ? StoragePalette.TextPrimary : StoragePalette.TextMuted);
        }

        private RectTransform CreateBox(Transform parent, string name, Vector2 position, Vector2 size, Color fill, Color border, float radius, float borderWidth, bool centered)
        {
            return StorageUIUtility.CreateBox(
                name,
                parent,
                centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f),
                centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f),
                centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f),
                position,
                size,
                fill,
                border,
                borderWidth,
                radius);
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
