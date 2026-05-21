using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenInteractionProvider :
        ICampusInteractionActionProvider,
        ICampusInteractionPromptOverrideProvider
    {
        private static readonly CampusFacilityType[] ServiceWindowTypes = { CampusFacilityType.ServiceWindow };
        private static readonly CampusFacilityType[] PickupTypes =
        {
            CampusFacilityType.PickupPoint,
            CampusFacilityType.ReadyItemSurface,
            CampusFacilityType.ReadyItemContainer
        };

        public static readonly CampusCanteenInteractionProvider Instance =
            new CampusCanteenInteractionProvider();

        public string ProviderId => "campus_canteen_interactions";

        public static Func<CampusCharacterRuntime, bool> BuildCanUseRule(string actionId, UnityEngine.Object target)
        {
            if (CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.CanteenWindow))
            {
                return actor => CanUseWindow(actor, target);
            }

            if (CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.CanteenWorkstation))
            {
                return actor => CanUseWorkstation(actor, target);
            }

            return null;
        }

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.CanteenWindow))
            {
                return TryHandleWindow(context, out message);
            }

            if (CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.CanteenWorkstation))
            {
                return TryHandleWorkstation(context, out message);
            }

            return false;
        }

        public bool TryResolvePrompt(CampusInteractionActionContext context, out string prompt)
        {
            prompt = string.Empty;
            CampusCharacterRuntime actor = ResolveActorRuntime(context.Actor);
            if (CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.CanteenWindow))
            {
                if (!TryResolveWindowState(context.SourceObject, out CampusCanteenWindowState state))
                {
                    return false;
                }

                if (actor != null &&
                    state.HasReadyMeal &&
                    string.Equals(state.ReadyCustomerId, actor.CharacterId, StringComparison.OrdinalIgnoreCase))
                {
                    prompt = CampusCanteenTextCatalog.Get(CampusCanteenTextId.TakeMealPrompt);
                    return true;
                }

                if (actor != null &&
                    state.HasPendingOrder &&
                    string.Equals(state.PendingCustomerId, actor.CharacterId, StringComparison.OrdinalIgnoreCase))
                {
                    prompt = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WaitMealPrompt);
                    return true;
                }

                prompt = CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderMealPrompt);
                return true;
            }

            if (CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.CanteenWorkstation))
            {
                prompt = HasPendingWindowNearWorkstation(context.SourceObject)
                    ? CampusCanteenTextCatalog.Get(CampusCanteenTextId.ServeMealPrompt)
                    : CampusCanteenTextCatalog.Get(CampusCanteenTextId.WorkstationIdlePrompt);
                return true;
            }

            return false;
        }

        private static bool CanUseWindow(CampusCharacterRuntime actor, UnityEngine.Object target)
        {
            if (actor == null || !TryResolveWindowState(ResolvePlacedObject(target), out CampusCanteenWindowState state))
            {
                return false;
            }

            string actorId = actor.CharacterId;
            if (state.HasReadyMeal)
            {
                return string.Equals(state.ReadyCustomerId, actorId, StringComparison.OrdinalIgnoreCase);
            }

            if (state.HasPendingOrder)
            {
                return string.Equals(state.PendingCustomerId, actorId, StringComparison.OrdinalIgnoreCase);
            }

            return !HasLunchBoxInHands(actor);
        }

        private static bool CanUseWorkstation(CampusCharacterRuntime actor, UnityEngine.Object target)
        {
            if (actor == null)
            {
                return false;
            }

            CampusPlacedObject workstation = ResolvePlacedObject(target);
            if (workstation == null)
            {
                return false;
            }

            if (HasLunchBoxInHands(actor))
            {
                return true;
            }

            return TryResolveLinkedWindow(workstation, out _, out CampusGameplayRoom.FacilityRecord windowRecord) &&
                   TryResolveWindowState(windowRecord.PlacedObject, out CampusCanteenWindowState state) &&
                   state.HasPendingOrder;
        }

        private static bool TryHandleWindow(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            CampusCharacterRuntime actor = ResolveActorRuntime(context.Actor);
            if (actor == null || !TryResolveWindowState(context.SourceObject, out CampusCanteenWindowState state))
            {
                return false;
            }

            if (state.HasReadyMeal &&
                string.Equals(state.ReadyCustomerId, actor.CharacterId, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryCollectReadyMeal(actor, state, context.SourceObject, out message))
                {
                    return false;
                }

                message = string.IsNullOrWhiteSpace(message)
                    ? CampusCanteenTextCatalog.Get(CampusCanteenTextId.TookMealLog)
                    : message;
                return true;
            }

            if (state.HasPendingOrder)
            {
                if (string.Equals(state.PendingCustomerId, actor.CharacterId, StringComparison.OrdinalIgnoreCase))
                {
                    message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WaitingMealLog);
                    return true;
                }

                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowBusyLog);
                return true;
            }

            if (HasLunchBoxInHands(actor))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.HandsFullLog);
                return true;
            }

            state.PlaceOrder(actor.CharacterId);
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderedMealLog);
            return true;
        }

        private static bool TryHandleWorkstation(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            CampusCharacterRuntime actor = ResolveActorRuntime(context.Actor);
            if (actor == null ||
                !TryResolveLinkedWindow(context.SourceObject, out CampusGameplayRoom room, out CampusGameplayRoom.FacilityRecord windowRecord))
            {
                return false;
            }

            if (!TryResolveWindowState(windowRecord.PlacedObject, out CampusCanteenWindowState state))
            {
                return false;
            }

            if (!state.HasPendingOrder)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WorkstationIdleLog);
                return true;
            }

            StorageItemModel heldMeal = FindHeldLunchBox(actor, out StorageContainerModel sourceContainer);
            if (heldMeal == null)
            {
                if (!TryPrepareMealInHands(actor, state.PendingCustomerId, KeyFor(windowRecord), room, out message))
                {
                    return false;
                }

                return true;
            }

            if (!TryResolvePickupPosition(room, windowRecord, out Vector3 pickupPosition))
            {
                return false;
            }

            if (!TryPlaceMealAtPickupPoint(actor, heldMeal, sourceContainer, pickupPosition, state, out message))
            {
                return false;
            }

            return true;
        }

        private static bool TryPrepareMealInHands(
            CampusCharacterRuntime actor,
            string customerId,
            string windowKey,
            CampusGameplayRoom room,
            out string message)
        {
            message = string.Empty;
            if (HasAnyNonLunchItemInHands(actor))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.HandsFullLog);
                return true;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                return false;
            }

            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            StorageItemModel item = registry.CreateItem("lunch_box", BuildMealInstanceId(customerId, windowKey));
            if (item == null)
            {
                return false;
            }

            item.OwnerId = customerId ?? string.Empty;
            item.LegalState = StorageItemLegalState.Personal;
            item.SourceRoomId = room != null ? room.RoomId : string.Empty;
            item.SourceLocation = windowKey;

            StorageTransferContext transferContext = StorageTransferContext.ForActor(actor.gameObject, StorageTransferReason.ScriptedTake);
            transferContext.SuppressNpcDetection = true;
            transferContext.SuppressSuspicion = true;
            transferContext.OwnerId = item.OwnerId;
            transferContext.SourceLocation = item.SourceLocation;

            bool pickedUp = CampusInventoryTransferService.Resolve().TryPickUpIntoHands(memory, item, transferContext, out StorageTransferResult result);
            message = result.Message;
            return pickedUp;
        }

        private static bool TryPlaceMealAtPickupPoint(
            CampusCharacterRuntime actor,
            StorageItemModel heldMeal,
            StorageContainerModel sourceContainer,
            Vector3 pickupPosition,
            CampusCanteenWindowState state,
            out string message)
        {
            message = string.Empty;
            StorageTransferContext transferContext = StorageTransferContext.ForActor(actor.gameObject, StorageTransferReason.DropToGround);
            transferContext.SuppressNpcDetection = true;
            transferContext.SuppressSuspicion = true;
            transferContext.OwnerId = heldMeal != null ? heldMeal.OwnerId : string.Empty;
            transferContext.SourceLocation = state != null ? state.name : string.Empty;

            bool dropped = CampusInventoryTransferService.Resolve().TryDropItemAtWorldPosition(
                actor.gameObject,
                heldMeal,
                sourceContainer,
                pickupPosition,
                transferContext,
                out StorageTransferResult result);
            if (!dropped)
            {
                message = result.Message;
                return false;
            }

            CampusDroppedStorageItem droppedItem = FindDroppedItem(heldMeal.InstanceId);
            if (state != null)
            {
                state.MarkMealReady(heldMeal.OwnerId, droppedItem);
            }

            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.PreparedMealLog);
            return true;
        }

        private static bool TryCollectReadyMeal(
            CampusCharacterRuntime actor,
            CampusCanteenWindowState state,
            CampusPlacedObject windowObject,
            out string message)
        {
            message = string.Empty;
            CampusDroppedStorageItem readyMeal = state.ReadyMealItem;
            if (readyMeal == null)
            {
                state.ClearReadyMeal();
                return false;
            }

            if (!readyMeal.TryPickup(actor, out StorageTransferResult result))
            {
                message = result.Message;
                return false;
            }

            state.ClearReadyMeal();
            state.ClearPendingOrder();
            if (windowObject != null &&
                windowObject.TryGetComponent(out CampusCanteenWindowState windowState) &&
                windowState == state)
            {
                // Keep the state component alive, just clear the current meal.
            }

            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.TookMealLog);
            return true;
        }

        private static bool HasPendingWindowNearWorkstation(CampusPlacedObject workstation)
        {
            return TryResolveLinkedWindow(workstation, out _, out CampusGameplayRoom.FacilityRecord windowRecord) &&
                   TryResolveWindowState(windowRecord.PlacedObject, out CampusCanteenWindowState state) &&
                   state.HasPendingOrder;
        }

        private static bool TryResolveWindowState(CampusPlacedObject windowObject, out CampusCanteenWindowState state)
        {
            state = null;
            if (windowObject == null || CampusFacilityTypeResolver.Resolve(windowObject) != CampusFacilityType.ServiceWindow)
            {
                return false;
            }

            state = windowObject.GetComponent<CampusCanteenWindowState>();
            if (state == null)
            {
                state = windowObject.gameObject.AddComponent<CampusCanteenWindowState>();
            }

            return true;
        }

        private static bool TryResolveLinkedWindow(
            CampusPlacedObject workstation,
            out CampusGameplayRoom room,
            out CampusGameplayRoom.FacilityRecord windowRecord)
        {
            room = null;
            windowRecord = null;
            if (!TryResolveRoomAndFacility(workstation, out room, out _))
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> windows = CampusNpcFacilitySelector.Collect(room, ServiceWindowTypes);
            if (windows.Count == 0)
            {
                return false;
            }

            Vector3 workstationPosition = workstation.transform.position;
            float bestDistance = float.MaxValue;
            CampusGameplayRoom.FacilityRecord bestRecord = null;
            for (int i = 0; i < windows.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord candidate = windows[i];
                if (candidate == null)
                {
                    continue;
                }

                float distance = (CampusNpcFacilitySelector.PositionOf(candidate) - workstationPosition).sqrMagnitude;
                if (bestRecord == null || distance < bestDistance)
                {
                    bestRecord = candidate;
                    bestDistance = distance;
                }
            }

            windowRecord = bestRecord;
            return windowRecord != null;
        }

        private static bool TryResolvePickupPosition(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord windowRecord,
            out Vector3 pickupPosition)
        {
            pickupPosition = Vector3.zero;
            if (room == null || windowRecord == null)
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> pickupRecords = CampusNpcFacilitySelector.Collect(room, PickupTypes);
            if (pickupRecords.Count == 0)
            {
                pickupPosition = CampusNpcFacilitySelector.PositionOf(windowRecord);
                return true;
            }

            Vector3 windowPosition = CampusNpcFacilitySelector.PositionOf(windowRecord);
            float bestDistance = float.MaxValue;
            CampusGameplayRoom.FacilityRecord bestRecord = null;
            for (int i = 0; i < pickupRecords.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord candidate = pickupRecords[i];
                if (candidate == null)
                {
                    continue;
                }

                float distance = (CampusNpcFacilitySelector.PositionOf(candidate) - windowPosition).sqrMagnitude;
                if (bestRecord == null || distance < bestDistance)
                {
                    bestRecord = candidate;
                    bestDistance = distance;
                }
            }

            pickupPosition = bestRecord != null
                ? CampusNpcFacilitySelector.PositionOf(bestRecord)
                : windowPosition;
            return true;
        }

        private static bool TryResolveRoomAndFacility(
            CampusPlacedObject placedObject,
            out CampusGameplayRoom room,
            out CampusGameplayRoom.FacilityRecord record)
        {
            room = null;
            record = null;
            CampusWorldService worldService = ResolveWorldService();
            CampusRoomRegistry registry = worldService != null ? worldService.RoomRegistry : null;
            if (placedObject == null || registry == null || registry.Rooms == null)
            {
                return false;
            }

            IReadOnlyList<CampusGameplayRoom> rooms = registry.Rooms;
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                CampusGameplayRoom candidateRoom = rooms[roomIndex];
                if (candidateRoom == null)
                {
                    continue;
                }

                IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = candidateRoom.Facilities;
                for (int facilityIndex = 0; facilityIndex < facilities.Count; facilityIndex++)
                {
                    CampusGameplayRoom.FacilityRecord candidateRecord = facilities[facilityIndex];
                    if (candidateRecord == null || !ReferenceEquals(candidateRecord.PlacedObject, placedObject))
                    {
                        continue;
                    }

                    room = candidateRoom;
                    record = candidateRecord;
                    return true;
                }
            }

            return false;
        }

        private static CampusPlacedObject ResolvePlacedObject(UnityEngine.Object target)
        {
            if (target is CampusPlacedObject placedObject)
            {
                return placedObject;
            }

            if (target is Component component)
            {
                return component.GetComponent<CampusPlacedObject>() ?? component.GetComponentInParent<CampusPlacedObject>();
            }

            if (target is GameObject gameObject)
            {
                return gameObject.GetComponent<CampusPlacedObject>() ?? gameObject.GetComponentInParent<CampusPlacedObject>();
            }

            return null;
        }

        private static CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            if (actor != null)
            {
                CampusCharacterRuntime runtime = actor.GetComponentInParent<CampusCharacterRuntime>();
                if (runtime != null)
                {
                    return runtime;
                }
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
        }

        private static CampusWorldService ResolveWorldService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null ? bootstrap.WorldService : UnityEngine.Object.FindFirstObjectByType<CampusWorldService>(FindObjectsInactive.Include);
        }

        private static bool HasLunchBoxInHands(CampusCharacterRuntime actor)
        {
            return FindHeldLunchBox(actor, out _) != null;
        }

        private static bool HasAnyNonLunchItemInHands(CampusCharacterRuntime actor)
        {
            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            StorageContainerModel[] hands = inventory.Hands;
            for (int i = 0; i < hands.Length; i++)
            {
                StorageContainerModel container = hands[i];
                if (container == null || container.Items == null)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < container.Items.Count; itemIndex++)
                {
                    StorageItemModel item = container.Items[itemIndex];
                    if (item != null && !string.Equals(item.DefinitionId, "lunch_box", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static StorageItemModel FindHeldLunchBox(CampusCharacterRuntime actor, out StorageContainerModel sourceContainer)
        {
            sourceContainer = null;
            if (actor == null)
            {
                return null;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            StorageContainerModel[] hands = inventory.Hands;
            for (int i = 0; i < hands.Length; i++)
            {
                StorageContainerModel container = hands[i];
                if (container == null || container.Items == null)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < container.Items.Count; itemIndex++)
                {
                    StorageItemModel item = container.Items[itemIndex];
                    if (item != null && string.Equals(item.DefinitionId, "lunch_box", StringComparison.OrdinalIgnoreCase))
                    {
                        sourceContainer = container;
                        return item;
                    }
                }
            }

            return null;
        }

        private static CampusDroppedStorageItem FindDroppedItem(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            CampusDroppedStorageItem[] items = UnityEngine.Object.FindObjectsByType<CampusDroppedStorageItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < items.Length; i++)
            {
                CampusDroppedStorageItem item = items[i];
                if (item != null && string.Equals(item.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static string KeyFor(CampusGameplayRoom.FacilityRecord record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(record.FacilityId)
                ? record.FacilityId.Trim()
                : "service_window";
        }

        private static string BuildMealInstanceId(string customerId, string windowKey)
        {
            string owner = string.IsNullOrWhiteSpace(customerId) ? "customer" : customerId.Trim();
            string window = string.IsNullOrWhiteSpace(windowKey) ? "window" : windowKey.Trim();
            return owner + ".meal." + window + "." + Guid.NewGuid().ToString("N");
        }
    }
}
