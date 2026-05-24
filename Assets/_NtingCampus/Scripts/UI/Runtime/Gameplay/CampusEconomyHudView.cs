using System.Globalization;
using DG.Tweening;
using Nting.Storage;
using NtingCampus.UI.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusEconomyHudView : MonoBehaviour
    {
        private const string CanvasRootName = "CampusEconomyHudCanvas";
        private const float PulseDuration = 0.18f;
        private const int SortingOrder = 28000;

        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private RectTransform topBarRoot;
        [SerializeField] private StorageBoxGraphic topBarBackdrop;
        [SerializeField] private RectTransform moneyCard;
        [SerializeField] private Text moneyLabelText;
        [SerializeField] private Text moneyValueText;
        [SerializeField] private RectTransform divineCard;
        [SerializeField] private Text divineLabelText;
        [SerializeField] private Text divineValueText;
        [SerializeField] private RectTransform checkoutCard;
        [SerializeField] private StorageBoxGraphic checkoutCardGraphic;
        [SerializeField] private CanvasGroup checkoutCanvasGroup;
        [SerializeField] private Text checkoutTitleText;
        [SerializeField] private Text checkoutCountLabelText;
        [SerializeField] private Text checkoutCountValueText;
        [SerializeField] private Text checkoutTotalLabelText;
        [SerializeField] private Text checkoutTotalValueText;
        [SerializeField] private Text checkoutStatusText;

        private CampusEconomyHudSnapshot snapshot;
        private bool hasSnapshot;
        private float checkoutVisibility;
        private Sequence checkoutVisibilityTween;
        private Tween moneyPulseTween;
        private Tween divinePulseTween;
        private Tween checkoutPulseTween;
        private readonly Vector2 checkoutVisiblePosition = new Vector2(-24f, 24f);
        private readonly Vector2 checkoutHiddenPosition = new Vector2(292f, 24f);

        public void Apply(CampusEconomyHudSnapshot nextSnapshot, bool immediate)
        {
            EnsureVisual();

            bool hasPreviousSnapshot = hasSnapshot;
            bool moneyChanged = hasPreviousSnapshot && snapshot.PlayerMoney != nextSnapshot.PlayerMoney;
            bool divineChanged = hasPreviousSnapshot && snapshot.DivinePower != nextSnapshot.DivinePower;
            bool checkoutChanged = hasPreviousSnapshot &&
                                   (snapshot.PendingCheckoutCount != nextSnapshot.PendingCheckoutCount ||
                                    snapshot.PendingCheckoutTotal != nextSnapshot.PendingCheckoutTotal ||
                                    snapshot.CanAffordCheckout != nextSnapshot.CanAffordCheckout ||
                                    snapshot.ShowCheckoutPanel != nextSnapshot.ShowCheckoutPanel);

            snapshot = nextSnapshot;
            hasSnapshot = true;

            UpdateText();
            UpdateCheckoutStyle();

            if (moneyChanged)
            {
                PulseValueCard(moneyCard, moneyValueText, StoragePalette.Accent, ref moneyPulseTween);
            }

            if (divineChanged)
            {
                PulseValueCard(divineCard, divineValueText, StoragePalette.Warning, ref divinePulseTween);
            }

            if (checkoutChanged)
            {
                PulseValueCard(checkoutCard, checkoutTotalValueText, StoragePalette.Accent, ref checkoutPulseTween);
            }

            if (immediate || !hasPreviousSnapshot)
            {
                KillTween(ref checkoutVisibilityTween);
                checkoutVisibility = snapshot.ShowCheckoutPanel ? 1f : 0f;
                ApplyCheckoutVisibility();
                if (checkoutCard != null)
                {
                    checkoutCard.localScale = Vector3.one;
                }

                return;
            }

            if (checkoutChanged)
            {
                AnimateCheckoutVisibility(snapshot.ShowCheckoutPanel);
            }
        }

        private void OnDisable()
        {
            KillTween(ref checkoutVisibilityTween);
            KillTween(ref moneyPulseTween);
            KillTween(ref divinePulseTween);
            KillTween(ref checkoutPulseTween);
        }

        private void EnsureVisual()
        {
            if (canvas != null &&
                canvasRoot != null &&
                moneyValueText != null &&
                divineValueText != null &&
                checkoutStatusText != null)
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
            canvasRoot.pivot = new Vector2(0.5f, 0.5f);

            canvas = GetOrAdd<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = GetOrAdd<GraphicRaycaster>(canvasObject);
            raycaster.enabled = false;

            BuildTopBar();
            BuildCheckoutCard();
            UpdateText();
            UpdateCheckoutStyle();
            checkoutVisibility = snapshot.ShowCheckoutPanel ? 1f : 0f;
            ApplyCheckoutVisibility();
        }

        private void BuildTopBar()
        {
            topBarRoot = StorageUIUtility.CreateBox(
                "TopBarRoot",
                canvasRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -18f),
                new Vector2(556f, 82f),
                new Color(0f, 0f, 0f, 0f),
                Color.clear,
                0f,
                0f);

            RectTransform backdropRect = StorageUIUtility.CreateStretchBox(
                "TopBarBackdrop",
                topBarRoot,
                new Vector2(-4f, -4f),
                new Vector2(4f, 4f),
                StoragePalette.WindowShadow,
                Color.clear,
                0f,
                24f);
            topBarBackdrop = backdropRect.GetComponent<StorageBoxGraphic>();
            topBarBackdrop.color = new Color(1f, 1f, 1f, 0.92f);

            moneyCard = CreateStatCard(
                topBarRoot,
                "MoneyCard",
                new Vector2(0f, 0f),
                out moneyLabelText,
                out moneyValueText);
            moneyValueText.color = StoragePalette.Accent;
            divineCard = CreateStatCard(
                topBarRoot,
                "DivineCard",
                new Vector2(282f, 0f),
                out divineLabelText,
                out divineValueText);
            divineValueText.color = StoragePalette.Warning;
        }

        private void BuildCheckoutCard()
        {
            checkoutCard = StorageUIUtility.CreateBox(
                "CheckoutCard",
                canvasRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                checkoutHiddenPosition,
                new Vector2(348f, 120f),
                StoragePalette.Window,
                StoragePalette.PanelBorder,
                1.6f,
                18f);
            checkoutCardGraphic = checkoutCard.GetComponent<StorageBoxGraphic>();
            checkoutCanvasGroup = GetOrAdd<CanvasGroup>(checkoutCard.gameObject);
            checkoutCanvasGroup.interactable = false;
            checkoutCanvasGroup.blocksRaycasts = false;
            checkoutCanvasGroup.alpha = 0f;
            checkoutCard.localScale = Vector3.one * 0.96f;

            StorageUIUtility.CreateStretchBox(
                "CheckoutGlow",
                checkoutCard,
                new Vector2(2f, 2f),
                new Vector2(-2f, -2f),
                StoragePalette.PanelInnerBorder,
                Color.clear,
                0f,
                16f);

            RectTransform accent = StorageUIUtility.CreateBox(
                "CheckoutAccent",
                checkoutCard,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 4f),
                StoragePalette.AccentDim,
                Color.clear,
                0f,
                16f);
            accent.offsetMin = new Vector2(14f, -4f);
            accent.offsetMax = new Vector2(-14f, 0f);

            checkoutTitleText = CreateTextNode(
                "CheckoutTitle",
                checkoutCard,
                new Vector2(16f, -12f),
                new Vector2(-16f, -42f),
                15,
                TextAnchor.UpperLeft,
                StoragePalette.TextSecondary,
                FontStyle.Normal);
            checkoutCountLabelText = CreateTextNode(
                "CheckoutCountLabel",
                checkoutCard,
                new Vector2(16f, -44f),
                new Vector2(-180f, -66f),
                13,
                TextAnchor.UpperLeft,
                StoragePalette.TextMuted,
                FontStyle.Normal);
            checkoutCountValueText = CreateTextNode(
                "CheckoutCountValue",
                checkoutCard,
                new Vector2(164f, -40f),
                new Vector2(-18f, -68f),
                18,
                TextAnchor.UpperRight,
                StoragePalette.TextPrimary,
                FontStyle.Bold);
            checkoutTotalLabelText = CreateTextNode(
                "CheckoutTotalLabel",
                checkoutCard,
                new Vector2(16f, -72f),
                new Vector2(-180f, -92f),
                13,
                TextAnchor.UpperLeft,
                StoragePalette.TextMuted,
                FontStyle.Normal);
            checkoutTotalValueText = CreateTextNode(
                "CheckoutTotalValue",
                checkoutCard,
                new Vector2(164f, -68f),
                new Vector2(-18f, -96f),
                19,
                TextAnchor.UpperRight,
                StoragePalette.Accent,
                FontStyle.Bold);
            checkoutStatusText = CreateTextNode(
                "CheckoutStatus",
                checkoutCard,
                new Vector2(16f, -94f),
                new Vector2(-18f, -112f),
                13,
                TextAnchor.LowerLeft,
                StoragePalette.TextSecondary,
                FontStyle.Bold);
        }

        private static RectTransform CreateStatCard(
            RectTransform parent,
            string name,
            Vector2 anchoredPosition,
            out Text labelText,
            out Text valueText)
        {
            RectTransform card = StorageUIUtility.CreateBox(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                new Vector2(270f, 72f),
                StoragePalette.PanelRaised,
                StoragePalette.PanelBorder,
                1.4f,
                18f);

            RectTransform inner = StorageUIUtility.CreateStretchBox(
                "InnerGlow",
                card,
                new Vector2(2f, 2f),
                new Vector2(-2f, -2f),
                StoragePalette.PanelInnerBorder,
                Color.clear,
                0f,
                16f);
            inner.GetComponent<StorageBoxGraphic>().color = new Color(1f, 1f, 1f, 0.7f);

            RectTransform accent = StorageUIUtility.CreateBox(
                "Accent",
                card,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 4f),
                StoragePalette.AccentDim,
                Color.clear,
                0f,
                16f);
            accent.offsetMin = new Vector2(16f, -4f);
            accent.offsetMax = new Vector2(-16f, 0f);

            labelText = CreateTextNode(
                "Label",
                card,
                new Vector2(16f, -10f),
                new Vector2(-16f, -30f),
                13,
                TextAnchor.UpperLeft,
                StoragePalette.TextSecondary,
                FontStyle.Normal);
            valueText = CreateTextNode(
                "Value",
                card,
                new Vector2(16f, -26f),
                new Vector2(-16f, -10f),
                28,
                TextAnchor.LowerLeft,
                StoragePalette.TextPrimary,
                FontStyle.Bold);
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Overflow;

            return card;
        }

        private static Text CreateTextNode(
            string name,
            RectTransform parent,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int size,
            TextAnchor alignment,
            Color color,
            FontStyle fontStyle)
        {
            Text text = StorageUIUtility.CreateText(name, parent, string.Empty, size, alignment, color);
            text.fontStyle = fontStyle;
            text.rectTransform.offsetMin = new Vector2(offsetMin.x, offsetMax.y);
            text.rectTransform.offsetMax = new Vector2(offsetMax.x, offsetMin.y);
            text.supportRichText = false;

            Shadow shadow = GetOrAdd<Shadow>(text.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
            shadow.effectDistance = new Vector2(0f, -1f);
            shadow.useGraphicAlpha = true;
            return text;
        }

        private void UpdateText()
        {
            if (!hasSnapshot)
            {
                return;
            }

            moneyLabelText.text = CampusEconomyUiTextCatalog.Get(CampusEconomyUiTextId.Money);
            moneyValueText.text = FormatNumber(snapshot.PlayerMoney);
            divineLabelText.text = CampusEconomyUiTextCatalog.Get(CampusEconomyUiTextId.DivinePower);
            divineValueText.text = FormatNumber(snapshot.DivinePower);
            checkoutTitleText.text = CampusEconomyUiTextCatalog.Get(CampusEconomyUiTextId.PendingCheckout);
            checkoutCountLabelText.text = CampusEconomyUiTextCatalog.Get(CampusEconomyUiTextId.PendingItems);
            checkoutCountValueText.text = FormatNumber(snapshot.PendingCheckoutCount);
            checkoutTotalLabelText.text = CampusEconomyUiTextCatalog.Get(CampusEconomyUiTextId.PendingTotal);
            checkoutTotalValueText.text = FormatNumber(snapshot.PendingCheckoutTotal);
            checkoutStatusText.text = CampusEconomyUiTextCatalog.Get(
                snapshot.CanAffordCheckout
                    ? CampusEconomyUiTextId.CanAfford
                    : CampusEconomyUiTextId.CannotAfford);
        }

        private void UpdateCheckoutStyle()
        {
            if (checkoutCardGraphic == null)
            {
                return;
            }

            Color fill = snapshot.CanAffordCheckout
                ? StoragePalette.Valid
                : StoragePalette.Invalid;
            Color border = snapshot.CanAffordCheckout
                ? StoragePalette.ValidBorder
                : StoragePalette.InvalidBorder;
            checkoutCardGraphic.SetStyle(fill, border, 1.6f, 18f);
            checkoutStatusText.color = snapshot.CanAffordCheckout
                ? StoragePalette.TextPrimary
                : new Color(1f, 0.88f, 0.84f, 1f);
        }

        private void AnimateCheckoutVisibility(bool visible)
        {
            KillTween(ref checkoutVisibilityTween);
            if (checkoutCard == null || checkoutCanvasGroup == null)
            {
                return;
            }

            checkoutVisibilityTween = DOTween.Sequence().SetUpdate(true);
            checkoutVisibilityTween.Join(DOTween.To(
                    () => checkoutVisibility,
                    value =>
                    {
                        checkoutVisibility = value;
                        ApplyCheckoutVisibility();
                    },
                    visible ? 1f : 0f,
                    visible ? 0.28f : 0.18f)
                .SetEase(visible ? Ease.OutCubic : Ease.InCubic));
            checkoutVisibilityTween.Join(checkoutCard.DOScale(visible ? 1f : 0.96f, visible ? 0.28f : 0.18f)
                .SetEase(visible ? Ease.OutBack : Ease.InQuad));
        }

        private void ApplyCheckoutVisibility()
        {
            if (checkoutCanvasGroup != null)
            {
                checkoutCanvasGroup.alpha = checkoutVisibility;
            }

            if (checkoutCard != null)
            {
                checkoutCard.anchoredPosition = Vector2.Lerp(
                    checkoutHiddenPosition,
                    checkoutVisiblePosition,
                    EaseOutCubic(checkoutVisibility));
            }
        }

        private void PulseValueCard(RectTransform card, Text valueText, Color pulseColor, ref Tween pulseTween)
        {
            if (card == null)
            {
                return;
            }

            KillTween(ref pulseTween);
            card.localScale = Vector3.one;
            pulseTween = card.DOPunchScale(new Vector3(0.045f, 0.045f, 0f), PulseDuration, 8, 0.8f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

            if (valueText != null)
            {
                valueText.DOKill(false);
                valueText.DOColor(pulseColor, PulseDuration * 0.35f)
                    .SetEase(Ease.OutQuad)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        private static void KillTween(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }

            tween = null;
        }

        private static void KillTween(ref Sequence tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }

            tween = null;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static string FormatNumber(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
