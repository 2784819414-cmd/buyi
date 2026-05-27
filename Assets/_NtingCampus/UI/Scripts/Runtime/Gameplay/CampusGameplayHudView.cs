using System.Globalization;
using DG.Tweening;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusGameplayHudView : MonoBehaviour
    {
        private const string CanvasRootName = "CampusGameplayHudCanvas";
        private const int SortingOrder = 28500;
        private const int HandNormalSortingOrder = SortingOrder + 10;
        private const int HandStorageSortingOrder = 32020;

        private static readonly Color HudPanelColor = new Color(0.055f, 0.068f, 0.086f, 0.44f);
        private static readonly Color HudRaisedPanelColor = new Color(0.075f, 0.09f, 0.115f, 0.50f);
        private static readonly Color HudPanelBorderColor = new Color(0.82f, 0.76f, 0.64f, 0.10f);
        private static readonly Color HudShadowColor = new Color(0.005f, 0.01f, 0.015f, 0.24f);

        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private Canvas handCanvas;
        [SerializeField] private RectTransform handRoot;
        [SerializeField] private Canvas tooltipCanvas;
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private Text dateText;
        [SerializeField] private Text weekdayText;
        [SerializeField] private Text segmentText;
        [SerializeField] private Text clockText;
        [SerializeField] private Text suspicionValueText;
        [SerializeField] private Text riskLabelText;
        [SerializeField] private Text alertnessValueText;
        [SerializeField] private Text warningValueText;
        [SerializeField] private Text areaText;
        [SerializeField] private Text floorText;
        [SerializeField] private Text headingText;
        [SerializeField] private RectTransform areaBannerRoot;
        [SerializeField] private CanvasGroup areaBannerGroup;
        [SerializeField] private Text areaBannerTitleText;
        [SerializeField] private Text areaBannerSubtitleText;
        [SerializeField] private Text moneyValueText;
        [SerializeField] private Text staminaValueText;
        [SerializeField] private RectTransform staminaTrack;
        [SerializeField] private RectTransform staminaFill;
        [SerializeField] private RectTransform staminaCapRegion;
        [SerializeField] private Text backpackValueText;
        [SerializeField] private RectTransform pendingCheckoutRoot;
        [SerializeField] private CanvasGroup pendingCheckoutGroup;
        [SerializeField] private Text pendingCheckoutTitleText;
        [SerializeField] private Text pendingCheckoutSummaryText;
        [SerializeField] private Text pendingCheckoutStatusText;
        [SerializeField] private StorageGridUI backpackGrid;
        [SerializeField] private Text backpackSlotStatusText;
        [SerializeField] private CampusHudBackpackSlotDragHandler backpackSlotDragHandler;
        [SerializeField] private CampusHudHandSlotDragHandler[] handSlotDragHandlers = new CampusHudHandSlotDragHandler[2];

        private StorageBoxGraphic staminaFillGraphic;
        private StorageBoxGraphic staminaCapRegionGraphic;
        private StorageItemTooltipUI hudItemTooltip;
        private const float AreaBannerHiddenY = 92f;
        private const float AreaBannerVisibleY = 112f;
        private const float PendingCheckoutHiddenX = 320f;
        private const float PendingCheckoutVisibleX = -24f;
        private const float StaminaTrackPadding = 2f;
        private const float StaminaFillHeight = 16f;
        private const float HandModuleWidth = 284f;
        private const float HandModuleHeight = 132f;
        private const float HandModulePadding = 16f;
        private const float HandModuleSlotSize = 76f;
        private const float HandModuleSlotGap = 10f;
        private const float HandModuleLabelTop = 12f;
        private const float HandModuleSlotTop = 40f;
        private const float HandModuleGridInset = 6f;
        private const float HandModuleGridSize = 64f;

        private Sequence pendingCheckoutTween;
        private Sequence areaBannerTween;
        private readonly StorageGridUI[] handGrids = new StorageGridUI[2];
        private string lastAreaBannerKey = string.Empty;
        private bool hasAreaBannerKey;
        private bool lastPendingCheckoutVisible;
        private bool hasPendingCheckoutVisibility;

        public bool LastStorageWindowOpen { get; private set; }

        public void Apply(
            CampusGameplayHudSnapshot snapshot,
            StorageContainerModel[] hands,
            StorageContainerModel backpackSlot,
            CampusCharacterRuntime playerRuntime,
            StorageWindowUI storageWindow,
            bool immediate,
            bool refreshHands)
        {
            EnsureVisual();
            ApplyText(snapshot);

            if (refreshHands)
            {
                ApplyHands(hands, storageWindow, playerRuntime);
                ApplyBackpackSlot(backpackSlot, playerRuntime);
            }

            ApplyHandLayer(storageWindow);
            UpdateAreaBanner(snapshot, immediate);
            UpdatePendingCheckoutBar(snapshot, immediate);
            LastStorageWindowOpen = storageWindow != null && storageWindow.IsOpen;
        }

        private void OnDisable()
        {
            if (pendingCheckoutTween != null && pendingCheckoutTween.IsActive())
            {
                pendingCheckoutTween.Kill();
            }

            if (areaBannerTween != null && areaBannerTween.IsActive())
            {
                areaBannerTween.Kill();
            }
        }

        private void EnsureVisual()
        {
            if (canvas != null &&
                canvasRoot != null &&
                handCanvas != null &&
                handRoot != null &&
                dateText != null &&
                moneyValueText != null &&
                staminaValueText != null &&
                staminaTrack != null &&
                staminaFill != null &&
                staminaCapRegion != null &&
                pendingCheckoutRoot != null &&
                pendingCheckoutGroup != null &&
                pendingCheckoutTitleText != null &&
                pendingCheckoutSummaryText != null &&
                pendingCheckoutStatusText != null &&
                backpackGrid != null &&
                handGrids[0] != null &&
                handGrids[1] != null)
            {
                return;
            }

            Transform existingRoot = transform.Find(CanvasRootName);
            GameObject canvasObject;
            if (existingRoot == null)
            {
                canvasObject = new GameObject(
                    CanvasRootName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(transform, false);
            }
            else
            {
                canvasObject = existingRoot.gameObject;
            }

            canvasRoot = canvasObject.GetComponent<RectTransform>();
            canvasRoot.anchorMin = Vector2.zero;
            canvasRoot.anchorMax = Vector2.one;
            canvasRoot.offsetMin = Vector2.zero;
            canvasRoot.offsetMax = Vector2.zero;

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.enabled = true;

            BuildHud();
        }

        private void BuildHud()
        {
            ClearExistingHud();

            RectTransform topLeftStack = CreatePanel("TopLeftStack", new Vector2(24f, -24f), new Vector2(192f, 382f));
            CreateSystemCard(topLeftStack);
            CreateRiskCard(topLeftStack);

            RectTransform topRightCard = CreatePanel("TopRightCard", new Vector2(-24f, -24f), new Vector2(278f, 150f), true);
            CreateNavigationCard(topRightCard);

            RectTransform bottomLeftCard = CreatePanel("BottomLeftCard", new Vector2(24f, 24f), new Vector2(332f, 176f), false, false);
            CreateStateCard(bottomLeftCard);

            CreateHandModule();
            CreateAreaBanner();
            CreatePendingCheckoutBar();
        }

        private void CreateAreaBanner()
        {
            GameObject areaBannerRootObject = StorageUIUtility.CreateRectObject("AreaBannerRoot", canvasRoot);
            areaBannerRoot = areaBannerRootObject.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(areaBannerRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            areaBannerRoot.anchoredPosition = new Vector2(0f, AreaBannerHiddenY);
            areaBannerRoot.sizeDelta = new Vector2(320f, 104f);
            areaBannerGroup = areaBannerRootObject.AddComponent<CanvasGroup>();
            areaBannerGroup.alpha = 0f;
            areaBannerGroup.blocksRaycasts = false;
            areaBannerGroup.interactable = false;

            RectTransform areaBannerShadow = StorageUIUtility.CreateBox(
                "AreaBanner_Shadow",
                areaBannerRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, -6f),
                new Vector2(304f, 92f),
                HudShadowColor,
                Color.clear,
                0f,
                18f);
            areaBannerShadow.SetAsFirstSibling();

            RectTransform areaBanner = StorageUIUtility.CreateBox(
                "AreaBanner",
                areaBannerRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(304f, 92f),
                HudPanelColor,
                HudPanelBorderColor,
                1f,
                18f);
            CreateAreaBannerContent(areaBanner);
        }

        private void CreatePendingCheckoutBar()
        {
            GameObject rootObject = StorageUIUtility.CreateRectObject("PendingCheckoutBarRoot", canvasRoot);
            pendingCheckoutRoot = rootObject.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(pendingCheckoutRoot, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            pendingCheckoutRoot.anchoredPosition = new Vector2(PendingCheckoutHiddenX, 0f);
            pendingCheckoutRoot.sizeDelta = new Vector2(304f, 104f);

            pendingCheckoutGroup = rootObject.AddComponent<CanvasGroup>();
            pendingCheckoutGroup.alpha = 0f;
            pendingCheckoutGroup.blocksRaycasts = false;
            pendingCheckoutGroup.interactable = false;

            RectTransform shadow = StorageUIUtility.CreateBox(
                "PendingCheckoutBar_Shadow",
                pendingCheckoutRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(8f, -6f),
                new Vector2(292f, 92f),
                HudShadowColor,
                Color.clear,
                0f,
                18f);
            shadow.SetAsFirstSibling();

            RectTransform panel = StorageUIUtility.CreateBox(
                "PendingCheckoutBar",
                pendingCheckoutRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(292f, 92f),
                HudRaisedPanelColor,
                HudPanelBorderColor,
                1f,
                18f);

            pendingCheckoutTitleText = CreateLabel(
                panel,
                "PendingCheckoutTitle",
                CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.PendingCheckout),
                13,
                new Vector2(20f, 14f),
                new Vector2(180f, 20f));
            pendingCheckoutSummaryText = CreateValueText(
                panel,
                "PendingCheckoutSummary",
                22,
                new Vector2(20f, 36f),
                new Vector2(236f, 28f),
                StoragePalette.Accent);
            pendingCheckoutStatusText = CreateMetaText(
                panel,
                "PendingCheckoutStatus",
                new Vector2(20f, 66f),
                new Vector2(236f, 18f));
        }

        private RectTransform CreatePanel(
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            bool rightAligned = false,
            bool topAligned = true,
            Vector2? pivotOverride = null)
        {
            Vector2 anchor = new Vector2(rightAligned ? 1f : 0f, topAligned ? 1f : 0f);
            Vector2 pivot = pivotOverride ?? anchor;
            RectTransform shadow = StorageUIUtility.CreateBox(
                name + "_Shadow",
                canvasRoot,
                anchor,
                anchor,
                pivot,
                anchoredPosition + new Vector2(rightAligned ? -6f : 6f, topAligned ? -6f : 6f),
                size,
                HudShadowColor,
                Color.clear,
                0f,
                18f);
            shadow.SetAsFirstSibling();

            return StorageUIUtility.CreateBox(
                name,
                canvasRoot,
                anchor,
                anchor,
                pivot,
                anchoredPosition,
                size,
                HudPanelColor,
                HudPanelBorderColor,
                1f,
                18f);
        }

        private void CreateSystemCard(RectTransform parent)
        {
            parent.sizeDelta = new Vector2(192f, 382f);

            RectTransform systemCard = StorageUIUtility.CreateBox(
                "SystemCard",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(192f, 210f),
                HudRaisedPanelColor,
                HudPanelBorderColor,
                1f,
                18f);

            dateText = CreateValueText(systemCard, "Date", 22, new Vector2(18f, 20f), new Vector2(156f, 32f));
            weekdayText = CreateChip(systemCard, "Weekday", new Vector2(18f, 68f), new Vector2(156f, 34f), 16);
            segmentText = CreateChip(systemCard, "Segment", new Vector2(18f, 110f), new Vector2(156f, 42f), 22);
            clockText = CreateValueText(systemCard, "Clock", 20, new Vector2(42f, 162f), new Vector2(120f, 24f));

            Text clockIcon = StorageUIUtility.CreateText("ClockIcon", systemCard, "◔", 22, TextAnchor.MiddleCenter, StoragePalette.TextPrimary);
            StorageUIUtility.SetTopLeft(clockIcon.rectTransform, 18f, 160f, 20f, 24f);
        }

        private void CreateRiskCard(RectTransform parent)
        {
            RectTransform riskCard = StorageUIUtility.CreateBox(
                "RiskCard",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, -226f),
                new Vector2(192f, 156f),
                HudRaisedPanelColor,
                HudPanelBorderColor,
                1f,
                18f);

            CreateLabel(riskCard, "SuspicionLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Suspicion), 15, new Vector2(18f, 16f), new Vector2(120f, 20f));
            suspicionValueText = CreateValueText(riskCard, "SuspicionValue", 22, new Vector2(18f, 52f), new Vector2(92f, 34f), StoragePalette.Warning);
            riskLabelText = CreateValueText(riskCard, "RiskLabel", 16, new Vector2(18f, 96f), new Vector2(120f, 24f));
            alertnessValueText = CreateMetaText(riskCard, "AlertnessValue", new Vector2(18f, 124f), new Vector2(156f, 18f));
            warningValueText = CreateMetaText(riskCard, "WarningValue", new Vector2(18f, 142f), new Vector2(156f, 18f));
        }

        private void CreateNavigationCard(RectTransform parent)
        {
            CreateLabel(parent, "AreaLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Floor), 12, new Vector2(18f, 16f), new Vector2(160f, 18f));
            areaText = CreateValueText(parent, "Area", 26, new Vector2(18f, 36f), new Vector2(210f, 34f));
            floorText = CreateMetaText(parent, "Floor", new Vector2(18f, 84f), new Vector2(200f, 20f));
            headingText = CreateMetaText(parent, "Heading", new Vector2(18f, 106f), new Vector2(200f, 20f));
        }

        private void CreateStateCard(RectTransform parent)
        {
            CreateLabel(parent, "MoneyLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Money), 12, new Vector2(18f, 16f), new Vector2(120f, 18f));
            moneyValueText = CreateValueText(parent, "Money", 20, new Vector2(18f, 34f), new Vector2(150f, 26f), StoragePalette.Accent);

            CreateLabel(parent, "StaminaLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Stamina), 12, new Vector2(18f, 68f), new Vector2(120f, 18f));
            staminaValueText = CreateValueText(parent, "StaminaValue", 18, new Vector2(178f, 66f), new Vector2(136f, 22f), StoragePalette.Paper);
            staminaValueText.alignment = TextAnchor.MiddleRight;

            staminaTrack = StorageUIUtility.CreateBox(
                "StaminaTrack",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -92f),
                new Vector2(296f, 20f),
                StoragePalette.ButtonNormal,
                StoragePalette.PanelBorder,
                1f,
                10f);

            staminaFill = StorageUIUtility.CreateBox(
                "StaminaFill",
                staminaTrack,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(StaminaTrackPadding, 0f),
                new Vector2(0f, StaminaFillHeight),
                StoragePalette.Accent,
                Color.clear,
                0f,
                8f);
            staminaFillGraphic = staminaFill.GetComponent<StorageBoxGraphic>();

            staminaCapRegion = StorageUIUtility.CreateBox(
                "StaminaCapRegion",
                staminaTrack,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-StaminaTrackPadding, 0f),
                new Vector2(0f, StaminaFillHeight),
                new Color(0.29f, 0.31f, 0.34f, 0.46f),
                Color.clear,
                0f,
                8f);
            staminaCapRegionGraphic = staminaCapRegion.GetComponent<StorageBoxGraphic>();
            staminaCapRegion.SetAsLastSibling();

            CreateLabel(parent, "BackpackLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Backpack), 12, new Vector2(18f, 118f), new Vector2(100f, 18f));
            backpackValueText = CreateValueText(parent, "BackpackValue", 16, new Vector2(18f, 136f), new Vector2(296f, 22f), StoragePalette.TextPrimary);
        }

        private void CreateHandModule()
        {
            GameObject handObject = new GameObject(
                "HandModuleCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            handObject.transform.SetParent(canvasRoot, false);

            handRoot = handObject.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(handRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            handRoot.anchoredPosition = new Vector2(-24f, 24f);
            handRoot.sizeDelta = new Vector2(HandModuleWidth, HandModuleHeight);

            handCanvas = handObject.GetComponent<Canvas>();
            handCanvas.overrideSorting = true;
            handCanvas.sortingOrder = HandNormalSortingOrder;

            CanvasScaler scaler = handObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = StorageUIUtility.CreateBox(
                "HandModule",
                handRoot,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                Vector2.zero,
                new Vector2(HandModuleWidth, HandModuleHeight),
                HudPanelColor,
                HudPanelBorderColor,
                1f,
                16f);

            float leftHandX = HandModulePadding;
            float rightHandX = leftHandX + HandModuleSlotSize + HandModuleSlotGap;
            float backpackX = rightHandX + HandModuleSlotSize + HandModuleSlotGap;

            CreateEquipmentSlotLabel(panel, "LeftHandLabel", StorageTextCatalog.Get(StorageTextId.LeftHand), leftHandX);
            CreateEquipmentSlotLabel(panel, "RightHandLabel", StorageTextCatalog.Get(StorageTextId.RightHand), rightHandX);
            handGrids[0] = CreateHandGrid(panel, "LeftHandDisplay", new Vector2(leftHandX, HandModuleSlotTop), 0);
            handGrids[1] = CreateHandGrid(panel, "RightHandDisplay", new Vector2(rightHandX, HandModuleSlotTop), 1);
            CreateEquipmentSlotLabel(
                panel,
                "BackpackSlotLabel",
                CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Backpack),
                backpackX);
            backpackSlotStatusText = CreateMetaText(
                panel,
                "BackpackSlotStatus",
                new Vector2(backpackX, HandModuleHeight - 24f),
                new Vector2(HandModuleSlotSize, 18f));
            backpackSlotStatusText.alignment = TextAnchor.MiddleCenter;
            backpackGrid = CreateBackpackGrid(panel, "BackpackDisplay", new Vector2(backpackX, HandModuleSlotTop));
            CreateTooltipLayer();
        }

        private void CreateTooltipLayer()
        {
            GameObject tooltipObject = new GameObject(
                "HudItemTooltipCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            tooltipObject.transform.SetParent(canvasRoot, false);

            tooltipRoot = tooltipObject.GetComponent<RectTransform>();
            tooltipRoot.anchorMin = Vector2.zero;
            tooltipRoot.anchorMax = Vector2.one;
            tooltipRoot.offsetMin = Vector2.zero;
            tooltipRoot.offsetMax = Vector2.zero;

            tooltipCanvas = tooltipObject.GetComponent<Canvas>();
            tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = HandStorageSortingOrder + 1;

            CanvasScaler scaler = tooltipObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            hudItemTooltip = StorageItemTooltipUI.Create(tooltipRoot);
        }

        private void CreateAreaBannerContent(RectTransform parent)
        {
            areaBannerTitleText = CreateValueText(parent, "AreaBannerTitle", 20, new Vector2(24f, 18f), new Vector2(240f, 28f));
            areaBannerTitleText.alignment = TextAnchor.MiddleCenter;
            areaBannerSubtitleText = CreateMetaText(parent, "AreaBannerSubtitle", new Vector2(24f, 48f), new Vector2(240f, 20f));
            areaBannerSubtitleText.alignment = TextAnchor.MiddleCenter;
        }

        private void ApplyText(CampusGameplayHudSnapshot snapshot)
        {
            dateText.text = snapshot.DateText;
            weekdayText.text = snapshot.WeekdayText;
            segmentText.text = snapshot.SegmentText;
            clockText.text = snapshot.ClockText;
            suspicionValueText.text = snapshot.SuspicionPercent.ToString(CultureInfo.InvariantCulture) + "%";
            riskLabelText.text = snapshot.RiskLabel;
            alertnessValueText.text = CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.TeacherAlertness) + " " + snapshot.TeacherAlertness;
            warningValueText.text = CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.WarningCount) + " " + snapshot.WarningCount;
            areaText.text = snapshot.AreaName;
            floorText.text = snapshot.FloorLabel;
            headingText.text = CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Facing) + " " + snapshot.HeadingLabel;
            areaBannerTitleText.text = snapshot.AreaName;
            areaBannerSubtitleText.text = snapshot.AreaSubtitle;
            moneyValueText.text = FormatNumber(snapshot.Money);
            backpackValueText.text = snapshot.BackpackStatus;
            pendingCheckoutTitleText.text = CampusGameplayHudTextCatalog.Get(ResolvePendingProtectedTransferTitle(snapshot));
            pendingCheckoutSummaryText.text = FormatPendingProtectedTransferSummary(snapshot);
            pendingCheckoutStatusText.text = CampusGameplayHudTextCatalog.Get(ResolvePendingProtectedTransferStatus(snapshot));
            pendingCheckoutStatusText.color = snapshot.PendingProtectedTransferMode == CampusPendingProtectedTransferHudMode.Registration ||
                                              snapshot.CanAffordCheckout
                ? StoragePalette.TextSecondary
                : StoragePalette.Warning;
            UpdateStaminaBar(snapshot.StaminaCurrent, snapshot.StaminaMax);
        }

        private static CampusGameplayHudTextId ResolvePendingProtectedTransferTitle(CampusGameplayHudSnapshot snapshot)
        {
            return snapshot.PendingProtectedTransferMode == CampusPendingProtectedTransferHudMode.Registration
                ? CampusGameplayHudTextId.PendingRegistration
                : CampusGameplayHudTextId.PendingCheckout;
        }

        private static string FormatPendingProtectedTransferSummary(CampusGameplayHudSnapshot snapshot)
        {
            return snapshot.PendingProtectedTransferMode == CampusPendingProtectedTransferHudMode.Registration
                ? CampusGameplayHudTextCatalog.Format(
                    CampusGameplayHudTextId.PendingRegistrationSummary,
                    snapshot.PendingCheckoutCount)
                : CampusGameplayHudTextCatalog.Format(
                    CampusGameplayHudTextId.PendingCheckoutSummary,
                    snapshot.PendingCheckoutCount,
                    FormatNumber(snapshot.PendingCheckoutTotal));
        }

        private static CampusGameplayHudTextId ResolvePendingProtectedTransferStatus(CampusGameplayHudSnapshot snapshot)
        {
            if (snapshot.PendingProtectedTransferMode == CampusPendingProtectedTransferHudMode.Registration)
            {
                return CampusGameplayHudTextId.ReadyToRegister;
            }

            return snapshot.CanAffordCheckout
                ? CampusGameplayHudTextId.ReadyToPay
                : CampusGameplayHudTextId.NotEnoughMoney;
        }

        private void UpdatePendingCheckoutBar(CampusGameplayHudSnapshot snapshot, bool immediate)
        {
            if (pendingCheckoutRoot == null || pendingCheckoutGroup == null)
            {
                return;
            }

            bool visible = snapshot.ShowPendingCheckout;
            bool visibilityChanged = !hasPendingCheckoutVisibility || lastPendingCheckoutVisible != visible;
            lastPendingCheckoutVisible = visible;
            hasPendingCheckoutVisibility = true;
            if (!visibilityChanged && !immediate)
            {
                return;
            }

            if (pendingCheckoutTween != null && pendingCheckoutTween.IsActive())
            {
                pendingCheckoutTween.Kill();
            }

            float targetAlpha = visible ? 1f : 0f;
            float targetX = visible ? PendingCheckoutVisibleX : PendingCheckoutHiddenX;
            if (immediate)
            {
                pendingCheckoutGroup.alpha = targetAlpha;
                pendingCheckoutRoot.anchoredPosition = new Vector2(targetX, 0f);
                return;
            }

            pendingCheckoutTween = DOTween.Sequence().SetUpdate(true);
            pendingCheckoutTween.Join(pendingCheckoutGroup.DOFade(targetAlpha, visible ? 0.22f : 0.16f).SetEase(visible ? Ease.OutCubic : Ease.InCubic));
            pendingCheckoutTween.Join(pendingCheckoutRoot.DOAnchorPosX(targetX, visible ? 0.22f : 0.16f).SetEase(visible ? Ease.OutCubic : Ease.InCubic));
        }

        private void UpdateStaminaBar(int currentStamina, int maxStamina)
        {
            int normalizedCurrent = Mathf.Max(0, currentStamina);
            int normalizedMax = Mathf.Max(0, maxStamina);
            float ratio = normalizedMax > 0 ? Mathf.Clamp01((float)normalizedCurrent / normalizedMax) : 0f;

            if (staminaValueText != null)
            {
                staminaValueText.text = normalizedCurrent + "/" + normalizedMax;
            }

            if (staminaFill != null)
            {
                float trackWidth = Mathf.Max(0f, staminaTrack != null ? staminaTrack.sizeDelta.x - StaminaTrackPadding * 2f : 0f);
                float baseMax = Mathf.Max(1f, CampusCharacterStaminaTuning.BaseMaxStamina);
                float usableWidth = trackWidth * Mathf.Clamp01((float)normalizedMax / baseMax);
                float fillWidth = trackWidth * Mathf.Clamp01((float)normalizedCurrent / baseMax);
                staminaFill.sizeDelta = new Vector2(fillWidth, StaminaFillHeight);
                staminaFill.gameObject.SetActive(trackWidth > 0.01f);

                if (staminaCapRegion != null)
                {
                    float capWidth = Mathf.Max(0f, trackWidth - usableWidth);
                    staminaCapRegion.sizeDelta = new Vector2(capWidth, StaminaFillHeight);
                    staminaCapRegion.gameObject.SetActive(capWidth > 0.01f);
                    if (staminaCapRegionGraphic != null)
                    {
                        staminaCapRegionGraphic.SetStyle(new Color(0.29f, 0.31f, 0.34f, 0.46f), Color.clear, 0f, 8f);
                    }
                }
            }

            if (staminaFillGraphic != null)
            {
                Color fillColor = ratio <= 0.25f ? StoragePalette.Warning : StoragePalette.Accent;
                staminaFillGraphic.SetStyle(fillColor, Color.clear, 0f, 8f);
            }
        }

        private void ApplyHands(StorageContainerModel[] hands, StorageWindowUI storageWindow, CampusCharacterRuntime playerRuntime)
        {
            StorageWindowUI ownerWindow = storageWindow != null && storageWindow.IsOpen
                ? storageWindow
                : null;

            BindHandGrid(0, CampusHandInventoryUtility.ResolveHandContainer(hands, 0), ownerWindow);
            BindHandGrid(1, CampusHandInventoryUtility.ResolveHandContainer(hands, 1), ownerWindow);
            ConfigureHandDragHandler(0, CampusHandInventoryUtility.ResolveHandContainer(hands, 0), ownerWindow, playerRuntime);
            ConfigureHandDragHandler(1, CampusHandInventoryUtility.ResolveHandContainer(hands, 1), ownerWindow, playerRuntime);
        }

        private void ApplyBackpackSlot(StorageContainerModel backpackSlot, CampusCharacterRuntime playerRuntime)
        {
            if (backpackGrid != null)
            {
                backpackGrid.Bind(backpackSlot, null);
            }

            StorageItemModel backpack = CampusBackpackInventoryUtility.ResolveEquippedBackpack(backpackSlot);
            if (backpackSlotStatusText != null)
            {
                backpackSlotStatusText.text = backpack != null
                    ? CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.EquippedBackpack)
                    : CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.NoBackpack);
                backpackSlotStatusText.color = backpack != null ? StoragePalette.Accent : StoragePalette.TextMuted;
            }

            if (backpackSlotDragHandler != null)
            {
                backpackSlotDragHandler.Configure(
                playerRuntime,
                backpackSlot,
                tooltipCanvas,
                tooltipRoot,
                ResolveEquipmentHitRect(backpackGrid),
                hudItemTooltip,
                tooltipRoot);
            }
        }

        private void ApplyHandLayer(StorageWindowUI storageWindow)
        {
            if (handCanvas == null || handRoot == null)
            {
                return;
            }

            bool storageOpen = storageWindow != null && storageWindow.IsOpen;
            handCanvas.sortingOrder = storageOpen ? HandStorageSortingOrder : HandNormalSortingOrder;
            handRoot.SetAsLastSibling();
        }

        private void UpdateAreaBanner(CampusGameplayHudSnapshot snapshot, bool immediate)
        {
            if (areaBannerRoot == null || areaBannerGroup == null)
            {
                return;
            }

            string areaKey = snapshot.AreaName + "|" + snapshot.FloorLabel;
            bool areaChanged = !hasAreaBannerKey || !string.Equals(lastAreaBannerKey, areaKey, System.StringComparison.Ordinal);
            lastAreaBannerKey = areaKey;
            hasAreaBannerKey = true;

            if (!areaChanged)
            {
                return;
            }

            if (areaBannerTween != null && areaBannerTween.IsActive())
            {
                areaBannerTween.Kill();
            }

            if (immediate)
            {
                areaBannerGroup.alpha = 0f;
                areaBannerRoot.anchoredPosition = new Vector2(0f, AreaBannerHiddenY);
            }

            areaBannerGroup.alpha = 0f;
            areaBannerRoot.anchoredPosition = new Vector2(0f, AreaBannerHiddenY);
            areaBannerTween = DOTween.Sequence().SetUpdate(true);
            areaBannerTween.Join(areaBannerGroup.DOFade(1f, 0.22f).SetEase(Ease.OutCubic));
            areaBannerTween.Join(areaBannerRoot.DOAnchorPosY(AreaBannerVisibleY, 0.22f).SetEase(Ease.OutCubic));
            areaBannerTween.AppendInterval(1.25f);
            areaBannerTween.Append(areaBannerGroup.DOFade(0f, 0.26f).SetEase(Ease.InCubic));
            areaBannerTween.Join(areaBannerRoot.DOAnchorPosY(AreaBannerHiddenY, 0.26f).SetEase(Ease.InCubic));
        }

        private static Text CreateLabel(Transform parent, string name, string value, int size, Vector2 position, Vector2 area)
        {
            Text text = StorageUIUtility.CreateText(name, parent, value, size, TextAnchor.MiddleLeft, StoragePalette.TextMuted);
            StorageUIUtility.SetTopLeft(text.rectTransform, position.x, position.y, area.x, area.y);
            return text;
        }

        private static Text CreateValueText(Transform parent, string name, int size, Vector2 position, Vector2 area)
        {
            return CreateValueText(parent, name, size, position, area, StoragePalette.TextPrimary);
        }

        private static Text CreateValueText(Transform parent, string name, int size, Vector2 position, Vector2 area, Color color)
        {
            Text text = StorageUIUtility.CreateText(name, parent, string.Empty, size, TextAnchor.MiddleLeft, color);
            text.fontStyle = FontStyle.Bold;
            StorageUIUtility.SetTopLeft(text.rectTransform, position.x, position.y, area.x, area.y);
            return text;
        }

        private static Text CreateMetaText(Transform parent, string name, Vector2 position, Vector2 area)
        {
            Text text = StorageUIUtility.CreateText(name, parent, string.Empty, 13, TextAnchor.MiddleLeft, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(text.rectTransform, position.x, position.y, area.x, area.y);
            return text;
        }

        private static Text CreateChip(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            RectTransform chip = StorageUIUtility.CreateBox(
                name + "_Chip",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(position.x, -position.y),
                size,
                StoragePalette.ButtonNormal,
                StoragePalette.PanelBorder,
                1f,
                10f);
            Text text = StorageUIUtility.CreateText(name, chip, string.Empty, fontSize, TextAnchor.MiddleCenter, StoragePalette.TextPrimary);
            text.fontStyle = FontStyle.Bold;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        private static Text CreateEquipmentSlotLabel(Transform parent, string name, string value, float x)
        {
            Text text = CreateLabel(
                parent,
                name,
                value,
                12,
                new Vector2(x, HandModuleLabelTop),
                new Vector2(HandModuleSlotSize, 18f));
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }

        private StorageGridUI CreateHandGrid(Transform parent, string name, Vector2 position, int index)
        {
            StorageGridUI grid = CreateEquipmentGrid(parent, name, position, true);
            RectTransform dragCatcher = CreateEquipmentDragCatcher(grid);
            if (index >= 0 && index < handSlotDragHandlers.Length)
            {
                handSlotDragHandlers[index] = dragCatcher.gameObject.AddComponent<CampusHudHandSlotDragHandler>();
            }

            return grid;
        }

        private StorageGridUI CreateBackpackGrid(Transform parent, string name, Vector2 position)
        {
            StorageGridUI grid = CreateEquipmentGrid(parent, name, position, true);
            RectTransform dragCatcher = CreateEquipmentDragCatcher(grid);
            backpackSlotDragHandler = dragCatcher.gameObject.AddComponent<CampusHudBackpackSlotDragHandler>();
            return grid;
        }

        private static StorageGridUI CreateEquipmentGrid(Transform parent, string name, Vector2 position, bool raycastTarget)
        {
            RectTransform slotRoot = StorageUIUtility.CreateBox(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(position.x, -position.y),
                new Vector2(HandModuleSlotSize, HandModuleSlotSize),
                StoragePalette.Slot,
                StoragePalette.SlotBorder,
                1f,
                12f,
                raycastTarget);

            StorageUIUtility.CreateStretchBox(
                "Inner",
                slotRoot,
                new Vector2(4f, 4f),
                new Vector2(-4f, -4f),
                new Color(1f, 1f, 1f, 0.02f),
                Color.clear,
                0f,
                10f);

            GameObject gridObject = StorageUIUtility.CreateRectObject("Grid", slotRoot);
            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(gridRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            gridRect.anchoredPosition = new Vector2(HandModuleGridInset, -HandModuleGridInset);
            gridRect.sizeDelta = new Vector2(HandModuleGridSize, HandModuleGridSize);

            StorageGridUI grid = gridObject.AddComponent<StorageGridUI>();
            grid.CellSize = HandModuleGridSize;
            grid.CellSpacing = 0f;
            grid.RenderItemViews = true;
            grid.DropArea = slotRoot;
            return grid;
        }

        private static RectTransform CreateEquipmentDragCatcher(StorageGridUI grid)
        {
            Transform parent = grid != null && grid.DropArea != null
                ? grid.DropArea
                : grid != null
                    ? grid.transform
                    : null;
            if (parent == null)
            {
                return null;
            }

            return StorageUIUtility.CreateStretchBox(
                "DragCatcher",
                parent,
                Vector2.zero,
                Vector2.zero,
                Color.clear,
                Color.clear,
                0f,
                0f,
                true);
        }

        private void BindHandGrid(int index, StorageContainerModel hand, StorageWindowUI storageWindow)
        {
            if (index < 0 || index >= handGrids.Length)
            {
                return;
            }

            StorageGridUI grid = handGrids[index];
            if (grid == null)
            {
                return;
            }

            StorageWindowUI ownerWindow = storageWindow != null && storageWindow.IsOpen
                ? storageWindow
                : null;
            grid.Bind(hand, ownerWindow);
        }

        private void ConfigureHandDragHandler(
            int index,
            StorageContainerModel hand,
            StorageWindowUI ownerWindow,
            CampusCharacterRuntime playerRuntime)
        {
            if (index < 0 || index >= handSlotDragHandlers.Length)
            {
                return;
            }

            CampusHudHandSlotDragHandler handler = handSlotDragHandlers[index];
            StorageGridUI grid = index < handGrids.Length ? handGrids[index] : null;
            if (handler == null)
            {
                return;
            }

            handler.Configure(
                playerRuntime,
                hand,
                tooltipCanvas,
                tooltipRoot,
                ResolveEquipmentHitRect(grid),
                hudItemTooltip,
                tooltipRoot,
                ownerWindow == null);
        }

        private static RectTransform ResolveEquipmentHitRect(StorageGridUI grid)
        {
            if (grid == null)
            {
                return null;
            }

            return grid.DropArea != null ? grid.DropArea : grid.RectTransform;
        }

        private void ClearExistingHud()
        {
            if (areaBannerTween != null && areaBannerTween.IsActive())
            {
                areaBannerTween.Kill();
            }

            lastAreaBannerKey = string.Empty;
            hasAreaBannerKey = false;
            lastPendingCheckoutVisible = false;
            hasPendingCheckoutVisibility = false;
            staminaFillGraphic = null;
            staminaCapRegionGraphic = null;
            staminaCapRegion = null;
            staminaFill = null;
            pendingCheckoutRoot = null;
            pendingCheckoutGroup = null;
            pendingCheckoutTitleText = null;
            pendingCheckoutSummaryText = null;
            pendingCheckoutStatusText = null;
            tooltipCanvas = null;
            tooltipRoot = null;
            hudItemTooltip = null;
            backpackGrid = null;
            backpackSlotStatusText = null;
            backpackSlotDragHandler = null;
            handGrids[0] = null;
            handGrids[1] = null;
            handSlotDragHandlers[0] = null;
            handSlotDragHandlers[1] = null;

            while (canvasRoot.childCount > 0)
            {
                Transform child = canvasRoot.GetChild(0);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static string FormatNumber(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
