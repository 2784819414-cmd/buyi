using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    [DisallowMultipleComponent]
    public sealed class CampusCommerceService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private List<CampusStoreCatalogEntry> catalog = CampusStoreCatalogEntry.CreateDefaultCatalog();
        [SerializeField] private bool logValidationIssues = true;
        [SerializeField, Min(1)] private int defaultShelfTargetItemCount = 6;
        [SerializeField, Min(0)] private int defaultStoreSuspicionRisk = 10;
        [SerializeField, Min(0.25f)] private float shelfRestockIntervalSeconds = 2.5f;
        [SerializeField, Min(0.25f)] private float theftAuditIntervalSeconds = 1.1f;
        [SerializeField] private List<string> checkedOutActorIdsToday = new List<string>();
        [SerializeField] private string currentSummary = string.Empty;
        [SerializeField, Min(0)] private int dailyStorePurchasesCompleted;
        [SerializeField, Min(0)] private int dailyStoreTheftCount;
        [SerializeField, Min(0)] private int unpaidStoreItemCount;
        [SerializeField, Min(0)] private int knownStoreShelfCount;
        [SerializeField, Min(0)] private int lastRestockedItemCount;
        [SerializeField] private int observedDay = -1;
        [SerializeField] private float nextShelfRestockTime;
        [SerializeField] private float nextTheftAuditTime;

        private readonly HashSet<string> warningKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CampusEcologyValidator.ValidationIssue> validationIssues =
            new List<CampusEcologyValidator.ValidationIssue>();

        private CampusStoreFacts facts;
        private CampusStoreStockService stock;
        private CampusStoreAuditService audit;
        private CampusStoreActions actions;
        private bool usingRuntimeFallbackCatalog;
        private bool hasValidatedSetup;

        public string CurrentSummary => currentSummary;
        public IReadOnlyList<CampusStoreCatalogEntry> Catalog => catalog;
        public bool IsUsingRuntimeFallbackCatalog => usingRuntimeFallbackCatalog;
        public IReadOnlyList<CampusEcologyValidator.ValidationIssue> ValidationIssues => validationIssues;
        public int DailyCanteenMealsServed
        {
            get
            {
                CampusCanteenService canteen = CampusCanteenService.Resolve(false);
                return canteen != null ? canteen.DailyMealsServed : 0;
            }
        }
        public int DailyStorePurchasesCompleted => dailyStorePurchasesCompleted;
        public int DailyStoreTheftCount => dailyStoreTheftCount;
        public int UnpaidStoreItemCount => unpaidStoreItemCount;
        public int KnownStoreShelfCount => knownStoreShelfCount;
        public int LastRestockedItemCount => lastRestockedItemCount;

        public static CampusCommerceService Resolve(bool createIfMissing = true)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.CommerceService != null)
            {
                return bootstrap.CommerceService;
            }

            CampusCommerceService existing =
                FindFirstObjectByType<CampusCommerceService>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.Initialize(bootstrap);
                return existing;
            }

            if (!createIfMissing)
            {
                return null;
            }

            GameObject host = bootstrap != null ? bootstrap.gameObject : new GameObject("CampusCommerceService");
            CampusCommerceService service = host.AddComponent<CampusCommerceService>();
            service.Initialize(bootstrap);
            return service;
        }

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            CampusGameBootstrap resolvedBootstrap =
                targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            bool bootstrapChanged = bootstrap != resolvedBootstrap;
            bootstrap = resolvedBootstrap;
            ResolveReferences();
            EnsureCatalog();
            SyncDay(true);
            if (!hasValidatedSetup || bootstrapChanged)
            {
                ValidateSetup(logValidationIssues);
            }

            nextShelfRestockTime = Time.time + 0.3f;
            nextTheftAuditTime = Time.time + 0.7f;
            UpdateSummary();
        }

        public string ResolveShelfContainerId(CampusPlacedObject shelf)
        {
            ResolveReferences();
            return stock != null ? stock.ResolveShelfContainerId(shelf) : "store_shelf_missing";
        }

        public bool TryPrepareShelfStorage(
            CampusPlacedObject shelf,
            StorageContainerModel container,
            out string message)
        {
            ResolveReferences();
            EnsureCatalog();
            if (stock == null)
            {
                message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.MissingShelfOrContainer);
                return false;
            }

            bool prepared = stock.TryPrepareShelfStorage(shelf, container, out int added, out message);
            if (added > 0)
            {
                lastRestockedItemCount += added;
            }

            return prepared;
        }

        public bool TryCheckout(GameObject actor, CampusPlacedObject checkout, out string message)
        {
            return TryCheckout(ResolveActorRuntime(actor), checkout, out message);
        }

        public bool TryCheckout(CampusCharacterRuntime actor, CampusPlacedObject checkout, out string message)
        {
            ResolveReferences();
            if (actions != null)
            {
                return actions.TryCheckout(actor, checkout, out message);
            }

            message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.MissingActorOrCheckout);
            return false;
        }

        public bool TryTakeOneItemFromShelf(
            CampusCharacterRuntime actor,
            CampusPlacedObject shelf,
            out string message)
        {
            ResolveReferences();
            if (actions != null)
            {
                return actions.TryTakeOneItemFromShelf(actor, shelf, out message);
            }

            message = CampusCommerceTextCatalog.Get(CampusCommerceTextId.MissingActorOrShelf);
            return false;
        }

        public bool TryFindShelfBrowseTarget(
            CampusCharacterRuntime actor,
            string preferredCategoryId,
            out CampusPlacedObject shelf,
            out string roomId,
            out Vector3 targetPosition)
        {
            shelf = null;
            roomId = string.Empty;
            targetPosition = Vector3.zero;
            return facts != null &&
                   facts.TryFindShelfBrowseTarget(
                       actor,
                       preferredCategoryId,
                       out shelf,
                       out roomId,
                       out targetPosition);
        }

        public bool TryFindCheckoutTarget(
            CampusCharacterRuntime actor,
            out CampusPlacedObject checkout,
            out string roomId,
            out Vector3 targetPosition)
        {
            checkout = null;
            roomId = string.Empty;
            targetPosition = Vector3.zero;
            return facts != null &&
                   facts.TryFindCheckoutTarget(actor, out checkout, out roomId, out targetPosition);
        }

        public bool ActorHasUnpaidStoreItems(CampusCharacterRuntime actor)
        {
            ResolveReferences();
            return audit != null && audit.ActorHasUnpaidStoreItems(actor);
        }

        public bool HasCheckedOutStoreToday(CampusCharacterRuntime actor)
        {
            return actor != null && ContainsId(checkedOutActorIdsToday, actor.CharacterId);
        }

        public bool IsStoreOpenNow()
        {
            return facts != null && facts.IsStoreOpenNow();
        }

        public bool IsUnpaidStoreItem(StorageItemModel item)
        {
            ResolveReferences();
            return audit != null && audit.IsUnpaidStoreItem(item);
        }

        public IReadOnlyList<CampusEcologyValidator.ValidationIssue> ValidateSetup(bool logIssues)
        {
            ResolveReferences();
            EnsureCatalog();
            validationIssues.Clear();

            List<CampusEcologyValidator.ValidationIssue> issues =
                CampusStoreValidator.Validate(worldService, catalog, usingRuntimeFallbackCatalog);
            for (int i = 0; i < issues.Count; i++)
            {
                validationIssues.Add(issues[i]);
            }

            if (logIssues)
            {
                CampusStoreValidator.LogIssues(validationIssues);
            }

            hasValidatedSetup = true;
            return validationIssues;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            EnsureCatalog();
            SyncDay(false);
            RestockShelvesIfDue();
            AuditUnpaidItemsIfDue();
            UpdateSummary();
        }

        private void OnValidate()
        {
            defaultShelfTargetItemCount = Mathf.Max(1, defaultShelfTargetItemCount);
            defaultStoreSuspicionRisk = Mathf.Max(0, defaultStoreSuspicionRisk);
            shelfRestockIntervalSeconds = Mathf.Max(0.25f, shelfRestockIntervalSeconds);
            theftAuditIntervalSeconds = Mathf.Max(0.25f, theftAuditIntervalSeconds);
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    catalog[i]?.Normalize();
                }
            }
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
            gameplayEventHub = gameplayEventHub != null ? gameplayEventHub : bootstrap.GameplayEventHub;

            if (facts == null)
            {
                facts = new CampusStoreFacts();
            }

            facts.SetContext(
                rosterService,
                worldService,
                scheduleService,
                RememberWarning,
                ResolveShelfContainerId);

            if (stock == null)
            {
                stock = new CampusStoreStockService(
                    facts,
                    ResolveCatalog,
                    () => defaultShelfTargetItemCount,
                    () => defaultStoreSuspicionRisk,
                    RememberWarning);
            }

            if (audit == null)
            {
                audit = new CampusStoreAuditService(
                    stock,
                    () => defaultStoreSuspicionRisk,
                    WriteLog);
            }

            audit.SetContext(rosterService, worldService, gameplayEventHub);

            if (actions == null)
            {
                actions = new CampusStoreActions(
                    facts,
                    GetOrCreateShelfContainer,
                    CountUnpaidItems,
                    AddCheckedOutActor,
                    AddDailyStorePurchases,
                    CurrentDay,
                    WriteLog);
            }
        }

        private void EnsureCatalog()
        {
            if (catalog == null || catalog.Count == 0)
            {
                catalog = CampusStoreCatalogEntry.CreateDefaultCatalog();
                usingRuntimeFallbackCatalog = true;
            }
        }

        private IReadOnlyList<CampusStoreCatalogEntry> ResolveCatalog()
        {
            EnsureCatalog();
            return catalog;
        }

        private void SyncDay(bool force)
        {
            int day = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 0;
            if (!force && observedDay == day)
            {
                return;
            }

            observedDay = day;
            checkedOutActorIdsToday.Clear();
            dailyStorePurchasesCompleted = 0;
            dailyStoreTheftCount = 0;
        }

        private void RestockShelvesIfDue()
        {
            if (Time.time < nextShelfRestockTime || stock == null)
            {
                return;
            }

            nextShelfRestockTime = Time.time + shelfRestockIntervalSeconds;
            CampusStoreStockRefreshResult result = stock.RestockShelves(worldService);
            knownStoreShelfCount = result.KnownShelfCount;
            lastRestockedItemCount = result.RestockedItemCount;
        }

        private void AuditUnpaidItemsIfDue()
        {
            if (Time.time < nextTheftAuditTime || audit == null)
            {
                return;
            }

            nextTheftAuditTime = Time.time + theftAuditIntervalSeconds;
            CampusStoreAuditResult result = audit.AuditUnpaidItems();
            unpaidStoreItemCount = result.UnpaidItemCount;
            AddDailyStoreThefts(result.TheftCount);
        }

        private StorageContainerModel GetOrCreateShelfContainer(CampusPlacedObject shelf)
        {
            if (stock == null)
            {
                return null;
            }

            StorageContainerModel container = stock.GetOrCreateShelfContainer(shelf, out int added);
            if (added > 0)
            {
                lastRestockedItemCount += added;
            }

            return container;
        }

        private int CountUnpaidItems(
            CampusCharacterInventory inventory,
            CampusGameplayRoom storeRoom,
            List<StorageItemModel> destination)
        {
            return audit != null
                ? audit.CountUnpaidItems(inventory, storeRoom, destination)
                : 0;
        }

        private void AddCheckedOutActor(CampusCharacterRuntime actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.CharacterId) || ContainsId(checkedOutActorIdsToday, actor.CharacterId))
            {
                return;
            }

            checkedOutActorIdsToday.Add(actor.CharacterId.Trim());
        }

        private void AddDailyStorePurchases(int count)
        {
            if (count > 0)
            {
                dailyStorePurchasesCompleted += count;
            }
        }

        private void AddDailyStoreThefts(int count)
        {
            if (count > 0)
            {
                dailyStoreTheftCount += count;
            }
        }

        private int CurrentDay()
        {
            return bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 0;
        }

        private void UpdateSummary()
        {
            currentSummary = CampusCommerceTextCatalog.Format(
                CampusCommerceTextId.Summary,
                knownStoreShelfCount,
                lastRestockedItemCount,
                unpaidStoreItemCount,
                dailyStorePurchasesCompleted,
                dailyStoreTheftCount);
        }

        private void RememberWarning(string key, string message)
        {
            if (string.IsNullOrWhiteSpace(key) || warningKeys.Contains(key))
            {
                return;
            }

            warningKeys.Add(key);
            Debug.LogWarning("[Store] " + message, this);
        }

        private void WriteLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog(message);
            }
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

        private static bool ContainsId(List<string> ids, string id)
        {
            if (ids == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            string normalized = id.Trim();
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

    }

}
