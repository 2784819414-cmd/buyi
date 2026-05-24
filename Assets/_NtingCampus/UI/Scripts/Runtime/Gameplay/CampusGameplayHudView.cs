using System.Globalization;
using DG.Tweening;
using Nting.Storage;
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

        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform canvasRoot;
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
        [SerializeField] private Text divineValueText;
        [SerializeField] private Text backpackValueText;
        [SerializeField] private Text interactionKeyText;
        [SerializeField] private Text interactionText;
        [SerializeField] private Text pendingCountText;
        [SerializeField] private Text pendingTotalText;
        [SerializeField] private Text pendingStatusText;
        [SerializeField] private CanvasGroup pendingGroup;
        [SerializeField] private RectTransform pendingCard;

        private const float AreaBannerHiddenY = 92f;
        private const float AreaBannerVisibleY = 112f;
        private Sequence pendingTween;
        private Sequence areaBannerTween;
        private readonly StorageGridUI[] handGrids = new StorageGridUI[2];
        private string lastAreaBannerKey = string.Empty;
        private bool hasAreaBannerKey;

        public bool LastInteractiveWindowOpen { get; private set; }

        public void AttachHandGridsToStorageWindow(StorageWindowUI storageWindow)
        {
            if (storageWindow == null)
            {
                return;
            }

            EnsureVisual();
            storageWindow.SetSharedHandGrids(handGrids);
        }

        public void Apply(
            CampusGameplayHudSnapshot snapshot,
            StorageContainerModel[] hands,
            StorageContainerModel backpack,
            StorageWindowUI storageWindow,
            bool immediate,
            bool refreshHands)
        {
            EnsureVisual();
            ApplyText(snapshot);
            if (refreshHands)
            {
                ApplyHands(hands, storageWindow);
            }

            UpdateAreaBanner(snapshot, immediate);
            ApplyPending(snapshot, immediate);
            LastInteractiveWindowOpen = storageWindow != null && storageWindow.IsOpen;
        }

        private void OnDisable()
        {
            if (pendingTween != null && pendingTween.IsActive())
            {
                pendingTween.Kill();
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
                dateText != null &&
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

            RectTransform bottomLeftCard = CreatePanel("BottomLeftCard", new Vector2(24f, 112f), new Vector2(332f, 176f), false, false);
            CreateStateCard(bottomLeftCard);

            RectTransform bottomRightCard = CreatePanel("BottomRightCard", new Vector2(-24f, 24f), new Vector2(360f, 198f), true, false);
            CreateInteractionCard(bottomRightCard);

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
                StoragePalette.WindowShadow,
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
                StoragePalette.Panel,
                StoragePalette.PanelBorder,
                1f,
                18f);
            CreateAreaBanner(areaBanner);
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
                StoragePalette.WindowShadow,
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
                StoragePalette.Panel,
                StoragePalette.PanelBorder,
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
                StoragePalette.PanelRaised,
                StoragePalette.PanelBorder,
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
                StoragePalette.PanelRaised,
                StoragePalette.PanelBorder,
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
            moneyValueText = CreateValueText(parent, "Money", 20, new Vector2(18f, 34f), new Vector2(120f, 26f), StoragePalette.Accent);

            CreateLabel(parent, "DivineLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.DivinePower), 12, new Vector2(170f, 16f), new Vector2(120f, 18f));
            divineValueText = CreateValueText(parent, "Divine", 20, new Vector2(170f, 34f), new Vector2(120f, 26f), StoragePalette.Paper);

            CreateLabel(parent, "BackpackLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Backpack), 12, new Vector2(18f, 70f), new Vector2(100f, 18f));
            backpackValueText = CreateMetaText(parent, "BackpackValue", new Vector2(18f, 90f), new Vector2(220f, 20f));

            CreateLabel(parent, "LeftHandLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.LeftHand), 12, new Vector2(18f, 118f), new Vector2(60f, 18f));
            CreateLabel(parent, "RightHandLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.RightHand), 12, new Vector2(140f, 118f), new Vector2(60f, 18f));
            handGrids[0] = CreateHandGrid(parent, "LeftHandDisplay", new Vector2(18f, 94f));
            handGrids[1] = CreateHandGrid(parent, "RightHandDisplay", new Vector2(140f, 94f));
        }

        private void CreateInteractionCard(RectTransform parent)
        {
            RectTransform interactionCard = StorageUIUtility.CreateBox(
                "InteractionCard",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 88f),
                StoragePalette.PanelRaised,
                StoragePalette.PanelBorder,
                1f,
                16f);
            interactionCard.offsetMin = new Vector2(0f, -88f);
            interactionCard.offsetMax = new Vector2(0f, 0f);

            RectTransform keyPlate = StorageUIUtility.CreateBox(
                "KeyPlate",
                interactionCard,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(18f, 0f),
                new Vector2(64f, 64f),
                StoragePalette.ButtonNormal,
                StoragePalette.PanelBorder,
                1f,
                12f);
            interactionKeyText = StorageUIUtility.CreateText("InteractionKey", keyPlate, string.Empty, 28, TextAnchor.MiddleCenter, StoragePalette.TextPrimary);
            interactionKeyText.fontStyle = FontStyle.Bold;
            interactionKeyText.rectTransform.offsetMin = Vector2.zero;
            interactionKeyText.rectTransform.offsetMax = Vector2.zero;

            interactionText = CreateValueText(interactionCard, "InteractionText", 18, new Vector2(96f, 20f), new Vector2(240f, 48f));

            pendingCard = StorageUIUtility.CreateBox(
                "PendingCard",
                parent,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 88f),
                StoragePalette.PanelRaised,
                StoragePalette.PanelBorder,
                1f,
                16f);
            pendingCard.offsetMin = new Vector2(0f, 0f);
            pendingCard.offsetMax = new Vector2(0f, 88f);
            pendingGroup = pendingCard.gameObject.AddComponent<CanvasGroup>();

            CreateLabel(pendingCard, "PendingLabel", CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.PendingCheckout), 12, new Vector2(18f, 14f), new Vector2(140f, 18f));
            pendingCountText = CreateMetaText(pendingCard, "PendingCount", new Vector2(18f, 34f), new Vector2(140f, 20f));
            pendingTotalText = CreateValueText(pendingCard, "PendingTotal", 20, new Vector2(200f, 24f), new Vector2(120f, 28f), StoragePalette.Accent);
            pendingStatusText = CreateMetaText(pendingCard, "PendingStatus", new Vector2(18f, 58f), new Vector2(280f, 18f));
        }

        private void CreateAreaBanner(RectTransform parent)
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
            divineValueText.text = FormatNumber(snapshot.DivinePower);
            backpackValueText.text = snapshot.BackpackStatus;
            interactionKeyText.text = snapshot.InteractionKeyText;
            interactionText.text = snapshot.InteractionText;
            interactionText.color = snapshot.InteractionAvailable ? StoragePalette.TextPrimary : StoragePalette.TextMuted;
            pendingCountText.text = CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.PendingItems) + " " + snapshot.PendingCheckoutCount;
            pendingTotalText.text = CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.PendingTotal) + " " + FormatNumber(snapshot.PendingCheckoutTotal);
            pendingStatusText.text = CampusGameplayHudTextCatalog.Get(
                snapshot.CanAffordCheckout
                    ? CampusGameplayHudTextId.ReadyToPay
                    : CampusGameplayHudTextId.NotEnoughMoney);
            pendingStatusText.color = snapshot.CanAffordCheckout ? StoragePalette.TextSecondary : StoragePalette.Warning;
        }

        private void ApplyHands(StorageContainerModel[] hands, StorageWindowUI storageWindow)
        {
            if (storageWindow != null && storageWindow.IsOpen)
            {
                storageWindow.SetSharedHandGrids(handGrids);
            }

            BindHandGrid(0, CampusHandInventoryUtility.ResolveHandContainer(hands, 0), storageWindow);
            BindHandGrid(1, CampusHandInventoryUtility.ResolveHandContainer(hands, 1), storageWindow);
        }

        private void ApplyPending(CampusGameplayHudSnapshot snapshot, bool immediate)
        {
            float targetAlpha = snapshot.ShowPendingCheckout ? 1f : 0f;
            if (pendingGroup == null || pendingCard == null)
            {
                return;
            }

            if (pendingTween != null && pendingTween.IsActive())
            {
                pendingTween.Kill();
            }

            if (immediate)
            {
                pendingGroup.alpha = targetAlpha;
                pendingCard.localScale = targetAlpha > 0f ? Vector3.one : Vector3.one * 0.98f;
                return;
            }

            pendingTween = DOTween.Sequence().SetUpdate(true);
            pendingTween.Join(pendingGroup.DOFade(targetAlpha, snapshot.ShowPendingCheckout ? 0.22f : 0.14f));
            pendingTween.Join(pendingCard.DOScale(targetAlpha > 0f ? 1f : 0.98f, snapshot.ShowPendingCheckout ? 0.22f : 0.14f)
                .SetEase(snapshot.ShowPendingCheckout ? Ease.OutCubic : Ease.InCubic));
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

        private static StorageGridUI CreateHandGrid(Transform parent, string name, Vector2 position)
        {
            RectTransform slotRoot = StorageUIUtility.CreateBox(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(position.x, -position.y),
                new Vector2(76f, 76f),
                StoragePalette.Slot,
                StoragePalette.SlotBorder,
                1f,
                12f);

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
            gridRect.anchoredPosition = new Vector2(6f, -6f);

            StorageGridUI grid = gridObject.AddComponent<StorageGridUI>();
            grid.CellSize = 64f;
            grid.CellSpacing = 0f;
            grid.RenderItemViews = true;
            grid.DropArea = slotRoot;
            return grid;
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

        private void ClearExistingHud()
        {
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
