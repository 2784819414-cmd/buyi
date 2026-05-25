using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal sealed class CampusRuntimeGameplayActorPreset
    {
        public readonly CampusLocalizedTextEntry Label;
        public readonly CampusLocalizedTextEntry NamePrefix;
        public readonly string IdPrefix;
        public readonly CampusCharacterRole Role;
        public readonly CampusTeacherDuty TeacherDuty;
        public readonly CampusStaffDuty StaffDuty;
        public readonly string ClassId;
        public readonly int Sleepiness;
        public readonly int Mischief;
        public readonly int InitialMoney;
        public readonly CampusCharacterTrait[] Traits;
        public readonly Color Color;

        public CampusRuntimeGameplayActorPreset(
            CampusLocalizedTextEntry label,
            CampusLocalizedTextEntry namePrefix,
            string idPrefix,
            CampusCharacterRole role,
            CampusTeacherDuty teacherDuty,
            CampusStaffDuty staffDuty,
            string classId,
            int sleepiness,
            int mischief,
            int initialMoney,
            CampusCharacterTrait[] traits,
            Color color)
        {
            Label = label;
            NamePrefix = namePrefix;
            IdPrefix = idPrefix;
            Role = role;
            TeacherDuty = teacherDuty;
            StaffDuty = staffDuty;
            ClassId = classId;
            Sleepiness = Mathf.Clamp(sleepiness, 0, 100);
            Mischief = Mathf.Clamp(mischief, 0, 100);
            InitialMoney = Mathf.Max(NtingCampus.Gameplay.Economy.CampusCharacterEconomyDefaults.UseRoleDefaultMoney, initialMoney);
            Traits = traits != null ? (CampusCharacterTrait[])traits.Clone() : Array.Empty<CampusCharacterTrait>();
            Color = color;
        }
    }

    internal readonly struct CampusRuntimeGameplayActorDraft
    {
        public readonly string RequestedId;
        public readonly string RequestedChineseName;
        public readonly string RequestedEnglishName;
        public readonly string RequestedClassId;

        public CampusRuntimeGameplayActorDraft(
            string requestedId,
            string requestedChineseName,
            string requestedEnglishName,
            string requestedClassId)
        {
            RequestedId = requestedId ?? string.Empty;
            RequestedChineseName = requestedChineseName ?? string.Empty;
            RequestedEnglishName = requestedEnglishName ?? string.Empty;
            RequestedClassId = requestedClassId ?? string.Empty;
        }
    }

    internal static class CampusRuntimeGameplayActorAuthoring
    {
        internal delegate CampusFloorRoot FloorResolver(int floorIndex);
        internal delegate void RuntimeObjectDestroyer(UnityEngine.Object target);

        internal static CampusRuntimeGameplayActorSnapshot CreateActor(
            CampusRuntimeGameplayActorPreset preset,
            CampusRuntimeGameplayActorDraft draft,
            int floorIndex,
            Vector3Int cell,
            IReadOnlyList<CampusRuntimeGameplayActorSnapshot> existingActors)
        {
            int ordinal = ResolveNextOrdinal(preset, existingActors);
            string englishName = ResolveEnglishName(preset, draft, ordinal);
            string chineseName = ResolveChineseName(preset, draft, ordinal);
            CampusRuntimeGameplayActorSnapshot actor = new CampusRuntimeGameplayActorSnapshot
            {
                Id = ResolveActorId(preset, draft, ordinal, existingActors),
                DisplayName = englishName,
                LocalizedDisplayName = ResolveLocalizedDisplayName(preset, ordinal, chineseName, englishName),
                Role = preset != null ? preset.Role : CampusCharacterRole.Student,
                TeacherDuty = preset != null ? preset.TeacherDuty : CampusTeacherDuty.None,
                StaffDuty = preset != null ? preset.StaffDuty : CampusStaffDuty.None,
                ClassId = ResolveClassId(preset, draft),
                InitialState = CampusCharacterState.Normal,
                IsPlayerControlled = false,
                FloorIndex = Mathf.Max(1, floorIndex),
                Cell = NormalizeCell(cell),
                Sleepiness = preset != null ? preset.Sleepiness : 40,
                Mischief = preset != null ? preset.Mischief : 20,
                InitialMoney = preset != null
                    ? preset.InitialMoney
                    : NtingCampus.Gameplay.Economy.CampusCharacterEconomyDefaults.UseRoleDefaultMoney,
                Traits = preset != null && preset.Traits != null
                    ? (CampusCharacterTrait[])preset.Traits.Clone()
                    : Array.Empty<CampusCharacterTrait>(),
                Assignments = new CampusCharacterAssignmentData()
            };

            ApplyRecommendedAssignments(actor);
            actor.Normalize();
            return actor;
        }

        internal static bool EraseActorsAtCell(
            List<CampusRuntimeGameplayActorSnapshot> cachedActors,
            CampusFloorRoot floor,
            Vector3Int cell,
            RuntimeObjectDestroyer destroyObject)
        {
            if (floor == null || cachedActors == null || destroyObject == null)
            {
                return false;
            }

            Vector3Int normalizedCell = NormalizeCell(cell);
            bool erased = false;
            HashSet<string> removedActorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = cachedActors.Count - 1; i >= 0; i--)
            {
                CampusRuntimeGameplayActorSnapshot actor = cachedActors[i];
                if (actor == null ||
                    actor.IsPlayerControlled ||
                    Mathf.Max(1, actor.FloorIndex) != floor.FloorIndex ||
                    NormalizeCell(actor.Cell) != normalizedCell)
                {
                    continue;
                }

                DestroyActorRuntime(actor.Id, destroyObject);
                if (!string.IsNullOrWhiteSpace(actor.Id))
                {
                    removedActorIds.Add(actor.Id.Trim());
                }

                cachedActors.RemoveAt(i);
                erased = true;
            }

            CampusRuntimeGameplayOverlayEntity[] entities =
                UnityEngine.Object.FindObjectsByType<CampusRuntimeGameplayOverlayEntity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = entities.Length - 1; i >= 0; i--)
            {
                CampusRuntimeGameplayOverlayEntity entity = entities[i];
                if (entity == null ||
                    !entity.IsActorEntity ||
                    entity.FloorIndex != floor.FloorIndex ||
                    NormalizeCell(entity.Cell) != normalizedCell)
                {
                    continue;
                }

                CampusCharacterRuntime runtime = entity.GetComponent<CampusCharacterRuntime>();
                if (runtime != null && removedActorIds.Contains(runtime.CharacterId))
                {
                    continue;
                }

                if (runtime != null && runtime.Data != null && runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                destroyObject(entity.gameObject);
                erased = true;
            }

            return erased;
        }

        internal static void CaptureSceneActors(
            List<CampusRuntimeGameplayActorSnapshot> output,
            CampusMapRoot mapRoot)
        {
            if (output == null)
            {
                return;
            }

            HashSet<string> processedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CampusSceneCharacterDefinition[] definitions =
                UnityEngine.Object.FindObjectsByType<CampusSceneCharacterDefinition>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < definitions.Length; i++)
            {
                CampusSceneCharacterDefinition definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                CampusCharacterRuntime runtime = definition.GetComponent<CampusCharacterRuntime>();
                CampusCharacterData data = runtime != null && runtime.Data != null
                    ? runtime.Data
                    : definition.BuildData();
                AddSceneActor(output, processedIds, data, definition.gameObject, definition.FloorIndex, mapRoot);
            }

            CampusCharacterRuntime[] runtimes =
                UnityEngine.Object.FindObjectsByType<CampusCharacterRuntime>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < runtimes.Length; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                CampusCharacterBodyController body = runtime.GetComponent<CampusCharacterBodyController>();
                int floorIndex = body != null ? body.FloorIndex : 1;
                AddSceneActor(output, processedIds, runtime.Data, runtime.gameObject, floorIndex, mapRoot);
            }
        }

        internal static void SpawnActorForEditing(
            CampusRuntimeGameplayActorSnapshot actor,
            FloorResolver resolveFloor)
        {
            if (actor == null || actor.IsPlayerControlled || string.IsNullOrWhiteSpace(actor.Id) || resolveFloor == null)
            {
                return;
            }

            CampusFloorRoot floor = resolveFloor(Mathf.Max(1, actor.FloorIndex));
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                return;
            }

            Vector3Int cell = NormalizeCell(actor.Cell);
            Vector3 worldPosition = floor.Grid.GetCellCenterWorld(cell);
            CampusCharacterRuntime existing = FindActorRuntime(actor.Id);
            if (existing != null)
            {
                CampusRuntimeGameplayOverlayEntity existingEntity =
                    existing.GetComponent<CampusRuntimeGameplayOverlayEntity>();
                if (existingEntity == null)
                {
                    existingEntity = existing.gameObject.AddComponent<CampusRuntimeGameplayOverlayEntity>();
                }

                existingEntity.Configure(true, floor.FloorIndex, cell);
                CampusCharacterBodyController existingBody = existing.GetComponent<CampusCharacterBodyController>();
                if (existingBody != null)
                {
                    existingBody.FloorIndex = floor.FloorIndex;
                    existingBody.EnsureSetup();
                    existingBody.Teleport(worldPosition);
                }
                else
                {
                    existing.transform.position = worldPosition;
                }

                return;
            }

            GameObject actorObject = new GameObject(string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.Id : actor.DisplayName);
            actorObject.transform.SetParent(floor.PropsRoot, false);
            actorObject.transform.position = worldPosition;

            CampusRuntimeGameplayOverlayEntity entity = actorObject.AddComponent<CampusRuntimeGameplayOverlayEntity>();
            entity.Configure(true, floor.FloorIndex, cell);

            CampusCharacterData data = BuildCharacterData(actor);
            CampusCharacterRuntime runtime = actorObject.AddComponent<CampusCharacterRuntime>();
            runtime.Bind(data, true);

            CampusCharacterBodyController body = actorObject.AddComponent<CampusCharacterBodyController>();
            body.FloorIndex = floor.FloorIndex;
            body.EnsureSetup();
            body.Teleport(worldPosition);

            NtingCampus.Gameplay.Core.CampusGameBootstrap bootstrap =
                NtingCampus.Gameplay.Core.CampusGameBootstrap.Instance;
            CampusNpcActor npcActor = actorObject.AddComponent<CampusNpcActor>();
            npcActor.Initialize(runtime, bootstrap, bootstrap != null ? bootstrap.WorldService : null);
        }

        internal static void RebuildRosterFromScene()
        {
            NtingCampus.Gameplay.Core.CampusGameBootstrap bootstrap =
                NtingCampus.Gameplay.Core.CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.RosterService != null)
            {
                bootstrap.RosterService.RebuildRosterFromScene();
            }
        }

        private static void AddSceneActor(
            List<CampusRuntimeGameplayActorSnapshot> output,
            HashSet<string> processedIds,
            CampusCharacterData data,
            GameObject actorObject,
            int floorIndex,
            CampusMapRoot mapRoot)
        {
            if (output == null ||
                processedIds == null ||
                data == null ||
                string.IsNullOrWhiteSpace(data.Id) ||
                !processedIds.Add(data.Id.Trim()))
            {
                return;
            }

            int resolvedFloor = Mathf.Max(1, floorIndex);
            Vector3 worldPosition = actorObject != null ? actorObject.transform.position : Vector3.zero;
            CampusRuntimeGameplayActorSnapshot actor = new CampusRuntimeGameplayActorSnapshot
            {
                Id = data.Id,
                DisplayName = data.GetDisplayName(CampusDisplayLanguage.English),
                LocalizedDisplayName = data.LocalizedDisplayName,
                Role = data.Role,
                TeacherDuty = data.TeacherDuty,
                StaffDuty = data.StaffDuty,
                ClassId = data.ClassId,
                InitialState = data.State,
                IsPlayerControlled = data.IsPlayerControlled,
                FloorIndex = resolvedFloor,
                Cell = ResolveCellForWorldPosition(mapRoot, resolvedFloor, worldPosition),
                Sleepiness = data.Sleepiness,
                Mischief = data.Mischief,
                InitialMoney = data.Money,
                Traits = CopyTraits(data.Traits),
                Assignments = data.Assignments != null ? data.Assignments.Clone() : new CampusCharacterAssignmentData()
            };
            actor.Normalize();
            output.Add(actor);
        }

        private static Vector3Int ResolveCellForWorldPosition(CampusMapRoot mapRoot, int floorIndex, Vector3 worldPosition)
        {
            CampusFloorRoot floor = mapRoot != null ? mapRoot.GetFloor(Mathf.Max(1, floorIndex)) : null;
            if (floor != null && floor.Grid != null)
            {
                return NormalizeCell(floor.Grid.WorldToCell(worldPosition));
            }

            return NormalizeCell(Vector3Int.RoundToInt(worldPosition));
        }

        private static int ResolveNextOrdinal(
            CampusRuntimeGameplayActorPreset preset,
            IReadOnlyList<CampusRuntimeGameplayActorSnapshot> existingActors)
        {
            string prefix = preset != null && !string.IsNullOrWhiteSpace(preset.IdPrefix)
                ? preset.IdPrefix.Trim()
                : "npc";
            int ordinal = 1;
            while (ActorIdExists(
                       prefix + "_" + ordinal.ToString(CultureInfo.InvariantCulture),
                       existingActors))
            {
                ordinal++;
            }

            return ordinal;
        }

        private static string ResolveActorId(
            CampusRuntimeGameplayActorPreset preset,
            CampusRuntimeGameplayActorDraft draft,
            int ordinal,
            IReadOnlyList<CampusRuntimeGameplayActorSnapshot> existingActors)
        {
            string requested = NormalizeActorId(draft.RequestedId);
            if (!string.IsNullOrWhiteSpace(requested))
            {
                return MakeUniqueActorId(requested, existingActors);
            }

            string prefix = preset != null && !string.IsNullOrWhiteSpace(preset.IdPrefix)
                ? preset.IdPrefix.Trim()
                : "npc";
            return MakeUniqueActorId(prefix + "_" + ordinal.ToString(CultureInfo.InvariantCulture), existingActors);
        }

        private static string ResolveChineseName(
            CampusRuntimeGameplayActorPreset preset,
            CampusRuntimeGameplayActorDraft draft,
            int ordinal)
        {
            if (!string.IsNullOrWhiteSpace(draft.RequestedChineseName))
            {
                return draft.RequestedChineseName.Trim();
            }

            string prefix = preset != null && !string.IsNullOrWhiteSpace(preset.NamePrefix.Chinese)
                ? preset.NamePrefix.Chinese.Trim()
                : "Actor";
            return prefix + ordinal.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveEnglishName(
            CampusRuntimeGameplayActorPreset preset,
            CampusRuntimeGameplayActorDraft draft,
            int ordinal)
        {
            if (!string.IsNullOrWhiteSpace(draft.RequestedEnglishName))
            {
                return draft.RequestedEnglishName.Trim();
            }

            string prefix = preset != null && !string.IsNullOrWhiteSpace(preset.NamePrefix.English)
                ? preset.NamePrefix.English.Trim()
                : "Npc";
            return prefix + ordinal.ToString(CultureInfo.InvariantCulture);
        }

        private static CampusLocalizedText ResolveLocalizedDisplayName(
            CampusRuntimeGameplayActorPreset preset,
            int ordinal,
            string chineseName,
            string englishName)
        {
            if (preset == null || !preset.NamePrefix.HasAnyText)
            {
                return new CampusLocalizedText(chineseName, englishName);
            }

            return new CampusLocalizedText(
                chineseName,
                englishName,
                BuildNameFromPrefix(preset.NamePrefix.TraditionalChinese, ordinal),
                BuildNameFromPrefix(preset.NamePrefix.Russian, ordinal),
                BuildNameFromPrefix(preset.NamePrefix.Japanese, ordinal));
        }

        private static string BuildNameFromPrefix(string prefix, int ordinal)
        {
            return string.IsNullOrWhiteSpace(prefix)
                ? string.Empty
                : prefix.Trim() + ordinal.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveClassId(
            CampusRuntimeGameplayActorPreset preset,
            CampusRuntimeGameplayActorDraft draft)
        {
            if (!string.IsNullOrWhiteSpace(draft.RequestedClassId))
            {
                return draft.RequestedClassId.Trim();
            }

            return preset != null && !string.IsNullOrWhiteSpace(preset.ClassId)
                ? preset.ClassId.Trim()
                : string.Empty;
        }

        private static string NormalizeActorId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9_]+", "_");
            normalized = Regex.Replace(normalized, "_+", "_").Trim('_');
            return normalized;
        }

        private static string MakeUniqueActorId(
            string baseId,
            IReadOnlyList<CampusRuntimeGameplayActorSnapshot> existingActors)
        {
            string normalized = NormalizeActorId(baseId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = "npc";
            }

            if (!ActorIdExists(normalized, existingActors))
            {
                return normalized;
            }

            int suffix = 2;
            string candidate;
            do
            {
                candidate = normalized + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            while (ActorIdExists(candidate, existingActors));

            return candidate;
        }

        private static bool ActorIdExists(
            string actorId,
            IReadOnlyList<CampusRuntimeGameplayActorSnapshot> existingActors)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return false;
            }

            string normalized = actorId.Trim();
            if (existingActors != null)
            {
                for (int i = 0; i < existingActors.Count; i++)
                {
                    CampusRuntimeGameplayActorSnapshot actor = existingActors[i];
                    if (actor != null && string.Equals(actor.Id, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return FindActorRuntime(normalized) != null;
        }

        private static void ApplyRecommendedAssignments(CampusRuntimeGameplayActorSnapshot actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.Assignments = actor.Assignments ?? new CampusCharacterAssignmentData();
            CampusGameplayFacilityMarker facility = null;
            switch (actor.Role)
            {
                case CampusCharacterRole.Student:
                    facility = FindNearestFacility(actor.FloorIndex, actor.Cell, CampusFacilityType.StudentDesk);
                    if (facility != null)
                    {
                        actor.Assignments.StudentDeskId = ResolveFacilityId(facility);
                    }

                    break;
                case CampusCharacterRole.Teacher:
                    facility = FindNearestFacility(
                        actor.FloorIndex,
                        actor.Cell,
                        CampusFacilityType.Podium,
                        CampusFacilityType.Blackboard);
                    if (facility != null)
                    {
                        actor.Assignments.TeacherPodiumId = ResolveFacilityId(facility);
                    }

                    CampusGameplayFacilityMarker officeDesk = FindNearestFacility(
                        actor.FloorIndex,
                        actor.Cell,
                        CampusFacilityType.OfficeDesk,
                        CampusFacilityType.Desk);
                    if (officeDesk != null)
                    {
                        actor.Assignments.OfficeDeskId = ResolveFacilityId(officeDesk);
                    }

                    break;
                case CampusCharacterRole.Staff:
                    ApplyRecommendedStaffAssignments(actor);
                    break;
            }

            actor.Assignments.Normalize();
        }

        private static void ApplyRecommendedStaffAssignments(CampusRuntimeGameplayActorSnapshot actor)
        {
            if (actor == null || actor.Assignments == null)
            {
                return;
            }

            CampusGameplayFacilityMarker facility = FindNearestFacility(
                actor.FloorIndex,
                actor.Cell,
                CampusFacilityType.OfficeDesk,
                CampusFacilityType.Desk,
                CampusFacilityType.Storage);

            if (facility != null)
            {
                actor.Assignments.PrimaryWorkstationId = ResolveFacilityId(facility);
            }
        }

        private static CampusGameplayFacilityMarker FindNearestFacility(
            int floorIndex,
            Vector3Int cell,
            params CampusFacilityType[] types)
        {
            CampusGameplayFacilityMarker[] markers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayFacilityMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            CampusGameplayFacilityMarker best = null;
            int bestDistance = int.MaxValue;
            Vector3Int normalizedCell = NormalizeCell(cell);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayFacilityMarker marker = markers[i];
                if (marker == null ||
                    marker.FloorIndex != Mathf.Max(1, floorIndex) ||
                    !MatchesFacilityType(marker.FacilityType, types))
                {
                    continue;
                }

                Vector3Int markerCell = NormalizeCell(marker.Cell);
                int distance = Mathf.Abs(markerCell.x - normalizedCell.x) +
                               Mathf.Abs(markerCell.y - normalizedCell.y);
                if (distance < bestDistance)
                {
                    best = marker;
                    bestDistance = distance;
                }
            }

            return bestDistance <= 8 ? best : null;
        }

        private static bool MatchesFacilityType(CampusFacilityType type, CampusFacilityType[] candidates)
        {
            if (candidates == null)
            {
                return false;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (type == candidates[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveFacilityId(CampusGameplayFacilityMarker marker)
        {
            if (marker == null)
            {
                return string.Empty;
            }

            string id = CampusGameplayFacilityMarker.NormalizeFacilityId(marker.FacilityId);
            return string.IsNullOrWhiteSpace(id)
                ? CampusGameplayFacilityMarker.BuildStableFacilityId(marker.FloorIndex, marker.FacilityType, marker.Cell)
                : id;
        }

        private static CampusCharacterRuntime FindActorRuntime(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return null;
            }

            CampusCharacterRuntime[] runtimes =
                UnityEngine.Object.FindObjectsByType<CampusCharacterRuntime>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < runtimes.Length; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime != null && string.Equals(runtime.CharacterId, actorId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private static void DestroyActorRuntime(string actorId, RuntimeObjectDestroyer destroyObject)
        {
            CampusCharacterRuntime runtime = FindActorRuntime(actorId);
            if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return;
            }

            CampusRuntimeGameplayOverlayEntity entity = runtime.GetComponent<CampusRuntimeGameplayOverlayEntity>();
            if (entity == null || !entity.IsActorEntity)
            {
                return;
            }

            destroyObject(runtime.gameObject);
        }

        private static CampusCharacterData BuildCharacterData(CampusRuntimeGameplayActorSnapshot actor)
        {
            CampusCharacterData data = new CampusCharacterData();
            if (actor == null)
            {
                return data;
            }

            data.Configure(
                actor.Id,
                actor.DisplayName,
                actor.LocalizedDisplayName,
                actor.Role,
                actor.TeacherDuty,
                actor.ClassId,
                actor.InitialState,
                actor.IsPlayerControlled,
                actor.Sleepiness,
                actor.Mischief,
                actor.Traits,
                actor.StaffDuty,
                actor.InitialMoney);
            data.SetAssignments(actor.Assignments);
            return data;
        }

        private static CampusCharacterTrait[] CopyTraits(IReadOnlyList<CampusCharacterTrait> traits)
        {
            if (traits == null || traits.Count == 0)
            {
                return Array.Empty<CampusCharacterTrait>();
            }

            CampusCharacterTrait[] copy = new CampusCharacterTrait[traits.Count];
            for (int i = 0; i < traits.Count; i++)
            {
                copy[i] = traits[i];
            }

            return copy;
        }

        private static Vector3Int NormalizeCell(Vector3Int cell)
        {
            return new Vector3Int(cell.x, cell.y, 0);
        }
    }
}
