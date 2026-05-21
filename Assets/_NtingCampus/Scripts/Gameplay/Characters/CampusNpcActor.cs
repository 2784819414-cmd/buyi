using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcActor : MonoBehaviour
    {
        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusGameplayEventHub eventHub;
        [SerializeField] private CampusNpcMotor motor;
        [SerializeField] private CampusNpcAiHost aiHost;
        [SerializeField] private CampusNpcPresentation presentation;
        [SerializeField] private CampusNpcInteractionPresenter interactionPresenter;
        [SerializeField] private CampusHeldItemVisual heldItemVisual;

        private bool actorStackReady;
        private CampusCharacterRuntime configuredRuntime;
        private CampusGameBootstrap configuredBootstrap;
        private CampusWorldService configuredWorldService;
        private CampusRosterService configuredRosterService;
        private CampusTimeController configuredTimeController;
        private CampusGameplayEventHub configuredEventHub;

        public CampusCharacterRuntime Runtime => runtime;
        public CampusNpcPersonalProfile Profile => aiHost != null ? aiHost.Profile : null;
        public CampusNpcIntent ActiveIntent => aiHost != null ? aiHost.ActiveIntent : null;

        public void Initialize(
            CampusCharacterRuntime targetRuntime,
            CampusGameBootstrap targetBootstrap,
            CampusWorldService targetWorldService)
        {
            runtime = targetRuntime != null ? targetRuntime : GetComponent<CampusCharacterRuntime>();
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            worldService = targetWorldService != null
                ? targetWorldService
                : bootstrap != null
                    ? bootstrap.WorldService
                    : null;

            ResolveReferences();
            EnsureActorStack();
            if (CanRunNpc())
            {
                aiHost.StartNpc(resetAmbientSpeech: true);
            }
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureActorStack();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureActorStack();
            if (CanRunNpc())
            {
                aiHost.StartNpc(resetAmbientSpeech: false);
            }
        }

        private void OnDisable()
        {
            aiHost?.StopNpc();
        }

        private void Update()
        {
            if (!HasCoreReferences())
            {
                ResolveReferences();
            }

            if (!CanRunNpc())
            {
                return;
            }

            if (!actorStackReady || HasMissingActorStack())
            {
                EnsureActorStack();
            }
            else
            {
                ConfigureActorStackIfNeeded(false);
            }

            aiHost.TickNpc();
        }

        private void ResolveReferences()
        {
            if (runtime == null)
            {
                runtime = GetComponent<CampusCharacterRuntime>();
            }

            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (bootstrap == null)
            {
                return;
            }

            worldService = worldService != null ? worldService : bootstrap.WorldService;
            rosterService = rosterService != null ? rosterService : bootstrap.RosterService;
            timeController = timeController != null ? timeController : bootstrap.TimeController;
            eventHub = eventHub != null ? eventHub : bootstrap.GameplayEventHub;
        }

        private void EnsureActorStack()
        {
            EnsureMotorComponent();
            EnsurePresentationComponent();
            EnsureInteractionPresenterComponent();
            EnsureHeldItemVisualComponent();
            EnsureAiHostComponent();
            actorStackReady = !HasMissingActorStack();
            ConfigureActorStackIfNeeded(true);
        }

        private bool CanRunNpc()
        {
            return runtime != null && runtime.Data != null && !runtime.Data.IsPlayerControlled;
        }

        private bool HasCoreReferences()
        {
            return runtime != null &&
                   bootstrap != null &&
                   worldService != null &&
                   rosterService != null &&
                   timeController != null &&
                   eventHub != null;
        }

        private bool HasMissingActorStack()
        {
            return motor == null ||
                   aiHost == null ||
                   presentation == null ||
                   interactionPresenter == null ||
                   heldItemVisual == null;
        }

        private void ConfigureActorStackIfNeeded(bool force)
        {
            if (!actorStackReady)
            {
                return;
            }

            if (!force &&
                configuredRuntime == runtime &&
                configuredBootstrap == bootstrap &&
                configuredWorldService == worldService &&
                configuredRosterService == rosterService &&
                configuredTimeController == timeController &&
                configuredEventHub == eventHub)
            {
                return;
            }

            motor.Configure(runtime, worldService);
            motor.Ensure();
            presentation.Ensure(runtime != null ? runtime.Data : null);
            aiHost.Configure(
                runtime,
                bootstrap,
                worldService,
                rosterService,
                timeController,
                eventHub,
                motor,
                interactionPresenter);
            interactionPresenter.Ensure(aiHost);
            heldItemVisual.RefreshImmediate();

            configuredRuntime = runtime;
            configuredBootstrap = bootstrap;
            configuredWorldService = worldService;
            configuredRosterService = rosterService;
            configuredTimeController = timeController;
            configuredEventHub = eventHub;
        }

        private void EnsureMotorComponent()
        {
            if (motor == null)
            {
                motor = GetComponent<CampusNpcMotor>();
            }

            if (motor == null)
            {
                motor = gameObject.AddComponent<CampusNpcMotor>();
            }
        }

        private void EnsurePresentationComponent()
        {
            if (presentation == null)
            {
                presentation = GetComponent<CampusNpcPresentation>();
            }

            if (presentation == null)
            {
                presentation = gameObject.AddComponent<CampusNpcPresentation>();
            }
        }

        private void EnsureInteractionPresenterComponent()
        {
            if (interactionPresenter == null)
            {
                interactionPresenter = GetComponent<CampusNpcInteractionPresenter>();
            }

            if (interactionPresenter == null)
            {
                interactionPresenter = gameObject.AddComponent<CampusNpcInteractionPresenter>();
            }
        }

        private void EnsureHeldItemVisualComponent()
        {
            if (heldItemVisual == null)
            {
                heldItemVisual = GetComponent<CampusHeldItemVisual>();
            }

            if (heldItemVisual == null)
            {
                heldItemVisual = gameObject.AddComponent<CampusHeldItemVisual>();
            }
        }

        private void EnsureAiHostComponent()
        {
            if (aiHost == null)
            {
                aiHost = GetComponent<CampusNpcAiHost>();
            }

            if (aiHost == null)
            {
                aiHost = gameObject.AddComponent<CampusNpcAiHost>();
            }
        }
    }
}
