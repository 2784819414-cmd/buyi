using System;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Modes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NtingCampus.Gameplay.Core
{
    [DisallowMultipleComponent]
    public sealed class CampusGameBootstrap : MonoBehaviour
    {
        [SerializeField, Min(1)] private int initialDay = 1;
        [SerializeField, Min(0)] private int initialMoney = 500;
        [SerializeField, Min(0)] private int initialDivinePower;
        [SerializeField] private CampusGameStateInitialization initialGameState =
            CampusGameStateInitialization.CreateDefault(1);
        [SerializeField] private bool showDebugPanel = true;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusModeController modeController;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusGameState gameState = new CampusGameState();
        [SerializeField] private CampusResourceState resourceState = new CampusResourceState();
        [SerializeField] private CampusEventLog eventLog = new CampusEventLog();

        public static CampusGameBootstrap Instance { get; private set; }

        public CampusGameState GameState => gameState;
        public CampusResourceState ResourceState => resourceState;
        public CampusEventLog EventLog => eventLog;
        public CampusTimeController TimeController => timeController;
        public CampusModeController ModeController => modeController;
        public CampusRosterService RosterService => rosterService;

        public static CampusGameBootstrap EnsureSceneBootstrap()
        {
            CampusGameBootstrap existing = FindFirstObjectByType<CampusGameBootstrap>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            GameObject bootstrapObject = new GameObject("NtingCampus_GameplayBootstrap");
            CampusGameBootstrap bootstrap = bootstrapObject.AddComponent<CampusGameBootstrap>();
            bootstrap.EnsureTimeController();
            bootstrap.EnsureModeController();
            bootstrap.EnsureDebugPanel();
            return bootstrap;
        }

        public void InitializeGameplay()
        {
            gameState = new CampusGameState();
            CampusGameStateInitialization gameStateInitialization = initialGameState;
            gameStateInitialization.InitialDay = initialDay;
            gameState.Reset(gameStateInitialization);

            resourceState = new CampusResourceState();
            resourceState.Reset(initialMoney, initialDivinePower);

            eventLog = new CampusEventLog();

            timeController = EnsureTimeController();
            timeController.InitializeTimeSystem(this, true);

            modeController = EnsureModeController();
            modeController.InitializeModes(this, false);

            rosterService = EnsureRosterService();
            rosterService.Initialize(this);

            eventLog.AddLog("[System] " + timeController.CurrentDateText +
                            " gameplay bootstrap ready. Money=" + resourceState.Money +
                            ", DivinePower=" + resourceState.DivinePower +
                            ", Day=" + gameState.Day +
                            ", Order=" + gameState.CampusOrder +
                            ", Chaos=" + gameState.CampusChaos + ".");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple CampusGameBootstrap instances detected. Keeping the first instance.");
                return;
            }

            Instance = this;
            InitializeGameplay();
            EnsureDebugPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnValidate()
        {
            initialDay = Mathf.Max(1, initialDay);
            initialMoney = Mathf.Max(0, initialMoney);
            initialDivinePower = Mathf.Max(0, initialDivinePower);
            initialGameState.InitialDay = initialDay;
            initialGameState.InitialCampusOrder = Mathf.Clamp(initialGameState.InitialCampusOrder, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialCampusChaos = Mathf.Clamp(initialGameState.InitialCampusChaos, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialTeacherAlertness = Mathf.Clamp(initialGameState.InitialTeacherAlertness, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialDivineInterest = Mathf.Clamp(initialGameState.InitialDivineInterest, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialDailyWarningCount = Mathf.Max(0, initialGameState.InitialDailyWarningCount);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameplayBootstrapAfterSceneLoad()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, "CampusMap", System.StringComparison.Ordinal))
            {
                return;
            }

            CampusGameBootstrap existing = EnsureSceneBootstrap();
            existing.EnsureTimeController();
            existing.EnsureModeController();
            existing.EnsureRosterService();
            existing.EnsureDebugPanel();
        }

        private void EnsureDebugPanel()
        {
            if (!showDebugPanel)
            {
                return;
            }

            CampusGameplayDebugPanel debugPanel = GetComponent<CampusGameplayDebugPanel>();
            if (debugPanel == null)
            {
                debugPanel = gameObject.AddComponent<CampusGameplayDebugPanel>();
            }

            debugPanel.Bind(this);
        }

        private CampusTimeController EnsureTimeController()
        {
            if (timeController != null)
            {
                return timeController;
            }

            timeController = GetComponent<CampusTimeController>();
            if (timeController == null)
            {
                timeController = gameObject.AddComponent<CampusTimeController>();
            }

            return timeController;
        }

        private CampusModeController EnsureModeController()
        {
            if (modeController != null)
            {
                return modeController;
            }

            modeController = GetComponent<CampusModeController>();
            if (modeController == null)
            {
                modeController = gameObject.AddComponent<CampusModeController>();
            }

            return modeController;
        }

        private CampusRosterService EnsureRosterService()
        {
            if (rosterService != null)
            {
                return rosterService;
            }

            rosterService = GetComponent<CampusRosterService>();
            if (rosterService == null)
            {
                rosterService = gameObject.AddComponent<CampusRosterService>();
            }

            return rosterService;
        }
    }
}
