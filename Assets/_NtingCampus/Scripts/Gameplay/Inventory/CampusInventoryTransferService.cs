using Nting.Storage;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    [DisallowMultipleComponent]
    public sealed class CampusInventoryTransferService : MonoBehaviour
    {
        private const string GroundContainerId = "ground";
        private const string ConsumedContainerId = "consumed";

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;

        private CampusInventoryContextResolver contextResolver;
        private CampusInventoryEventPublisher eventPublisher;
        private CampusHandPickupService handPickup;
        private CampusItemLegalityService legalityService;

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
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;
            contextResolver = new CampusInventoryContextResolver(bootstrap);
            legalityService = new CampusItemLegalityService();
            eventPublisher = new CampusInventoryEventPublisher(gameplayEventHub, contextResolver);
            handPickup = new CampusHandPickupService(this);
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
            EnsureServices();
            context = contextResolver.Normalize(context);
            source = source ?? item?.CurrentContainer;
            if (!ValidateMove(item, target, out result))
            {
                return false;
            }

            if (source == target)
            {
                return TryMoveWithinContainer(item, source, target, x, y, context, out result);
            }

            CampusMoveRuleContext rules = BuildMoveRuleContext(item, source, target, context);
            if (!StorageTransferService.TryMove(item, source, target, x, y, out _, out string errorMessage))
            {
                result = StorageTransferResult.Fail(errorMessage);
                return false;
            }

            FinalizeMove(item, source, target != null ? target.Id : string.Empty, context, rules, target, out result);
            return true;
        }

        public bool TryPickUpIntoHands(
            StorageMemory memory,
            StorageItemModel item,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            EnsureServices();
            return handPickup.TryPickUpIntoHands(memory, item, context, out result);
        }

        public bool TryMoveToFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (targets == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.TargetBlocked));
                return false;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                StorageContainerModel target = targets[i];
                if (target != null && target != source && TryPlaceFirstFit(item, source, target, context, out result))
                {
                    return true;
                }
            }

            result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.TargetBlocked));
            return false;
        }

        public bool TryDropItemToGround(
            GameObject sourceContext,
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            EnsureServices();
            context = contextResolver.Normalize(context);
            source = source ?? item?.CurrentContainer;
            if (item == null || source == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            CampusMoveRuleContext rules = BuildMoveRuleContext(item, source, null, context);
            StorageItemEvidenceState previousEvidence = item.EnsureEvidence().Clone();
            if (!StorageTransferService.TryRemove(item, source, out StorageItemPosition previousPosition, out string removeError))
            {
                result = StorageTransferResult.Fail(removeError);
                return false;
            }

            ApplyLegalityState(item, source, null, context, rules);
            if (!CampusStorageGroundItemUtility.TryDropItemToGround(sourceContext, item, out string dropError))
            {
                item.EnsureEvidence().CopyFrom(previousEvidence);
                StorageTransferService.Restore(item, source, previousPosition);
                result = StorageTransferResult.Fail(string.IsNullOrWhiteSpace(dropError) ? StorageTextCatalog.Get(StorageTextId.CouldNotDropToGround) : dropError);
                return false;
            }

            FinalizeMove(item, source, GroundContainerId, context, rules, null, out result);
            return true;
        }

        public bool TryDropItemAtWorldPosition(
            GameObject sourceContext,
            StorageItemModel item,
            StorageContainerModel source,
            Vector3 worldPosition,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            EnsureServices();
            context = contextResolver.Normalize(context);
            source = source ?? item?.CurrentContainer;
            if (item == null || source == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            CampusMoveRuleContext rules = BuildMoveRuleContext(item, source, null, context);
            StorageItemEvidenceState previousEvidence = item.EnsureEvidence().Clone();
            if (!StorageTransferService.TryRemove(item, source, out StorageItemPosition previousPosition, out string removeError))
            {
                result = StorageTransferResult.Fail(removeError);
                return false;
            }

            ApplyLegalityState(item, source, null, context, rules);
            if (!CampusStorageGroundItemUtility.TryPlaceItemAtWorldPosition(sourceContext, item, worldPosition, out string dropError))
            {
                item.EnsureEvidence().CopyFrom(previousEvidence);
                StorageTransferService.Restore(item, source, previousPosition);
                result = StorageTransferResult.Fail(string.IsNullOrWhiteSpace(dropError) ? StorageTextCatalog.Get(StorageTextId.CouldNotDropToGround) : dropError);
                return false;
            }

            FinalizeMove(item, source, GroundContainerId, context, rules, null, out result);
            return true;
        }

        public bool TryConsumeItem(
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            EnsureServices();
            context = contextResolver.Normalize(context);
            source = source ?? item?.CurrentContainer;
            bool destroyedEvidence = item != null && item.IsStolenEvidence;
            if (!StorageTransferService.TryRemove(item, source, out _, out string errorMessage))
            {
                result = StorageTransferResult.Fail(errorMessage);
                return false;
            }

            if (destroyedEvidence)
            {
                item.EnsureEvidence().MarkEvidenceDestroyed();
            }

            eventPublisher.PublishTransfer(item, source, ConsumedContainerId, context, destroyedEvidence, false);
            if (destroyedEvidence && !context.SuppressNpcDetection)
            {
                eventPublisher.PublishProtectedItemMoved(
                    item,
                    source,
                    ConsumedContainerId,
                    context,
                    item.OwnerId,
                    CampusItemRiskUtility.ResolveProtectedMoveRisk(item, source, context));
            }

            result = new StorageTransferResult(
                true,
                destroyedEvidence,
                false,
                StorageTextCatalog.Format(StorageTextId.UsedItem, CampusInventoryEventPublisher.ResolveItemName(item)),
                string.Empty);
            return true;
        }

        internal bool TryPlaceFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (target != null && item != null && target.FindFirstFit(item, out Vector2Int position))
            {
                return TryMoveItem(item, source, target, position.x, position.y, context, out result);
            }

            result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.TargetBlocked));
            return false;
        }

        private bool TryMoveWithinContainer(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (!StorageTransferService.TryMove(item, source, target, x, y, out _, out string errorMessage))
            {
                result = StorageTransferResult.Fail(errorMessage);
                return false;
            }

            eventPublisher.PublishTransfer(item, source, target.Id, context, false, false);
            result = new StorageTransferResult(
                true,
                false,
                false,
                StorageTextCatalog.Format(StorageTextId.MovedItem, CampusInventoryEventPublisher.ResolveItemName(item)),
                string.Empty);
            return true;
        }

        private void FinalizeMove(
            StorageItemModel item,
            StorageContainerModel source,
            string targetContainerId,
            StorageTransferContext context,
            CampusMoveRuleContext rules,
            StorageContainerModel target,
            out StorageTransferResult result)
        {
            ApplyLegalityState(item, source, target, context, rules);
            eventPublisher.PublishTransfer(item, source, targetContainerId, context, rules.Illegal, false);
            if (rules.Illegal && !context.SuppressNpcDetection)
            {
                eventPublisher.PublishProtectedItemMoved(
                    item,
                    source,
                    targetContainerId,
                    context,
                    rules.OwnerId,
                    rules.SuspicionRisk);
            }

            result = new StorageTransferResult(
                true,
                rules.Illegal,
                false,
                BuildSuccessMessage(item, rules.Illegal),
                string.Empty);
        }

        private void ApplyLegalityState(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            StorageTransferContext context,
            CampusMoveRuleContext rules)
        {
            if (rules.Illegal)
            {
                legalityService.MarkIllegalMove(item, source, context, rules.RoomId, rules.OwnerId, rules.SuspicionRisk);
                return;
            }

            legalityService.PreservePersonalOwnership(item, source, target, rules.ActorId);
        }

        private CampusMoveRuleContext BuildMoveRuleContext(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            StorageTransferContext context)
        {
            string actorId = contextResolver.ResolveActorId(context);
            string roomId = contextResolver.ResolveRoomId(context, source);
            string ownerId = CampusInventoryContextResolver.ResolveOwnerId(item, source, context);
            bool illegal = legalityService.IsIllegalTake(item, source, target, context, actorId);
            int suspicionRisk = illegal
                ? CampusItemRiskUtility.ResolveProtectedMoveRisk(item, source, context)
                : 0;
            return new CampusMoveRuleContext(actorId, roomId, ownerId, illegal, suspicionRisk);
        }

        private void EnsureServices()
        {
            if (contextResolver == null ||
                eventPublisher == null ||
                handPickup == null ||
                legalityService == null)
            {
                Initialize(bootstrap);
            }
        }

        private static bool ValidateMove(
            StorageItemModel item,
            StorageContainerModel target,
            out StorageTransferResult result)
        {
            if (item == null || target == null)
            {
                result = StorageTransferResult.Fail(item == null
                    ? StorageTextCatalog.Get(StorageTextId.MissingItem)
                    : StorageTextCatalog.Get(StorageTextId.MissingTargetContainer));
                return false;
            }

            result = StorageTransferResult.Fail(string.Empty);
            return true;
        }

        private static string BuildSuccessMessage(StorageItemModel item, bool illegal)
        {
            return illegal
                ? StorageTextCatalog.Format(StorageTextId.MovedItem, CampusInventoryEventPublisher.ResolveItemName(item))
                : StorageTextCatalog.Format(StorageTextId.MovedItem, CampusInventoryEventPublisher.ResolveItemName(item));
        }

        private readonly struct CampusMoveRuleContext
        {
            public CampusMoveRuleContext(
                string actorId,
                string roomId,
                string ownerId,
                bool illegal,
                int suspicionRisk)
            {
                ActorId = actorId;
                RoomId = roomId;
                OwnerId = ownerId;
                Illegal = illegal;
                SuspicionRisk = suspicionRisk;
            }

            public string ActorId { get; }
            public string RoomId { get; }
            public string OwnerId { get; }
            public bool Illegal { get; }
            public int SuspicionRisk { get; }
        }
    }
}
