using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcAgent : MonoBehaviour
    {
        private const string VisualRootName = "NpcVisual";
        private const string InteractionAnchorName = "NpcTalkAnchor";
        private const string LegacyInteractionTargetName = "NpcInteractionTarget";
        private const float ArrivalDistance = 0.14f;
        private const float DecisionIntervalSeconds = 0.75f;
        private const float DeliveryLeadSeconds = 28f;
        private const float AmbientSpeechMinDelaySeconds = 8f;
        private const float AmbientSpeechMaxDelaySeconds = 18f;

        private static readonly Dictionary<int, Sprite> GeneratedBodySprites = new Dictionary<int, Sprite>();

        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusGameplayEventHub eventHub;
        [SerializeField] private CampusCharacterBodyController bodyController;
        [SerializeField] private CampusGridNavigationAgent navigationAgent;
        [SerializeField] private CampusNpcInteractable interactable;
        [SerializeField] private CampusNpcSpeechBubble speechBubble;
        [SerializeField] private Transform speechAnchor;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField, Min(0.2f)] private float walkSpeed = 1.35f;
        [SerializeField] private CampusNpcPersonalProfile profile = new CampusNpcPersonalProfile();
        [SerializeField] private CampusNpcMindState mind = new CampusNpcMindState();
        [SerializeField] private float nextDecisionTime = -1f;
        [SerializeField] private float nextAmbientSpeechTime = -1f;
        [SerializeField] private int ambientSpeechSerial;
        [SerializeField] private int personalSeed;
        [SerializeField] private float personalSpeedMultiplier = 1f;

        private CampusTimeController subscribedTimeController;
        private CampusGameplayEventHub subscribedEventHub;

        public CampusCharacterRuntime Runtime => runtime;
        public CampusNpcPersonalProfile Profile => profile;
        public CampusNpcIntent ActiveIntent => mind.CurrentIntent;

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
            RebuildPersonalProfile();
            nextAmbientSpeechTime = -1f;
            EnsureAmbientSpeechScheduled(true);
            DecideNow();
        }

        public bool TryTalk(GameObject actor, out string spokenLine)
        {
            spokenLine = BuildInteractiveLine(actor);
            Say(spokenLine, 2.2f, true);
            return !string.IsNullOrWhiteSpace(spokenLine);
        }

        public bool TryExecuteAction(string actionId, string payload, CampusInteractionAnchor anchor = null)
        {
            string normalizedActionId = CampusInteractionActionIds.Normalize(actionId);
            if (string.IsNullOrWhiteSpace(normalizedActionId) ||
                CampusInteractionActionIds.Equals(normalizedActionId, CampusInteractionActionIds.NpcTalk))
            {
                return TryTalk(anchor != null ? anchor.gameObject : null, out _);
            }

            return false;
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
            SubscribeToEvents();
            RebuildPersonalProfile();
            EnsureAmbientSpeechScheduled(true);
            DecideNow();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            if (navigationAgent != null)
            {
                navigationAgent.ClearDestination();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            ResolveReferences();
            SubscribeToEvents();
            if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return;
            }

            EnsureActorStack();
            UpdateKnownRoom();
            UpdateDeliveryState();
            CompleteHeldIntentIfNeeded();
            CompletePickupIfArrived();

            if (Time.time >= nextDecisionTime)
            {
                DecideNow();
            }

            TickNavigation();
            UpdateAmbientSpeech();
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

            if (bootstrap != null)
            {
                worldService = worldService != null ? worldService : bootstrap.WorldService;
                rosterService = rosterService != null ? rosterService : bootstrap.RosterService;
                timeController = timeController != null ? timeController : bootstrap.TimeController;
                eventHub = eventHub != null ? eventHub : bootstrap.GameplayEventHub;
            }

            if (bodyController == null)
            {
                bodyController = GetComponent<CampusCharacterBodyController>();
            }

            if (navigationAgent == null)
            {
                navigationAgent = GetComponent<CampusGridNavigationAgent>();
            }
        }

        private void EnsureActorStack()
        {
            EnsureBodyController();
            EnsureNavigationAgent();
            EnsurePresentation();
            EnsureInteraction();
        }

        private void EnsureBodyController()
        {
            if (bodyController == null)
            {
                bodyController = GetComponent<CampusCharacterBodyController>();
            }

            if (bodyController == null)
            {
                bodyController = gameObject.AddComponent<CampusCharacterBodyController>();
            }

            bodyController.MoveSpeed = walkSpeed * ResolvePersonalSpeedMultiplier();
            bodyController.FloorIndex = ResolveRuntimeFloorIndex();
            bodyController.SetMovementEnabled(true);
            bodyController.EnsureSetup();
        }

        private void EnsureNavigationAgent()
        {
            if (navigationAgent == null)
            {
                navigationAgent = GetComponent<CampusGridNavigationAgent>();
            }

            if (navigationAgent == null)
            {
                navigationAgent = gameObject.AddComponent<CampusGridNavigationAgent>();
            }

            navigationAgent.Configure(
                walkSpeed * ResolvePersonalSpeedMultiplier(),
                ResolveRuntimeFloorIndex(),
                ResolvePersonalSeed(),
                0.8f,
                0.16f,
                0.9f,
                0.035f);
        }

        private void EnsurePresentation()
        {
            if (sortingGroup == null)
            {
                sortingGroup = GetComponent<SortingGroup>();
            }

            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
            }

            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (bodyRenderer == null)
            {
                Transform visualRoot = transform.Find(VisualRootName);
                if (visualRoot == null)
                {
                    GameObject visualObject = new GameObject(VisualRootName);
                    visualObject.transform.SetParent(transform, false);
                    visualRoot = visualObject.transform;
                }

                bodyRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            if (bodyRenderer.sprite == null)
            {
                bodyRenderer.sprite = GetGeneratedBodySprite(ResolveShirtColor());
            }
        }

        private void EnsureInteraction()
        {
            RemoveLegacyInteractionTarget();
            if (speechAnchor == null)
            {
                Transform existingAnchor = transform.Find(InteractionAnchorName);
                if (existingAnchor == null)
                {
                    GameObject anchorObject = new GameObject(InteractionAnchorName);
                    anchorObject.transform.SetParent(transform, false);
                    anchorObject.transform.localPosition = new Vector3(0f, 0.82f, 0f);
                    existingAnchor = anchorObject.transform;
                }

                speechAnchor = existingAnchor;
            }

            if (speechBubble == null)
            {
                speechBubble = GetComponent<CampusNpcSpeechBubble>();
            }

            if (speechBubble == null)
            {
                speechBubble = gameObject.AddComponent<CampusNpcSpeechBubble>();
            }

            speechBubble.Bind(speechAnchor);

            if (interactable == null)
            {
                interactable = GetComponent<CampusNpcInteractable>();
            }

            if (interactable == null)
            {
                interactable = gameObject.AddComponent<CampusNpcInteractable>();
            }

            interactable.Bind(this);

            CircleCollider2D collider = speechAnchor.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = speechAnchor.gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.radius = 0.82f;
            collider.offset = new Vector2(0f, -0.48f);

            CampusInteractionAnchor anchor = speechAnchor.GetComponent<CampusInteractionAnchor>();
            if (anchor == null)
            {
                anchor = speechAnchor.gameObject.AddComponent<CampusInteractionAnchor>();
            }

            anchor.InteractionTarget = interactable;
            anchor.ActionId = CampusInteractionActionIds.NpcTalk;
            anchor.Payload = string.Empty;
            anchor.PromptAnchor = speechAnchor;
            anchor.PromptText = "Talk";
            anchor.KeyOverride = string.Empty;
            anchor.Priority = 55;
            anchor.IsAvailable = true;
            anchor.HideWhenUnavailable = false;
            anchor.LogInteraction = false;
        }

        private void RemoveLegacyInteractionTarget()
        {
            Transform legacyTarget = transform.Find(LegacyInteractionTargetName);
            if (legacyTarget == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(legacyTarget.gameObject);
            }
            else
            {
                DestroyImmediate(legacyTarget.gameObject);
            }
        }

        private void SubscribeToEvents()
        {
            if (subscribedTimeController != timeController)
            {
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

            if (subscribedEventHub != eventHub)
            {
                if (subscribedEventHub != null)
                {
                    subscribedEventHub.PrankAttempted -= HandlePrankAttempted;
                    subscribedEventHub.PrankResolved -= HandlePrankResolved;
                    subscribedEventHub.ItemTheftObserved -= HandleItemTheftObserved;
                    subscribedEventHub.InventoryQuestioned -= HandleInventoryQuestioned;
                    subscribedEventHub.ContrabandFound -= HandleContrabandFound;
                }

                subscribedEventHub = eventHub;
                if (subscribedEventHub != null)
                {
                    subscribedEventHub.PrankAttempted += HandlePrankAttempted;
                    subscribedEventHub.PrankResolved += HandlePrankResolved;
                    subscribedEventHub.ItemTheftObserved += HandleItemTheftObserved;
                    subscribedEventHub.InventoryQuestioned += HandleInventoryQuestioned;
                    subscribedEventHub.ContrabandFound += HandleContrabandFound;
                }
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (subscribedTimeController != null)
            {
                subscribedTimeController.SegmentChanged -= HandleSegmentChanged;
                subscribedTimeController = null;
            }

            if (subscribedEventHub != null)
            {
                subscribedEventHub.PrankAttempted -= HandlePrankAttempted;
                subscribedEventHub.PrankResolved -= HandlePrankResolved;
                subscribedEventHub.ItemTheftObserved -= HandleItemTheftObserved;
                subscribedEventHub.InventoryQuestioned -= HandleInventoryQuestioned;
                subscribedEventHub.ContrabandFound -= HandleContrabandFound;
                subscribedEventHub = null;
            }
        }

        private void RebuildPersonalProfile()
        {
            profile = CampusNpcProfileBuilder.Build(runtime, worldService, rosterService);
            if (runtime != null && runtime.Data != null)
            {
                runtime.Data.SyncAssignmentsFromProfile(profile);
            }
        }

        private void HandleSegmentChanged(CampusTimeSegment previousSegment, CampusTimeSegment currentSegment)
        {
            if (runtime != null && runtime.Data != null && CampusNpcScheduleFacts.IsClassSession(currentSegment))
            {
                runtime.Data.SetState(CampusCharacterState.Normal);
            }

            RebuildPersonalProfile();
            mind.IntentHoldUntil = -1f;
            nextDecisionTime = 0f;
        }

        private void DecideNow()
        {
            nextDecisionTime = Time.time + DecisionIntervalSeconds;
            CampusNpcPlanDecision decision = CampusNpcIntentPlanner.Choose(BuildPlanningContext());
            if (decision.StartsDeliveryOrder)
            {
                mind.DeliveryState = CampusNpcDeliveryState.Ordering;
            }

            ApplyIntent(decision.Intent);
        }

        private CampusNpcPlanningContext BuildPlanningContext()
        {
            return new CampusNpcPlanningContext(
                runtime != null ? runtime.Data : null,
                profile,
                mind,
                ResolveCurrentSegment(),
                Time.time,
                ResolvePersonalSeed(),
                ResolveRoomTarget);
        }

        private void UpdateDeliveryState()
        {
            if (mind.DeliveryState == CampusNpcDeliveryState.Waiting &&
                mind.DeliveryReadyAt > 0f &&
                Time.time >= mind.DeliveryReadyAt)
            {
                mind.DeliveryState = CampusNpcDeliveryState.ReadyForPickup;
                Say("Delivery is here.", 1.8f, false);
                nextDecisionTime = 0f;
            }
        }

        private void CompleteHeldIntentIfNeeded()
        {
            if (mind.CurrentIntent == null ||
                mind.CurrentIntent.Kind != CampusNpcIntentKind.UsePhoneForDelivery ||
                Time.time < mind.IntentHoldUntil)
            {
                return;
            }

            mind.DeliveryState = CampusNpcDeliveryState.Waiting;
            mind.DeliveryReadyAt = Time.time + DeliveryLeadSeconds + PositiveModulo(ResolvePersonalSeed(), 7);
            mind.NextDeliveryOrderAllowedAt = Time.time + 480f;
            mind.IntentHoldUntil = -1f;
            Say("Order placed.", 1.8f, false);
            nextDecisionTime = 0f;
        }

        private void CompletePickupIfArrived()
        {
            if (mind.CurrentIntent == null ||
                mind.CurrentIntent.Kind != CampusNpcIntentKind.PickupDelivery ||
                mind.DeliveryState != CampusNpcDeliveryState.ReadyForPickup ||
                !HasArrivedAtCurrentIntent())
            {
                return;
            }

            mind.DeliveryState = CampusNpcDeliveryState.PickedUp;
            mind.DeliveryReadyAt = -1f;
            mind.NextDeliveryOrderAllowedAt = Time.time + 600f;
            Say("Got my delivery.", 1.8f, false);
            nextDecisionTime = 0f;
        }

        private void ApplyIntent(CampusNpcIntent nextIntent)
        {
            CampusNpcIntentRunner.Apply(
                mind,
                nextIntent,
                navigationAgent,
                bodyController,
                Time.time,
                kind => Say(ResolveIntentLine(kind), 1.8f, false));
        }

        private void TickNavigation()
        {
            CampusNpcIntentRunner.TickNavigation(
                mind,
                navigationAgent,
                bodyController,
                walkSpeed * ResolvePersonalSpeedMultiplier(),
                ResolveRuntimeFloorIndex(),
                ResolvePersonalSeed());
        }

        private bool HasArrivedAtCurrentIntent()
        {
            return CampusNpcIntentRunner.HasArrived(transform, mind.CurrentIntent, ArrivalDistance);
        }

        private Vector3 ResolveRoomTarget(CampusRoomType roomType, float radius)
        {
            CampusGameplayRoom room = worldService != null ? worldService.FindFirstUsableRoom(roomType) : null;
            if (room == null && worldService != null)
            {
                room = worldService.FindFirstRoom(roomType);
            }

            if (room == null)
            {
                return transform.position;
            }

            int seed = ResolvePersonalSeed();
            float angle = PositiveModulo(seed * 89, 360) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Mathf.Max(0f, radius);
            Vector3 target = room.WorldCenter + offset;
            target.z = transform.position.z;
            return target;
        }

        private void UpdateKnownRoom()
        {
            if (runtime == null || runtime.Data == null || worldService == null)
            {
                return;
            }

            CampusGameplayRoom room = worldService.FindRoomForRuntime(runtime);
            if (room != null)
            {
                runtime.Data.SetCurrentRoom(room.RoomId);
            }
        }

        private CampusTimeSegment ResolveCurrentSegment()
        {
            return timeController != null ? timeController.CurrentSegment : CampusTimeSegment.MorningClass1;
        }

        private int ResolveRuntimeFloorIndex()
        {
            if (runtime != null && CampusRuntimeGameplayOverlayLoader.TryGetManagedEntity(runtime, out CampusRuntimeGameplayOverlayEntity entity))
            {
                return entity.FloorIndex;
            }

            CampusSceneCharacterDefinition sceneCharacter = GetComponent<CampusSceneCharacterDefinition>();
            if (sceneCharacter != null)
            {
                return sceneCharacter.FloorIndex;
            }

            if (runtime != null && runtime.Data != null && worldService != null)
            {
                CampusGameplayRoom room = worldService.FindRoomById(runtime.Data.CurrentRoomId);
                if (room != null)
                {
                    return room.FloorIndex;
                }
            }

            return 1;
        }

        private void HandlePrankAttempted(CampusPrankAttemptedEvent eventData)
        {
            RememberRoomEvent(eventData.RoomId, 8f);
        }

        private void HandlePrankResolved(CampusPrankResolvedEvent eventData)
        {
            RememberRoomEvent(eventData.RoomId, 7f);
        }

        private void HandleItemTheftObserved(CampusItemTheftObservedEvent eventData)
        {
            RememberRoomEvent(eventData.RoomId, 9f);
        }

        private void HandleInventoryQuestioned(CampusInventoryQuestionedEvent eventData)
        {
            RememberRoomEvent(eventData.RoomId, 5f);
        }

        private void HandleContrabandFound(CampusContrabandFoundEvent eventData)
        {
            RememberRoomEvent(eventData.RoomId, 10f);
        }

        private void RememberRoomEvent(string roomId, float seconds)
        {
            if (worldService == null || string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            CampusGameplayRoom room = worldService.FindRoomById(roomId);
            if (room == null)
            {
                return;
            }

            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return;
            }

            bool sameRoom = string.Equals(data.CurrentRoomId, room.RoomId, StringComparison.OrdinalIgnoreCase);
            if (sameRoom || data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
            {
                mind.RememberFocus(room.RoomId, room.WorldCenter, seconds);
                nextDecisionTime = 0f;
            }
        }

        private void UpdateAmbientSpeech()
        {
            if (Time.time < nextAmbientSpeechTime || speechBubble == null || runtime == null || runtime.Data == null)
            {
                return;
            }

            ambientSpeechSerial++;
            bool shouldSpeak = ShouldSpeakAmbientLine(ambientSpeechSerial);
            ScheduleNextAmbientSpeech(false);
            if (!shouldSpeak)
            {
                return;
            }

            Say(ResolveAmbientLine(), 1.7f, false);
        }

        private void EnsureAmbientSpeechScheduled(bool initial)
        {
            if (nextAmbientSpeechTime > Time.time)
            {
                return;
            }

            ScheduleNextAmbientSpeech(initial);
        }

        private void ScheduleNextAmbientSpeech(bool initial)
        {
            int seed = ResolvePersonalSeed();
            int salt = initial ? 197 : 311 + ambientSpeechSerial * 37;
            float random01 = PseudoRandom01(seed, salt);
            float delay = initial
                ? Mathf.Lerp(3.5f, 13.5f, random01)
                : Mathf.Lerp(AmbientSpeechMinDelaySeconds, AmbientSpeechMaxDelaySeconds, random01);
            nextAmbientSpeechTime = Time.time + delay;
        }

        private bool ShouldSpeakAmbientLine(int serial)
        {
            int roll = Mathf.FloorToInt(PseudoRandom01(ResolvePersonalSeed(), 911 + serial * 53) * 100f);
            return roll < 18;
        }

        private string BuildInteractiveLine(GameObject actor)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return "...";
            }

            switch (data.Role)
            {
                case CampusCharacterRole.Teacher:
                    return CampusNpcScheduleFacts.IsClassSession(ResolveCurrentSegment())
                        ? "Class is in progress."
                        : "I am heading back to the office.";
                case CampusCharacterRole.Staff:
                    return ResolveStaffLine(data);
                default:
                    return ResolveStudentLine();
            }
        }

        private string ResolveStudentLine()
        {
            if (mind.DeliveryState == CampusNpcDeliveryState.ReadyForPickup)
            {
                return "My delivery arrived.";
            }

            if (CampusNpcScheduleFacts.IsClassSession(ResolveCurrentSegment()))
            {
                return "I need to get to my desk.";
            }

            return "I am deciding what to do next.";
        }

        private string ResolveStaffLine(CampusCharacterData data)
        {
            CampusStaffDuty duty = data != null ? data.StaffDuty : CampusStaffDuty.None;
            if ((duty & CampusStaffDuty.StoreOwner) != 0 || (duty & CampusStaffDuty.BookstoreOwner) != 0)
            {
                return CampusNpcScheduleFacts.IsStoreOpen(ResolveCurrentSegment())
                    ? "The register is open."
                    : "I am checking the shelves.";
            }

            if ((duty & CampusStaffDuty.DeliveryWatcher) != 0)
            {
                return "I am watching the delivery area.";
            }

            return CampusNpcScheduleFacts.IsMealPeak(ResolveCurrentSegment())
                ? "The counter is open."
                : "I am covering the windows.";
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

        private static string ResolveIntentLine(CampusNpcIntentKind kind)
        {
            switch (kind)
            {
                case CampusNpcIntentKind.AttendAssignedDesk:
                    return "Back to my desk.";
                case CampusNpcIntentKind.TeachAssignedClass:
                    return "Heading to class.";
                case CampusNpcIntentKind.ReturnToOfficeDesk:
                    return "Back to the office.";
                case CampusNpcIntentKind.WorkCanteenCounter:
                    return "Counter duty.";
                case CampusNpcIntentKind.CoverCanteenWindows:
                    return "Covering the windows.";
                case CampusNpcIntentKind.WorkStoreCheckout:
                    return "Register duty.";
                case CampusNpcIntentKind.AuditStoreShelves:
                    return "Checking shelves.";
                case CampusNpcIntentKind.WatchDeliveryPoint:
                    return "Watching deliveries.";
                case CampusNpcIntentKind.UsePhoneForDelivery:
                    return "Ordering delivery.";
                case CampusNpcIntentKind.PickupDelivery:
                    return "Picking up delivery.";
                case CampusNpcIntentKind.RestInDorm:
                    return "Back to the dorm.";
                case CampusNpcIntentKind.InvestigateEvent:
                    return "What happened?";
                case CampusNpcIntentKind.WatchEvent:
                    return "Something happened.";
                default:
                    return string.Empty;
            }
        }

        private void Say(string line, float durationSeconds, bool writeToLog)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (speechBubble != null)
            {
                speechBubble.Speak(line.Trim(), durationSeconds);
            }

            if (writeToLog && bootstrap != null && bootstrap.EventLog != null && runtime != null && runtime.Data != null)
            {
                bootstrap.EventLog.AddLog("[NPC] " + runtime.Data.DisplayName + ": " + line.Trim());
            }
        }

        private int ResolvePersonalSeed()
        {
            if (personalSeed == 0)
            {
                string id = runtime != null && runtime.Data != null ? runtime.Data.Id : name;
                personalSeed = Mathf.Max(1, Mathf.Abs(StableHash(id)));
            }

            return personalSeed;
        }

        private float ResolvePersonalSpeedMultiplier()
        {
            if (personalSpeedMultiplier <= 0f)
            {
                personalSpeedMultiplier = Mathf.Lerp(0.9f, 1.12f, PositiveModulo(ResolvePersonalSeed(), 100) / 99f);
            }

            return personalSpeedMultiplier;
        }

        private Color ResolveShirtColor()
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return new Color(0.48f, 0.62f, 0.84f, 1f);
            }

            switch (data.Role)
            {
                case CampusCharacterRole.Teacher:
                    return new Color(0.48f, 0.48f, 0.52f, 1f);
                case CampusCharacterRole.Staff:
                    return new Color(0.65f, 0.54f, 0.32f, 1f);
                default:
                    int seed = PositiveModulo(StableHash(data.Id), 3);
                    if (seed == 0)
                    {
                        return new Color(0.38f, 0.56f, 0.83f, 1f);
                    }

                    return seed == 1
                        ? new Color(0.62f, 0.42f, 0.66f, 1f)
                        : new Color(0.42f, 0.62f, 0.48f, 1f);
            }
        }

        private static Sprite GetGeneratedBodySprite(Color shirtColor)
        {
            int key = ColorToKey(shirtColor);
            if (GeneratedBodySprites.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            const int width = 24;
            const int height = 32;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            FillRect(pixels, width, 9, 24, 6, 5, new Color(0.98f, 0.82f, 0.62f, 1f));
            FillRect(pixels, width, 7, 12, 10, 12, shirtColor);
            FillRect(pixels, width, 8, 4, 3, 8, new Color(0.22f, 0.22f, 0.24f, 1f));
            FillRect(pixels, width, 13, 4, 3, 8, new Color(0.22f, 0.22f, 0.24f, 1f));
            FillRect(pixels, width, 5, 12, 2, 8, shirtColor * 0.9f);
            FillRect(pixels, width, 17, 12, 2, 8, shirtColor * 0.9f);

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.08f), 32f);
            sprite.name = "GeneratedNpcBody_" + key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            GeneratedBodySprites[key] = sprite;
            return sprite;
        }

        private static void FillRect(Color[] pixels, int textureWidth, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    int index = py * textureWidth + px;
                    if (index >= 0 && index < pixels.Length)
                    {
                        pixels[index] = color;
                    }
                }
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                string normalized = value ?? string.Empty;
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash = hash * 31 + char.ToUpperInvariant(normalized[i]);
                }

                return hash;
            }
        }

        private static int PositiveModulo(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int result = value % count;
            return result < 0 ? result + count : result;
        }

        private static float PseudoRandom01(int seed, int salt)
        {
            unchecked
            {
                int value = seed;
                value = (value * 397) ^ salt;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return PositiveModulo(value, 10000) / 9999f;
            }
        }

        private static int ColorToKey(Color color)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
            int a = Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f);
            return (r << 24) ^ (g << 16) ^ (b << 8) ^ a;
        }
    }
}
