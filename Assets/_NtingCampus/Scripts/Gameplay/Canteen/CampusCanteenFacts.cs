using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenFacts
    {
        private static readonly CampusCanteenStation[] EmptyStations = Array.Empty<CampusCanteenStation>();

        private const float WindowUseDistance = 0.95f;
        private const float MealPickupRadius = 0.65f;
        private const float ClerkStationDistance = 1.15f;

        private CampusGameBootstrap bootstrap;
        private CampusCanteenStationRegistry stationRegistry;
        private Func<CampusCharacterRuntime, bool> hasReceivedMealToday;

        public void SetContext(
            CampusGameBootstrap targetBootstrap,
            CampusCanteenStationRegistry targetStationRegistry,
            Func<CampusCharacterRuntime, bool> receivedMealResolver)
        {
            bootstrap = targetBootstrap;
            stationRegistry = targetStationRegistry;
            hasReceivedMealToday = receivedMealResolver;
        }

        public IReadOnlyList<CampusCanteenStation> Stations
        {
            get
            {
                stationRegistry?.RefreshIfNeeded(false);
                return stationRegistry != null ? stationRegistry.Stations : EmptyStations;
            }
        }

        public bool TryResolveServingWindowPrompt(CampusPlacedObject sourceObject, out string prompt)
        {
            if (TryResolveStation(sourceObject, out CampusCanteenStation station))
            {
                prompt = CampusCanteenTextCatalog.Format(CampusCanteenTextId.WindowPrompt, station.DisplayName);
                return true;
            }

            prompt = string.Empty;
            return false;
        }

        public bool TryFindWindowForCustomer(CampusCharacterRuntime customer, out CampusCanteenStation station)
        {
            return TryFindCustomerWindow(customer, false, out station);
        }

        public bool TryFindWindowWithFoodForCustomer(CampusCharacterRuntime customer, out CampusCanteenStation station)
        {
            return TryFindCustomerWindow(customer, true, out station);
        }

        public bool TryFindDutyWindowForClerk(CampusCharacterRuntime clerk, out CampusCanteenStation station)
        {
            station = null;
            if (!IsServingOpenNow() || !IsCanteenClerk(clerk))
            {
                return false;
            }

            string assignedId = clerk != null && clerk.Data != null
                ? clerk.Data.Assignments.PrimaryWorkstationId
                : string.Empty;
            IReadOnlyList<CampusCanteenStation> stations = Stations;
            for (int i = 0; i < stations.Count; i++)
            {
                CampusCanteenStation candidate = stations[i];
                if (candidate != null &&
                    candidate.HasServingWindow &&
                    candidate.MatchesWorkstationId(assignedId))
                {
                    station = candidate;
                    return true;
                }
            }

            return TryFindNearestStation(clerk != null ? clerk.transform.position : Vector3.zero, false, out station);
        }

        public bool HasFoodAtStation(CampusCanteenStation station)
        {
            return TryFindFoodAtStation(station, out _);
        }

        public bool HasReceivedMealToday(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   hasReceivedMealToday != null &&
                   hasReceivedMealToday(runtime);
        }

        public bool IsServingOpenNow()
        {
            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            return timeController == null ||
                   CampusNpcScheduleFacts.IsCanteenShiftActive(timeController.CurrentSegment);
        }

        public bool TryFindClerkAtStation(CampusCanteenStation station, out CampusCharacterRuntime clerk)
        {
            clerk = null;
            if (station == null || bootstrap == null || bootstrap.RosterService == null)
            {
                return false;
            }

            IReadOnlyList<CampusCharacterRuntime> runtimes = bootstrap.RosterService.Runtimes;
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (!IsCanteenClerk(runtime))
                {
                    continue;
                }

                if (Vector2.Distance(runtime.transform.position, station.ClerkPosition) <= ClerkStationDistance)
                {
                    clerk = runtime;
                    return true;
                }
            }

            return false;
        }

        public bool TryFindStationWhereClerkStands(CampusCharacterRuntime clerk, out CampusCanteenStation station)
        {
            station = null;
            if (!IsCanteenClerk(clerk))
            {
                return false;
            }

            IReadOnlyList<CampusCanteenStation> stations = Stations;
            for (int i = 0; i < stations.Count; i++)
            {
                CampusCanteenStation candidate = stations[i];
                if (candidate != null &&
                    Vector2.Distance(clerk.transform.position, candidate.ClerkPosition) <= ClerkStationDistance)
                {
                    station = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool IsCustomerAtWindow(CampusCharacterRuntime customer, CampusCanteenStation station)
        {
            return customer != null &&
                   station != null &&
                   Vector2.Distance(customer.transform.position, station.CustomerPosition) <= WindowUseDistance;
        }

        public bool TryFindNearestStation(
            Vector3 origin,
            bool requireFood,
            out CampusCanteenStation station)
        {
            station = null;
            IReadOnlyList<CampusCanteenStation> stations = Stations;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < stations.Count; i++)
            {
                CampusCanteenStation candidate = stations[i];
                if (candidate == null ||
                    !candidate.HasServingWindow ||
                    candidate.WindowObject == null ||
                    requireFood && !HasFoodAtStation(candidate))
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude((Vector2)(candidate.CustomerPosition - origin));
                if (distance < bestDistance)
                {
                    station = candidate;
                    bestDistance = distance;
                }
            }

            return station != null;
        }

        public bool ValidateWindowUse(
            CampusCharacterRuntime actor,
            CampusCanteenStation station,
            Vector3 expectedPosition,
            out string message)
        {
            if (actor == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoCharacter);
                return false;
            }

            if (station == null || station.WindowObject == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoServingWindow);
                return false;
            }

            if (!IsServingOpenNow())
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowClosed);
                return false;
            }

            if (Vector2.Distance(actor.transform.position, expectedPosition) > WindowUseDistance)
            {
                message = CampusCanteenTextCatalog.Format(CampusCanteenTextId.StandAtWindow, station.DisplayName);
                return false;
            }

            message = string.Empty;
            return true;
        }

        public bool TryResolveStation(CampusPlacedObject sourceObject, out CampusCanteenStation station)
        {
            station = null;
            if (sourceObject == null || stationRegistry == null)
            {
                return false;
            }

            return stationRegistry.TryFindStationForObject(sourceObject, out station);
        }

        public bool TryGetStation(string stationId, out CampusCanteenStation station)
        {
            station = null;
            return stationRegistry != null &&
                   stationRegistry.TryGetStation(stationId, out station);
        }

        public bool TryFindFoodAtStation(CampusCanteenStation station, out CampusDroppedStorageItem droppedItem)
        {
            droppedItem = null;
            if (station == null)
            {
                return false;
            }

            CampusDroppedStorageItem[] droppedItems =
                UnityEngine.Object.FindObjectsByType<CampusDroppedStorageItem>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < droppedItems.Length; i++)
            {
                CampusDroppedStorageItem candidate = droppedItems[i];
                if (!MatchesStationFood(station, candidate))
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude((Vector2)(candidate.transform.position - station.MealDropPosition));
                if (distance < bestDistance)
                {
                    droppedItem = candidate;
                    bestDistance = distance;
                }
            }

            return droppedItem != null;
        }

        internal static bool IsCanteenClerk(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   runtime.Data != null &&
                   runtime.Data.Role == CampusCharacterRole.Staff &&
                   (runtime.Data.StaffDuty & CampusStaffDuty.CanteenClerk) != 0;
        }

        private bool TryFindCustomerWindow(
            CampusCharacterRuntime customer,
            bool requireFood,
            out CampusCanteenStation station)
        {
            station = null;
            if (customer == null ||
                !IsServingOpenNow() ||
                HasReceivedMealToday(customer))
            {
                return false;
            }

            return TryFindNearestStation(customer.transform.position, requireFood, out station);
        }

        private static bool MatchesStationFood(CampusCanteenStation station, CampusDroppedStorageItem item)
        {
            if (station == null || item == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(item.SourceContainerId) &&
                string.Equals(item.SourceContainerId, station.CounterContainerId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(item.SourceLocation) &&
                   string.Equals(item.SourceLocation, station.DisplayName, StringComparison.OrdinalIgnoreCase) &&
                   Vector2.Distance(item.transform.position, station.MealDropPosition) <= MealPickupRadius;
        }
    }
}
