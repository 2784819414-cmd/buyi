using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal static class CampusCanteenServiceWindowAvailability
    {
        private const float WorkerStandActivationRadius = 0.85f;
        private const float ServiceWindowActivationRadius = 1.25f;

        public static bool IsAvailable(CampusPlacedObject window)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            return CampusServiceStationCatalog.TryResolveByPlacedObject(worldService, window, out CampusServiceStation station) &&
                   IsAvailable(station);
        }

        public static bool IsAvailable(CampusServiceStation station)
        {
            return TryResolveAssignedOperator(station, out _);
        }

        public static bool TryResolveAssignedOperator(
            CampusServiceStation station,
            out CampusCharacterRuntime runtime)
        {
            runtime = null;
            if (!station.IsOperational || station.InteractionFacility == null)
            {
                return false;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusRosterService rosterService = bootstrap != null ? bootstrap.RosterService : null;
            IReadOnlyList<CampusCharacterRuntime> runtimes = rosterService != null
                ? rosterService.Runtimes
                : Array.Empty<CampusCharacterRuntime>();
            string serviceWindowKey = ResolveServiceWindowKey(station);
            if (string.IsNullOrEmpty(serviceWindowKey))
            {
                return false;
            }

            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime candidate = runtimes[i];
                if (!CanOperateWindow(candidate))
                {
                    continue;
                }

                CampusNpcPersonalProfile profile = ResolveNpcProfile(candidate);
                if (profile == null ||
                    !string.Equals(
                        profile.PrimaryServiceWindowKey,
                        serviceWindowKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsRuntimeAtStation(candidate, station))
                {
                    runtime = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool CanOperateWindow(CampusCharacterRuntime runtime)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null ||
                data.Role != CampusCharacterRole.Staff ||
                (data.StaffDuty & CampusStaffDuty.SupportStaff) == 0)
            {
                return false;
            }

            CampusTimeController timeController = CampusGameBootstrap.Instance != null
                ? CampusGameBootstrap.Instance.TimeController
                : null;
            return timeController != null &&
                   CampusNpcScheduleFacts.IsMealPeak(timeController.CurrentSegment);
        }

        private static bool IsRuntimeAtStation(
            CampusCharacterRuntime runtime,
            CampusServiceStation station)
        {
            if (runtime == null)
            {
                return false;
            }

            Vector2 runtimePosition = runtime.transform.position;
            if (station.HasOperatorSlot)
            {
                Vector2 workerStandPosition = CampusNpcFacilitySelector.PositionOf(station.OperatorSlot);
                if (Vector2.SqrMagnitude(runtimePosition - workerStandPosition) <=
                    WorkerStandActivationRadius * WorkerStandActivationRadius)
                {
                    return true;
                }
            }

            Vector2 serviceWindowPosition = CampusServiceStation.PositionOf(station.InteractionFacility);
            return Vector2.SqrMagnitude(runtimePosition - serviceWindowPosition) <=
                   ServiceWindowActivationRadius * ServiceWindowActivationRadius;
        }

        private static CampusNpcPersonalProfile ResolveNpcProfile(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return null;
            }

            CampusNpcActor actor = runtime.GetComponent<CampusNpcActor>();
            if (actor != null && actor.Profile != null)
            {
                return actor.Profile;
            }

            CampusNpcAiHost aiHost = runtime.GetComponent<CampusNpcAiHost>();
            return aiHost != null ? aiHost.Profile : null;
        }

        private static string ResolveServiceWindowKey(CampusServiceStation station)
        {
            return station.Room != null && station.InteractionFacility != null
                ? CampusNpcFacilitySelector.KeyFor(station.Room, station.InteractionFacility)
                : string.Empty;
        }
    }
}
