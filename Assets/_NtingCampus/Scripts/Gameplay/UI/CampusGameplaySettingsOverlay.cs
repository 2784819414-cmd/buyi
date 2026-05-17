using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NtingCampus.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class CampusGameplaySettingsOverlay : MonoBehaviour
    {
        private const string StartupSceneName = "Startup";
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Vector2 WindowSize = new Vector2(640f, 360f);

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
        [SerializeField, Range(0.5f, 1.5f)] private float uiScaleSensitivity = 1f;
        [SerializeField, Range(0f, 1f)] private float uiScaleMatchWidthOrHeight = 0.5f;
        [SerializeField, Min(0.5f)] private float minUiScale = 0.8f;
        [SerializeField, Min(1f)] private float maxUiScale = 2.25f;

        private bool isVisible;
        private bool pauseCaptured;
        private CampusGameplayPauseState pauseState;

        private void Awake()
        {
            bootstrap = bootstrap != null ? bootstrap : GetComponent<CampusGameBootstrap>();
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
            SetVisible(false);
        }

        private void OnGUI()
        {
            if (!isVisible)
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
                CampusPlayerUiTheme theme = CampusPlayerUiTheme.Instance;
                theme.DrawOverlay();
                Rect windowRect = CampusGuiScaleUtility.BuildCenteredRect(ReferenceResolution, WindowSize, Vector2.zero);
                GUILayout.BeginArea(windowRect, theme.Panel);
                GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsTitle), theme.Title);
                GUILayout.Space(8f);
                GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.SettingsDescription), theme.Subtitle);
                GUILayout.Space(18f);
                GUILayout.BeginVertical(theme.SectionCard);
                GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language), theme.SectionHeader);
                GUILayout.Space(10f);
                DrawLanguageButtons(theme);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(
                        CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.ReturnToMainMenu),
                        theme.SecondaryButton,
                        GUILayout.Height(44f),
                        GUILayout.Width(240f)))
                {
                    ReturnToMainMenu();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(
                        CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Continue),
                        theme.PrimaryButton,
                        GUILayout.Height(44f),
                        GUILayout.Width(220f)))
                {
                    SetVisible(false);
                }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        private void DrawLanguageButtons(CampusPlayerUiTheme theme)
        {
            CampusDisplayLanguage currentLanguage = CampusLanguageState.CurrentLanguage;
            GUILayout.BeginHorizontal();
            DrawLanguageButton(theme, CampusDisplayLanguage.Chinese, CampusPlayerUiTextId.Chinese, currentLanguage);
            GUILayout.Space(10f);
            DrawLanguageButton(theme, CampusDisplayLanguage.English, CampusPlayerUiTextId.English, currentLanguage);
            GUILayout.Space(10f);
            DrawLanguageButton(theme, CampusDisplayLanguage.Bilingual, CampusPlayerUiTextId.Bilingual, currentLanguage);
            GUILayout.EndHorizontal();
        }

        private static void DrawLanguageButton(
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
            }
        }

        private void SetVisible(bool visible)
        {
            if (isVisible == visible)
            {
                return;
            }

            isVisible = visible;
            if (isVisible)
            {
                if (!pauseCaptured)
                {
                    pauseState = CampusGameplayPauseUtility.Pause(bootstrap);
                    pauseCaptured = true;
                }
                return;
            }

            if (pauseCaptured)
            {
                CampusGameplayPauseUtility.Resume(bootstrap, pauseState);
                pauseCaptured = false;
            }
        }

        private void ReturnToMainMenu()
        {
            isVisible = false;
            pauseCaptured = false;
            CampusLaunchConfigStore.Clear();
            SceneManager.LoadScene(StartupSceneName, LoadSceneMode.Single);
        }
    }
}
