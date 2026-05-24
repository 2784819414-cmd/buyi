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
        private const string StartupSceneName = "Startup";
        private const int SortingOrder = 39500;
        private const float MainPanelWidth = 1720f;
        private const float MainPanelHeight = 760f;
        private const float MainPanelPadding = 20f;
        private const float MainPanelContentWidth = MainPanelWidth - MainPanelPadding * 2f;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        private Canvas canvas;
        private RectTransform canvasRoot;
        private RectTransform backdropPanel;
        private RectTransform mainPanel;
        private CanvasGroup mainCanvasGroup;
        private RectTransform scrollContent;
        private CanvasGroup loadingCanvasGroup;
        private Tween mainPanelTween;
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
        private Text languageSectionTitleText;
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

        private Button chineseButton;
        private Button englishButton;
        private Button bilingualButton;
        private Button pauseButton;
        private Button resumeButton;
        private Button timeResetButton;
        private Button timeApplyButton;
        private Button returnButton;
        private Button continueButton;

        private InputField yearField;
        private InputField monthField;
        private InputField dayField;
        private InputField hourField;
        private InputField minuteField;

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
            if (!CampusInteractionInput.WasKeyPressed(toggleKey))
            {
                return;
            }

            CampusRuntimeMapEditor runtimeMapEditor = CampusRuntimeMapEditor.Instance;
            if (runtimeMapEditor != null && runtimeMapEditor.IsOpen)
            {
                return;
            }

            SetVisible(!isVisible);
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
                false);
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
            RectTransform header = CampusUiRuntimeBuilder.CreatePanel(
                "Header",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -22f),
                new Vector2(-MainPanelPadding * 2f, 112f),
                CampusUiVisualTheme.PanelRaised,
                CampusUiVisualTheme.BorderSoft,
                1.15f,
                22f,
                false);

            titleText = CampusUiRuntimeBuilder.CreateText(
                "Title",
                header,
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
                header,
                string.Empty,
                17,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextSecondary);
            subtitleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            subtitleText.rectTransform.anchorMax = new Vector2(0f, 0f);
            subtitleText.rectTransform.pivot = new Vector2(0f, 0f);
            subtitleText.rectTransform.anchoredPosition = new Vector2(30f, 16f);
            subtitleText.rectTransform.sizeDelta = new Vector2(700f, 42f);

            Text tip = CampusUiRuntimeBuilder.CreateText(
                "Tip",
                header,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Continue),
                14,
                TextAnchor.LowerRight,
                CampusUiVisualTheme.TextMuted);
            tip.rectTransform.anchorMin = new Vector2(1f, 0f);
            tip.rectTransform.anchorMax = new Vector2(1f, 0f);
            tip.rectTransform.pivot = new Vector2(1f, 0f);
            tip.rectTransform.anchoredPosition = new Vector2(-28f, 16f);
            tip.rectTransform.sizeDelta = new Vector2(260f, 32f);
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

            RectTransform languageCard = CreateSectionCard(scrollContent, "LanguageCard", 96f, out languageSectionTitleText);
            BuildLanguageCard(languageCard);

            RectTransform controlCard = CreateSectionCard(scrollContent, "TimeControlCard", 154f, out timeControlSectionTitleText);
            BuildTimeControlCard(controlCard);

            RectTransform testCard = CreateSectionCard(scrollContent, "TimeTestCard", 256f, out timeTestSectionTitleText);
            BuildTimeTestCard(testCard);
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

            chineseButton = CreateLanguageButton(card, CampusDisplayLanguage.Chinese, CampusPlayerUiTextId.Chinese, new Vector2(132f, -52f));
            englishButton = CreateLanguageButton(card, CampusDisplayLanguage.English, CampusPlayerUiTextId.English, new Vector2(260f, -52f));
            bilingualButton = CreateLanguageButton(card, CampusDisplayLanguage.Bilingual, CampusPlayerUiTextId.Bilingual, new Vector2(388f, -52f));
        }

        private Button CreateLanguageButton(
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
            rect.sizeDelta = new Vector2(120f, 34f);
            return button;
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

            returnButton = CampusUiRuntimeBuilder.CreateButton(
                "ReturnButton",
                footer,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.ReturnToMainMenu),
                ReturnToMainMenu,
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft,
                16f,
                1.1f,
                CampusUiVisualTheme.TextSecondary,
                16);
            RectTransform returnRect = returnButton.GetComponent<RectTransform>();
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

        private void RefreshLocalizedText()
        {
            if (titleText != null)
            {
                titleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsTitle);
            }

            if (subtitleText != null)
            {
                subtitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsDescription);
            }

            if (languageSectionTitleText != null)
            {
                languageSectionTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language);
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

            SetButtonText(chineseButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Chinese));
            SetButtonText(englishButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.English));
            SetButtonText(bilingualButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Bilingual));
            SetButtonText(pauseButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimePause));
            SetButtonText(resumeButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeResume));
            SetButtonText(timeResetButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestReset));
            SetButtonText(timeApplyButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestApply));
            SetButtonText(returnButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.ReturnToMainMenu));
            SetButtonText(continueButton, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Continue));

            RefreshLanguageButtons();
            RefreshTimeControlState();
            RefreshTimeTestStatus();
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
            SetLanguageButtonStyle(chineseButton, CampusDisplayLanguage.Chinese);
            SetLanguageButtonStyle(englishButton, CampusDisplayLanguage.English);
            SetLanguageButtonStyle(bilingualButton, CampusDisplayLanguage.Bilingual);
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

                SyncTimeDraftFromController();
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
    }
}
