using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private static bool TryBuildOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionDefinitionRecord actionDefinition,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null || actionDefinition == null)
            {
                return false;
            }

            switch (actionDefinition.TargetKind)
            {
                case CampusNpcEcologyTargetKind.RoomFacility:
                    return TryBuildRoomFacilityOpportunity(npc, entry, actionDefinition, out opportunity);
                case CampusNpcEcologyTargetKind.ServiceStation:
                    return TryBuildServiceStationOpportunity(npc, entry, actionDefinition, out opportunity);
                default:
                    if (!TryResolveTarget(npc, entry, actionDefinition, out ResolvedTarget target) ||
                        !TryBuildAction(actionDefinition, target, out CampusCharacterAction action))
                    {
                        return false;
                    }

                    return TryCreateOpportunity(entry, actionDefinition, target, action, out opportunity);
            }
        }

        private static bool TryBuildRoomFacilityOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionDefinitionRecord actionDefinition,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (!TryResolveTargetRoom(npc, actionDefinition, out CampusGameplayRoom room))
            {
                return false;
            }

            return TryBuildRoomScopedFacilityOpportunity(
                npc,
                entry,
                actionDefinition,
                room,
                TryResolveNearestFacilityTarget,
                out opportunity);
        }

        private static bool TryBuildServiceStationOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionDefinitionRecord actionDefinition,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (!TryResolveServiceStationRoom(npc, actionDefinition, out CampusGameplayRoom room) ||
                !TryResolveServiceStation(room, npc, entry, actionDefinition, out CampusServiceStation station))
            {
                return false;
            }

            bool canServeNow = CampusServiceStationRuntimeAvailability.CanServeNow(station);
            ResolvedTarget target = BuildServiceStationTarget(npc, entry, station);
            if (!target.IsValid)
            {
                return false;
            }

            if (canServeNow)
            {
                if (!TryBuildAction(actionDefinition, target, out CampusCharacterAction action))
                {
                    return false;
                }

                return TryCreateOpportunity(entry, actionDefinition, target, action, out opportunity);
            }

            return TryCreateHoldOpportunity(
                entry,
                actionDefinition,
                target,
                TargetRetryHoldSeconds,
                out opportunity);
        }

        private static bool TryBuildRoomScopedFacilityOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionDefinitionRecord actionDefinition,
            CampusGameplayRoom room,
            RoomScopedTargetResolver resolver,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (npc == null || room == null || resolver == null)
            {
                return false;
            }

            if (!IsRuntimeInsideRoom(npc, room))
            {
                return TryCreateAreaOpportunity(
                    npc,
                    entry,
                    actionDefinition,
                    room,
                    0f,
                    out opportunity);
            }

            if (resolver(npc, room, entry, actionDefinition, out ResolvedTarget target) &&
                TryBuildAction(actionDefinition, target, out CampusCharacterAction action) &&
                TryCreateOpportunity(entry, actionDefinition, target, action, out opportunity))
            {
                return true;
            }

            return TryCreateAreaOpportunity(
                npc,
                entry,
                actionDefinition,
                room,
                TargetRetryHoldSeconds,
                out opportunity);
        }

        private static bool TryBuildAction(
            ActionDefinitionRecord actionDefinition,
            ResolvedTarget target,
            out CampusCharacterAction action)
        {
            action = CampusCharacterAction.NoOp();
            switch (actionDefinition.ActionMode)
            {
                case CampusNpcEcologyActionMode.NoOp:
                    return true;

                case CampusNpcEcologyActionMode.PressInteract:
                    if (target.TargetObject == null)
                    {
                        return false;
                    }

                    action = CampusCharacterAction.PressInteract(target.TargetObject);
                    return true;

                case CampusNpcEcologyActionMode.PressInteractionAction:
                    if (target.TargetObject == null ||
                        string.IsNullOrWhiteSpace(actionDefinition.ExecuteActionId))
                    {
                        return false;
                    }

                    action = CampusCharacterAction.PressInteractionAction(
                        target.TargetObject,
                        actionDefinition.ExecuteActionId,
                        actionDefinition.Payload);
                    return true;

                case CampusNpcEcologyActionMode.DomainAction:
                    if (string.IsNullOrWhiteSpace(actionDefinition.ExecuteActionId))
                    {
                        return false;
                    }

                    action = CampusCharacterAction.DomainAction(
                        actionDefinition.ExecuteActionId,
                        target.TargetObject,
                        actionDefinition.Payload);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryCreateOpportunity(
            ScheduleEntryRecord entry,
            ActionDefinitionRecord actionDefinition,
            ResolvedTarget target,
            CampusCharacterAction action,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null || actionDefinition == null || !target.IsValid)
            {
                return false;
            }

            string intentLabel = string.IsNullOrWhiteSpace(entry.IntentLabel) ? entry.Id : entry.IntentLabel;
            string actionId = string.IsNullOrWhiteSpace(entry.Id) ? actionDefinition.Id : entry.Id;
            opportunity = CampusNpcActionOpportunity.MoveTo(
                actionId,
                action,
                target.Position,
                target.RoomId,
                actionDefinition.StopDistance,
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
            ActionDefinitionRecord actionDefinition,
            CampusGameplayRoom room,
            float arrivalHoldSeconds,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null || actionDefinition == null)
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
                    actionDefinition,
                    areaTarget,
                    CampusCharacterAction.NoOp(),
                    out opportunity);
            }

            string intentLabel = string.IsNullOrWhiteSpace(entry.IntentLabel) ? entry.Id : entry.IntentLabel;
            string actionId = string.IsNullOrWhiteSpace(entry.Id) ? actionDefinition.Id : entry.Id;
            opportunity = CampusNpcActionOpportunity.MoveToAndHold(
                actionId,
                CampusCharacterAction.NoOp(),
                areaTarget.Position,
                areaTarget.RoomId,
                actionDefinition.StopDistance,
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
            ActionDefinitionRecord actionDefinition,
            ResolvedTarget target,
            float arrivalHoldSeconds,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (entry == null || actionDefinition == null || !target.IsValid)
            {
                return false;
            }

            string intentLabel = string.IsNullOrWhiteSpace(entry.IntentLabel) ? entry.Id : entry.IntentLabel;
            string actionId = string.IsNullOrWhiteSpace(entry.Id) ? actionDefinition.Id : entry.Id;
            opportunity = CampusNpcActionOpportunity.MoveToAndHold(
                actionId,
                CampusCharacterAction.NoOp(),
                target.Position,
                target.RoomId,
                actionDefinition.StopDistance,
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
            ActionDefinitionRecord actionDefinition,
            out ResolvedTarget target)
        {
            target = default;
            CampusNpcPersonalProfile profile = npc.Profile;
            switch (actionDefinition.TargetKind)
            {
                case CampusNpcEcologyTargetKind.StudentDesk:
                    return TryResolveAssignedFacilityTarget(
                        npc,
                        profile != null ? profile.StudentClassroomId : string.Empty,
                        profile != null ? profile.StudentDeskKey : string.Empty,
                        profile != null ? profile.StudentDeskPosition : Vector3.zero,
                        CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.StudentDesks),
                        out target);

                case CampusNpcEcologyTargetKind.TeacherPodium:
                    return TryResolveAssignedFacilityTarget(
                        npc,
                        profile != null ? profile.TeacherClassroomId : string.Empty,
                        profile != null ? profile.TeacherPodiumKey : string.Empty,
                        profile != null ? profile.TeacherPodiumPosition : Vector3.zero,
                        CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.Podiums),
                        out target);

                case CampusNpcEcologyTargetKind.OfficeDesk:
                    return TryResolveAssignedFacilityTarget(
                        npc,
                        profile != null ? profile.OfficeRoomId : string.Empty,
                        profile != null ? profile.OfficeDeskKey : string.Empty,
                        profile != null ? profile.OfficeDeskPosition : Vector3.zero,
                        CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.OfficeDesks),
                        out target);

                case CampusNpcEcologyTargetKind.PrimaryWorkstation:
                    return TryResolveAssignedFacilityTarget(
                        npc,
                        profile != null ? profile.WorkRoomId : string.Empty,
                        profile != null ? profile.PrimaryWorkstationKey : string.Empty,
                        profile != null ? profile.PrimaryWorkstationPosition : Vector3.zero,
                        ResolvePrimaryWorkstationFacilityTypes(npc),
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
                    return TryResolveNearestRoomTarget(npc, actionDefinition.RoomType, entry, out target);

                case CampusNpcEcologyTargetKind.None:
                    target = CreateResolvedTarget(
                        npc != null ? npc.Runtime : null,
                        npc.Runtime.transform.position,
                        npc.Data.CurrentRoomId,
                        false,
                        BuildRoomScopedTargetId(
                            npc.Data.CurrentRoomId,
                            string.IsNullOrWhiteSpace(entry != null ? entry.Id : string.Empty)
                                ? actionDefinition.Id
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
            Vector3 fallbackPosition,
            CampusFacilityType[] facilityTypes,
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
                target = CreateResolvedTarget(
                    record.PlacedObject,
                    CampusNpcFacilitySelector.PositionOf(record),
                    room.RoomId,
                    RequiresExactAssignedFacilityNavigation(record),
                    CampusNpcFacilitySelector.KeyFor(room, record));
                return true;
            }

            // Assigned facility fallback still requires an explicit cached position.
            if (fallbackPosition != Vector3.zero)
            {
                target = CreateResolvedTarget(
                    null,
                    fallbackPosition,
                    roomId,
                    false,
                    NormalizeId(facilityKey));
                return true;
            }

            return false;
        }

        private static CampusFacilityType[] ResolvePrimaryWorkstationFacilityTypes(CampusNpcAiRuntime npc)
        {
            CampusCharacterData data = npc != null ? npc.Data : null;
            if (data != null &&
                data.Role == CampusCharacterRole.Staff &&
                (data.StaffDuty & CampusStaffDuty.SupportStaff) != 0)
            {
                return CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.WorkerStands);
            }

            return CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.Workstations);
        }

        private static bool RequiresExactAssignedFacilityNavigation(CampusGameplayRoom.FacilityRecord record)
        {
            if (record == null)
            {
                return false;
            }

            switch (record.FacilityType)
            {
                case CampusFacilityType.WorkerStandPoint:
                case CampusFacilityType.WaitingPoint:
                case CampusFacilityType.PickupPoint:
                case CampusFacilityType.DropPoint:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryResolveTargetRoom(
            CampusNpcAiRuntime npc,
            ActionDefinitionRecord actionDefinition,
            out CampusGameplayRoom room)
        {
            room = null;
            if (npc == null ||
                npc.WorldService == null ||
                actionDefinition == null ||
                actionDefinition.RoomType == CampusRoomType.Unknown)
            {
                return false;
            }

            List<CampusGameplayRoom> rooms = CampusNpcRoomSelector.GetRooms(
                npc.WorldService,
                actionDefinition.RoomType);
            if (rooms.Count == 0)
            {
                return false;
            }

            if (actionDefinition.TargetKind == CampusNpcEcologyTargetKind.RoomFacility)
            {
                CampusFacilityType[] facilityTypes = GetFacilityGroup(actionDefinition.FacilityGroupId);
                if (facilityTypes.Length == 0)
                {
                    return false;
                }

                List<CampusGameplayRoom> candidateRooms = new List<CampusGameplayRoom>();
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (HasAnyFacility(rooms[i], facilityTypes))
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
            ActionDefinitionRecord actionDefinition,
            out CampusGameplayRoom room)
        {
            room = null;
            if (npc == null ||
                npc.WorldService == null ||
                actionDefinition == null ||
                string.IsNullOrEmpty(actionDefinition.ExecuteActionId))
            {
                return false;
            }

            List<CampusGameplayRoom> rooms = actionDefinition.RoomType != CampusRoomType.Unknown
                ? CampusNpcRoomSelector.GetRooms(npc.WorldService, actionDefinition.RoomType)
                : new List<CampusGameplayRoom>(npc.WorldService.RoomRegistry != null
                    ? npc.WorldService.RoomRegistry.Rooms
                    : Array.Empty<CampusGameplayRoom>());
            if (rooms.Count == 0)
            {
                return false;
            }

            List<CampusGameplayRoom> candidateRooms = new List<CampusGameplayRoom>();
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom candidateRoom = rooms[i];
                if (candidateRoom == null ||
                    CampusServiceStationCatalog.Collect(candidateRoom, actionDefinition.ExecuteActionId).Count == 0)
                {
                    continue;
                }

                candidateRooms.Add(candidateRoom);
            }

            if (candidateRooms.Count == 0)
            {
                return false;
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
            ActionDefinitionRecord actionDefinition,
            out ResolvedTarget target)
        {
            target = default;
            CampusFacilityType[] facilityTypes = GetFacilityGroup(actionDefinition.FacilityGroupId);
            if (npc == null || room == null || facilityTypes.Length == 0)
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> facilities = CampusNpcFacilitySelector.Collect(room, facilityTypes);
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
                CampusNpcFacilitySelector.PositionOf(selectedFacility),
                room.RoomId,
                false,
                CampusNpcFacilitySelector.KeyFor(room, selectedFacility));
            return true;
        }

        private static bool TryResolveServiceStation(
            CampusGameplayRoom room,
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionDefinitionRecord actionDefinition,
            out CampusServiceStation station)
        {
            station = default;
            if (room == null || actionDefinition == null)
            {
                return false;
            }

            List<CampusServiceStation> stations = CampusServiceStationCatalog.Collect(
                room,
                actionDefinition.ExecuteActionId);
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
            CampusGameplayRoom.FacilityRecord navigationRecord = CampusServiceStationRuntimeAvailability.CanServeNow(station)
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
                RequiresExactAssignedFacilityNavigation(navigationRecord),
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
