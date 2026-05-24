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
            public RectTransform Indicator;
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

        private const string StartupSceneName = "Startup";
        private const string GameplaySceneName = "CampusMap";
        private const string StartupBackgroundResourcePath = "UI/Startup/startup_background_nostalgia_v1";
        private const string StartupLoadingOverlayResourcePath = "UI/Startup/startup_loading_overlay_v1";
        private const int SortingOrder = 40000;
        private const float LoadingProgressWidth = 1288f;
        private const float MinimumLoadingVisibleSeconds = 1.6f;
        private const float LoadingActivationProgress = 0.985f;

        [SerializeField] private bool showOnStartup = true;
        [SerializeField, Min(1f)] private float windowWidth = 1560f;
        [SerializeField, Min(1f)] private float windowHeight = 920f;

        private readonly List<LoadOption> mapOptions = new List<LoadOption>();
        private readonly List<LoadOption> saveOptions = new List<LoadOption>();
        private readonly List<LoadOptionView> mapOptionViews = new List<LoadOptionView>();
        private readonly List<LoadOptionView> saveOptionViews = new List<LoadOptionView>();
        private readonly List<LanguageButtonView> languageButtonViews = new List<LanguageButtonView>();

        private Canvas canvas;
        private RectTransform canvasRoot;
        private CanvasGroup mainCanvasGroup;
        private CanvasGroup loadingCanvasGroup;
        private RectTransform mainPanel;
        private RectTransform loadingPanel;
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
        private Text createMapButtonText;
        private Text refreshButtonText;
        private Text startButtonText;
        private Text headerTitleText;
        private Text mapTitleText;
        private Text saveTitleText;
        private Text loadingTitleText;
        private Text loadingStatusText;
        private Text loadingPercentText;
        private Text loadingPercentCaptionText;
        private RectTransform loadingFillRect;
        private RectTransform loadingSweepRect;
        private AsyncOperation loadingOperation;
        private Tween mainPanelTween;
        private Tween loadingPanelTween;
        private Tween loadingSweepTween;
        private Sprite startupBackgroundSprite;
        private Sprite startupLoadingOverlaySprite;
        private string newMapName = string.Empty;
        private string statusMessage = string.Empty;
        private float loadingProgress;
        private float displayedLoadingProgress;
        private float loadingVisibleTime;
        private int selectedMapIndex;
        private int selectedSaveIndex;
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
                new Color(0.24f, 0.22f, 0.18f, 0.88f),
                new Color(0.92f, 0.66f, 0.28f, 0.50f),
                1.1f,
                22f,
                false);
            mainCanvasGroup = mainPanel.gameObject.AddComponent<CanvasGroup>();
            mainCanvasGroup.alpha = 1f;
            mainCanvasGroup.interactable = true;
            mainCanvasGroup.blocksRaycasts = true;

            RectTransform shadow = CampusUiRuntimeBuilder.CreatePanel(
                "StartupPanelShadow",
                canvasRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(12f, -12f),
                new Vector2(windowWidth + 18f, windowHeight + 20f),
                CampusUiVisualTheme.CardShadow,
                Color.clear,
                0f,
                32f,
                false);
            shadow.SetSiblingIndex(mainPanel.GetSiblingIndex());

            BuildHeader(mainPanel);
            BuildLanguageRow(mainPanel);
            BuildOptionPanels(mainPanel);
            BuildFooter(mainPanel);
            BuildLoadingOverlay(canvasRoot);
            RefreshLocalizedText();
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
                    new Color(1f, 1f, 1f, 0.88f),
                    false,
                    false);
                artwork.rectTransform.offsetMin = new Vector2(-20f, -20f);
                artwork.rectTransform.offsetMax = new Vector2(20f, 20f);
            }

            RectTransform dimmer = CampusUiRuntimeBuilder.CreateFullScreenPanel(
                "BackdropDimmer",
                backdrop,
                new Color(0.22f, 0.16f, 0.08f, 0.16f),
                false);
            dimmer.SetAsLastSibling();
        }

        private void BuildHeader(Transform parent)
        {
            RectTransform header = CampusUiRuntimeBuilder.CreatePanel(
                "Header",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(windowWidth - 48f, 104f),
                new Color(0.40f, 0.32f, 0.23f, 0.76f),
                CampusUiVisualTheme.BorderSoft,
                1.0f,
                18f,
                false);

            RectTransform accent = CampusUiRuntimeBuilder.CreatePanel(
                "HeaderAccent",
                header,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 6f),
                new Color(1f, 0.78f, 0.32f, 0.55f),
                Color.clear,
                0f,
                18f,
                false);
            accent.offsetMin = new Vector2(24f, -6f);
            accent.offsetMax = new Vector2(-24f, 0f);

            RectTransform line = CampusUiRuntimeBuilder.CreatePanel(
                "HeaderRule",
                header,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(0f, 2f),
                new Color(1f, 0.86f, 0.46f, 0.28f),
                Color.clear,
                0f,
                0f,
                false);
            line.offsetMin = new Vector2(24f, 16f);
            line.offsetMax = new Vector2(-24f, 18f);

            headerTitleText = CampusUiRuntimeBuilder.CreateText(
                "Title",
                header,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartupTitle),
                52,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextGold,
                FontStyle.Bold);
            headerTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerTitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            headerTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            headerTitleText.rectTransform.anchoredPosition = new Vector2(34f, -20f);
            headerTitleText.rectTransform.sizeDelta = new Vector2(560f, 58f);
        }

        private void BuildLanguageRow(Transform parent)
        {
            RectTransform row = CampusUiRuntimeBuilder.CreatePanel(
                "LanguageRow",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -150f),
                new Vector2(windowWidth - 48f, 50f),
                new Color(0.18f, 0.20f, 0.17f, 0.58f),
                new Color(0.90f, 0.70f, 0.38f, 0.26f),
                0.9f,
                14f,
                false);

            CreateLanguageButton(row, CampusDisplayLanguage.Chinese, CampusPlayerUiTextId.Chinese, new Vector2(22f, 8f));
            CreateLanguageButton(row, CampusDisplayLanguage.English, CampusPlayerUiTextId.English, new Vector2(154f, 8f));
            CreateLanguageButton(row, CampusDisplayLanguage.Bilingual, CampusPlayerUiTextId.Bilingual, new Vector2(286f, 8f));
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
                CampusLanguageState.CurrentLanguage == language ? new Color(0.78f, 0.49f, 0.16f, 0.88f) : new Color(0.17f, 0.18f, 0.15f, 0.72f),
                CampusLanguageState.CurrentLanguage == language ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderSoft,
                12f,
                1.1f,
                CampusUiVisualTheme.TextPrimary,
                15);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(118f, 32f);

            Text label = button.GetComponentInChildren<Text>();
            languageButtonViews.Add(new LanguageButtonView
            {
                Language = language,
                LabelId = labelId,
                Button = button,
                Label = label
            });
        }

        private void BuildOptionPanels(Transform parent)
        {
            CreateOptionPanel(
                parent,
                "MapPanel",
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Map),
                new Vector2(24f, -224f),
                new Vector2(1008f, 526f),
                out RectTransform mapPanel,
                out mapContent,
                out mapCountText);

            CreateOptionPanel(
                parent,
                "SavePanel",
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Save),
                new Vector2(1050f, -224f),
                new Vector2(486f, 526f),
                out RectTransform savePanel,
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
                new Color(0.20f, 0.21f, 0.17f, 0.76f),
                new Color(0.88f, 0.64f, 0.30f, 0.50f),
                1.1f,
                18f,
                false);

            RectTransform accent = CampusUiRuntimeBuilder.CreatePanel(
                name + "_Accent",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 5f),
                new Color(1f, 0.75f, 0.24f, 0.42f),
                Color.clear,
                0f,
                18f,
                false);
            accent.offsetMin = new Vector2(18f, -5f);
            accent.offsetMax = new Vector2(-18f, 0f);

            Text sectionTitle = CampusUiRuntimeBuilder.CreateText(
                name + "_Title",
                panel,
                title,
                28,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            sectionTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            sectionTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            sectionTitle.rectTransform.pivot = new Vector2(0f, 1f);
            sectionTitle.rectTransform.anchoredPosition = new Vector2(24f, -20f);
            sectionTitle.rectTransform.sizeDelta = new Vector2(360f, 34f);

            countText = CampusUiRuntimeBuilder.CreateText(
                name + "_Count",
                panel,
                string.Empty,
                13,
                TextAnchor.MiddleRight,
                CampusUiVisualTheme.TextMuted,
                FontStyle.Bold);
            countText.rectTransform.anchorMin = new Vector2(1f, 1f);
            countText.rectTransform.anchorMax = new Vector2(1f, 1f);
            countText.rectTransform.pivot = new Vector2(1f, 1f);
            countText.rectTransform.anchoredPosition = new Vector2(-22f, -18f);
            countText.rectTransform.sizeDelta = new Vector2(40f, 22f);

            ScrollRect scrollRect = CampusUiRuntimeBuilder.CreateScrollView(
                name + "_Scroll",
                panel,
                new Vector2(size.x - 30f, size.y - 90f),
                out RectTransform viewport,
                out content,
                new Color(0.12f, 0.14f, 0.12f, 0.64f),
                new Color(0.80f, 0.58f, 0.30f, 0.32f),
                1.0f,
                14f);

            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 1f);
            scrollRectTransform.anchorMax = new Vector2(0f, 1f);
            scrollRectTransform.pivot = new Vector2(0f, 1f);
            scrollRectTransform.anchoredPosition = new Vector2(15f, -68f);
            scrollRectTransform.sizeDelta = new Vector2(size.x - 30f, size.y - 84f);

            viewport.offsetMin = new Vector2(10f, 10f);
            viewport.offsetMax = new Vector2(-14f, -10f);

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
            RectTransform footer = CampusUiRuntimeBuilder.CreatePanel(
                "Footer",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -804f),
                new Vector2(windowWidth - 48f, 92f),
                new Color(0.19f, 0.20f, 0.16f, 0.70f),
                new Color(0.84f, 0.62f, 0.34f, 0.32f),
                1.05f,
                14f,
                false);

            RectTransform rule = CampusUiRuntimeBuilder.CreatePanel(
                "FooterRule",
                footer,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 2f),
                new Color(1f, 0.76f, 0.30f, 0.10f),
                Color.clear,
                0f,
                0f,
                false);
            rule.offsetMin = new Vector2(18f, -2f);
            rule.offsetMax = new Vector2(-18f, 0f);

            statusText = CampusUiRuntimeBuilder.CreateText(
                "Status",
                footer,
                string.Empty,
                13,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextSecondary);
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(0f, 1f);
            statusText.rectTransform.pivot = new Vector2(0f, 0.5f);
            statusText.rectTransform.anchoredPosition = new Vector2(22f, 0f);
            statusText.rectTransform.sizeDelta = new Vector2(420f, 56f);

            newMapNameField = CampusUiRuntimeBuilder.CreateInputField(
                "NewMapInput",
                footer,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.EnterNewMapName),
                16,
                CampusUiVisualTheme.TextPrimary,
                CampusUiVisualTheme.TextMuted);
            newMapNamePlaceholderText = newMapNameField.placeholder as Text;
            RectTransform inputRect = newMapNameField.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 1f);
            inputRect.anchorMax = new Vector2(0f, 1f);
            inputRect.pivot = new Vector2(0f, 1f);
            inputRect.anchoredPosition = new Vector2(520f, -28f);
            inputRect.sizeDelta = new Vector2(320f, 36f);
            newMapNameField.onValueChanged.AddListener(value => newMapName = value ?? string.Empty);

            createMapButton = CampusUiRuntimeBuilder.CreateButton(
                "CreateMapButton",
                footer,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.CreateNewMap),
                BeginNewMapLoad,
                CampusUiVisualTheme.AccentSoftFill,
                CampusUiVisualTheme.Accent,
                16f,
                1.1f,
                CampusUiVisualTheme.TextPrimary,
                16);
            createMapButtonText = createMapButton.GetComponentInChildren<Text>();
            RectTransform createRect = createMapButton.GetComponent<RectTransform>();
            createRect.anchorMin = new Vector2(0f, 1f);
            createRect.anchorMax = new Vector2(0f, 1f);
            createRect.pivot = new Vector2(0f, 1f);
            createRect.anchoredPosition = new Vector2(854f, -28f);
            createRect.sizeDelta = new Vector2(160f, 36f);

            refreshButton = CampusUiRuntimeBuilder.CreateButton(
                "RefreshButton",
                footer,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Refresh),
                RebuildOptions,
                CampusUiVisualTheme.PanelDim,
                CampusUiVisualTheme.BorderSoft,
                16f,
                1.1f,
                CampusUiVisualTheme.TextSecondary,
                16);
            refreshButtonText = refreshButton.GetComponentInChildren<Text>();
            RectTransform refreshRect = refreshButton.GetComponent<RectTransform>();
            refreshRect.anchorMin = new Vector2(0f, 1f);
            refreshRect.anchorMax = new Vector2(0f, 1f);
            refreshRect.pivot = new Vector2(0f, 1f);
            refreshRect.anchoredPosition = new Vector2(1028f, -28f);
            refreshRect.sizeDelta = new Vector2(126f, 36f);

            startButton = CampusUiRuntimeBuilder.CreateButton(
                "StartButton",
                footer,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartGame),
                BeginGameLoad,
                CampusUiVisualTheme.AccentSoftFill,
                CampusUiVisualTheme.Accent,
                18f,
                1.4f,
                CampusUiVisualTheme.TextPrimary,
                18);
            startButtonText = startButton.GetComponentInChildren<Text>();
            RectTransform startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(1f, 1f);
            startRect.anchorMax = new Vector2(1f, 1f);
            startRect.pivot = new Vector2(1f, 1f);
            startRect.anchoredPosition = new Vector2(-22f, -26f);
            startRect.sizeDelta = new Vector2(214f, 44f);

            RefreshFooterText();
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
                    new Color(1f, 1f, 1f, 0.92f),
                    false,
                    false);
                artwork.rectTransform.offsetMin = new Vector2(-30f, -30f);
                artwork.rectTransform.offsetMax = new Vector2(30f, 30f);
            }

            RectTransform dimmer = CampusUiRuntimeBuilder.CreateFullScreenPanel(
                "LoadingBackdropDimmer",
                backdrop,
                new Color(0.40f, 0.27f, 0.12f, 0.22f),
                false);
            dimmer.SetAsLastSibling();

            RectTransform page = CampusUiRuntimeBuilder.CreatePanel(
                "LoadingPage",
                loadingPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1520f, 860f),
                new Color(0.40f, 0.34f, 0.25f, 0.62f),
                new Color(1f, 0.82f, 0.44f, 0.42f),
                1f,
                8f,
                false);
            page.SetAsLastSibling();
            page.localScale = Vector3.one * 0.99f;

            RectTransform topRule = CampusUiRuntimeBuilder.CreatePanel(
                "LoadingTopRule",
                page,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 2f),
                new Color(1f, 0.84f, 0.42f, 0.68f),
                Color.clear,
                0f,
                0f,
                false);
            topRule.offsetMin = new Vector2(96f, -2f);
            topRule.offsetMax = new Vector2(-96f, 0f);

            RectTransform leftRail = CampusUiRuntimeBuilder.CreatePanel(
                "LoadingLeftRail",
                page,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(96f, 0f),
                new Vector2(4f, 0f),
                new Color(1f, 0.76f, 0.28f, 1f),
                Color.clear,
                0f,
                2f,
                false);
            leftRail.offsetMin = new Vector2(96f, 136f);
            leftRail.offsetMax = new Vector2(100f, -168f);

            loadingTitleText = CampusUiRuntimeBuilder.CreateText(
                "LoadingTitle",
                page,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Loading),
                64,
                TextAnchor.MiddleLeft,
                new Color(1f, 0.92f, 0.66f, 1f),
                FontStyle.Bold);
            loadingTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            loadingTitleText.rectTransform.anchorMax = new Vector2(0f, 1f);
            loadingTitleText.rectTransform.pivot = new Vector2(0f, 1f);
            loadingTitleText.rectTransform.anchoredPosition = new Vector2(132f, -154f);
            loadingTitleText.rectTransform.sizeDelta = new Vector2(420f, 68f);

            loadingStatusText = CampusUiRuntimeBuilder.CreateText(
                "LoadingStatus",
                page,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingGameplayScene),
                20,
                TextAnchor.UpperLeft,
                CampusUiVisualTheme.TextPrimary);
            loadingStatusText.rectTransform.anchorMin = new Vector2(0f, 1f);
            loadingStatusText.rectTransform.anchorMax = new Vector2(0f, 1f);
            loadingStatusText.rectTransform.pivot = new Vector2(0f, 1f);
            loadingStatusText.rectTransform.anchoredPosition = new Vector2(136f, -238f);
            loadingStatusText.rectTransform.sizeDelta = new Vector2(680f, 36f);

            loadingPercentText = CampusUiRuntimeBuilder.CreateText(
                "LoadingPercent",
                page,
                "0%",
                132,
                TextAnchor.MiddleRight,
                new Color(1f, 0.95f, 0.78f, 1f),
                FontStyle.Bold);
            loadingPercentText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            loadingPercentText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            loadingPercentText.rectTransform.pivot = new Vector2(1f, 0.5f);
            loadingPercentText.rectTransform.anchoredPosition = new Vector2(-128f, -52f);
            loadingPercentText.rectTransform.sizeDelta = new Vector2(420f, 140f);

            loadingPercentCaptionText = CampusUiRuntimeBuilder.CreateText(
                "LoadingPercentCaption",
                page,
                CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingGameplayScene),
                15,
                TextAnchor.MiddleRight,
                CampusUiVisualTheme.TextSecondary);
            loadingPercentCaptionText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            loadingPercentCaptionText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            loadingPercentCaptionText.rectTransform.pivot = new Vector2(1f, 0.5f);
            loadingPercentCaptionText.rectTransform.anchoredPosition = new Vector2(-132f, 54f);
            loadingPercentCaptionText.rectTransform.sizeDelta = new Vector2(360f, 24f);

            RectTransform progressFrame = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressFrame",
                page,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 132f),
                new Vector2(LoadingProgressWidth + 8f, 26f),
                new Color(0.13f, 0.16f, 0.14f, 0.84f),
                new Color(1f, 0.82f, 0.42f, 0.58f),
                1.1f,
                13f,
                false);

            RectTransform progressInnerShade = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressInnerShade",
                progressFrame,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 1f, 1f, 0.03f),
                Color.clear,
                0f,
                11f,
                false);
            progressInnerShade.offsetMin = new Vector2(3f, 3f);
            progressInnerShade.offsetMax = new Vector2(-3f, -3f);

            loadingFillRect = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressFill",
                progressFrame,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(2f, 0f),
                new Vector2(0f, 18f),
                new Color(1f, 0.76f, 0.26f, 1f),
                Color.clear,
                0f,
                10f,
                false);
            loadingFillRect.anchoredPosition = new Vector2(4f, 0f);
            loadingFillRect.pivot = new Vector2(0f, 0.5f);

            RectTransform fillGlow = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressFillGlow",
                loadingFillRect,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 1f, 1f, 0.10f),
                Color.clear,
                0f,
                10f,
                false);
            fillGlow.offsetMin = new Vector2(0f, 2f);
            fillGlow.offsetMax = new Vector2(0f, -2f);

            loadingSweepRect = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressSweep",
                progressFrame,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-60f, 0f),
                new Vector2(120f, 18f),
                new Color(1f, 0.97f, 0.78f, 0.48f),
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

            RectTransform markerRow = CampusUiRuntimeBuilder.CreatePanel(
                "ProgressMarkerRow",
                page,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 194f),
                new Vector2(0f, 8f),
                Color.clear,
                Color.clear,
                0f,
                0f,
                false);
            markerRow.offsetMin = new Vector2(132f, 194f);
            markerRow.offsetMax = new Vector2(-132f, 202f);

            for (int i = 0; i < 5; i++)
            {
                float normalized = i / 4f;
                RectTransform marker = CampusUiRuntimeBuilder.CreatePanel(
                    "ProgressMarker_" + i,
                    markerRow,
                    new Vector2(normalized, 0.5f),
                    new Vector2(normalized, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(i == 0 || i == 4 ? 6f : 4f, i == 0 || i == 4 ? 6f : 4f),
                    new Color(1f, 0.76f, 0.30f, i == 0 || i == 4 ? 0.42f : 0.22f),
                    Color.clear,
                    0f,
                    3f,
                    false);
                marker.anchoredPosition = Vector2.zero;
            }

            ApplyLoadingProgressVisual(0f);
        }

        private void RefreshLocalizedText()
        {
            if (headerTitleText != null)
            {
                headerTitleText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartupTitle);
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

            if (loadingPercentCaptionText != null && !isLoading)
            {
                loadingPercentCaptionText.text = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.PreparingSceneTransition);
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

            for (int i = 0; i < languageButtonViews.Count; i++)
            {
                LanguageButtonView view = languageButtonViews[i];
                if (view == null || view.Label == null)
                {
                    continue;
                }

                view.Label.text = CampusPlayerUiTextCatalog.Get(view.LabelId);
            }

            RefreshLanguageVisuals();
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
                LoadOptionView view = CreateOptionView(content, option, isMapPanel, () =>
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
            bool isMapPanel,
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
            rect.sizeDelta = new Vector2(0f, 80f);

            LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 80f;
            layoutElement.minHeight = 80f;
            layoutElement.flexibleWidth = 1f;

            StorageBoxGraphic graphic = cardObject.GetComponent<StorageBoxGraphic>();
            graphic.SetStyle(new Color(0.13f, 0.15f, 0.12f, 0.72f), CampusUiVisualTheme.BorderMuted, 0.9f, 12f);

            RectTransform indicator = CampusUiRuntimeBuilder.CreatePanel(
                "Indicator",
                cardObject.transform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(14f, 0f),
                new Vector2(5f, 52f),
                new Color(1f, 0.72f, 0.20f, 0.42f),
                Color.clear,
                0f,
                3f,
                false);

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
                18,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextPrimary,
                FontStyle.Bold);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(28f, -14f);
            titleText.rectTransform.sizeDelta = new Vector2(0f, 24f);

            Text metaText = CampusUiRuntimeBuilder.CreateText(
                "Meta",
                cardObject.transform,
                ResolveOptionMeta(option),
                13,
                TextAnchor.MiddleLeft,
                CampusUiVisualTheme.TextMuted);
            metaText.rectTransform.anchorMin = new Vector2(0f, 0f);
            metaText.rectTransform.anchorMax = new Vector2(1f, 0f);
            metaText.rectTransform.pivot = new Vector2(0.5f, 0f);
            metaText.rectTransform.anchoredPosition = new Vector2(28f, 12f);
            metaText.rectTransform.sizeDelta = new Vector2(0f, 20f);

            return new LoadOptionView
            {
                Root = rect,
                Background = graphic,
                Indicator = indicator,
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
                    selected ? new Color(0.43f, 0.32f, 0.22f, 0.94f) : new Color(0.13f, 0.15f, 0.12f, 0.72f),
                    selected ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderMuted,
                    selected ? 1.4f : 0.9f,
                    selected ? 14f : 12f);
                if (view.Indicator != null)
                {
                    StorageBoxGraphic indicatorGraphic = view.Indicator.GetComponent<StorageBoxGraphic>();
                    if (indicatorGraphic != null)
                    {
                        indicatorGraphic.SetStyle(
                            selected ? new Color(1f, 0.72f, 0.20f, 0.72f) : new Color(1f, 0.76f, 0.30f, 0.14f),
                            Color.clear,
                            0f,
                            3f);
                    }
                }
                view.TitleText.color = selected ? CampusUiVisualTheme.TextGold : CampusUiVisualTheme.TextPrimary;
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

            if (loadingPercentText != null)
            {
                int percentValue = Mathf.RoundToInt(clampedProgress * 100f);
                loadingPercentText.text = percentValue.ToString("00") + "%";
            }
        }

        private void SetLoadingStatus(string message)
        {
            if (loadingStatusText != null)
            {
                loadingStatusText.text = message;
            }

            if (loadingPercentCaptionText != null)
            {
                loadingPercentCaptionText.text = message ?? string.Empty;
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
                        selected ? CampusUiVisualTheme.AccentSoftFill : CampusUiVisualTheme.PanelDim,
                        selected ? CampusUiVisualTheme.Accent : CampusUiVisualTheme.BorderSoft,
                        selected ? 1.4f : 1.1f,
                        16f);
                }

                view.Label.color = selected ? CampusUiVisualTheme.TextGold : CampusUiVisualTheme.TextPrimary;
            }

            RefreshFooterText();
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
