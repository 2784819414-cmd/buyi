using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private static bool TryBuildOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionChainRecord actionChain,
            ActionRecord action,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null ||
                action == null ||
                string.IsNullOrEmpty(action.ActionId) ||
                !Data.ActionTargetRulesByActionId.TryGetValue(action.ActionId, out List<ActionTargetRuleRecord> targetRules))
            {
                return false;
            }

            for (int i = 0; i < targetRules.Count; i++)
            {
                ActionTargetRuleRecord targetRule = targetRules[i];
                if (!AppliesToActionChain(targetRule, actionChain) ||
                    !PassesRuntimeRequirements(targetRule, npc))
                {
                    continue;
                }

                if (TryBuildOpportunityForTargetRule(npc, entry, action, targetRule, out opportunity))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildOpportunityForTargetRule(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null || action == null || targetRule == null)
            {
                return false;
            }

            switch (targetRule.TargetKind)
            {
                case CampusNpcEcologyTargetKind.RoomFacility:
                    return TryBuildRoomFacilityOpportunity(npc, entry, action, targetRule, out opportunity);
                case CampusNpcEcologyTargetKind.ServiceStation:
                    return TryBuildServiceStationOpportunity(npc, entry, action, targetRule, out opportunity);
                default:
                    if (!TryResolveTarget(npc, entry, targetRule, action, out ResolvedTarget target) ||
                        !TryBuildAction(action, target, out CampusCharacterAction characterAction))
                    {
                        return false;
                    }

                    return TryCreateOpportunity(entry, action, targetRule, target, characterAction, out opportunity);
            }
        }

        private static bool TryBuildRoomFacilityOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (!TryResolveTargetRoom(npc, targetRule, out CampusGameplayRoom room))
            {
                return false;
            }

            return TryBuildRoomScopedFacilityOpportunity(
                npc,
                entry,
                action,
                targetRule,
                room,
                out opportunity);
        }

        private static bool TryBuildServiceStationOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (!TryResolveServiceStationRoom(npc, action, targetRule, out CampusGameplayRoom room) ||
                !TryResolveServiceStation(room, npc, entry, action, targetRule, out CampusServiceStation station))
            {
                return false;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            bool canServeNow = CampusServiceStationRuntimeAvailability.CanServeNow(
                station,
                npc.WorldService,
                bootstrap != null ? bootstrap.RosterService : null,
                bootstrap != null ? bootstrap.TimeController : null);
            ResolvedTarget target = BuildServiceStationTarget(npc, entry, station);
            if (!target.IsValid)
            {
                return false;
            }

            if (canServeNow)
            {
                if (!TryBuildAction(action, target, out CampusCharacterAction characterAction))
                {
                    return false;
                }

                return TryCreateOpportunity(entry, action, targetRule, target, characterAction, out opportunity);
            }

            return TryCreateHoldOpportunity(
                entry,
                action,
                targetRule,
                target,
                TargetRetryHoldSeconds,
                out opportunity);
        }

        private static bool TryBuildRoomScopedFacilityOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            CampusGameplayRoom room,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (npc == null || room == null || targetRule == null)
            {
                return false;
            }

            if (!IsRuntimeInsideRoom(npc, room))
            {
                return TryCreateAreaOpportunity(
                    npc,
                    entry,
                    action,
                    targetRule,
                    room,
                    0f,
                    out opportunity);
            }

            if (TryResolveNearestFacilityTarget(npc, room, entry, targetRule, out ResolvedTarget target) &&
                TryBuildAction(action, target, out CampusCharacterAction characterAction) &&
                TryCreateOpportunity(entry, action, targetRule, target, characterAction, out opportunity))
            {
                return true;
            }

            return TryCreateAreaOpportunity(
                npc,
                entry,
                action,
                targetRule,
                room,
                TargetRetryHoldSeconds,
                out opportunity);
        }

        private static bool TryBuildAction(
            ActionRecord action,
            ResolvedTarget target,
            out CampusCharacterAction characterAction)
        {
            characterAction = CampusCharacterAction.NoOp();
            if (action == null)
            {
                return false;
            }

            switch (action.ActionMode)
            {
                case CampusNpcEcologyActionMode.NoOp:
                    return true;

                case CampusNpcEcologyActionMode.PressInteract:
                    if (target.TargetObject == null)
                    {
                        return false;
                    }

                    characterAction = CampusCharacterAction.PressInteract(target.TargetObject);
                    return true;

                case CampusNpcEcologyActionMode.PressInteractionAction:
                    if (target.TargetObject == null || string.IsNullOrWhiteSpace(action.ActionId))
                    {
                        return false;
                    }

                    characterAction = CampusCharacterAction.PressInteractionAction(
                        target.TargetObject,
                        action.ActionId,
                        action.Payload);
                    return true;

                case CampusNpcEcologyActionMode.DomainAction:
                    if (string.IsNullOrWhiteSpace(action.ActionId))
                    {
                        return false;
                    }

                    characterAction = CampusCharacterAction.DomainAction(
                        action.ActionId,
                        target.TargetObject,
                        action.Payload);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryCreateOpportunity(
            ScheduleEntryRecord entry,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            ResolvedTarget target,
            CampusCharacterAction characterAction,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null || action == null || targetRule == null || !target.IsValid)
            {
                return false;
            }

            string intentLabel = string.IsNullOrWhiteSpace(entry.IntentLabel) ? entry.Id : entry.IntentLabel;
            opportunity = CampusNpcActionOpportunity.MoveTo(
                action.ActionId,
                characterAction,
                target.Position,
                target.RoomId,
                targetRule.StopDistance,
                entry.Score,
                entry.IntentKind,
                intentLabel,
                target.RequireExactNavigation,
                target.TargetId);
            return true;
        }

        private static bool TryCreateAreaOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            CampusGameplayRoom room,
            float arrivalHoldSeconds,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null || action == null || targetRule == null)
            {
                return false;
            }

            ResolvedTarget areaTarget = BuildRoomAreaTarget(npc, room, entry);
            if (!areaTarget.IsValid)
            {
                return false;
            }

            if (arrivalHoldSeconds <= 0f)
            {
                return TryCreateOpportunity(
                    entry,
                    action,
                    targetRule,
                    areaTarget,
                    CampusCharacterAction.NoOp(),
                    out opportunity);
            }

            string intentLabel = string.IsNullOrWhiteSpace(entry.IntentLabel) ? entry.Id : entry.IntentLabel;
            opportunity = CampusNpcActionOpportunity.MoveToAndHold(
                action.ActionId,
                CampusCharacterAction.NoOp(),
                areaTarget.Position,
                areaTarget.RoomId,
                targetRule.StopDistance,
                entry.Score,
                entry.IntentKind,
                intentLabel,
                arrivalHoldSeconds,
                false,
                areaTarget.TargetId);
            return true;
        }

        private static bool TryCreateHoldOpportunity(
            ScheduleEntryRecord entry,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            ResolvedTarget target,
            float arrivalHoldSeconds,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null || action == null || targetRule == null || !target.IsValid)
            {
                return false;
            }

            string intentLabel = string.IsNullOrWhiteSpace(entry.IntentLabel) ? entry.Id : entry.IntentLabel;
            opportunity = CampusNpcActionOpportunity.MoveToAndHold(
                action.ActionId,
                CampusCharacterAction.NoOp(),
                target.Position,
                target.RoomId,
                targetRule.StopDistance,
                entry.Score,
                entry.IntentKind,
                intentLabel,
                arrivalHoldSeconds,
                target.RequireExactNavigation,
                target.TargetId);
            return true;
        }

        private static bool TryResolveTarget(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionTargetRuleRecord targetRule,
            ActionRecord action,
            out ResolvedTarget target)
        {
            target = default;
            if (npc == null || targetRule == null)
            {
                return false;
            }

            CampusNpcPersonalProfile profile = npc.Profile;
            switch (targetRule.TargetKind)
            {
                case CampusNpcEcologyTargetKind.StudentDesk:
                    return TryResolveAssignedFacilityTarget(
                        npc,
                        profile != null ? profile.StudentClassroomId : string.Empty,
                        profile != null ? profile.StudentDeskKey : string.Empty,
                        targetRule.FacilityTypes,
                        targetRule.NavigationFacilityTypes,
                        out target);

                case CampusNpcEcologyTargetKind.TeacherPodium:
                    return TryResolveAssignedFacilityTarget(
                        npc,
                        profile != null ? profile.TeacherClassroomId : string.Empty,
                        profile != null ? profile.TeacherPodiumKey : string.Empty,
                        targetRule.FacilityTypes,
                        targetRule.NavigationFacilityTypes,
                        out target);

                case CampusNpcEcologyTargetKind.OfficeDesk:
                    return TryResolveAssignedFacilityTarget(
                        npc,
                        profile != null ? profile.OfficeRoomId : string.Empty,
                        profile != null ? profile.OfficeDeskKey : string.Empty,
                        targetRule.FacilityTypes,
                        targetRule.NavigationFacilityTypes,
                        out target);

                case CampusNpcEcologyTargetKind.PrimaryWorkstation:
                    return TryResolveAssignedFacilityTarget(
                        npc,
                        profile != null ? profile.WorkRoomId : string.Empty,
                        profile != null ? profile.PrimaryWorkstationKey : string.Empty,
                        targetRule.FacilityTypes,
                        targetRule.NavigationFacilityTypes,
                        out target);

                case CampusNpcEcologyTargetKind.Dorm:
                    if (profile == null || profile.DormPosition == Vector3.zero)
                    {
                        return false;
                    }

                    target = CreateResolvedTarget(
                        null,
                        profile.DormPosition,
                        profile.DormRoomId,
                        false,
                        BuildRoomScopedTargetId(profile.DormRoomId, "dorm"));
                    return true;

                case CampusNpcEcologyTargetKind.Common:
                    if (profile != null && profile.CommonPosition != Vector3.zero)
                    {
                        target = CreateResolvedTarget(
                            null,
                            profile.CommonPosition,
                            profile.CommonRoomId,
                            false,
                            BuildRoomScopedTargetId(profile.CommonRoomId, "common"));
                        return true;
                    }

                    return TryResolveNearestRoomTarget(
                               npc,
                               CampusRoomType.CommonActivityZone,
                               entry,
                               out target) ||
                           TryResolveNearestRoomTarget(
                               npc,
                               CampusRoomType.Corridor,
                               entry,
                               out target);

                case CampusNpcEcologyTargetKind.RoomType:
                    return TryResolveNearestRoomTarget(npc, targetRule.RoomType, entry, out target);

                case CampusNpcEcologyTargetKind.DroppedStorageItem:
                    return TryResolveDroppedStorageItemTarget(npc, targetRule, out target);

                case CampusNpcEcologyTargetKind.None:
                    if (npc.Runtime == null || npc.Data == null)
                    {
                        return false;
                    }

                    target = CreateResolvedTarget(
                        npc.Runtime,
                        npc.Runtime.transform.position,
                        npc.Data.CurrentRoomId,
                        false,
                        BuildRoomScopedTargetId(
                            npc.Data.CurrentRoomId,
                            string.IsNullOrWhiteSpace(entry != null ? entry.Id : string.Empty)
                                ? action != null ? action.ActionId : string.Empty
                                : entry.Id));
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryResolveAssignedFacilityTarget(
            CampusNpcAiRuntime npc,
            string roomId,
            string facilityKey,
            CampusFacilityType[] facilityTypes,
            CampusFacilityType[] navigationFacilityTypes,
            out ResolvedTarget target)
        {
            target = default;
            if (npc == null || npc.WorldService == null)
            {
                return false;
            }

            CampusGameplayRoom room = CampusNpcRoomSelector.ResolveAssigned(
                npc.WorldService,
                roomId,
                CampusRoomType.Unknown);
            if (room != null &&
                CampusNpcFacilitySelector.TryFindInRoom(
                    room,
                    CampusNpcFacilitySelector.Collect(room, facilityTypes),
                    facilityKey,
                    out CampusGameplayRoom.FacilityRecord record))
            {
                if (!TryResolveNavigationFacility(room, record, navigationFacilityTypes, out CampusGameplayRoom.FacilityRecord navigationRecord))
                {
                    return false;
                }

                target = CreateResolvedTarget(
                    record.PlacedObject,
                    CampusNpcFacilityApproachResolver.ResolveApproachPosition(npc, room, navigationRecord),
                    room.RoomId,
                    CampusNpcFacilityApproachResolver.RequiresExactNavigation(navigationRecord),
                    CampusNpcFacilitySelector.KeyFor(room, record));
                return true;
            }

            return false;
        }

        private static bool TryResolveNavigationFacility(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord assignedRecord,
            CampusFacilityType[] navigationFacilityTypes,
            out CampusGameplayRoom.FacilityRecord navigationRecord)
        {
            navigationRecord = assignedRecord;
            if (room == null ||
                assignedRecord == null ||
                navigationFacilityTypes == null ||
                navigationFacilityTypes.Length == 0)
            {
                return assignedRecord != null;
            }

            string assignedKey = CampusNpcFacilitySelector.KeyFor(room, assignedRecord);
            IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = room.Facilities;
            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord candidate = facilities[i];
                if (candidate == null ||
                    !MatchesFacilityType(candidate.FacilityType, navigationFacilityTypes) ||
                    !MatchesOwnerFacilityId(candidate.OwnerFacilityId, assignedKey, assignedRecord.FacilityId))
                {
                    continue;
                }

                navigationRecord = candidate;
                return true;
            }

            navigationRecord = null;
            return false;
        }

        private static bool TryResolveDroppedStorageItemTarget(
            CampusNpcAiRuntime npc,
            ActionTargetRuleRecord targetRule,
            out ResolvedTarget target)
        {
            target = default;
            if (npc == null || npc.Runtime == null || targetRule == null)
            {
                return false;
            }

            IReadOnlyList<CampusDroppedStorageItem> items = CampusDroppedStorageItemRegistry.ActiveItems;
            CampusDroppedStorageItem best = null;
            float bestDistance = float.MaxValue;
            Vector3 npcPosition = npc.Runtime.transform.position;
            for (int i = 0; i < items.Count; i++)
            {
                CampusDroppedStorageItem item = items[i];
                if (!MatchesDroppedStorageItemFilter(npc, item, targetRule))
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude((Vector2)(item.transform.position - npcPosition));
                if (best == null || distance < bestDistance)
                {
                    best = item;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                return false;
            }

            target = CreateResolvedTarget(
                best,
                ResolveDroppedItemApproachPosition(npc, best),
                ResolveDroppedItemRoomId(npc, best),
                false,
                BuildDroppedItemTargetId(best));
            return true;
        }

        private static Vector3 ResolveDroppedItemApproachPosition(
            CampusNpcAiRuntime npc,
            CampusDroppedStorageItem item)
        {
            if (item == null)
            {
                return Vector3.zero;
            }

            CampusFloorRoot floor = item.GetComponentInParent<CampusFloorRoot>();
            if (floor == null)
            {
                CampusPlacedObject placedObject = item.GetComponent<CampusPlacedObject>();
                floor = placedObject != null ? placedObject.GetComponentInParent<CampusFloorRoot>() : null;
            }

            if (floor == null || floor.Grid == null)
            {
                return item.transform.position;
            }

            Vector3Int itemCell = floor.Grid.WorldToCell(item.transform.position);
            itemCell.z = 0;
            return CampusNpcFacilityApproachResolver.ResolveCellApproachPosition(
                npc,
                floor,
                null,
                itemCell,
                item.transform.position.z,
                2,
                false);
        }

        private static bool MatchesDroppedStorageItemFilter(
            CampusNpcAiRuntime npc,
            CampusDroppedStorageItem item,
            ActionTargetRuleRecord targetRule)
        {
            if (npc == null || item == null || targetRule == null)
            {
                return false;
            }

            if (string.Equals(targetRule.Owner, "Self", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.OwnerId, npc.Data != null ? npc.Data.Id : string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(targetRule.SourceLocation) &&
                !string.Equals(item.SourceLocation, targetRule.SourceLocation, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(targetRule.SourceContainerPrefix) &&
                !NormalizeId(item.SourceContainerId).StartsWith(targetRule.SourceContainerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.IsNullOrEmpty(targetRule.DefinitionId) ||
                   string.Equals(item.DefinitionId, targetRule.DefinitionId, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDroppedItemRoomId(CampusNpcAiRuntime npc, CampusDroppedStorageItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(item.SourceRoomId))
            {
                return item.SourceRoomId.Trim();
            }

            CampusPlacedObject placedObject = item.GetComponent<CampusPlacedObject>();
            CampusGameplayRoom room = npc != null && npc.WorldService != null && placedObject != null
                ? npc.WorldService.FindRoomForPosition(placedObject.FloorIndex, item.transform.position)
                : null;
            return room != null ? room.RoomId : string.Empty;
        }

        private static string BuildDroppedItemTargetId(CampusDroppedStorageItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string instanceId = NormalizeId(item.InstanceId);
            return string.IsNullOrEmpty(instanceId)
                ? "dropped_item:" + NormalizeId(item.DefinitionId)
                : "dropped_item:" + instanceId;
        }

        private static bool TryResolveTargetRoom(
            CampusNpcAiRuntime npc,
            ActionTargetRuleRecord targetRule,
            out CampusGameplayRoom room)
        {
            room = null;
            if (npc == null ||
                npc.WorldService == null ||
                targetRule == null ||
                targetRule.RoomType == CampusRoomType.Unknown)
            {
                return false;
            }

            List<CampusGameplayRoom> rooms = CampusNpcRoomSelector.GetRooms(
                npc.WorldService,
                targetRule.RoomType);
            if (rooms.Count == 0)
            {
                return false;
            }

            if (targetRule.TargetKind == CampusNpcEcologyTargetKind.RoomFacility)
            {
                if (targetRule.FacilityTypes.Length == 0)
                {
                    return false;
                }

                List<CampusGameplayRoom> candidateRooms = new List<CampusGameplayRoom>();
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (HasAnyFacility(rooms[i], targetRule.FacilityTypes))
                    {
                        candidateRooms.Add(rooms[i]);
                    }
                }

                rooms = candidateRooms;
                if (rooms.Count == 0)
                {
                    return false;
                }
            }

            CampusGameplayRoom currentRoom = npc.WorldService.FindRoomForRuntime(npc.Runtime);
            room = CampusNpcRoomSelector.ChooseNearest(
                rooms,
                npc.Runtime.transform.position,
                currentRoom != null ? currentRoom.RoomId : string.Empty);
            return room != null;
        }

        private static bool TryResolveServiceStationRoom(
            CampusNpcAiRuntime npc,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            out CampusGameplayRoom room)
        {
            room = null;
            if (npc == null ||
                npc.WorldService == null ||
                action == null ||
                targetRule == null ||
                string.IsNullOrEmpty(action.ActionId))
            {
                return false;
            }

            List<CampusServiceStation> candidateStations = npc.WorldService.ServiceStations.Find(
                action.ActionId,
                targetRule.StationTypeIds,
                targetRule.RoomType);
            if (candidateStations.Count == 0)
            {
                return false;
            }

            List<CampusGameplayRoom> candidateRooms = new List<CampusGameplayRoom>();
            for (int i = 0; i < candidateStations.Count; i++)
            {
                CampusGameplayRoom candidateRoom = candidateStations[i].Room;
                if (candidateRoom != null && !ContainsRoom(candidateRooms, candidateRoom))
                {
                    candidateRooms.Add(candidateRoom);
                }
            }

            CampusGameplayRoom currentRoom = npc.WorldService.FindRoomForRuntime(npc.Runtime);
            room = CampusNpcRoomSelector.ChooseNearest(
                candidateRooms,
                npc.Runtime.transform.position,
                currentRoom != null ? currentRoom.RoomId : string.Empty);
            return room != null;
        }

        private static bool TryResolveNearestRoomTarget(
            CampusNpcAiRuntime npc,
            CampusRoomType roomType,
            ScheduleEntryRecord entry,
            out ResolvedTarget target)
        {
            target = default;
            if (npc == null || npc.WorldService == null || roomType == CampusRoomType.Unknown)
            {
                return false;
            }

            List<CampusGameplayRoom> rooms = CampusNpcRoomSelector.GetRooms(npc.WorldService, roomType);
            if (rooms.Count == 0)
            {
                return false;
            }

            CampusGameplayRoom currentRoom = npc.WorldService.FindRoomForRuntime(npc.Runtime);
            CampusGameplayRoom room = CampusNpcRoomSelector.ChooseNearest(
                rooms,
                npc.Runtime.transform.position,
                currentRoom != null && currentRoom.RoomType == roomType ? currentRoom.RoomId : string.Empty);
            if (room == null)
            {
                return false;
            }

            target = BuildRoomAreaTarget(npc, room, entry);
            return target.IsValid;
        }

        private static bool TryResolveNearestFacilityTarget(
            CampusNpcAiRuntime npc,
            CampusGameplayRoom room,
            ScheduleEntryRecord entry,
            ActionTargetRuleRecord targetRule,
            out ResolvedTarget target)
        {
            target = default;
            if (npc == null || room == null || targetRule == null || targetRule.FacilityTypes.Length == 0)
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> facilities = CampusNpcFacilitySelector.Collect(room, targetRule.FacilityTypes);
            if (facilities.Count == 0)
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> candidates = new List<CampusGameplayRoom.FacilityRecord>();
            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord record = facilities[i];
                if (!CampusNpcFacilityRuntimeAvailability.CanUseAsNpcTarget(room, record))
                {
                    continue;
                }

                if (!MatchesObjectIdFilter(record, targetRule.ObjectIds))
                {
                    continue;
                }

                candidates.Add(record);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            CampusGameplayRoom.FacilityRecord selectedFacility =
                candidates[ChooseFacilityTargetIndex(npc, entry, candidates.Count)];
            target = CreateResolvedTarget(
                selectedFacility.PlacedObject,
                CampusNpcFacilityApproachResolver.ResolveApproachPosition(npc, room, selectedFacility),
                room.RoomId,
                false,
                CampusNpcFacilitySelector.KeyFor(room, selectedFacility));
            return true;
        }

        private static bool TryResolveServiceStation(
            CampusGameplayRoom room,
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionRecord action,
            ActionTargetRuleRecord targetRule,
            out CampusServiceStation station)
        {
            station = default;
            if (room == null || action == null)
            {
                return false;
            }

            List<CampusServiceStation> stations = npc.WorldService.ServiceStations.FindInRoom(
                room,
                action.ActionId,
                targetRule.StationTypeIds);
            if (stations.Count == 0)
            {
                return false;
            }

            station = stations[ChooseServiceStationIndex(npc, entry, stations.Count)];
            return station.HasInteractionFacility;
        }

        private static ResolvedTarget BuildServiceStationTarget(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            CampusServiceStation station)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusGameplayRoom.FacilityRecord navigationRecord = CampusServiceStationRuntimeAvailability.CanServeNow(
                    station,
                    npc != null ? npc.WorldService : null,
                    bootstrap != null ? bootstrap.RosterService : null,
                    bootstrap != null ? bootstrap.TimeController : null)
                ? (station.HasCustomerSlot ? station.CustomerSlot : station.InteractionFacility)
                : ChooseServiceStationWaitSlot(npc, entry, station);
            if (station.Room == null || navigationRecord == null)
            {
                return default;
            }

            string navigationTargetId = CampusNpcFacilitySelector.KeyFor(station.Room, navigationRecord);
            string stationId = NormalizeId(station.StationId);
            string targetId = !string.IsNullOrEmpty(stationId) && !string.IsNullOrEmpty(navigationTargetId)
                ? stationId + ":" + navigationTargetId
                : !string.IsNullOrEmpty(navigationTargetId) ? navigationTargetId : stationId;

            return CreateResolvedTarget(
                station.InteractionFacility != null ? station.InteractionFacility.PlacedObject : null,
                CampusServiceStation.PositionOf(navigationRecord),
                station.Room.RoomId,
                CampusNpcFacilityApproachResolver.RequiresExactNavigation(navigationRecord),
                targetId);
        }

        private static CampusGameplayRoom.FacilityRecord ChooseServiceStationWaitSlot(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            CampusServiceStation station)
        {
            if (station.QueueSlots.Count > 0)
            {
                int waitIndex = ChooseServiceStationIndex(npc, entry, station.QueueSlots.Count);
                return station.QueueSlots[waitIndex];
            }

            if (station.HasCustomerSlot)
            {
                return station.CustomerSlot;
            }

            return station.InteractionFacility;
        }

        private static bool HasAnyFacility(
            CampusGameplayRoom room,
            CampusFacilityType[] facilityTypes)
        {
            if (room == null || facilityTypes == null || facilityTypes.Length == 0)
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> facilities =
                CampusNpcFacilitySelector.Collect(room, facilityTypes);
            return facilities.Count > 0;
        }

        private static bool ContainsRoom(
            IReadOnlyList<CampusGameplayRoom> rooms,
            CampusGameplayRoom target)
        {
            if (rooms == null || target == null)
            {
                return false;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room != null &&
                    string.Equals(room.RoomId, target.RoomId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesObjectIdFilter(
            CampusGameplayRoom.FacilityRecord record,
            string[] objectIds)
        {
            if (objectIds == null || objectIds.Length == 0)
            {
                return true;
            }

            CampusPlacedObject placed = record != null ? record.PlacedObject : null;
            string objectId = NormalizeId(placed != null ? placed.ObjectId : string.Empty);
            if (string.IsNullOrEmpty(objectId))
            {
                return false;
            }

            return ContainsId(objectIds, objectId);
        }

        private static bool MatchesFacilityType(
            CampusFacilityType type,
            CampusFacilityType[] allowedTypes)
        {
            if (allowedTypes == null || allowedTypes.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (type == allowedTypes[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesOwnerFacilityId(
            string ownerFacilityId,
            string assignedKey,
            string assignedRecordId)
        {
            string normalizedOwner = NormalizeId(ownerFacilityId);
            if (string.IsNullOrEmpty(normalizedOwner))
            {
                return false;
            }

            return string.Equals(normalizedOwner, NormalizeId(assignedKey), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalizedOwner, NormalizeId(assignedRecordId), StringComparison.OrdinalIgnoreCase);
        }

        private static int ChooseFacilityTargetIndex(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            int candidateCount)
        {
            string actorKey = CampusNpcStableIds.CharacterKey(npc != null ? npc.Runtime : null);
            int visitSalt = npc != null ? npc.CurrentClockMinute / 10 : 0;
            int entrySalt = CampusNpcStableIds.Hash(entry != null ? entry.Id : string.Empty);
            return CampusNpcStableIds.PositiveModulo(
                CampusNpcStableIds.Hash(actorKey) + entrySalt + visitSalt,
                candidateCount);
        }

        private static int ChooseServiceStationIndex(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            int candidateCount)
        {
            return ChooseFacilityTargetIndex(npc, entry, candidateCount);
        }

        private static ResolvedTarget BuildRoomAreaTarget(
            CampusNpcAiRuntime npc,
            CampusGameplayRoom room,
            ScheduleEntryRecord entry)
        {
            if (room == null)
            {
                return default;
            }

            int seed = (npc != null ? npc.PersonalSeed : 1) +
                       CampusNpcStableIds.Hash(entry != null ? entry.Id : string.Empty) +
                       Mathf.FloorToInt((npc != null ? npc.Time : Time.time) * 0.6f) * 37;
            BoundsInt bounds = room.MarkerBounds;
            int width = Mathf.Max(1, bounds.size.x);
            int height = Mathf.Max(1, bounds.size.y);
            int x = bounds.xMin + CampusNpcStableIds.PositiveModulo(seed * 17 + 11, width);
            int y = bounds.yMin + CampusNpcStableIds.PositiveModulo(seed * 31 + 7, height);

            return CreateResolvedTarget(
                null,
                new Vector3(x + 0.5f, y + 0.5f, 0f),
                room.RoomId,
                false,
                BuildRoomScopedTargetId(
                    room.RoomId,
                    string.IsNullOrEmpty(NormalizeId(entry != null ? entry.Id : string.Empty))
                        ? "area"
                        : "area:" + NormalizeId(entry != null ? entry.Id : string.Empty)));
        }

        private static bool IsRuntimeInsideRoom(CampusNpcAiRuntime npc, CampusGameplayRoom room)
        {
            if (npc == null || room == null || npc.WorldService == null || npc.Runtime == null)
            {
                return false;
            }

            CampusGameplayRoom currentRoom = npc.WorldService.FindRoomForRuntime(npc.Runtime);
            return currentRoom != null &&
                   string.Equals(currentRoom.RoomId, room.RoomId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PassesRuntimeRequirements(
            ActionTargetRuleRecord targetRule,
            CampusNpcAiRuntime npc)
        {
            if (targetRule == null || npc == null)
            {
                return false;
            }

            return CampusNpcActionRequirementCatalog.PassesAll(
                npc.Runtime,
                targetRule.RequirementIds);
        }

        private static bool AppliesToActionChain(
            ActionTargetRuleRecord targetRule,
            ActionChainRecord actionChain)
        {
            if (targetRule == null || targetRule.ActionChainIds.Length == 0)
            {
                return true;
            }

            return actionChain != null && ContainsId(targetRule.ActionChainIds, actionChain.Id);
        }

        private static ResolvedTarget CreateResolvedTarget(
            UnityEngine.Object targetObject,
            Vector3 position,
            string roomId,
            bool requireExactNavigation,
            string targetId)
        {
            return new ResolvedTarget
            {
                IsValid = true,
                TargetObject = targetObject,
                Position = position,
                RoomId = roomId ?? string.Empty,
                RequireExactNavigation = requireExactNavigation,
                TargetId = NormalizeId(targetId)
            };
        }

        private static string BuildRoomScopedTargetId(string roomId, string suffix)
        {
            string normalizedRoomId = NormalizeId(roomId);
            string normalizedSuffix = NormalizeId(suffix);
            if (string.IsNullOrEmpty(normalizedRoomId))
            {
                return normalizedSuffix;
            }

            return string.IsNullOrEmpty(normalizedSuffix)
                ? normalizedRoomId
                : normalizedRoomId + ":" + normalizedSuffix;
        }
    }
}
