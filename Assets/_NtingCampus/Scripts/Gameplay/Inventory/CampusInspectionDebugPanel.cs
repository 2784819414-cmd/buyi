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
        [SerializeField] private string lastActionMessage =
            CampusInspectionTextCatalog.Get(CampusInspectionTextId.InitialDebugHint);

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
                GUILayout.BeginArea(
                    PanelRect,
                    CampusInspectionTextCatalog.Get(CampusInspectionTextId.DebugPanelTitle) +
                    " (" + CampusInteractionInput.GetKeyLabel(toggleKey) + ")",
                    GUI.skin.window);
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
                GUILayout.Label(CampusInspectionTextCatalog.Get(CampusInspectionTextId.ServiceMissing));
                return;
            }

            CampusInspectionDebugSnapshot snapshot = inspectionService.BuildDebugSnapshot();
            DrawSnapshot(snapshot);
            GUILayout.Space(6f);
            GUILayout.Label(CampusInspectionTextCatalog.Get(CampusInspectionTextId.Actions));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(CampusInspectionTextCatalog.Get(CampusInspectionTextId.SeedContraband)))
            {
                RunAction(inspectionService.TrySeedDebugContraband);
            }

            if (GUILayout.Button(CampusInspectionTextCatalog.Get(CampusInspectionTextId.ForceQuestioning)))
            {
                RunAction(inspectionService.TryForceQuestioning);
            }

            if (GUILayout.Button(CampusInspectionTextCatalog.Get(CampusInspectionTextId.ForceSearch)))
            {
                RunAction(inspectionService.TryForceSearch);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label(CampusInspectionTextCatalog.FormatLine(CampusInspectionTextId.LastAction, lastActionMessage));
        }

        private static void DrawSnapshot(CampusInspectionDebugSnapshot snapshot)
        {
            DrawLine(CampusInspectionTextId.Status, snapshot.Status);
            DrawLine(CampusInspectionTextId.Room, snapshot.IsAvailable ? snapshot.RoomId + " / " + snapshot.RoomType : "-");
            DrawLine(
                CampusInspectionTextId.AreaPressure,
                CampusInspectionTextCatalog.Get(CampusInspectionTextId.Search) + "=" + snapshot.AreaSearchPressure +
                ", " + CampusInspectionTextCatalog.Get(CampusInspectionTextId.Question) + "=" + snapshot.AreaQuestioningPressure);
            DrawLine(CampusInspectionTextId.SearchInspector, FormatActor(snapshot.SearchInspectorName, snapshot.SearchInspectorId));
            DrawLine(CampusInspectionTextId.Questioner, FormatActor(snapshot.QuestionerName, snapshot.QuestionerId));
            DrawLine(CampusInspectionTextId.HighestVigilanceNpc,
                FormatActor(snapshot.HighestVigilanceNpcName, snapshot.HighestVigilanceNpcId) +
                " / " + snapshot.HighestVigilancePressure);
            DrawLine(CampusInspectionTextId.FinalChance,
                CampusInspectionTextCatalog.Get(CampusInspectionTextId.Search) + "=" + snapshot.SearchPressure +
                "%, " + CampusInspectionTextCatalog.Get(CampusInspectionTextId.Question) + "=" +
                snapshot.QuestioningPressure + "%");
            DrawLine(CampusInspectionTextId.Cooldown,
                CampusInspectionTextCatalog.Get(CampusInspectionTextId.Search) + "=" +
                snapshot.SearchCooldownRemaining.ToString("0.0") + "s, " +
                CampusInspectionTextCatalog.Get(CampusInspectionTextId.Question) + "=" +
                snapshot.QuestioningCooldownRemaining.ToString("0.0") + "s");
            DrawLine(CampusInspectionTextId.CarriedContraband,
                snapshot.HasContraband
                    ? CampusInspectionTextCatalog.Format(
                        CampusInspectionTextId.InContainer,
                        snapshot.ContrabandItemName,
                        snapshot.ContrabandContainerId)
                    : CampusInspectionTextCatalog.Get(CampusInspectionTextId.None));
            DrawLine(CampusInspectionTextId.ConfiscatedEvidence, snapshot.ConfiscatedItemCount.ToString());
        }

        private static void DrawLine(CampusInspectionTextId label, string value)
        {
            GUILayout.Label(CampusInspectionTextCatalog.FormatLine(label, value));
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
                lastActionMessage = CampusInspectionTextCatalog.Get(CampusInspectionTextId.MissingAction);
                return;
            }

            bool succeeded = action(out string message);
            lastActionMessage = CampusInspectionTextCatalog.Format(
                succeeded ? CampusInspectionTextId.ActionOk : CampusInspectionTextId.ActionFailed,
                message);
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
