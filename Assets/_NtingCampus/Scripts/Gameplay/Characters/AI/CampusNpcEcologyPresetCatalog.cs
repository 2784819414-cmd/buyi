using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal enum CampusNpcEcologyScheduleWindow
    {
        Always = 0,
        ClassSession = 1,
        Break = 2,
        MealPeak = 3,
        StudentFreeMovement = 4,
        DormWindow = 5,
        TeacherOfficeWindow = 6,
        StaffOffDuty = 7
    }

    internal enum CampusNpcEcologyTargetKind
    {
        None = 0,
        StudentDesk = 1,
        TeacherPodium = 2,
        OfficeDesk = 3,
        PrimaryWorkstation = 4,
        Dorm = 5,
        Common = 6,
        RoomType = 7,
        RoomFacility = 8
    }

    internal enum CampusNpcEcologyActionMode
    {
        NoOp = 0,
        PressInteract = 1,
        PressInteractionAction = 2,
        DomainAction = 3
    }

    internal static class CampusNpcEcologyPresetCatalog
    {
        private const string PresetFileName = "NpcEcologyPresets.json";

        private static EcologyPresetData cachedData;

        private static readonly EcologyPresetData BuiltInData = BuildBuiltInData();

        public static CampusFacilityType[] GetFacilityGroup(string groupId)
        {
            string normalizedId = NormalizeId(groupId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return Array.Empty<CampusFacilityType>();
            }

            EcologyPresetData data = Data;
            for (int i = 0; i < data.FacilityGroups.Count; i++)
            {
                FacilityGroupRecord record = data.FacilityGroups[i];
                if (record != null && string.Equals(record.Id, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return record.FacilityTypes;
                }
            }

            return Array.Empty<CampusFacilityType>();
        }

        public static bool TryResolveDefaultIntent(CampusNpcAiRuntime npc, out CampusNpcIntent intent)
        {
            intent = null;
            if (npc == null || npc.Data == null)
            {
                return false;
            }

            EcologyPresetData data = Data;
            for (int i = 0; i < data.RolePlans.Count; i++)
            {
                RolePlanRecord record = data.RolePlans[i];
                if (!MatchesRolePlan(record, npc))
                {
                    continue;
                }

                if (TryBuildIntentFromPlan(npc, record, out intent))
                {
                    return true;
                }
            }

            return false;
        }

        public static void CollectOpportunities(
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results)
        {
            if (!npc.IsValid || results == null)
            {
                return;
            }

            EcologyPresetData data = Data;
            for (int i = 0; i < data.Opportunities.Count; i++)
            {
                OpportunityRecord record = data.Opportunities[i];
                if (!MatchesOpportunity(record, npc, query))
                {
                    continue;
                }

                if (TryBuildOpportunity(npc.RuntimeState, record, out CampusNpcActionOpportunity opportunity))
                {
                    results.Add(opportunity);
                }
            }
        }

        public static bool EnableSelectionDebug => Data.EnableSelectionDebug;

        private static EcologyPresetData Data
        {
            get
            {
                if (cachedData == null)
                {
                    cachedData = LoadData();
                }

                return cachedData;
            }
        }

        private static EcologyPresetData BuildBuiltInData()
        {
            EcologyPresetData data = new EcologyPresetData();
            data.FacilityGroups.Add(FacilityGroupRecord.Create(
                CampusNpcFacilityGroups.StudentDesks,
                CampusFacilityType.StudentDesk));
            data.FacilityGroups.Add(FacilityGroupRecord.Create(
                CampusNpcFacilityGroups.Podiums,
                CampusFacilityType.Podium,
                CampusFacilityType.Blackboard));
            data.FacilityGroups.Add(FacilityGroupRecord.Create(
                CampusNpcFacilityGroups.OfficeDesks,
                CampusFacilityType.OfficeDesk,
                CampusFacilityType.Desk));
            data.FacilityGroups.Add(FacilityGroupRecord.Create(
                CampusNpcFacilityGroups.Workstations,
                CampusFacilityType.OfficeDesk,
                CampusFacilityType.Desk,
                CampusFacilityType.Storage));

            data.RolePlans.Add(RolePlanRecord.Create(
                "student_class_session",
                CampusCharacterRole.Student,
                CampusNpcEcologyScheduleWindow.ClassSession,
                CampusNpcEcologyTargetKind.StudentDesk,
                CampusNpcIntentKind.AttendAssignedDesk,
                "Class",
                0.16f,
                0.25f));
            data.RolePlans.Add(RolePlanRecord.Create(
                "student_dorm_window",
                CampusCharacterRole.Student,
                CampusNpcEcologyScheduleWindow.DormWindow,
                CampusNpcEcologyTargetKind.Dorm,
                CampusNpcIntentKind.RestInDorm,
                "Dorm",
                0.22f,
                0.35f));
            data.RolePlans.Add(RolePlanRecord.Create(
                "teacher_class_session",
                CampusCharacterRole.Teacher,
                CampusNpcEcologyScheduleWindow.ClassSession,
                CampusNpcEcologyTargetKind.TeacherPodium,
                CampusNpcIntentKind.TeachAssignedClass,
                "Teach",
                0.18f,
                0.20f));
            data.RolePlans.Add(RolePlanRecord.Create(
                "teacher_office_window",
                CampusCharacterRole.Teacher,
                CampusNpcEcologyScheduleWindow.TeacherOfficeWindow,
                CampusNpcEcologyTargetKind.OfficeDesk,
                CampusNpcIntentKind.ReturnToOfficeDesk,
                "Office",
                0.18f,
                0.25f));
            return data;
        }

        private static EcologyPresetData LoadData()
        {
            if (!CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                return BuiltInData;
            }

            try
            {
                EcologyPresetFile file = JsonUtility.FromJson<EcologyPresetFile>(json);
                EcologyPresetData parsed = ParseFile(file);
                return parsed.IsEmpty ? BuiltInData : parsed;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Failed to load " + PresetFileName + ": " + exception.Message);
                return BuiltInData;
            }
        }

        private static EcologyPresetData ParseFile(EcologyPresetFile file)
        {
            EcologyPresetData data = new EcologyPresetData();
            if (file == null)
            {
                return data;
            }

            data.EnableSelectionDebug = file.EnableSelectionDebug;

            if (file.FacilityGroups != null)
            {
                for (int i = 0; i < file.FacilityGroups.Count; i++)
                {
                    FacilityGroupFileRecord record = file.FacilityGroups[i];
                    if (record == null)
                    {
                        continue;
                    }

                    string id = NormalizeId(record.Id);
                    CampusFacilityType[] facilityTypes = ParseFacilityTypes(record.FacilityTypes);
                    if (string.IsNullOrEmpty(id) || facilityTypes.Length == 0)
                    {
                        continue;
                    }

                    data.FacilityGroups.Add(new FacilityGroupRecord(id, facilityTypes));
                }
            }

            if (file.RolePlans != null)
            {
                for (int i = 0; i < file.RolePlans.Count; i++)
                {
                    RolePlanFileRecord record = file.RolePlans[i];
                    if (record == null)
                    {
                        continue;
                    }

                    string id = NormalizeId(record.Id);
                    CampusCharacterRole role = ParseEnum(record.Role, CampusCharacterRole.Student);
                    CampusNpcEcologyTargetKind targetKind = ParseEnum(record.TargetKind, CampusNpcEcologyTargetKind.None);
                    if (string.IsNullOrEmpty(id) || targetKind == CampusNpcEcologyTargetKind.None)
                    {
                        continue;
                    }

                    data.RolePlans.Add(new RolePlanRecord(
                        id,
                        role,
                        ParseEnum(record.ScheduleWindow, CampusNpcEcologyScheduleWindow.Always),
                        ParseTeacherDuties(record.TeacherDuties),
                        ParseStaffDuties(record.StaffDuties),
                        ParseTraits(record.Traits),
                        targetKind,
                        ParseEnum(record.IntentKind, CampusNpcIntentKind.Roam),
                        string.IsNullOrWhiteSpace(record.IntentLabel) ? id : record.IntentLabel.Trim(),
                        ParseEnum(record.RoomType, CampusRoomType.Unknown),
                        NormalizeId(record.FacilityGroupId),
                        Mathf.Max(0.02f, record.StopDistance <= 0f ? 0.18f : record.StopDistance),
                        Mathf.Max(0f, record.RoomTargetRadius),
                        Mathf.Max(0f, record.HoldSeconds)));
                }
            }

            if (file.Opportunities != null)
            {
                for (int i = 0; i < file.Opportunities.Count; i++)
                {
                    OpportunityFileRecord record = file.Opportunities[i];
                    if (record == null)
                    {
                        continue;
                    }

                    string id = NormalizeId(record.Id);
                    CampusNpcEcologyTargetKind targetKind = ParseEnum(record.TargetKind, CampusNpcEcologyTargetKind.None);
                    if (string.IsNullOrEmpty(id) || targetKind == CampusNpcEcologyTargetKind.None)
                    {
                        continue;
                    }

                    data.Opportunities.Add(new OpportunityRecord(
                        id,
                        ParseEnum(record.Purpose, CampusNpcOpportunityPurpose.FreeMovement),
                        ParseEnum(record.Role, CampusCharacterRole.Student),
                        ParseTeacherDuties(record.TeacherDuties),
                        ParseStaffDuties(record.StaffDuties),
                        ParseTraits(record.Traits),
                        ParseEnum(record.ScheduleWindow, CampusNpcEcologyScheduleWindow.Always),
                        targetKind,
                        ParseEnum(record.IntentKind, CampusNpcIntentKind.Roam),
                        string.IsNullOrWhiteSpace(record.IntentLabel) ? id : record.IntentLabel.Trim(),
                        ParseEnum(record.RoomType, CampusRoomType.Unknown),
                        NormalizeId(record.FacilityGroupId),
                        Mathf.Max(0.02f, record.StopDistance <= 0f ? 0.18f : record.StopDistance),
                        Mathf.Max(0f, record.RoomTargetRadius),
                        Mathf.Max(0f, record.Score),
                        Mathf.Max(0f, record.HoldSeconds),
                        ParseEnum(record.ActionMode, CampusNpcEcologyActionMode.NoOp),
                        NormalizeId(record.ActionId),
                        record.Payload ?? string.Empty));
                }
            }

            return data;
        }

        private static bool MatchesRolePlan(RolePlanRecord record, CampusNpcAiRuntime npc)
        {
            return record != null &&
                   npc != null &&
                   npc.Data != null &&
                   npc.Data.Role == record.Role &&
                   MatchesSchedule(record.ScheduleWindow, npc.Segment) &&
                   MatchesTeacherDuties(record.TeacherDuties, npc.Data.TeacherDuty) &&
                   MatchesStaffDuties(record.StaffDuties, npc.Data.StaffDuty) &&
                   MatchesTraits(record.Traits, npc.Data);
        }

        private static bool MatchesOpportunity(
            OpportunityRecord record,
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query)
        {
            return record != null &&
                   npc.Data != null &&
                   record.Purpose == query.Purpose &&
                   npc.Data.Role == record.Role &&
                   MatchesSchedule(record.ScheduleWindow, npc.Segment) &&
                   MatchesTeacherDuties(record.TeacherDuties, npc.Data.TeacherDuty) &&
                   MatchesStaffDuties(record.StaffDuties, npc.Data.StaffDuty) &&
                   MatchesTraits(record.Traits, npc.Data);
        }

        private static bool TryBuildIntentFromPlan(
            CampusNpcAiRuntime npc,
            RolePlanRecord record,
            out CampusNpcIntent intent)
        {
            intent = null;
            if (!TryResolveTarget(npc, record.TargetKind, record.RoomType, record.FacilityGroupId, record.RoomTargetRadius, out ResolvedTarget target))
            {
                return false;
            }

            CampusNpcActionOpportunity opportunity = record.HoldSeconds > 0f
                ? CampusNpcActionOpportunity.HoldAt(
                    record.Id,
                    CampusCharacterAction.NoOp(),
                    target.Position,
                    target.RoomId,
                    100f,
                    record.IntentKind,
                    record.IntentLabel,
                    record.HoldSeconds)
                : CampusNpcActionOpportunity.MoveTo(
                    record.Id,
                    CampusCharacterAction.NoOp(),
                    target.Position,
                    target.RoomId,
                    record.StopDistance,
                    100f,
                    record.IntentKind,
                    record.IntentLabel);
            intent = opportunity.ToIntent();
            return true;
        }

        private static bool TryBuildOpportunity(
            CampusNpcAiRuntime npc,
            OpportunityRecord record,
            out CampusNpcActionOpportunity opportunity)
        {
            opportunity = null;
            if (!TryResolveTarget(npc, record.TargetKind, record.RoomType, record.FacilityGroupId, record.RoomTargetRadius, out ResolvedTarget target))
            {
                return false;
            }

            if (!TryBuildAction(record, target, out CampusCharacterAction action))
            {
                return false;
            }

            Func<CampusCharacterRuntime, bool> canUse =
                CampusCanteenInteractionProvider.BuildCanUseRule(record.ActionId, target.TargetObject);

            opportunity = record.HoldSeconds > 0f
                ? CampusNpcActionOpportunity.HoldAt(
                    record.Id,
                    action,
                    target.Position,
                    target.RoomId,
                    record.Score,
                    record.IntentKind,
                    record.IntentLabel,
                    record.HoldSeconds,
                    canUse)
                : CampusNpcActionOpportunity.MoveTo(
                    record.Id,
                    action,
                    target.Position,
                    target.RoomId,
                    record.StopDistance,
                    record.Score,
                    record.IntentKind,
                    record.IntentLabel,
                    canUse);
            return true;
        }

        private static bool TryBuildAction(
            OpportunityRecord record,
            ResolvedTarget target,
            out CampusCharacterAction action)
        {
            switch (record.ActionMode)
            {
                case CampusNpcEcologyActionMode.NoOp:
                    action = CampusCharacterAction.NoOp();
                    return true;
                case CampusNpcEcologyActionMode.PressInteract:
                    if (target.TargetObject != null)
                    {
                        action = CampusCharacterAction.PressInteract(target.TargetObject);
                        return true;
                    }

                    break;
                case CampusNpcEcologyActionMode.PressInteractionAction:
                    if (target.TargetObject != null && !string.IsNullOrWhiteSpace(record.ActionId))
                    {
                        action = CampusCharacterAction.PressInteractionAction(
                            target.TargetObject,
                            record.ActionId,
                            record.Payload);
                        return true;
                    }

                    break;
                case CampusNpcEcologyActionMode.DomainAction:
                    if (!string.IsNullOrWhiteSpace(record.ActionId))
                    {
                        action = CampusCharacterAction.DomainAction(
                            record.ActionId,
                            target.TargetObject,
                            record.Payload);
                        return true;
                    }

                    break;
            }

            action = null;
            return false;
        }

        private static bool TryResolveTarget(
            CampusNpcAiRuntime npc,
            CampusNpcEcologyTargetKind targetKind,
            CampusRoomType roomType,
            string facilityGroupId,
            float roomTargetRadius,
            out ResolvedTarget target)
        {
            target = default;
            if (npc == null || npc.Profile == null)
            {
                return false;
            }

            CampusNpcPersonalProfile profile = npc.Profile;
            switch (targetKind)
            {
                case CampusNpcEcologyTargetKind.StudentDesk:
                    target.RoomId = profile.StudentClassroomId;
                    target.Position = profile.HasStudentDesk
                        ? profile.StudentDeskPosition
                        : npc.RoomTarget(CampusRoomType.Classroom, roomTargetRadius <= 0f ? 0.25f : roomTargetRadius);
                    return target.Position != Vector3.zero || !string.IsNullOrWhiteSpace(target.RoomId);
                case CampusNpcEcologyTargetKind.TeacherPodium:
                    target.RoomId = profile.TeacherClassroomId;
                    target.Position = profile.HasTeacherPodium
                        ? profile.TeacherPodiumPosition
                        : npc.RoomTarget(CampusRoomType.Classroom, roomTargetRadius <= 0f ? 0.20f : roomTargetRadius);
                    return target.Position != Vector3.zero || !string.IsNullOrWhiteSpace(target.RoomId);
                case CampusNpcEcologyTargetKind.OfficeDesk:
                    target.RoomId = profile.OfficeRoomId;
                    target.Position = profile.HasOfficeDesk
                        ? profile.OfficeDeskPosition
                        : npc.RoomTarget(CampusRoomType.Office, roomTargetRadius <= 0f ? 0.25f : roomTargetRadius);
                    return target.Position != Vector3.zero || !string.IsNullOrWhiteSpace(target.RoomId);
                case CampusNpcEcologyTargetKind.PrimaryWorkstation:
                    target.RoomId = profile.WorkRoomId;
                    target.Position = profile.HasPrimaryWorkstation
                        ? profile.PrimaryWorkstationPosition
                        : npc.RoomTarget(CampusRoomType.Office, roomTargetRadius <= 0f ? 0.25f : roomTargetRadius);
                    return target.Position != Vector3.zero || !string.IsNullOrWhiteSpace(target.RoomId);
                case CampusNpcEcologyTargetKind.Dorm:
                    target.RoomId = profile.DormRoomId;
                    target.Position = profile.DormPosition != Vector3.zero
                        ? profile.DormPosition
                        : npc.RoomTarget(CampusRoomType.Dormitory, roomTargetRadius <= 0f ? 0.35f : roomTargetRadius);
                    return target.Position != Vector3.zero || !string.IsNullOrWhiteSpace(target.RoomId);
                case CampusNpcEcologyTargetKind.Common:
                    target.RoomId = profile.CommonRoomId;
                    target.Position = profile.CommonPosition != Vector3.zero
                        ? profile.CommonPosition
                        : npc.RoomTarget(CampusRoomType.Corridor, roomTargetRadius <= 0f ? 0.45f : roomTargetRadius);
                    return target.Position != Vector3.zero || !string.IsNullOrWhiteSpace(target.RoomId);
                case CampusNpcEcologyTargetKind.RoomType:
                    target.Position = npc.RoomTarget(roomType, roomTargetRadius <= 0f ? 0.30f : roomTargetRadius);
                    return target.Position != Vector3.zero;
                case CampusNpcEcologyTargetKind.RoomFacility:
                    return TryResolveRoomFacilityTarget(
                        npc,
                        roomType,
                        facilityGroupId,
                        roomTargetRadius <= 0f ? 0.20f : roomTargetRadius,
                        out target);
                default:
                    return false;
            }
        }

        private static bool TryResolveRoomFacilityTarget(
            CampusNpcAiRuntime npc,
            CampusRoomType roomType,
            string facilityGroupId,
            float fallbackRadius,
            out ResolvedTarget target)
        {
            target = default;
            if (npc == null || npc.WorldService == null)
            {
                return false;
            }

            CampusFacilityType[] facilityTypes = GetFacilityGroup(facilityGroupId);
            if (facilityTypes.Length == 0)
            {
                return false;
            }

            List<CampusGameplayRoom> rooms = CampusNpcRoomSelector.GetRooms(npc.WorldService, roomType);
            CampusGameplayRoom room = CampusNpcRoomSelector.Choose(
                rooms,
                (npc.Data != null ? npc.Data.Id : string.Empty) + ":" + facilityGroupId,
                npc.PersonalSeed);
            if (room == null)
            {
                return false;
            }

            CampusGameplayRoom.FacilityRecord record;
            if (CampusNpcFacilitySelector.TryChoose(
                    room,
                    facilityTypes,
                    CampusNpcStableIds.PositiveModulo(npc.PersonalSeed, 97),
                    out record))
            {
                target.RoomId = room.RoomId;
                target.Position = CampusNpcFacilitySelector.PositionOf(record);
                target.TargetObject = record.PlacedObject;
                return true;
            }

            target.RoomId = room.RoomId;
            target.Position = CampusNpcRoomSelector.PointNearCenter(room, npc.PersonalSeed, fallbackRadius);
            return true;
        }

        private static bool MatchesSchedule(CampusNpcEcologyScheduleWindow window, CampusTimeSegment segment)
        {
            switch (window)
            {
                case CampusNpcEcologyScheduleWindow.Always:
                    return true;
                case CampusNpcEcologyScheduleWindow.ClassSession:
                    return CampusNpcScheduleFacts.IsClassSession(segment);
                case CampusNpcEcologyScheduleWindow.Break:
                    return CampusNpcScheduleFacts.IsBreak(segment);
                case CampusNpcEcologyScheduleWindow.MealPeak:
                    return CampusNpcScheduleFacts.IsMealPeak(segment);
                case CampusNpcEcologyScheduleWindow.StudentFreeMovement:
                    return CampusNpcScheduleFacts.IsStudentFreeMovementWindow(segment);
                case CampusNpcEcologyScheduleWindow.DormWindow:
                    return CampusNpcScheduleFacts.IsDormWindow(segment);
                case CampusNpcEcologyScheduleWindow.TeacherOfficeWindow:
                    return CampusNpcScheduleFacts.IsTeacherOfficeWindow(segment);
                case CampusNpcEcologyScheduleWindow.StaffOffDuty:
                    return CampusNpcScheduleFacts.IsStaffOffDuty(segment);
                default:
                    return false;
            }
        }

        private static bool MatchesTeacherDuties(CampusTeacherDuty[] expected, CampusTeacherDuty actual)
        {
            if (expected == null || expected.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                CampusTeacherDuty duty = expected[i];
                if (duty == CampusTeacherDuty.None ? actual == CampusTeacherDuty.None : (actual & duty) == duty)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesStaffDuties(CampusStaffDuty[] expected, CampusStaffDuty actual)
        {
            if (expected == null || expected.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                CampusStaffDuty duty = expected[i];
                if (duty == CampusStaffDuty.None ? actual == CampusStaffDuty.None : (actual & duty) == duty)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesTraits(CampusCharacterTrait[] expected, CampusCharacterData data)
        {
            if (expected == null || expected.Length == 0 || data == null)
            {
                return true;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (data.HasTrait(expected[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static CampusFacilityType[] ParseFacilityTypes(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<CampusFacilityType>();
            }

            List<CampusFacilityType> result = new List<CampusFacilityType>();
            for (int i = 0; i < values.Length; i++)
            {
                CampusFacilityType type = ParseEnum(values[i], CampusFacilityType.Unknown);
                if (type != CampusFacilityType.Unknown && !result.Contains(type))
                {
                    result.Add(type);
                }
            }

            return result.ToArray();
        }

        private static CampusTeacherDuty[] ParseTeacherDuties(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<CampusTeacherDuty>();
            }

            List<CampusTeacherDuty> result = new List<CampusTeacherDuty>();
            for (int i = 0; i < values.Length; i++)
            {
                CampusTeacherDuty duty = ParseEnum(values[i], CampusTeacherDuty.None);
                if (!result.Contains(duty))
                {
                    result.Add(duty);
                }
            }

            return result.ToArray();
        }

        private static CampusStaffDuty[] ParseStaffDuties(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<CampusStaffDuty>();
            }

            List<CampusStaffDuty> result = new List<CampusStaffDuty>();
            for (int i = 0; i < values.Length; i++)
            {
                CampusStaffDuty duty = ParseEnum(values[i], CampusStaffDuty.None);
                if (!result.Contains(duty))
                {
                    result.Add(duty);
                }
            }

            return result.ToArray();
        }

        private static CampusCharacterTrait[] ParseTraits(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<CampusCharacterTrait>();
            }

            List<CampusCharacterTrait> result = new List<CampusCharacterTrait>();
            for (int i = 0; i < values.Length; i++)
            {
                CampusCharacterTrait trait = ParseEnum(values[i], CampusCharacterTrait.Ordinary);
                if (!result.Contains(trait))
                {
                    result.Add(trait);
                }
            }

            return result.ToArray();
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Enum.TryParse(value.Trim(), true, out TEnum parsed)
                ? parsed
                : fallback;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(' ', '_').ToLowerInvariant();
        }

        private struct ResolvedTarget
        {
            public string RoomId;
            public Vector3 Position;
            public UnityEngine.Object TargetObject;
        }

        private sealed class EcologyPresetData
        {
            public bool EnableSelectionDebug;
            public readonly List<FacilityGroupRecord> FacilityGroups = new List<FacilityGroupRecord>();
            public readonly List<RolePlanRecord> RolePlans = new List<RolePlanRecord>();
            public readonly List<OpportunityRecord> Opportunities = new List<OpportunityRecord>();

            public bool IsEmpty =>
                FacilityGroups.Count == 0 &&
                RolePlans.Count == 0 &&
                Opportunities.Count == 0;
        }

        private sealed class FacilityGroupRecord
        {
            public FacilityGroupRecord(string id, CampusFacilityType[] facilityTypes)
            {
                Id = id;
                FacilityTypes = facilityTypes ?? Array.Empty<CampusFacilityType>();
            }

            public string Id { get; }
            public CampusFacilityType[] FacilityTypes { get; }

            public static FacilityGroupRecord Create(string id, params CampusFacilityType[] facilityTypes)
            {
                return new FacilityGroupRecord(id, facilityTypes);
            }
        }

        private sealed class RolePlanRecord
        {
            public RolePlanRecord(
                string id,
                CampusCharacterRole role,
                CampusNpcEcologyScheduleWindow scheduleWindow,
                CampusTeacherDuty[] teacherDuties,
                CampusStaffDuty[] staffDuties,
                CampusCharacterTrait[] traits,
                CampusNpcEcologyTargetKind targetKind,
                CampusNpcIntentKind intentKind,
                string intentLabel,
                CampusRoomType roomType,
                string facilityGroupId,
                float stopDistance,
                float roomTargetRadius,
                float holdSeconds)
            {
                Id = id;
                Role = role;
                ScheduleWindow = scheduleWindow;
                TeacherDuties = teacherDuties ?? Array.Empty<CampusTeacherDuty>();
                StaffDuties = staffDuties ?? Array.Empty<CampusStaffDuty>();
                Traits = traits ?? Array.Empty<CampusCharacterTrait>();
                TargetKind = targetKind;
                IntentKind = intentKind;
                IntentLabel = intentLabel ?? string.Empty;
                RoomType = roomType;
                FacilityGroupId = facilityGroupId ?? string.Empty;
                StopDistance = stopDistance;
                RoomTargetRadius = roomTargetRadius;
                HoldSeconds = holdSeconds;
            }

            public string Id { get; }
            public CampusCharacterRole Role { get; }
            public CampusNpcEcologyScheduleWindow ScheduleWindow { get; }
            public CampusTeacherDuty[] TeacherDuties { get; }
            public CampusStaffDuty[] StaffDuties { get; }
            public CampusCharacterTrait[] Traits { get; }
            public CampusNpcEcologyTargetKind TargetKind { get; }
            public CampusNpcIntentKind IntentKind { get; }
            public string IntentLabel { get; }
            public CampusRoomType RoomType { get; }
            public string FacilityGroupId { get; }
            public float StopDistance { get; }
            public float RoomTargetRadius { get; }
            public float HoldSeconds { get; }

            public static RolePlanRecord Create(
                string id,
                CampusCharacterRole role,
                CampusNpcEcologyScheduleWindow scheduleWindow,
                CampusNpcEcologyTargetKind targetKind,
                CampusNpcIntentKind intentKind,
                string intentLabel,
                float stopDistance,
                float roomTargetRadius)
            {
                return new RolePlanRecord(
                    id,
                    role,
                    scheduleWindow,
                    Array.Empty<CampusTeacherDuty>(),
                    Array.Empty<CampusStaffDuty>(),
                    Array.Empty<CampusCharacterTrait>(),
                    targetKind,
                    intentKind,
                    intentLabel,
                    CampusRoomType.Unknown,
                    string.Empty,
                    stopDistance,
                    roomTargetRadius,
                    0f);
            }
        }

        private sealed class OpportunityRecord
        {
            public OpportunityRecord(
                string id,
                CampusNpcOpportunityPurpose purpose,
                CampusCharacterRole role,
                CampusTeacherDuty[] teacherDuties,
                CampusStaffDuty[] staffDuties,
                CampusCharacterTrait[] traits,
                CampusNpcEcologyScheduleWindow scheduleWindow,
                CampusNpcEcologyTargetKind targetKind,
                CampusNpcIntentKind intentKind,
                string intentLabel,
                CampusRoomType roomType,
                string facilityGroupId,
                float stopDistance,
                float roomTargetRadius,
                float score,
                float holdSeconds,
                CampusNpcEcologyActionMode actionMode,
                string actionId,
                string payload)
            {
                Id = id;
                Purpose = purpose;
                Role = role;
                TeacherDuties = teacherDuties ?? Array.Empty<CampusTeacherDuty>();
                StaffDuties = staffDuties ?? Array.Empty<CampusStaffDuty>();
                Traits = traits ?? Array.Empty<CampusCharacterTrait>();
                ScheduleWindow = scheduleWindow;
                TargetKind = targetKind;
                IntentKind = intentKind;
                IntentLabel = intentLabel ?? string.Empty;
                RoomType = roomType;
                FacilityGroupId = facilityGroupId ?? string.Empty;
                StopDistance = stopDistance;
                RoomTargetRadius = roomTargetRadius;
                Score = score;
                HoldSeconds = holdSeconds;
                ActionMode = actionMode;
                ActionId = actionId ?? string.Empty;
                Payload = payload ?? string.Empty;
            }

            public string Id { get; }
            public CampusNpcOpportunityPurpose Purpose { get; }
            public CampusCharacterRole Role { get; }
            public CampusTeacherDuty[] TeacherDuties { get; }
            public CampusStaffDuty[] StaffDuties { get; }
            public CampusCharacterTrait[] Traits { get; }
            public CampusNpcEcologyScheduleWindow ScheduleWindow { get; }
            public CampusNpcEcologyTargetKind TargetKind { get; }
            public CampusNpcIntentKind IntentKind { get; }
            public string IntentLabel { get; }
            public CampusRoomType RoomType { get; }
            public string FacilityGroupId { get; }
            public float StopDistance { get; }
            public float RoomTargetRadius { get; }
            public float Score { get; }
            public float HoldSeconds { get; }
            public CampusNpcEcologyActionMode ActionMode { get; }
            public string ActionId { get; }
            public string Payload { get; }
        }

        [Serializable]
        private sealed class EcologyPresetFile
        {
            public bool EnableSelectionDebug = false;
            public List<FacilityGroupFileRecord> FacilityGroups = new List<FacilityGroupFileRecord>();
            public List<RolePlanFileRecord> RolePlans = new List<RolePlanFileRecord>();
            public List<OpportunityFileRecord> Opportunities = new List<OpportunityFileRecord>();
        }

        [Serializable]
        private sealed class FacilityGroupFileRecord
        {
            public string Id = string.Empty;
            public string[] FacilityTypes = Array.Empty<string>();
        }

        [Serializable]
        private sealed class RolePlanFileRecord
        {
            public string Id = string.Empty;
            public string Role = string.Empty;
            public string[] TeacherDuties = Array.Empty<string>();
            public string[] StaffDuties = Array.Empty<string>();
            public string[] Traits = Array.Empty<string>();
            public string ScheduleWindow = string.Empty;
            public string TargetKind = string.Empty;
            public string IntentKind = string.Empty;
            public string IntentLabel = string.Empty;
            public string RoomType = string.Empty;
            public string FacilityGroupId = string.Empty;
            public float StopDistance = 0.18f;
            public float RoomTargetRadius = 0.30f;
            public float HoldSeconds = 0f;
        }

        [Serializable]
        private sealed class OpportunityFileRecord
        {
            public string Id = string.Empty;
            public string Purpose = string.Empty;
            public string Role = string.Empty;
            public string[] TeacherDuties = Array.Empty<string>();
            public string[] StaffDuties = Array.Empty<string>();
            public string[] Traits = Array.Empty<string>();
            public string ScheduleWindow = string.Empty;
            public string TargetKind = string.Empty;
            public string IntentKind = string.Empty;
            public string IntentLabel = string.Empty;
            public string RoomType = string.Empty;
            public string FacilityGroupId = string.Empty;
            public float StopDistance = 0.18f;
            public float RoomTargetRadius = 0.20f;
            public float Score = 10f;
            public float HoldSeconds = 0f;
            public string ActionMode = string.Empty;
            public string ActionId = string.Empty;
            public string Payload = string.Empty;
        }
    }
}
