using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Modes;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Sanctions;
using NtingCampus.Gameplay.Schedule;
using UnityEngine;

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
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private CampusPrankService prankService;
        [SerializeField] private CampusSanctionService sanctionService;
        [SerializeField] private CampusGameState gameState = new CampusGameState();
        [SerializeField] private CampusResourceState resourceState = new CampusResourceState();
        [SerializeField] private CampusEventLog eventLog = new CampusEventLog();

        private bool isInitialized;

        public static CampusGameBootstrap Instance { get; private set; }

        public CampusGameState GameState => gameState;
        public CampusResourceState ResourceState => resourceState;
        public CampusEventLog EventLog => eventLog;
        public CampusTimeController TimeController => timeController;
        public CampusModeController ModeController => modeController;
        public CampusWorldService WorldService => worldService;
        public CampusRosterService RosterService => rosterService;
        public CampusScheduleService ScheduleService => scheduleService;
        public CampusGameplayEventHub GameplayEventHub => gameplayEventHub;
        public CampusPrankService PrankService => prankService;
        public CampusSanctionService SanctionService => sanctionService;

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
            bootstrap.EnsureWorldService();
            bootstrap.EnsureRosterService();
            bootstrap.EnsureScheduleService();
            bootstrap.EnsureGameplayEventHub();
            bootstrap.EnsureSanctionService();
            bootstrap.EnsurePrankService();
            bootstrap.EnsureDebugPanel();
            return bootstrap;
        }

        public void InitializeGameplay()
        {
            if (isInitialized)
            {
                return;
            }

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

            worldService = EnsureWorldService();
            worldService.Initialize(this);

            rosterService = EnsureRosterService();
            rosterService.Initialize(this);

            scheduleService = EnsureScheduleService();
            scheduleService.Initialize(this);

            gameplayEventHub = EnsureGameplayEventHub();
            gameplayEventHub.Initialize(this);

            sanctionService = EnsureSanctionService();
            sanctionService.Initialize(this);

            prankService = EnsurePrankService();
            prankService.Initialize(this);

            isInitialized = true;
            eventLog.AddLog("[System] " + timeController.CurrentDateText +
                            " gameplay bootstrap initialized. Money=" + resourceState.Money +
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

        private CampusWorldService EnsureWorldService()
        {
            if (worldService != null)
            {
                return worldService;
            }

            worldService = GetComponent<CampusWorldService>();
            if (worldService == null)
            {
                worldService = gameObject.AddComponent<CampusWorldService>();
            }

            return worldService;
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

        private CampusScheduleService EnsureScheduleService()
        {
            if (scheduleService != null)
            {
                return scheduleService;
            }

            scheduleService = GetComponent<CampusScheduleService>();
            if (scheduleService == null)
            {
                scheduleService = gameObject.AddComponent<CampusScheduleService>();
            }

            return scheduleService;
        }

        private CampusGameplayEventHub EnsureGameplayEventHub()
        {
            if (gameplayEventHub != null)
            {
                return gameplayEventHub;
            }

            gameplayEventHub = GetComponent<CampusGameplayEventHub>();
            if (gameplayEventHub == null)
            {
                gameplayEventHub = gameObject.AddComponent<CampusGameplayEventHub>();
            }

            return gameplayEventHub;
        }

        private CampusPrankService EnsurePrankService()
        {
            if (prankService != null)
            {
                return prankService;
            }

            prankService = GetComponent<CampusPrankService>();
            if (prankService == null)
            {
                prankService = gameObject.AddComponent<CampusPrankService>();
            }

            return prankService;
        }

        private CampusSanctionService EnsureSanctionService()
        {
            if (sanctionService != null)
            {
                return sanctionService;
            }

            sanctionService = GetComponent<CampusSanctionService>();
            if (sanctionService == null)
            {
                sanctionService = gameObject.AddComponent<CampusSanctionService>();
            }

            return sanctionService;
        }
    }
}
