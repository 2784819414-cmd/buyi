using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Modes;
using NtingCampus.Gameplay.Skeleton;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    [DisallowMultipleComponent]
    public sealed class CampusGameplayDebugPanel : MonoBehaviour
    {
        private static readonly Rect PanelRect = new Rect(10f, 10f, 620f, 920f);

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusMischiefActionController mischiefController;
        [SerializeField] private CampusMischiefConsequenceController consequenceController;
        [SerializeField] private CampusDisplayLanguage displayLanguage = CampusDisplayLanguage.Bilingual;
        [SerializeField] private string selectedCharacterId = string.Empty;
        [SerializeField] private Vector2 scrollPosition;

        public void Bind(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap;
        }

        private void Awake()
        {
            ResolveBootstrap();
            ResolveMischiefController();
            ResolveConsequenceController();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            CampusRuntimeMapEditor runtimeMapEditor = CampusRuntimeMapEditor.Instance;
            if (runtimeMapEditor != null && runtimeMapEditor.IsOpen)
            {
                return;
            }

            CampusGameBootstrap targetBootstrap = ResolveBootstrap();
            if (targetBootstrap == null)
            {
                return;
            }

            CampusTimeController timeController = targetBootstrap.TimeController;
            CampusResourceState resourceState = targetBootstrap.ResourceState;
            CampusEventLog eventLog = targetBootstrap.EventLog;
            CampusGameState gameState = targetBootstrap.GameState;
            CampusModeController targetModeController = targetBootstrap.ModeController;
            CampusRosterService rosterService = targetBootstrap.RosterService;
            CampusMischiefActionController targetMischiefController = ResolveMischiefController();
            CampusMischiefConsequenceController targetConsequenceController = ResolveConsequenceController();

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.GameDate, timeController != null ? timeController.CurrentDateText : "-"));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Time, timeController != null ? timeController.CurrentClockText : "-"));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Segment, timeController != null ? timeController.CurrentSegmentName : "-"));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Schedule, timeController != null ? timeController.CurrentTimeLabel : "-"));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Mode, targetModeController != null ? CampusGameplayDebugTextCatalog.FormatMode(displayLanguage, targetModeController.CurrentMode) : "-"));
            DrawGameState(gameState, displayLanguage);
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Money, resourceState != null ? resourceState.Money.ToString() : "-"));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.DivinePower, resourceState != null ? resourceState.DivinePower.ToString() : "-"));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.TimeScale, timeController != null ? timeController.TimeScale.ToString("0.##") + "x" : "-"));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.SpeedMode, timeController != null ? CampusGameplayDebugTextCatalog.FormatSpeedMode(displayLanguage, timeController.SpeedMode) : "-"));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.CameraOrtho, ResolveCameraOrthoText(displayLanguage)));
            DrawM1Controls(targetModeController, timeController, displayLanguage);
            GUILayout.Space(4f);
            DrawRosterState(rosterService, displayLanguage);
            GUILayout.Space(4f);
            DrawMischiefState(targetMischiefController, displayLanguage);
            GUILayout.Space(4f);
            DrawAreaRiskState(targetMischiefController, targetConsequenceController, displayLanguage);
            GUILayout.Space(4f);
            DrawRecentConsequenceLogs(targetConsequenceController, displayLanguage);
            GUILayout.Space(4f);
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.RecentEventLogs));
            DrawRecentLogs(eventLog, 10, displayLanguage);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private CampusGameBootstrap ResolveBootstrap()
        {
            if (bootstrap != null)
            {
                return bootstrap;
            }

            bootstrap = GetComponent<CampusGameBootstrap>();
            if (bootstrap != null)
            {
                return bootstrap;
            }

            bootstrap = CampusGameBootstrap.Instance;
            return bootstrap;
        }

        private CampusMischiefActionController ResolveMischiefController()
        {
            if (mischiefController != null)
            {
                return mischiefController;
            }

            mischiefController = FindFirstObjectByType<CampusMischiefActionController>(FindObjectsInactive.Include);
            return mischiefController;
        }

        private CampusMischiefConsequenceController ResolveConsequenceController()
        {
            if (consequenceController != null)
            {
                return consequenceController;
            }

            consequenceController = FindFirstObjectByType<CampusMischiefConsequenceController>(FindObjectsInactive.Include);
            return consequenceController;
        }

        private static void DrawM1Controls(CampusModeController modeController, CampusTimeController timeController, CampusDisplayLanguage displayLanguage)
        {
            GUILayout.Space(4f);
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.M1Controls));

            if (modeController != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.StudentBody)))
                {
                    modeController.SetMode(CampusGameMode.StudentBody, true);
                }

                if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.GodView)))
                {
                    modeController.SetMode(CampusGameMode.GodView, true);
                }

                GUILayout.EndHorizontal();
            }

            if (timeController == null)
            {
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Pause)))
            {
                timeController.SetSpeedMode(CampusTimeSpeedMode.Paused);
            }

            if (GUILayout.Button("1x"))
            {
                timeController.SetSpeedMode(CampusTimeSpeedMode.Normal);
            }

            if (GUILayout.Button("2x"))
            {
                timeController.SetSpeedMode(CampusTimeSpeedMode.Fast);
            }

            if (GUILayout.Button("200x"))
            {
                timeController.SetCustomTimeScale(200f);
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawGameState(CampusGameState gameState, CampusDisplayLanguage displayLanguage)
        {
            if (gameState == null)
            {
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Day, "-"));
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.CampusOrder, "-"));
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.CampusChaos, "-"));
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.TeacherAlertness, "-"));
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.DivineInterest, "-"));
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.DailyWarnings, "-"));
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.ShrineRoom, "-"));
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.LandExpansion, "-"));
                return;
            }

            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Day, gameState.Day));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.CampusOrder, gameState.CampusOrder));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.CampusChaos, gameState.CampusChaos));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.TeacherAlertness, gameState.TeacherAlertness));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.DivineInterest, gameState.DivineInterest));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.DailyWarnings, gameState.DailyWarningCount));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.ShrineRoom, CampusGameplayDebugTextCatalog.FormatLockState(displayLanguage, gameState.ShrineRoomUnlocked)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.LandExpansion, CampusGameplayDebugTextCatalog.FormatLockState(displayLanguage, gameState.LandExpansionUnlocked)));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.WarnPlus)))
            {
                gameState.AddDailyWarningCount(1);
            }

            if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.AlertPlus)))
            {
                gameState.AddTeacherAlertness(5);
            }

            if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.ChaosPlus)))
            {
                gameState.AddCampusChaos(5);
            }

            if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.InterestPlus)))
            {
                gameState.AddDivineInterest(5);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.OrderPlus)))
            {
                gameState.AddCampusOrder(5);
            }

            if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.UnlockShrine)))
            {
                gameState.UnlockShrineRoom();
            }

            if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.UnlockLand)))
            {
                gameState.UnlockLandExpansion();
            }

            GUILayout.EndHorizontal();
        }

        private static string ResolveCameraOrthoText(CampusDisplayLanguage displayLanguage)
        {
            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            }

            if (targetCamera == null)
            {
                return "-";
            }

            return targetCamera.orthographic
                ? targetCamera.orthographicSize.ToString("0.##")
                : CampusGameplayDebugTextCatalog.FormatPerspective(displayLanguage, targetCamera.fieldOfView.ToString("0.##"));
        }

        private void DrawRosterState(CampusRosterService rosterService, CampusDisplayLanguage displayLanguage)
        {
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Roster));
            if (rosterService == null)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None));
                return;
            }

            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Students, rosterService.StudentCount));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Teachers, rosterService.TeacherCount));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Player,
                            (rosterService.PlayerRuntime != null && rosterService.PlayerRuntime.Data != null
                                ? rosterService.PlayerRuntime.Data.DisplayName
                                : "-")));

            IReadOnlyList<CampusCharacterData> characters = rosterService.Characters;
            if (characters == null || characters.Count == 0)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.NoCharacters));
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedCharacterId) && characters[0] != null)
            {
                selectedCharacterId = characters[0].Id;
            }

            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.RosterList));
            for (int i = 0; i < characters.Count; i++)
            {
                CampusCharacterData data = characters[i];
                if (data == null)
                {
                    continue;
                }

                string buttonLabel = data.DisplayName + " [" + CampusGameplayDebugTextCatalog.FormatCharacterRole(displayLanguage, data.Role) + "]";
                if (GUILayout.Button(buttonLabel))
                {
                    selectedCharacterId = data.Id;
                }
            }

            CampusCharacterData selected = rosterService.FindCharacterData(selectedCharacterId);
            if (selected == null)
            {
                return;
            }

            GUILayout.Space(4f);
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.SelectedCharacter));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Name, selected.DisplayName));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Role, CampusGameplayDebugTextCatalog.FormatCharacterRole(displayLanguage, selected.Role)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Duty, CampusGameplayDebugTextCatalog.FormatTeacherDuty(displayLanguage, selected.TeacherDuty)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Class, string.IsNullOrWhiteSpace(selected.ClassId) ? "-" : selected.ClassId));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.State, CampusGameplayDebugTextCatalog.FormatCharacterState(displayLanguage, selected.State)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Sleepiness, selected.Sleepiness));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Mischief, selected.Mischief));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.StudyToday,
                CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Language) + "=" + selected.StudyTodayWorldLanguage +
                ", " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Math) + "=" + selected.StudyTodayMath));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Mastery,
                CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Language) + "=" + selected.MasteryWorldLanguage +
                ", " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Math) + "=" + selected.MasteryMath));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Traits, JoinTraits(selected.Traits, displayLanguage)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Memories, JoinMemories(selected.Memories, displayLanguage)));
        }

        private static void DrawMischiefState(CampusMischiefActionController controller, CampusDisplayLanguage displayLanguage)
        {
            if (controller == null)
            {
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.MischiefSkeleton, "-"));
                return;
            }

            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.MischiefHeat, controller.MischiefHeat));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.CurrentAction, controller.CurrentAvailableActionName));
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.InteractHint));
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.TodayCounts));
            IReadOnlyList<CampusMischiefActionDefinition> actions = CampusMischiefActionController.ActionDefinitions;
            for (int i = 0; i < actions.Count; i++)
            {
                CampusMischiefActionDefinition action = actions[i];
                GUILayout.Label("- " + action.DisplayName + ": " + controller.GetDailyActionCount(action.FunctionId));
            }

            GUILayout.Space(4f);
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.DebugActions));
            for (int i = 0; i < actions.Count; i++)
            {
                CampusMischiefActionDefinition action = actions[i];
                if (GUILayout.Button(action.DisplayName))
                {
                    controller.TryTriggerByFunctionId(action.FunctionId);
                }
            }
        }

        private static void DrawAreaRiskState(
            CampusMischiefActionController controller,
            CampusMischiefConsequenceController consequences,
            CampusDisplayLanguage displayLanguage)
        {
            if (consequences == null)
            {
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.AreaRisk, "-"));
                return;
            }

            string currentAreaHot = "-";
            if (controller != null &&
                controller.TryGetActionDefinition(controller.CurrentAvailableFunctionId, out CampusMischiefActionDefinition currentAction))
            {
                currentAreaHot = CampusGameplayDebugTextCatalog.FormatBool(displayLanguage, consequences.IsAreaSensitive(currentAction.AreaName));
            }

            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.CurrentAreaSensitive, currentAreaHot));
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.AreaSuspicionAndAlertLevel));
            IReadOnlyList<CampusMischiefAreaState> areas = consequences.AreaStates;
            for (int i = 0; i < areas.Count; i++)
            {
                CampusMischiefAreaState area = areas[i];
                if (area == null)
                {
                    continue;
                }

                GUILayout.Label("- " + area.AreaName +
                                ": " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Suspicion) + "=" + area.Suspicion +
                                " " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.AlertLevel) + "=" + area.AlertLevel +
                                " " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Hot) + "=" + CampusGameplayDebugTextCatalog.FormatBool(displayLanguage, area.IsTemporarilyHot));
            }
        }

        private static void DrawRecentConsequenceLogs(CampusMischiefConsequenceController consequences, CampusDisplayLanguage displayLanguage)
        {
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.RecentConsequences));
            IReadOnlyList<string> logs = consequences != null ? consequences.RecentConsequenceLogs : null;
            int logCount = logs != null ? logs.Count : 0;
            if (logCount == 0)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None));
                return;
            }

            int startIndex = Mathf.Max(0, logCount - 5);
            for (int i = startIndex; i < logCount; i++)
            {
                GUILayout.Label("- " + logs[i]);
            }
        }

        private static void DrawRecentLogs(CampusEventLog eventLog, int maxVisibleCount, CampusDisplayLanguage displayLanguage)
        {
            IReadOnlyList<string> entries = eventLog != null ? eventLog.Entries : null;
            int entryCount = entries != null ? entries.Count : 0;
            if (entryCount == 0)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None));
                return;
            }

            int startIndex = Mathf.Max(0, entryCount - Mathf.Max(1, maxVisibleCount));
            for (int i = startIndex; i < entryCount; i++)
            {
                GUILayout.Label("- " + entries[i]);
            }
        }

        private static string JoinTraits(IReadOnlyList<CampusCharacterTrait> traits, CampusDisplayLanguage displayLanguage)
        {
            if (traits == null || traits.Count == 0)
            {
                return CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None);
            }

            List<string> names = new List<string>(traits.Count);
            for (int i = 0; i < traits.Count; i++)
            {
                names.Add(CampusGameplayDebugTextCatalog.FormatCharacterTrait(displayLanguage, traits[i]));
            }

            return string.Join(", ", names);
        }

        private static string JoinMemories(IReadOnlyList<string> memories, CampusDisplayLanguage displayLanguage)
        {
            if (memories == null || memories.Count == 0)
            {
                return CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None);
            }

            return string.Join(", ", memories);
        }
    }
}
