using System.Collections.Generic;
using DG.Tweening;
using Nting.Storage;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusGameplaySettingsOverlay : MonoBehaviour
    {
        private sealed class LanguageButtonView
        {
            public CampusDisplayLanguage Language;
            public CampusPlayerUiTextId LabelId;
            public Button Button;
        }

        private const string StartupSceneName = "Startup";
        private const int SortingOrder = 32767;
        private const float MainPanelWidth = 1720f;
        private const float MainPanelHeight = 760f;
        private const float MainPanelPadding = 20f;
        private const float MainPanelContentWidth = MainPanelWidth - MainPanelPadding * 2f;

        private enum OverlayPage
        {
            Home,
            SettingsMenu,
            Language,
            KeyBindings,
            TimeMenu,
            TimeControl,
            TimeTest
        }

        [SerializeField] private CampusGameBootstrap bootstrap;
        private Canvas canvas;
        private RectTransform canvasRoot;
        private RectTransform backdropPanel;
        private RectTransform mainPanel;
        private CanvasGroup mainCanvasGroup;
        private RectTransform scrollContent;
        private RectTransform homeCard;
        private RectTransform settingsMenuCard;
        private RectTransform timeMenuCard;
        private RectTransform languageCard;
        private RectTransform keyBindingCard;
        private RectTransform timeControlCard;
        private RectTransform timeTestCard;
        private RectTransform footerPanel;
        private CanvasGroup loadingCanvasGroup;
        private Tween mainPanelTween;
        private OverlayPage currentPage = OverlayPage.Home;
        private bool isVisible;
        private bool pauseCaptured;
        private CampusGameplayPauseState pauseState;
        private string timeTestYearText = string.Empty;
        private string timeTestMonthText = string.Empty;
        private string timeTestDayText = string.Empty;
        private string timeTestHourText = string.Empty;
        private string timeTestMinuteText = string.Empty;
        private string timeTestStatusMessage = string.Empty;

        private Text titleText;
        private Text subtitleText;
        private Text homeSectionTitleText;
        private Text languageSectionTitleText;
        private Text keyBindingSectionTitleText;
        private Text keyBindingDescriptionText;
        private Text keyBindingStatusText;
        private Text timeControlSectionTitleText;
        private Text timeControlDescriptionText;
        private Text timeControlStateText;
        private Text timeTestSectionTitleText;
        private Text timeTestDescriptionText;
        private Text timeTestDateLabelText;
        private Text timeTestHourLabelText;
        private Text timeTestMinuteLabelText;
        private Text timeTestStatusText;
        private Text footerHintText;

        private readonly List<LanguageButtonView> languageButtonViews = new List<LanguageButtonView>();
        private Button menuContinueButton;
        private Button menuSettingsButton;
        private Button menuTimeButton;
        private Button menuReturnButton;
        private Button settingsLanguageButton;
        private Button settingsKeyBindingButton;
        private Button timeControlMenuButton;
        private Button timeTestMenuButton;
        private Button resetAllKeyBindingsButton;
        private Button pauseButton;
        private Button resumeButton;
        private Button timeResetButton;
        private Button timeApplyButton;
        private Button backButton;
        private Button continueButton;

        private InputField yearField;
        private InputField monthField;
        private InputField dayField;
        private InputField hourField;
        private InputField minuteField;
        private readonly List<KeyBindingRow> keyBindingRows = new List<KeyBindingRow>();
        private CampusGameplayInputActionId? pendingKeyBindingAction;
        private string keyBindingStatusMessage = string.Empty;

        private void Awake()
        {
            bootstrap = bootstrap != null ? bootstrap : GetComponent<CampusGameBootstrap>();
        }

        private void Start()
        {
            EnsureVisual();
            RefreshLocalizedText();
            SyncTimeDraftFromController();
            RefreshTimeControlState();
            SetVisible(false, true);
        }

        private void Update()
        {
            bootstrap = bootstrap != null ? bootstrap : CampusGameBootstrap.Instance;
            if (isVisible && pendingKeyBindingAction.HasValue)
            {
                CapturePendingKeyBinding();
                return;
            }

            if (!CampusGameplayInputBindings.WasPressed(CampusGameplayInputActionId.Settings))
            {
                return;
            }

            if (isVisible)
            {
                if (currentPage != OverlayPage.Home)
                {
                    ReturnToParentPage();
                    return;
                }

                SetVisible(false);
                return;
            }

            CampusRuntimeMapEditor runtimeMapEditor = CampusRuntimeMapEditor.Instance;
            if (runtimeMapEditor != null && runtimeMapEditor.IsOpen)
            {
                return;
            }

            SetVisible(true);
        }

        private void OnDisable()
        {
            SetVisible(false, true);
        }

        public void SetVisible(bool visible)
        {
            SetVisible(visible, false);
        }

        private void EnsureVisual()
        {
            if (canvas != null)
            {
                return;
            }

            CampusUiRuntimeBuilder.EnsureEventSystem();
            canvas = CampusUiRuntimeBuilder.CreateScreenCanvas(gameObject, "CampusGameplaySettingsCanvas", SortingOrder);
            canvasRoot = canvas.GetComponent<RectTransform>();

            RectTransform backdrop = CampusUiRuntimeBuilder.CreateFullScreenPanel(
                "Backdrop",
                canvasRoot,
                CampusUiVisualTheme.Overlay,
                true);
            backdrop.SetAsFirstSibling();
            backdropPanel = backdrop;
            backdropPanel.gameObject.SetActive(false);

            mainPanel = CampusUiRuntimeBuilder.CreatePanel(
                "SettingsPanel",
                canvasRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(MainPanelWidth, MainPanelHeight),
                CampusUiVisualTheme.Panel,
                CampusUiVisualTheme.Border,
                1.8f,
                26f,
                false);
            mainCanvasGroup = mainPanel.gameObject.AddComponent<CanvasGroup>();
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.interactable = true;
            mainCanvasGroup.blocksRaycasts = true;
            mainPanel.gameObject.SetActive(false);

            BuildHeader(mainPanel);
            BuildScrollArea(mainPanel);
            BuildFooter(mainPanel);
        }

        private void BuildHeader(Transform parent)
        {
            titleText = CampusUiRuntimeBuilder.CreateText(
                "Title",
                parent,
                string.Empty,
                42,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextGold,
                FontStyle.Bold);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(30f, -18f);
            titleText.rectTransform.sizeDelta = new Vector2(380f, 44f);

            subtitleText = CampusUiRuntimeBuilder.CreateText(
                "Subtitle",
                parent,
                string.Empty,
                17,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextSecondary);
            subtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            subtitleText.rectTransform.pivot = new Vector2(0f, 1f);
            subtitleText.rectTransform.anchoredPosition = new Vector2(30f, -64f);
            subtitleText.rectTransform.sizeDelta = new Vector2(700f, 42f);
        }

        private void BuildScrollArea(Transform parent)
        {
            ScrollRect scrollRect = CampusUiRuntimeBuilder.CreateScrollView(
                "SettingsScroll",
                parent,
                new Vector2(MainPanelContentWidth, 536f),
                out RectTransform viewport,
                out scrollContent,
                CampusUiVisualTheme.PanelSoft,
                CampusUiVisualTheme.BorderMuted,
                1.05f,
                18f);

            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 1f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 1f);
            scrollRectTransform.anchoredPosition = new Vector2(0f, -144f);
            scrollRectTransform.sizeDelta = new Vector2(-MainPanelPadding * 2f, 536f);

            VerticalLayoutGroup layout = scrollContent.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(14, 14, 14, 14);
                layout.spacing = 12f;
            }

            viewport.offsetMin = new Vector2(10f, 10f);
            viewport.offsetMax = new Vector2(-10f, -10f);

            homeCard = CreateSectionCard(scrollContent, "HomeCard", 430f, out homeSectionTitleText);
            BuildHomeCard(homeCard);

            settingsMenuCard = CreateSectionCard(scrollContent, "SettingsMenuCard", 250f, out Text settingsMenuTitleText);
            BuildSettingsMenuCard(settingsMenuCard);

            languageCard = CreateSectionCard(scrollContent, "LanguageCard", 112f, out languageSectionTitleText);
            BuildLanguageCard(languageCard);

            keyBindingCard = CreateSectionCard(scrollContent, "KeyBindingCard", 450f, out keyBindingSectionTitleText);
            BuildKeyBindingCard(keyBindingCard);

            timeMenuCard = CreateSectionCard(scrollContent, "TimeMenuCard", 250f, out Text timeMenuTitleText);
            BuildTimeMenuCard(timeMenuCard);

            timeControlCard = CreateSectionCard(scrollContent, "TimeControlCard", 154f, out timeControlSectionTitleText);
            BuildTimeControlCard(timeControlCard);

            timeTestCard = CreateSectionCard(scrollContent, "TimeTestCard", 256f, out timeTestSectionTitleText);
            BuildTimeTestCard(timeTestCard);
        }

        private RectTransform CreateSectionCard(Transform parent, string name, float height, out Text sectionTitle)
        {
            RectTransform card = CampusUiRuntimeBuilder.CreatePanel(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, height),
                CampusUiVisualTheme.PanelRaised,
                CampusUiVisualTheme.BorderSoft,
                1.05f,
                18f,
                false);

            LayoutElement layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleWidth = 1f;

            sectionTitle = CampusUiRuntimeBuilder.CreateText(
                name + "_Title",
                card,
                string.Empty,
                20,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            sectionTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            sectionTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            sectionTitle.rectTransform.pivot = new Vector2(0f, 1f);
            sectionTitle.rectTransform.anchoredPosition = new Vector2(22f, -16f);
            sectionTitle.rectTransform.sizeDelta = new Vector2(320f, 24f);

            return card;
        }

        private void BuildHomeCard(RectTransform card)
        {
            menuContinueButton = CreateHomeButton(
                card,
                "HomeContinueButton",
                CampusPlayerUiTextId.Continue,
                new Vector2(370f, -86f),
                () => SetVisible(false),
                CampusUiVisualTheme.AccentSoftFill,
                CampusUiVisualTheme.Accent);

            menuSettingsButton = CreateHomeButton(
                card,
                "HomeSettingsButton",
                CampusPlayerUiTextId.SettingsTitle,
                new Vector2(370f, -164f),
                () => SetPage(OverlayPage.SettingsMenu),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft);

            menuTimeButton = CreateHomeButton(
                card,
                "HomeTimeButton",
                CampusPlayerUiTextId.AdjustTime,
                new Vector2(370f, -242f),
                () => SetPage(OverlayPage.TimeMenu),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft);

            menuReturnButton = CreateHomeButton(
                card,
                "HomeReturnButton",
                CampusPlayerUiTextId.ReturnToMainMenu,
                new Vector2(370f, -320f),
                ReturnToMainMenu,
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft);
        }

        private void BuildSettingsMenuCard(RectTransform card)
        {
            settingsLanguageButton = CreateHomeButton(
                card,
                "SettingsLanguageButton",
                CampusPlayerUiTextId.Language,
                new Vector2(370f, -86f),
                () => SetPage(OverlayPage.Language),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft);

            settingsKeyBindingButton = CreateHomeButton(
                card,
                "SettingsKeyBindingButton",
                CampusPlayerUiTextId.KeyBindingTitle,
                new Vector2(370f, -164f),
                () => SetPage(OverlayPage.KeyBindings),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft);
        }

        private void BuildTimeMenuCard(RectTransform card)
        {
            timeControlMenuButton = CreateHomeButton(
                card,
                "TimeControlMenuButton",
                CampusPlayerUiTextId.TimeControlTitle,
                new Vector2(370f, -86f),
                () => SetPage(OverlayPage.TimeControl),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft);

            timeTestMenuButton = CreateHomeButton(
                card,
                "TimeTestMenuButton",
                CampusPlayerUiTextId.TimeTestTitle,
                new Vector2(370f, -164f),
                () => SetPage(OverlayPage.TimeTest),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft);
        }

        private Button CreateHomeButton(
            Transform parent,
            string name,
            CampusPlayerUiTextId textId,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction action,
            Color fill,
            Color border)
        {
            Button button = CampusUiRuntimeBuilder.CreateButton(
                name,
                parent,
                CampusPlayerUiTextCatalog.Get(textId),
                action,
                fill,
                border,
                16f,
                1.2f,
                CampusUiVisualTheme.TextPrimary,
                22);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(940f, 54f);
            return button;
        }

        private void BuildLanguageCard(RectTransform card)
        {
            Text label = CampusUiRuntimeBuilder.CreateText(
                "LanguageLabel",
                card,
                string.Empty,
                15,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted,
                FontStyle.Bold);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(22f, -48f);
            label.rectTransform.sizeDelta = new Vector2(120f, 20f);

            languageButtonViews.Clear();
            for (int i = 0; i < CampusDisplayLanguageCatalog.All.Count; i++)
            {
                CampusDisplayLanguage language = CampusDisplayLanguageCatalog.All[i];
                CreateLanguageButton(
                    card,
                    language,
                    CampusPlayerUiTextCatalog.GetLanguageNameTextId(language),
                    new Vector2(132f + 172f * i, -56f));
            }
        }

        private void CreateLanguageButton(
            Transform parent,
            CampusDisplayLanguage language,
            CampusPlayerUiTextId labelId,
            Vector2 anchoredPosition)
        {
            Button button = CampusUiRuntimeBuilder.CreateButton(
                "Language_" + language,
                parent,
                CampusPlayerUiTextCatalog.Get(labelId),
                () => SetLanguage(language),
                CampusLanguageState.CurrentLanguage == language ? CampusUiVisualTheme.AccentSoftFill : CampusUiVisualTheme.PanelDim,
                CampusLanguageState.CurrentLanguage == language ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderSoft,
                14f,
                1.1f,
                CampusUiVisualTheme.TextPrimary,
                15);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(160f, 36f);

            languageButtonViews.Add(new LanguageButtonView
            {
                Language = language,
                LabelId = labelId,
                Button = button
            });
        }

        private void BuildKeyBindingCard(RectTransform card)
        {
            keyBindingDescriptionText = CampusUiRuntimeBuilder.CreateText(
                "KeyBindingDescription",
                card,
                string.Empty,
                15,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextSecondary);
            keyBindingDescriptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            keyBindingDescriptionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            keyBindingDescriptionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            keyBindingDescriptionText.rectTransform.anchoredPosition = new Vector2(22f, -48f);
            keyBindingDescriptionText.rectTransform.sizeDelta = new Vector2(-44f, 34f);

            resetAllKeyBindingsButton = CampusUiRuntimeBuilder.CreateButton(
                "ResetAllKeyBindingsButton",
                card,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingResetAll),
                ResetAllKeyBindings,
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft,
                14f,
                1.05f,
                CampusUiVisualTheme.TextSecondary,
                14);
            RectTransform resetAllRect = resetAllKeyBindingsButton.GetComponent<RectTransform>();
            resetAllRect.anchorMin = new Vector2(1f, 1f);
            resetAllRect.anchorMax = new Vector2(1f, 1f);
            resetAllRect.pivot = new Vector2(1f, 1f);
            resetAllRect.anchoredPosition = new Vector2(-22f, -14f);
            resetAllRect.sizeDelta = new Vector2(180f, 32f);

            keyBindingRows.Clear();
            IReadOnlyList<CampusGameplayInputActionId> actions = CampusGameplayInputBindings.RebindableActions;
            const float leftColumnX = 22f;
            const float rightColumnX = 826f;
            const float firstRowY = -88f;
            const float rowHeight = 38f;
            int rowsPerColumn = Mathf.CeilToInt(actions.Count * 0.5f);

            for (int i = 0; i < actions.Count; i++)
            {
                int column = i / rowsPerColumn;
                int row = i % rowsPerColumn;
                float x = column == 0 ? leftColumnX : rightColumnX;
                float y = firstRowY - row * rowHeight;
                CreateKeyBindingRow(card, actions[i], new Vector2(x, y));
            }

            keyBindingStatusText = CampusUiRuntimeBuilder.CreateText(
                "KeyBindingStatus",
                card,
                string.Empty,
                13,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted);
            keyBindingStatusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            keyBindingStatusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            keyBindingStatusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            keyBindingStatusText.rectTransform.anchoredPosition = new Vector2(22f, 14f);
            keyBindingStatusText.rectTransform.sizeDelta = new Vector2(-44f, 22f);
        }

        private void CreateKeyBindingRow(
            Transform parent,
            CampusGameplayInputActionId actionId,
            Vector2 anchoredPosition)
        {
            Text actionText = CampusUiRuntimeBuilder.CreateText(
                actionId + "_Label",
                parent,
                string.Empty,
                14,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            actionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            actionText.rectTransform.anchorMax = new Vector2(0f, 1f);
            actionText.rectTransform.pivot = new Vector2(0f, 1f);
            actionText.rectTransform.anchoredPosition = anchoredPosition;
            actionText.rectTransform.sizeDelta = new Vector2(216f, 32f);

            Button keyButton = CampusUiRuntimeBuilder.CreateButton(
                actionId + "_KeyButton",
                parent,
                string.Empty,
                () => BeginKeyBinding(actionId),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft,
                12f,
                1.05f,
                CampusUiVisualTheme.TextPrimary,
                14);
            RectTransform keyRect = keyButton.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0f, 1f);
            keyRect.anchorMax = new Vector2(0f, 1f);
            keyRect.pivot = new Vector2(0f, 1f);
            keyRect.anchoredPosition = anchoredPosition + new Vector2(224f, 0f);
            keyRect.sizeDelta = new Vector2(132f, 32f);

            Button resetButton = CampusUiRuntimeBuilder.CreateButton(
                actionId + "_ResetButton",
                parent,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingReset),
                () => ResetKeyBinding(actionId),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderMuted,
                12f,
                1.05f,
                CampusUiVisualTheme.TextSecondary,
                13);
            RectTransform resetRect = resetButton.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(0f, 1f);
            resetRect.anchorMax = new Vector2(0f, 1f);
            resetRect.pivot = new Vector2(0f, 1f);
            resetRect.anchoredPosition = anchoredPosition + new Vector2(366f, 0f);
            resetRect.sizeDelta = new Vector2(92f, 32f);

            keyBindingRows.Add(new KeyBindingRow(actionId, actionText, keyButton, resetButton));
        }

        private void BuildTimeControlCard(RectTransform card)
        {
            timeControlDescriptionText = CampusUiRuntimeBuilder.CreateText(
                "TimeControlDescription",
                card,
                string.Empty,
                15,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextSecondary);
            timeControlDescriptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            timeControlDescriptionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            timeControlDescriptionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            timeControlDescriptionText.rectTransform.anchoredPosition = new Vector2(22f, -48f);
            timeControlDescriptionText.rectTransform.sizeDelta = new Vector2(-44f, 36f);

            timeControlStateText = CampusUiRuntimeBuilder.CreateText(
                "TimeControlState",
                card,
                string.Empty,
                15,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextGold,
                FontStyle.Bold);
            timeControlStateText.rectTransform.anchorMin = new Vector2(0f, 1f);
            timeControlStateText.rectTransform.anchorMax = new Vector2(0f, 1f);
            timeControlStateText.rectTransform.pivot = new Vector2(0f, 1f);
            timeControlStateText.rectTransform.anchoredPosition = new Vector2(22f, -88f);
            timeControlStateText.rectTransform.sizeDelta = new Vector2(220f, 22f);

            pauseButton = CreateSmallActionButton(card, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimePause), new Vector2(22f, -118f), CampusUiVisualTheme.Warning);
            pauseButton.onClick.AddListener(() =>
            {
                CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
                if (timeController != null)
                {
                    timeController.PauseTime(true);
                    RefreshTimeControlState();
                }
            });

            resumeButton = CreateSmallActionButton(card, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeResume), new Vector2(152f, -118f), CampusUiVisualTheme.Success);
            resumeButton.onClick.AddListener(() =>
            {
                CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
                if (timeController != null)
                {
                    timeController.ResumeTime(true);
                    RefreshTimeControlState();
                }
            });
        }

        private void BuildTimeTestCard(RectTransform card)
        {
            timeTestDescriptionText = CampusUiRuntimeBuilder.CreateText(
                "TimeTestDescription",
                card,
                string.Empty,
                15,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextSecondary);
            timeTestDescriptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            timeTestDescriptionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            timeTestDescriptionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            timeTestDescriptionText.rectTransform.anchoredPosition = new Vector2(22f, -48f);
            timeTestDescriptionText.rectTransform.sizeDelta = new Vector2(-44f, 34f);

            timeTestDateLabelText = CampusUiRuntimeBuilder.CreateText(
                "DateLabel",
                card,
                string.Empty,
                14,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted,
                FontStyle.Bold);
            timeTestDateLabelText.rectTransform.anchorMin = new Vector2(0f, 1f);
            timeTestDateLabelText.rectTransform.anchorMax = new Vector2(0f, 1f);
            timeTestDateLabelText.rectTransform.pivot = new Vector2(0f, 1f);
            timeTestDateLabelText.rectTransform.anchoredPosition = new Vector2(22f, -90f);
            timeTestDateLabelText.rectTransform.sizeDelta = new Vector2(240f, 20f);

            timeTestYearText = string.Empty;
            timeTestMonthText = string.Empty;
            timeTestDayText = string.Empty;
            timeTestHourText = string.Empty;
            timeTestMinuteText = string.Empty;

            yearField = CreateSmallInputField(card, "YearField", new Vector2(22f, -118f), 74f);
            monthField = CreateSmallInputField(card, "MonthField", new Vector2(106f, -118f), 74f);
            dayField = CreateSmallInputField(card, "DayField", new Vector2(190f, -118f), 74f);
            timeTestHourLabelText = CampusUiRuntimeBuilder.CreateText(
                "HourLabel",
                card,
                string.Empty,
                14,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted,
                FontStyle.Bold);
            timeTestHourLabelText.rectTransform.anchorMin = new Vector2(0f, 1f);
            timeTestHourLabelText.rectTransform.anchorMax = new Vector2(0f, 1f);
            timeTestHourLabelText.rectTransform.pivot = new Vector2(0f, 1f);
            timeTestHourLabelText.rectTransform.anchoredPosition = new Vector2(290f, -90f);
            timeTestHourLabelText.rectTransform.sizeDelta = new Vector2(120f, 20f);

            hourField = CreateSmallInputField(card, "HourField", new Vector2(290f, -118f), 74f);

            timeTestMinuteLabelText = CampusUiRuntimeBuilder.CreateText(
                "MinuteLabel",
                card,
                string.Empty,
                14,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted,
                FontStyle.Bold);
            timeTestMinuteLabelText.rectTransform.anchorMin = new Vector2(0f, 1f);
            timeTestMinuteLabelText.rectTransform.anchorMax = new Vector2(0f, 1f);
            timeTestMinuteLabelText.rectTransform.pivot = new Vector2(0f, 1f);
            timeTestMinuteLabelText.rectTransform.anchoredPosition = new Vector2(374f, -90f);
            timeTestMinuteLabelText.rectTransform.sizeDelta = new Vector2(140f, 20f);

            minuteField = CreateSmallInputField(card, "MinuteField", new Vector2(374f, -118f), 74f);

            timeResetButton = CreateSmallActionButton(card, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestReset), new Vector2(468f, -118f), CampusUiVisualTheme.PanelDim);
            timeResetButton.onClick.AddListener(SyncTimeDraftFromController);

            timeApplyButton = CampusUiRuntimeBuilder.CreateButton(
                "ApplyTimeButton",
                card,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestApply),
                ApplyTimeDraft,
                CampusUiVisualTheme.AccentSoftFill,
                CampusUiVisualTheme.Accent,
                16f,
                1.1f,
                CampusUiVisualTheme.TextPrimary,
                16);
            RectTransform applyRect = timeApplyButton.GetComponent<RectTransform>();
            applyRect.anchorMin = new Vector2(0f, 1f);
            applyRect.anchorMax = new Vector2(0f, 1f);
            applyRect.pivot = new Vector2(0f, 1f);
            applyRect.anchoredPosition = new Vector2(22f, -168f);
            applyRect.sizeDelta = new Vector2(170f, 36f);

            timeTestStatusText = CampusUiRuntimeBuilder.CreateText(
                "Status",
                card,
                string.Empty,
                13,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.Warning);
            timeTestStatusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            timeTestStatusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            timeTestStatusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            timeTestStatusText.rectTransform.anchoredPosition = new Vector2(22f, 14f);
            timeTestStatusText.rectTransform.sizeDelta = new Vector2(-44f, 22f);
        }

        private void BuildFooter(Transform parent)
        {
            RectTransform footer = CampusUiRuntimeBuilder.CreatePanel(
                "Footer",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -702f),
                new Vector2(-MainPanelPadding * 2f, 76f),
                CampusUiVisualTheme.PanelSoft,
                CampusUiVisualTheme.BorderMuted,
                1.05f,
                18f,
                false);
            footerPanel = footer;

            footerHintText = CampusUiRuntimeBuilder.CreateText(
                "FooterHint",
                footer,
                string.Empty,
                14,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted);
            footerHintText.rectTransform.anchorMin = new Vector2(0f, 0f);
            footerHintText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            footerHintText.rectTransform.pivot = new Vector2(0f, 0.5f);
            footerHintText.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            footerHintText.rectTransform.sizeDelta = new Vector2(360f, 28f);

            backButton = CampusUiRuntimeBuilder.CreateButton(
                "BackButton",
                footer,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.BackToEscMenu),
                ReturnToParentPage,
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft,
                16f,
                1.1f,
                CampusUiVisualTheme.TextSecondary,
                16);
            RectTransform returnRect = backButton.GetComponent<RectTransform>();
            returnRect.anchorMin = new Vector2(1f, 0.5f);
            returnRect.anchorMax = new Vector2(1f, 0.5f);
            returnRect.pivot = new Vector2(1f, 0.5f);
            returnRect.anchoredPosition = new Vector2(-232f, 0f);
            returnRect.sizeDelta = new Vector2(182f, 34f);

            continueButton = CampusUiRuntimeBuilder.CreateButton(
                "ContinueButton",
                footer,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Continue),
                () => SetVisible(false),
                CampusUiVisualTheme.AccentSoftFill,
                CampusUiVisualTheme.Accent,
                16f,
                1.2f,
                CampusUiVisualTheme.TextPrimary,
                16);
            RectTransform continueRect = continueButton.GetComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(1f, 0.5f);
            continueRect.anchorMax = new Vector2(1f, 0.5f);
            continueRect.pivot = new Vector2(1f, 0.5f);
            continueRect.anchoredPosition = new Vector2(-24f, 0f);
            continueRect.sizeDelta = new Vector2(184f, 38f);
        }

        private Button CreateSmallActionButton(Transform parent, string label, Vector2 anchoredPosition, Color accent)
        {
            Button button = CampusUiRuntimeBuilder.CreateButton(
                label,
                parent,
                label,
                null,
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft,
                14f,
                1.05f,
                CampusUiVisualTheme.TextPrimary,
                15);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(120f, 34f);

            StorageBoxGraphic graphic = button.targetGraphic as StorageBoxGraphic;
            if (graphic != null)
            {
                graphic.SetStyle(CampusUiVisualTheme.PanelDim, accent, 1.05f, 14f);
            }

            Text buttonLabel = button.GetComponentInChildren<Text>();
            if (buttonLabel != null)
            {
                buttonLabel.color = CampusUiVisualTheme.TextPrimary;
            }

            return button;
        }

        private InputField CreateSmallInputField(Transform parent, string name, Vector2 anchoredPosition, float width)
        {
            InputField field = CampusUiRuntimeBuilder.CreateInputField(
                name,
                parent,
                string.Empty,
                15,
                CampusUiVisualTheme.TextPrimary,
                CampusUiVisualTheme.TextMuted);
            RectTransform rect = field.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(width, 34f);
            return field;
        }

        private void SetPage(OverlayPage page)
        {
            currentPage = page;
            pendingKeyBindingAction = null;
            keyBindingStatusMessage = string.Empty;
            RefreshPageState();
        }

        private void ReturnToParentPage()
        {
            switch (currentPage)
            {
                case OverlayPage.Language:
                case OverlayPage.KeyBindings:
                    SetPage(OverlayPage.SettingsMenu);
                    return;
                case OverlayPage.TimeControl:
                case OverlayPage.TimeTest:
                    SetPage(OverlayPage.TimeMenu);
                    return;
                default:
                    SetPage(OverlayPage.Home);
                    return;
            }
        }

        private void RefreshPageState()
        {
            SetCardVisible(homeCard, currentPage == OverlayPage.Home);
            SetCardVisible(settingsMenuCard, currentPage == OverlayPage.SettingsMenu);
            SetCardVisible(languageCard, currentPage == OverlayPage.Language);
            SetCardVisible(keyBindingCard, currentPage == OverlayPage.KeyBindings);
            SetCardVisible(timeMenuCard, currentPage == OverlayPage.TimeMenu);
            SetCardVisible(timeControlCard, currentPage == OverlayPage.TimeControl);
            SetCardVisible(timeTestCard, currentPage == OverlayPage.TimeTest);

            if (footerPanel != null)
            {
                footerPanel.gameObject.SetActive(currentPage != OverlayPage.Home);
            }

            if (titleText != null)
            {
                titleText.text = ResolvePageTitle();
            }

            if (subtitleText != null)
            {
                subtitleText.text = ResolvePageDescription();
            }

            if (scrollContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
            }
        }

        private static void SetCardVisible(RectTransform card, bool visible)
        {
            if (card != null)
            {
                card.gameObject.SetActive(visible);
            }
        }

        private string ResolvePageTitle()
        {
            switch (currentPage)
            {
                case OverlayPage.SettingsMenu:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsTitle);
                case OverlayPage.Language:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language);
                case OverlayPage.KeyBindings:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingTitle);
                case OverlayPage.TimeMenu:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.AdjustTime);
                case OverlayPage.TimeControl:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeControlTitle);
                case OverlayPage.TimeTest:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestTitle);
                default:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.EscMenuTitle);
            }
        }

        private string ResolvePageDescription()
        {
            switch (currentPage)
            {
                case OverlayPage.SettingsMenu:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsDescription);
                case OverlayPage.Language:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsDescription);
                case OverlayPage.KeyBindings:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingDescription);
                case OverlayPage.TimeMenu:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeSettingsDescription);
                case OverlayPage.TimeControl:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeControlDescription);
                case OverlayPage.TimeTest:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestDescription);
                default:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.EscMenuDescription);
            }
        }

        private void BeginKeyBinding(CampusGameplayInputActionId actionId)
        {
            pendingKeyBindingAction = actionId;
            keyBindingStatusMessage = CampusPlayerUiTextCatalog.Format(
                CampusPlayerUiTextId.KeyBindingWaiting,
                GetKeyBindingActionLabel(actionId));
            RefreshKeyBindingRows();
        }

        private void CapturePendingKeyBinding()
        {
            if (!pendingKeyBindingAction.HasValue)
            {
                return;
            }

            if (!CampusInteractionInput.TryReadPressedKeyboardKey(out KeyCode pressedKey))
            {
                return;
            }

            CampusGameplayInputActionId actionId = pendingKeyBindingAction.Value;
            if (!CampusGameplayInputBindings.TrySetBinding(actionId, pressedKey, out CampusGameplayInputActionId conflict))
            {
                keyBindingStatusMessage = CampusPlayerUiTextCatalog.Format(
                    CampusPlayerUiTextId.KeyBindingConflict,
                    CampusInteractionInput.GetKeyLabel(pressedKey),
                    GetKeyBindingActionLabel(conflict));
                pendingKeyBindingAction = null;
                RefreshKeyBindingRows();
                return;
            }

            keyBindingStatusMessage = CampusPlayerUiTextCatalog.Format(
                CampusPlayerUiTextId.KeyBindingApplied,
                GetKeyBindingActionLabel(actionId),
                CampusInteractionInput.GetKeyLabel(pressedKey));
            pendingKeyBindingAction = null;
            RefreshKeyBindingRows();
        }

        private void ResetKeyBinding(CampusGameplayInputActionId actionId)
        {
            CampusGameplayInputBindings.ResetBinding(actionId);
            pendingKeyBindingAction = null;
            keyBindingStatusMessage = string.Empty;
            RefreshKeyBindingRows();
        }

        private void ResetAllKeyBindings()
        {
            CampusGameplayInputBindings.ResetAll();
            pendingKeyBindingAction = null;
            keyBindingStatusMessage = string.Empty;
            RefreshKeyBindingRows();
        }

        private void RefreshKeyBindingRows()
        {
            for (int i = 0; i < keyBindingRows.Count; i++)
            {
                KeyBindingRow row = keyBindingRows[i];
                if (row.ActionText != null)
                {
                    row.ActionText.text = GetKeyBindingActionLabel(row.ActionId);
                }

                bool pending = pendingKeyBindingAction.HasValue && pendingKeyBindingAction.Value == row.ActionId;
                SetButtonText(
                    row.KeyButton,
                    pending
                        ? CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingListening)
                        : CampusGameplayInputBindings.GetBindingLabel(row.ActionId));
                SetButtonText(row.ResetButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingReset));

                StorageBoxGraphic keyGraphic = row.KeyButton != null ? row.KeyButton.targetGraphic as StorageBoxGraphic : null;
                if (keyGraphic != null)
                {
                    keyGraphic.SetStyle(
                        pending ? CampusUiVisualTheme.AccentSoftFill : CampusUiVisualTheme.PanelDim,
                        pending ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderSoft,
                        pending ? 1.4f : 1.05f,
                        12f);
                }
            }

            if (keyBindingStatusText != null)
            {
                keyBindingStatusText.text = keyBindingStatusMessage;
                keyBindingStatusText.color = string.IsNullOrWhiteSpace(keyBindingStatusMessage)
                    ? CampusUiVisualTheme.TextMuted
                    : CampusUiVisualTheme.TextGold;
            }
        }

        private string GetKeyBindingActionLabel(CampusGameplayInputActionId actionId)
        {
            return CampusPlayerUiTextCatalog.Get(GetKeyBindingTextId(actionId));
        }

        private static CampusPlayerUiTextId GetKeyBindingTextId(CampusGameplayInputActionId actionId)
        {
            switch (actionId)
            {
                case CampusGameplayInputActionId.MoveUpPrimary: return CampusPlayerUiTextId.InputMoveUpPrimary;
                case CampusGameplayInputActionId.MoveDownPrimary: return CampusPlayerUiTextId.InputMoveDownPrimary;
                case CampusGameplayInputActionId.MoveLeftPrimary: return CampusPlayerUiTextId.InputMoveLeftPrimary;
                case CampusGameplayInputActionId.MoveRightPrimary: return CampusPlayerUiTextId.InputMoveRightPrimary;
                case CampusGameplayInputActionId.MoveUpSecondary: return CampusPlayerUiTextId.InputMoveUpSecondary;
                case CampusGameplayInputActionId.MoveDownSecondary: return CampusPlayerUiTextId.InputMoveDownSecondary;
                case CampusGameplayInputActionId.MoveLeftSecondary: return CampusPlayerUiTextId.InputMoveLeftSecondary;
                case CampusGameplayInputActionId.MoveRightSecondary: return CampusPlayerUiTextId.InputMoveRightSecondary;
                case CampusGameplayInputActionId.Interact: return CampusPlayerUiTextId.InputInteract;
                case CampusGameplayInputActionId.Sprint: return CampusPlayerUiTextId.InputSprint;
                case CampusGameplayInputActionId.Backpack: return CampusPlayerUiTextId.InputBackpack;
                case CampusGameplayInputActionId.Settings: return CampusPlayerUiTextId.InputSettings;
                case CampusGameplayInputActionId.ToggleMode: return CampusPlayerUiTextId.InputToggleMode;
                case CampusGameplayInputActionId.TimePause: return CampusPlayerUiTextId.InputTimePause;
                case CampusGameplayInputActionId.TimeNormalSpeed: return CampusPlayerUiTextId.InputTimeNormalSpeed;
                case CampusGameplayInputActionId.TimeFastSpeed: return CampusPlayerUiTextId.InputTimeFastSpeed;
                case CampusGameplayInputActionId.TimeMaxSpeed: return CampusPlayerUiTextId.InputTimeMaxSpeed;
                default: return CampusPlayerUiTextId.KeyBindingTitle;
            }
        }

        private void RefreshLocalizedText()
        {
            if (titleText != null)
            {
                titleText.text = ResolvePageTitle();
            }

            if (subtitleText != null)
            {
                subtitleText.text = ResolvePageDescription();
            }

            if (homeSectionTitleText != null)
            {
                homeSectionTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.EscMenuTitle);
            }

            if (languageSectionTitleText != null)
            {
                languageSectionTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language);
            }

            if (keyBindingSectionTitleText != null)
            {
                keyBindingSectionTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingTitle);
            }

            if (keyBindingDescriptionText != null)
            {
                keyBindingDescriptionText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingDescription);
            }

            if (timeControlSectionTitleText != null)
            {
                timeControlSectionTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeControlTitle);
            }

            if (timeControlDescriptionText != null)
            {
                timeControlDescriptionText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeControlDescription);
            }

            if (timeTestSectionTitleText != null)
            {
                timeTestSectionTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestTitle);
            }

            if (timeTestDescriptionText != null)
            {
                timeTestDescriptionText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestDescription);
            }

            if (timeTestDateLabelText != null)
            {
                timeTestDateLabelText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestDate);
            }

            if (timeTestHourLabelText != null)
            {
                timeTestHourLabelText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestHour);
            }

            if (timeTestMinuteLabelText != null)
            {
                timeTestMinuteLabelText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestMinute);
            }

            if (footerHintText != null)
            {
                footerHintText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Continue);
            }

            for (int i = 0; i < languageButtonViews.Count; i++)
            {
                LanguageButtonView view = languageButtonViews[i];
                SetButtonText(view.Button, CampusPlayerUiTextCatalog.Get(view.LabelId));
            }

            SetButtonText(menuContinueButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Continue));
            SetButtonText(menuSettingsButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsTitle));
            SetButtonText(menuTimeButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.AdjustTime));
            SetButtonText(menuReturnButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.ReturnToMainMenu));
            SetButtonText(settingsLanguageButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language));
            SetButtonText(settingsKeyBindingButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingTitle));
            SetButtonText(timeControlMenuButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeControlTitle));
            SetButtonText(timeTestMenuButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestTitle));
            SetButtonText(resetAllKeyBindingsButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingResetAll));
            SetButtonText(pauseButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimePause));
            SetButtonText(resumeButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeResume));
            SetButtonText(timeResetButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestReset));
            SetButtonText(timeApplyButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestApply));
            SetButtonText(backButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.BackToEscMenu));
            SetButtonText(continueButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Continue));

            RefreshLanguageButtons();
            RefreshKeyBindingRows();
            RefreshTimeControlState();
            RefreshTimeTestStatus();
            RefreshPageState();
        }

        private void SetButtonText(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            Text buttonText = button.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = text;
            }
        }

        private void RefreshLanguageButtons()
        {
            for (int i = 0; i < languageButtonViews.Count; i++)
            {
                LanguageButtonView view = languageButtonViews[i];
                SetLanguageButtonStyle(view.Button, view.Language);
            }
        }

        private void SetLanguageButtonStyle(Button button, CampusDisplayLanguage language)
        {
            if (button == null)
            {
                return;
            }

            bool selected = CampusLanguageState.CurrentLanguage == language;
            StorageBoxGraphic graphic = button.targetGraphic as StorageBoxGraphic;
            if (graphic != null)
            {
                graphic.SetStyle(
                    selected ? CampusUiVisualTheme.AccentSoftFill : CampusUiVisualTheme.PanelDim,
                    selected ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderSoft,
                    selected ? 1.4f : 1.1f,
                    14f);
            }

            Text buttonText = button.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.color = selected ? CampusUiVisualTheme.TextGold : CampusUiVisualTheme.TextPrimary;
            }
        }

        private void RefreshTimeControlState()
        {
            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            if (timeController == null)
            {
                return;
            }

            if (timeControlStateText != null)
            {
                timeControlStateText.text = CampusPlayerUiTextCatalog.Get(
                    timeController.IsTimePaused
                        ? CampusPlayerUiTextId.TimePauseStatus
                        : CampusPlayerUiTextId.TimeRunningStatus);
            }

            StorageBoxGraphic pauseGraphic = pauseButton != null ? pauseButton.targetGraphic as StorageBoxGraphic : null;
            if (pauseGraphic != null)
            {
                pauseGraphic.SetStyle(
                    timeController.IsTimePaused ? CampusUiVisualTheme.Warning : CampusUiVisualTheme.PanelDim,
                    CampusUiVisualTheme.BorderSoft,
                    1.05f,
                    14f);
            }

            StorageBoxGraphic resumeGraphic = resumeButton != null ? resumeButton.targetGraphic as StorageBoxGraphic : null;
            if (resumeGraphic != null)
            {
                resumeGraphic.SetStyle(
                    timeController.IsTimePaused ? CampusUiVisualTheme.PanelDim : CampusUiVisualTheme.SuccessSoft,
                    CampusUiVisualTheme.BorderSoft,
                    1.05f,
                    14f);
            }
        }

        private void RefreshTimeTestStatus()
        {
            if (timeTestStatusText != null)
            {
                timeTestStatusText.text = timeTestStatusMessage;
                timeTestStatusText.color = string.IsNullOrWhiteSpace(timeTestStatusMessage) ? CampusUiVisualTheme.TextMuted : CampusUiVisualTheme.Warning;
            }
        }

        private void SyncTimeDraftFromController()
        {
            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            if (timeController == null)
            {
                return;
            }

            CampusGameDate date = timeController.CurrentDate;
            int minuteOfDay = CampusTimeSchedule.NormalizeMinuteOfDay(
                Mathf.FloorToInt(timeController.CurrentGameHour * 60f));
            int hour = minuteOfDay / 60;
            int minute = minuteOfDay % 60;

            timeTestYearText = date.Year.ToString();
            timeTestMonthText = date.Month.ToString("00");
            timeTestDayText = date.Day.ToString("00");
            timeTestHourText = hour.ToString("00");
            timeTestMinuteText = minute.ToString("00");

            if (yearField != null) yearField.SetTextWithoutNotify(timeTestYearText);
            if (monthField != null) monthField.SetTextWithoutNotify(timeTestMonthText);
            if (dayField != null) dayField.SetTextWithoutNotify(timeTestDayText);
            if (hourField != null) hourField.SetTextWithoutNotify(timeTestHourText);
            if (minuteField != null) minuteField.SetTextWithoutNotify(timeTestMinuteText);
        }

        private void ApplyTimeDraft()
        {
            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            if (timeController == null)
            {
                return;
            }

            timeTestYearText = yearField != null ? yearField.text : timeTestYearText;
            timeTestMonthText = monthField != null ? monthField.text : timeTestMonthText;
            timeTestDayText = dayField != null ? dayField.text : timeTestDayText;
            timeTestHourText = hourField != null ? hourField.text : timeTestHourText;
            timeTestMinuteText = minuteField != null ? minuteField.text : timeTestMinuteText;

            if (!int.TryParse(timeTestYearText, out int year) ||
                !int.TryParse(timeTestMonthText, out int month) ||
                !int.TryParse(timeTestDayText, out int day))
            {
                timeTestStatusMessage = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidDate);
                RefreshTimeTestStatus();
                return;
            }

            if (!int.TryParse(timeTestHourText, out int hour))
            {
                timeTestStatusMessage = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidHour);
                RefreshTimeTestStatus();
                return;
            }

            if (!int.TryParse(timeTestMinuteText, out int minute))
            {
                timeTestStatusMessage = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidMinute);
                RefreshTimeTestStatus();
                return;
            }

            if (!timeController.TrySetDateAndClock(year, month, day, hour, minute, true, out string errorMessage))
            {
                timeTestStatusMessage = errorMessage;
                RefreshTimeTestStatus();
                return;
            }

            timeTestStatusMessage = string.Empty;
            RefreshTimeTestStatus();
            SyncTimeDraftFromController();
            RefreshTimeControlState();
        }

        private void SetLanguage(CampusDisplayLanguage language)
        {
            CampusLanguageState.SetLanguage(language);
            RefreshLocalizedText();
        }

        private void ReturnToMainMenu()
        {
            isVisible = false;
            pauseCaptured = false;
            CampusLaunchConfigStore.Clear();
            SceneManager.LoadScene(StartupSceneName, LoadSceneMode.Single);
        }

        private void SetVisible(bool visible, bool immediate)
        {
            if (isVisible == visible && !immediate)
            {
                return;
            }

            isVisible = visible;
            if (visible)
            {
                if (mainCanvasGroup == null || mainPanel == null)
                {
                    return;
                }

                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = SortingOrder;
                    canvas.transform.SetAsLastSibling();
                }

                SetPage(OverlayPage.Home);
                SyncTimeDraftFromController();
                RefreshKeyBindingRows();
                RefreshTimeControlState();
                if (!pauseCaptured)
                {
                    pauseState = CampusGameplayPauseUtility.Pause(bootstrap);
                    pauseCaptured = true;
                }

                if (backdropPanel != null)
                {
                    backdropPanel.gameObject.SetActive(true);
                }

                mainPanel.gameObject.SetActive(true);

                KillTween(ref mainPanelTween);
                if (immediate)
                {
                    mainCanvasGroup.alpha = 1f;
                    mainCanvasGroup.interactable = true;
                    mainCanvasGroup.blocksRaycasts = true;
                    mainPanel.localScale = Vector3.one;
                }
                else
                {
                    mainPanelTween = CampusUiTweenUtility.OpenPanel(mainCanvasGroup, mainPanel, 0.24f, 0.97f);
                }

                return;
            }

            if (pauseCaptured)
            {
                CampusGameplayPauseUtility.Resume(bootstrap, pauseState);
                pauseCaptured = false;
            }

            pendingKeyBindingAction = null;
            keyBindingStatusMessage = string.Empty;
            RefreshKeyBindingRows();

            if (mainCanvasGroup == null || mainPanel == null)
            {
                return;
            }

            KillTween(ref mainPanelTween);
            if (immediate)
            {
                mainCanvasGroup.alpha = 0f;
                mainCanvasGroup.interactable = false;
                mainCanvasGroup.blocksRaycasts = false;
                mainPanel.gameObject.SetActive(false);
                if (backdropPanel != null)
                {
                    backdropPanel.gameObject.SetActive(false);
                }
                return;
            }

            mainPanelTween = CampusUiTweenUtility.ClosePanel(mainCanvasGroup, mainPanel, 0.18f, 0.98f);
            mainPanelTween.OnComplete(() =>
            {
                mainPanel.gameObject.SetActive(false);
                if (backdropPanel != null)
                {
                    backdropPanel.gameObject.SetActive(false);
                }
            });
        }

        private void KillTween(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }

            tween = null;
        }

        private sealed class KeyBindingRow
        {
            public KeyBindingRow(
                CampusGameplayInputActionId actionId,
                Text actionText,
                Button keyButton,
                Button resetButton)
            {
                ActionId = actionId;
                ActionText = actionText;
                KeyButton = keyButton;
                ResetButton = resetButton;
            }

            public CampusGameplayInputActionId ActionId { get; }
            public Text ActionText { get; }
            public Button KeyButton { get; }
            public Button ResetButton { get; }
        }
    }
}
