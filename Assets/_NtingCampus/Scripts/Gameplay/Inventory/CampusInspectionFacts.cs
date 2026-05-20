using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    internal sealed class CampusInspectionFacts
    {
        private CampusGameBootstrap bootstrap;
        private CampusWorldService worldService;
        private CampusRosterService rosterService;
        private float maxInspectionDistance;
        private CampusInspectionPressure globalSearchPressure;
        private CampusInspectionPressure globalQuestioningPressure;
        private IReadOnlyList<CampusAreaInspectionPressureRule> areaPressureRules;
        private IReadOnlyList<CampusNpcInspectionPressureRule> npcPressureRules;

        public void Configure(
            CampusGameBootstrap bootstrap,
            CampusWorldService worldService,
            CampusRosterService rosterService,
            float maxInspectionDistance,
            CampusInspectionPressure globalSearchPressure,
            CampusInspectionPressure globalQuestioningPressure,
            IReadOnlyList<CampusAreaInspectionPressureRule> areaPressureRules,
            IReadOnlyList<CampusNpcInspectionPressureRule> npcPressureRules)
        {
            this.bootstrap = bootstrap;
            this.worldService = worldService;
            this.rosterService = rosterService;
            this.maxInspectionDistance = Mathf.Max(0.5f, maxInspectionDistance);
            this.globalSearchPressure = globalSearchPressure;
            this.globalQuestioningPressure = globalQuestioningPressure;
            this.areaPressureRules = areaPressureRules;
            this.npcPressureRules = npcPressureRules;
        }

        public bool TryResolveInspectionTarget(
            CampusCharacterRuntime requestedTarget,
            out CampusCharacterRuntime targetRuntime,
            out CampusGameplayRoom room,
            out string message)
        {
            targetRuntime = null;
            room = null;
            message = string.Empty;
            if (bootstrap == null || rosterService == null || worldService == null)
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.ServicesNotInitialized);
                return false;
            }

            targetRuntime = requestedTarget;
            if (targetRuntime == null || targetRuntime.Data == null)
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.TargetRuntimeUnavailable);
                return false;
            }

            room = worldService.FindRoomForRuntime(targetRuntime);
            if (room == null)
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.TargetNotInRoom);
                return false;
            }

            message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.Ready);
            return true;
        }

        public bool TryFindBestInspector(
            CampusGameplayRoom room,
            CampusCharacterRuntime targetRuntime,
            bool requireAuthority,
            out CampusCharacterRuntime inspector)
        {
            inspector = null;
            if (room == null || rosterService == null)
            {
                return false;
            }

            float bestScore = float.MinValue;
            for (int i = 0; i < rosterService.Runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = rosterService.Runtimes[i];
                if (!CanInspect(runtime, targetRuntime, room, requireAuthority))
                {
                    continue;
                }

                float distance = targetRuntime != null
                    ? Vector2.Distance(runtime.transform.position, targetRuntime.transform.position)
                    : 0f;
                if (distance > maxInspectionDistance)
                {
                    continue;
                }

                int vigilance = ResolveNpcVigilancePressure(runtime).Value;
                float score = vigilance + Mathf.Max(0f, maxInspectionDistance - distance) * 8f;
                if (score > bestScore)
                {
                    bestScore = score;
                    inspector = runtime;
                }
            }

            return inspector != null;
        }

        public bool TryFindHighestVigilanceNpc(
            CampusGameplayRoom room,
            CampusCharacterRuntime targetRuntime,
            out CampusCharacterRuntime highestNpc)
        {
            highestNpc = null;
            if (room == null || rosterService == null)
            {
                return false;
            }

            int highestPressure = -1;
            for (int i = 0; i < rosterService.Runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = rosterService.Runtimes[i];
                if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                if (targetRuntime != null &&
                    string.Equals(runtime.CharacterId, targetRuntime.CharacterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsRuntimeInRoom(runtime, room))
                {
                    continue;
                }

                if (targetRuntime != null &&
                    Vector2.Distance(runtime.transform.position, targetRuntime.transform.position) > maxInspectionDistance)
                {
                    continue;
                }

                int pressure = ResolveNpcVigilancePressure(runtime).Value;
                if (pressure > highestPressure)
                {
                    highestPressure = pressure;
                    highestNpc = runtime;
                }
            }

            return highestNpc != null;
        }

        public bool IsRuntimeInRoom(CampusCharacterRuntime runtime, CampusGameplayRoom room)
        {
            if (runtime == null || runtime.Data == null || room == null)
            {
                return false;
            }

            CampusGameplayRoom currentRoom = worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
            string currentRoomId = currentRoom != null ? currentRoom.RoomId : runtime.Data.CurrentRoomId;
            return string.Equals(currentRoomId, room.RoomId, StringComparison.OrdinalIgnoreCase);
        }

        public int ResolveSearchPressure(
            CampusGameplayRoom room,
            CampusCharacterRuntime inspector,
            CampusCharacterRuntime targetRuntime,
            StorageItemModel contrabandItem)
        {
            int pressure = globalSearchPressure.Value;
            pressure += ResolveAreaSearchPressure(room).Value;
            pressure += Mathf.RoundToInt(ResolveNpcVigilancePressure(inspector).Value * 0.45f);
            if (bootstrap != null && bootstrap.GameState != null)
            {
                pressure += Mathf.RoundToInt(bootstrap.GameState.TeacherAlertness * 0.25f);
            }

            pressure += Mathf.RoundToInt(ResolveTargetSuspicion(inspector, targetRuntime) * 0.35f);

            if (contrabandItem != null)
            {
                pressure += Mathf.Clamp(10 + Mathf.Max(0, contrabandItem.SuspicionRisk) / 2, 10, 24);
            }

            return Mathf.Clamp(pressure, 0, 100);
        }

        public int ResolveQuestioningPressure(
            CampusGameplayRoom room,
            CampusCharacterRuntime inspector,
            CampusCharacterRuntime targetRuntime,
            bool hasContraband)
        {
            int pressure = globalQuestioningPressure.Value;
            pressure += ResolveAreaQuestioningPressure(room).Value;
            pressure += Mathf.RoundToInt(ResolveNpcVigilancePressure(inspector).Value * 0.35f);
            if (bootstrap != null && bootstrap.GameState != null)
            {
                pressure += Mathf.RoundToInt(bootstrap.GameState.TeacherAlertness * 0.15f);
            }

            pressure += Mathf.RoundToInt(ResolveTargetSuspicion(inspector, targetRuntime) * 0.25f);

            if (hasContraband)
            {
                pressure += 8;
            }

            return Mathf.Clamp(pressure, 0, 100);
        }

        public CampusInspectionPressure ResolveAreaSearchPressure(CampusGameplayRoom room)
        {
            CampusAreaInspectionPressureRule rule = FindMatchingAreaRule(room);
            return rule != null ? rule.SearchPressure : CampusInspectionPressure.Of(0);
        }

        public CampusInspectionPressure ResolveAreaQuestioningPressure(CampusGameplayRoom room)
        {
            CampusAreaInspectionPressureRule rule = FindMatchingAreaRule(room);
            return rule != null ? rule.QuestioningPressure : CampusInspectionPressure.Of(0);
        }

        public CampusInspectionPressure ResolveNpcVigilancePressure(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return CampusInspectionPressure.Of(0);
            }

            if (TryResolveSpecificNpcVigilance(runtime.CharacterId, out CampusInspectionPressure specificPressure))
            {
                return specificPressure;
            }

            CampusCharacterData data = runtime.Data;
            if (data.Role == CampusCharacterRole.Teacher)
            {
                return CampusInspectionPressure.Of(48);
            }

            if (data.Role == CampusCharacterRole.Staff)
            {
                return CampusInspectionPressure.Of(42);
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                return CampusInspectionPressure.Of(28);
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return CampusInspectionPressure.Of(18);
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                return CampusInspectionPressure.Of(5);
            }

            return CampusInspectionPressure.Of(12);
        }

        public float ResolveTargetSuspicion(CampusCharacterRuntime inspector, CampusCharacterRuntime targetRuntime)
        {
            if (targetRuntime == null || targetRuntime.Data == null)
            {
                return 0f;
            }

            if (targetRuntime.Data.IsPlayerControlled)
            {
                return bootstrap != null && bootstrap.GameState != null
                    ? bootstrap.GameState.PlayerSuspicion
                    : 0f;
            }

            string targetId = ResolveRuntimeId(targetRuntime);
            return inspector != null &&
                   inspector.Data != null &&
                   !string.IsNullOrWhiteSpace(targetId)
                ? inspector.Data.GetRelationshipSuspicion(targetId)
                : 0f;
        }

        public static bool IsAuthority(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   runtime.Data != null &&
                   (runtime.Data.Role == CampusCharacterRole.Teacher ||
                    runtime.Data.Role == CampusCharacterRole.Staff);
        }

        private bool CanInspect(
            CampusCharacterRuntime runtime,
            CampusCharacterRuntime targetRuntime,
            CampusGameplayRoom room,
            bool requireAuthority)
        {
            if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return false;
            }

            if (targetRuntime != null &&
                string.Equals(runtime.CharacterId, targetRuntime.CharacterId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!IsRuntimeInRoom(runtime, room))
            {
                return false;
            }

            if (requireAuthority)
            {
                return IsAuthority(runtime);
            }

            return IsAuthority(runtime) ||
                   runtime.Data.HasTrait(CampusCharacterTrait.Tattletale) ||
                   runtime.Data.HasTrait(CampusCharacterTrait.GoodStudent) ||
                   HasNpcSpecificVigilance(runtime);
        }

        private bool HasNpcSpecificVigilance(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   TryResolveSpecificNpcVigilance(runtime.CharacterId, out CampusInspectionPressure pressure) &&
                   pressure.Value > 0;
        }

        private bool TryResolveSpecificNpcVigilance(string characterId, out CampusInspectionPressure pressure)
        {
            pressure = CampusInspectionPressure.Of(0);
            if (npcPressureRules == null || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            for (int i = 0; i < npcPressureRules.Count; i++)
            {
                CampusNpcInspectionPressureRule rule = npcPressureRules[i];
                if (rule != null && rule.Matches(characterId))
                {
                    pressure = rule.VigilancePressure;
                    return true;
                }
            }

            return false;
        }

        private CampusAreaInspectionPressureRule FindMatchingAreaRule(CampusGameplayRoom room)
        {
            if (room == null || areaPressureRules == null)
            {
                return null;
            }

            CampusAreaInspectionPressureRule fallback = null;
            for (int i = 0; i < areaPressureRules.Count; i++)
            {
                CampusAreaInspectionPressureRule rule = areaPressureRules[i];
                if (rule == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(rule.RoomId) && rule.Matches(room))
                {
                    return rule;
                }

                if (fallback == null && rule.Matches(room))
                {
                    fallback = rule;
                }
            }

            return fallback;
        }

        private static string ResolveRuntimeId(CampusCharacterRuntime runtime)
        {
            return runtime != null && !string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.CharacterId
                : string.Empty;
        }
    }
}
