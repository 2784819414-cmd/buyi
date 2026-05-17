using System;
using System.Collections.Generic;
using System.IO;
using NtingCampusMapEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NtingCampus.Gameplay.UI
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

        private const string StartupSceneName = "Startup";
        private const string GameplaySceneName = "CampusMap";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Vector2 WindowSize = new Vector2(1280f, 820f);
        private static readonly Vector2 WindowOffset = new Vector2(0f, -10f);
        private static readonly Color StartupCameraColor = new Color(0.05f, 0.09f, 0.16f, 1f);

        [SerializeField] private bool showOnStartup = true;
        [SerializeField, Range(0.5f, 1.5f)] private float uiScaleSensitivity = 1f;
        [SerializeField, Range(0f, 1f)] private float uiScaleMatchWidthOrHeight = 0.5f;
        [SerializeField, Min(0.5f)] private float minUiScale = 0.8f;
        [SerializeField, Min(1f)] private float maxUiScale = 2.25f;
        [SerializeField] private string mapFolderOverride = string.Empty;
        [SerializeField] private string saveFolderOverride = string.Empty;
        [SerializeField] private string exportFolderOverride = string.Empty;

        private readonly List<LoadOption> mapOptions = new List<LoadOption>();
        private readonly List<LoadOption> saveOptions = new List<LoadOption>();

        private Vector2 mapScrollPosition;
        private Vector2 saveScrollPosition;
        private AsyncOperation loadingOperation;
        private string statusText = string.Empty;
        private string newMapName = string.Empty;
        private int selectedMapIndex;
        private int selectedSaveIndex;
        private bool isVisible;
        private bool isLoading;

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

            RebuildOptions();
            isVisible = true;
        }

        private void Update()
        {
            if (!isLoading || loadingOperation == null)
            {
                return;
            }

            if (loadingOperation.isDone)
            {
                enabled = false;
            }
        }

        private void OnGUI()
        {
            if (!showOnStartup)
            {
                return;
            }

            using (CampusGuiScaleUtility.BeginScaledGui(
                       ReferenceResolution,
                       uiScaleMatchWidthOrHeight,
                       uiScaleSensitivity,
                       minUiScale,
                       maxUiScale))
            {
                if (isLoading)
                {
                    DrawLoadingOverlay();
                    return;
                }

                if (!isVisible)
                {
                    return;
                }

                CampusPlayerUiTheme theme = CampusPlayerUiTheme.Instance;
                theme.DrawOverlay();
                Rect windowRect = CampusGuiScaleUtility.BuildCenteredRect(ReferenceResolution, WindowSize, WindowOffset);
                GUILayout.BeginArea(windowRect, theme.Panel);
                GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartupTitle), theme.Title);
                GUILayout.Space(8f);
                GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartupDescription), theme.Subtitle);
                GUILayout.Space(18f);
                DrawLanguageSection(theme);
                GUILayout.Space(22f);

                GUILayout.BeginHorizontal();
                DrawOptionColumn(
                    theme,
                    CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Map),
                    mapOptions,
                    selectedMapIndex,
                    ref mapScrollPosition,
                    out int nextMapIndex);
                selectedMapIndex = nextMapIndex;
                GUILayout.Space(18f);
                DrawOptionColumn(
                    theme,
                    CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Save),
                    saveOptions,
                    selectedSaveIndex,
                    ref saveScrollPosition,
                    out int nextSaveIndex);
                selectedSaveIndex = nextSaveIndex;
                GUILayout.EndHorizontal();

                GUILayout.Space(14f);
                if (!string.IsNullOrWhiteSpace(statusText))
                {
                    GUILayout.Label(statusText, theme.StatusLabel, GUILayout.Height(26f));
                }
                else
                {
                    GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartHint), theme.Subtitle, GUILayout.Height(26f));
                }

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(500f));
                GUILayout.Label("新地图名称 / New Map Name", theme.Subtitle, GUILayout.Height(24f));
                newMapName = GUILayout.TextField(newMapName ?? string.Empty, GUILayout.Height(38f), GUILayout.Width(500f));
                GUILayout.EndVertical();

                GUILayout.Space(16f);
                if (GUILayout.Button(
                        CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.CreateNewMap),
                        theme.SecondaryButton,
                        GUILayout.Height(48f),
                        GUILayout.Width(220f)))
                {
                    BeginNewMapLoad();
                }

                GUILayout.Space(16f);
                if (GUILayout.Button(
                        CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Refresh),
                        theme.SecondaryButton,
                        GUILayout.Height(48f),
                        GUILayout.Width(180f)))
                {
                    RebuildOptions();
                }

                GUILayout.FlexibleSpace();
                GUI.enabled = mapOptions.Count > 0 && saveOptions.Count > 0;
                if (GUILayout.Button(
                        CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.StartGame),
                        theme.PrimaryButton,
                        GUILayout.Height(56f),
                        GUILayout.Width(320f)))
                {
                    BeginGameLoad();
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        private void DrawOptionColumn(
            CampusPlayerUiTheme theme,
            string title,
            List<LoadOption> options,
            int selectedIndex,
            ref Vector2 scrollPosition,
            out int nextSelectedIndex)
        {
            nextSelectedIndex = selectedIndex;
            GUILayout.BeginVertical(theme.SectionCard, GUILayout.Width(603f), GUILayout.Height(430f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, theme.SectionHeader);
            GUILayout.FlexibleSpace();
            GUILayout.Label(options.Count.ToString(), theme.SectionMeta, GUILayout.Width(32f));
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
            for (int i = 0; i < options.Count; i++)
            {
                GUIStyle buttonStyle = selectedIndex == i ? theme.OptionButtonSelected : theme.OptionButton;
                string label = selectedIndex == i ? "> " + options[i].Label : options[i].Label;
                if (GUILayout.Button(label, buttonStyle, GUILayout.Height(60f)))
                {
                    nextSelectedIndex = i;
                }

                GUILayout.Space(8f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void RebuildOptions()
        {
            mapOptions.Clear();
            saveOptions.Clear();
            selectedMapIndex = 0;
            selectedSaveIndex = 0;
            statusText = string.Empty;

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
        }

        private void BeginGameLoad()
        {
            if (mapOptions.Count == 0 || saveOptions.Count == 0)
            {
                statusText = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.NoLaunchOptions);
                return;
            }

            LoadOption mapOption = mapOptions[Mathf.Clamp(selectedMapIndex, 0, mapOptions.Count - 1)];
            LoadOption saveOption = saveOptions[Mathf.Clamp(selectedSaveIndex, 0, saveOptions.Count - 1)];

            CampusLaunchConfigStore.SetPendingSelection(
                mapOption != null && !mapOption.IsSceneDefault ? mapOption.Path : string.Empty,
                mapOption != null ? mapOption.Source : CampusRuntimeMapLoadSource.Scene,
                saveOption != null && !saveOption.IsNone ? saveOption.Path : string.Empty,
                saveOption != null ? saveOption.Source : CampusRuntimeMapLoadSource.Scene);

            isVisible = false;
            isLoading = true;
            statusText = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingGameplayScene);
            loadingOperation = SceneManager.LoadSceneAsync(GameplaySceneName, LoadSceneMode.Single);
            if (loadingOperation == null)
            {
                isLoading = false;
                isVisible = true;
                statusText = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingFailed);
                CampusLaunchConfigStore.Clear();
            }
        }

        private void BeginNewMapLoad()
        {
            string sanitizedMapName = SanitizeMapName(newMapName);
            if (string.IsNullOrWhiteSpace(sanitizedMapName))
            {
                statusText = "请输入新地图名称。";
                return;
            }

            string mapFolder = ResolveMapFolder();
            Directory.CreateDirectory(mapFolder);
            string mapPath = Path.Combine(mapFolder, "CampusMap_" + sanitizedMapName + ".json");
            if (File.Exists(mapPath))
            {
                statusText = "地图已存在：" + Path.GetFileNameWithoutExtension(mapPath);
                return;
            }

            CampusLaunchConfigStore.SetPendingSelection(
                mapPath,
                CampusRuntimeMapLoadSource.AuthoringPackage,
                string.Empty,
                CampusRuntimeMapLoadSource.Scene,
                true,
                sanitizedMapName);

            isVisible = false;
            isLoading = true;
            statusText = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingGameplayScene);
            loadingOperation = SceneManager.LoadSceneAsync(GameplaySceneName, LoadSceneMode.Single);
            if (loadingOperation == null)
            {
                isLoading = false;
                isVisible = true;
                statusText = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.LoadingFailed);
                CampusLaunchConfigStore.Clear();
            }
        }

        private void DrawLoadingOverlay()
        {
            CampusPlayerUiTheme theme = CampusPlayerUiTheme.Instance;
            theme.DrawOverlay();
            Rect windowRect = CampusGuiScaleUtility.BuildCenteredRect(ReferenceResolution, WindowSize, WindowOffset);
            Rect cardRect = new Rect(windowRect.x + 260f, windowRect.y + 190f, windowRect.width - 520f, 210f);
            GUILayout.BeginArea(cardRect, theme.Panel);
            GUILayout.Space(4f);
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Loading), theme.Title);
            GUILayout.Space(10f);
            GUILayout.Label(ResolveLoadingStatusText(), theme.OverlayLabel, GUILayout.Height(50f));
            GUILayout.Space(18f);
            Rect progressRect = GUILayoutUtility.GetRect(cardRect.width - 56f, 22f);
            GUI.Box(progressRect, GUIContent.none, theme.ProgressTrack);
            float progress = ResolveLoadingProgress();
            Rect fillRect = new Rect(
                progressRect.x + 3f,
                progressRect.y + 3f,
                Mathf.Max(0f, (progressRect.width - 6f) * progress),
                progressRect.height - 6f);
            GUI.Box(fillRect, GUIContent.none, theme.ProgressFill);
            GUILayout.EndArea();
        }

        private float ResolveLoadingProgress()
        {
            if (loadingOperation == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(loadingOperation.progress / 0.9f);
        }

        private string ResolveLoadingStatusText()
        {
            if (!string.IsNullOrWhiteSpace(statusText))
            {
                return statusText;
            }

            return CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.PreparingSceneTransition);
        }

        private void DrawLanguageSection(CampusPlayerUiTheme theme)
        {
            CampusDisplayLanguage currentLanguage = CampusLanguageState.CurrentLanguage;
            GUILayout.BeginVertical(theme.SectionCard, GUILayout.Height(108f));
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language), theme.SectionHeader);
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            DrawLanguageButton(theme, CampusDisplayLanguage.Chinese, CampusPlayerUiTextId.Chinese, currentLanguage);
            GUILayout.Space(10f);
            DrawLanguageButton(theme, CampusDisplayLanguage.English, CampusPlayerUiTextId.English, currentLanguage);
            GUILayout.Space(10f);
            DrawLanguageButton(theme, CampusDisplayLanguage.Bilingual, CampusPlayerUiTextId.Bilingual, currentLanguage);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawLanguageButton(
            CampusPlayerUiTheme theme,
            CampusDisplayLanguage language,
            CampusPlayerUiTextId labelId,
            CampusDisplayLanguage currentLanguage)
        {
            GUIStyle style = currentLanguage == language ? theme.PillButtonSelected : theme.PillButton;
            if (GUILayout.Button(
                    CampusPlayerUiTextCatalog.Get(labelId),
                    style,
                    GUILayout.Height(42f),
                    GUILayout.Width(220f)))
            {
                CampusLanguageState.SetLanguage(language);
                RebuildOptions();
            }
        }

        private string ResolveMapFolder()
        {
            if (!string.IsNullOrWhiteSpace(mapFolderOverride))
            {
                return mapFolderOverride;
            }

            return Path.Combine(Application.dataPath, "NtingCampus", "UserGeneratedRuntimeContent");
        }

        private string ResolveSaveFolder()
        {
            if (!string.IsNullOrWhiteSpace(saveFolderOverride))
            {
                return saveFolderOverride;
            }

            return Path.Combine(Application.persistentDataPath, "CampusPlayerMapSave");
        }

        private string ResolveExportFolder()
        {
            if (!string.IsNullOrWhiteSpace(exportFolderOverride))
            {
                return exportFolderOverride;
            }

            return Path.Combine(Application.persistentDataPath, "CampusMapExports");
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
            DateTime leftTime = File.GetLastWriteTimeUtc(left);
            DateTime rightTime = File.GetLastWriteTimeUtc(right);
            return rightTime.CompareTo(leftTime);
        }

        private static string SanitizeMapName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
            {
                trimmed = trimmed.Replace(invalidChars[i], '_');
            }

            trimmed = trimmed.Replace(' ', '_');
            while (trimmed.Contains("__"))
            {
                trimmed = trimmed.Replace("__", "_");
            }

            return trimmed.Trim('_');
        }

        private static void EnsureStartupCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            Camera existingCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (existingCamera != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("Startup Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = StartupCameraColor;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }
    }
}
