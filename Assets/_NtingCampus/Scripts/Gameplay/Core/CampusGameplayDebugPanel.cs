using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Modes;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Sanctions;
using NtingCampus.Gameplay.Schedule;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    [DisallowMultipleComponent]
    public sealed class CampusGameplayDebugPanel : MonoBehaviour
    {
        private static readonly Rect PanelRect = new Rect(10f, 10f, 620f, 920f);
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusPrankService prankService;
        [SerializeField] private CampusSanctionService sanctionService;
        [SerializeField] private CampusClassroomLoopService classroomLoopService;
        [SerializeField] private CampusDisplayLanguage defaultLanguage = CampusDisplayLanguage.Chinese;
        [SerializeField] private KeyCode toggleKey = KeyCode.F9;
        [SerializeField] private bool isVisible;
        [SerializeField, Range(0.5f, 1.5f)] private float uiScaleSensitivity = 1f;
        [SerializeField, Range(0f, 1f)] private float uiScaleMatchWidthOrHeight = 0.45f;
        [SerializeField, Min(0.5f)] private float minUiScale = 0.85f;
        [SerializeField, Min(1f)] private float maxUiScale = 2.1f;
        [SerializeField] private string selectedCharacterId = string.Empty;
        [SerializeField] private Vector2 scrollPosition;

        public void Bind(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap;
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
        }

        private void Awake()
        {
            ResolveBootstrap();
            ResolvePrankService();
            ResolveSanctionService();
        }

        private void Update()
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

            if (CampusInteractionInput.WasKeyPressed(toggleKey))
            {
                isVisible = !isVisible;
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !isVisible)
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
            CampusNpcEcologyService npcEcologyService = targetBootstrap.NpcEcologyService;
            CampusCommerceService commerceService = targetBootstrap.CommerceService;
            CampusPrankService targetPrankService = ResolvePrankService();
            CampusSanctionService targetSanctionService = ResolveSanctionService();
            CampusClassroomLoopService targetClassroomLoopService = ResolveClassroomLoopService(targetBootstrap);
            CampusDisplayLanguage displayLanguage = CampusLanguageState.CurrentLanguage;
            if (!Application.isPlaying)
            {
                displayLanguage = defaultLanguage;
            }

            using (CampusGuiScaleUtility.BeginScaledGui(
                       ReferenceResolution,
                       uiScaleMatchWidthOrHeight,
                       uiScaleSensitivity,
                       minUiScale,
                       maxUiScale))
            {
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
                DrawFormalMainlineState(targetPrankService, targetSanctionService, targetClassroomLoopService, npcEcologyService, commerceService, displayLanguage);
                GUILayout.Space(4f);
                GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.RecentEventLogs));
                DrawRecentLogs(eventLog, 10, displayLanguage);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
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

        private CampusPrankService ResolvePrankService()
        {
            if (prankService != null)
            {
                return prankService;
            }

            prankService = FindFirstObjectByType<CampusPrankService>(FindObjectsInactive.Include);
            return prankService;
        }

        private CampusSanctionService ResolveSanctionService()
        {
            if (sanctionService != null)
            {
                return sanctionService;
            }

            sanctionService = FindFirstObjectByType<CampusSanctionService>(FindObjectsInactive.Include);
            return sanctionService;
        }

        private CampusClassroomLoopService ResolveClassroomLoopService(CampusGameBootstrap targetBootstrap)
        {
            if (classroomLoopService != null)
            {
                return classroomLoopService;
            }

            classroomLoopService = targetBootstrap != null ? targetBootstrap.ClassroomLoopService : null;
            if (classroomLoopService != null)
            {
                return classroomLoopService;
            }

            classroomLoopService = FindFirstObjectByType<CampusClassroomLoopService>(FindObjectsInactive.Include);
            return classroomLoopService;
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
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Suspicion, "-"));
                GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.AlertLevel, "-"));
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
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Suspicion, gameState.PlayerSuspicion));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.AlertLevel,
                CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Canteen) + "=" + gameState.CanteenAlertLevel +
                ", " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Delivery) + "=" + gameState.DeliveryAlertLevel));
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
                                ? rosterService.PlayerRuntime.Data.GetDisplayName(displayLanguage)
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

                string buttonLabel = data.GetDisplayName(displayLanguage) + " [" + CampusGameplayDebugTextCatalog.FormatCharacterRole(displayLanguage, data.Role) + "]";
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
            selected.EnsureEcologyInitialized();
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Name, selected.GetDisplayName(displayLanguage)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Role, CampusGameplayDebugTextCatalog.FormatCharacterRole(displayLanguage, selected.Role)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Duty, FormatDuty(selected, displayLanguage)));
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
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Ecology) + ": " +
                            CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Mood) + "=" + selected.Mood +
                            ", " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Social) + "=" + selected.SocialEnergy);
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Relationships) + ": " +
                            FormatRelationships(selected.Relationships, displayLanguage));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Traits, JoinTraits(selected.Traits, displayLanguage)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.FormatLine(displayLanguage, CampusGameplayDebugTextId.Memories, JoinMemories(selected.Memories, displayLanguage)));
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Possessions) + ": " +
                            FormatPossessions(selected.Possessions, displayLanguage));
        }

        private static string FormatDuty(CampusCharacterData data, CampusDisplayLanguage displayLanguage)
        {
            if (data == null)
            {
                return "-";
            }

            if (data.Role == CampusCharacterRole.Staff)
            {
                return CampusGameplayDebugTextCatalog.FormatStaffDuty(displayLanguage, data.StaffDuty);
            }

            return CampusGameplayDebugTextCatalog.FormatTeacherDuty(displayLanguage, data.TeacherDuty);
        }

        private static void DrawFormalMainlineState(
            CampusPrankService prankService,
            CampusSanctionService sanctionService,
            CampusClassroomLoopService classroomLoopService,
            CampusNpcEcologyService npcEcologyService,
            CampusCommerceService commerceService,
            CampusDisplayLanguage displayLanguage)
        {
            GUILayout.Label(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.FormalMainline));
            if (classroomLoopService == null)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.ClassroomLoopServiceMissing));
            }
            else
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.ClassroomLoop) + ": " + classroomLoopService.CurrentPrompt);
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.ActiveClassroom) + ": " + (string.IsNullOrWhiteSpace(classroomLoopService.ActiveClassroomId) ? "-" : classroomLoopService.ActiveClassroomId));
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.DistractedTeacher) + ": " + (string.IsNullOrWhiteSpace(classroomLoopService.DistractedTeacherId) ? "-" : classroomLoopService.DistractedTeacherId));
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.DozeSneakCaughtToday) + ": " +
                                classroomLoopService.DailyDozeEventCount + "/" +
                                classroomLoopService.DailySneakOutCount + "/" +
                                classroomLoopService.DailyCaughtSkippingCount);
                if (GUILayout.Button(CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.ForceSleepyDistraction)))
                {
                    classroomLoopService.TryForceSleepyDistraction();
                }
            }

            if (prankService == null)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.PrankServiceMissing));
            }
            else
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Prompt) + ": " + prankService.CurrentPrompt);
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.DailyPassNoteCount) + ": " + prankService.DailyPassNoteCount);
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.CanteenTheftCount) + ": " + prankService.DailyCanteenTheftCount + " / " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Clerk) + "=" + prankService.CurrentCanteenClerkState);
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.DeliveryTheftCount) + ": " + prankService.DailyDeliveryTheftCount + " / " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Order) + "=" + prankService.ActiveDeliveryOrderState + " " + prankService.ActiveDeliveryItemName);
            }

            if (sanctionService == null)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.SanctionServiceMissing));
            }
            else
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.LastSanction) + ": " + sanctionService.LastIssuedLevel);
            }

            if (npcEcologyService == null)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.NpcEcologyMissing));
            }
            else
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.NpcEcology) + ": " + npcEcologyService.CurrentSummary);
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.GossipEcologyEventsToday) + ": " + npcEcologyService.GossipHeat + "/" + npcEcologyService.DailyEcologyEventCount);
            }

            if (commerceService == null)
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.CommerceMissing));
            }
            else
            {
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.Commerce) + ": " + commerceService.CurrentSummary);
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.MealsStorePurchasesToday) + ": " +
                                commerceService.DailyCanteenMealsServed + "/" +
                                commerceService.DailyStorePurchasesCompleted);
                GUILayout.Label("- " + CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.StoreShelvesUnpaidThefts) + ": " +
                                commerceService.KnownStoreShelfCount + "/" +
                                commerceService.UnpaidStoreItemCount + "/" +
                                commerceService.DailyStoreTheftCount);
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

        private static string FormatRelationships(IReadOnlyList<CampusCharacterRelationship> relationships, CampusDisplayLanguage displayLanguage)
        {
            if (relationships == null || relationships.Count == 0)
            {
                return CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None);
            }

            List<string> parts = new List<string>(relationships.Count);
            for (int i = 0; i < relationships.Count; i++)
            {
                CampusCharacterRelationship relationship = relationships[i];
                if (relationship == null || string.IsNullOrWhiteSpace(relationship.TargetId))
                {
                    continue;
                }

                parts.Add(relationship.TargetId + " T=" + relationship.Trust + " S=" + relationship.Suspicion);
            }

            return parts.Count > 0
                ? string.Join(", ", parts)
                : CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None);
        }

        private static string FormatPossessions(IReadOnlyList<CampusCharacterPossession> possessions, CampusDisplayLanguage displayLanguage)
        {
            if (possessions == null || possessions.Count == 0)
            {
                return CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None);
            }

            List<string> parts = new List<string>(possessions.Count);
            for (int i = 0; i < possessions.Count; i++)
            {
                CampusCharacterPossession possession = possessions[i];
                if (possession == null || string.IsNullOrWhiteSpace(possession.DisplayName))
                {
                    continue;
                }

                parts.Add(possession.DisplayName);
            }

            return parts.Count > 0
                ? string.Join(", ", parts)
                : CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None);
        }

        private static string JoinMemories(IReadOnlyList<CampusCharacterMemoryId> memories, CampusDisplayLanguage displayLanguage)
        {
            if (memories == null || memories.Count == 0)
            {
                return CampusGameplayDebugTextCatalog.Get(displayLanguage, CampusGameplayDebugTextId.None);
            }

            List<string> names = new List<string>(memories.Count);
            for (int i = 0; i < memories.Count; i++)
            {
                names.Add(CampusCharacterTextCatalog.GetMemory(displayLanguage, memories[i]));
            }

            return string.Join(", ", names);
        }
    }
}
