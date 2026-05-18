using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    [DisallowMultipleComponent]
    public sealed class CampusEcologyOverviewPanel : MonoBehaviour
    {
        private static readonly Rect PanelRect = new Rect(1160f, 10f, 520f, 620f);
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private KeyCode toggleKey = KeyCode.F12;
        [SerializeField] private bool isVisible;
        [SerializeField, Range(0.5f, 1.5f)] private float uiScaleSensitivity = 1f;
        [SerializeField, Range(0f, 1f)] private float uiScaleMatchWidthOrHeight = 0.45f;
        [SerializeField, Min(0.5f)] private float minUiScale = 0.85f;
        [SerializeField, Min(1f)] private float maxUiScale = 2.1f;
        [SerializeField] private Vector2 scrollPosition;

        public void Bind(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
        }

        private void Awake()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
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

            using (CampusGuiScaleUtility.BeginScaledGui(
                       ReferenceResolution,
                       uiScaleMatchWidthOrHeight,
                       uiScaleSensitivity,
                       minUiScale,
                       maxUiScale))
            {
                GUILayout.BeginArea(PanelRect, "Ecology Overview (" + CampusInteractionInput.GetKeyLabel(toggleKey) + ")", GUI.skin.window);
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
                DrawOverview(targetBootstrap);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
        }

        private void DrawOverview(CampusGameBootstrap targetBootstrap)
        {
            CampusGameState gameState = targetBootstrap.GameState;
            CampusRosterService rosterService = targetBootstrap.RosterService;
            CampusNpcEcologyService npcEcologyService = targetBootstrap.NpcEcologyService;
            CampusInspectionService inspectionService = targetBootstrap.InspectionService;
            CampusPrankService prankService = targetBootstrap.PrankService;
            CampusInspectionDebugSnapshot inspectionSnapshot = inspectionService != null
                ? inspectionService.BuildDebugSnapshot()
                : default;

            GUILayout.Label("World State");
            DrawLine("Order / Chaos", gameState != null ? gameState.CampusOrder + " / " + gameState.CampusChaos : "-");
            DrawLine("Teacher Alert / Player Suspicion",
                gameState != null ? gameState.TeacherAlertness + " / " + gameState.PlayerSuspicion : "-");
            DrawLine("Daily Warnings", gameState != null ? gameState.DailyWarningCount.ToString() : "-");

            GUILayout.Space(6f);
            GUILayout.Label("Player Risk");
            DrawLine("Room", inspectionSnapshot.IsAvailable ? inspectionSnapshot.RoomId + " / " + inspectionSnapshot.RoomType : "-");
            DrawLine("Carried Contraband",
                inspectionSnapshot.HasContraband
                    ? inspectionSnapshot.ContrabandItemName + " in " + inspectionSnapshot.ContrabandContainerId
                    : "none");
            DrawLine("Confiscated Evidence", inspectionSnapshot.ConfiscatedItemCount.ToString());
            DrawLine("Highest Nearby Vigilance",
                FormatActor(inspectionSnapshot.HighestVigilanceNpcName, inspectionSnapshot.HighestVigilanceNpcId) +
                " / " + inspectionSnapshot.HighestVigilancePressure);

            GUILayout.Space(6f);
            GUILayout.Label("Inspection Ecology");
            if (inspectionService == null)
            {
                DrawLine("Inspection", "none");
            }
            else
            {
                DrawLine("Today Q/S/Found/Confiscated",
                    inspectionService.DailyQuestioningCount + "/" +
                    inspectionService.DailySearchCount + "/" +
                    inspectionService.DailyContrabandFoundCount + "/" +
                    inspectionService.DailyConfiscatedItemCount);
                DrawLine("Reports / Proactive Patrols",
                    inspectionService.DailyTattletaleReportCount + "/" +
                    inspectionService.DailyProactiveInspectionCount);
                DrawLine("Highest Risk Area", FormatHighestRiskArea(inspectionService.AreaPressureRules));
                DrawLine("Current", inspectionService.CurrentInspectionSummary);
            }

            GUILayout.Space(6f);
            GUILayout.Label("NPC Ecology");
            if (npcEcologyService == null)
            {
                DrawLine("NPC Ecology", "none");
            }
            else
            {
                DrawLine("Gossip / Events Today", npcEcologyService.GossipHeat + " / " + npcEcologyService.DailyEcologyEventCount);
                DrawLine("Most Suspicious NPC", ResolveMostSuspiciousNpc(rosterService));
                DrawLine("Current", npcEcologyService.CurrentSummary);
            }

            GUILayout.Space(6f);
            GUILayout.Label("Theft Loops");
            if (prankService == null)
            {
                DrawLine("Prank Service", "none");
            }
            else
            {
                DrawLine("Pass Notes / Canteen Theft / Delivery Theft",
                    prankService.DailyPassNoteCount + " / " +
                    prankService.DailyCanteenTheftCount + " / " +
                    prankService.DailyDeliveryTheftCount);
                DrawLine("Delivery", prankService.ActiveDeliveryOrderState + " " + prankService.ActiveDeliveryItemName);
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

        private static void DrawLine(string label, string value)
        {
            GUILayout.Label(label + ": " + (string.IsNullOrWhiteSpace(value) ? "-" : value));
        }

        private static string FormatHighestRiskArea(IReadOnlyList<CampusAreaInspectionPressureRule> rules)
        {
            if (rules == null || rules.Count == 0)
            {
                return "-";
            }

            CampusAreaInspectionPressureRule bestRule = null;
            int bestScore = -1;
            for (int i = 0; i < rules.Count; i++)
            {
                CampusAreaInspectionPressureRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                int score = rule.SearchPressure.Value + rule.QuestioningPressure.Value;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRule = rule;
                }
            }

            if (bestRule == null)
            {
                return "-";
            }

            string label = !string.IsNullOrWhiteSpace(bestRule.RoomId)
                ? bestRule.RoomId
                : bestRule.RoomType.ToString();
            return label + " S=" + bestRule.SearchPressure.Value + " Q=" + bestRule.QuestioningPressure.Value;
        }

        private static string ResolveMostSuspiciousNpc(CampusRosterService rosterService)
        {
            if (rosterService == null || rosterService.PlayerRuntime == null)
            {
                return "-";
            }

            string playerId = rosterService.PlayerRuntime.CharacterId;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return "-";
            }

            CampusCharacterRuntime bestRuntime = null;
            int bestSuspicion = -1;
            IReadOnlyList<CampusCharacterRuntime> runtimes = rosterService.Runtimes;
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                int suspicion = runtime.Data.GetRelationshipSuspicion(playerId);
                if (suspicion > bestSuspicion)
                {
                    bestSuspicion = suspicion;
                    bestRuntime = runtime;
                }
            }

            return bestRuntime != null
                ? FormatActor(bestRuntime.Data.DisplayName, bestRuntime.CharacterId) + " / " + bestSuspicion
                : "-";
        }

        private static string FormatActor(string displayName, string actorId)
        {
            if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(actorId))
            {
                return "-";
            }

            if (string.IsNullOrWhiteSpace(actorId))
            {
                return displayName;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return actorId;
            }

            return displayName + " (" + actorId + ")";
        }
    }
}
