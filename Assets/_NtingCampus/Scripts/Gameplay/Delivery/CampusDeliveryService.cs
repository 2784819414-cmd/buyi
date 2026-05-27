using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Delivery
{
    internal enum CampusDeliveryOrderState
    {
        Ordered = 0,
        Delivered = 1,
        Claimed = 2,
        Stolen = 3
    }

    internal sealed class CampusDeliveryOrder
    {
        public string ActorId = string.Empty;
        public string MealId = string.Empty;
        public int Day;
        public int OrderedMinute;
        public int DeliveryMinute;
        public string ItemInstanceId = string.Empty;
        public CampusDeliveryOrderState State;
    }

    [DisallowMultipleComponent]
    public sealed class CampusDeliveryService : MonoBehaviour
    {
        private const float DeliveryCheckIntervalSeconds = 1.2f;
        private const float AngryMarkDurationSeconds = 2.4f;

        [SerializeField] private CampusGameBootstrap bootstrap;

        private readonly Dictionary<string, CampusDeliveryOrder> orders =
            new Dictionary<string, CampusDeliveryOrder>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> skippedOrderKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CampusGameplayEventHub subscribedEventHub;
        private CampusTimeController subscribedTimeController;
        private float nextDeliveryCheckTime;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            Subscribe();
        }

        public bool HasDeliverySpot()
        {
            return TryResolveDeliverySpot(out _, out _);
        }

        public bool CanPlaceOrder(CampusCharacterRuntime actor)
        {
            if (actor == null || actor.Data == null || actor.Data.IsPlayerControlled)
            {
                return false;
            }

            string mealId = ResolveUpcomingMeal();
            if (string.IsNullOrEmpty(mealId) ||
                HasAnyMealOrder(actor.CharacterId, mealId) ||
                skippedOrderKeys.Contains(BuildMealKey(actor.CharacterId, ResolveDay(), mealId)) ||
                !HasDeliverySpot())
            {
                return false;
            }

            return ShouldOrderForMeal(actor, mealId);
        }

        public bool TryPlaceOrder(CampusCharacterRuntime actor)
        {
            if (!CanPlaceOrder(actor))
            {
                MarkSkipped(actor, ResolveUpcomingMeal());
                return false;
            }

            string mealId = ResolveUpcomingMeal();
            CampusDeliveryRules rules = CampusDeliveryPresetCatalog.Rules;
            int now = ResolveCurrentMinute();
            int delay = ResolveDeliveryDelay(actor.CharacterId, mealId, rules);
            CampusDeliveryOrder order = new CampusDeliveryOrder
            {
                ActorId = actor.CharacterId,
                MealId = mealId,
                Day = ResolveDay(),
                OrderedMinute = now,
                DeliveryMinute = CampusTimeSchedule.NormalizeMinuteOfDay(now + delay),
                ItemInstanceId = BuildItemInstanceId(actor.CharacterId, mealId, now),
                State = CampusDeliveryOrderState.Ordered
            };

            orders[BuildMealKey(order.ActorId, order.Day, order.MealId)] = order;
            Debug.Log(CampusDeliveryTextCatalog.Format(
                CampusDeliveryTextId.OrderPlacedConsole,
                ResolveActorName(actor),
                actor.CharacterId,
                order.MealId,
                CampusTimeSchedule.FormatClockMinute(order.OrderedMinute),
                CampusTimeSchedule.FormatClockMinute(order.DeliveryMinute)));
            WriteLog(CampusDeliveryTextCatalog.Format(
                CampusDeliveryTextId.OrderPlacedLog,
                ResolveActorName(actor)));
            return true;
        }

        public bool HasActiveOrder(CampusCharacterRuntime actor)
        {
            CampusDeliveryOrder order = ResolveMealOrder(actor);
            return order != null &&
                   (order.State == CampusDeliveryOrderState.Ordered ||
                    order.State == CampusDeliveryOrderState.Delivered);
        }

        public bool HasDeliveredOrder(CampusCharacterRuntime actor)
        {
            CampusDeliveryOrder order = ResolveMealOrder(actor);
            return order != null && order.State == CampusDeliveryOrderState.Delivered;
        }

        public bool ShouldSkipCanteen(CampusCharacterRuntime actor)
        {
            CampusDeliveryOrder order = ResolveMealOrder(actor);
            return order != null &&
                   order.State != CampusDeliveryOrderState.Stolen;
        }

        private void Update()
        {
            Subscribe();
            if (Time.time < nextDeliveryCheckTime)
            {
                return;
            }

            nextDeliveryCheckTime = Time.time + DeliveryCheckIntervalSeconds;
            DeliverDueOrders();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void DeliverDueOrders()
        {
            if (orders.Count == 0)
            {
                return;
            }

            List<CampusDeliveryOrder> pending = new List<CampusDeliveryOrder>(orders.Values);
            for (int i = 0; i < pending.Count; i++)
            {
                CampusDeliveryOrder order = pending[i];
                if (order == null || order.State != CampusDeliveryOrderState.Ordered || !IsOrderDue(order))
                {
                    continue;
                }

                TryDeliver(order);
            }
        }

        private bool TryDeliver(CampusDeliveryOrder order)
        {
            if (order == null ||
                !TryResolveDeliverySpot(out CampusPlacedObject spot, out CampusGameplayRoom room) ||
                !TryCreateDeliveryItem(order, room, out StorageItemModel item))
            {
                return false;
            }

            Vector3 position = ResolveDeliveryPosition(spot, order);
            if (!CampusStorageGroundItemUtility.TryPlaceItemAtWorldPosition(
                    spot.gameObject,
                    item,
                    position,
                    out _,
                    out _))
            {
                return false;
            }

            order.State = CampusDeliveryOrderState.Delivered;
            WriteLog(CampusDeliveryTextCatalog.Format(
                CampusDeliveryTextId.DeliveryArrivedLog,
                ResolveActorName(order.ActorId)));
            return true;
        }

        private bool TryCreateDeliveryItem(
            CampusDeliveryOrder order,
            CampusGameplayRoom room,
            out StorageItemModel item)
        {
            item = null;
            StorageMemory memory = StorageMemory.GetOrCreate();
            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            CampusDeliveryRules rules = CampusDeliveryPresetCatalog.Rules;
            if (registry == null ||
                !registry.TryGetDefinition(rules.ItemDefinitionId, out StorageItemDefinition definition) ||
                definition == null)
            {
                return false;
            }

            item = registry.CreateItem(rules.ItemDefinitionId, order.ItemInstanceId);
            if (item == null)
            {
                return false;
            }

            item.OwnerId = order.ActorId;
            item.LegalState = StorageItemLegalState.Personal;
            item.SourceLocation = rules.SourceLocationId;
            item.SourceRoomId = room != null ? room.RoomId : string.Empty;
            item.SourceContainerId = BuildSourceContainerId(order);
            return true;
        }

        private bool TryResolveDeliverySpot(out CampusPlacedObject spot, out CampusGameplayRoom room)
        {
            spot = null;
            room = null;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            if (worldService == null)
            {
                return false;
            }

            CampusDeliveryRules rules = CampusDeliveryPresetCatalog.Rules;
            List<CampusGameplayRoom> outdoorRooms = worldService.GetRoomsByType(CampusRoomType.Outdoor, true);
            for (int roomIndex = 0; roomIndex < outdoorRooms.Count; roomIndex++)
            {
                CampusGameplayRoom candidateRoom = outdoorRooms[roomIndex];
                if (candidateRoom == null)
                {
                    continue;
                }

                IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = candidateRoom.Facilities;
                for (int facilityIndex = 0; facilityIndex < facilities.Count; facilityIndex++)
                {
                    CampusGameplayRoom.FacilityRecord facility = facilities[facilityIndex];
                    CampusPlacedObject placed = facility != null ? facility.PlacedObject : null;
                    if (placed == null)
                    {
                        continue;
                    }

                    if (IdEquals(placed.ObjectId, rules.DeliverySpotObjectId) ||
                        IdEquals(placed.InteractionPresetEid, rules.DeliverySpotInteractionPresetEid))
                    {
                        spot = placed;
                        room = candidateRoom;
                        return true;
                    }
                }
            }

            return false;
        }

        private void HandleItemTransferred(CampusItemTransferredEvent eventData)
        {
            if (string.IsNullOrWhiteSpace(eventData.ItemInstanceId))
            {
                return;
            }

            foreach (CampusDeliveryOrder order in orders.Values)
            {
                if (order == null ||
                    order.State != CampusDeliveryOrderState.Delivered ||
                    !IdEquals(order.ItemInstanceId, eventData.ItemInstanceId))
                {
                    continue;
                }

                if (IdEquals(order.ActorId, eventData.ActorId))
                {
                    order.State = CampusDeliveryOrderState.Claimed;
                    return;
                }

                order.State = CampusDeliveryOrderState.Stolen;
                NotifyDeliveryStolen(order);
                return;
            }
        }

        private void NotifyDeliveryStolen(CampusDeliveryOrder order)
        {
            CampusCharacterRuntime owner = bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.FindRuntime(order.ActorId)
                : null;
            if (owner != null)
            {
                CampusCharacterSpeechUtility.Speak(
                    owner,
                    CampusDeliveryTextCatalog.Get(CampusDeliveryTextId.AngryMark),
                    AngryMarkDurationSeconds);
                CampusNpcAiHost host = owner.GetComponent<CampusNpcAiHost>();
                host?.RequestDecisionSoon();
            }

            WriteLog(CampusDeliveryTextCatalog.Format(
                CampusDeliveryTextId.DeliveryStolenLog,
                ResolveActorName(order.ActorId)));
        }

        private CampusDeliveryOrder ResolveMealOrder(CampusCharacterRuntime actor)
        {
            return actor == null ? null : ResolveMealOrder(actor.CharacterId);
        }

        private CampusDeliveryOrder ResolveMealOrder(string actorId)
        {
            string currentMeal = ResolveCurrentMeal();
            if (string.IsNullOrEmpty(currentMeal))
            {
                return null;
            }

            orders.TryGetValue(BuildMealKey(actorId, ResolveDay(), currentMeal), out CampusDeliveryOrder order);
            return order;
        }

        private bool HasAnyMealOrder(string actorId, string mealId)
        {
            return orders.ContainsKey(BuildMealKey(actorId, ResolveDay(), mealId));
        }

        private bool ShouldOrderForMeal(CampusCharacterRuntime actor, string mealId)
        {
            int chance = CampusDeliveryPresetCatalog.Rules.OrderChancePercent;
            if (chance <= 0)
            {
                return false;
            }

            if (chance >= 100)
            {
                return true;
            }

            int roll = PositiveModulo(Hash(actor.CharacterId) + ResolveDay() * 97 + Hash(mealId) * 131, 100);
            return roll < chance;
        }

        private void MarkSkipped(CampusCharacterRuntime actor, string mealId)
        {
            if (actor == null || string.IsNullOrEmpty(mealId))
            {
                return;
            }

            skippedOrderKeys.Add(BuildMealKey(actor.CharacterId, ResolveDay(), mealId));
        }

        private bool IsOrderDue(CampusDeliveryOrder order)
        {
            int now = ResolveCurrentMinute();
            int ordered = order.OrderedMinute;
            int due = order.DeliveryMinute;
            return ordered <= due
                ? now >= due || now < ordered
                : now >= due && now < ordered;
        }

        private int ResolveDeliveryDelay(string actorId, string mealId, CampusDeliveryRules rules)
        {
            int range = Mathf.Max(1, rules.MaxDeliveryMinutes - rules.MinDeliveryMinutes + 1);
            int offset = PositiveModulo(Hash(actorId) + ResolveDay() * 53 + Hash(mealId) * 197, range);
            return rules.MinDeliveryMinutes + offset;
        }

        private string ResolveUpcomingMeal()
        {
            return CampusDeliveryPresetCatalog.ResolveUpcomingMeal(ResolveCurrentMinute());
        }

        private string ResolveCurrentMeal()
        {
            CampusTimeSegment segment = bootstrap != null && bootstrap.TimeController != null
                ? bootstrap.TimeController.CurrentSegment
                : CampusTimeSegment.MorningClass1;
            return CampusDeliveryPresetCatalog.ResolveActiveMeal(segment);
        }

        private int ResolveCurrentMinute()
        {
            return bootstrap != null && bootstrap.TimeController != null
                ? Mathf.FloorToInt(Mathf.Repeat(bootstrap.TimeController.CurrentGameHour * 60f, 24f * 60f))
                : 0;
        }

        private int ResolveDay()
        {
            return bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 1;
        }

        private string BuildMealKey(string actorId, int day, string mealId)
        {
            return CleanId(actorId) + ":d" + Mathf.Max(1, day) + ":" + CleanId(mealId);
        }

        private static string BuildItemInstanceId(string actorId, string mealId, int orderedMinute)
        {
            return CleanId(actorId) + ".delivery." + CleanId(mealId) + "." + orderedMinute.ToString("0000");
        }

        private static string BuildSourceContainerId(CampusDeliveryOrder order)
        {
            string prefix = CampusDeliveryPresetCatalog.Rules.SourceContainerPrefix;
            return prefix + "." + CleanId(order.ActorId) + ".d" + Mathf.Max(1, order.Day) + "." + CleanId(order.MealId);
        }

        private static Vector3 ResolveDeliveryPosition(CampusPlacedObject spot, CampusDeliveryOrder order)
        {
            int slot = PositiveModulo(Hash(order.ActorId) + Hash(order.MealId) * 7, 5);
            float x = (slot - 2) * 0.18f;
            return spot.transform.TransformPoint(new Vector3(x, -0.12f, 0f));
        }

        private string ResolveActorName(CampusCharacterRuntime actor)
        {
            return actor != null && actor.Data != null
                ? actor.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)
                : string.Empty;
        }

        private string ResolveActorName(string actorId)
        {
            CampusCharacterRuntime actor = bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.FindRuntime(actorId)
                : null;
            return actor != null ? ResolveActorName(actor) : actorId;
        }

        private void HandleGameDateChanged(CampusGameDate date)
        {
            orders.Clear();
            skippedOrderKeys.Clear();
        }

        private void Subscribe()
        {
            CampusGameplayEventHub eventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;
            if (subscribedEventHub != eventHub)
            {
                if (subscribedEventHub != null)
                {
                    subscribedEventHub.ItemTransferred -= HandleItemTransferred;
                }

                subscribedEventHub = eventHub;
                if (subscribedEventHub != null)
                {
                    subscribedEventHub.ItemTransferred += HandleItemTransferred;
                }
            }

            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            if (subscribedTimeController != timeController)
            {
                if (subscribedTimeController != null)
                {
                    subscribedTimeController.GameDateChanged -= HandleGameDateChanged;
                }

                subscribedTimeController = timeController;
                if (subscribedTimeController != null)
                {
                    subscribedTimeController.GameDateChanged += HandleGameDateChanged;
                }
            }
        }

        private void Unsubscribe()
        {
            if (subscribedEventHub != null)
            {
                subscribedEventHub.ItemTransferred -= HandleItemTransferred;
                subscribedEventHub = null;
            }

            if (subscribedTimeController != null)
            {
                subscribedTimeController.GameDateChanged -= HandleGameDateChanged;
                subscribedTimeController = null;
            }
        }

        private void WriteLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog(message);
            }
        }

        private static bool IdEquals(string left, string right)
        {
            return string.Equals(CleanId(left), CleanId(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string CleanId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static int Hash(string value)
        {
            unchecked
            {
                int hash = 23;
                string normalized = CleanId(value);
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash = hash * 31 + char.ToUpperInvariant(normalized[i]);
                }

                return hash;
            }
        }

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
