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
        private static readonly Vector2 WindowSize = new Vector2(760f, 620f);
        private const float ContentViewportHeight = 430f;
        private const float FooterButtonHeight = 44f;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
        [SerializeField, Range(0.5f, 1.5f)] private float uiScaleSensitivity = 1f;
        [SerializeField, Range(0f, 1f)] private float uiScaleMatchWidthOrHeight = 0.5f;
        [SerializeField, Min(0.5f)] private float minUiScale = 0.8f;
        [SerializeField, Min(1f)] private float maxUiScale = 2.25f;

        private bool isVisible;
        private bool pauseCaptured;
        private CampusGameplayPauseState pauseState;
        private string timeTestYearText = string.Empty;
        private string timeTestMonthText = string.Empty;
        private string timeTestDayText = string.Empty;
        private string timeTestHourText = string.Empty;
        private string timeTestMinuteText = string.Empty;
        private string timeTestStatusText = string.Empty;
        private Vector2 contentScrollPosition;

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
                contentScrollPosition = GUILayout.BeginScrollView(
                    contentScrollPosition,
                    false,
                    true,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUILayout.Height(ContentViewportHeight));
                GUILayout.BeginVertical(theme.SectionCard);
                GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Language), theme.SectionHeader);
                GUILayout.Space(10f);
                DrawLanguageButtons(theme);
                GUILayout.EndVertical();
                GUILayout.Space(14f);
                DrawTimeControlSection(theme);
                GUILayout.Space(14f);
                DrawTimeTestSection(theme);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrWhiteSpace(timeTestStatusText))
                {
                    GUILayout.Label(timeTestStatusText, theme.Subtitle);
                    GUILayout.Space(10f);
                }
                GUILayout.EndScrollView();
                GUILayout.Space(14f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(
                        CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.ReturnToMainMenu),
                        theme.SecondaryButton,
                        GUILayout.Height(FooterButtonHeight),
                        GUILayout.Width(240f)))
                {
                    ReturnToMainMenu();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(
                        CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.Continue),
                        theme.PrimaryButton,
                        GUILayout.Height(FooterButtonHeight),
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

        private void DrawTimeControlSection(CampusPlayerUiTheme theme)
        {
            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            if (timeController == null)
            {
                return;
            }

            GUILayout.BeginVertical(theme.SectionCard);
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeControlTitle), theme.SectionHeader);
            GUILayout.Space(8f);
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeControlDescription), theme.Subtitle);
            GUILayout.Space(10f);
            GUILayout.Label(
                CampusPlayerUiTextCatalog.Get(
                    timeController.IsTimePaused
                        ? CampusPlayerUiTextId.TimePauseStatus
                        : CampusPlayerUiTextId.TimeRunningStatus),
                theme.Subtitle);
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimePause),
                    theme.SecondaryButton,
                    GUILayout.Height(40f),
                    GUILayout.Width(220f)))
            {
                timeController.PauseTime(true);
            }

            GUILayout.Space(12f);

            if (GUILayout.Button(
                    CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeResume),
                    theme.PrimaryButton,
                    GUILayout.Height(40f),
                    GUILayout.Width(220f)))
            {
                timeController.ResumeTime(true);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawTimeTestSection(CampusPlayerUiTheme theme)
        {
            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            if (timeController == null)
            {
                return;
            }

            GUILayout.BeginVertical(theme.SectionCard);
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestTitle), theme.SectionHeader);
            GUILayout.Space(8f);
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestDescription), theme.Subtitle);
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(220f));
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestDate), theme.Subtitle);
            timeTestYearText = GUILayout.TextField(timeTestYearText ?? string.Empty, GUILayout.Height(34f), GUILayout.Width(68f));
            GUILayout.Space(6f);
            timeTestMonthText = GUILayout.TextField(timeTestMonthText ?? string.Empty, GUILayout.Height(34f), GUILayout.Width(68f));
            GUILayout.Space(6f);
            timeTestDayText = GUILayout.TextField(timeTestDayText ?? string.Empty, GUILayout.Height(34f), GUILayout.Width(68f));
            GUILayout.EndVertical();

            GUILayout.Space(18f);

            GUILayout.BeginVertical(GUILayout.Width(180f));
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestHour), theme.Subtitle);
            timeTestHourText = GUILayout.TextField(timeTestHourText ?? string.Empty, GUILayout.Height(34f), GUILayout.Width(68f));
            GUILayout.Space(14f);
            GUILayout.Label(CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestMinute), theme.Subtitle);
            timeTestMinuteText = GUILayout.TextField(timeTestMinuteText ?? string.Empty, GUILayout.Height(34f), GUILayout.Width(68f));
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical(GUILayout.Width(220f));
            if (GUILayout.Button(
                    CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestReset),
                    theme.SecondaryButton,
                    GUILayout.Height(40f),
                    GUILayout.Width(220f)))
            {
                SyncTimeDraftFromController();
            }

            GUILayout.Space(10f);

            if (GUILayout.Button(
                    CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestApply),
                    theme.PrimaryButton,
                    GUILayout.Height(46f),
                    GUILayout.Width(220f)))
            {
                ApplyTimeDraft();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
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
                SyncTimeDraftFromController();
                timeTestStatusText = string.Empty;
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
        }

        private void ApplyTimeDraft()
        {
            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            if (timeController == null)
            {
                return;
            }

            if (!int.TryParse(timeTestYearText, out int year) ||
                !int.TryParse(timeTestMonthText, out int month) ||
                !int.TryParse(timeTestDayText, out int day))
            {
                timeTestStatusText = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidDate);
                return;
            }

            if (!int.TryParse(timeTestHourText, out int hour))
            {
                timeTestStatusText = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidHour);
                return;
            }

            if (!int.TryParse(timeTestMinuteText, out int minute))
            {
                timeTestStatusText = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidMinute);
                return;
            }

            if (!timeController.TrySetDateAndClock(year, month, day, hour, minute, true, out string errorMessage))
            {
                timeTestStatusText = errorMessage;
                return;
            }

            timeTestStatusText = string.Empty;
            SyncTimeDraftFromController();
        }
    }
}
