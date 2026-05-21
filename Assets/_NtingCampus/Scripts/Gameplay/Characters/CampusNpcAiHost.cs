using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcAiHost : MonoBehaviour, ICampusNpcTalkSource
    {
        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusGameplayEventHub eventHub;
        [SerializeField] private CampusNpcMotor motor;
        [SerializeField] private CampusNpcInteractionPresenter interactionPresenter;
        [SerializeField] private CampusNpcPersonalProfile profile = new CampusNpcPersonalProfile();
        [SerializeField] private CampusNpcMindState mind = new CampusNpcMindState();

        private readonly CampusNpcAiRuntime aiRuntime = new CampusNpcAiRuntime();
        private ICampusNpcAiController aiController;
        private CampusTimeController subscribedTimeController;
        private bool runtimeBindingDirty = true;
        private bool hasBoundRole;
        private CampusCharacterRole boundRole;

        public CampusCharacterRuntime Runtime => runtime;
        internal CampusNpcAiRuntime AiRuntime
        {
            get
            {
                BindAiRuntimeIfNeeded();
                return aiRuntime;
            }
        }

        public CampusNpcPersonalProfile Profile => profile;
        public CampusNpcIntent ActiveIntent => mind.CurrentIntent;
        public bool IsTalkAvailable => enabled && runtime != null && runtime.Data != null;

        public void Configure(
            CampusCharacterRuntime targetRuntime,
            CampusGameBootstrap targetBootstrap,
            CampusWorldService targetWorldService,
            CampusRosterService targetRosterService,
            CampusTimeController targetTimeController,
            CampusGameplayEventHub targetEventHub,
            CampusNpcMotor targetMotor,
            CampusNpcInteractionPresenter targetInteractionPresenter)
        {
            CampusCharacterRuntime nextRuntime = targetRuntime != null ? targetRuntime : runtime;
            CampusGameBootstrap nextBootstrap = targetBootstrap != null ? targetBootstrap : bootstrap;
            CampusWorldService nextWorldService = targetWorldService != null ? targetWorldService : worldService;
            CampusRosterService nextRosterService = targetRosterService != null ? targetRosterService : rosterService;
            CampusTimeController nextTimeController = targetTimeController != null ? targetTimeController : timeController;
            CampusGameplayEventHub nextEventHub = targetEventHub != null ? targetEventHub : eventHub;
            CampusNpcMotor nextMotor = targetMotor != null ? targetMotor : motor;
            CampusNpcInteractionPresenter nextInteractionPresenter = targetInteractionPresenter != null
                ? targetInteractionPresenter
                : interactionPresenter;

            runtimeBindingDirty =
                runtimeBindingDirty ||
                runtime != nextRuntime ||
                bootstrap != nextBootstrap ||
                worldService != nextWorldService ||
                rosterService != nextRosterService ||
                timeController != nextTimeController ||
                eventHub != nextEventHub ||
                motor != nextMotor ||
                interactionPresenter != nextInteractionPresenter;

            runtime = nextRuntime;
            bootstrap = nextBootstrap;
            worldService = nextWorldService;
            rosterService = nextRosterService;
            timeController = nextTimeController;
            eventHub = nextEventHub;
            motor = nextMotor;
            interactionPresenter = nextInteractionPresenter;
        }

        public void StartNpc(bool resetAmbientSpeech)
        {
            SubscribeToRuntimeSignals();
            EnsureAiController();
            RebuildPersonalProfile();
            if (resetAmbientSpeech)
            {
                interactionPresenter?.ResetAmbientSpeechSchedule();
            }

            interactionPresenter?.EnsureAmbientSpeechScheduled(ResolvePersonalSeed(), true);
            aiRuntime.RequestDecisionSoon();
        }

        public void StopNpc()
        {
            UnsubscribeFromRuntimeSignals();
            motor?.ClearNavigation();
        }

        public void TickNpc()
        {
            if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return;
            }

            SubscribeToRuntimeSignals();
            EnsureAiController();
            aiRuntime.RefreshSensesIfDue();
            aiRuntime.ObserveEventFacts();
            aiRuntime.ObserveInventoryFacts();
            aiController?.Tick();
            interactionPresenter?.TickAmbientSpeech(ResolvePersonalSeed(), ResolveAmbientLine);
        }

        public bool TryTalk(GameObject actor, out string spokenLine)
        {
            spokenLine = BuildInteractiveLine(actor);
            Say(spokenLine, 2.2f, true);
            return !string.IsNullOrWhiteSpace(spokenLine);
        }

        public string ResolveInteractionPrompt(GameObject actor)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            string displayName = data != null
                ? data.GetDisplayName(CampusLanguageState.CurrentLanguage)
                : CampusInteractionTextCatalog.Get(CampusInteractionTextId.UnknownActor);
            return CampusCharacterTextCatalog.FormatTalkPrompt(CampusLanguageState.CurrentLanguage, displayName);
        }

        public void RequestDecisionSoon()
        {
            aiRuntime.RequestDecisionSoon();
        }

        private void RebuildPersonalProfile()
        {
            EnsureAiController();
            if (aiController != null)
            {
                profile = aiController.BuildProfile();
            }
            else
            {
                profile = new CampusNpcPersonalProfile();
                profile.Reset(runtime != null ? runtime.Data : null);
            }

            aiRuntime.SetProfile(profile);
        }

        private void EnsureAiController()
        {
            BindAiRuntimeIfNeeded();
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                aiController = null;
                hasBoundRole = false;
                return;
            }

            if (aiController == null || !hasBoundRole || boundRole != data.Role)
            {
                aiController = CampusNpcAiControllerFactory.Create(data);
                boundRole = data.Role;
                hasBoundRole = true;
                aiController?.Bind(aiRuntime);
            }
        }

        private void BindAiRuntimeIfNeeded()
        {
            if (!runtimeBindingDirty)
            {
                return;
            }

            aiRuntime.Bind(
                runtime,
                bootstrap,
                worldService,
                rosterService,
                timeController,
                eventHub,
                mind,
                profile,
                transform,
                ResolvePersonalSeed,
                Say);
            motor?.BindNavigator(aiRuntime.Navigator);
            runtimeBindingDirty = false;
        }

        private void SubscribeToRuntimeSignals()
        {
            if (subscribedTimeController == timeController)
            {
                return;
            }

            if (subscribedTimeController != null)
            {
                subscribedTimeController.SegmentChanged -= HandleSegmentChanged;
            }

            subscribedTimeController = timeController;
            if (subscribedTimeController != null)
            {
                subscribedTimeController.SegmentChanged += HandleSegmentChanged;
            }
        }

        private void UnsubscribeFromRuntimeSignals()
        {
            if (subscribedTimeController != null)
            {
                subscribedTimeController.SegmentChanged -= HandleSegmentChanged;
                subscribedTimeController = null;
            }
        }

        private void HandleSegmentChanged(CampusTimeSegment previousSegment, CampusTimeSegment currentSegment)
        {
            aiRuntime.HandleSegmentChanged(currentSegment);
            mind.IntentHoldUntil = -1f;
        }

        private string BuildInteractiveLine(GameObject actor)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return CampusCharacterTextCatalog.GetDialogue(
                    CampusLanguageState.CurrentLanguage,
                    CampusCharacterDialogueId.Missing);
            }

            EnsureAiController();
            return aiController != null
                ? aiController.BuildInteractiveLine()
                : CampusCharacterTextCatalog.GetDialogue(
                    CampusLanguageState.CurrentLanguage,
                    CampusCharacterDialogueId.Missing);
        }

        private string ResolveAmbientLine()
        {
            CampusNpcIntent intent = mind.CurrentIntent;
            if (intent == null)
            {
                return string.Empty;
            }

            return ResolveIntentLine(intent.Kind);
        }

        private string ResolveIntentLine(CampusNpcIntentKind kind)
        {
            EnsureAiController();
            string controllerLine = aiController != null
                ? aiController.ResolveIntentLine(kind)
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(controllerLine))
            {
                return controllerLine;
            }

            return string.Empty;
        }

        private void Say(string line, float durationSeconds, bool writeToLog)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            interactionPresenter?.Speak(line, durationSeconds);

            if (writeToLog && bootstrap != null && bootstrap.EventLog != null && runtime != null && runtime.Data != null)
            {
                bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatNpcSpeechLog(
                    CampusLanguageState.CurrentLanguage,
                    runtime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage),
                    line.Trim()));
            }
        }

        private int ResolvePersonalSeed()
        {
            return motor != null ? motor.PersonalSeed : 1;
        }
    }
}
