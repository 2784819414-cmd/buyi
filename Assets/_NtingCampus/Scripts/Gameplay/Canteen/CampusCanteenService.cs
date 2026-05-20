using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    [DisallowMultipleComponent]
    public sealed class CampusCanteenService : MonoBehaviour
    {
        private static readonly CampusCanteenStation[] EmptyStations = Array.Empty<CampusCanteenStation>();
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusCanteenMenuProfile menuProfile;
        [SerializeField] private bool logValidationIssues = true;
        [SerializeField] private List<string> servedActorIdsToday = new List<string>();
        [SerializeField] private int observedDay = -1;
        [SerializeField] private int dailyMealsServed;
        [SerializeField] private string currentSummary = string.Empty;

        private readonly List<CampusEcologyValidator.ValidationIssue> validationIssues =
            new List<CampusEcologyValidator.ValidationIssue>();

        private CampusCanteenDishFactory dishFactory;
        private CampusCanteenStockService stockService;
        private CampusCanteenStationRegistry stationRegistry;
        private CampusCanteenFacts facts;
        private CampusCanteenWindowActions windowActions;
        private CampusCanteenStockActions stockActions;
        private bool usingRuntimeFallbackMenu;
        private bool hasValidatedSetup;

        public int DailyMealsServed => dailyMealsServed;
        public CampusCanteenMenuProfile Menu => menuProfile;
        public bool IsUsingRuntimeFallbackMenu => usingRuntimeFallbackMenu;
        public IReadOnlyList<CampusEcologyValidator.ValidationIssue> ValidationIssues => validationIssues;
        public IReadOnlyList<CampusCanteenStation> Stations
        {
            get
            {
                EnsureReady();
                return facts != null ? facts.Stations : EmptyStations;
            }
        }

        public string CurrentSummary
        {
            get
            {
                EnsureReady();
                SyncDay(false);
                currentSummary = BuildSummary();
                return currentSummary;
            }
        }

        public static CampusCanteenService Resolve(bool createIfMissing = true)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.CanteenService != null)
            {
                return bootstrap.CanteenService;
            }

            CampusCanteenService existing = FindFirstObjectByType<CampusCanteenService>(FindObjectsInactive.Include);
            if (existing != null || !createIfMissing)
            {
                existing?.Initialize(bootstrap);
                return existing;
            }

            GameObject host = bootstrap != null ? bootstrap.gameObject : new GameObject("CampusCanteenService");
            CampusCanteenService service = host.AddComponent<CampusCanteenService>();
            service.Initialize(bootstrap);
            return service;
        }

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            CampusGameBootstrap resolvedBootstrap =
                targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            bool bootstrapChanged = bootstrap != resolvedBootstrap;
            bootstrap = resolvedBootstrap;
            EnsureReady();
            SyncDay(true);
            if (!hasValidatedSetup || bootstrapChanged)
            {
                ValidateSetup(logValidationIssues);
            }
            else
            {
                stationRegistry.RefreshIfNeeded(true);
            }

            currentSummary = BuildSummary();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureReady();
            SyncDay(false);
            stationRegistry.RefreshIfNeeded(false);
            currentSummary = BuildSummary();
        }

        public bool InteractWithServingWindow(GameObject actor, CampusPlacedObject sourceObject, out string message)
        {
            EnsureReady();
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowUnavailable);
            return windowActions != null &&
                   windowActions.InteractWithServingWindow(actor, sourceObject, out message);
        }

        public bool TryResolveServingWindowPrompt(CampusPlacedObject sourceObject, out string prompt)
        {
            EnsureReady();
            prompt = string.Empty;
            return facts != null && facts.TryResolveServingWindowPrompt(sourceObject, out prompt);
        }

        public bool TryFindWindowForCustomer(CampusCharacterRuntime customer, out CampusCanteenStation station)
        {
            EnsureReady();
            return facts.TryFindWindowForCustomer(customer, out station);
        }

        public bool TryFindWindowWithFoodForCustomer(CampusCharacterRuntime customer, out CampusCanteenStation station)
        {
            EnsureReady();
            return facts.TryFindWindowWithFoodForCustomer(customer, out station);
        }

        public bool TryFindDutyWindowForClerk(CampusCharacterRuntime clerk, out CampusCanteenStation station)
        {
            EnsureReady();
            return facts.TryFindDutyWindowForClerk(clerk, out station);
        }

        public bool TryUseWindowLikePlayer(CampusCharacterRuntime actor, CampusCanteenStation station, out string message)
        {
            EnsureReady();
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowUnavailable);
            return windowActions != null &&
                   windowActions.TryUseWindowLikePlayer(actor, station, out message);
        }

        public bool TryPrepareMealAtWindow(
            CampusCharacterRuntime clerk,
            CampusCanteenStation station,
            out string message)
        {
            EnsureReady();
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowUnavailable);
            return windowActions != null &&
                   windowActions.TryPrepareMealAtWindow(clerk, station, out message);
        }

        public bool TryTakeMealAtWindow(
            CampusCharacterRuntime customer,
            CampusCanteenStation station,
            out string message)
        {
            EnsureReady();
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowUnavailable);
            return windowActions != null &&
                   windowActions.TryTakeMealAtWindow(customer, station, out message);
        }

        public bool OpenStockStorage(GameObject actor, CampusPlacedObject sourceObject, out string message)
        {
            EnsureReady();
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoStockContainer);
            return stockActions != null &&
                   stockActions.OpenStockStorage(actor, sourceObject, out message);
        }

        public bool TryStealStockDish(GameObject actor, string dishId, out string message)
        {
            EnsureReady();
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoMatchingFood);
            return stockActions != null &&
                   stockActions.TryStealStockDish(actor, dishId, out message);
        }

        public bool OpenStockStorage(GameObject actor, string stationId, out string message)
        {
            EnsureReady();
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoStockContainer);
            return stockActions != null &&
                   stockActions.OpenStockStorage(actor, stationId, out message);
        }

        public IReadOnlyList<StorageItemModel> GetStockItems(string stationId)
        {
            EnsureReady();
            if (!TryGetStation(stationId, out CampusCanteenStation station))
            {
                return stockService.GetStockItems(StorageMemory.GetOrCreate(), null, menuProfile);
            }

            return stockService.GetStockItems(
                StorageMemory.GetOrCreate(),
                station,
                menuProfile);
        }

        public IReadOnlyList<CampusCanteenDishDefinition> GetMenuItems(string stationId)
        {
            EnsureReady();
            return stockService.GetMenuItems(
                menuProfile,
                TryGetStation(stationId, out CampusCanteenStation station) ? station : null);
        }

        public int CountMealsOnCounter(string stationId)
        {
            return TryGetStation(stationId, out CampusCanteenStation station) && HasFoodAtStation(station) ? 1 : 0;
        }

        public IReadOnlyList<CampusEcologyValidator.ValidationIssue> ValidateSetup(bool logIssues)
        {
            EnsureReady();
            stationRegistry?.RefreshIfNeeded(true);
            validationIssues.Clear();

            List<CampusEcologyValidator.ValidationIssue> issues =
                CampusCanteenValidator.Validate(menuProfile, Stations, usingRuntimeFallbackMenu);
            for (int i = 0; i < issues.Count; i++)
            {
                validationIssues.Add(issues[i]);
            }

            if (logIssues)
            {
                CampusCanteenValidator.LogIssues(validationIssues);
            }

            hasValidatedSetup = true;
            return validationIssues;
        }

        public bool TryGetStation(string stationId, out CampusCanteenStation station)
        {
            EnsureReady();
            return stationRegistry.TryGetStation(stationId, out station);
        }

        public bool HasReceivedMealToday(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   (ContainsId(servedActorIdsToday, runtime.CharacterId) ||
                    (runtime.Data != null && runtime.Data.HasMemory(CampusCharacterMemoryId.ReceivedCanteenMeal)));
        }

        public bool HasFoodAtStation(CampusCanteenStation station)
        {
            EnsureReady();
            return facts.HasFoodAtStation(station);
        }

        public bool IsServingOpenNow()
        {
            EnsureReady();
            return facts.IsServingOpenNow();
        }

        public bool TryFindClerkAtStation(CampusCanteenStation station, out CampusCharacterRuntime clerk)
        {
            EnsureReady();
            return facts.TryFindClerkAtStation(station, out clerk);
        }

        public bool TryFindStationWhereClerkStands(CampusCharacterRuntime clerk, out CampusCanteenStation station)
        {
            EnsureReady();
            return facts.TryFindStationWhereClerkStands(clerk, out station);
        }

        public bool IsCustomerAtWindow(CampusCharacterRuntime customer, CampusCanteenStation station)
        {
            EnsureReady();
            return facts.IsCustomerAtWindow(customer, station);
        }

        private void MarkMealReceived(CampusCharacterRuntime customer)
        {
            if (customer == null)
            {
                return;
            }

            AddId(servedActorIdsToday, customer.CharacterId);
            customer.Data?.AddMemory(CampusCharacterMemoryId.ReceivedCanteenMeal);
            dailyMealsServed++;
        }

        private void EnsureReady()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (menuProfile == null)
            {
                menuProfile = CampusCanteenMenuProfile.CreateFallback();
                usingRuntimeFallbackMenu = true;
            }

            if (dishFactory == null)
            {
                dishFactory = new CampusCanteenDishFactory();
            }

            if (stockService == null)
            {
                stockService = new CampusCanteenStockService(dishFactory);
            }

            if (stationRegistry == null)
            {
                stationRegistry = new CampusCanteenStationRegistry(bootstrap != null ? bootstrap.WorldService : null);
            }
            else
            {
                stationRegistry.SetWorldService(bootstrap != null ? bootstrap.WorldService : null);
            }

            if (servedActorIdsToday == null)
            {
                servedActorIdsToday = new List<string>();
            }

            if (facts == null)
            {
                facts = new CampusCanteenFacts();
            }

            facts.SetContext(bootstrap, stationRegistry, HasReceivedMealToday);

            if (windowActions == null)
            {
                windowActions = new CampusCanteenWindowActions(
                    facts,
                    () => menuProfile,
                    () => dishFactory,
                    MarkMealReceived,
                    WriteLog);
            }

            if (stockActions == null)
            {
                stockActions = new CampusCanteenStockActions(
                    facts,
                    stockService,
                    () => menuProfile,
                    () => dishFactory);
            }
        }

        private void SyncDay(bool force)
        {
            int day = CurrentDay();
            if (!force && observedDay == day)
            {
                return;
            }

            observedDay = day;
            dailyMealsServed = 0;
            servedActorIdsToday.Clear();
        }

        private int CurrentDay()
        {
            return bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 0;
        }

        private string BuildSummary()
        {
            IReadOnlyList<CampusCanteenStation> stations = Stations;
            int mealsOnCounter = 0;
            for (int i = 0; i < stations.Count; i++)
            {
                if (HasFoodAtStation(stations[i]))
                {
                    mealsOnCounter++;
                }
            }

            return CampusCanteenTextCatalog.Format(
                CampusCanteenTextId.Summary,
                stations.Count,
                mealsOnCounter,
                dailyMealsServed);
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

        private static void AddId(List<string> ids, string id)
        {
            if (ids == null || string.IsNullOrWhiteSpace(id) || ContainsId(ids, id))
            {
                return;
            }

            ids.Add(id.Trim());
        }

        private static void WriteLog(string message)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog(CampusCanteenTextCatalog.Format(CampusCanteenTextId.LogLine, message));
            }
        }
    }
}
