using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    public enum CampusCommerceNeedType
    {
        None = 0,
        CanteenMeal = 1,
        StorePurchase = 2
    }

    public enum CampusCommerceStep
    {
        None = 0,
        QueueingCanteenMeal = 1,
        OrderingCanteenMeal = 2,
        ClerkPreparingCanteenMeal = 3,
        ReceivingCanteenMeal = 4,
        BrowsingStoreShelf = 5,
        SelectingStoreItem = 6,
        QueueingStoreCheckout = 7,
        PayingStoreCheckout = 8,
        ReceivingStorePurchase = 9,
        Completed = 10,
        Failed = 11
    }

    [DisallowMultipleComponent]
    public sealed class CampusCommerceService : MonoBehaviour
    {
        private const float NeedScanSeconds = 1.6f;
        private const float TransactionRetentionSeconds = 55f;
        private const float CounterReachDistance = 0.86f;
        private const float QueueReachDistance = 0.72f;
        private const float ShelfReachDistance = 0.78f;
        private const float CanteenOrderSeconds = 1.1f;
        private const float CanteenPrepareSeconds = 2.2f;
        private const float StoreSelectSeconds = 1.4f;
        private const float StorePaySeconds = 1.2f;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private List<CampusCommerceTransaction> transactions = new List<CampusCommerceTransaction>();
        [SerializeField] private List<string> mealCustomerIdsToday = new List<string>();
        [SerializeField] private List<string> storeCustomerIdsToday = new List<string>();
        [SerializeField] private string currentSummary = "Commerce service waiting for concrete needs.";
        [SerializeField, Min(0)] private int dailyCanteenMealsServed;
        [SerializeField, Min(0)] private int dailyStorePurchasesCompleted;
        [SerializeField] private int observedDay = -1;
        [SerializeField] private float nextNeedScanTime;

        public IReadOnlyList<CampusCommerceTransaction> Transactions => transactions;
        public string CurrentSummary => currentSummary;
        public int DailyCanteenMealsServed => dailyCanteenMealsServed;
        public int DailyStorePurchasesCompleted => dailyStorePurchasesCompleted;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            scheduleService = bootstrap != null ? bootstrap.ScheduleService : null;
            SyncDayState(true);
            nextNeedScanTime = Time.time + 0.8f;
        }

        public bool TryBuildCommerceDirective(CampusCharacterRuntime runtime, out CampusCharacterTaskDirective directive)
        {
            directive = null;
            if (runtime == null || runtime.Data == null)
            {
                return false;
            }

            CampusCommerceTransaction transaction = FindActiveTransactionForCustomer(runtime.CharacterId);
            if (transaction == null)
            {
                return false;
            }

            directive = new CampusCharacterTaskDirective
            {
                HoldRadius = 0.14f,
                DebugLabel = transaction.Step.ToString()
            };

            switch (transaction.Step)
            {
                case CampusCommerceStep.QueueingCanteenMeal:
                    directive.TaskType = CampusCharacterTaskType.QueueCanteenMeal;
                    directive.TargetRoomType = CampusRoomType.Canteen;
                    directive.PreferredFacilityType = CampusFacilityType.CanteenQueuePoint;
                    return true;
                case CampusCommerceStep.OrderingCanteenMeal:
                case CampusCommerceStep.ClerkPreparingCanteenMeal:
                case CampusCommerceStep.ReceivingCanteenMeal:
                    directive.TaskType = CampusCharacterTaskType.ReceiveCanteenMeal;
                    directive.TargetRoomType = CampusRoomType.Canteen;
                    directive.PreferredFacilityType = CampusFacilityType.CanteenCounter;
                    return true;
                case CampusCommerceStep.BrowsingStoreShelf:
                case CampusCommerceStep.SelectingStoreItem:
                    directive.TaskType = CampusCharacterTaskType.BrowseStoreShelf;
                    directive.TargetRoomType = CampusRoomType.Store;
                    directive.PreferredFacilityType = CampusFacilityType.StoreShelf;
                    return true;
                case CampusCommerceStep.QueueingStoreCheckout:
                    directive.TaskType = CampusCharacterTaskType.QueueStoreCheckout;
                    directive.TargetRoomType = CampusRoomType.Store;
                    directive.PreferredFacilityType = CampusFacilityType.StoreQueuePoint;
                    return true;
                case CampusCommerceStep.PayingStoreCheckout:
                case CampusCommerceStep.ReceivingStorePurchase:
                    directive.TaskType = CampusCharacterTaskType.PayStoreCheckout;
                    directive.TargetRoomType = CampusRoomType.Store;
                    directive.PreferredFacilityType = CampusFacilityType.StoreCheckout;
                    return true;
                default:
                    return false;
            }
        }

        public bool TryResolveCommerceTaskTarget(
            CampusCharacterRuntime runtime,
            CampusCharacterTaskType taskType,
            CampusGameplayRoom room,
            Vector3 anchor,
            out Vector3 target)
        {
            target = default;
            if (runtime == null)
            {
                return false;
            }

            if (taskType == CampusCharacterTaskType.WorkStoreCheckout)
            {
                return TryResolveFacilityWorldPosition(room, CampusFacilityType.StoreCheckout, out target);
            }

            CampusCommerceTransaction transaction = FindActiveTransactionForCustomer(runtime.CharacterId);
            return transaction != null && TryResolveTargetForTransaction(transaction, taskType, room, anchor, out target);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            SyncDayState(false);
            ScanStudentNeedsIfNeeded();
            RefreshQueueIndexes();
            ProcessTransactions();
            TrimFinishedTransactions();
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (bootstrap == null)
            {
                return;
            }

            rosterService = rosterService != null ? rosterService : bootstrap.RosterService;
            worldService = worldService != null ? worldService : bootstrap.WorldService;
            scheduleService = scheduleService != null ? scheduleService : bootstrap.ScheduleService;
        }

        private void SyncDayState(bool force)
        {
            int day = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 0;
            if (!force && observedDay == day)
            {
                return;
            }

            observedDay = day;
            mealCustomerIdsToday.Clear();
            storeCustomerIdsToday.Clear();
            dailyCanteenMealsServed = 0;
            dailyStorePurchasesCompleted = 0;
            transactions.RemoveAll(transaction => transaction == null || transaction.IsFinished);
        }

        private void ScanStudentNeedsIfNeeded()
        {
            if (Time.time < nextNeedScanTime || rosterService == null || worldService == null || scheduleService == null)
            {
                return;
            }

            nextNeedScanTime = Time.time + NeedScanSeconds;
            int createdCount = 0;
            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Student))
            {
                if (createdCount >= 2)
                {
                    return;
                }

                if (!CanCreateNeedFor(runtime))
                {
                    continue;
                }

                CampusCharacterData data = runtime.Data;
                if (IsMealWindowNow() &&
                    !ContainsId(mealCustomerIdsToday, runtime.CharacterId) &&
                    TryCreateCanteenMealRequest(runtime))
                {
                    mealCustomerIdsToday.Add(runtime.CharacterId);
                    createdCount++;
                    continue;
                }

                if (IsStoreWindowNow() &&
                    !ContainsId(storeCustomerIdsToday, runtime.CharacterId) &&
                    ShouldVisitStore(data) &&
                    TryCreateStorePurchaseRequest(runtime))
                {
                    storeCustomerIdsToday.Add(runtime.CharacterId);
                    createdCount++;
                }
            }
        }

        private bool CanCreateNeedFor(CampusCharacterRuntime runtime)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            return data != null &&
                   !data.IsPlayerControlled &&
                   data.State != CampusCharacterState.Punished &&
                   data.State != CampusCharacterState.Sleeping &&
                   FindActiveTransactionForCustomer(runtime.CharacterId) == null;
        }

        private bool TryCreateCanteenMealRequest(CampusCharacterRuntime customer)
        {
            CampusGameplayRoom room = FindServiceRoom(CampusRoomType.Canteen);
            if (room == null ||
                !HasFacility(room, CampusFacilityType.CanteenCounter) ||
                FindStaffWithDuty(CampusStaffDuty.CanteenClerk) == null)
            {
                return false;
            }

            string itemId = "canteen_meal_" + SelectStableIndex(customer, 3);
            string itemName = ResolveCanteenMealName(itemId);
            CampusCommerceTransaction transaction = CampusCommerceTransaction.Create(
                Guid.NewGuid().ToString("N"),
                CampusCommerceNeedType.CanteenMeal,
                CampusCommerceStep.QueueingCanteenMeal,
                customer.CharacterId,
                room.RoomId,
                itemId,
                itemName,
                0,
                Time.time);
            transactions.Add(transaction);
            WriteLog("[Canteen] " + FormatName(customer) + " walks to the meal queue for " + itemName + ".");
            return true;
        }

        private bool TryCreateStorePurchaseRequest(CampusCharacterRuntime customer)
        {
            CampusGameplayRoom room = FindServiceRoom(CampusRoomType.Store);
            if (room == null ||
                !HasFacility(room, CampusFacilityType.StoreShelf) ||
                !HasFacility(room, CampusFacilityType.StoreCheckout) ||
                FindStaffWithDuty(CampusStaffDuty.StoreOwner) == null)
            {
                return false;
            }

            string itemId = "store_item_" + SelectStableIndex(customer, 4);
            string itemName = ResolveStoreItemName(itemId);
            int price = ResolveStoreItemPrice(itemId);
            CampusCommerceTransaction transaction = CampusCommerceTransaction.Create(
                Guid.NewGuid().ToString("N"),
                CampusCommerceNeedType.StorePurchase,
                CampusCommerceStep.BrowsingStoreShelf,
                customer.CharacterId,
                room.RoomId,
                itemId,
                itemName,
                price,
                Time.time);
            transactions.Add(transaction);
            WriteLog("[Store] " + FormatName(customer) + " goes to the shelf for " + itemName + ".");
            return true;
        }

        private void RefreshQueueIndexes()
        {
            int canteenIndex = 0;
            int storeIndex = 0;
            for (int i = 0; i < transactions.Count; i++)
            {
                CampusCommerceTransaction transaction = transactions[i];
                if (transaction == null || transaction.IsFinished)
                {
                    continue;
                }

                if (transaction.Step == CampusCommerceStep.QueueingCanteenMeal)
                {
                    transaction.SetQueueIndex(canteenIndex++);
                }
                else if (transaction.Step == CampusCommerceStep.QueueingStoreCheckout)
                {
                    transaction.SetQueueIndex(storeIndex++);
                }
                else
                {
                    transaction.SetQueueIndex(0);
                }
            }
        }

        private void ProcessTransactions()
        {
            for (int i = 0; i < transactions.Count; i++)
            {
                CampusCommerceTransaction transaction = transactions[i];
                if (transaction == null || transaction.IsFinished)
                {
                    continue;
                }

                switch (transaction.NeedType)
                {
                    case CampusCommerceNeedType.CanteenMeal:
                        ProcessCanteenTransaction(transaction);
                        break;
                    case CampusCommerceNeedType.StorePurchase:
                        ProcessStoreTransaction(transaction);
                        break;
                }
            }
        }

        private void ProcessCanteenTransaction(CampusCommerceTransaction transaction)
        {
            CampusCharacterRuntime customer = rosterService != null ? rosterService.FindRuntime(transaction.CustomerId) : null;
            CampusGameplayRoom room = worldService != null ? worldService.FindRoomById(transaction.RoomId) : null;
            if (customer == null || customer.Data == null || room == null)
            {
                Fail(transaction, "customer or canteen disappeared");
                return;
            }

            CampusCharacterRuntime clerk = FindStaffWithDutyInRoom(CampusStaffDuty.CanteenClerk, room);
            switch (transaction.Step)
            {
                case CampusCommerceStep.QueueingCanteenMeal:
                    if (transaction.QueueIndex == 0 &&
                        clerk != null &&
                        !IsDutyBusy(CampusStaffDuty.CanteenClerk, transaction.RequestId) &&
                        IsRuntimeNearTaskTarget(customer, transaction, CampusCharacterTaskType.QueueCanteenMeal, room, QueueReachDistance) &&
                        IsRuntimeNearFacility(clerk, room, CampusFacilityType.CanteenCounter, CounterReachDistance))
                    {
                        transaction.AssignClerk(clerk.CharacterId);
                        transaction.SetStep(CampusCommerceStep.OrderingCanteenMeal, Time.time);
                        WriteLog("[Canteen] " + FormatName(clerk) + " takes " + FormatName(customer) + "'s order: " + transaction.ItemDisplayName + ".");
                    }
                    break;
                case CampusCommerceStep.OrderingCanteenMeal:
                    if (Elapsed(transaction) >= CanteenOrderSeconds)
                    {
                        transaction.SetStep(CampusCommerceStep.ClerkPreparingCanteenMeal, Time.time);
                        WriteLog("[Canteen] " + ResolveClerkName(transaction) + " starts preparing " + transaction.ItemDisplayName + ".");
                    }
                    break;
                case CampusCommerceStep.ClerkPreparingCanteenMeal:
                    if (Elapsed(transaction) >= CanteenPrepareSeconds)
                    {
                        transaction.SetStep(CampusCommerceStep.ReceivingCanteenMeal, Time.time);
                        WriteLog("[Canteen] " + ResolveClerkName(transaction) + " puts " + transaction.ItemDisplayName + " on the counter.");
                    }
                    break;
                case CampusCommerceStep.ReceivingCanteenMeal:
                    if (IsRuntimeNearTaskTarget(customer, transaction, CampusCharacterTaskType.ReceiveCanteenMeal, room, CounterReachDistance))
                    {
                        CompleteCanteenTransaction(transaction, customer);
                    }
                    break;
            }
        }

        private void ProcessStoreTransaction(CampusCommerceTransaction transaction)
        {
            CampusCharacterRuntime customer = rosterService != null ? rosterService.FindRuntime(transaction.CustomerId) : null;
            CampusGameplayRoom room = worldService != null ? worldService.FindRoomById(transaction.RoomId) : null;
            if (customer == null || customer.Data == null || room == null)
            {
                Fail(transaction, "customer or store disappeared");
                return;
            }

            switch (transaction.Step)
            {
                case CampusCommerceStep.BrowsingStoreShelf:
                    if (IsRuntimeNearTaskTarget(customer, transaction, CampusCharacterTaskType.BrowseStoreShelf, room, ShelfReachDistance))
                    {
                        transaction.SetStep(CampusCommerceStep.SelectingStoreItem, Time.time);
                        customer.Data.AddMemory(CampusCharacterMemoryId.SelectedStoreItem);
                        WriteLog("[Store] " + FormatName(customer) + " takes " + transaction.ItemDisplayName + " from the shelf.");
                    }
                    break;
                case CampusCommerceStep.SelectingStoreItem:
                    if (Elapsed(transaction) >= StoreSelectSeconds)
                    {
                        transaction.SetStep(CampusCommerceStep.QueueingStoreCheckout, Time.time);
                        WriteLog("[Store] " + FormatName(customer) + " carries " + transaction.ItemDisplayName + " to the checkout line.");
                    }
                    break;
                case CampusCommerceStep.QueueingStoreCheckout:
                    CampusCharacterRuntime cashier = FindStaffWithDutyInRoom(CampusStaffDuty.StoreOwner, room);
                    if (transaction.QueueIndex == 0 &&
                        cashier != null &&
                        !IsDutyBusy(CampusStaffDuty.StoreOwner, transaction.RequestId) &&
                        IsRuntimeNearTaskTarget(customer, transaction, CampusCharacterTaskType.QueueStoreCheckout, room, QueueReachDistance) &&
                        IsRuntimeNearFacility(cashier, room, CampusFacilityType.StoreCheckout, CounterReachDistance))
                    {
                        transaction.AssignClerk(cashier.CharacterId);
                        transaction.SetStep(CampusCommerceStep.PayingStoreCheckout, Time.time);
                        customer.Data.AddMemory(CampusCharacterMemoryId.PaidAtStoreCheckout);
                        WriteLog("[Store] " + FormatName(cashier) + " scans " + transaction.ItemDisplayName + " for " + FormatName(customer) + ". Price=" + transaction.Price + ".");
                    }
                    break;
                case CampusCommerceStep.PayingStoreCheckout:
                    if (Elapsed(transaction) >= StorePaySeconds)
                    {
                        transaction.SetStep(CampusCommerceStep.ReceivingStorePurchase, Time.time);
                        WriteLog("[Store] " + ResolveClerkName(transaction) + " takes payment and bags " + transaction.ItemDisplayName + ".");
                    }
                    break;
                case CampusCommerceStep.ReceivingStorePurchase:
                    if (IsRuntimeNearTaskTarget(customer, transaction, CampusCharacterTaskType.PayStoreCheckout, room, CounterReachDistance))
                    {
                        CompleteStoreTransaction(transaction, customer);
                    }
                    break;
            }
        }

        private void CompleteCanteenTransaction(CampusCommerceTransaction transaction, CampusCharacterRuntime customer)
        {
            customer.Data.AddPossession(transaction.ItemId, transaction.ItemDisplayName, "Canteen counter", CurrentDay());
            customer.Data.AddMemory(CampusCharacterMemoryId.ReceivedCanteenMeal);
            transaction.SetStep(CampusCommerceStep.Completed, Time.time);
            dailyCanteenMealsServed++;
            WriteLog("[Canteen] " + FormatName(customer) + " receives " + transaction.ItemDisplayName + " and leaves the line.");
        }

        private void CompleteStoreTransaction(CampusCommerceTransaction transaction, CampusCharacterRuntime customer)
        {
            customer.Data.AddPossession(transaction.ItemId, transaction.ItemDisplayName, "Store checkout", CurrentDay());
            customer.Data.AddMemory(CampusCharacterMemoryId.ReceivedStorePurchase);
            transaction.SetStep(CampusCommerceStep.Completed, Time.time);
            dailyStorePurchasesCompleted++;
            WriteLog("[Store] " + FormatName(customer) + " receives " + transaction.ItemDisplayName + " after checkout.");
        }

        private void Fail(CampusCommerceTransaction transaction, string reason)
        {
            if (transaction == null)
            {
                return;
            }

            transaction.SetStep(CampusCommerceStep.Failed, Time.time);
            WriteLog("[Commerce] " + transaction.CustomerId + " transaction failed: " + reason + ".");
        }

        private bool IsDutyBusy(CampusStaffDuty duty, string allowedRequestId)
        {
            for (int i = 0; i < transactions.Count; i++)
            {
                CampusCommerceTransaction transaction = transactions[i];
                if (transaction == null ||
                    transaction.IsFinished ||
                    string.Equals(transaction.RequestId, allowedRequestId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (duty == CampusStaffDuty.CanteenClerk &&
                    (transaction.Step == CampusCommerceStep.OrderingCanteenMeal ||
                     transaction.Step == CampusCommerceStep.ClerkPreparingCanteenMeal ||
                     transaction.Step == CampusCommerceStep.ReceivingCanteenMeal))
                {
                    return true;
                }

                if (duty == CampusStaffDuty.StoreOwner &&
                    (transaction.Step == CampusCommerceStep.PayingStoreCheckout ||
                     transaction.Step == CampusCommerceStep.ReceivingStorePurchase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveTargetForTransaction(
            CampusCommerceTransaction transaction,
            CampusCharacterTaskType taskType,
            CampusGameplayRoom room,
            Vector3 anchor,
            out Vector3 target)
        {
            target = anchor;
            if (transaction == null || room == null)
            {
                return false;
            }

            switch (taskType)
            {
                case CampusCharacterTaskType.QueueCanteenMeal:
                    target = ResolveQueueTarget(
                        room,
                        CampusFacilityType.CanteenQueuePoint,
                        CampusFacilityType.CanteenCounter,
                        transaction.QueueIndex,
                        0.62f,
                        anchor);
                    return true;
                case CampusCharacterTaskType.ReceiveCanteenMeal:
                    target = ResolveCounterCustomerTarget(room, CampusFacilityType.CanteenCounter, anchor);
                    return true;
                case CampusCharacterTaskType.BrowseStoreShelf:
                    target = TryResolveFacilityWorldPosition(room, CampusFacilityType.StoreShelf, out Vector3 shelf)
                        ? shelf + Vector3.down * 0.45f
                        : anchor;
                    return true;
                case CampusCharacterTaskType.QueueStoreCheckout:
                    target = ResolveQueueTarget(
                        room,
                        CampusFacilityType.StoreQueuePoint,
                        CampusFacilityType.StoreCheckout,
                        transaction.QueueIndex,
                        0.58f,
                        anchor);
                    return true;
                case CampusCharacterTaskType.PayStoreCheckout:
                    target = ResolveCounterCustomerTarget(room, CampusFacilityType.StoreCheckout, anchor);
                    return true;
                default:
                    return false;
            }
        }

        private Vector3 ResolveQueueTarget(
            CampusGameplayRoom room,
            CampusFacilityType queueType,
            CampusFacilityType fallbackType,
            int queueIndex,
            float spacing,
            Vector3 fallback)
        {
            Vector3 basePosition = TryResolveFacilityWorldPosition(room, queueType, out Vector3 explicitQueue)
                ? explicitQueue
                : TryResolveFacilityWorldPosition(room, fallbackType, out Vector3 fallbackFacility)
                    ? fallbackFacility + Vector3.down * 0.75f
                    : fallback;
            return basePosition + Vector3.down * Mathf.Max(0, queueIndex) * spacing;
        }

        private Vector3 ResolveCounterCustomerTarget(CampusGameplayRoom room, CampusFacilityType counterType, Vector3 fallback)
        {
            return TryResolveFacilityWorldPosition(room, counterType, out Vector3 counter)
                ? counter + Vector3.down * 0.55f
                : fallback;
        }

        private bool IsRuntimeNearTaskTarget(
            CampusCharacterRuntime runtime,
            CampusCommerceTransaction transaction,
            CampusCharacterTaskType taskType,
            CampusGameplayRoom room,
            float distance)
        {
            if (runtime == null ||
                !TryResolveTargetForTransaction(transaction, taskType, room, runtime.transform.position, out Vector3 target))
            {
                return false;
            }

            return Vector2.Distance(runtime.transform.position, target) <= distance;
        }

        private bool IsRuntimeNearFacility(
            CampusCharacterRuntime runtime,
            CampusGameplayRoom room,
            CampusFacilityType facilityType,
            float distance)
        {
            return runtime != null &&
                   TryResolveFacilityWorldPosition(room, facilityType, out Vector3 target) &&
                   Vector2.Distance(runtime.transform.position, target) <= distance;
        }

        private bool TryResolveFacilityWorldPosition(CampusGameplayRoom room, CampusFacilityType facilityType, out Vector3 position)
        {
            position = default;
            CampusGameplayRoom.FacilityRecord record = FindFacility(room, facilityType);
            if (record == null)
            {
                return false;
            }

            position = new Vector3(record.Cell.x + 0.5f, record.Cell.y + 0.5f, 0f);
            return true;
        }

        private CampusGameplayRoom.FacilityRecord FindFacility(CampusGameplayRoom room, CampusFacilityType facilityType)
        {
            if (room == null || room.Facilities == null)
            {
                return null;
            }

            for (int i = 0; i < room.Facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord record = room.Facilities[i];
                if (record != null && record.FacilityType == facilityType)
                {
                    return record;
                }
            }

            return null;
        }

        private bool HasFacility(CampusGameplayRoom room, CampusFacilityType facilityType)
        {
            return room != null && room.GetFacilityCount(facilityType) > 0;
        }

        private CampusGameplayRoom FindServiceRoom(CampusRoomType roomType)
        {
            return worldService != null ? worldService.FindFirstUsableRoom(roomType) ?? worldService.FindFirstRoom(roomType) : null;
        }

        private CampusCharacterRuntime FindStaffWithDuty(CampusStaffDuty duty)
        {
            if (rosterService == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Staff))
            {
                if (runtime != null && runtime.Data != null && (runtime.Data.StaffDuty & duty) != 0)
                {
                    return runtime;
                }
            }

            return null;
        }

        private CampusCharacterRuntime FindStaffWithDutyInRoom(CampusStaffDuty duty, CampusGameplayRoom room)
        {
            if (rosterService == null || worldService == null || room == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Staff))
            {
                if (runtime == null || runtime.Data == null || (runtime.Data.StaffDuty & duty) == 0)
                {
                    continue;
                }

                CampusGameplayRoom staffRoom = worldService.FindRoomForRuntime(runtime);
                if (staffRoom != null && string.Equals(staffRoom.RoomId, room.RoomId, StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private CampusCommerceTransaction FindActiveTransactionForCustomer(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return null;
            }

            for (int i = 0; i < transactions.Count; i++)
            {
                CampusCommerceTransaction transaction = transactions[i];
                if (transaction != null &&
                    !transaction.IsFinished &&
                    string.Equals(transaction.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
                {
                    return transaction;
                }
            }

            return null;
        }

        private void TrimFinishedTransactions()
        {
            float now = Time.time;
            for (int i = transactions.Count - 1; i >= 0; i--)
            {
                CampusCommerceTransaction transaction = transactions[i];
                if (transaction == null ||
                    (transaction.IsFinished && now - transaction.StepStartedAt >= TransactionRetentionSeconds))
                {
                    transactions.RemoveAt(i);
                }
            }
        }

        private bool IsMealWindowNow()
        {
            CampusTimeSegment segment = scheduleService != null && scheduleService.TimeController != null
                ? scheduleService.TimeController.CurrentSegment
                : CampusTimeSegment.WakeUp;
            return segment == CampusTimeSegment.LunchBreak ||
                   segment == CampusTimeSegment.DinnerBreak;
        }

        private bool IsStoreWindowNow()
        {
            CampusTimeSegment segment = scheduleService != null && scheduleService.TimeController != null
                ? scheduleService.TimeController.CurrentSegment
                : CampusTimeSegment.WakeUp;
            switch (segment)
            {
                case CampusTimeSegment.MorningBreak1:
                case CampusTimeSegment.MorningExerciseBreak:
                case CampusTimeSegment.MorningBreak2:
                case CampusTimeSegment.AfternoonBreak1:
                case CampusTimeSegment.AfternoonBreak2:
                case CampusTimeSegment.AfternoonBreak3:
                case CampusTimeSegment.DinnerBreak:
                case CampusTimeSegment.EveningBreak1:
                case CampusTimeSegment.EveningBreak2:
                case CampusTimeSegment.NightFree:
                    return true;
                default:
                    return false;
            }
        }

        private bool ShouldVisitStore(CampusCharacterData data)
        {
            if (data == null)
            {
                return false;
            }

            int chancePercent = 18 + Mathf.Clamp(data.Mischief / 6, 0, 12);
            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                chancePercent += 12;
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                chancePercent -= 8;
            }

            int bucket = Mathf.FloorToInt(Time.time / NeedScanSeconds);
            return PseudoRandom01(StableHash(data.Id) + bucket * 31, 271) <= Mathf.Clamp01(chancePercent / 100f);
        }

        private int SelectStableIndex(CampusCharacterRuntime runtime, int count)
        {
            int safeCount = Mathf.Max(1, count);
            int seed = StableHash(runtime != null ? runtime.CharacterId : string.Empty) + CurrentDay() * 97;
            return Mathf.Abs(seed) % safeCount;
        }

        private static string ResolveCanteenMealName(string itemId)
        {
            switch (itemId)
            {
                case "canteen_meal_0":
                    return "rice plate";
                case "canteen_meal_1":
                    return "noodle bowl";
                default:
                    return "malatang bowl";
            }
        }

        private static string ResolveStoreItemName(string itemId)
        {
            switch (itemId)
            {
                case "store_item_0":
                    return "mineral water";
                case "store_item_1":
                    return "notebook";
                case "store_item_2":
                    return "spicy chips";
                default:
                    return "pencil pack";
            }
        }

        private static int ResolveStoreItemPrice(string itemId)
        {
            switch (itemId)
            {
                case "store_item_0":
                    return 3;
                case "store_item_1":
                    return 6;
                case "store_item_2":
                    return 5;
                default:
                    return 4;
            }
        }

        private string ResolveClerkName(CampusCommerceTransaction transaction)
        {
            CampusCharacterRuntime clerk = rosterService != null ? rosterService.FindRuntime(transaction.ClerkId) : null;
            return FormatName(clerk);
        }

        private static string FormatName(CampusCharacterRuntime runtime)
        {
            return runtime != null && runtime.Data != null
                ? runtime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)
                : "Actor";
        }

        private int CurrentDay()
        {
            return bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 0;
        }

        private static float Elapsed(CampusCommerceTransaction transaction)
        {
            return transaction != null ? Time.time - transaction.StepStartedAt : 0f;
        }

        private static bool ContainsId(List<string> values, string id)
        {
            if (values == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void WriteLog(string message)
        {
            currentSummary = message;
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
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
    }

    [Serializable]
    public sealed class CampusCommerceTransaction
    {
        [SerializeField] private string requestId = string.Empty;
        [SerializeField] private CampusCommerceNeedType needType;
        [SerializeField] private CampusCommerceStep step;
        [SerializeField] private string customerId = string.Empty;
        [SerializeField] private string clerkId = string.Empty;
        [SerializeField] private string roomId = string.Empty;
        [SerializeField] private string itemId = string.Empty;
        [SerializeField] private string itemDisplayName = string.Empty;
        [SerializeField, Min(0)] private int price;
        [SerializeField, Min(0)] private int queueIndex;
        [SerializeField] private float createdAt;
        [SerializeField] private float stepStartedAt;

        public string RequestId => requestId;
        public CampusCommerceNeedType NeedType => needType;
        public CampusCommerceStep Step => step;
        public string CustomerId => customerId;
        public string ClerkId => clerkId;
        public string RoomId => roomId;
        public string ItemId => itemId;
        public string ItemDisplayName => itemDisplayName;
        public int Price => price;
        public int QueueIndex => queueIndex;
        public float CreatedAt => createdAt;
        public float StepStartedAt => stepStartedAt;
        public bool IsFinished => step == CampusCommerceStep.Completed || step == CampusCommerceStep.Failed;

        public static CampusCommerceTransaction Create(
            string id,
            CampusCommerceNeedType transactionNeedType,
            CampusCommerceStep initialStep,
            string transactionCustomerId,
            string transactionRoomId,
            string transactionItemId,
            string transactionItemDisplayName,
            int transactionPrice,
            float now)
        {
            CampusCommerceTransaction transaction = new CampusCommerceTransaction();
            transaction.requestId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
            transaction.needType = transactionNeedType;
            transaction.step = initialStep;
            transaction.customerId = string.IsNullOrWhiteSpace(transactionCustomerId) ? string.Empty : transactionCustomerId.Trim();
            transaction.roomId = string.IsNullOrWhiteSpace(transactionRoomId) ? string.Empty : transactionRoomId.Trim();
            transaction.itemId = string.IsNullOrWhiteSpace(transactionItemId) ? string.Empty : transactionItemId.Trim();
            transaction.itemDisplayName = string.IsNullOrWhiteSpace(transactionItemDisplayName)
                ? transaction.itemId
                : transactionItemDisplayName.Trim();
            transaction.price = Mathf.Max(0, transactionPrice);
            transaction.createdAt = now;
            transaction.stepStartedAt = now;
            return transaction;
        }

        public void SetStep(CampusCommerceStep nextStep, float now)
        {
            step = nextStep;
            stepStartedAt = now;
        }

        public void AssignClerk(string transactionClerkId)
        {
            clerkId = string.IsNullOrWhiteSpace(transactionClerkId) ? string.Empty : transactionClerkId.Trim();
        }

        public void SetQueueIndex(int value)
        {
            queueIndex = Mathf.Max(0, value);
        }
    }
}
