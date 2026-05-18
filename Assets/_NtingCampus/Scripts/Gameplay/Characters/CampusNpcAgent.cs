using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CampusCharacterRuntime))]
    public sealed class CampusNpcAgent : MonoBehaviour
    {
        private const string VisualRootName = "NpcVisual";
        private const string InteractionAnchorName = "NpcTalkAnchor";
        private const string InteractionTargetName = "NpcInteractionTarget";
        private const float ArrivalDistance = 0.14f;
        private const float DisturbanceMemorySeconds = 10f;
        private const int MaxPathSearchIterations = 900;

        private static readonly Dictionary<int, Sprite> CachedBodySprites = new Dictionary<int, Sprite>();

        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusCommerceService commerceService;
        [SerializeField] private CampusNpcEcologyService ecologyService;
        [SerializeField] private CampusInspectionService inspectionService;
        [SerializeField] private CampusNpcInteractable interactable;
        [SerializeField] private CampusNpcSpeechBubble speechBubble;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private Transform speechAnchor;
        [SerializeField] private CampusCharacterBodyController bodyController;
        [SerializeField] private bool speechBubbleBound;
        [SerializeField, Min(0.2f)] private float walkSpeed = 1.35f;
        [SerializeField, Min(0.1f)] private float retargetIntervalSeconds = 0.16f;
        [SerializeField, Min(1f)] private float minAmbientSpeechSeconds = 4f;
        [SerializeField, Min(2f)] private float maxAmbientSpeechSeconds = 8f;
        [SerializeField, Min(0.2f)] private float patrolStride = 0.65f;
        [SerializeField, Min(0.2f)] private float doorInteractDistance = 0.92f;
        [SerializeField, Min(0f)] private float separationRadius = 0.86f;
        [SerializeField, Min(0f)] private float separationStrength = 1.15f;
        [SerializeField, Range(0f, 0.35f)] private float personalSpeedVariance = 0.16f;
        [SerializeField, Min(0.15f)] private float pathReplanIntervalSeconds = 0.55f;
        [SerializeField, Min(0f)] private float waypointArrivalDistance = 0.16f;
        [SerializeField, Min(0.2f)] private float npcCollisionRefreshSeconds = 0.9f;
        [SerializeField, Min(0.5f)] private float npcDoorCloseDelaySeconds = 2.4f;
        [SerializeField, Min(0.4f)] private float npcDoorClearanceRadius = 0.92f;
        [SerializeField, Min(1f)] private float freeRoamTargetRefreshSeconds = 4.5f;
        [SerializeField, Min(1f)] private float autonomousActionCheckSeconds = 6.5f;

        [SerializeField] private CampusCharacterTaskType currentTaskType;
        [SerializeField] private string currentTaskLabel = string.Empty;
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private Vector3 anchorPosition;
        [SerializeField] private Vector3 pathWaypointPosition;
        [SerializeField] private float nextRetargetTime;
        [SerializeField] private float nextPathReplanTime;
        [SerializeField] private float nextBehaviorDecisionTime;
        [SerializeField] private float pauseUntilTime;
        [SerializeField] private float nextNpcCollisionRefreshTime;
        [SerializeField] private float nextAmbientSpeechTime;
        [SerializeField] private float nextAutonomousActionTime;
        [SerializeField] private float latestRoomDisturbanceAt = -999f;
        [SerializeField] private int personalSeed;
        [SerializeField] private float personalSpeedMultiplier = 1f;
        [SerializeField] private bool isMoving;
        [SerializeField] private int pathCellIndex;
        [SerializeField] private Vector3Int lastPathStartCell;
        [SerializeField] private Vector3Int lastPathTargetCell;

        private static readonly Dictionary<string, string> ReservedFacilityOwnerByKey =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> ReservedFacilityKeyByOwner =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> ReservedStandOwnerByKey =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> ReservedStandKeyByOwner =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<CampusDoor3D, float> PendingDoorCloseTimeByDoor =
            new Dictionary<CampusDoor3D, float>();

        private static readonly Dictionary<RestroomStallDoor, float> PendingStallDoorCloseTimeByDoor =
            new Dictionary<RestroomStallDoor, float>();

        private static readonly List<CampusDoor3D> DoorCloseScratch = new List<CampusDoor3D>();
        private static readonly List<RestroomStallDoor> StallDoorCloseScratch = new List<RestroomStallDoor>();
        private static float nextSharedDoorMaintenanceTime;

        private CampusGameplayEventHub gameplayEventHub;
        private readonly List<Vector3Int> pathCells = new List<Vector3Int>();
        private string activeReservedFacilityKey = string.Empty;
        private string activeReservedStandKey = string.Empty;
        private bool subscribedToGameplayEvents;

        public CampusCharacterRuntime Runtime => runtime;

        public void Initialize(CampusCharacterRuntime targetRuntime, CampusGameBootstrap targetBootstrap, CampusWorldService targetWorldService)
        {
            runtime = targetRuntime != null ? targetRuntime : GetComponent<CampusCharacterRuntime>();
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            worldService = targetWorldService != null
                ? targetWorldService
                : bootstrap != null
                    ? bootstrap.WorldService
                    : null;
            scheduleService = bootstrap != null ? bootstrap.ScheduleService : null;
            commerceService = bootstrap != null ? bootstrap.CommerceService : null;
            ecologyService = bootstrap != null ? bootstrap.NpcEcologyService : null;
            inspectionService = bootstrap != null ? bootstrap.InspectionService : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;

            EnsurePresentation();
            EnsureBodyController();
            EnsureInteraction();
            EnsureGameplayEventSubscription();
            EnsurePersonalProfile();

            anchorPosition = transform.position;
            targetPosition = transform.position;
            pathWaypointPosition = transform.position;
            currentTaskType = CampusCharacterTaskType.Idle;
            currentTaskLabel = "Idle";
            nextRetargetTime = Time.time + ResolvePersonalDelay(0.1f, 0.45f, 13);
            nextPathReplanTime = 0f;
            nextBehaviorDecisionTime = Time.time + ResolvePersonalDelay(0.35f, 1.1f, 17);
            pauseUntilTime = 0f;
            nextNpcCollisionRefreshTime = 0f;
            nextAmbientSpeechTime = Time.time + UnityEngine.Random.Range(minAmbientSpeechSeconds, maxAmbientSpeechSeconds);
            nextAutonomousActionTime = Time.time + ResolvePersonalDelay(2.5f, autonomousActionCheckSeconds, 41);
            pathCells.Clear();
            pathCellIndex = 0;
            isMoving = false;
        }

        public bool TryTalk(GameObject actor, out string spokenLine)
        {
            spokenLine = BuildInteractiveLine(actor);
            if (string.IsNullOrWhiteSpace(spokenLine))
            {
                return false;
            }

            Say(spokenLine, 3.2f, true);
            if (runtime != null && runtime.Data != null)
            {
                runtime.Data.AddMemory(CampusCharacterMemoryId.TalkedToActor);
            }

            return true;
        }

        public bool TryExecuteAction(string actionId, string payload, CampusInteractionAnchor anchor = null)
        {
            Component target = anchor != null && anchor.InteractionTarget is Component targetComponent
                ? targetComponent
                : this;
            return CampusGameplayActionService.TryExecuteShared(new CampusGameplayActionRequest(
                gameObject,
                actionId,
                payload,
                anchor,
                target,
                "npc_agent"));
        }

        private void Awake()
        {
            runtime = runtime != null ? runtime : GetComponent<CampusCharacterRuntime>();
            Initialize(runtime, bootstrap, worldService);
        }

        private void OnDestroy()
        {
            ReleaseFacilityReservation();
            ReleaseGameplayEventSubscription();
        }

        private void Update()
        {
            if (!Application.isPlaying || runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return;
            }

            ResolveReferences();
            UpdateStateFromTaskContext();
            RefreshTaskIfNeeded();
            ApplyMovement();
            RefreshNpcCollisionIgnoresIfNeeded();
            UpdateNpcDoorHabit();
            UpdateAutonomousActions();
            UpdateAmbientSpeech();
        }

        private void LateUpdate()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            CampusRenderSortingUtility.ConfigureTopDownTransparencySort();
            int baseOrder = 1120 - Mathf.RoundToInt(transform.position.y * 100f);
            bodyRenderer.sortingOrder = baseOrder;
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = baseOrder;
            }
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (worldService == null && bootstrap != null)
            {
                worldService = bootstrap.WorldService;
            }

            if (scheduleService == null && bootstrap != null)
            {
                scheduleService = bootstrap.ScheduleService;
            }

            if (commerceService == null && bootstrap != null)
            {
                commerceService = bootstrap.CommerceService;
            }

            if (ecologyService == null && bootstrap != null)
            {
                ecologyService = bootstrap.NpcEcologyService;
            }

            if (inspectionService == null && bootstrap != null)
            {
                inspectionService = bootstrap.InspectionService;
            }

            if (gameplayEventHub == null && bootstrap != null)
            {
                gameplayEventHub = bootstrap.GameplayEventHub;
            }

            if (runtime == null)
            {
                runtime = GetComponent<CampusCharacterRuntime>();
            }

            EnsurePresentation();
            EnsureInteraction();
            EnsureBodyController();
            EnsureGameplayEventSubscription();
            EnsurePersonalProfile();
        }

        private void EnsurePersonalProfile()
        {
            if (runtime == null)
            {
                return;
            }

            if (personalSeed == 0)
            {
                string key = !string.IsNullOrWhiteSpace(runtime.CharacterId)
                    ? runtime.CharacterId
                    : gameObject.GetInstanceID().ToString();
                personalSeed = Mathf.Max(1, Mathf.Abs(StableHash(key)));
            }

            float speedNoise = PseudoRandom01(personalSeed, 31) * 2f - 1f;
            personalSpeedMultiplier = Mathf.Max(0.55f, 1f + speedNoise * personalSpeedVariance);
        }

        private void EnsurePresentation()
        {
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            if (sortingGroup == null)
            {
                sortingGroup = GetComponent<SortingGroup>();
                if (sortingGroup == null)
                {
                    sortingGroup = gameObject.AddComponent<SortingGroup>();
                }
            }

            Transform visualRoot = transform.Find(VisualRootName);
            if (visualRoot == null)
            {
                GameObject visualObject = new GameObject(VisualRootName);
                visualObject.transform.SetParent(transform, false);
                visualRoot = visualObject.transform;
            }

            if (bodyRenderer == null)
            {
                bodyRenderer = visualRoot.GetComponent<SpriteRenderer>();
                if (bodyRenderer == null)
                {
                    bodyRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            bodyRenderer.sprite = GetBodySprite(ResolveShirtColor());
            bodyRenderer.color = Color.white;
            bodyRenderer.drawMode = SpriteDrawMode.Simple;
            bodyRenderer.transform.localScale = runtime.Data.Role == CampusCharacterRole.Teacher
                ? new Vector3(0.92f, 0.92f, 1f)
                : new Vector3(0.84f, 0.84f, 1f);

            if (speechAnchor == null)
            {
                GameObject speechAnchorObject = new GameObject("SpeechAnchor");
                speechAnchorObject.transform.SetParent(transform, false);
                speechAnchorObject.transform.localPosition = new Vector3(0f, 0.82f, 0f);
                speechAnchor = speechAnchorObject.transform;
            }

            if (speechBubble == null)
            {
                speechBubble = GetComponent<CampusNpcSpeechBubble>();
                if (speechBubble == null)
                {
                    speechBubble = gameObject.AddComponent<CampusNpcSpeechBubble>();
                }
            }

            if (!speechBubbleBound)
            {
                speechBubble.Bind(speechAnchor);
                speechBubbleBound = true;
            }
        }

        private void EnsureInteraction()
        {
            if (interactable == null)
            {
                Transform interactionRoot = transform.Find(InteractionTargetName);
                if (interactionRoot == null)
                {
                    GameObject interactionObject = new GameObject(InteractionTargetName);
                    interactionObject.transform.SetParent(transform, false);
                    interactionRoot = interactionObject.transform;
                }

                interactable = interactionRoot.GetComponent<CampusNpcInteractable>();
                if (interactable == null)
                {
                    interactable = interactionRoot.gameObject.AddComponent<CampusNpcInteractable>();
                }
            }

            interactable.Bind(this);

            Transform anchorRoot = transform.Find(InteractionAnchorName);
            if (anchorRoot == null)
            {
                GameObject anchorObject = new GameObject(InteractionAnchorName);
                anchorObject.transform.SetParent(transform, false);
                anchorObject.transform.localPosition = Vector3.zero;
                anchorRoot = anchorObject.transform;
            }
            else
            {
                anchorRoot.localPosition = Vector3.zero;
            }

            CircleCollider2D trigger = anchorRoot.GetComponent<CircleCollider2D>();
            if (trigger == null)
            {
                trigger = anchorRoot.gameObject.AddComponent<CircleCollider2D>();
            }

            trigger.isTrigger = true;
            trigger.offset = new Vector2(0f, -0.02f);
            trigger.radius = 0.82f;

            CampusInteractionAnchor anchor = anchorRoot.GetComponent<CampusInteractionAnchor>();
            if (anchor == null)
            {
                anchor = anchorRoot.gameObject.AddComponent<CampusInteractionAnchor>();
            }

            anchor.InteractionTarget = interactable;
            anchor.ActionId = CampusInteractionActionIds.NpcTalk;
            anchor.PromptAnchor = speechAnchor != null ? speechAnchor : transform;
            anchor.PromptText = CampusCharacterTextCatalog.FormatTalkPrompt(
                CampusLanguageState.CurrentLanguage,
                ResolveDisplayName());
            anchor.Priority = 95;
            anchor.IsAvailable = true;
            anchor.HideWhenUnavailable = false;
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

            bodyController.MoveSpeed = walkSpeed;
            bodyController.FloorIndex = ResolveRuntimeFloorIndex();
            bodyController.EnsureSetup();
            bodyController.SetMovementEnabled(true);
        }

        private void EnsureGameplayEventSubscription()
        {
            if (subscribedToGameplayEvents || gameplayEventHub == null)
            {
                return;
            }

            gameplayEventHub.PrankAttempted += HandlePrankAttempted;
            gameplayEventHub.PrankResolved += HandlePrankResolved;
            gameplayEventHub.SanctionIssued += HandleSanctionIssued;
            gameplayEventHub.StudentDozedOff += HandleStudentDozedOff;
            gameplayEventHub.TeacherDistracted += HandleTeacherDistracted;
            gameplayEventHub.ActorSkipClass += HandleActorSkipClass;
            gameplayEventHub.ItemTransferred += HandleItemTransferred;
            gameplayEventHub.ItemTheftObserved += HandleItemTheftObserved;
            gameplayEventHub.InventoryQuestioned += HandleInventoryQuestioned;
            gameplayEventHub.ContrabandFound += HandleContrabandFound;
            subscribedToGameplayEvents = true;
        }

        private void ReleaseGameplayEventSubscription()
        {
            if (!subscribedToGameplayEvents || gameplayEventHub == null)
            {
                return;
            }

            gameplayEventHub.PrankAttempted -= HandlePrankAttempted;
            gameplayEventHub.PrankResolved -= HandlePrankResolved;
            gameplayEventHub.SanctionIssued -= HandleSanctionIssued;
            gameplayEventHub.StudentDozedOff -= HandleStudentDozedOff;
            gameplayEventHub.TeacherDistracted -= HandleTeacherDistracted;
            gameplayEventHub.ActorSkipClass -= HandleActorSkipClass;
            gameplayEventHub.ItemTransferred -= HandleItemTransferred;
            gameplayEventHub.ItemTheftObserved -= HandleItemTheftObserved;
            gameplayEventHub.InventoryQuestioned -= HandleInventoryQuestioned;
            gameplayEventHub.ContrabandFound -= HandleContrabandFound;
            subscribedToGameplayEvents = false;
        }

        private void UpdateStateFromTaskContext()
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return;
            }

            if (data.State == CampusCharacterState.Punished)
            {
                return;
            }

            if (data.State == CampusCharacterState.Sleeping && currentTaskType == CampusCharacterTaskType.DozeAtDesk)
            {
                return;
            }

            if (HasRecentDisturbance())
            {
                if (data.Role == CampusCharacterRole.Teacher)
                {
                    data.SetState(CampusCharacterState.Nervous);
                    return;
                }

                if (data.HasTrait(CampusCharacterTrait.Troublemaker))
                {
                    data.SetState(CampusCharacterState.Excited);
                    return;
                }

                data.SetState(CampusCharacterState.Nervous);
                return;
            }

            if (currentTaskType == CampusCharacterTaskType.DozeAtDesk || data.Sleepiness >= 75)
            {
                data.SetState(CampusCharacterState.Drowsy);
                return;
            }

            if (currentTaskType == CampusCharacterTaskType.Socialize)
            {
                data.SetState(CampusCharacterState.Excited);
                return;
            }

            data.SetState(CampusCharacterState.Normal);
        }

        private void RefreshTaskIfNeeded()
        {
            if (Time.time < nextRetargetTime)
            {
                return;
            }

            nextRetargetTime = Time.time + ResolvePersonalDelay(
                retargetIntervalSeconds * 0.75f,
                retargetIntervalSeconds * 1.65f,
                Mathf.FloorToInt(Time.time * 7f));
            CampusCharacterTaskDirective directive = BuildEffectiveDirective();
            CampusCharacterTaskType previousTaskType = currentTaskType;
            Vector3 previousTarget = targetPosition;
            currentTaskType = directive.TaskType;
            currentTaskLabel = directive.DebugLabel;
            if (!ShouldReserveFacilityTask(currentTaskType))
            {
                ReleaseFacilityReservation();
            }

            CampusGameplayRoom targetRoom = scheduleService != null
                ? scheduleService.ResolveBestRoom(runtime, directive)
                : ResolveCurrentRoom();
            anchorPosition = ResolveTaskAnchor(targetRoom, directive);
            targetPosition = ResolveTaskTarget(targetRoom, directive, anchorPosition);
            if (previousTaskType != currentTaskType || Vector2.Distance(previousTarget, targetPosition) > 0.18f)
            {
                nextPathReplanTime = 0f;
            }

            isMoving = Vector2.Distance(transform.position, targetPosition) > Mathf.Max(ArrivalDistance, directive.HoldRadius * 0.5f);
        }

        private CampusCharacterTaskDirective BuildEffectiveDirective()
        {
            CampusCharacterTaskDirective directive = scheduleService != null
                ? scheduleService.BuildDirective(runtime)
                : new CampusCharacterTaskDirective();
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (directive == null || data == null)
            {
                return directive ?? new CampusCharacterTaskDirective();
            }

            if (commerceService != null &&
                commerceService.TryBuildCommerceDirective(runtime, out CampusCharacterTaskDirective commerceDirective))
            {
                return commerceDirective;
            }

            CampusPrankService prankService = bootstrap != null ? bootstrap.PrankService : null;
            if (prankService != null &&
                prankService.TryBuildDeliveryOwnerDirective(runtime, out CampusCharacterTaskDirective deliveryDirective))
            {
                return deliveryDirective;
            }

            if (inspectionService != null &&
                inspectionService.TryBuildNpcInspectionDirective(runtime, out CampusCharacterTaskDirective inspectionDirective))
            {
                return inspectionDirective;
            }

            if (HasRecentDisturbance())
            {
                if (data.Role == CampusCharacterRole.Staff)
                {
                    return directive;
                }

                if (data.Role == CampusCharacterRole.Teacher)
                {
                    directive.TaskType = CampusCharacterTaskType.InvestigateDisturbance;
                    directive.TargetRoomType = CampusRoomType.Classroom;
                    directive.PreferredFacilityType = CampusFacilityType.Door;
                    directive.HoldRadius = 0.16f;
                    directive.DebugLabel = "Investigate";
                    return directive;
                }

                if (data.HasTrait(CampusCharacterTrait.Troublemaker))
                {
                    directive.TaskType = CampusCharacterTaskType.Socialize;
                    directive.TargetRoomType = CampusRoomType.Classroom;
                    directive.PreferredFacilityType = CampusFacilityType.StudentDesk;
                    directive.HoldRadius = 0.24f;
                    directive.DebugLabel = "WatchDrama";
                    return directive;
                }

                directive.TaskType = CampusCharacterTaskType.AvoidDisturbance;
                directive.TargetRoomType = CampusRoomType.Classroom;
                directive.PreferredFacilityType = CampusFacilityType.Door;
                directive.HoldRadius = 0.2f;
                directive.DebugLabel = "AvoidTrouble";
            }

            return directive;
        }

        private void ApplyMovement()
        {
            EnsureBodyController();
            bodyController.MoveSpeed = walkSpeed * personalSpeedMultiplier;
            bodyController.FloorIndex = ResolveRuntimeFloorIndex();
            UpdateIndividualPause();
            if (Time.time < pauseUntilTime)
            {
                isMoving = false;
                bodyController.SetMovementInput(Vector2.zero);
                return;
            }

            RefreshPathIfNeeded();
            AdvancePathWaypointIfNeeded();
            TryOpenBlockingDoor();

            Vector2 toFinalTarget = (Vector2)(targetPosition - transform.position);
            if (toFinalTarget.sqrMagnitude <= ArrivalDistance * ArrivalDistance)
            {
                isMoving = false;
                bodyController.SetMovementInput(Vector2.zero);
                return;
            }

            Vector2 desired = (Vector2)(pathWaypointPosition - transform.position);
            if (desired.sqrMagnitude <= 0.0001f)
            {
                desired = toFinalTarget;
            }

            Vector2 separation = ResolveSeparationVector();
            Vector2 input = desired.normalized + separation * separationStrength;
            if (input.sqrMagnitude <= 0.0001f)
            {
                input = desired.normalized;
            }

            isMoving = true;
            bodyController.SetMovementInput(input.normalized);
        }

        private void TryOpenBlockingDoor()
        {
            Vector2 currentPosition = transform.position;
            Vector2 toTarget = (Vector2)(pathWaypointPosition - transform.position);
            if (toTarget.sqrMagnitude <= 0.01f)
            {
                toTarget = (Vector2)(targetPosition - transform.position);
            }

            if (toTarget.sqrMagnitude <= 0.01f)
            {
                return;
            }

            Vector2 moveDirection = toTarget.normalized;
            if (TryFindClosedDoor(currentPosition, moveDirection, out CampusDoor3D door3D))
            {
                door3D.Open();
                ScheduleDoorClose(door3D);
                return;
            }

            if (TryFindClosedStallDoor(currentPosition, moveDirection, out RestroomStallDoor stallDoor))
            {
                stallDoor.Open();
                ScheduleDoorClose(stallDoor);
            }
        }

        private void RefreshNpcCollisionIgnoresIfNeeded()
        {
            if (Time.time < nextNpcCollisionRefreshTime)
            {
                return;
            }

            nextNpcCollisionRefreshTime = Time.time + ResolvePersonalDelay(
                npcCollisionRefreshSeconds * 0.75f,
                npcCollisionRefreshSeconds * 1.35f,
                89);

            RefreshNpcCollisionIgnores();
        }

        private void RefreshNpcCollisionIgnores()
        {
            if (!IsAiControlledCharacter(this) || bodyController == null || bodyController.SolidCollider == null)
            {
                return;
            }

            CapsuleCollider2D selfCollider = bodyController.SolidCollider;
            CampusNpcAgent[] agents = FindObjectsByType<CampusNpcAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < agents.Length; i++)
            {
                CampusNpcAgent other = agents[i];
                if (other == null || other == this || other.bodyController == null || other.bodyController.SolidCollider == null)
                {
                    continue;
                }

                bool ignoreCollision = IsAiControlledCharacter(other);
                Physics2D.IgnoreCollision(selfCollider, other.bodyController.SolidCollider, ignoreCollision);
            }
        }

        private static bool IsAiControlledCharacter(CampusNpcAgent agent)
        {
            return agent != null &&
                   agent.runtime != null &&
                   agent.runtime.Data != null &&
                   !agent.runtime.Data.IsPlayerControlled;
        }

        private void ScheduleDoorClose(CampusDoor3D door)
        {
            if (door == null)
            {
                return;
            }

            float closeAt = Time.time + npcDoorCloseDelaySeconds;
            if (PendingDoorCloseTimeByDoor.TryGetValue(door, out float existingCloseAt))
            {
                closeAt = Mathf.Max(closeAt, existingCloseAt);
            }

            PendingDoorCloseTimeByDoor[door] = closeAt;
        }

        private void ScheduleDoorClose(RestroomStallDoor door)
        {
            if (door == null)
            {
                return;
            }

            float closeAt = Time.time + npcDoorCloseDelaySeconds;
            if (PendingStallDoorCloseTimeByDoor.TryGetValue(door, out float existingCloseAt))
            {
                closeAt = Mathf.Max(closeAt, existingCloseAt);
            }

            PendingStallDoorCloseTimeByDoor[door] = closeAt;
        }

        private void UpdateNpcDoorHabit()
        {
            if (Time.time < nextSharedDoorMaintenanceTime)
            {
                return;
            }

            nextSharedDoorMaintenanceTime = Time.time + 0.25f;
            ProcessPendingDoorClosures();
            ProcessPendingStallDoorClosures();
        }

        private void ProcessPendingDoorClosures()
        {
            if (PendingDoorCloseTimeByDoor.Count == 0)
            {
                return;
            }

            DoorCloseScratch.Clear();
            foreach (KeyValuePair<CampusDoor3D, float> entry in PendingDoorCloseTimeByDoor)
            {
                DoorCloseScratch.Add(entry.Key);
            }

            for (int i = 0; i < DoorCloseScratch.Count; i++)
            {
                CampusDoor3D door = DoorCloseScratch[i];
                if (door == null || !door.IsOpen)
                {
                    PendingDoorCloseTimeByDoor.Remove(door);
                    continue;
                }

                if (!PendingDoorCloseTimeByDoor.TryGetValue(door, out float closeAt) || Time.time < closeAt)
                {
                    continue;
                }

                if (IsAnyCharacterNearDoor(door.transform.position))
                {
                    PendingDoorCloseTimeByDoor[door] = Time.time + 0.75f;
                    continue;
                }

                door.Close();
                PendingDoorCloseTimeByDoor.Remove(door);
            }

            DoorCloseScratch.Clear();
        }

        private void ProcessPendingStallDoorClosures()
        {
            if (PendingStallDoorCloseTimeByDoor.Count == 0)
            {
                return;
            }

            StallDoorCloseScratch.Clear();
            foreach (KeyValuePair<RestroomStallDoor, float> entry in PendingStallDoorCloseTimeByDoor)
            {
                StallDoorCloseScratch.Add(entry.Key);
            }

            for (int i = 0; i < StallDoorCloseScratch.Count; i++)
            {
                RestroomStallDoor door = StallDoorCloseScratch[i];
                if (door == null || !door.IsOpen)
                {
                    PendingStallDoorCloseTimeByDoor.Remove(door);
                    continue;
                }

                if (!PendingStallDoorCloseTimeByDoor.TryGetValue(door, out float closeAt) || Time.time < closeAt)
                {
                    continue;
                }

                if (IsAnyCharacterNearDoor(door.transform.position))
                {
                    PendingStallDoorCloseTimeByDoor[door] = Time.time + 0.75f;
                    continue;
                }

                door.Close();
                PendingStallDoorCloseTimeByDoor.Remove(door);
            }

            StallDoorCloseScratch.Clear();
        }

        private bool IsAnyCharacterNearDoor(Vector3 doorPosition)
        {
            float radiusSqr = npcDoorClearanceRadius * npcDoorClearanceRadius;
            CampusCharacterBodyController[] bodies = FindObjectsByType<CampusCharacterBodyController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
            {
                CampusCharacterBodyController body = bodies[i];
                if (body != null && ((Vector2)(body.transform.position - doorPosition)).sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindClosedDoor(Vector2 currentPosition, Vector2 moveDirection, out CampusDoor3D door3D)
        {
            door3D = null;
            CampusDoor3D[] doors = FindObjectsByType<CampusDoor3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < doors.Length; i++)
            {
                CampusDoor3D candidate = doors[i];
                if (candidate == null || candidate.IsOpen || candidate.DoorCollider == null || !candidate.DoorCollider.enabled)
                {
                    continue;
                }

                Vector2 doorPosition = candidate.transform.position;
                float distance = Vector2.Distance(currentPosition, doorPosition);
                if (distance > doorInteractDistance)
                {
                    continue;
                }

                Vector2 toDoor = (doorPosition - currentPosition).normalized;
                float facing = Vector2.Dot(moveDirection, toDoor);
                if (facing < 0.15f)
                {
                    continue;
                }

                float score = distance - facing * 0.25f;
                if (score < bestScore)
                {
                    bestScore = score;
                    door3D = candidate;
                }
            }

            return door3D != null;
        }

        private bool TryFindClosedStallDoor(Vector2 currentPosition, Vector2 moveDirection, out RestroomStallDoor stallDoor)
        {
            stallDoor = null;
            RestroomStallDoor[] doors = FindObjectsByType<RestroomStallDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < doors.Length; i++)
            {
                RestroomStallDoor candidate = doors[i];
                if (candidate == null || candidate.IsOpen || candidate.DoorCollider == null || !candidate.DoorCollider.enabled)
                {
                    continue;
                }

                Vector2 doorPosition = candidate.transform.position;
                float distance = Vector2.Distance(currentPosition, doorPosition);
                if (distance > doorInteractDistance)
                {
                    continue;
                }

                Vector2 toDoor = (doorPosition - currentPosition).normalized;
                float facing = Vector2.Dot(moveDirection, toDoor);
                if (facing < 0.15f)
                {
                    continue;
                }

                float score = distance - facing * 0.25f;
                if (score < bestScore)
                {
                    bestScore = score;
                    stallDoor = candidate;
                }
            }

            return stallDoor != null;
        }

        private void RefreshPathIfNeeded()
        {
            Vector3Int startCell = ResolveCurrentCell();
            Vector3Int targetCell = WorldToCell(targetPosition);
            bool targetChanged = targetCell != lastPathTargetCell;
            bool startMovedOffPath = pathCells.Count == 0 || !pathCells.Contains(startCell);
            if (!targetChanged && !startMovedOffPath && Time.time < nextPathReplanTime)
            {
                return;
            }

            nextPathReplanTime = Time.time + ResolvePersonalDelay(
                pathReplanIntervalSeconds * 0.75f,
                pathReplanIntervalSeconds * 1.45f,
                Mathf.FloorToInt(Time.time * 5f));
            lastPathStartCell = startCell;
            lastPathTargetCell = targetCell;
            pathCells.Clear();
            pathCellIndex = 0;

            if (TryBuildPath(startCell, targetCell, pathCells))
            {
                pathCellIndex = Mathf.Min(1, pathCells.Count - 1);
                pathWaypointPosition = pathCells.Count > 0 ? CellCenterToWorld(pathCells[pathCellIndex]) : targetPosition;
                return;
            }

            pathWaypointPosition = targetPosition;
        }

        private void AdvancePathWaypointIfNeeded()
        {
            if (pathCells.Count == 0)
            {
                pathWaypointPosition = targetPosition;
                return;
            }

            while (pathCellIndex < pathCells.Count - 1 &&
                   Vector2.Distance(transform.position, CellCenterToWorld(pathCells[pathCellIndex])) <= waypointArrivalDistance)
            {
                pathCellIndex++;
            }

            pathWaypointPosition = pathCellIndex >= 0 && pathCellIndex < pathCells.Count
                ? CellCenterToWorld(pathCells[pathCellIndex])
                : targetPosition;
        }

        private bool TryBuildPath(Vector3Int startCell, Vector3Int targetCell, List<Vector3Int> output)
        {
            output.Clear();
            if (startCell == targetCell)
            {
                output.Add(startCell);
                return true;
            }

            CampusFloorRoot floor = ResolveCurrentFloor();
            if (floor == null)
            {
                return false;
            }

            targetCell = ResolveNearestWalkableCell(floor, targetCell, startCell);
            int minX = Mathf.Min(startCell.x, targetCell.x) - 18;
            int maxX = Mathf.Max(startCell.x, targetCell.x) + 18;
            int minY = Mathf.Min(startCell.y, targetCell.y) - 18;
            int maxY = Mathf.Max(startCell.y, targetCell.y) + 18;

            floor.RefreshUsedBoundsIfDirty();
            if (floor.UsedBounds.size.x > 0 && floor.UsedBounds.size.y > 0)
            {
                minX = Mathf.Max(minX, floor.UsedBounds.xMin - 2);
                maxX = Mathf.Min(maxX, floor.UsedBounds.xMax + 2);
                minY = Mathf.Max(minY, floor.UsedBounds.yMin - 2);
                maxY = Mathf.Min(maxY, floor.UsedBounds.yMax + 2);
            }

            Dictionary<Vector3Int, PathNode> nodes = new Dictionary<Vector3Int, PathNode>();
            List<PathNode> open = new List<PathNode>();
            HashSet<Vector3Int> closed = new HashSet<Vector3Int>();
            PathNode start = new PathNode(startCell, 0f, Heuristic(startCell, targetCell), null);
            nodes[startCell] = start;
            open.Add(start);

            int iterations = 0;
            while (open.Count > 0 && iterations++ < MaxPathSearchIterations)
            {
                int bestIndex = 0;
                float bestScore = open[0].TotalCost;
                for (int i = 1; i < open.Count; i++)
                {
                    if (open[i].TotalCost < bestScore)
                    {
                        bestScore = open[i].TotalCost;
                        bestIndex = i;
                    }
                }

                PathNode current = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (!closed.Add(current.Cell))
                {
                    continue;
                }

                if (current.Cell == targetCell)
                {
                    ReconstructPath(current, output);
                    return output.Count > 0;
                }

                AddPathNeighbor(floor, current, new Vector3Int(current.Cell.x + 1, current.Cell.y, 0), targetCell, minX, maxX, minY, maxY, nodes, open, closed);
                AddPathNeighbor(floor, current, new Vector3Int(current.Cell.x - 1, current.Cell.y, 0), targetCell, minX, maxX, minY, maxY, nodes, open, closed);
                AddPathNeighbor(floor, current, new Vector3Int(current.Cell.x, current.Cell.y + 1, 0), targetCell, minX, maxX, minY, maxY, nodes, open, closed);
                AddPathNeighbor(floor, current, new Vector3Int(current.Cell.x, current.Cell.y - 1, 0), targetCell, minX, maxX, minY, maxY, nodes, open, closed);
            }

            return false;
        }

        private void AddPathNeighbor(
            CampusFloorRoot floor,
            PathNode current,
            Vector3Int neighbor,
            Vector3Int targetCell,
            int minX,
            int maxX,
            int minY,
            int maxY,
            Dictionary<Vector3Int, PathNode> nodes,
            List<PathNode> open,
            HashSet<Vector3Int> closed)
        {
            if (neighbor.x < minX || neighbor.x > maxX || neighbor.y < minY || neighbor.y > maxY || closed.Contains(neighbor))
            {
                return;
            }

            if (neighbor != targetCell && !IsWalkableCell(floor, neighbor))
            {
                return;
            }

            float movementCost = current.CostFromStart +
                                 1f +
                                 PersonalCellCost(neighbor) +
                                 DynamicOccupancyCost(neighbor, targetCell);
            if (nodes.TryGetValue(neighbor, out PathNode existing))
            {
                if (movementCost >= existing.CostFromStart)
                {
                    return;
                }

                existing.CostFromStart = movementCost;
                existing.Parent = current;
                return;
            }

            PathNode node = new PathNode(neighbor, movementCost, Heuristic(neighbor, targetCell), current);
            nodes[neighbor] = node;
            open.Add(node);
        }

        private float PersonalCellCost(Vector3Int cell)
        {
            return PseudoRandom01(personalSeed + cell.x * 193 + cell.y * 389, 71) * 0.18f;
        }

        private float DynamicOccupancyCost(Vector3Int cell, Vector3Int targetCell)
        {
            float cost = 0f;
            string ownerId = ResolveReservationOwnerId();
            string targetStandKey = BuildStandReservationKey(cell);
            if (ReservedStandOwnerByKey.TryGetValue(targetStandKey, out string standOwner) &&
                !string.Equals(standOwner, ownerId, StringComparison.OrdinalIgnoreCase))
            {
                cost += cell == targetCell ? 18f : 5f;
            }

            CampusNpcAgent[] agents = FindObjectsByType<CampusNpcAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < agents.Length; i++)
            {
                CampusNpcAgent other = agents[i];
                if (other == null || other == this || other.runtime == null || other.runtime.Data == null || other.runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                Vector3Int otherCell = other.ResolveCurrentCell();
                if (otherCell == cell)
                {
                    cost += cell == targetCell ? 12f : 4.5f;
                }

                Vector3Int otherWaypointCell = WorldToCell(other.pathWaypointPosition);
                if (otherWaypointCell == cell)
                {
                    cost += 1.8f;
                }
            }

            return cost;
        }

        private static float Heuristic(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static void ReconstructPath(PathNode endNode, List<Vector3Int> output)
        {
            output.Clear();
            PathNode current = endNode;
            while (current != null)
            {
                output.Add(current.Cell);
                current = current.Parent;
            }

            output.Reverse();
        }

        private Vector3Int ResolveNearestWalkableCell(CampusFloorRoot floor, Vector3Int preferredCell, Vector3Int fromCell)
        {
            preferredCell.z = 0;
            if (IsWalkableCell(floor, preferredCell))
            {
                return preferredCell;
            }

            Vector3Int best = preferredCell;
            float bestScore = float.PositiveInfinity;
            for (int radius = 1; radius <= 5; radius++)
            {
                for (int y = preferredCell.y - radius; y <= preferredCell.y + radius; y++)
                {
                    for (int x = preferredCell.x - radius; x <= preferredCell.x + radius; x++)
                    {
                        if (Mathf.Abs(x - preferredCell.x) != radius && Mathf.Abs(y - preferredCell.y) != radius)
                        {
                            continue;
                        }

                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (!IsWalkableCell(floor, cell))
                        {
                            continue;
                        }

                        float score = Mathf.Abs(x - fromCell.x) + Mathf.Abs(y - fromCell.y);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = cell;
                        }
                    }
                }

                if (bestScore < float.PositiveInfinity)
                {
                    return best;
                }
            }

            return preferredCell;
        }

        private bool IsWalkableCell(CampusFloorRoot floor, Vector3Int cell)
        {
            cell.z = 0;
            if (floor == null)
            {
                return false;
            }

            if (floor.FloorTilemap != null && !floor.FloorTilemap.HasTile(cell))
            {
                return false;
            }

            if (CampusWallTileUtility.HasWall(CampusWallTileUtility.GetWallLogicTilemap(floor), cell))
            {
                return false;
            }

            return !IsBlockedByPlacedObject(floor, cell);
        }

        private bool IsBlockedByPlacedObject(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.PropsRoot == null)
            {
                return false;
            }

            CampusPlacedObject[] placedObjects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int i = 0; i < placedObjects.Length; i++)
            {
                CampusPlacedObject placedObject = placedObjects[i];
                if (placedObject == null || !placedObject.BlocksMovement || !placedObject.ContainsCell(cell))
                {
                    continue;
                }

                if (placedObject.GetComponent<CampusDoor3D>() != null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private CampusFloorRoot ResolveCurrentFloor()
        {
            CampusMapRoot mapRoot = FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            return mapRoot != null ? mapRoot.GetFloor(ResolveRuntimeFloorIndex()) : null;
        }

        private Vector2 ResolveSeparationVector()
        {
            if (separationRadius <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 currentPosition = transform.position;
            Vector2 separation = Vector2.zero;
            CampusNpcAgent[] agents = FindObjectsByType<CampusNpcAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < agents.Length; i++)
            {
                CampusNpcAgent other = agents[i];
                if (other == null || other == this || other.runtime == null || other.runtime.Data == null || other.runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                Vector2 delta = currentPosition - (Vector2)other.transform.position;
                float distance = delta.magnitude;
                if (distance <= 0.001f || distance > separationRadius)
                {
                    continue;
                }

                separation += delta.normalized * ((separationRadius - distance) / separationRadius);
            }

            return separation.sqrMagnitude > 1f ? separation.normalized : separation;
        }

        private void UpdateIndividualPause()
        {
            if (Time.time < nextBehaviorDecisionTime)
            {
                return;
            }

            nextBehaviorDecisionTime = Time.time + ResolvePersonalDelay(0.45f, 1.1f, Mathf.FloorToInt(Time.time * 11f));
            float chance = IsStrictScheduleTask(currentTaskType) ? 0.008f : 0.045f;
            if (PseudoRandom01(personalSeed + Mathf.FloorToInt(Time.time * 19f), 97) > chance)
            {
                return;
            }

            float pauseDuration = IsStrictScheduleTask(currentTaskType)
                ? ResolvePersonalDelay(0.05f, 0.14f, 101)
                : ResolvePersonalDelay(0.1f, 0.32f, 103);
            pauseUntilTime = Mathf.Max(pauseUntilTime, Time.time + pauseDuration);
        }

        private void UpdateAutonomousActions()
        {
            if (Time.time < nextAutonomousActionTime)
            {
                return;
            }

            nextAutonomousActionTime = Time.time + ResolvePersonalDelay(
                autonomousActionCheckSeconds * 0.75f,
                autonomousActionCheckSeconds * 1.45f,
                211);

            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null ||
                data.IsPlayerControlled ||
                data.State == CampusCharacterState.Punished ||
                data.State == CampusCharacterState.Sleeping)
            {
                return;
            }

            if (Time.time < pauseUntilTime ||
                Vector2.Distance(transform.position, targetPosition) > Mathf.Max(ArrivalDistance * 2f, 0.32f))
            {
                return;
            }

            if (TryHandleAutonomousInspection(data))
            {
                return;
            }

            if (data.Role != CampusCharacterRole.Student)
            {
                return;
            }

            if (!ShouldConsiderAutonomousPrank(data))
            {
                return;
            }

            CampusGameplayRoom room = ResolveCurrentRoom();
            if (!TryChooseAutonomousPrankPayload(data, room, out string payload))
            {
                return;
            }

            CampusPrankService prankService = bootstrap != null ? bootstrap.PrankService : null;
            if (prankService == null || !prankService.CanExecutePayload(payload, gameObject, out _))
            {
                return;
            }

            if (!TryExecuteAction(CampusInteractionActionIds.PrankExecute, payload))
            {
                return;
            }

            pauseUntilTime = Mathf.Max(pauseUntilTime, Time.time + ResolvePersonalDelay(0.45f, 1.1f, 223));
            nextAutonomousActionTime = Time.time + ResolvePersonalDelay(18f, 34f, 227);
            string line = BuildAutonomousPrankLine(payload);
            if (!string.IsNullOrWhiteSpace(line))
            {
                Say(line, 2.4f, false);
            }
        }

        private bool TryHandleAutonomousInspection(CampusCharacterData data)
        {
            if (data == null || inspectionService == null)
            {
                return false;
            }

            if (!inspectionService.TryNpcProactiveInspection(runtime, out string line))
            {
                return false;
            }

            pauseUntilTime = Mathf.Max(pauseUntilTime, Time.time + ResolvePersonalDelay(0.45f, 1.2f, 219));
            nextAutonomousActionTime = Time.time + ResolvePersonalDelay(12f, 28f, 221);
            if (!string.IsNullOrWhiteSpace(line))
            {
                Say(line, 2.4f, false);
            }

            return true;
        }

        private bool ShouldConsiderAutonomousPrank(CampusCharacterData data)
        {
            if (data == null)
            {
                return false;
            }

            int impulse = data.Mischief;
            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                impulse += 34;
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                impulse -= 18;
            }

            if (data.HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                impulse -= 8;
            }

            if (data.Mood <= 25)
            {
                impulse += 8;
            }

            if (data.SocialEnergy <= 20)
            {
                impulse -= 10;
            }

            float chance = Mathf.Clamp01(0.01f + Mathf.Clamp(impulse, 0, 140) / 100f * 0.085f);
            if (IsStrictScheduleTask(currentTaskType) && !data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                chance *= 0.35f;
            }

            int tick = Mathf.FloorToInt(Time.time / Mathf.Max(1f, autonomousActionCheckSeconds));
            return PseudoRandom01(personalSeed + tick * 17, 229) <= chance;
        }

        private bool TryChooseAutonomousPrankPayload(
            CampusCharacterData data,
            CampusGameplayRoom room,
            out string payload)
        {
            payload = string.Empty;
            if (data == null || room == null)
            {
                return false;
            }

            bool boldEnough = data.HasTrait(CampusCharacterTrait.Troublemaker) || data.Mischief >= 52;
            if (!boldEnough)
            {
                return false;
            }

            if (room.RoomType == CampusRoomType.Classroom &&
                scheduleService != null &&
                scheduleService.IsClassSessionNow())
            {
                payload = CampusPrankPayloadIds.PassNote;
                return true;
            }

            if (room.RoomType == CampusRoomType.Canteen)
            {
                payload = ChooseAutonomousCanteenPayload();
                return true;
            }

            if (room.RoomType == CampusRoomType.Outdoor)
            {
                payload = CampusPrankPayloadIds.StealDelivery;
                return true;
            }

            return false;
        }

        private string ChooseAutonomousCanteenPayload()
        {
            float roll = PseudoRandom01(personalSeed + Mathf.FloorToInt(Time.time * 3f), 233);
            if (roll < 0.34f)
            {
                return CampusPrankPayloadIds.StealFriedChicken;
            }

            if (roll < 0.67f)
            {
                return CampusPrankPayloadIds.StealBurger;
            }

            return CampusPrankPayloadIds.StealOden;
        }

        private static string BuildAutonomousPrankLine(string payload)
        {
            if (string.Equals(payload, CampusPrankPayloadIds.PassNote, StringComparison.OrdinalIgnoreCase))
            {
                return "Pass this along.";
            }

            if (string.Equals(payload, CampusPrankPayloadIds.StealDelivery, StringComparison.OrdinalIgnoreCase))
            {
                return "Nobody saw that.";
            }

            return "Just borrowing this.";
        }

        private void UpdateAmbientSpeech()
        {
            if (Time.time < nextAmbientSpeechTime)
            {
                return;
            }

            nextAmbientSpeechTime = Time.time + UnityEngine.Random.Range(minAmbientSpeechSeconds, maxAmbientSpeechSeconds);
            string ambientLine = BuildAmbientLine();
            if (!string.IsNullOrWhiteSpace(ambientLine))
            {
                Say(ambientLine, 2.2f, false);
            }
        }

        private CampusGameplayRoom ResolveCurrentRoom()
        {
            if (runtime == null || runtime.Data == null || worldService == null)
            {
                return null;
            }

            CampusGameplayRoom room = worldService.FindRoomForPosition(ResolveRuntimeFloorIndex(), transform.position);
            return room ?? worldService.FindRoomById(runtime.Data.CurrentRoomId);
        }

        private Vector3 ResolveTaskAnchor(CampusGameplayRoom room, CampusCharacterTaskDirective directive)
        {
            Vector3 fallback = ResolveRoomFallbackAnchor(room, directive);
            if (directive == null)
            {
                return fallback;
            }

            switch (directive.TaskType)
            {
                case CampusCharacterTaskType.AttendClass:
                case CampusCharacterTaskType.DozeAtDesk:
                    return ResolveFacilityPosition(room, CampusFacilityType.StudentDesk, fallback);
                case CampusCharacterTaskType.TeachClass:
                    return ResolveFacilityPosition(room, CampusFacilityType.Podium, ResolveFacilityPosition(room, CampusFacilityType.Blackboard, fallback));
                case CampusCharacterTaskType.UseOfficeDesk:
                    return ResolveFacilityPosition(room, CampusFacilityType.OfficeDesk, fallback);
                case CampusCharacterTaskType.RestInDorm:
                    return ResolveFacilityPosition(room, CampusFacilityType.Bed, fallback);
                case CampusCharacterTaskType.CheckBulletinBoard:
                    return ResolveFacilityPosition(room, CampusFacilityType.BulletinBoard, fallback);
                case CampusCharacterTaskType.WorkCanteenCounter:
                    return ResolveFacilityPosition(room, CampusFacilityType.CanteenCounter, fallback);
                case CampusCharacterTaskType.QueueCanteenMeal:
                    return ResolveFacilityPosition(room, CampusFacilityType.CanteenQueuePoint, fallback);
                case CampusCharacterTaskType.ReceiveCanteenMeal:
                    return fallback;
                case CampusCharacterTaskType.BrowseStoreShelf:
                    return ResolveFacilityPosition(room, CampusFacilityType.StoreShelf, fallback);
                case CampusCharacterTaskType.QueueStoreCheckout:
                    return ResolveFacilityPosition(room, CampusFacilityType.StoreQueuePoint, fallback);
                case CampusCharacterTaskType.PayStoreCheckout:
                    return fallback;
                case CampusCharacterTaskType.WorkStoreCheckout:
                    return ResolveFacilityPosition(room, CampusFacilityType.StoreCheckout, fallback);
                case CampusCharacterTaskType.WatchDeliveryPoint:
                case CampusCharacterTaskType.PickupDelivery:
                case CampusCharacterTaskType.SearchMissingDelivery:
                    return ResolveFacilityPosition(room, CampusFacilityType.DeliveryDropPoint, fallback);
                case CampusCharacterTaskType.ReportMissingDelivery:
                    return ResolveFacilityPosition(room, CampusFacilityType.OfficeDesk, fallback);
                case CampusCharacterTaskType.PatrolHallway:
                case CampusCharacterTaskType.InvestigateDisturbance:
                case CampusCharacterTaskType.AvoidDisturbance:
                    return ResolveFacilityPosition(room, CampusFacilityType.Door, fallback);
                case CampusCharacterTaskType.Socialize:
                case CampusCharacterTaskType.WanderCommonArea:
                    return fallback;
                default:
                    return fallback;
            }
        }

        private Vector3 ResolveTaskTarget(CampusGameplayRoom room, CampusCharacterTaskDirective directive, Vector3 anchor)
        {
            if (TryResolveRoomEntryTarget(room, directive, out Vector3 entryTarget))
            {
                return entryTarget;
            }

            CampusCharacterRuntime player = bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
            int seed = !string.IsNullOrWhiteSpace(runtime != null ? runtime.CharacterId : string.Empty)
                ? Mathf.Abs(runtime.CharacterId.GetHashCode())
                : GetInstanceID();

            switch (directive.TaskType)
            {
                case CampusCharacterTaskType.QueueCanteenMeal:
                case CampusCharacterTaskType.ReceiveCanteenMeal:
                case CampusCharacterTaskType.BrowseStoreShelf:
                case CampusCharacterTaskType.QueueStoreCheckout:
                case CampusCharacterTaskType.PayStoreCheckout:
                case CampusCharacterTaskType.WorkStoreCheckout:
                    if (commerceService != null &&
                        commerceService.TryResolveCommerceTaskTarget(runtime, directive.TaskType, room, anchor, out Vector3 commerceTarget))
                    {
                        return ClampToRoom(room, commerceTarget);
                    }

                    return ClampToRoom(room, anchor);
                case CampusCharacterTaskType.AttendClass:
                case CampusCharacterTaskType.DozeAtDesk:
                case CampusCharacterTaskType.TeachClass:
                case CampusCharacterTaskType.UseOfficeDesk:
                case CampusCharacterTaskType.RestInDorm:
                case CampusCharacterTaskType.CheckBulletinBoard:
                case CampusCharacterTaskType.PickupDelivery:
                case CampusCharacterTaskType.SearchMissingDelivery:
                case CampusCharacterTaskType.ReportMissingDelivery:
                    return ClampToRoom(room, anchor);
                case CampusCharacterTaskType.PatrolHallway:
                    if (TryResolvePersonalRoamTarget(room, seed + 31, Mathf.FloorToInt(Time.time / freeRoamTargetRefreshSeconds), out Vector3 patrolTarget))
                    {
                        return patrolTarget;
                    }

                    return ClampToRoom(room, anchor + ResolveLoopOffset(seed, patrolStride));
                case CampusCharacterTaskType.WanderCommonArea:
                    if (TryResolvePersonalRoamTarget(room, seed, Mathf.FloorToInt(Time.time / freeRoamTargetRefreshSeconds), out Vector3 wanderTarget))
                    {
                        return wanderTarget;
                    }

                    return ClampToRoom(room, anchor + ResolveLoopOffset(seed + Mathf.FloorToInt(Time.time * 0.2f), patrolStride * 2.2f));
                case CampusCharacterTaskType.Socialize:
                    if (player != null && SameRoom(player, room != null ? room.RoomId : string.Empty))
                    {
                        return ClampToRoom(room, ResolveOffsetFromPlayer(player, 0.9f));
                    }

                    if (TryResolvePersonalRoamTarget(room, seed + 17, Mathf.FloorToInt(Time.time / (freeRoamTargetRefreshSeconds * 1.4f)), out Vector3 socialTarget))
                    {
                        return socialTarget;
                    }

                    return ClampToRoom(room, anchor + ResolveLoopOffset(seed + 17, patrolStride * 1.8f));
                case CampusCharacterTaskType.InvestigateDisturbance:
                    if (player != null && SameRoom(player, room != null ? room.RoomId : string.Empty))
                    {
                        return ClampToRoom(room, ResolveOffsetFromPlayer(player, 0.65f));
                    }

                    return ClampToRoom(room, anchor);
                case CampusCharacterTaskType.AvoidDisturbance:
                    if (player != null && SameRoom(player, room != null ? room.RoomId : string.Empty))
                    {
                        return ClampToRoom(room, ResolveOppositeFromPlayer(player));
                    }

                    return ClampToRoom(room, anchor + ResolveLoopOffset(seed + 23, 0.52f));
                default:
                    return ClampToRoom(room, anchor);
            }
        }

        private static Vector3 ResolveLoopOffset(int seed, float radius)
        {
            float angle = (seed % 360) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.65f, 0f);
        }

        private Vector3 ResolveRoomFallbackAnchor(CampusGameplayRoom room, CampusCharacterTaskDirective directive)
        {
            if (room == null)
            {
                return transform.position;
            }

            int seed = !string.IsNullOrWhiteSpace(runtime != null ? runtime.CharacterId : string.Empty)
                ? Mathf.Abs(runtime.CharacterId.GetHashCode())
                : GetInstanceID();
            CampusCharacterTaskType taskType = directive != null ? directive.TaskType : CampusCharacterTaskType.Idle;

            switch (taskType)
            {
                case CampusCharacterTaskType.PatrolHallway:
                case CampusCharacterTaskType.InvestigateDisturbance:
                case CampusCharacterTaskType.AvoidDisturbance:
                    if (TryFindNearestStandableBoundaryCell(room, transform.position, out Vector3Int boundaryCell))
                    {
                        return CellCenterToWorld(boundaryCell);
                    }

                    break;
                case CampusCharacterTaskType.Socialize:
                case CampusCharacterTaskType.WanderCommonArea:
                    if (TryResolvePersonalRoamTarget(
                        room,
                        seed + 53,
                        Mathf.FloorToInt(Time.time / Mathf.Max(1f, freeRoamTargetRefreshSeconds)),
                        out Vector3 roamTarget))
                    {
                        return roamTarget;
                    }

                    break;
            }

            if (TryResolvePersonalRoamTarget(room, seed + 7, 0, out Vector3 personalTarget))
            {
                return personalTarget;
            }

            if (TryFindNearestStandableRoomCell(room, WorldToCell(transform.position), out Vector3Int nearestCell))
            {
                return CellCenterToWorld(nearestCell);
            }

            return ClampToRoom(room, room.WorldCenter);
        }

        private bool TryResolvePersonalRoamTarget(
            CampusGameplayRoom room,
            int seed,
            int bucket,
            out Vector3 target)
        {
            target = default;
            if (room == null || room.MarkerBounds.size.x <= 0 || room.MarkerBounds.size.y <= 0)
            {
                return false;
            }

            BoundsInt bounds = room.MarkerBounds;
            int width = Mathf.Max(1, bounds.size.x);
            int height = Mathf.Max(1, bounds.size.y);
            int total = width * height;
            int start = Mathf.Abs(seed * 73856093 ^ bucket * 19349663) % total;
            int step = Mathf.Abs(seed * 83492791) % Mathf.Max(1, total - 1) + 1;
            if (GreatestCommonDivisor(step, total) != 1)
            {
                step = 1;
            }

            for (int i = 0; i < total; i++)
            {
                int index = (start + i * step) % total;
                Vector3Int cell = new Vector3Int(bounds.xMin + index % width, bounds.yMin + index / width, 0);
                if (!IsStandableRoomCell(room, cell) || ShouldAvoidOccupiedStandCell(cell))
                {
                    continue;
                }

                target = CellCenterToWorld(cell);
                return true;
            }

            for (int i = 0; i < total; i++)
            {
                int index = (start + i * step) % total;
                Vector3Int cell = new Vector3Int(bounds.xMin + index % width, bounds.yMin + index / width, 0);
                if (!IsStandableRoomCell(room, cell))
                {
                    continue;
                }

                target = CellCenterToWorld(cell);
                return true;
            }

            return false;
        }

        private static int GreatestCommonDivisor(int left, int right)
        {
            left = Mathf.Abs(left);
            right = Mathf.Abs(right);
            while (right != 0)
            {
                int remainder = left % right;
                left = right;
                right = remainder;
            }

            return Mathf.Max(1, left);
        }

        private Vector3 ResolveFacilityPosition(CampusGameplayRoom room, CampusFacilityType facilityType, Vector3 fallback)
        {
            CampusGameplayRoom.FacilityRecord record = ResolveFacilityRecord(room, facilityType);
            return record != null ? ResolveFacilityStandPosition(room, record, fallback) : fallback;
        }

        private CampusGameplayRoom.FacilityRecord ResolveFacilityRecord(CampusGameplayRoom room, CampusFacilityType facilityType)
        {
            if (room == null || room.Facilities == null || room.Facilities.Count == 0)
            {
                return null;
            }

            List<CampusGameplayRoom.FacilityRecord> candidates = new List<CampusGameplayRoom.FacilityRecord>();
            for (int i = 0; i < room.Facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord record = room.Facilities[i];
                if (record != null && record.FacilityType == facilityType)
                {
                    candidates.Add(record);
                }
            }

            if (candidates.Count == 0)
            {
                if (ShouldReserveFacilityType(facilityType))
                {
                    ReleaseFacilityReservation();
                }

                return null;
            }

            candidates.Sort((left, right) =>
                FacilityChoiceScore(room, left, facilityType).CompareTo(FacilityChoiceScore(room, right, facilityType)));

            if (!ShouldReserveFacilityType(facilityType))
            {
                return candidates[0];
            }

            string ownerId = ResolveReservationOwnerId();
            for (int i = 0; i < candidates.Count; i++)
            {
                string key = BuildFacilityReservationKey(room, candidates[i], facilityType);
                if (ReservedFacilityOwnerByKey.TryGetValue(key, out string owner) &&
                    string.Equals(owner, ownerId, StringComparison.OrdinalIgnoreCase))
                {
                    if (!CanUseFacilityStand(room, candidates[i], facilityType, ownerId))
                    {
                        ReleaseFacilityReservation();
                        continue;
                    }

                    activeReservedFacilityKey = key;
                    return candidates[i];
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string key = BuildFacilityReservationKey(room, candidates[i], facilityType);
                if (ReservedFacilityOwnerByKey.TryGetValue(key, out string owner) &&
                    !string.Equals(owner, ownerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CanUseFacilityStand(room, candidates[i], facilityType, ownerId))
                {
                    continue;
                }

                ReserveFacility(key, ownerId);
                return candidates[i];
            }

            ReleaseFacilityReservation();
            return null;
        }

        private float FacilityChoiceScore(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record,
            CampusFacilityType facilityType)
        {
            Vector3Int cell = record != null ? record.Cell : default;
            float distance = CellDistanceScore(cell, transform.position);
            float randomBias = PseudoRandom01(
                personalSeed + cell.x * 131 + cell.y * 197 + (int)facilityType * 421,
                Mathf.FloorToInt(Time.time / 45f)) * 4.5f;
            float roleBias = runtime != null && runtime.Data != null && runtime.Data.Role == CampusCharacterRole.Teacher
                ? -1.2f
                : 0f;
            return randomBias + distance * 0.08f + roleBias;
        }

        private static bool ShouldReserveFacilityType(CampusFacilityType facilityType)
        {
            switch (facilityType)
            {
                case CampusFacilityType.StudentDesk:
                case CampusFacilityType.Podium:
                case CampusFacilityType.Blackboard:
                case CampusFacilityType.OfficeDesk:
                case CampusFacilityType.Bed:
                case CampusFacilityType.CanteenCounter:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ShouldReserveFacilityTask(CampusCharacterTaskType taskType)
        {
            switch (taskType)
            {
                case CampusCharacterTaskType.AttendClass:
                case CampusCharacterTaskType.DozeAtDesk:
                case CampusCharacterTaskType.TeachClass:
                case CampusCharacterTaskType.UseOfficeDesk:
                case CampusCharacterTaskType.RestInDorm:
                case CampusCharacterTaskType.WorkCanteenCounter:
                case CampusCharacterTaskType.WorkStoreCheckout:
                    return true;
                default:
                    return false;
            }
        }

        private string BuildFacilityReservationKey(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record,
            CampusFacilityType facilityType)
        {
            string roomId = room != null && !string.IsNullOrWhiteSpace(room.RoomId) ? room.RoomId : "room";
            string facilityId = record != null && !string.IsNullOrWhiteSpace(record.FacilityId)
                ? record.FacilityId
                : facilityType.ToString();
            Vector3Int cell = record != null ? record.Cell : default;
            return roomId + "|" + facilityType + "|" + facilityId + "|" + cell.x + "," + cell.y;
        }

        private string ResolveReservationOwnerId()
        {
            return runtime != null && !string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.CharacterId
                : GetInstanceID().ToString();
        }

        private void ReserveFacility(string key, string ownerId)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(ownerId))
            {
                return;
            }

            if (string.Equals(activeReservedFacilityKey, key, StringComparison.OrdinalIgnoreCase))
            {
                ReservedFacilityOwnerByKey[key] = ownerId;
                ReservedFacilityKeyByOwner[ownerId] = key;
                return;
            }

            ReleaseFacilityReservation();
            activeReservedFacilityKey = key;
            ReservedFacilityOwnerByKey[key] = ownerId;
            ReservedFacilityKeyByOwner[ownerId] = key;
        }

        private void ReleaseFacilityReservation()
        {
            ReleaseStandReservation();
            string ownerId = ResolveReservationOwnerId();
            if (!string.IsNullOrWhiteSpace(activeReservedFacilityKey) &&
                ReservedFacilityOwnerByKey.TryGetValue(activeReservedFacilityKey, out string owner) &&
                string.Equals(owner, ownerId, StringComparison.OrdinalIgnoreCase))
            {
                ReservedFacilityOwnerByKey.Remove(activeReservedFacilityKey);
            }

            if (!string.IsNullOrWhiteSpace(ownerId) &&
                ReservedFacilityKeyByOwner.TryGetValue(ownerId, out string ownerKey) &&
                string.Equals(ownerKey, activeReservedFacilityKey, StringComparison.OrdinalIgnoreCase))
            {
                ReservedFacilityKeyByOwner.Remove(ownerId);
            }

            activeReservedFacilityKey = string.Empty;
        }

        private bool TryReserveStandCell(Vector3Int cell, string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return false;
            }

            string key = BuildStandReservationKey(cell);
            if (string.Equals(activeReservedStandKey, key, StringComparison.OrdinalIgnoreCase))
            {
                ReservedStandOwnerByKey[key] = ownerId;
                ReservedStandKeyByOwner[ownerId] = key;
                return true;
            }

            if (ReservedStandOwnerByKey.TryGetValue(key, out string owner) &&
                !string.Equals(owner, ownerId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ReleaseStandReservation();
            activeReservedStandKey = key;
            ReservedStandOwnerByKey[key] = ownerId;
            ReservedStandKeyByOwner[ownerId] = key;
            return true;
        }

        private void ReleaseStandReservation()
        {
            string ownerId = ResolveReservationOwnerId();
            if (!string.IsNullOrWhiteSpace(activeReservedStandKey) &&
                ReservedStandOwnerByKey.TryGetValue(activeReservedStandKey, out string owner) &&
                string.Equals(owner, ownerId, StringComparison.OrdinalIgnoreCase))
            {
                ReservedStandOwnerByKey.Remove(activeReservedStandKey);
            }

            if (!string.IsNullOrWhiteSpace(ownerId) &&
                ReservedStandKeyByOwner.TryGetValue(ownerId, out string ownerKey) &&
                string.Equals(ownerKey, activeReservedStandKey, StringComparison.OrdinalIgnoreCase))
            {
                ReservedStandKeyByOwner.Remove(ownerId);
            }

            activeReservedStandKey = string.Empty;
        }

        private static string BuildStandReservationKey(Vector3Int cell)
        {
            return "stand|" + cell.x + "," + cell.y;
        }

        private bool CanUseFacilityStand(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record,
            CampusFacilityType facilityType,
            string ownerId)
        {
            if (!ShouldReserveFacilityType(facilityType))
            {
                return true;
            }

            if (!TryResolveFixedFacilityStandCell(room, record, out Vector3Int standCell))
            {
                return false;
            }

            string standKey = BuildStandReservationKey(standCell);
            return !ReservedStandOwnerByKey.TryGetValue(standKey, out string standOwner) ||
                   string.Equals(standOwner, ownerId, StringComparison.OrdinalIgnoreCase);
        }

        private Vector3 ResolveFacilityStandPosition(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record,
            Vector3 fallback)
        {
            if (room == null || record == null)
            {
                fallback.z = transform.position.z;
                return fallback;
            }

            if (ShouldReserveFacilityType(record.FacilityType))
            {
                if (TryResolveFixedFacilityStandCell(room, record, out Vector3Int fixedCell) &&
                    TryReserveStandCell(fixedCell, ResolveReservationOwnerId()))
                {
                    return CellCenterToWorld(fixedCell);
                }

                ReleaseStandReservation();
                fallback.z = transform.position.z;
                return fallback;
            }

            List<Vector3Int> candidates = BuildFacilityStandCandidates(room, record);
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3Int candidate = candidates[i];
                if (!IsStandableRoomCell(room, candidate))
                {
                    continue;
                }

                if (ShouldAvoidOccupiedStandCell(candidate))
                {
                    continue;
                }

                return CellCenterToWorld(candidate);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3Int candidate = candidates[i];
                if (!IsStandableRoomCell(room, candidate))
                {
                    continue;
                }

                return CellCenterToWorld(candidate);
            }

            if (TryFindNearestStandableRoomCell(room, record.Cell, out Vector3Int nearestCell))
            {
                return CellCenterToWorld(nearestCell);
            }

            fallback.z = transform.position.z;
            return fallback;
        }

        private bool TryResolveFixedFacilityStandCell(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record,
            out Vector3Int standCell)
        {
            standCell = default;
            if (room == null || record == null)
            {
                return false;
            }

            if (!TryGetFacilityLocalStandDirection(record.FacilityType, out Vector3Int localDirection))
            {
                return false;
            }

            Vector3Int worldDirection = RotateGridDirection(localDirection, ResolveFacilityRotation90(record));
            standCell = ResolveDirectedStandCell(record, worldDirection);
            return IsStandableRoomCell(room, standCell);
        }

        private static bool TryGetFacilityLocalStandDirection(CampusFacilityType facilityType, out Vector3Int direction)
        {
            switch (facilityType)
            {
                case CampusFacilityType.Podium:
                    direction = Vector3Int.up;
                    return true;
                case CampusFacilityType.StudentDesk:
                case CampusFacilityType.Blackboard:
                case CampusFacilityType.OfficeDesk:
                case CampusFacilityType.Bed:
                case CampusFacilityType.BulletinBoard:
                case CampusFacilityType.CanteenCounter:
                case CampusFacilityType.CanteenQueuePoint:
                case CampusFacilityType.StoreShelf:
                case CampusFacilityType.StoreCheckout:
                case CampusFacilityType.StoreQueuePoint:
                    direction = Vector3Int.down;
                    return true;
                default:
                    direction = Vector3Int.zero;
                    return false;
            }
        }

        private static int ResolveFacilityRotation90(CampusGameplayRoom.FacilityRecord record)
        {
            CampusPlacedObject placedObject = record != null ? record.PlacedObject : null;
            return placedObject != null ? CampusPlacedObject.NormalizeRotation90(placedObject.Rotation90) : 0;
        }

        private static Vector3Int RotateGridDirection(Vector3Int direction, int rotation90)
        {
            direction.z = 0;
            switch (CampusPlacedObject.NormalizeRotation90(rotation90))
            {
                case 1:
                    return new Vector3Int(-direction.y, direction.x, 0);
                case 2:
                    return new Vector3Int(-direction.x, -direction.y, 0);
                case 3:
                    return new Vector3Int(direction.y, -direction.x, 0);
                default:
                    return direction;
            }
        }

        private static Vector3Int ResolveDirectedStandCell(
            CampusGameplayRoom.FacilityRecord record,
            Vector3Int direction)
        {
            Vector3Int origin = record != null ? record.Cell : default;
            origin.z = 0;
            Vector2Int size = Vector2Int.one;
            CampusPlacedObject placedObject = record != null ? record.PlacedObject : null;
            if (placedObject != null)
            {
                size = placedObject.RotatedFootprintSize;
            }

            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            int minX = origin.x;
            int maxX = origin.x + size.x - 1;
            int minY = origin.y;
            int maxY = origin.y + size.y - 1;

            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) && direction.x != 0)
            {
                int y = minY + (size.y - 1) / 2;
                int x = direction.x > 0 ? maxX + 1 : minX - 1;
                return new Vector3Int(x, y, 0);
            }

            int centerX = minX + (size.x - 1) / 2;
            int edgeY = direction.y > 0 ? maxY + 1 : minY - 1;
            return new Vector3Int(centerX, edgeY, 0);
        }

        private List<Vector3Int> BuildFacilityStandCandidates(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record)
        {
            List<Vector3Int> candidates = new List<Vector3Int>();
            Vector3Int origin = record.Cell;
            origin.z = 0;
            Vector2Int size = Vector2Int.one;
            CampusPlacedObject placedObject = record.PlacedObject;
            if (placedObject != null)
            {
                size = placedObject.RotatedFootprintSize;
            }

            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            int minX = origin.x;
            int maxX = origin.x + size.x - 1;
            int minY = origin.y;
            int maxY = origin.y + size.y - 1;

            if (record.FacilityType == CampusFacilityType.StudentDesk)
            {
                AddEdgeCells(candidates, minX, maxX, minY - 1);
                AddVerticalEdgeCells(candidates, minX - 1, minY, maxY);
                AddVerticalEdgeCells(candidates, maxX + 1, minY, maxY);
                AddEdgeCells(candidates, minX, maxX, maxY + 1);
                AddCandidate(candidates, new Vector3Int(minX - 1, minY - 1, 0));
                AddCandidate(candidates, new Vector3Int(maxX + 1, minY - 1, 0));
                AddCandidate(candidates, new Vector3Int(minX - 1, maxY + 1, 0));
                AddCandidate(candidates, new Vector3Int(maxX + 1, maxY + 1, 0));
                return candidates;
            }

            AddPerimeterCells(candidates, minX, maxX, minY, maxY);
            if (record.FacilityType == CampusFacilityType.Door)
            {
                Vector3 actorPosition = transform.position;
                candidates.Sort((left, right) =>
                    CellDistanceScore(left, actorPosition).CompareTo(CellDistanceScore(right, actorPosition)));
                return candidates;
            }

            Vector3 preferredSide = room != null ? room.WorldCenter : transform.position;
            candidates.Sort((left, right) =>
                CellDistanceScore(left, preferredSide).CompareTo(CellDistanceScore(right, preferredSide)));
            return candidates;
        }

        private bool TryResolveRoomEntryTarget(
            CampusGameplayRoom room,
            CampusCharacterTaskDirective directive,
            out Vector3 entryTarget)
        {
            entryTarget = default;
            if (room == null || directive == null || !ShouldRouteThroughRoomEntry(directive.TaskType))
            {
                return false;
            }

            Vector3Int currentCell = ResolveCurrentCell();
            if (room.ContainsCell(currentCell))
            {
                return false;
            }

            CampusGameplayRoom.FacilityRecord door = ResolveFacilityRecord(room, CampusFacilityType.Door);
            if (door != null)
            {
                entryTarget = ResolveFacilityStandPosition(room, door, room.WorldCenter);
                return true;
            }

            if (TryFindNearestStandableBoundaryCell(room, transform.position, out Vector3Int boundaryCell))
            {
                entryTarget = CellCenterToWorld(boundaryCell);
                return true;
            }

            return false;
        }

        private static bool ShouldRouteThroughRoomEntry(CampusCharacterTaskType taskType)
        {
            switch (taskType)
            {
                case CampusCharacterTaskType.AttendClass:
                case CampusCharacterTaskType.DozeAtDesk:
                case CampusCharacterTaskType.TeachClass:
                case CampusCharacterTaskType.UseOfficeDesk:
                case CampusCharacterTaskType.RestInDorm:
                case CampusCharacterTaskType.CheckBulletinBoard:
                case CampusCharacterTaskType.PickupDelivery:
                case CampusCharacterTaskType.SearchMissingDelivery:
                case CampusCharacterTaskType.ReportMissingDelivery:
                case CampusCharacterTaskType.QueueCanteenMeal:
                case CampusCharacterTaskType.ReceiveCanteenMeal:
                case CampusCharacterTaskType.BrowseStoreShelf:
                case CampusCharacterTaskType.QueueStoreCheckout:
                case CampusCharacterTaskType.PayStoreCheckout:
                case CampusCharacterTaskType.WorkStoreCheckout:
                    return true;
                default:
                    return false;
            }
        }

        private bool TryFindNearestStandableBoundaryCell(CampusGameplayRoom room, Vector3 fromPosition, out Vector3Int result)
        {
            result = default;
            if (room == null || room.MarkerBounds.size.x <= 0 || room.MarkerBounds.size.y <= 0)
            {
                return false;
            }

            float bestScore = float.PositiveInfinity;
            BoundsInt bounds = room.MarkerBounds;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    bool boundary =
                        x == bounds.xMin ||
                        x == bounds.xMax - 1 ||
                        y == bounds.yMin ||
                        y == bounds.yMax - 1;
                    if (!boundary)
                    {
                        continue;
                    }

                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (!IsStandableRoomCell(room, cell))
                    {
                        continue;
                    }

                    float score = CellDistanceScore(cell, fromPosition);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        result = cell;
                    }
                }
            }

            return bestScore < float.PositiveInfinity;
        }

        private bool TryFindNearestStandableRoomCell(CampusGameplayRoom room, Vector3Int fromCell, out Vector3Int result)
        {
            result = default;
            if (room == null || room.MarkerBounds.size.x <= 0 || room.MarkerBounds.size.y <= 0)
            {
                return false;
            }

            float bestScore = float.PositiveInfinity;
            BoundsInt bounds = room.MarkerBounds;
            Vector3 fromPosition = CellCenterToWorld(fromCell);
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (!IsStandableRoomCell(room, cell))
                    {
                        continue;
                    }

                    float score = CellDistanceScore(cell, fromPosition);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        result = cell;
                    }
                }
            }

            return bestScore < float.PositiveInfinity;
        }

        private bool IsStandableRoomCell(CampusGameplayRoom room, Vector3Int cell)
        {
            cell.z = 0;
            if (room == null || !room.ContainsCell(cell))
            {
                return false;
            }

            if (room.Facilities == null)
            {
                return true;
            }

            for (int i = 0; i < room.Facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord record = room.Facilities[i];
                CampusPlacedObject placedObject = record != null ? record.PlacedObject : null;
                if (placedObject != null && placedObject.BlocksMovement && placedObject.ContainsCell(cell))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldAvoidOccupiedStandCell(Vector3Int cell)
        {
            CampusNpcAgent[] agents = FindObjectsByType<CampusNpcAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < agents.Length; i++)
            {
                CampusNpcAgent other = agents[i];
                if (other == null || other == this || other.runtime == null || other.runtime.Data == null || other.runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                if (other.ResolveCurrentCell() == cell)
                {
                    return true;
                }

                if (WorldToCell(other.targetPosition) == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3Int ResolveCurrentCell()
        {
            return new Vector3Int(
                Mathf.FloorToInt(transform.position.x),
                Mathf.FloorToInt(transform.position.y),
                0);
        }

        private static Vector3Int WorldToCell(Vector3 worldPosition)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPosition.x),
                Mathf.FloorToInt(worldPosition.y),
                0);
        }

        private Vector3 CellCenterToWorld(Vector3Int cell)
        {
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, transform.position.z);
        }

        private static float CellDistanceScore(Vector3Int cell, Vector3 target)
        {
            float x = cell.x + 0.5f - target.x;
            float y = cell.y + 0.5f - target.y;
            return x * x + y * y;
        }

        private static void AddPerimeterCells(List<Vector3Int> candidates, int minX, int maxX, int minY, int maxY)
        {
            AddEdgeCells(candidates, minX, maxX, minY - 1);
            AddEdgeCells(candidates, minX, maxX, maxY + 1);
            AddVerticalEdgeCells(candidates, minX - 1, minY, maxY);
            AddVerticalEdgeCells(candidates, maxX + 1, minY, maxY);
        }

        private static void AddEdgeCells(List<Vector3Int> candidates, int minX, int maxX, int y)
        {
            for (int x = minX; x <= maxX; x++)
            {
                AddCandidate(candidates, new Vector3Int(x, y, 0));
            }
        }

        private static void AddVerticalEdgeCells(List<Vector3Int> candidates, int x, int minY, int maxY)
        {
            for (int y = minY; y <= maxY; y++)
            {
                AddCandidate(candidates, new Vector3Int(x, y, 0));
            }
        }

        private static void AddCandidate(List<Vector3Int> candidates, Vector3Int cell)
        {
            if (candidates == null || candidates.Contains(cell))
            {
                return;
            }

            cell.z = 0;
            candidates.Add(cell);
        }

        private float ResolvePersonalDelay(float minSeconds, float maxSeconds, int salt)
        {
            float min = Mathf.Max(0f, Mathf.Min(minSeconds, maxSeconds));
            float max = Mathf.Max(min, Mathf.Max(minSeconds, maxSeconds));
            return Mathf.Lerp(min, max, PseudoRandom01(personalSeed + Mathf.FloorToInt(Time.time * 23f), salt));
        }

        private static bool IsStrictScheduleTask(CampusCharacterTaskType taskType)
        {
            switch (taskType)
            {
                case CampusCharacterTaskType.AttendClass:
                case CampusCharacterTaskType.DozeAtDesk:
                case CampusCharacterTaskType.TeachClass:
                case CampusCharacterTaskType.UseOfficeDesk:
                case CampusCharacterTaskType.RestInDorm:
                case CampusCharacterTaskType.WorkCanteenCounter:
                case CampusCharacterTaskType.WorkStoreCheckout:
                    return true;
                default:
                    return false;
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }

                return hash == int.MinValue ? int.MaxValue : hash;
            }
        }

        private static float PseudoRandom01(int seed, int salt)
        {
            unchecked
            {
                int value = seed;
                value ^= salt * 374761393;
                value = (value << 13) ^ value;
                int mixed = value * (value * value * 15731 + 789221) + 1376312589;
                return (mixed & 0x7fffffff) / 2147483647f;
            }
        }

        private Vector3 ResolveOffsetFromPlayer(CampusCharacterRuntime playerRuntime, float desiredDistance)
        {
            if (playerRuntime == null)
            {
                return transform.position;
            }

            Vector3 direction = (transform.position - playerRuntime.transform.position).normalized;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.right;
            }

            Vector3 target = playerRuntime.transform.position + direction * Mathf.Max(0.45f, desiredDistance);
            target.z = transform.position.z;
            return target;
        }

        private Vector3 ResolveOppositeFromPlayer(CampusCharacterRuntime playerRuntime)
        {
            return ResolveOffsetFromPlayer(playerRuntime, 1.35f);
        }

        private Vector3 ClampToRoom(CampusGameplayRoom room, Vector3 position)
        {
            if (room == null || room.MarkerBounds.size.x <= 0 || room.MarkerBounds.size.y <= 0)
            {
                position.z = transform.position.z;
                return position;
            }

            float minX = room.MarkerBounds.xMin + 0.35f;
            float maxX = room.MarkerBounds.xMax - 0.35f;
            float minY = room.MarkerBounds.yMin + 0.35f;
            float maxY = room.MarkerBounds.yMax - 0.35f;
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minY, maxY);
            position.z = transform.position.z;
            return position;
        }

        private int ResolveRuntimeFloorIndex()
        {
            if (CampusRuntimeGameplayOverlayLoader.TryGetManagedEntity(this, out CampusRuntimeGameplayOverlayEntity overlayEntity))
            {
                return overlayEntity.FloorIndex;
            }

            CampusSceneCharacterDefinition sceneCharacter = GetComponent<CampusSceneCharacterDefinition>();
            if (sceneCharacter != null)
            {
                return sceneCharacter.FloorIndex;
            }

            return 1;
        }

        private bool HasRecentDisturbance()
        {
            return Time.time - latestRoomDisturbanceAt <= DisturbanceMemorySeconds;
        }

        private void HandlePrankAttempted(CampusPrankAttemptedEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime.Data.Role == CampusCharacterRole.Teacher)
            {
                Say("Who is passing notes back there?", 2.1f, false);
            }
        }

        private void HandlePrankResolved(CampusPrankResolvedEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime.Data.Role == CampusCharacterRole.Teacher)
            {
                Say(eventData.DetectedByTeacher ? "Enough. Eyes front." : "Back to work.", 2f, false);
            }
        }

        private void HandleSanctionIssued(CampusSanctionIssuedEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime.CharacterId == eventData.ActorId)
            {
                return;
            }

            if (runtime.Data.Role == CampusCharacterRole.Teacher)
            {
                Say("That is what rule-breaking buys you.", 2.1f, false);
            }
            else if (runtime.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                Say("I knew someone would get caught.", 1.9f, false);
            }
        }

        private void HandleStudentDozedOff(CampusStudentDozedOffEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime.CharacterId == eventData.StudentId)
            {
                Say("Zzz...", 2.2f, false);
            }
            else if (runtime.Data.Role == CampusCharacterRole.Student && runtime.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                Say("?", 1.4f, false);
            }
        }

        private void HandleTeacherDistracted(CampusTeacherDistractedEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime.CharacterId == eventData.TeacherId)
            {
                Say("?", 1.6f, false);
            }
        }

        private void HandleActorSkipClass(CampusActorSkipClassEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null ||
                !string.Equals(room.RoomId, eventData.FromRoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (eventData.DetectedByTeacher && runtime.Data.Role == CampusCharacterRole.Teacher)
            {
                Say("!", 1.7f, false);
            }
            else if (!eventData.DetectedByTeacher && runtime.Data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                Say("他溜了？", 1.8f, false);
            }
        }

        private void HandleItemTransferred(CampusItemTransferredEvent eventData)
        {
            if (!eventData.Illegal)
            {
                return;
            }

            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime == null || runtime.Data == null || runtime.CharacterId == eventData.ActorId)
            {
                return;
            }

            if (runtime.Data.Role == CampusCharacterRole.Teacher || runtime.Data.Role == CampusCharacterRole.Staff)
            {
                Say(eventData.Observed ? "Put that back." : "Something is missing here.", 2.1f, false);
            }
            else if (runtime.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                Say("I saw where that went.", 1.9f, false);
            }
        }

        private void HandleItemTheftObserved(CampusItemTheftObservedEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            if (runtime.CharacterId == eventData.WitnessId)
            {
                Say("I saw you take that.", 2.2f, false);
            }
            else if (runtime.Data.Role == CampusCharacterRole.Teacher)
            {
                Say("Whose property is that?", 2f, false);
            }
        }

        private void HandleInventoryQuestioned(CampusInventoryQuestionedEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            if (runtime.CharacterId == eventData.InspectorId)
            {
                Say(eventData.FoundContraband ? "Open the bag. Now." : "Show me what you are carrying.", 2.2f, false);
            }
            else if (runtime.Data.Role == CampusCharacterRole.Teacher || runtime.Data.Role == CampusCharacterRole.Staff)
            {
                Say(eventData.FoundContraband ? "Bring that here." : "Move along.", 1.8f, false);
            }
            else if (runtime.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                Say(eventData.FoundContraband ? "I knew it." : "Something is off.", 1.8f, false);
            }
        }

        private void HandleContrabandFound(CampusContrabandFoundEvent eventData)
        {
            CampusGameplayRoom room = ResolveCurrentRoom();
            if (room == null || !string.Equals(room.RoomId, eventData.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            latestRoomDisturbanceAt = Time.time;
            nextRetargetTime = 0f;
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            if (runtime.CharacterId == eventData.InspectorId)
            {
                Say("This is not yours.", 2.1f, false);
            }
            else if (runtime.Data.Role == CampusCharacterRole.Teacher)
            {
                Say("Confiscate it.", 2f, false);
            }
            else if (runtime.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                Say("That is going straight to a teacher.", 2f, false);
            }
        }

        private bool SameRoom(CampusCharacterRuntime otherRuntime, string roomId)
        {
            if (otherRuntime == null || otherRuntime.Data == null || string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            return string.Equals(otherRuntime.Data.CurrentRoomId, roomId, StringComparison.OrdinalIgnoreCase);
        }

        private string BuildInteractiveLine(GameObject actor)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return "...";
            }

            string actorId = ResolveActorId(actor);
            if (ecologyService != null && ecologyService.TryBuildInteractiveLine(data, actorId, currentTaskType, out string ecologyLine))
            {
                return ecologyLine;
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                switch (currentTaskType)
                {
                    case CampusCharacterTaskType.TeachClass:
                        return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.TeacherTeachClassInteractive);
                    case CampusCharacterTaskType.InvestigateDisturbance:
                        return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.TeacherInvestigateInteractive);
                    case CampusCharacterTaskType.PatrolHallway:
                        return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.TeacherPatrolInteractive);
                    default:
                        return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.TeacherDefaultInteractive);
                }
            }

            if (data.Role == CampusCharacterRole.Staff)
            {
                if ((data.StaffDuty & CampusStaffDuty.CanteenClerk) != 0)
                {
                    return "The counter is open, but I am watching the trays.";
                }

                if ((data.StaffDuty & CampusStaffDuty.DeliveryWatcher) != 0)
                {
                    return "Packages outside the gate are not school property. Keep moving.";
                }

                if ((data.StaffDuty & CampusStaffDuty.StoreOwner) != 0)
                {
                    return "Put it on the counter before paying.";
                }

                return "I am on duty.";
            }

            switch (currentTaskType)
            {
                case CampusCharacterTaskType.QueueCanteenMeal:
                    return "I am waiting for my meal.";
                case CampusCharacterTaskType.ReceiveCanteenMeal:
                    return "That tray is mine.";
                case CampusCharacterTaskType.BrowseStoreShelf:
                    return "I am choosing something from the shelf.";
                case CampusCharacterTaskType.QueueStoreCheckout:
                    return "The line is moving.";
                case CampusCharacterTaskType.PayStoreCheckout:
                    return "I still need the cashier.";
                case CampusCharacterTaskType.AttendClass:
                    return CampusCharacterTextCatalog.GetDialogue(
                        CampusLanguageState.CurrentLanguage,
                        data.HasTrait(CampusCharacterTrait.GoodStudent)
                            ? CampusCharacterDialogueId.StudentAttendClassGoodInteractive
                            : CampusCharacterDialogueId.StudentAttendClassDefaultInteractive);
                case CampusCharacterTaskType.DozeAtDesk:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentDozeInteractive);
                case CampusCharacterTaskType.Socialize:
                    return CampusCharacterTextCatalog.GetDialogue(
                        CampusLanguageState.CurrentLanguage,
                        data.HasTrait(CampusCharacterTrait.Troublemaker)
                            ? CampusCharacterDialogueId.StudentSocializeTroublemakerInteractive
                            : CampusCharacterDialogueId.StudentSocializeDefaultInteractive);
                case CampusCharacterTaskType.AvoidDisturbance:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentAvoidDisturbanceInteractive);
                case CampusCharacterTaskType.CheckBulletinBoard:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentCheckBulletinInteractive);
                case CampusCharacterTaskType.RestInDorm:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentRestDormInteractive);
                default:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentDefaultInteractive);
            }
        }

        private static string ResolveActorId(GameObject actor)
        {
            if (actor == null)
            {
                return string.Empty;
            }

            CampusCharacterRuntime actorRuntime = actor.GetComponent<CampusCharacterRuntime>();
            if (actorRuntime != null && !string.IsNullOrWhiteSpace(actorRuntime.CharacterId))
            {
                return actorRuntime.CharacterId;
            }

            CampusPlayerCharacter playerCharacter = actor.GetComponent<CampusPlayerCharacter>();
            return playerCharacter != null ? playerCharacter.CharacterId : string.Empty;
        }

        private string BuildAmbientLine()
        {
            if (runtime != null &&
                runtime.Data != null &&
                ecologyService != null &&
                ecologyService.TryBuildAmbientLine(runtime.Data, currentTaskType, out string ecologyLine))
            {
                return ecologyLine;
            }

            switch (currentTaskType)
            {
                case CampusCharacterTaskType.AttendClass:
                    return CampusCharacterTextCatalog.GetDialogue(
                        CampusLanguageState.CurrentLanguage,
                        runtime.Data.HasTrait(CampusCharacterTrait.GoodStudent)
                            ? CampusCharacterDialogueId.StudentAttendClassGoodAmbient
                            : CampusCharacterDialogueId.StudentAttendClassDefaultAmbient);
                case CampusCharacterTaskType.DozeAtDesk:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentDozeAmbient);
                case CampusCharacterTaskType.TeachClass:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.TeacherTeachClassAmbient);
                case CampusCharacterTaskType.PatrolHallway:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.TeacherPatrolAmbient);
                case CampusCharacterTaskType.UseOfficeDesk:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.TeacherOfficeAmbient);
                case CampusCharacterTaskType.RestInDorm:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentRestDormAmbient);
                case CampusCharacterTaskType.WanderCommonArea:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentWanderAmbient);
                case CampusCharacterTaskType.CheckBulletinBoard:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentCheckBulletinAmbient);
                case CampusCharacterTaskType.Socialize:
                    return CampusCharacterTextCatalog.GetDialogue(
                        CampusLanguageState.CurrentLanguage,
                        runtime.Data.HasTrait(CampusCharacterTrait.Troublemaker)
                            ? CampusCharacterDialogueId.StudentSocializeTroublemakerAmbient
                            : CampusCharacterDialogueId.StudentSocializeDefaultAmbient);
                case CampusCharacterTaskType.InvestigateDisturbance:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.TeacherInvestigateAmbient);
                case CampusCharacterTaskType.AvoidDisturbance:
                    return CampusCharacterTextCatalog.GetDialogue(CampusLanguageState.CurrentLanguage, CampusCharacterDialogueId.StudentAvoidDisturbanceAmbient);
                case CampusCharacterTaskType.WorkCanteenCounter:
                    return "Malatang, noodles, fried chicken. One pair of hands.";
                case CampusCharacterTaskType.WorkStoreCheckout:
                    return "Scan, count, bag. Next.";
                case CampusCharacterTaskType.QueueCanteenMeal:
                    return "Still waiting for the counter.";
                case CampusCharacterTaskType.ReceiveCanteenMeal:
                    return "Got it, thanks.";
                case CampusCharacterTaskType.BrowseStoreShelf:
                    return "This one looks useful.";
                case CampusCharacterTaskType.QueueStoreCheckout:
                    return "Checkout line.";
                case CampusCharacterTaskType.PayStoreCheckout:
                    return "Paying now.";
                case CampusCharacterTaskType.WatchDeliveryPoint:
                    return "No deliveries at the camera gate.";
                case CampusCharacterTaskType.PickupDelivery:
                    return "I ordered it. It should be here.";
                case CampusCharacterTaskType.SearchMissingDelivery:
                    return "Where did my delivery go?";
                case CampusCharacterTaskType.ReportMissingDelivery:
                    return "Teacher, my delivery disappeared.";
                default:
                    return CampusCharacterTextCatalog.GetDialogue(
                        CampusLanguageState.CurrentLanguage,
                        runtime.Data.Role == CampusCharacterRole.Teacher
                            ? CampusCharacterDialogueId.TeacherDefaultAmbient
                            : CampusCharacterDialogueId.StudentDefaultAmbient);
            }
        }

        private void Say(string line, float durationSeconds, bool writeToLog)
        {
            if (speechBubble != null)
            {
                speechBubble.Speak(line, durationSeconds);
            }

            if (writeToLog && bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatTalkLog(
                    CampusLanguageState.CurrentLanguage,
                    ResolveDisplayName(),
                    line));
            }
        }

        private string ResolveDisplayName()
        {
            return runtime != null && runtime.Data != null
                ? runtime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)
                : gameObject.name;
        }

        private Color ResolveShirtColor()
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return new Color(0.33f, 0.57f, 0.78f, 1f);
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                return (data.TeacherDuty & CampusTeacherDuty.MathTeacher) != 0
                    ? new Color(0.74f, 0.49f, 0.22f, 1f)
                    : new Color(0.28f, 0.51f, 0.73f, 1f);
            }

            if (data.Role == CampusCharacterRole.Staff)
            {
                return (data.StaffDuty & CampusStaffDuty.CanteenClerk) != 0
                    ? new Color(0.72f, 0.58f, 0.34f, 1f)
                    : new Color(0.42f, 0.56f, 0.52f, 1f);
            }

            if (data.HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                return new Color(0.53f, 0.52f, 0.82f, 1f);
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return new Color(0.34f, 0.66f, 0.40f, 1f);
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                return new Color(0.82f, 0.70f, 0.26f, 1f);
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                return new Color(0.55f, 0.31f, 0.24f, 1f);
            }

            return new Color(0.33f, 0.57f, 0.78f, 1f);
        }

        private static Sprite GetBodySprite(Color shirtColor)
        {
            int spriteKey = ColorToKey(shirtColor);
            if (CachedBodySprites.TryGetValue(spriteKey, out Sprite cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.alphaIsTransparency = true;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color hair = new Color32(36, 20, 21, 255);
            Color skin = new Color32(150, 100, 90, 255);
            Color shirt = shirtColor;
            Color pants = new Color32(16, 28, 58, 255);
            Color shoes = new Color32(10, 14, 32, 255);

            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            FillRect(pixels, 16, 4, 11, 8, 2, hair);
            FillRect(pixels, 16, 4, 9, 1, 2, hair);
            FillRect(pixels, 16, 5, 8, 6, 3, skin);
            FillRect(pixels, 16, 4, 5, 2, 3, skin);
            FillRect(pixels, 16, 10, 5, 2, 3, skin);
            FillRect(pixels, 16, 6, 4, 4, 4, shirt);
            FillRect(pixels, 16, 6, 0, 2, 4, pants);
            FillRect(pixels, 16, 8, 0, 2, 4, pants);
            FillRect(pixels, 16, 5, 0, 2, 1, shoes);
            FillRect(pixels, 16, 9, 0, 2, 1, shoes);
            FillRect(pixels, 16, 5, 7, 1, 1, hair);
            FillRect(pixels, 16, 10, 7, 1, 1, hair);

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.02f),
                16f);
            sprite.name = "CampusNpcBodySprite_" + spriteKey;
            CachedBodySprites[spriteKey] = sprite;
            return sprite;
        }

        private static void FillRect(Color[] pixels, int textureWidth, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    if (px < 0 || px >= textureWidth || py < 0 || py >= 16)
                    {
                        continue;
                    }

                    pixels[py * textureWidth + px] = color;
                }
            }
        }

        private static int ColorToKey(Color color)
        {
            Color32 c = color;
            return (c.r << 16) | (c.g << 8) | c.b;
        }

        private sealed class PathNode
        {
            public PathNode(Vector3Int cell, float costFromStart, float estimatedCost, PathNode parent)
            {
                Cell = cell;
                CostFromStart = costFromStart;
                EstimatedCost = estimatedCost;
                Parent = parent;
            }

            public Vector3Int Cell { get; }
            public float CostFromStart { get; set; }
            public float EstimatedCost { get; }
            public float TotalCost => CostFromStart + EstimatedCost;
            public PathNode Parent { get; set; }
        }
    }
}
