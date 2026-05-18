using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    [DisallowMultipleComponent]
    public sealed class CampusInspectionDebugPanel : MonoBehaviour
    {
        private static readonly Rect PanelRect = new Rect(650f, 10f, 500f, 520f);
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusInspectionService inspectionService;
        [SerializeField] private KeyCode toggleKey = KeyCode.F11;
        [SerializeField] private bool isVisible;
        [SerializeField, Range(0.5f, 1.5f)] private float uiScaleSensitivity = 1f;
        [SerializeField, Range(0f, 1f)] private float uiScaleMatchWidthOrHeight = 0.45f;
        [SerializeField, Min(0.5f)] private float minUiScale = 0.85f;
        [SerializeField, Min(1f)] private float maxUiScale = 2.1f;
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private string lastActionMessage = "Press F11 to toggle inspection debug.";

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            inspectionService = bootstrap != null ? bootstrap.InspectionService : inspectionService;
            if (inspectionService == null)
            {
                inspectionService = CampusInspectionService.Resolve();
            }
        }

        private void Awake()
        {
            Initialize(bootstrap);
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

            ResolveReferences();
            using (CampusGuiScaleUtility.BeginScaledGui(
                       ReferenceResolution,
                       uiScaleMatchWidthOrHeight,
                       uiScaleSensitivity,
                       minUiScale,
                       maxUiScale))
            {
                GUILayout.BeginArea(PanelRect, "Inspection Debug (" + CampusInteractionInput.GetKeyLabel(toggleKey) + ")", GUI.skin.window);
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
                DrawPanelContent();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
        }

        private void DrawPanelContent()
        {
            if (inspectionService == null)
            {
                GUILayout.Label("InspectionService: none");
                return;
            }

            CampusInspectionDebugSnapshot snapshot = inspectionService.BuildDebugSnapshot();
            DrawSnapshot(snapshot);
            GUILayout.Space(6f);
            GUILayout.Label("Actions");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Seed Contraband"))
            {
                RunAction(inspectionService.TrySeedDebugContraband);
            }

            if (GUILayout.Button("Force Questioning"))
            {
                RunAction(inspectionService.TryForceQuestioning);
            }

            if (GUILayout.Button("Force Search"))
            {
                RunAction(inspectionService.TryForceSearch);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label("Last Action: " + (string.IsNullOrWhiteSpace(lastActionMessage) ? "-" : lastActionMessage));
        }

        private static void DrawSnapshot(CampusInspectionDebugSnapshot snapshot)
        {
            GUILayout.Label("Status: " + snapshot.Status);
            DrawLine("Room", snapshot.IsAvailable ? snapshot.RoomId + " / " + snapshot.RoomType : "-");
            DrawLine("Area Pressure", "Search=" + snapshot.AreaSearchPressure + ", Question=" + snapshot.AreaQuestioningPressure);
            DrawLine("Search Inspector", FormatActor(snapshot.SearchInspectorName, snapshot.SearchInspectorId));
            DrawLine("Questioner", FormatActor(snapshot.QuestionerName, snapshot.QuestionerId));
            DrawLine("Highest Vigilance NPC",
                FormatActor(snapshot.HighestVigilanceNpcName, snapshot.HighestVigilanceNpcId) +
                " / " + snapshot.HighestVigilancePressure);
            DrawLine("Final Chance",
                "Search=" + snapshot.SearchPressure + "%, Question=" + snapshot.QuestioningPressure + "%");
            DrawLine("Cooldown",
                "Search=" + snapshot.SearchCooldownRemaining.ToString("0.0") +
                "s, Question=" + snapshot.QuestioningCooldownRemaining.ToString("0.0") + "s");
            DrawLine("Carried Contraband",
                snapshot.HasContraband
                    ? snapshot.ContrabandItemName + " in " + snapshot.ContrabandContainerId
                    : "none");
            DrawLine("Confiscated Evidence", snapshot.ConfiscatedItemCount.ToString());
        }

        private static void DrawLine(string label, string value)
        {
            GUILayout.Label(label + ": " + (string.IsNullOrWhiteSpace(value) ? "-" : value));
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

        private void RunAction(InspectionDebugAction action)
        {
            if (action == null)
            {
                lastActionMessage = "Missing action.";
                return;
            }

            bool succeeded = action(out string message);
            lastActionMessage = (succeeded ? "OK: " : "Failed: ") + message;
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (inspectionService == null && bootstrap != null)
            {
                inspectionService = bootstrap.InspectionService;
            }

            if (inspectionService == null)
            {
                inspectionService = FindFirstObjectByType<CampusInspectionService>(FindObjectsInactive.Include);
            }
        }

        private delegate bool InspectionDebugAction(out string message);
    }
}
