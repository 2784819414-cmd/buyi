using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Sanctions;
using UnityEngine;

namespace NtingCampus.Gameplay.Events
{
    public readonly struct CampusPrankAttemptedEvent
    {
        public CampusPrankAttemptedEvent(
            CampusPrankType prankType,
            string actorId,
            string targetId,
            string roomId,
            bool duringClassSession)
        {
            PrankType = prankType;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            DuringClassSession = duringClassSession;
        }

        public CampusPrankType PrankType { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string RoomId { get; }
        public bool DuringClassSession { get; }
    }

    public readonly struct CampusPrankResolvedEvent
    {
        public CampusPrankResolvedEvent(
            CampusPrankType prankType,
            string actorId,
            string targetId,
            string roomId,
            bool succeeded,
            bool detectedByTeacher,
            int divinePowerReward)
        {
            PrankType = prankType;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            Succeeded = succeeded;
            DetectedByTeacher = detectedByTeacher;
            DivinePowerReward = divinePowerReward;
        }

        public CampusPrankType PrankType { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string RoomId { get; }
        public bool Succeeded { get; }
        public bool DetectedByTeacher { get; }
        public int DivinePowerReward { get; }
    }

    public readonly struct CampusSanctionIssuedEvent
    {
        public CampusSanctionIssuedEvent(
            string actorId,
            string roomId,
            CampusSanctionLevel sanctionLevel,
            int warningCount)
        {
            ActorId = actorId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            SanctionLevel = sanctionLevel;
            WarningCount = warningCount;
        }

        public string ActorId { get; }
        public string RoomId { get; }
        public CampusSanctionLevel SanctionLevel { get; }
        public int WarningCount { get; }
    }

    public readonly struct CampusStudentDozedOffEvent
    {
        public CampusStudentDozedOffEvent(string studentId, string roomId, int sleepiness)
        {
            StudentId = studentId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            Sleepiness = sleepiness;
        }

        public string StudentId { get; }
        public string RoomId { get; }
        public int Sleepiness { get; }
    }

    public readonly struct CampusTeacherDistractedEvent
    {
        public CampusTeacherDistractedEvent(
            string teacherId,
            string sourceStudentId,
            string roomId,
            float durationSeconds)
        {
            TeacherId = teacherId ?? string.Empty;
            SourceStudentId = sourceStudentId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            DurationSeconds = durationSeconds;
        }

        public string TeacherId { get; }
        public string SourceStudentId { get; }
        public string RoomId { get; }
        public float DurationSeconds { get; }
    }

    public readonly struct CampusActorSkipClassEvent
    {
        public CampusActorSkipClassEvent(
            string actorId,
            string fromRoomId,
            string toRoomId,
            string teacherId,
            bool detectedByTeacher,
            bool usedDistraction,
            string reason)
        {
            ActorId = actorId ?? string.Empty;
            FromRoomId = fromRoomId ?? string.Empty;
            ToRoomId = toRoomId ?? string.Empty;
            TeacherId = teacherId ?? string.Empty;
            DetectedByTeacher = detectedByTeacher;
            UsedDistraction = usedDistraction;
            Reason = reason ?? string.Empty;
        }

        public string ActorId { get; }
        public string FromRoomId { get; }
        public string ToRoomId { get; }
        public string TeacherId { get; }
        public bool DetectedByTeacher { get; }
        public bool UsedDistraction { get; }
        public string Reason { get; }
    }

    public readonly struct CampusItemTransferredEvent
    {
        public CampusItemTransferredEvent(
            string actorId,
            string itemInstanceId,
            string itemDefinitionId,
            string itemDisplayName,
            string sourceContainerId,
            string targetContainerId,
            string roomId,
            StorageTransferReason reason,
            bool illegal,
            bool observed)
        {
            ActorId = actorId ?? string.Empty;
            ItemInstanceId = itemInstanceId ?? string.Empty;
            ItemDefinitionId = itemDefinitionId ?? string.Empty;
            ItemDisplayName = itemDisplayName ?? string.Empty;
            SourceContainerId = sourceContainerId ?? string.Empty;
            TargetContainerId = targetContainerId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            Reason = reason;
            Illegal = illegal;
            Observed = observed;
        }

        public string ActorId { get; }
        public string ItemInstanceId { get; }
        public string ItemDefinitionId { get; }
        public string ItemDisplayName { get; }
        public string SourceContainerId { get; }
        public string TargetContainerId { get; }
        public string RoomId { get; }
        public StorageTransferReason Reason { get; }
        public bool Illegal { get; }
        public bool Observed { get; }
    }

    public readonly struct CampusItemTheftObservedEvent
    {
        public CampusItemTheftObservedEvent(
            string actorId,
            string witnessId,
            string ownerId,
            string itemInstanceId,
            string itemDefinitionId,
            string itemDisplayName,
            string sourceContainerId,
            string targetContainerId,
            string roomId,
            int suspicionAmount,
            bool shouldIssueSanction)
        {
            ActorId = actorId ?? string.Empty;
            WitnessId = witnessId ?? string.Empty;
            OwnerId = ownerId ?? string.Empty;
            ItemInstanceId = itemInstanceId ?? string.Empty;
            ItemDefinitionId = itemDefinitionId ?? string.Empty;
            ItemDisplayName = itemDisplayName ?? string.Empty;
            SourceContainerId = sourceContainerId ?? string.Empty;
            TargetContainerId = targetContainerId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            SuspicionAmount = suspicionAmount;
            ShouldIssueSanction = shouldIssueSanction;
        }

        public string ActorId { get; }
        public string WitnessId { get; }
        public string OwnerId { get; }
        public string ItemInstanceId { get; }
        public string ItemDefinitionId { get; }
        public string ItemDisplayName { get; }
        public string SourceContainerId { get; }
        public string TargetContainerId { get; }
        public string RoomId { get; }
        public int SuspicionAmount { get; }
        public bool ShouldIssueSanction { get; }
    }

    public readonly struct CampusInventoryQuestionedEvent
    {
        public CampusInventoryQuestionedEvent(
            string actorId,
            string inspectorId,
            string roomId,
            int inspectionPressure,
            bool foundContraband)
        {
            ActorId = actorId ?? string.Empty;
            InspectorId = inspectorId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            InspectionPressure = inspectionPressure;
            FoundContraband = foundContraband;
        }

        public string ActorId { get; }
        public string InspectorId { get; }
        public string RoomId { get; }
        public int InspectionPressure { get; }
        public bool FoundContraband { get; }
    }

    public readonly struct CampusContrabandFoundEvent
    {
        public CampusContrabandFoundEvent(
            string actorId,
            string inspectorId,
            string roomId,
            string itemInstanceId,
            string itemDefinitionId,
            string itemDisplayName,
            string containerId,
            int inspectionPressure,
            bool shouldIssueSanction)
        {
            ActorId = actorId ?? string.Empty;
            InspectorId = inspectorId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ItemInstanceId = itemInstanceId ?? string.Empty;
            ItemDefinitionId = itemDefinitionId ?? string.Empty;
            ItemDisplayName = itemDisplayName ?? string.Empty;
            ContainerId = containerId ?? string.Empty;
            InspectionPressure = inspectionPressure;
            ShouldIssueSanction = shouldIssueSanction;
        }

        public string ActorId { get; }
        public string InspectorId { get; }
        public string RoomId { get; }
        public string ItemInstanceId { get; }
        public string ItemDefinitionId { get; }
        public string ItemDisplayName { get; }
        public string ContainerId { get; }
        public int InspectionPressure { get; }
        public bool ShouldIssueSanction { get; }
    }

    [DisallowMultipleComponent]
    public sealed class CampusGameplayEventHub : MonoBehaviour
    {
        private const int MaxRecentEvents = 20;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private List<string> recentEventIds = new List<string>(MaxRecentEvents);

        public IReadOnlyList<string> RecentEventIds => recentEventIds;
        public event System.Action<CampusPrankAttemptedEvent> PrankAttempted;
        public event System.Action<CampusPrankResolvedEvent> PrankResolved;
        public event System.Action<CampusSanctionIssuedEvent> SanctionIssued;
        public event System.Action<CampusStudentDozedOffEvent> StudentDozedOff;
        public event System.Action<CampusTeacherDistractedEvent> TeacherDistracted;
        public event System.Action<CampusActorSkipClassEvent> ActorSkipClass;
        public event System.Action<CampusItemTransferredEvent> ItemTransferred;
        public event System.Action<CampusItemTheftObservedEvent> ItemTheftObserved;
        public event System.Action<CampusInventoryQuestionedEvent> InventoryQuestioned;
        public event System.Action<CampusContrabandFoundEvent> ContrabandFound;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
        }

        public void Record(string eventId)
        {
            string normalizedId = string.IsNullOrWhiteSpace(eventId) ? "gameplay.unknown" : eventId.Trim();
            recentEventIds.Add(normalizedId);
            if (recentEventIds.Count > MaxRecentEvents)
            {
                recentEventIds.RemoveRange(0, recentEventIds.Count - MaxRecentEvents);
            }

            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog("[Event] " + normalizedId);
            }
        }

        public void PublishPrankAttempted(CampusPrankAttemptedEvent eventData)
        {
            Record("prank.attempted." + Normalize(eventData.PrankType));
            PrankAttempted?.Invoke(eventData);
        }

        public void PublishPrankResolved(CampusPrankResolvedEvent eventData)
        {
            string suffix = eventData.Succeeded ? "success" : "failure";
            Record("prank.resolved." + Normalize(eventData.PrankType) + "." + suffix);
            PrankResolved?.Invoke(eventData);
        }

        public void PublishSanctionIssued(CampusSanctionIssuedEvent eventData)
        {
            Record("sanction.issued." + Normalize(eventData.SanctionLevel));
            SanctionIssued?.Invoke(eventData);
        }

        public void PublishStudentDozedOff(CampusStudentDozedOffEvent eventData)
        {
            Record("classroom.student_dozed_off");
            StudentDozedOff?.Invoke(eventData);
        }

        public void PublishTeacherDistracted(CampusTeacherDistractedEvent eventData)
        {
            Record("classroom.teacher_distracted");
            TeacherDistracted?.Invoke(eventData);
        }

        public void PublishActorSkipClass(CampusActorSkipClassEvent eventData)
        {
            string suffix = eventData.DetectedByTeacher ? "detected" : "escaped";
            Record("classroom.skip_class." + suffix);
            ActorSkipClass?.Invoke(eventData);
        }

        public void PublishItemTransferred(CampusItemTransferredEvent eventData)
        {
            if (eventData.Illegal)
            {
                string suffix = eventData.Observed ? "observed" : "quiet";
                Record("item.transfer." + Normalize(eventData.Reason) + "." + suffix);
            }

            ItemTransferred?.Invoke(eventData);
        }

        public void PublishItemTheftObserved(CampusItemTheftObservedEvent eventData)
        {
            Record("item.theft.observed");
            ItemTheftObserved?.Invoke(eventData);
        }

        public void PublishInventoryQuestioned(CampusInventoryQuestionedEvent eventData)
        {
            Record(eventData.FoundContraband ? "inventory.questioned.hit" : "inventory.questioned.clear");
            InventoryQuestioned?.Invoke(eventData);
        }

        public void PublishContrabandFound(CampusContrabandFoundEvent eventData)
        {
            Record("inventory.contraband.found");
            ContrabandFound?.Invoke(eventData);
        }

        private static string Normalize(CampusPrankType prankType)
        {
            return prankType.ToString().ToLowerInvariant();
        }

        private static string Normalize(CampusSanctionLevel sanctionLevel)
        {
            return sanctionLevel.ToString().ToLowerInvariant();
        }

        private static string Normalize(StorageTransferReason reason)
        {
            return reason.ToString().ToLowerInvariant();
        }
    }
}
