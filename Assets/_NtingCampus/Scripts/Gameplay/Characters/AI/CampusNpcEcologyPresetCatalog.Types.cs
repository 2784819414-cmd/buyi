using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
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
        RoomFacility = 8,
        ServiceStation = 9,
        DroppedStorageItem = 10
    }

    internal enum CampusNpcEcologyActionMode
    {
        NoOp = 0,
        PressInteract = 1,
        DomainAction = 2,
        PressInteractionAction = 3
    }

    internal enum CampusNpcEcologyActionRepeatPolicy
    {
        Always = 0,
        OnceUntilScheduleChange = 1
    }

    internal static partial class CampusNpcEcologyPresetCatalog
    {
        [Serializable]
        private sealed class EcologyPresetFile
        {
            public bool EnableSelectionDebug = false;
            public List<ActionCatalogFileRecord> ActionCatalog = new List<ActionCatalogFileRecord>();
            public List<ActionTargetRuleFileRecord> ActionTargetRules = new List<ActionTargetRuleFileRecord>();
            public List<ActionChainFileRecord> ActionChains = new List<ActionChainFileRecord>();
            public List<NpcDecisionProfileFileRecord> NpcDecisionProfiles = new List<NpcDecisionProfileFileRecord>();
        }

        [Serializable]
        private sealed class ActionCatalogFileRecord
        {
            public string ActionId = string.Empty;
            public string ActionMode = string.Empty;
            public string Payload = string.Empty;
            public string RepeatPolicy = string.Empty;
        }

        [Serializable]
        private sealed class ActionTargetRuleFileRecord
        {
            public string Id = string.Empty;
            public string ActionId = string.Empty;
            public string TargetKind = string.Empty;
            public string RoomType = string.Empty;
            public string[] FacilityTypes = Array.Empty<string>();
            public string Owner = string.Empty;
            public string SourceLocation = string.Empty;
            public string SourceContainerPrefix = string.Empty;
            public string DefinitionId = string.Empty;
            public float StopDistance = 0.18f;
            public string[] Requirements = Array.Empty<string>();
        }

        [Serializable]
        private sealed class ActionChainFileRecord
        {
            public string Id = string.Empty;
            public string[] ActionIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class NpcDecisionProfileFileRecord
        {
            public string Id = string.Empty;
            public string Role = string.Empty;
            public string[] CharacterIds = Array.Empty<string>();
            public string[] TeacherDuties = Array.Empty<string>();
            public string[] StaffDuties = Array.Empty<string>();
            public string[] Traits = Array.Empty<string>();
            public List<ScheduleEntryFileRecord> Entries = new List<ScheduleEntryFileRecord>();
        }

        [Serializable]
        private sealed class ScheduleEntryFileRecord
        {
            public string Id = string.Empty;
            public string[] Segments = Array.Empty<string>();
            public string[] ScheduleWindows = Array.Empty<string>();
            public string StartClock = string.Empty;
            public string EndClock = string.Empty;
            public string IntentKind = string.Empty;
            public string IntentLabel = string.Empty;
            public string ActionChainId = string.Empty;
            public float Score = 0f;
        }

        private sealed class EcologyPresetData
        {
            public bool EnableSelectionDebug;
            public readonly Dictionary<string, ActionRecord> ActionCatalog =
                new Dictionary<string, ActionRecord>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, ActionTargetRuleRecord> ActionTargetRules =
                new Dictionary<string, ActionTargetRuleRecord>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<ActionTargetRuleRecord>> ActionTargetRulesByActionId =
                new Dictionary<string, List<ActionTargetRuleRecord>>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, ActionChainRecord> ActionChains =
                new Dictionary<string, ActionChainRecord>(StringComparer.OrdinalIgnoreCase);
            public readonly List<NpcDecisionProfileRecord> NpcDecisionProfiles = new List<NpcDecisionProfileRecord>();
        }

        private sealed class ActionRecord
        {
            public string ActionId = string.Empty;
            public CampusNpcEcologyActionMode ActionMode = CampusNpcEcologyActionMode.NoOp;
            public string Payload = string.Empty;
            public CampusNpcEcologyActionRepeatPolicy RepeatPolicy = CampusNpcEcologyActionRepeatPolicy.Always;
        }

        private sealed class ActionTargetRuleRecord
        {
            public string Id = string.Empty;
            public string ActionId = string.Empty;
            public CampusNpcEcologyTargetKind TargetKind = CampusNpcEcologyTargetKind.None;
            public CampusRoomType RoomType = CampusRoomType.Unknown;
            public CampusFacilityType[] FacilityTypes = Array.Empty<CampusFacilityType>();
            public string Owner = string.Empty;
            public string SourceLocation = string.Empty;
            public string SourceContainerPrefix = string.Empty;
            public string DefinitionId = string.Empty;
            public float StopDistance = 0.18f;
            public string[] RequirementIds = Array.Empty<string>();
        }

        private sealed class ActionChainRecord
        {
            public string Id = string.Empty;
            public string[] ActionIds = Array.Empty<string>();
        }

        private sealed class NpcDecisionProfileRecord
        {
            public string Id = string.Empty;
            public CampusCharacterRole Role = CampusCharacterRole.Student;
            public string[] CharacterIds = Array.Empty<string>();
            public CampusTeacherDuty TeacherDutyMask = CampusTeacherDuty.None;
            public CampusStaffDuty StaffDutyMask = CampusStaffDuty.None;
            public CampusCharacterTrait[] Traits = Array.Empty<CampusCharacterTrait>();
            public List<ScheduleEntryRecord> Entries = new List<ScheduleEntryRecord>();
        }

        private sealed class ScheduleEntryRecord
        {
            public string Id = string.Empty;
            public CampusTimeSegment[] Segments = Array.Empty<CampusTimeSegment>();
            public CampusNpcEcologyScheduleWindow[] ScheduleWindows = Array.Empty<CampusNpcEcologyScheduleWindow>();
            public bool HasClockRange;
            public int StartMinute;
            public int EndMinute;
            public CampusNpcIntentKind IntentKind = CampusNpcIntentKind.Roam;
            public string IntentLabel = string.Empty;
            public string ActionChainId = string.Empty;
            public float Score;
        }

        private struct ResolvedTarget
        {
            public bool IsValid;
            public UnityEngine.Object TargetObject;
            public Vector3 Position;
            public string RoomId;
            public bool RequireExactNavigation;
            public string TargetId;
        }

        private readonly struct SelectedEntry
        {
            public SelectedEntry(
                NpcDecisionProfileRecord profile,
                ScheduleEntryRecord entry,
                ActionChainRecord actionChain,
                ActionRecord action,
                int stepIndex,
                CampusNpcActionOpportunity opportunity,
                float score)
            {
                Profile = profile;
                Entry = entry;
                ActionChain = actionChain;
                Action = action;
                StepIndex = stepIndex;
                Opportunity = opportunity;
                Score = score;
            }

            public NpcDecisionProfileRecord Profile { get; }
            public ScheduleEntryRecord Entry { get; }
            public ActionChainRecord ActionChain { get; }
            public ActionRecord Action { get; }
            public int StepIndex { get; }
            public CampusNpcActionOpportunity Opportunity { get; }
            public float Score { get; }
        }
    }
}
