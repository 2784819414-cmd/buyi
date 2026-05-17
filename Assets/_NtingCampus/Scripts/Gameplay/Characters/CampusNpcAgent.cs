using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
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

        private static readonly Dictionary<int, Sprite> CachedBodySprites = new Dictionary<int, Sprite>();

        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusNpcInteractable interactable;
        [SerializeField] private CampusNpcSpeechBubble speechBubble;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private Transform speechAnchor;
        [SerializeField] private CampusCharacterBodyController bodyController;
        [SerializeField] private bool speechBubbleBound;
        [SerializeField, Min(0.2f)] private float walkSpeed = 0.95f;
        [SerializeField, Min(0.1f)] private float retargetIntervalSeconds = 0.22f;
        [SerializeField, Min(1f)] private float minAmbientSpeechSeconds = 4f;
        [SerializeField, Min(2f)] private float maxAmbientSpeechSeconds = 8f;
        [SerializeField, Min(0.2f)] private float patrolStride = 0.65f;
        [SerializeField, Min(0.2f)] private float doorInteractDistance = 0.92f;

        [SerializeField] private CampusCharacterTaskType currentTaskType;
        [SerializeField] private string currentTaskLabel = string.Empty;
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private Vector3 anchorPosition;
        [SerializeField] private float nextRetargetTime;
        [SerializeField] private float nextAmbientSpeechTime;
        [SerializeField] private float latestRoomDisturbanceAt = -999f;
        [SerializeField] private bool isMoving;

        private CampusGameplayEventHub gameplayEventHub;
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
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;

            EnsurePresentation();
            EnsureBodyController();
            EnsureInteraction();
            EnsureGameplayEventSubscription();

            anchorPosition = transform.position;
            targetPosition = transform.position;
            currentTaskType = CampusCharacterTaskType.Idle;
            currentTaskLabel = "Idle";
            nextRetargetTime = Time.time + UnityEngine.Random.Range(0.1f, 0.45f);
            nextAmbientSpeechTime = Time.time + UnityEngine.Random.Range(minAmbientSpeechSeconds, maxAmbientSpeechSeconds);
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
                runtime.Data.AddMemory("talked_to_player");
            }

            return true;
        }

        private void Awake()
        {
            runtime = runtime != null ? runtime : GetComponent<CampusCharacterRuntime>();
            Initialize(runtime, bootstrap, worldService);
        }

        private void OnDestroy()
        {
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
            anchor.PromptText = "Talk " + ResolveDisplayName();
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

            nextRetargetTime = Time.time + retargetIntervalSeconds;
            CampusCharacterTaskDirective directive = BuildEffectiveDirective();
            currentTaskType = directive.TaskType;
            currentTaskLabel = directive.DebugLabel;

            CampusGameplayRoom targetRoom = scheduleService != null
                ? scheduleService.ResolveBestRoom(runtime, directive)
                : ResolveCurrentRoom();
            anchorPosition = ResolveTaskAnchor(targetRoom, directive);
            targetPosition = ResolveTaskTarget(targetRoom, directive, anchorPosition);
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

            if (HasRecentDisturbance())
            {
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
            bodyController.MoveSpeed = walkSpeed;
            bodyController.FloorIndex = ResolveRuntimeFloorIndex();
            TryOpenBlockingDoor();

            Vector2 direction = (Vector2)(targetPosition - transform.position);
            if (direction.sqrMagnitude <= ArrivalDistance * ArrivalDistance)
            {
                isMoving = false;
                bodyController.SetMovementInput(Vector2.zero);
                return;
            }

            isMoving = true;
            bodyController.SetMovementInput(direction.normalized);
        }

        private void TryOpenBlockingDoor()
        {
            Vector2 currentPosition = transform.position;
            Vector2 toTarget = (Vector2)(targetPosition - transform.position);
            if (toTarget.sqrMagnitude <= 0.01f)
            {
                return;
            }

            Vector2 moveDirection = toTarget.normalized;
            if (TryFindClosedDoor(currentPosition, moveDirection, out CampusDoor3D door3D))
            {
                door3D.Open();
                return;
            }

            if (TryFindClosedStallDoor(currentPosition, moveDirection, out RestroomStallDoor stallDoor))
            {
                stallDoor.Open();
            }
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

            CampusGameplayRoom room = worldService.FindRoomById(runtime.Data.CurrentRoomId);
            return room ?? worldService.FindRoomForRuntime(runtime);
        }

        private Vector3 ResolveTaskAnchor(CampusGameplayRoom room, CampusCharacterTaskDirective directive)
        {
            Vector3 fallback = room != null ? room.WorldCenter : transform.position;
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
                case CampusCharacterTaskType.PatrolHallway:
                case CampusCharacterTaskType.InvestigateDisturbance:
                case CampusCharacterTaskType.AvoidDisturbance:
                    return ResolveFacilityPosition(room, CampusFacilityType.Door, fallback);
                case CampusCharacterTaskType.Socialize:
                case CampusCharacterTaskType.WanderCommonArea:
                    return room != null ? room.WorldCenter : fallback;
                default:
                    return fallback;
            }
        }

        private Vector3 ResolveTaskTarget(CampusGameplayRoom room, CampusCharacterTaskDirective directive, Vector3 anchor)
        {
            CampusCharacterRuntime player = bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
            int seed = !string.IsNullOrWhiteSpace(runtime != null ? runtime.CharacterId : string.Empty)
                ? Mathf.Abs(runtime.CharacterId.GetHashCode())
                : GetInstanceID();

            switch (directive.TaskType)
            {
                case CampusCharacterTaskType.AttendClass:
                case CampusCharacterTaskType.DozeAtDesk:
                case CampusCharacterTaskType.TeachClass:
                case CampusCharacterTaskType.UseOfficeDesk:
                case CampusCharacterTaskType.RestInDorm:
                case CampusCharacterTaskType.CheckBulletinBoard:
                    return ClampToRoom(room, anchor);
                case CampusCharacterTaskType.PatrolHallway:
                    return ClampToRoom(room, anchor + ResolveLoopOffset(seed, patrolStride));
                case CampusCharacterTaskType.WanderCommonArea:
                    return ClampToRoom(room, anchor + ResolveLoopOffset(seed + Mathf.FloorToInt(Time.time * 0.7f), patrolStride * 0.8f));
                case CampusCharacterTaskType.Socialize:
                    if (player != null && SameRoom(player, room != null ? room.RoomId : string.Empty))
                    {
                        return ClampToRoom(room, ResolveOffsetFromPlayer(player, 0.9f));
                    }

                    return ClampToRoom(room, anchor + ResolveLoopOffset(seed + 17, 0.45f));
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

        private Vector3 ResolveFacilityPosition(CampusGameplayRoom room, CampusFacilityType facilityType, Vector3 fallback)
        {
            if (room == null || room.Facilities == null || room.Facilities.Count == 0)
            {
                return fallback;
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
                return fallback;
            }

            int index = 0;
            if (!string.IsNullOrWhiteSpace(runtime != null ? runtime.CharacterId : string.Empty))
            {
                index = Mathf.Abs(runtime.CharacterId.GetHashCode()) % candidates.Count;
            }

            Vector3Int cell = candidates[index].Cell;
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, transform.position.z);
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

            if (data.Role == CampusCharacterRole.Teacher)
            {
                switch (currentTaskType)
                {
                    case CampusCharacterTaskType.TeachClass:
                        return "Back to your seat. We are not done here.";
                    case CampusCharacterTaskType.InvestigateDisturbance:
                        return "I am already tracking what happened.";
                    case CampusCharacterTaskType.PatrolHallway:
                        return "Keep the corridor clear and keep moving.";
                    default:
                        return "Use your time properly. I am watching.";
                }
            }

            switch (currentTaskType)
            {
                case CampusCharacterTaskType.AttendClass:
                    return data.HasTrait(CampusCharacterTrait.GoodStudent)
                        ? "I am trying to keep up with the lesson."
                        : "Can we talk after class instead?";
                case CampusCharacterTaskType.DozeAtDesk:
                    return "If I close my eyes for one second, I am finished.";
                case CampusCharacterTaskType.Socialize:
                    return data.HasTrait(CampusCharacterTrait.Troublemaker)
                        ? "Something has to happen around here."
                        : "Breaks go by too fast.";
                case CampusCharacterTaskType.AvoidDisturbance:
                    return "No chance. I am not taking the blame for this.";
                case CampusCharacterTaskType.CheckBulletinBoard:
                    return "There is always something worth noticing on the board.";
                case CampusCharacterTaskType.RestInDorm:
                    return "I just need a quiet corner for a while.";
                default:
                    return "I am keeping my head down.";
            }
        }

        private string BuildAmbientLine()
        {
            switch (currentTaskType)
            {
                case CampusCharacterTaskType.AttendClass:
                    return runtime.Data.HasTrait(CampusCharacterTrait.GoodStudent) ? "Need to remember this." : "Back to the page...";
                case CampusCharacterTaskType.DozeAtDesk:
                    return "Just staying awake...";
                case CampusCharacterTaskType.TeachClass:
                    return "Eyes on the lesson.";
                case CampusCharacterTaskType.PatrolHallway:
                    return "Hallway stays clear.";
                case CampusCharacterTaskType.UseOfficeDesk:
                    return "Another report, another delay.";
                case CampusCharacterTaskType.RestInDorm:
                    return "Finally, a quieter room.";
                case CampusCharacterTaskType.WanderCommonArea:
                    return "Break is not long enough.";
                case CampusCharacterTaskType.CheckBulletinBoard:
                    return "Someone always posts something interesting.";
                case CampusCharacterTaskType.Socialize:
                    return runtime.Data.HasTrait(CampusCharacterTrait.Troublemaker) ? "This room needs a spark." : "What happened back there?";
                case CampusCharacterTaskType.InvestigateDisturbance:
                    return "No more whispers.";
                case CampusCharacterTaskType.AvoidDisturbance:
                    return "Do not drag me into it.";
                default:
                    return runtime.Data.Role == CampusCharacterRole.Teacher ? "Quiet room, good." : "Mm.";
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
                bootstrap.EventLog.AddLog("[Talk] " + ResolveDisplayName() + ": " + line);
            }
        }

        private string ResolveDisplayName()
        {
            return runtime != null && runtime.Data != null && !string.IsNullOrWhiteSpace(runtime.Data.DisplayName)
                ? runtime.Data.DisplayName
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
    }
}
