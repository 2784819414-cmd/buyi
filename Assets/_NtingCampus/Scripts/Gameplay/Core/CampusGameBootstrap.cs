using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Modes;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Sanctions;
using NtingCampus.Gameplay.Schedule;
using NtingCampus.Gameplay.TheftConsequences;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    [DisallowMultipleComponent]
    public sealed class CampusGameBootstrap : MonoBehaviour
    {
        [SerializeField, Min(1)] private int initialDay = 1;
        [SerializeField] private CampusGameStateInitialization initialGameState =
            CampusGameStateInitialization.CreateDefault(1);
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusModeController modeController;
        [SerializeField] private CampusGameplayActionService actionService;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private CampusInventoryTransferService inventoryTransferService;
        [SerializeField] private CampusSanctionService sanctionService;
        [SerializeField] private CampusTheftConsequenceService theftConsequenceService;
        [SerializeField] private CampusEconomyService economyService;
        [SerializeField] private CampusGameplayHudController gameplayHudController;
        [SerializeField] private CampusPlayerInventoryController playerInventoryController;
        [SerializeField] private CampusGameState gameState = new CampusGameState();
        [SerializeField] private CampusEventLog eventLog = new CampusEventLog();

        private bool isInitialized;

        public static CampusGameBootstrap Instance { get; private set; }

        public CampusGameState GameState => gameState;
        public CampusEventLog EventLog => eventLog;
        public CampusTimeController TimeController => timeController;
        public CampusModeController ModeController => modeController;
        public CampusGameplayActionService ActionService => actionService;
        public CampusWorldService WorldService => worldService;
        public CampusRosterService RosterService => rosterService;
        public CampusScheduleService ScheduleService => scheduleService;
        public CampusGameplayEventHub GameplayEventHub => gameplayEventHub;
        public CampusInventoryTransferService InventoryTransferService => inventoryTransferService;
        public CampusSanctionService SanctionService => sanctionService;
        public CampusTheftConsequenceService TheftConsequenceService => theftConsequenceService;
        public CampusEconomyService EconomyService => economyService;
        public CampusGameplayHudController GameplayHudController => gameplayHudController;
        public CampusPlayerInventoryController PlayerInventoryController => playerInventoryController;

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
            bootstrap.EnsureActionService();
            bootstrap.EnsureWorldService();
            bootstrap.EnsureRosterService();
            bootstrap.EnsureScheduleService();
            bootstrap.EnsureGameplayEventHub();
            bootstrap.EnsureInventoryTransferService();
            bootstrap.EnsureSanctionService();
            bootstrap.EnsureTheftConsequenceService();
            bootstrap.EnsureEconomyService();
            bootstrap.EnsureGameplayHudController();
            bootstrap.EnsurePlayerInventoryController();
            bootstrap.EnsureSettingsOverlay();
            bootstrap.EnsureLaunchSelectionApplier();
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

            eventLog = new CampusEventLog();

            timeController = EnsureTimeController();
            timeController.InitializeTimeSystem(this, true);

            actionService = EnsureActionService();
            actionService.Initialize(this);

            worldService = EnsureWorldService();
            worldService.Initialize(this);

            rosterService = EnsureRosterService();
            rosterService.Initialize(this);

            worldService.ValidateEcology(rosterService, true);

            modeController = EnsureModeController();
            modeController.InitializeModes(this, false);

            scheduleService = EnsureScheduleService();
            scheduleService.Initialize(this);

            gameplayEventHub = EnsureGameplayEventHub();
            gameplayEventHub.Initialize(this);

            inventoryTransferService = EnsureInventoryTransferService();
            inventoryTransferService.Initialize(this);

            sanctionService = EnsureSanctionService();
            sanctionService.Initialize(this);

            economyService = EnsureEconomyService();
            economyService.Initialize(this);

            theftConsequenceService = EnsureTheftConsequenceService();
            theftConsequenceService.Initialize(this);

            playerInventoryController = EnsurePlayerInventoryController();
            playerInventoryController.Initialize(this);

            gameplayHudController = EnsureGameplayHudController();
            gameplayHudController.Initialize(this);

            isInitialized = true;
            eventLog.AddLog(CampusCoreTextCatalog.Format(
                CampusCoreTextId.GameplayBootstrapInitialized,
                timeController.CurrentDateText,
                gameState.Day,
                gameState.CampusOrder,
                gameState.CampusChaos));
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(CampusCoreTextCatalog.Get(CampusCoreTextId.MultipleBootstrapInstances));
                return;
            }

            Instance = this;
            InitializeGameplay();
            EnsureSettingsOverlay();
            EnsureLaunchSelectionApplier();
            EnsurePlayerInventoryController();
            EnsureInventoryTransferService();
            EnsureTheftConsequenceService();
            EnsureGameplayHudController();
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
            initialGameState.InitialDay = initialDay;
            initialGameState.InitialCampusOrder = Mathf.Clamp(initialGameState.InitialCampusOrder, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialCampusChaos = Mathf.Clamp(initialGameState.InitialCampusChaos, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialTeacherAlertness = Mathf.Clamp(initialGameState.InitialTeacherAlertness, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialDivineInterest = Mathf.Clamp(initialGameState.InitialDivineInterest, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialPlayerSuspicion = Mathf.Clamp(initialGameState.InitialPlayerSuspicion, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialPlayerTheftEvidence = Mathf.Clamp(initialGameState.InitialPlayerTheftEvidence, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialPlayerTheftRecord = Mathf.Clamp(initialGameState.InitialPlayerTheftRecord, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialCampusRumor = Mathf.Clamp(initialGameState.InitialCampusRumor, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialCampusCrackdown = Mathf.Clamp(initialGameState.InitialCampusCrackdown, CampusGameState.StatMin, CampusGameState.StatMax);
            initialGameState.InitialDailyWarningCount = Mathf.Max(0, initialGameState.InitialDailyWarningCount);
        }

        private void EnsureLaunchSelectionApplier()
        {
            CampusLaunchSelectionApplier launchSelectionApplier = GetComponent<CampusLaunchSelectionApplier>();
            if (launchSelectionApplier == null)
            {
                gameObject.AddComponent<CampusLaunchSelectionApplier>();
            }

            CampusRuntimeGameplayOverlayLoader overlayLoader = GetComponent<CampusRuntimeGameplayOverlayLoader>();
            if (overlayLoader == null)
            {
                gameObject.AddComponent<CampusRuntimeGameplayOverlayLoader>();
            }
        }

        private void EnsureSettingsOverlay()
        {
            CampusGameplaySettingsOverlay settingsOverlay = GetComponent<CampusGameplaySettingsOverlay>();
            if (settingsOverlay == null)
            {
                gameObject.AddComponent<CampusGameplaySettingsOverlay>();
            }
        }

        private CampusPlayerInventoryController EnsurePlayerInventoryController()
        {
            if (playerInventoryController != null)
            {
                return playerInventoryController;
            }

            playerInventoryController = GetComponent<CampusPlayerInventoryController>();
            if (playerInventoryController == null)
            {
                playerInventoryController = gameObject.AddComponent<CampusPlayerInventoryController>();
            }

            return playerInventoryController;
        }

        private CampusGameplayHudController EnsureGameplayHudController()
        {
            if (gameplayHudController != null)
            {
                return gameplayHudController;
            }

            gameplayHudController = GetComponent<CampusGameplayHudController>();
            if (gameplayHudController == null)
            {
                gameplayHudController = gameObject.AddComponent<CampusGameplayHudController>();
            }

            return gameplayHudController;
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

        private CampusGameplayActionService EnsureActionService()
        {
            if (actionService != null)
            {
                return actionService;
            }

            actionService = GetComponent<CampusGameplayActionService>();
            if (actionService == null)
            {
                actionService = gameObject.AddComponent<CampusGameplayActionService>();
            }

            return actionService;
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

        private CampusInventoryTransferService EnsureInventoryTransferService()
        {
            if (inventoryTransferService != null)
            {
                return inventoryTransferService;
            }

            inventoryTransferService = GetComponent<CampusInventoryTransferService>();
            if (inventoryTransferService == null)
            {
                inventoryTransferService = gameObject.AddComponent<CampusInventoryTransferService>();
            }

            return inventoryTransferService;
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

        private CampusTheftConsequenceService EnsureTheftConsequenceService()
        {
            if (theftConsequenceService != null)
            {
                return theftConsequenceService;
            }

            theftConsequenceService = GetComponent<CampusTheftConsequenceService>();
            if (theftConsequenceService == null)
            {
                theftConsequenceService = gameObject.AddComponent<CampusTheftConsequenceService>();
            }

            return theftConsequenceService;
        }

        private CampusEconomyService EnsureEconomyService()
        {
            if (economyService != null)
            {
                return economyService;
            }

            economyService = GetComponent<CampusEconomyService>();
            if (economyService == null)
            {
                economyService = gameObject.AddComponent<CampusEconomyService>();
            }

            return economyService;
        }
    }
}

