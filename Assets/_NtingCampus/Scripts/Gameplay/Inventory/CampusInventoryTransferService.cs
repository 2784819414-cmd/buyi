using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    [DisallowMultipleComponent]
    public sealed class CampusInventoryTransferService : MonoBehaviour
    {
        private const string GroundContainerId = "ground";
        private const string ConsumedContainerId = "consumed";
        private const int SuspicionWarningThreshold = 70;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;

        public static CampusInventoryTransferService Resolve()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.InventoryTransferService != null)
            {
                return bootstrap.InventoryTransferService;
            }

            CampusInventoryTransferService existing =
                FindFirstObjectByType<CampusInventoryTransferService>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.Initialize(bootstrap);
                return existing;
            }

            GameObject host = bootstrap != null ? bootstrap.gameObject : new GameObject("CampusInventoryTransferService");
            CampusInventoryTransferService service = host.AddComponent<CampusInventoryTransferService>();
            service.Initialize(bootstrap);
            return service;
        }

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;
        }

        public bool TryMoveItem(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            context = NormalizeContext(context);
            source = source ?? item?.CurrentContainer;
            result = StorageTransferResult.Fail(string.Empty);

            if (item == null)
            {
                result = StorageTransferResult.Fail("Missing item.");
                return false;
            }

            if (target == null)
            {
                result = StorageTransferResult.Fail("Missing target container.");
                return false;
            }

            string actorId = ResolveActorId(context);
            if (source == target)
            {
                if (!target.PlaceItem(item, x, y))
                {
                    result = StorageTransferResult.Fail("Target space is blocked.");
                    return false;
                }

                PublishTransfer(item, source, target.Id, context, false, false);
                result = new StorageTransferResult(true, false, false, "Moved " + ResolveItemName(item) + ".", string.Empty);
                return true;
            }

            if (!target.CanPlace(item, x, y))
            {
                result = StorageTransferResult.Fail("Target space is blocked.");
                return false;
            }

            bool illegal = IsIllegalTake(item, source, target, context);
            string roomId = ResolveRoomId(context, source);
            string ownerId = ResolveOwnerId(item, source, context);
            int previousX = item.X;
            int previousY = item.Y;

            if (source != null && !source.RemoveItem(item))
            {
                result = StorageTransferResult.Fail("Could not remove item from source container.");
                return false;
            }

            if (!target.PlaceItem(item, x, y))
            {
                if (source != null)
                {
                    source.PlaceItem(item, previousX, previousY);
                }

                result = StorageTransferResult.Fail("Target space is blocked.");
                return false;
            }

            if (illegal)
            {
                MarkItemAsStolen(item, source, context, roomId, ownerId);
            }
            else
            {
                PreservePersonalOwnership(item, source, target, actorId);
            }

            CampusCharacterRuntime witness = null;
            bool observed = illegal &&
                            !context.SuppressNpcDetection &&
                            TryFindWitness(context, actorId, roomId, ownerId, out witness);
            string witnessId = observed && witness != null ? witness.CharacterId : string.Empty;
            ApplyTransferConsequences(item, source, target.Id, context, illegal, observed, witness);
            PublishTransfer(item, source, target.Id, context, illegal, observed);
            if (illegal && observed)
            {
                PublishObservedTheft(item, source, target.Id, context, witness, ownerId);
            }

            result = new StorageTransferResult(
                true,
                illegal,
                observed,
                BuildSuccessMessage(item, illegal, observed),
                witnessId);
            return true;
        }

        public bool TryPlaceInCarriedStorage(
            StorageMemory memory,
            StorageItemModel item,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (memory == null)
            {
                result = StorageTransferResult.Fail("Storage memory is unavailable.");
                return false;
            }

            if (item == null)
            {
                result = StorageTransferResult.Fail("Item is unavailable.");
                return false;
            }

            StoragePlayerInventoryUtility.GetOrCreateHandContainers(memory);
            StorageContainerModel backpack = StoragePlayerInventoryUtility.GetOrCreateBackpack(memory);
            StorageContainerModel[] pockets = StoragePlayerInventoryUtility.GetOrCreatePocketContainers(memory);

            if (TryPlaceFirstFit(item, null, backpack, context, out result))
            {
                return true;
            }

            for (int i = 0; i < pockets.Length; i++)
            {
                if (TryPlaceFirstFit(item, null, pockets[i], context, out result))
                {
                    return true;
                }
            }

            result = StorageTransferResult.Fail("No backpack or pocket space for " + ResolveItemName(item) + ".");
            return false;
        }

        public bool TryMoveToFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (targets == null)
            {
                result = StorageTransferResult.Fail("No target containers are available.");
                return false;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                StorageContainerModel target = targets[i];
                if (target == null || target == source)
                {
                    continue;
                }

                if (TryPlaceFirstFit(item, source, target, context, out result))
                {
                    return true;
                }
            }

            result = StorageTransferResult.Fail("Target space is blocked.");
            return false;
        }

        public bool TryDropItemToGround(
            GameObject sourceContext,
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            context = NormalizeContext(context);
            result = StorageTransferResult.Fail(string.Empty);
            source = source ?? item?.CurrentContainer;
            if (item == null || source == null)
            {
                result = StorageTransferResult.Fail("Missing item or source container.");
                return false;
            }

            int previousX = item.X;
            int previousY = item.Y;
            bool illegal = IsIllegalTake(item, source, null, context);
            string actorId = ResolveActorId(context);
            string roomId = ResolveRoomId(context, source);
            string ownerId = ResolveOwnerId(item, source, context);

            if (!source.RemoveItem(item))
            {
                result = StorageTransferResult.Fail("Could not remove item from source container.");
                return false;
            }

            if (!CampusStorageGroundItemUtility.TryDropItemToGround(sourceContext, item, out string errorMessage))
            {
                source.PlaceItem(item, previousX, previousY);
                result = StorageTransferResult.Fail(string.IsNullOrWhiteSpace(errorMessage)
                    ? "Could not drop item to ground."
                    : errorMessage);
                return false;
            }

            if (illegal)
            {
                MarkItemAsStolen(item, source, context, roomId, ownerId);
            }

            CampusCharacterRuntime witness = null;
            bool observed = illegal &&
                            !context.SuppressNpcDetection &&
                            TryFindWitness(context, actorId, roomId, ownerId, out witness);
            string witnessId = observed && witness != null ? witness.CharacterId : string.Empty;
            ApplyTransferConsequences(item, source, GroundContainerId, context, illegal, observed, witness);
            PublishTransfer(item, source, GroundContainerId, context, illegal, observed);
            if (illegal && observed)
            {
                PublishObservedTheft(item, source, GroundContainerId, context, witness, ownerId);
            }

            result = new StorageTransferResult(
                true,
                illegal,
                observed,
                BuildSuccessMessage(item, illegal, observed),
                witnessId);
            return true;
        }

        public bool TryConsumeItem(
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            context = NormalizeContext(context);
            source = source ?? item?.CurrentContainer;
            result = StorageTransferResult.Fail(string.Empty);
            if (item == null || source == null)
            {
                result = StorageTransferResult.Fail("Missing item or source container.");
                return false;
            }

            bool destroyedEvidence = item.IsStolenEvidence;
            if (!source.RemoveItem(item))
            {
                result = StorageTransferResult.Fail("Could not consume " + ResolveItemName(item) + ".");
                return false;
            }

            if (destroyedEvidence)
            {
                item.LegalState = StorageItemLegalState.EvidenceDestroyed;
                if (!context.SuppressSuspicion && bootstrap != null && bootstrap.GameState != null)
                {
                    bootstrap.GameState.AddPlayerSuspicion(4);
                    bootstrap.GameState.AddCampusChaos(1);
                }
            }

            PublishTransfer(item, source, ConsumedContainerId, context, destroyedEvidence, false);
            result = new StorageTransferResult(true, destroyedEvidence, false, "Used " + ResolveItemName(item) + ".", string.Empty);
            return true;
        }

        private bool TryPlaceFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (target == null || item == null)
            {
                return false;
            }

            if (!target.FindFirstFit(item, out Vector2Int position))
            {
                return false;
            }

            return TryMoveItem(item, source, target, position.x, position.y, context, out result);
        }

        private bool IsIllegalTake(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            StorageTransferContext context)
        {
            if (context != null && context.AllowProtectedTake)
            {
                return false;
            }

            if (context != null && context.ForceIllegal)
            {
                return true;
            }

            if (item != null && item.IsStolenEvidence)
            {
                return false;
            }

            string actorId = ResolveActorId(context);
            if (IsActorOwner(actorId, item, source))
            {
                return false;
            }

            if (source == null)
            {
                return false;
            }

            if (source.IsPlayerCarried || source.AccessPolicy == StorageContainerAccessPolicy.PlayerCarried)
            {
                return false;
            }

            bool protectedSource = source.AccessPolicy == StorageContainerAccessPolicy.OwnedPrivate ||
                                   source.AccessPolicy == StorageContainerAccessPolicy.ProtectedPublic ||
                                   source.AccessPolicy == StorageContainerAccessPolicy.StaffOnly ||
                                   source.AccessPolicy == StorageContainerAccessPolicy.Commerce ||
                                   !source.AllowTakingContents;
            if (!protectedSource)
            {
                return item != null && !item.AllowTaking;
            }

            bool takingAway = target == null ||
                              target.IsPlayerCarried ||
                              target.AccessPolicy == StorageContainerAccessPolicy.PlayerCarried ||
                              target.AccessPolicy == StorageContainerAccessPolicy.Ground;
            return takingAway || (item != null && !item.AllowTaking);
        }

        private static bool IsActorOwner(string actorId, StorageItemModel item, StorageContainerModel source)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return false;
            }

            string normalizedActorId = actorId.Trim();
            if (item != null &&
                !string.IsNullOrWhiteSpace(item.OwnerId) &&
                string.Equals(item.OwnerId.Trim(), normalizedActorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return source != null &&
                   !string.IsNullOrWhiteSpace(source.OwnerId) &&
                   string.Equals(source.OwnerId.Trim(), normalizedActorId, StringComparison.OrdinalIgnoreCase);
        }

        private static void PreservePersonalOwnership(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            string actorId)
        {
            if (item == null || string.IsNullOrWhiteSpace(actorId))
            {
                return;
            }

            bool movingFromCarried = source != null &&
                                     (source.IsPlayerCarried ||
                                      source.AccessPolicy == StorageContainerAccessPolicy.PlayerCarried);
            bool movingIntoProtected = target != null &&
                                       !target.IsPlayerCarried &&
                                       target.AccessPolicy != StorageContainerAccessPolicy.PlayerCarried &&
                                       target.AccessPolicy != StorageContainerAccessPolicy.Open &&
                                       target.AccessPolicy != StorageContainerAccessPolicy.Ground;
            if (!movingFromCarried || !movingIntoProtected)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(item.OwnerId))
            {
                item.OwnerId = actorId.Trim();
            }

            if (item.LegalState == StorageItemLegalState.Unknown)
            {
                item.LegalState = StorageItemLegalState.Personal;
            }
        }

        private void MarkItemAsStolen(
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            string roomId,
            string ownerId)
        {
            if (item == null)
            {
                return;
            }

            item.LegalState = StorageItemLegalState.Stolen;
            item.StolenDuringSession = true;
            item.AllowTaking = false;
            item.OwnerId = string.IsNullOrWhiteSpace(item.OwnerId) ? ownerId : item.OwnerId;
            item.SourceContainerId = source != null ? source.Id : item.SourceContainerId;
            item.SourceRoomId = string.IsNullOrWhiteSpace(roomId) ? item.SourceRoomId : roomId;
            item.SourceLocation = !string.IsNullOrWhiteSpace(context.SourceLocation)
                ? context.SourceLocation
                : source != null && !string.IsNullOrWhiteSpace(source.DisplayName)
                    ? source.DisplayName
                    : item.SourceLocation;
            item.SuspicionRisk = Mathf.Max(item.SuspicionRisk, ResolveSuspicionAmount(item, source, context, false));
        }

        private void ApplyTransferConsequences(
            StorageItemModel item,
            StorageContainerModel source,
            string targetContainerId,
            StorageTransferContext context,
            bool illegal,
            bool observed,
            CampusCharacterRuntime witness)
        {
            if (!illegal || context.SuppressSuspicion || bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            int suspicion = ResolveSuspicionAmount(item, source, context, observed);
            bootstrap.GameState.AddPlayerSuspicion(suspicion);
            bootstrap.GameState.AddCampusChaos(observed ? 4 : 2);
            bootstrap.GameState.AddCampusOrder(observed ? -3 : -1);
            if (observed)
            {
                bootstrap.GameState.AddTeacherAlertness(4);
            }

            if (bootstrap.EventLog != null)
            {
                string witnessText = observed && witness != null ? " Witness=" + witness.CharacterId + "." : string.Empty;
                bootstrap.EventLog.AddLog("[Inventory] Protected item moved: " + ResolveItemName(item) +
                                          " -> " + targetContainerId +
                                          ". Suspicion +" + suspicion + "." +
                                          witnessText);
            }

            if (bootstrap.GameState.PlayerSuspicion < SuspicionWarningThreshold)
            {
                return;
            }

            bootstrap.GameState.AddDailyWarningCount(1);
            bootstrap.GameState.AddTeacherAlertness(5);
            bootstrap.GameState.SetPlayerSuspicion(45);
            if (bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog("[Inventory] Suspicion threshold reached after protected item movement.");
            }
        }

        private int ResolveSuspicionAmount(
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            bool observed)
        {
            if (context != null && context.SuspicionRiskOverride >= 0)
            {
                return Mathf.Max(0, context.SuspicionRiskOverride + (observed ? 10 : 0));
            }

            int amount = 8;
            if (source != null)
            {
                amount += Mathf.Max(0, source.SuspicionRisk);
                switch (source.AccessPolicy)
                {
                    case StorageContainerAccessPolicy.StaffOnly:
                        amount += 12;
                        break;
                    case StorageContainerAccessPolicy.Commerce:
                        amount += 8;
                        break;
                    case StorageContainerAccessPolicy.OwnedPrivate:
                        amount += 7;
                        break;
                    case StorageContainerAccessPolicy.ProtectedPublic:
                        amount += 5;
                        break;
                }
            }

            if (item != null)
            {
                amount += Mathf.Max(0, item.SuspicionRisk);
                if (item.Weight >= 2f)
                {
                    amount += 2;
                }
            }

            if (observed)
            {
                amount += 10;
            }

            return Mathf.Clamp(amount, 1, 45);
        }

        private bool TryFindWitness(
            StorageTransferContext context,
            string actorId,
            string roomId,
            string ownerId,
            out CampusCharacterRuntime witness)
        {
            witness = null;
            if (rosterService == null || string.IsNullOrWhiteSpace(actorId))
            {
                return false;
            }

            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(context);
            for (int i = 0; i < rosterService.Runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = rosterService.Runtimes[i];
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                if (string.Equals(runtime.CharacterId, actorId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CanWitnessRoom(runtime, roomId))
                {
                    continue;
                }

                if (actorRuntime != null &&
                    Vector2.Distance(runtime.transform.position, actorRuntime.transform.position) > 4.75f)
                {
                    continue;
                }

                float chance = ResolveWitnessChance(runtime, ownerId);
                if (chance <= 0f)
                {
                    continue;
                }

                if (UnityEngine.Random.value <= chance)
                {
                    witness = runtime;
                    return true;
                }
            }

            return false;
        }

        private bool CanWitnessRoom(CampusCharacterRuntime runtime, string roomId)
        {
            if (runtime == null || runtime.Data == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                return true;
            }

            CampusGameplayRoom currentRoom = worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
            string currentRoomId = currentRoom != null ? currentRoom.RoomId : runtime.Data.CurrentRoomId;
            return string.Equals(currentRoomId, roomId, StringComparison.OrdinalIgnoreCase);
        }

        private float ResolveWitnessChance(CampusCharacterRuntime runtime, string ownerId)
        {
            if (runtime == null || runtime.Data == null)
            {
                return 0f;
            }

            CampusCharacterData data = runtime.Data;
            float alertnessBonus = bootstrap != null && bootstrap.GameState != null
                ? bootstrap.GameState.TeacherAlertness * 0.0025f
                : 0f;

            if (!string.IsNullOrWhiteSpace(ownerId) &&
                string.Equals(runtime.CharacterId, ownerId, StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Clamp01(0.86f + alertnessBonus);
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                return Mathf.Clamp01(0.68f + alertnessBonus);
            }

            if (data.Role == CampusCharacterRole.Staff)
            {
                return Mathf.Clamp01(0.62f + alertnessBonus);
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                return Mathf.Clamp01(0.36f + alertnessBonus);
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return Mathf.Clamp01(0.18f + alertnessBonus * 0.5f);
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                return 0.07f;
            }

            return Mathf.Clamp01(0.12f + alertnessBonus * 0.25f);
        }

        private void PublishTransfer(
            StorageItemModel item,
            StorageContainerModel source,
            string targetContainerId,
            StorageTransferContext context,
            bool illegal,
            bool observed)
        {
            if (gameplayEventHub == null || item == null)
            {
                return;
            }

            gameplayEventHub.PublishItemTransferred(new CampusItemTransferredEvent(
                ResolveActorId(context),
                ResolveInstanceId(item),
                item.DefinitionId,
                ResolveItemName(item),
                source != null ? source.Id : string.Empty,
                targetContainerId,
                ResolveRoomId(context, source),
                context.Reason,
                illegal,
                observed));
        }

        private void PublishObservedTheft(
            StorageItemModel item,
            StorageContainerModel source,
            string targetContainerId,
            StorageTransferContext context,
            CampusCharacterRuntime witness,
            string ownerId)
        {
            if (gameplayEventHub == null || item == null || witness == null)
            {
                return;
            }

            int suspicion = ResolveSuspicionAmount(item, source, context, true);
            bool shouldIssueSanction = witness.Data != null &&
                                       (witness.Data.Role == CampusCharacterRole.Teacher ||
                                        witness.Data.Role == CampusCharacterRole.Staff);
            gameplayEventHub.PublishItemTheftObserved(new CampusItemTheftObservedEvent(
                ResolveActorId(context),
                witness.CharacterId,
                ownerId,
                ResolveInstanceId(item),
                item.DefinitionId,
                ResolveItemName(item),
                source != null ? source.Id : string.Empty,
                targetContainerId,
                ResolveRoomId(context, source),
                suspicion,
                shouldIssueSanction));
        }

        private StorageTransferContext NormalizeContext(StorageTransferContext context)
        {
            context = context ?? new StorageTransferContext();
            if (string.IsNullOrWhiteSpace(context.ActorId))
            {
                CampusCharacterRuntime actorRuntime = ResolveActorRuntime(context);
                context.ActorId = actorRuntime != null ? actorRuntime.CharacterId : "player";
            }

            if (string.IsNullOrWhiteSpace(context.RoomId))
            {
                CampusCharacterRuntime actorRuntime = ResolveActorRuntime(context);
                CampusGameplayRoom room = actorRuntime != null && worldService != null
                    ? worldService.FindRoomForRuntime(actorRuntime)
                    : null;
                context.RoomId = room != null ? room.RoomId : string.Empty;
            }

            return context;
        }

        private CampusCharacterRuntime ResolveActorRuntime(StorageTransferContext context)
        {
            if (context != null && context.Actor != null)
            {
                CampusCharacterRuntime runtime = context.Actor.GetComponentInParent<CampusCharacterRuntime>();
                if (runtime != null)
                {
                    return runtime;
                }
            }

            return rosterService != null ? rosterService.PlayerRuntime : null;
        }

        private string ResolveActorId(StorageTransferContext context)
        {
            if (context != null && !string.IsNullOrWhiteSpace(context.ActorId))
            {
                return context.ActorId.Trim();
            }

            CampusCharacterRuntime runtime = ResolveActorRuntime(context);
            return runtime != null && !string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.CharacterId
                : "player";
        }

        private string ResolveRoomId(StorageTransferContext context, StorageContainerModel source)
        {
            if (context != null && !string.IsNullOrWhiteSpace(context.RoomId))
            {
                return context.RoomId.Trim();
            }

            if (source != null && !string.IsNullOrWhiteSpace(source.RoomId))
            {
                return source.RoomId.Trim();
            }

            CampusCharacterRuntime runtime = ResolveActorRuntime(context);
            CampusGameplayRoom room = runtime != null && worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
            return room != null ? room.RoomId : string.Empty;
        }

        private static string ResolveOwnerId(
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.OwnerId))
            {
                return item.OwnerId.Trim();
            }

            if (context != null && !string.IsNullOrWhiteSpace(context.OwnerId))
            {
                return context.OwnerId.Trim();
            }

            return source != null && !string.IsNullOrWhiteSpace(source.OwnerId)
                ? source.OwnerId.Trim()
                : string.Empty;
        }

        private static string ResolveInstanceId(StorageItemModel item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(item.InstanceId) ? item.InstanceId : item.Id;
        }

        private static string ResolveItemName(StorageItemModel item)
        {
            if (item == null)
            {
                return "item";
            }

            if (!string.IsNullOrWhiteSpace(item.DisplayName))
            {
                return item.DisplayName;
            }

            return !string.IsNullOrWhiteSpace(item.DefinitionId) ? item.DefinitionId : "item";
        }

        private static string BuildSuccessMessage(StorageItemModel item, bool illegal, bool observed)
        {
            if (!illegal)
            {
                return "Moved " + ResolveItemName(item) + ".";
            }

            return observed
                ? "Moved " + ResolveItemName(item) + ", but someone noticed."
                : "Moved protected item: " + ResolveItemName(item) + ".";
        }
    }
}
