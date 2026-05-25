using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using Nting.Storage;
using NtingCampusMapEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusStartupSelectionController : MonoBehaviour
    {
        private sealed class LoadOption
        {
            public string Label;
            public string Path;
            public bool IsNone;
            public bool IsSceneDefault;
            public CampusRuntimeMapLoadSource Source;
        }

        private sealed class LoadOptionView
        {
            public LoadOption Option;
            public RectTransform Root;
            public StorageBoxGraphic Background;
            public Button Button;
            public Text TitleText;
            public Text MetaText;
            public LayoutElement Layout;
        }

        private sealed class LanguageButtonView
        {
            public CampusDisplayLanguage Language;
            public CampusPlayerUiTextId LabelId;
            public Button Button;
            public Text Label;
        }

        private sealed class SettingsSectionButtonView
        {
            public SettingsSection Section;
            public CampusPlayerUiTextId LabelId;
            public Button Button;
            public Text Label;
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

        private enum StartupPage
        {
            Home,
            Play,
            Settings,
            About
        }

        private enum SettingsSection
        {
            Language,
            KeyBindings
        }

        private const string StartupSceneName = "Startup";
        private const string GameplaySceneName = "CampusMap";
        private const string StartupBackgroundResourcePath = "UI/Startup/startup_background_nostalgia_v1";
        private const string StartupLoadingOverlayResourcePath = "UI/Startup/startup_loading_overlay_v1";
        private const string StartupLogoChineseResourcePath = "UI/Startup/startup_logo_nting_campus_v1";
        private const string StartupLogoEnglishResourcePath = "UI/Startup/startup_logo_nting_campus_en_v1";
        private const string StartupLogoTraditionalChineseResourcePath = "UI/Startup/startup_logo_nting_campus_zh_hant_v1";
        private const string StartupLogoRussianResourcePath = "UI/Startup/startup_logo_nting_campus_ru_v1";
        private const string StartupLogoJapaneseResourcePath = "UI/Startup/startup_logo_nting_campus_ja_v1";
        private const int SortingOrder = 40000;
        private const float LoadingProgressWidth = 1288f;
        private const float MinimumLoadingVisibleSeconds = 1.6f;
        private const float LoadingActivationProgress = 0.985f;
        private static readonly Color StartupBackdropTint = new Color(0.02f, 0.02f, 0.02f, 0.72f);
        private static readonly Color StartupBackdropImageTint = new Color(1f, 1f, 1f, 0.52f);
        private static readonly Color StartupMainPanelFill = new Color(0.10f, 0.11f, 0.10f, 0.62f);
        private static readonly Color StartupMainPanelBorder = new Color(1f, 0.80f, 0.42f, 0.14f);
        private static readonly Color StartupSectionFill = new Color(0.13f, 0.14f, 0.12f, 0.54f);
        private static readonly Color StartupSectionBorder = new Color(1f, 0.80f, 0.42f, 0.08f);
        private static readonly Color StartupCardFill = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color StartupCardFillSelected = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color StartupFooterFill = new Color(0.08f, 0.09f, 0.08f, 0.60f);
        private static readonly Color StartupAccent = new Color(1f, 0.75f, 0.30f, 0.86f);

        [SerializeField] private bool showOnStartup = true;
        [SerializeField, Min(1f)] private float windowWidth = 1560f;
        [SerializeField, Min(1f)] private float windowHeight = 920f;

        private readonly List<LoadOption> mapOptions = new List<LoadOption>();
        private readonly List<LoadOption> saveOptions = new List<LoadOption>();
        private readonly List<LoadOptionView> mapOptionViews = new List<LoadOptionView>();
        private readonly List<LoadOptionView> saveOptionViews = new List<LoadOptionView>();
        private readonly List<LanguageButtonView> languageButtonViews = new List<LanguageButtonView>();
        private readonly List<SettingsSectionButtonView> settingsSectionButtonViews = new List<SettingsSectionButtonView>();
        private readonly List<KeyBindingRow> keyBindingRows = new List<KeyBindingRow>();
        private readonly Dictionary<CampusDisplayLanguage, Sprite> startupLogoSprites = new Dictionary<CampusDisplayLanguage, Sprite>();

        private Canvas canvas;
        private RectTransform canvasRoot;
        private CanvasGroup mainCanvasGroup;
        private CanvasGroup loadingCanvasGroup;
        private RectTransform mainPanel;
        private RectTransform headerPanel;
        private RectTransform loadingPanel;
        private RectTransform homePanel;
        private RectTransform settingsPanel;
        private RectTransform settingsLanguageContentPanel;
        private RectTransform settingsKeyBindingContentPanel;
        private RectTransform mapPanel;
        private RectTransform savePanel;
        private RectTransform footerPanel;
        private RectTransform aboutPanel;
        private RectTransform mapContent;
        private RectTransform saveContent;
        private Text mapCountText;
        private Text saveCountText;
        private Text statusText;
        private Text newMapNamePlaceholderText;
        private InputField newMapNameField;
        private Button createMapButton;
        private Button refreshButton;
        private Button startButton;
        private Button homeStartButton;
        private Button homeSettingsButton;
        private Button homeAboutButton;
        private Button playBackButton;
        private Button settingsBackButton;
        private Button aboutBackButton;
        private Button resetAllKeyBindingsButton;
        private Text createMapButtonText;
        private Text refreshButtonText;
        private Text startButtonText;
        private Text homeStartButtonText;
        private Text homeSettingsButtonText;
        private Text homeAboutButtonText;
        private Text settingsLanguageTitleText;
        private Text settingsLanguageDescriptionText;
        private Image homeLogoImage;
        private Text playBackButtonText;
        private Text settingsBackButtonText;
        private Text aboutBackButtonText;
        private Text headerTitleText;
        private Text mapTitleText;
        private Text saveTitleText;
        private Text aboutTitleText;
        private Text aboutBodyText;
        private Text keyBindingSectionTitleText;
        private Text keyBindingDescriptionText;
        private Text keyBindingStatusText;
        private Text loadingTitleText;
        private Text headerSubtitleText;
        private Text loadingStatusText;
        private RectTransform loadingFillRect;
        private RectTransform loadingSweepRect;
        private AsyncOperation loadingOperation;
        private Tween mainPanelTween;
        private Tween loadingPanelTween;
        private Tween loadingSweepTween;
        private Sprite startupBackgroundSprite;
        private Sprite startupLoadingOverlaySprite;
        private Sprite startupLogoSprite;
        private string newMapName = string.Empty;
        private string statusMessage = string.Empty;
        private string keyBindingStatusMessage = string.Empty;
        private float loadingProgress;
        private float displayedLoadingProgress;
        private float loadingVisibleTime;
        private int selectedMapIndex;
        private int selectedSaveIndex;
        private StartupPage currentPage = StartupPage.Home;
        private SettingsSection currentSettingsSection = SettingsSection.Language;
        private CampusGameplayInputActionId? pendingKeyBindingAction;
        private bool isLoading;
        private bool loadingSceneActivationRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallStartupSceneWatcher()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstallForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstallForScene(scene);
        }

        private static void TryInstallForScene(Scene scene)
        {
            if (!scene.IsValid() || !string.Equals(scene.name, StartupSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindFirstObjectByType<CampusStartupSelectionController>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            EnsureStartupCamera();

            GameObject host = new GameObject("CampusStartupSelection");
            host.AddComponent<CampusStartupSelectionController>();
        }

        private void Start()
        {
            if (!showOnStartup)
            {
                enabled = false;
                return;
            }

            EnsureVisual();
            RebuildOptions();
            SetMainPanelVisible(true, true);
            SetLoadingVisible(false, true);
        }

        private void Update()
        {
            if (currentPage == StartupPage.Settings &&
                currentSettingsSection == SettingsSection.KeyBindings &&
                pendingKeyBindingAction.HasValue)
            {
                CapturePendingKeyBinding();
                if (!isLoading)
                {
                    return;
                }
            }

            if (!isLoading || loadingOperation == null)
            {
                return;
            }

            UpdateLoadingProgress();
            UpdateDisplayedLoadingProgress();
            TryActivateLoadedScene();
            if (loadingOperation.isDone)
            {
                SetLoadingProgress(1f);
                displayedLoadingProgress = 1f;
                ApplyLoadingProgressVisual(displayedLoadingProgress);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            KillTween(ref mainPanelTween);
            KillTween(ref loadingPanelTween);
            KillTween(ref loadingSweepTween);
        }

        public void RebuildOptions()
        {
            int previousMapIndex = selectedMapIndex;
            int previousSaveIndex = selectedSaveIndex;

            mapOptions.Clear();
            saveOptions.Clear();
            statusMessage = string.Empty;

            mapOptions.Add(new LoadOption
            {
                Label = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SceneDefault),
                IsSceneDefault = true,
                Source = CampusRuntimeMapLoadSource.Scene
            });

            string mapFolder = ResolveMapFolder();
            if (Directory.Exists(mapFolder))
            {
                string[] mapFiles = Directory.GetFiles(mapFolder, "*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(mapFiles, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < mapFiles.Length; i++)
                {
                    string fileName = Path.GetFileName(mapFiles[i]);
                    if (string.Equals(fileName, "authoring_manifest.json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (fileName.EndsWith(".gameplay.json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    mapOptions.Add(new LoadOption
                    {
                        Label = BuildMapLabel(fileName),
                        Path = mapFiles[i],
                        Source = CampusRuntimeMapLoadSource.AuthoringPackage
                    });
                }
            }

            saveOptions.Add(new LoadOption
            {
                Label = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.NoSave),
                IsNone = true,
                Source = CampusRuntimeMapLoadSource.Scene
            });

            string saveFolder = ResolveSaveFolder();
            string autoSavePath = Path.Combine(saveFolder, "CampusMap_PlayerSave.json");
            if (File.Exists(autoSavePath))
            {
                saveOptions.Add(new LoadOption
                {
                    Label = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.AutoSave),
                    Path = autoSavePath,
                    Source = CampusRuntimeMapLoadSource.PlayerSave
                });
            }

            string exportFolder = ResolveExportFolder();
            if (Directory.Exists(exportFolder))
            {
                string[] exportFiles = Directory.GetFiles(exportFolder, "CampusMap_*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(exportFiles, CompareLastWriteTimeDescending);
                for (int i = 0; i < exportFiles.Length; i++)
                {
                    string path = exportFiles[i];
                    if (string.Equals(path, autoSavePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    saveOptions.Add(new LoadOption
                    {
                        Label = Path.GetFileNameWithoutExtension(path),
                        Path = path,
                        Source = CampusRuntimeMapLoadSource.PlayerSave
                    });
                }
            }

            if (canvas != null)
            {
                selectedMapIndex = mapOptions.Count > 0 ? Mathf.Clamp(previousMapIndex, 0, mapOptions.Count - 1) : 0;
                selectedSaveIndex = saveOptions.Count > 0 ? Mathf.Clamp(previousSaveIndex, 0, saveOptions.Count - 1) : 0;
                RefreshOptionPanels();
                RefreshFooterText();
            }
        }

        private void BeginGameLoad()
        {
            if (mapOptions.Count == 0 || saveOptions.Count == 0)
            {
                SetStatus(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.NoLaunchOptions), true);
                return;
            }

            LoadOption mapOption = mapOptions[Mathf.Clamp(selectedMapIndex, 0, mapOptions.Count - 1)];
            LoadOption saveOption = saveOptions[Mathf.Clamp(selectedSaveIndex, 0, saveOptions.Count - 1)];

            CampusLaunchConfigStore.SetPendingSelection(
                mapOption != null && !mapOption.IsSceneDefault ? mapOption.Path : string.Empty,
                mapOption != null ? mapOption.Source : CampusRuntimeMapLoadSource.Scene,
                saveOption != null && !saveOption.IsNone ? saveOption.Path : string.Empty,
                saveOption != null ? saveOption.Source : CampusRuntimeMapLoadSource.Scene);

            StartGameplaySceneLoad();
        }

        private void BeginNewMapLoad()
        {
            string sanitizedMapName = SanitizeMapName(newMapName);
            if (string.IsNullOrWhiteSpace(sanitizedMapName))
            {
                SetStatus(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.EnterNewMapName), true);
                return;
            }

            string mapFolder = ResolveMapFolder();
            Directory.CreateDirectory(mapFolder);
            string mapPath = Path.Combine(mapFolder, "CampusMap_" + sanitizedMapName + ".json");
            if (File.Exists(mapPath))
            {
                SetStatus(CampusPlayerUiTextCatalog.Format(
                    CampusPlayerUiTextId.MapAlreadyExists,
                    Path.GetFileNameWithoutExtension(mapPath)), true);
                return;
            }

            CampusLaunchConfigStore.SetPendingSelection(
                mapPath,
                CampusRuntimeMapLoadSource.AuthoringPackage,
                string.Empty,
                CampusRuntimeMapLoadSource.Scene,
                true,
                sanitizedMapName);

            StartGameplaySceneLoad();
        }

        private void StartGameplaySceneLoad()
        {
            isLoading = true;
            loadingSceneActivationRequested = false;
            SetMainPanelVisible(false, true);
            SetLoadingVisible(true, true);
            SetLoadingStatus(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingGameplayScene));
            loadingOperation = SceneManager.LoadSceneAsync(GameplaySceneName, LoadSceneMode.Single);
            if (loadingOperation == null)
            {
                isLoading = false;
                SetLoadingVisible(false, false);
                SetMainPanelVisible(true, false);
                SetStatus(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingFailed), true);
                CampusLaunchConfigStore.Clear();
                return;
            }

            loadingOperation.allowSceneActivation = false;
        }

        private void EnsureVisual()
        {
            if (canvas != null)
            {
                return;
            }

            startupBackgroundSprite = startupBackgroundSprite != null
                ? startupBackgroundSprite
                : Resources.Load<Sprite>(StartupBackgroundResourcePath);
            startupLoadingOverlaySprite = startupLoadingOverlaySprite != null
                ? startupLoadingOverlaySprite
                : Resources.Load<Sprite>(StartupLoadingOverlayResourcePath);
            startupLogoSprite = ResolveStartupLogoSprite();

            CampusUiRuntimeBuilder.EnsureEventSystem();
            canvas = CampusUiRuntimeBuilder.CreateScreenCanvas(gameObject, "CampusStartupCanvas", SortingOrder);
            canvasRoot = canvas.GetComponent<RectTransform>();

            BuildBackdrop(canvasRoot);

            mainPanel = CampusUiRuntimeBuilder.CreatePanel(
                "StartupPanel",
                canvasRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(windowWidth, windowHeight),
                StartupMainPanelFill,
                StartupMainPanelBorder,
                0.95f,
                18f,
                false);
            mainCanvasGroup = mainPanel.gameObject.AddComponent<CanvasGroup>();
            mainCanvasGroup.alpha = 1f;
            mainCanvasGroup.interactable = true;
            mainCanvasGroup.blocksRaycasts = true;

            BuildHeader(mainPanel);
            BuildHomePanel(mainPanel);
            BuildSettingsPanel(mainPanel);
            BuildOptionPanels(mainPanel);
            BuildFooter(mainPanel);
            BuildAboutPanel(mainPanel);
            BuildLoadingOverlay(canvasRoot);
            RefreshLocalizedText();
            SetPage(StartupPage.Home);
        }

        private void BuildBackdrop(Transform parent)
        {
            RectTransform backdrop = CampusUiRuntimeBuilder.CreateFullScreenPanel(
                "Backdrop",
                parent,
                CampusUiVisualTheme.BackgroundDeep,
                false);
            backdrop.SetAsFirstSibling();

            if (startupBackgroundSprite != null)
            {
                Image artwork = CampusUiRuntimeBuilder.CreateImage(
                    "BackdropArtwork",
                    backdrop,
                    startupBackgroundSprite,
                    StartupBackdropImageTint,
                    false,
                    false);
                artwork.rectTransform.offsetMin = new Vector2(-12f, -12f);
                artwork.rectTransform.offsetMax = new Vector2(12f, 12f);
            }

            RectTransform dimmer = CampusUiRuntimeBuilder.CreateFullScreenPanel(
                "BackdropDimmer",
                backdrop,
                StartupBackdropTint,
                false);
            dimmer.SetAsLastSibling();
        }

        private void BuildHeader(Transform parent)
        {
            headerPanel = CampusUiRuntimeBuilder.CreatePanel(
                "Header",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(windowWidth - 48f, 62f),
                Color.clear,
                Color.clear,
                0f,
                0f,
                false);

            headerTitleText = CampusUiRuntimeBuilder.CreateText(
                "Title",
                headerPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartupTitle),
                34,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            headerTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerTitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            headerTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            headerTitleText.rectTransform.anchoredPosition = new Vector2(24f, -8f);
            headerTitleText.rectTransform.sizeDelta = new Vector2(420f, 42f);

            headerSubtitleText = CampusUiRuntimeBuilder.CreateText(
                "Subtitle",
                headerPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartupDescription),
                16,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextSecondary);
            headerSubtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerSubtitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            headerSubtitleText.rectTransform.pivot = new Vector2(0f, 1f);
            headerSubtitleText.rectTransform.anchoredPosition = new Vector2(30f, -52f);
            headerSubtitleText.rectTransform.sizeDelta = new Vector2(900f, 26f);
        }

        private void BuildHomePanel(Transform parent)
        {
            homePanel = CampusUiRuntimeBuilder.CreatePanel(
                "HomePanel",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -150f),
                new Vector2(windowWidth - 48f, 746f),
                new Color(0.19f, 0.20f, 0.16f, 0.70f),
                new Color(0.84f, 0.62f, 0.34f, 0.32f),
                1.05f,
                18f,
                false);
            homePanel.GetComponent<StorageBoxGraphic>()?.SetStyle(Color.clear, Color.clear, 0f, 0f);

            BuildHomeLogo(homePanel);

            homeStartButton = CreateMainMenuButton(
                homePanel,
                "HomeStartButton",
                CampusPlayerUiTextId.StartGame,
                new Vector2(430f, -300f),
                () => SetPage(StartupPage.Play),
                true);
            homeSettingsButton = CreateMainMenuButton(
                homePanel,
                "HomeSettingsButton",
                CampusPlayerUiTextId.SettingsTitle,
                new Vector2(430f, -382f),
                () => SetPage(StartupPage.Settings),
                false);
            homeAboutButton = CreateMainMenuButton(
                homePanel,
                "HomeAboutButton",
                CampusPlayerUiTextId.About,
                new Vector2(430f, -464f),
                () => SetPage(StartupPage.About),
                false);

            homeStartButtonText = homeStartButton.GetComponentInChildren<Text>();
            homeSettingsButtonText = homeSettingsButton.GetComponentInChildren<Text>();
            homeAboutButtonText = homeAboutButton.GetComponentInChildren<Text>();
        }

        private void BuildHomeLogo(Transform parent)
        {
            homeLogoImage = CampusUiRuntimeBuilder.CreateImage(
                "HomeLogo",
                parent,
                ResolveStartupLogoSprite(),
                Color.white,
                true,
                false);
            homeLogoImage.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            homeLogoImage.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            homeLogoImage.rectTransform.pivot = new Vector2(0.5f, 1f);
            homeLogoImage.rectTransform.anchoredPosition = new Vector2(0f, 140f);
            homeLogoImage.rectTransform.sizeDelta = new Vector2(880f, 410f);
            homeLogoImage.gameObject.AddComponent<CampusStartupLogoGlowController>();
        }

        private Sprite ResolveStartupLogoSprite()
        {
            CampusDisplayLanguage language = CampusLanguageState.CurrentLanguage;
            if (startupLogoSprites.TryGetValue(language, out Sprite cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            string resourcePath = ResolveStartupLogoResourcePath(language);
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null && language != CampusDisplayLanguage.Chinese)
            {
                sprite = Resources.Load<Sprite>(StartupLogoChineseResourcePath);
            }

            if (sprite != null)
            {
                startupLogoSprites[language] = sprite;
            }

            return sprite;
        }

        private static string ResolveStartupLogoResourcePath(CampusDisplayLanguage language)
        {
            switch (CampusDisplayLanguageCatalog.Normalize(language))
            {
                case CampusDisplayLanguage.English:
                    return StartupLogoEnglishResourcePath;
                case CampusDisplayLanguage.TraditionalChinese:
                    return StartupLogoTraditionalChineseResourcePath;
                case CampusDisplayLanguage.Russian:
                    return StartupLogoRussianResourcePath;
                case CampusDisplayLanguage.Japanese:
                    return StartupLogoJapaneseResourcePath;
                default:
                    return StartupLogoChineseResourcePath;
            }
        }

        private Button CreateMainMenuButton(
            Transform parent,
            string name,
            CampusPlayerUiTextId labelId,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction action,
            bool primary)
        {
            Button button = CampusUiRuntimeBuilder.CreateButton(
                name,
                parent,
                CampusPlayerUiTextCatalog.Get(labelId),
                action,
                primary ? CampusUiVisualTheme.AccentSoftFill : CampusUiVisualTheme.PanelDim,
                primary ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderSoft,
                18f,
                primary ? 1.35f : 1.05f,
                CampusUiVisualTheme.TextPrimary,
                28);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(700f, 64f);
            return button;
        }

        private void BuildSettingsPanel(Transform parent)
        {
            settingsPanel = CampusUiRuntimeBuilder.CreatePanel(
                "SettingsPanel",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -150f),
                new Vector2(windowWidth - 48f, 746f),
                CampusUiVisualTheme.PanelSoft,
                CampusUiVisualTheme.BorderMuted,
                1.0f,
                18f,
                false);

            ScrollRect selectorScrollRect = CampusUiRuntimeBuilder.CreateScrollView(
                "StartupSettingsSectionSelector",
                settingsPanel,
                new Vector2(windowWidth - 92f, 176f),
                out RectTransform selectorViewport,
                out RectTransform selectorContent,
                CampusUiVisualTheme.PanelRaised,
                CampusUiVisualTheme.BorderSoft,
                1.05f,
                18f);

            RectTransform selectorScrollRectTransform = selectorScrollRect.GetComponent<RectTransform>();
            selectorScrollRectTransform.anchorMin = new Vector2(0f, 1f);
            selectorScrollRectTransform.anchorMax = new Vector2(0f, 1f);
            selectorScrollRectTransform.pivot = new Vector2(0f, 1f);
            selectorScrollRectTransform.anchoredPosition = new Vector2(22f, -16f);
            selectorScrollRectTransform.sizeDelta = new Vector2(windowWidth - 92f, 176f);

            selectorViewport.offsetMin = new Vector2(12f, 12f);
            selectorViewport.offsetMax = new Vector2(-16f, -12f);

            VerticalLayoutGroup selectorLayout = selectorContent.GetComponent<VerticalLayoutGroup>();
            if (selectorLayout != null)
            {
                selectorLayout.padding = new RectOffset(280, 280, 18, 18);
                selectorLayout.spacing = 12f;
            }

            CreateSettingsSectionButton(
                selectorContent,
                SettingsSection.Language,
                CampusPlayerUiTextId.Language);
            CreateSettingsSectionButton(
                selectorContent,
                SettingsSection.KeyBindings,
                CampusPlayerUiTextId.KeyBindingTitle);

            ScrollRect languageScrollRect = CampusUiRuntimeBuilder.CreateScrollView(
                "StartupSettingsLanguageContent",
                settingsPanel,
                new Vector2(windowWidth - 92f, 458f),
                out RectTransform languageViewport,
                out RectTransform languageContent,
                CampusUiVisualTheme.PanelRaised,
                CampusUiVisualTheme.BorderSoft,
                1.05f,
                18f);
            settingsLanguageContentPanel = languageScrollRect.GetComponent<RectTransform>();
            settingsLanguageContentPanel.anchorMin = new Vector2(0f, 1f);
            settingsLanguageContentPanel.anchorMax = new Vector2(0f, 1f);
            settingsLanguageContentPanel.pivot = new Vector2(0f, 1f);
            settingsLanguageContentPanel.anchoredPosition = new Vector2(22f, -204f);
            settingsLanguageContentPanel.sizeDelta = new Vector2(windowWidth - 92f, 458f);

            languageViewport.offsetMin = new Vector2(12f, 12f);
            languageViewport.offsetMax = new Vector2(-16f, -12f);
            ConfigureSettingsScrollContent(languageContent);

            RectTransform languageCard = CreateSettingsScrollCard(
                languageContent,
                "StartupSettingsLanguageCard",
                430f);
            BuildStartupLanguageCard(languageCard);

            ScrollRect keyBindingScrollRect = CampusUiRuntimeBuilder.CreateScrollView(
                "StartupSettingsKeyBindingContent",
                settingsPanel,
                new Vector2(windowWidth - 92f, 458f),
                out RectTransform keyBindingViewport,
                out RectTransform keyBindingContent,
                CampusUiVisualTheme.PanelRaised,
                CampusUiVisualTheme.BorderSoft,
                1.05f,
                18f);
            settingsKeyBindingContentPanel = keyBindingScrollRect.GetComponent<RectTransform>();
            settingsKeyBindingContentPanel.anchorMin = new Vector2(0f, 1f);
            settingsKeyBindingContentPanel.anchorMax = new Vector2(0f, 1f);
            settingsKeyBindingContentPanel.pivot = new Vector2(0f, 1f);
            settingsKeyBindingContentPanel.anchoredPosition = new Vector2(22f, -204f);
            settingsKeyBindingContentPanel.sizeDelta = new Vector2(windowWidth - 92f, 458f);

            keyBindingViewport.offsetMin = new Vector2(12f, 12f);
            keyBindingViewport.offsetMax = new Vector2(-16f, -12f);
            ConfigureSettingsScrollContent(keyBindingContent);

            RectTransform keyBindingCard = CreateSettingsScrollCard(
                keyBindingContent,
                "StartupSettingsKeyBindingCard",
                GetStartupKeyBindingCardHeight());
            BuildStartupKeyBindingCard(keyBindingCard);

            RectTransform footer = CampusUiRuntimeBuilder.CreatePanel(
                "StartupSettingsFooter",
                settingsPanel,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0f),
                new Vector2(-32f, 76f),
                CampusUiVisualTheme.PanelSoft,
                CampusUiVisualTheme.BorderMuted,
                1.05f,
                18f,
                false);

            settingsBackButton = CampusUiRuntimeBuilder.CreateButton(
                "SettingsBackButton",
                footer,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.BackToEscMenu),
                () => SetPage(StartupPage.Home),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft,
                16f,
                1.1f,
                CampusUiVisualTheme.TextSecondary,
                16);
            settingsBackButtonText = settingsBackButton.GetComponentInChildren<Text>();
            RectTransform backRect = settingsBackButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(1f, 0.5f);
            backRect.anchorMax = new Vector2(1f, 0.5f);
            backRect.pivot = new Vector2(1f, 0.5f);
            backRect.anchoredPosition = new Vector2(-24f, 0f);
            backRect.sizeDelta = new Vector2(184f, 34f);

            ApplySettingsSectionVisibility();
            RefreshSettingsSectionVisuals();
        }

        private void ConfigureSettingsScrollContent(RectTransform content)
        {
            if (content == null)
            {
                return;
            }

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(12, 12, 12, 12);
                layout.spacing = 12f;
            }
        }

        private RectTransform CreateSettingsScrollCard(Transform parent, string name, float height)
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
            return card;
        }

        private void CreateSettingsSectionButton(
            Transform parent,
            SettingsSection section,
            CampusPlayerUiTextId labelId)
        {
            Button button = CampusUiRuntimeBuilder.CreateButton(
                "SettingsSection_" + section,
                parent,
                CampusPlayerUiTextCatalog.Get(labelId),
                () => SetSettingsSection(section),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft,
                16f,
                1.05f,
                CampusUiVisualTheme.TextPrimary,
                18);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 44f);

            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 44f;
            layout.minHeight = 44f;
            layout.flexibleWidth = 1f;

            settingsSectionButtonViews.Add(new SettingsSectionButtonView
            {
                Section = section,
                LabelId = labelId,
                Button = button,
                Label = button.GetComponentInChildren<Text>()
            });
        }

        private void BuildStartupLanguageCard(RectTransform card)
        {
            settingsLanguageTitleText = CampusUiRuntimeBuilder.CreateText(
                "StartupSettingsLanguageTitle",
                card,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language),
                20,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            settingsLanguageTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            settingsLanguageTitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            settingsLanguageTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            settingsLanguageTitleText.rectTransform.anchoredPosition = new Vector2(24f, -18f);
            settingsLanguageTitleText.rectTransform.sizeDelta = new Vector2(240f, 24f);

            settingsLanguageDescriptionText = CampusUiRuntimeBuilder.CreateText(
                "StartupSettingsLanguageDescription",
                card,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsDescription),
                15,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextSecondary);
            settingsLanguageDescriptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            settingsLanguageDescriptionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            settingsLanguageDescriptionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            settingsLanguageDescriptionText.rectTransform.anchoredPosition = new Vector2(24f, -54f);
            settingsLanguageDescriptionText.rectTransform.sizeDelta = new Vector2(-48f, 44f);

            for (int i = 0; i < CampusDisplayLanguageCatalog.All.Count; i++)
            {
                CampusDisplayLanguage language = CampusDisplayLanguageCatalog.All[i];
                CreateLanguageButton(
                    card,
                    language,
                    CampusPlayerUiTextCatalog.GetLanguageNameTextId(language),
                    new Vector2(0f, -138f - 56f * i));
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
                CampusLanguageState.CurrentLanguage == language ? StartupAccent : new Color(0.12f, 0.13f, 0.12f, 0.72f),
                CampusLanguageState.CurrentLanguage == language ? StartupAccent : CampusUiVisualTheme.BorderMuted,
                12f,
                0.9f,
                CampusUiVisualTheme.TextPrimary,
                15);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(300f, 42f);

            Text label = button.GetComponentInChildren<Text>();
            languageButtonViews.Add(new LanguageButtonView
            {
                Language = language,
                LabelId = labelId,
                Button = button,
                Label = label
            });
        }

        private void BuildStartupKeyBindingCard(Transform card)
        {
            keyBindingSectionTitleText = CampusUiRuntimeBuilder.CreateText(
                "StartupSettingsKeyBindingTitle",
                card,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingTitle),
                20,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            keyBindingSectionTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            keyBindingSectionTitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            keyBindingSectionTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            keyBindingSectionTitleText.rectTransform.anchoredPosition = new Vector2(24f, -18f);
            keyBindingSectionTitleText.rectTransform.sizeDelta = new Vector2(280f, 24f);

            keyBindingDescriptionText = CampusUiRuntimeBuilder.CreateText(
                "StartupKeyBindingDescription",
                card,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingDescription),
                15,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextSecondary);
            keyBindingDescriptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            keyBindingDescriptionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            keyBindingDescriptionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            keyBindingDescriptionText.rectTransform.anchoredPosition = new Vector2(24f, -54f);
            keyBindingDescriptionText.rectTransform.sizeDelta = new Vector2(-236f, 42f);

            resetAllKeyBindingsButton = CampusUiRuntimeBuilder.CreateButton(
                "StartupResetAllKeyBindingsButton",
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
            resetAllRect.anchoredPosition = new Vector2(-24f, -18f);
            resetAllRect.sizeDelta = new Vector2(184f, 34f);

            keyBindingRows.Clear();
            IReadOnlyList<CampusGameplayInputActionId> actions = CampusGameplayInputBindings.RebindableActions;
            const float leftColumnX = 180f;
            const float rightColumnX = 762f;
            const float firstRowY = -110f;
            const float rowHeight = 46f;
            int rowsPerColumn = Mathf.CeilToInt(actions.Count * 0.5f);

            for (int i = 0; i < actions.Count; i++)
            {
                int column = i / rowsPerColumn;
                int row = i % rowsPerColumn;
                float x = column == 0 ? leftColumnX : rightColumnX;
                float y = firstRowY - row * rowHeight;
                CreateStartupKeyBindingRow(card, actions[i], new Vector2(x, y));
            }

            keyBindingStatusText = CampusUiRuntimeBuilder.CreateText(
                "StartupKeyBindingStatus",
                card,
                string.Empty,
                13,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted);
            keyBindingStatusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            keyBindingStatusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            keyBindingStatusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            keyBindingStatusText.rectTransform.anchoredPosition = new Vector2(22f, 14f);
            keyBindingStatusText.rectTransform.sizeDelta = new Vector2(-44f, 24f);

            RefreshKeyBindingRows();
        }

        private float GetStartupKeyBindingCardHeight()
        {
            int rowsPerColumn = Mathf.CeilToInt(CampusGameplayInputBindings.RebindableActions.Count * 0.5f);
            return 160f + rowsPerColumn * 46f + 56f;
        }

        private void CreateStartupKeyBindingRow(
            Transform parent,
            CampusGameplayInputActionId actionId,
            Vector2 anchoredPosition)
        {
            Text actionText = CampusUiRuntimeBuilder.CreateText(
                actionId + "_StartupLabel",
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
            actionText.rectTransform.sizeDelta = new Vector2(210f, 32f);

            Button keyButton = CampusUiRuntimeBuilder.CreateButton(
                actionId + "_StartupKeyButton",
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
            keyRect.anchoredPosition = anchoredPosition + new Vector2(218f, 0f);
            keyRect.sizeDelta = new Vector2(124f, 32f);

            Button resetButton = CampusUiRuntimeBuilder.CreateButton(
                actionId + "_StartupResetButton",
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
            resetRect.anchoredPosition = anchoredPosition + new Vector2(352f, 0f);
            resetRect.sizeDelta = new Vector2(86f, 32f);

            keyBindingRows.Add(new KeyBindingRow(actionId, actionText, keyButton, resetButton));
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
                SetText(row.KeyButton != null ? row.KeyButton.GetComponentInChildren<Text>() : null,
                    pending
                        ? CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingListening)
                        : CampusGameplayInputBindings.GetBindingLabel(row.ActionId));
                SetText(row.ResetButton != null ? row.ResetButton.GetComponentInChildren<Text>() : null,
                    CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingReset));

                StorageBoxGraphic keyGraphic = row.KeyButton != null ? row.KeyButton.targetGraphic as StorageBoxGraphic : null;
                if (keyGraphic != null)
                {
                    keyGraphic.SetStyle(
                        pending ? CampusUiVisualTheme.AccentSoftFill : CampusUiVisualTheme.PanelDim,
                        pending ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderSoft,
                        pending ? 1.35f : 1.05f,
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

        private void BuildOptionPanels(Transform parent)
        {
            CreateOptionPanel(
                parent,
                "MapPanel",
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Map),
                new Vector2(24f, -110f),
                new Vector2(986f, 598f),
                out mapPanel,
                out mapContent,
                out mapCountText);

            CreateOptionPanel(
                parent,
                "SavePanel",
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Save),
                new Vector2(1026f, -110f),
                new Vector2(510f, 598f),
                out savePanel,
                out saveContent,
                out saveCountText);
        }

        private void CreateOptionPanel(
            Transform parent,
            string name,
            string title,
            Vector2 position,
            Vector2 size,
            out RectTransform panel,
            out RectTransform content,
            out Text countText)
        {
            panel = CampusUiRuntimeBuilder.CreatePanel(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                position,
                size,
                new Color(0.09f, 0.10f, 0.09f, 0.42f),
                new Color(1f, 0.80f, 0.42f, 0.08f),
                0.75f,
                12f,
                false);

            Text sectionTitle = CampusUiRuntimeBuilder.CreateText(
                name + "_Title",
                panel,
                title,
                20,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            sectionTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            sectionTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            sectionTitle.rectTransform.pivot = new Vector2(0f, 1f);
            sectionTitle.rectTransform.anchoredPosition = new Vector2(18f, -14f);
            sectionTitle.rectTransform.sizeDelta = new Vector2(280f, 24f);

            countText = CampusUiRuntimeBuilder.CreateText(
                name + "_Count",
                panel,
                string.Empty,
                12,
                TextAnchor.MiddleRight,
                CampusUiVisualTheme.TextMuted,
                FontStyle.Bold);
            countText.rectTransform.anchorMin = new Vector2(1f, 1f);
            countText.rectTransform.anchorMax = new Vector2(1f, 1f);
            countText.rectTransform.pivot = new Vector2(1f, 1f);
            countText.rectTransform.anchoredPosition = new Vector2(-18f, -14f);
            countText.rectTransform.sizeDelta = new Vector2(48f, 18f);

            ScrollRect scrollRect = CampusUiRuntimeBuilder.CreateScrollView(
                name + "_Scroll",
                panel,
                new Vector2(size.x - 24f, size.y - 56f),
                out RectTransform viewport,
                out content,
                Color.clear,
                new Color(1f, 0.80f, 0.42f, 0.05f),
                0.65f,
                10f);

            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 1f);
            scrollRectTransform.anchorMax = new Vector2(0f, 1f);
            scrollRectTransform.pivot = new Vector2(0f, 1f);
            scrollRectTransform.anchoredPosition = new Vector2(12f, -42f);
            scrollRectTransform.sizeDelta = new Vector2(size.x - 24f, size.y - 54f);

            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-10f, -4f);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = 8f;
            }

            if (string.Equals(name, "MapPanel", StringComparison.Ordinal))
            {
                mapTitleText = sectionTitle;
            }
            else if (string.Equals(name, "SavePanel", StringComparison.Ordinal))
            {
                saveTitleText = sectionTitle;
            }
        }

        private void BuildFooter(Transform parent)
        {
            footerPanel = CampusUiRuntimeBuilder.CreatePanel(
                "Footer",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -726f),
                new Vector2(windowWidth - 48f, 74f),
                new Color(0.08f, 0.09f, 0.08f, 0.40f),
                new Color(1f, 0.80f, 0.42f, 0.06f),
                0.7f,
                12f,
                false);

            statusText = CampusUiRuntimeBuilder.CreateText(
                "Status",
                footerPanel,
                string.Empty,
                13,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextSecondary);
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(0f, 1f);
            statusText.rectTransform.pivot = new Vector2(0f, 0.5f);
            statusText.rectTransform.anchoredPosition = new Vector2(18f, 0f);
            statusText.rectTransform.sizeDelta = new Vector2(280f, 44f);

            newMapNameField = CampusUiRuntimeBuilder.CreateInputField(
                "NewMapInput",
                footerPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.EnterNewMapName),
                16,
                CampusUiVisualTheme.TextPrimary,
                CampusUiVisualTheme.TextMuted);
            newMapNamePlaceholderText = newMapNameField.placeholder as Text;
            RectTransform inputRect = newMapNameField.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 1f);
            inputRect.anchorMax = new Vector2(0f, 1f);
            inputRect.pivot = new Vector2(0f, 1f);
            inputRect.anchoredPosition = new Vector2(336f, -20f);
            inputRect.sizeDelta = new Vector2(298f, 34f);
            newMapNameField.onValueChanged.AddListener(value => newMapName = value ?? string.Empty);

            createMapButton = CampusUiRuntimeBuilder.CreateButton(
                "CreateMapButton",
                footerPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.CreateNewMap),
                BeginNewMapLoad,
                StartupAccent,
                StartupAccent,
                16f,
                0.9f,
                CampusUiVisualTheme.TextPrimary,
                16);
            createMapButtonText = createMapButton.GetComponentInChildren<Text>();
            RectTransform createRect = createMapButton.GetComponent<RectTransform>();
            createRect.anchorMin = new Vector2(0f, 1f);
            createRect.anchorMax = new Vector2(0f, 1f);
            createRect.pivot = new Vector2(0f, 1f);
            createRect.anchoredPosition = new Vector2(648f, -20f);
            createRect.sizeDelta = new Vector2(146f, 34f);

            refreshButton = CampusUiRuntimeBuilder.CreateButton(
                "RefreshButton",
                footerPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Refresh),
                RebuildOptions,
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderMuted,
                16f,
                0.9f,
                CampusUiVisualTheme.TextSecondary,
                16);
            refreshButtonText = refreshButton.GetComponentInChildren<Text>();
            RectTransform refreshRect = refreshButton.GetComponent<RectTransform>();
            refreshRect.anchorMin = new Vector2(0f, 1f);
            refreshRect.anchorMax = new Vector2(0f, 1f);
            refreshRect.pivot = new Vector2(0f, 1f);
            refreshRect.anchoredPosition = new Vector2(806f, -20f);
            refreshRect.sizeDelta = new Vector2(108f, 34f);

            startButton = CampusUiRuntimeBuilder.CreateButton(
                "StartButton",
                footerPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartGame),
                BeginGameLoad,
                StartupAccent,
                StartupAccent,
                18f,
                1.0f,
                CampusUiVisualTheme.TextPrimary,
                18);
            startButtonText = startButton.GetComponentInChildren<Text>();
            RectTransform startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(1f, 1f);
            startRect.anchorMax = new Vector2(1f, 1f);
            startRect.pivot = new Vector2(1f, 1f);
            startRect.anchoredPosition = new Vector2(-18f, -17f);
            startRect.sizeDelta = new Vector2(204f, 40f);

            playBackButton = CampusUiRuntimeBuilder.CreateButton(
                "PlayBackButton",
                footerPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.BackToEscMenu),
                () => SetPage(StartupPage.Home),
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderMuted,
                16f,
                0.9f,
                CampusUiVisualTheme.TextSecondary,
                16);
            playBackButtonText = playBackButton.GetComponentInChildren<Text>();
            RectTransform backRect = playBackButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(1f, 1f);
            backRect.anchorMax = new Vector2(1f, 1f);
            backRect.pivot = new Vector2(1f, 1f);
            backRect.anchoredPosition = new Vector2(-236f, -20f);
            backRect.sizeDelta = new Vector2(128f, 34f);

            RefreshFooterText();
        }

        private void BuildAboutPanel(Transform parent)
        {
            aboutPanel = CampusUiRuntimeBuilder.CreatePanel(
                "AboutPanel",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -150f),
                new Vector2(windowWidth - 48f, 746f),
                new Color(0.19f, 0.20f, 0.16f, 0.70f),
                new Color(0.84f, 0.62f, 0.34f, 0.32f),
                1.05f,
                18f,
                false);

            aboutTitleText = CampusUiRuntimeBuilder.CreateText(
                "AboutTitle",
                aboutPanel,
                string.Empty,
                34,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextGold,
                FontStyle.Bold);
            aboutTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            aboutTitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            aboutTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            aboutTitleText.rectTransform.anchoredPosition = new Vector2(44f, -48f);
            aboutTitleText.rectTransform.sizeDelta = new Vector2(520f, 46f);

            aboutBodyText = CampusUiRuntimeBuilder.CreateText(
                "AboutBody",
                aboutPanel,
                string.Empty,
                18,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextSecondary);
            aboutBodyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            aboutBodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            aboutBodyText.rectTransform.pivot = new Vector2(0.5f, 1f);
            aboutBodyText.rectTransform.anchoredPosition = new Vector2(44f, -112f);
            aboutBodyText.rectTransform.sizeDelta = new Vector2(-88f, 360f);

            aboutBackButton = CreateMainMenuButton(
                aboutPanel,
                "AboutBackButton",
                CampusPlayerUiTextId.BackToEscMenu,
                new Vector2(430f, -560f),
                () => SetPage(StartupPage.Home),
                false);
            aboutBackButtonText = aboutBackButton.GetComponentInChildren<Text>();
        }

        private void SetPage(StartupPage page)
        {
            if (page != StartupPage.Settings)
            {
                pendingKeyBindingAction = null;
                keyBindingStatusMessage = string.Empty;
                RefreshKeyBindingRows();
            }

            currentPage = page;
            ApplyPageVisibility();
            RefreshLocalizedText();
        }

        private void SetSettingsSection(SettingsSection section)
        {
            if (currentSettingsSection == section)
            {
                return;
            }

            if (currentSettingsSection == SettingsSection.KeyBindings)
            {
                pendingKeyBindingAction = null;
                keyBindingStatusMessage = string.Empty;
            }

            currentSettingsSection = section;
            ApplySettingsSectionVisibility();
            RefreshSettingsSectionVisuals();
            RefreshKeyBindingRows();
        }

        private void ApplyPageVisibility()
        {
            bool home = currentPage == StartupPage.Home;
            SetPanelChromeVisible(!home);
            SetVisible(homePanel, currentPage == StartupPage.Home);
            SetVisible(settingsPanel, currentPage == StartupPage.Settings);
            SetVisible(mapPanel, currentPage == StartupPage.Play);
            SetVisible(savePanel, currentPage == StartupPage.Play);
            SetVisible(footerPanel, currentPage == StartupPage.Play);
            SetVisible(aboutPanel, currentPage == StartupPage.About);
            ApplySettingsSectionVisibility();
        }

        private void ApplySettingsSectionVisibility()
        {
            bool showLanguage = currentPage == StartupPage.Settings && currentSettingsSection == SettingsSection.Language;
            bool showKeyBindings = currentPage == StartupPage.Settings && currentSettingsSection == SettingsSection.KeyBindings;
            SetVisible(settingsLanguageContentPanel, showLanguage);
            SetVisible(settingsKeyBindingContentPanel, showKeyBindings);
        }

        private void SetPanelChromeVisible(bool visible)
        {
            SetVisible(headerPanel, visible);
            if (mainPanel != null)
            {
                StorageBoxGraphic graphic = mainPanel.GetComponent<StorageBoxGraphic>();
                if (graphic != null)
                {
                    graphic.SetStyle(
                        visible ? StartupMainPanelFill : Color.clear,
                        visible ? StartupMainPanelBorder : Color.clear,
                        visible ? 0.95f : 0f,
                        visible ? 18f : 0f);
                }
            }
        }

        private static void SetVisible(RectTransform rect, bool visible)
        {
            if (rect != null)
            {
                rect.gameObject.SetActive(visible);
            }
        }

        private void BuildLoadingOverlay(Transform parent)
        {
            loadingPanel = CampusUiRuntimeBuilder.CreateFullScreenPanel(
                "LoadingOverlay",
                parent,
                Color.clear,
                false);
            loadingCanvasGroup = loadingPanel.gameObject.AddComponent<CanvasGroup>();
            loadingCanvasGroup.alpha = 0f;
            loadingCanvasGroup.interactable = false;
            loadingCanvasGroup.blocksRaycasts = false;
            loadingPanel.gameObject.SetActive(false);

            RectTransform backdrop = CampusUiRuntimeBuilder.CreateFullScreenPanel(
                "LoadingBackdrop",
                loadingPanel,
                CampusUiVisualTheme.BackgroundDeep,
                false);
            backdrop.SetAsFirstSibling();

            Sprite loadingArtworkSprite = startupBackgroundSprite != null
                ? startupBackgroundSprite
                : startupLoadingOverlaySprite;
            if (loadingArtworkSprite != null)
            {
                Image artwork = CampusUiRuntimeBuilder.CreateImage(
                    "LoadingBackdropArtwork",
                    backdrop,
                    loadingArtworkSprite,
                    StartupBackdropImageTint,
                    false,
                    false);
                artwork.rectTransform.offsetMin = new Vector2(-14f, -14f);
                artwork.rectTransform.offsetMax = new Vector2(14f, 14f);
            }

            RectTransform dimmer = CampusUiRuntimeBuilder.CreateFullScreenPanel(
                "LoadingBackdropDimmer",
                backdrop,
                StartupBackdropTint,
                false);
            dimmer.SetAsLastSibling();

            loadingTitleText = CampusUiRuntimeBuilder.CreateText(
                "LoadingTitle",
                loadingPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Loading),
                52,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            loadingTitleText.rectTransform.anchorMin = new Vector2(0.18f, 0.72f);
            loadingTitleText.rectTransform.anchorMax = new Vector2(0.18f, 0.72f);
            loadingTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            loadingTitleText.rectTransform.anchoredPosition = Vector2.zero;
            loadingTitleText.rectTransform.sizeDelta = new Vector2(420f, 58f);

            loadingStatusText = CampusUiRuntimeBuilder.CreateText(
                "LoadingStatus",
                loadingPanel,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingGameplayScene),
                20,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextPrimary);
            loadingStatusText.rectTransform.anchorMin = new Vector2(0.18f, 0.66f);
            loadingStatusText.rectTransform.anchorMax = new Vector2(0.18f, 0.66f);
            loadingStatusText.rectTransform.pivot = new Vector2(0f, 1f);
            loadingStatusText.rectTransform.anchoredPosition = Vector2.zero;
            loadingStatusText.rectTransform.sizeDelta = new Vector2(680f, 36f);

            RectTransform progressFrame = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressFrame",
                loadingPanel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 168f),
                new Vector2(LoadingProgressWidth + 8f, 26f),
                StartupSectionFill,
                StartupSectionBorder,
                0.85f,
                10f,
                false);

            loadingFillRect = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressFill",
                progressFrame,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(2f, 0f),
                new Vector2(0f, 18f),
                StartupAccent,
                Color.clear,
                0f,
                10f,
                false);
            loadingFillRect.anchoredPosition = new Vector2(4f, 0f);
            loadingFillRect.pivot = new Vector2(0f, 0.5f);

            loadingSweepRect = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressSweep",
                progressFrame,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-60f, 0f),
                new Vector2(120f, 18f),
                new Color(1f, 1f, 1f, 0.24f),
                Color.clear,
                0f,
                10f,
                false);
            loadingSweepRect.SetAsLastSibling();
            KillTween(ref loadingSweepTween);
            loadingSweepTween = loadingSweepRect
                .DOAnchorPosX(LoadingProgressWidth - 60f, 1.1f)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true)
                .Pause();

            ApplyLoadingProgressVisual(0f);
        }

        private void RefreshLocalizedText()
        {
            if (headerTitleText != null)
            {
                headerTitleText.text = ResolveHeaderTitle();
            }

            if (mapTitleText != null)
            {
                mapTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Map);
            }

            if (saveTitleText != null)
            {
                saveTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Save);
            }

            if (loadingTitleText != null)
            {
                loadingTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Loading);
            }

            if (headerSubtitleText != null)
            {
                string subtitle = ResolveHeaderSubtitle();
                headerSubtitleText.text = subtitle;
                headerSubtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));
            }

            if (!isLoading && loadingStatusText != null)
            {
                loadingStatusText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.PreparingSceneTransition);
            }

            if (newMapNamePlaceholderText != null)
            {
                newMapNamePlaceholderText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.EnterNewMapName);
            }

            if (createMapButtonText != null)
            {
                createMapButtonText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.CreateNewMap);
            }

            if (refreshButtonText != null)
            {
                refreshButtonText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Refresh);
            }

            if (startButtonText != null)
            {
                startButtonText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartGame);
            }

            SetText(homeStartButtonText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartGame));
            SetText(homeSettingsButtonText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsTitle));
            SetText(homeAboutButtonText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.About));
            if (homeLogoImage != null)
            {
                homeLogoImage.sprite = ResolveStartupLogoSprite();
            }

            SetText(settingsLanguageTitleText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language));
            SetText(settingsLanguageDescriptionText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsDescription));
            SetText(keyBindingSectionTitleText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingTitle));
            SetText(keyBindingDescriptionText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingDescription));
            SetText(playBackButtonText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.BackToEscMenu));
            SetText(settingsBackButtonText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.BackToEscMenu));
            SetText(aboutBackButtonText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.BackToEscMenu));
            SetText(aboutTitleText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.About));
            SetText(aboutBodyText, CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.AboutDescription));
            SetText(resetAllKeyBindingsButton != null ? resetAllKeyBindingsButton.GetComponentInChildren<Text>() : null,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.KeyBindingResetAll));

            for (int i = 0; i < languageButtonViews.Count; i++)
            {
                LanguageButtonView view = languageButtonViews[i];
                if (view == null || view.Label == null)
                {
                    continue;
                }

                view.Label.text = CampusPlayerUiTextCatalog.Get(view.LabelId);
            }

            for (int i = 0; i < settingsSectionButtonViews.Count; i++)
            {
                SettingsSectionButtonView view = settingsSectionButtonViews[i];
                if (view == null || view.Label == null)
                {
                    continue;
                }

                view.Label.text = CampusPlayerUiTextCatalog.Get(view.LabelId);
            }

            RefreshLanguageVisuals();
            RefreshSettingsSectionVisuals();
            RefreshKeyBindingRows();
        }

        private string ResolveHeaderTitle()
        {
            switch (currentPage)
            {
                case StartupPage.Play:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartGame);
                case StartupPage.Settings:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsTitle);
                case StartupPage.About:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.About);
                default:
                    return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartupTitle);
            }
        }

        private string ResolveHeaderSubtitle()
        {
            return string.Empty;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private void RefreshOptionPanels()
        {
            BuildOptionViews(mapOptions, mapContent, mapOptionViews, selectedMapIndex, true);
            BuildOptionViews(saveOptions, saveContent, saveOptionViews, selectedSaveIndex, false);
            RefreshFooterText();
        }

        private void BuildOptionViews(
            List<LoadOption> options,
            RectTransform content,
            List<LoadOptionView> views,
            int selectedIndex,
            bool isMapPanel)
        {
            ClearChildren(content);
            views.Clear();

            for (int i = 0; i < options.Count; i++)
            {
                LoadOption option = options[i];
                int optionIndex = i;
                LoadOptionView view = CreateOptionView(content, option, () =>
                {
                    if (isMapPanel)
                    {
                        selectedMapIndex = optionIndex;
                    }
                    else
                    {
                        selectedSaveIndex = optionIndex;
                    }

                    RefreshOptionPanels();
                });
                view.Option = option;
                views.Add(view);
            }

            RefreshOptionStyles(isMapPanel ? mapOptionViews : saveOptionViews, selectedIndex);
        }

        private LoadOptionView CreateOptionView(
            Transform parent,
            LoadOption option,
            Action onClick)
        {
            GameObject cardObject = new GameObject(
                "OptionCard",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(StorageBoxGraphic),
                typeof(Button),
                typeof(LayoutElement));
            cardObject.transform.SetParent(parent, false);

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 68f);

            LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 68f;
            layoutElement.minHeight = 68f;
            layoutElement.flexibleWidth = 1f;

            StorageBoxGraphic graphic = cardObject.GetComponent<StorageBoxGraphic>();
            graphic.SetStyle(new Color(1f, 1f, 1f, 0.03f), new Color(1f, 1f, 1f, 0.04f), 0.7f, 9f);

            Button button = cardObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.ColorTint;
            StorageUIUtility.ApplyButtonTransition(button);
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            Text titleText = CampusUiRuntimeBuilder.CreateText(
                "Title",
                cardObject.transform,
                option.Label,
                16,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(18f, -10f);
            titleText.rectTransform.sizeDelta = new Vector2(0f, 20f);

            Text metaText = CampusUiRuntimeBuilder.CreateText(
                "Meta",
                cardObject.transform,
                ResolveOptionMeta(option),
                12,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted);
            metaText.rectTransform.anchorMin = new Vector2(0f, 0f);
            metaText.rectTransform.anchorMax = new Vector2(1f, 0f);
            metaText.rectTransform.pivot = new Vector2(0.5f, 0f);
            metaText.rectTransform.anchoredPosition = new Vector2(18f, 8f);
            metaText.rectTransform.sizeDelta = new Vector2(0f, 16f);

            return new LoadOptionView
            {
                Root = rect,
                Background = graphic,
                Button = button,
                TitleText = titleText,
                MetaText = metaText,
                Layout = layoutElement
            };
        }

        private void RefreshOptionStyles(List<LoadOptionView> views, int selectedIndex)
        {
            for (int i = 0; i < views.Count; i++)
            {
                LoadOptionView view = views[i];
                bool selected = i == selectedIndex;
                if (view == null || view.Background == null || view.TitleText == null || view.MetaText == null)
                {
                    continue;
                }

                view.Background.SetStyle(
                    selected ? new Color(1f, 0.75f, 0.30f, 0.10f) : new Color(1f, 1f, 1f, 0.02f),
                    selected ? StartupAccent : new Color(1f, 1f, 1f, 0.04f),
                    selected ? 0.95f : 0.65f,
                    9f);
                view.TitleText.color = selected ? CampusUiVisualTheme.TextPrimary : CampusUiVisualTheme.TextSecondary;
                view.MetaText.color = selected ? CampusUiVisualTheme.TextSecondary : CampusUiVisualTheme.TextMuted;
                view.Root.localScale = Vector3.one;
            }
        }

        private void RefreshFooterText()
        {
            if (mapCountText != null)
            {
                mapCountText.text = mapOptions.Count.ToString();
            }

            if (saveCountText != null)
            {
                saveCountText.text = saveOptions.Count.ToString();
            }

            if (newMapNameField != null && string.IsNullOrWhiteSpace(newMapNameField.text) && !string.IsNullOrWhiteSpace(newMapName))
            {
                newMapNameField.SetTextWithoutNotify(newMapName);
            }

            if (startButton != null)
            {
                startButton.interactable = !isLoading && mapOptions.Count > 0 && saveOptions.Count > 0;
            }

            if (createMapButton != null)
            {
                createMapButton.interactable = !isLoading;
            }

            if (refreshButton != null)
            {
                refreshButton.interactable = !isLoading;
            }
        }

        private void SetLoadingProgress(float progress)
        {
            float targetProgress = Mathf.Clamp01(progress);
            if (targetProgress < loadingProgress)
            {
                return;
            }

            loadingProgress = targetProgress;
        }

        private void UpdateLoadingProgress()
        {
            if (loadingOperation == null)
            {
                return;
            }

            float operationProgress = Mathf.Clamp01(loadingOperation.progress / 0.9f);
            float elapsed = Time.unscaledTime - loadingVisibleTime;
            float animatedFloor = Mathf.Clamp01(elapsed / MinimumLoadingVisibleSeconds) * 0.88f;
            float targetProgress = loadingOperation.isDone || loadingSceneActivationRequested
                ? 1f
                : Mathf.Min(LoadingActivationProgress, Mathf.Max(operationProgress, animatedFloor));

            SetLoadingProgress(targetProgress);
        }

        private void TryActivateLoadedScene()
        {
            if (loadingOperation == null || loadingSceneActivationRequested)
            {
                return;
            }

            bool sceneDataReady = loadingOperation.progress >= 0.9f;
            bool visibleLongEnough = Time.unscaledTime - loadingVisibleTime >= MinimumLoadingVisibleSeconds;
            bool animationReady = displayedLoadingProgress >= LoadingActivationProgress - 0.002f;
            if (!sceneDataReady || !visibleLongEnough || !animationReady)
            {
                return;
            }

            loadingSceneActivationRequested = true;
            SetLoadingProgress(1f);
            displayedLoadingProgress = 1f;
            ApplyLoadingProgressVisual(displayedLoadingProgress);
            loadingOperation.allowSceneActivation = true;
        }

        private void UpdateDisplayedLoadingProgress()
        {
            if (!isLoading)
            {
                return;
            }

            float speed = loadingProgress >= 1f ? 4f : 1.35f;
            displayedLoadingProgress = Mathf.MoveTowards(
                displayedLoadingProgress,
                loadingProgress,
                Time.unscaledDeltaTime * speed);
            ApplyLoadingProgressVisual(displayedLoadingProgress);
        }

        private void ApplyLoadingProgressVisual(float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);

            if (loadingFillRect != null)
            {
                float fillWidth = Mathf.Max(2f, LoadingProgressWidth * clampedProgress);
                loadingFillRect.sizeDelta = new Vector2(fillWidth, 18f);
            }
        }

        private void SetLoadingStatus(string message)
        {
            if (loadingStatusText != null)
            {
                loadingStatusText.text = message;
            }
        }

        private void SetStatus(string message, bool warning)
        {
            statusMessage = message ?? string.Empty;
            if (statusText != null)
            {
                statusText.text = statusMessage;
                statusText.color = warning ? CampusUiVisualTheme.Warning : CampusUiVisualTheme.TextSecondary;
            }
        }

        private void SetMainPanelVisible(bool visible, bool immediate)
        {
            if (mainPanel == null || mainCanvasGroup == null)
            {
                return;
            }

            KillTween(ref mainPanelTween);
            if (immediate)
            {
                mainCanvasGroup.alpha = visible ? 1f : 0f;
                mainCanvasGroup.interactable = visible;
                mainCanvasGroup.blocksRaycasts = visible;
                mainPanel.localScale = Vector3.one;
                mainPanel.gameObject.SetActive(true);
                return;
            }

            if (!visible)
            {
                mainPanelTween = CampusUiTweenUtility.ClosePanel(mainCanvasGroup, mainPanel, 0.18f, 0.98f);
                mainPanelTween.OnComplete(() => mainPanel.gameObject.SetActive(false));
                return;
            }

            mainPanel.gameObject.SetActive(true);
            mainPanelTween = CampusUiTweenUtility.OpenPanel(mainCanvasGroup, mainPanel, 0.24f, 0.97f);
        }

        private void SetLoadingVisible(bool visible, bool immediate)
        {
            if (loadingPanel == null || loadingCanvasGroup == null)
            {
                return;
            }

            KillTween(ref loadingPanelTween);
            if (visible)
            {
                ForceMainPanelHidden();
                loadingPanel.gameObject.SetActive(true);
                loadingVisibleTime = Time.unscaledTime;
                if (loadingSweepTween != null)
                {
                    loadingSweepTween.Restart();
                }
                loadingProgress = 0f;
                displayedLoadingProgress = 0f;
                ApplyLoadingProgressVisual(0f);
                if (immediate)
                {
                    loadingCanvasGroup.alpha = 1f;
                    loadingCanvasGroup.interactable = true;
                    loadingCanvasGroup.blocksRaycasts = true;
                    loadingPanel.localScale = Vector3.one;
                    return;
                }

                loadingPanelTween = CampusUiTweenUtility.OpenPanel(loadingCanvasGroup, loadingPanel, 0.24f, 0.97f);
                return;
            }

            if (immediate)
            {
                loadingCanvasGroup.alpha = 0f;
                loadingCanvasGroup.interactable = false;
                loadingCanvasGroup.blocksRaycasts = false;
                loadingPanel.gameObject.SetActive(false);
                if (loadingSweepTween != null)
                {
                    loadingSweepTween.Pause();
                }
                return;
            }

            loadingPanelTween = CampusUiTweenUtility.ClosePanel(loadingCanvasGroup, loadingPanel, 0.16f, 0.98f);
            loadingPanelTween.OnComplete(() =>
            {
                loadingPanel.gameObject.SetActive(false);
                if (loadingSweepTween != null)
                {
                    loadingSweepTween.Pause();
                }
            });
        }

        private void SetLanguage(CampusDisplayLanguage language)
        {
            CampusLanguageState.SetLanguage(language);
            RefreshLocalizedText();
            RebuildOptions();
        }

        private void RefreshLanguageVisuals()
        {
            for (int i = 0; i < languageButtonViews.Count; i++)
            {
                LanguageButtonView view = languageButtonViews[i];
                if (view == null || view.Button == null || view.Label == null)
                {
                    continue;
                }

                bool selected = CampusLanguageState.CurrentLanguage == view.Language;
                StorageBoxGraphic graphic = view.Button.targetGraphic as StorageBoxGraphic;
                if (graphic != null)
                {
                    graphic.SetStyle(
                        selected ? StartupAccent : CampusUiVisualTheme.PanelDim,
                        selected ? StartupAccent : CampusUiVisualTheme.BorderMuted,
                        selected ? 1.05f : 0.9f,
                        14f);
                }

                view.Label.color = selected ? CampusUiVisualTheme.TextPrimary : CampusUiVisualTheme.TextSecondary;
            }

            RefreshFooterText();
        }

        private void RefreshSettingsSectionVisuals()
        {
            for (int i = 0; i < settingsSectionButtonViews.Count; i++)
            {
                SettingsSectionButtonView view = settingsSectionButtonViews[i];
                if (view == null || view.Button == null || view.Label == null)
                {
                    continue;
                }

                bool selected = currentSettingsSection == view.Section;
                StorageBoxGraphic graphic = view.Button.targetGraphic as StorageBoxGraphic;
                if (graphic != null)
                {
                    graphic.SetStyle(
                        selected ? CampusUiVisualTheme.AccentSoftFill : CampusUiVisualTheme.PanelDim,
                        selected ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderSoft,
                        selected ? 1.25f : 1.05f,
                        16f);
                }

                view.Label.color = selected ? CampusUiVisualTheme.TextPrimary : CampusUiVisualTheme.TextSecondary;
            }
        }

        private string ResolveOptionMeta(LoadOption option)
        {
            if (option == null)
            {
                return string.Empty;
            }

            if (option.IsSceneDefault)
            {
                return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SceneDefault);
            }

            if (option.IsNone)
            {
                return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.NoSave);
            }

            if (option.Source == CampusRuntimeMapLoadSource.PlayerSave)
            {
                return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.AutoSave);
            }

            return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.AuthoringPackage);
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
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

        private void ForceMainPanelHidden()
        {
            if (mainPanel == null || mainCanvasGroup == null)
            {
                return;
            }

            KillTween(ref mainPanelTween);
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.interactable = false;
            mainCanvasGroup.blocksRaycasts = false;
            mainPanel.localScale = Vector3.one;
            mainPanel.gameObject.SetActive(false);
        }

        private static string BuildMapLabel(string fileName)
        {
            if (string.Equals(fileName, "CampusMap_AuthoringPackage.json", StringComparison.OrdinalIgnoreCase))
            {
                return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.AuthoringPackage);
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static int CompareLastWriteTimeDescending(string left, string right)
        {
            DateTime leftTime = File.Exists(left) ? File.GetLastWriteTimeUtc(left) : DateTime.MinValue;
            DateTime rightTime = File.Exists(right) ? File.GetLastWriteTimeUtc(right) : DateTime.MinValue;
            return rightTime.CompareTo(leftTime);
        }

        private string ResolveMapFolder()
        {
            return Path.Combine(Application.dataPath, "NtingCampus", "UserGeneratedRuntimeContent");
        }

        private string ResolveSaveFolder()
        {
            return Path.Combine(Application.persistentDataPath, "CampusPlayerMapSave");
        }

        private string ResolveExportFolder()
        {
            return Path.Combine(Application.persistentDataPath, "CampusMapExports");
        }

        private static string SanitizeMapName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] buffer = value.Trim().ToCharArray();
            int writeIndex = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                char character = buffer[i];
                if (Array.IndexOf(invalidChars, character) >= 0)
                {
                    continue;
                }

                buffer[writeIndex++] = character;
            }

            return new string(buffer, 0, writeIndex).Trim();
        }

        private void KillTween(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }

            tween = null;
        }

        private static void EnsureStartupCamera()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("StartupCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera newCamera = cameraObject.AddComponent<Camera>();
            newCamera.clearFlags = CameraClearFlags.SolidColor;
            newCamera.backgroundColor = CampusUiVisualTheme.BackgroundDeep;
            newCamera.orthographic = true;
            newCamera.orthographicSize = 5f;
        }
    }
}
