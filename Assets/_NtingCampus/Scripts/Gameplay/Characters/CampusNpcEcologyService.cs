using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Schedule;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public readonly struct CampusNpcEventFact
    {
        public CampusNpcEventFact(
            int serial,
            string factId,
            string actorId,
            string targetId,
            string roomId,
            int gossipHeat,
            string summary)
        {
            Serial = serial;
            FactId = factId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            GossipHeat = gossipHeat;
            Summary = summary ?? string.Empty;
            RealtimeSeconds = Time.time;
        }

        public int Serial { get; }
        public string FactId { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string RoomId { get; }
        public int GossipHeat { get; }
        public string Summary { get; }
        public float RealtimeSeconds { get; }
    }

    [DisallowMultipleComponent]
    public sealed class CampusNpcEcologyService : MonoBehaviour
    {
        private const int MaxRecentFacts = 64;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField, Range(0, 100)] private int gossipHeat;
        [SerializeField, Min(0)] private int dailyEcologyEventCount;
        [SerializeField] private string currentSummary =
            CampusNpcEcologyTextCatalog.Get(CampusNpcEcologyTextId.WaitingForEvents);

        private readonly List<CampusNpcEventFact> recentFacts =
            new List<CampusNpcEventFact>(MaxRecentFacts);

        private bool subscribed;
        private int nextFactSerial;

        public int GossipHeat => gossipHeat;
        public int DailyEcologyEventCount => dailyEcologyEventCount;
        public string CurrentSummary => currentSummary;
        public int LatestFactSerial => nextFactSerial;
        public IReadOnlyList<CampusNpcEventFact> RecentFacts => recentFacts;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            if (subscribed)
            {
                ReleaseSubscriptions();
            }

            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            timeController = bootstrap != null ? bootstrap.TimeController : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;
            Subscribe();
        }

        public int CopyFactsAfter(int lastSeenSerial, List<CampusNpcEventFact> output)
        {
            if (output == null)
            {
                return nextFactSerial;
            }

            for (int i = 0; i < recentFacts.Count; i++)
            {
                CampusNpcEventFact fact = recentFacts[i];
                if (fact.Serial > lastSeenSerial)
                {
                    output.Add(fact);
                }
            }

            return nextFactSerial;
        }

        private void OnDestroy()
        {
            ReleaseSubscriptions();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (timeController != null)
            {
                timeController.SegmentChanged += HandleSegmentChanged;
                timeController.DailySettlementStarted += HandleDailySettlementStarted;
            }

            if (gameplayEventHub != null)
            {
                gameplayEventHub.PrankAttempted += HandlePrankAttempted;
                gameplayEventHub.PrankResolved += HandlePrankResolved;
                gameplayEventHub.SanctionIssued += HandleSanctionIssued;
                gameplayEventHub.StudentDozedOff += HandleStudentDozedOff;
                gameplayEventHub.TeacherDistracted += HandleTeacherDistracted;
                gameplayEventHub.ActorSkipClass += HandleActorSkipClass;
                gameplayEventHub.ItemTransferred += HandleItemTransferred;
                gameplayEventHub.ItemTheftObserved += HandleItemTheftObserved;
                gameplayEventHub.InventoryQuestioned += HandleInventoryQuestioned;
                gameplayEventHub.ContrabandFound += HandleContrabandFound;
            }

            subscribed = true;
        }

        private void ReleaseSubscriptions()
        {
            if (!subscribed)
            {
                return;
            }

            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
                timeController.DailySettlementStarted -= HandleDailySettlementStarted;
            }

            if (gameplayEventHub != null)
            {
                gameplayEventHub.PrankAttempted -= HandlePrankAttempted;
                gameplayEventHub.PrankResolved -= HandlePrankResolved;
                gameplayEventHub.SanctionIssued -= HandleSanctionIssued;
                gameplayEventHub.StudentDozedOff -= HandleStudentDozedOff;
                gameplayEventHub.TeacherDistracted -= HandleTeacherDistracted;
                gameplayEventHub.ActorSkipClass -= HandleActorSkipClass;
                gameplayEventHub.ItemTransferred -= HandleItemTransferred;
                gameplayEventHub.ItemTheftObserved -= HandleItemTheftObserved;
                gameplayEventHub.InventoryQuestioned -= HandleInventoryQuestioned;
                gameplayEventHub.ContrabandFound -= HandleContrabandFound;
            }

            subscribed = false;
        }

        private void HandleSegmentChanged(CampusTimeSegment previousSegment, CampusTimeSegment currentSegment)
        {
            currentSummary = CampusNpcEcologyTextCatalog.Format(
                CampusNpcEcologyTextId.SegmentPulse,
                CampusTimeSchedule.GetDisplayName(CampusLanguageState.CurrentLanguage, currentSegment));
            RecordFact(
                "time.segment_changed",
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                currentSummary,
                false,
                false);
        }

        private void HandleDailySettlementStarted(CampusGameDate _)
        {
            gossipHeat = Mathf.Max(0, gossipHeat - 25);
            dailyEcologyEventCount = 0;
            currentSummary = CampusNpcEcologyTextCatalog.Get(CampusNpcEcologyTextId.DailyRecovery);
            RecordFact(
                "time.daily_settlement",
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                currentSummary,
                false,
                false);
        }

        private void HandlePrankAttempted(CampusPrankAttemptedEvent eventData)
        {
            RecordFact(
                "prank.attempted." + eventData.PrankType,
                eventData.ActorId,
                eventData.TargetId,
                eventData.RoomId,
                3,
                CampusNpcEcologyTextId.PrankAttemptNoticed);
        }

        private void HandlePrankResolved(CampusPrankResolvedEvent eventData)
        {
            CampusNpcEcologyTextId summaryId = eventData.DetectedByTeacher
                ? CampusNpcEcologyTextId.DetectedPrankRisk
                : CampusNpcEcologyTextId.UndetectedPrankShift;
            int heat = eventData.DetectedByTeacher ? 12 : eventData.Succeeded ? 6 : 3;
            string suffix = eventData.DetectedByTeacher
                ? "detected"
                : eventData.Succeeded ? "success" : "failure";
            RecordFact(
                "prank.resolved." + eventData.PrankType + "." + suffix,
                eventData.ActorId,
                eventData.TargetId,
                eventData.RoomId,
                heat,
                summaryId);
        }

        private void HandleSanctionIssued(CampusSanctionIssuedEvent eventData)
        {
            RecordFact(
                "sanction.issued." + eventData.SanctionLevel,
                eventData.ActorId,
                string.Empty,
                eventData.RoomId,
                8,
                CampusNpcEcologyTextId.SanctionReacted);
        }

        private void HandleStudentDozedOff(CampusStudentDozedOffEvent eventData)
        {
            RecordFact(
                "classroom.student_dozed_off",
                eventData.StudentId,
                string.Empty,
                eventData.RoomId,
                1,
                CampusNpcEcologyTextId.ClassroomDozingReacted);
        }

        private void HandleTeacherDistracted(CampusTeacherDistractedEvent eventData)
        {
            RecordFact(
                "classroom.teacher_distracted",
                eventData.SourceStudentId,
                eventData.TeacherId,
                eventData.RoomId,
                0,
                CampusNpcEcologyTextId.TeacherDistractionChangedMood);
        }

        private void HandleActorSkipClass(CampusActorSkipClassEvent eventData)
        {
            CampusNpcEcologyTextId summaryId = eventData.DetectedByTeacher
                ? CampusNpcEcologyTextId.DetectedClassSkipping
                : CampusNpcEcologyTextId.EscapedClassSkipping;
            RecordFact(
                eventData.DetectedByTeacher ? "classroom.skip_class.detected" : "classroom.skip_class.escaped",
                eventData.ActorId,
                eventData.TeacherId,
                eventData.FromRoomId,
                eventData.DetectedByTeacher ? 12 : 7,
                summaryId);
        }

        private void HandleItemTransferred(CampusItemTransferredEvent eventData)
        {
            if (!eventData.Illegal)
            {
                return;
            }

            RecordFact(
                "item.transfer.illegal." + eventData.Reason,
                eventData.ActorId,
                string.Empty,
                eventData.RoomId,
                eventData.Observed ? 2 : 1,
                CampusNpcEcologyTextId.ProtectedItemMoveFact);
        }

        private void HandleItemTheftObserved(CampusItemTheftObservedEvent eventData)
        {
            RecordFact(
                "item.theft.observed",
                eventData.ActorId,
                eventData.WitnessId,
                eventData.RoomId,
                14,
                CampusNpcEcologyTextId.WitnessedTheftEscalated);
        }

        private void HandleInventoryQuestioned(CampusInventoryQuestionedEvent eventData)
        {
            CampusNpcEcologyTextId summaryId = eventData.FoundContraband
                ? CampusNpcEcologyTextId.ContrabandQuestioning
                : CampusNpcEcologyTextId.BagQuestioning;
            RecordFact(
                eventData.FoundContraband ? "inventory.questioned.hit" : "inventory.questioned.clear",
                eventData.ActorId,
                eventData.InspectorId,
                eventData.RoomId,
                eventData.FoundContraband ? 16 : 4,
                summaryId);
        }

        private void HandleContrabandFound(CampusContrabandFoundEvent eventData)
        {
            RecordFact(
                "inventory.contraband.found",
                eventData.ActorId,
                eventData.InspectorId,
                eventData.RoomId,
                18,
                CampusNpcEcologyTextId.ContrabandFoundEscalated);
        }

        private void RecordFact(
            string factId,
            string actorId,
            string targetId,
            string roomId,
            int heatDelta,
            CampusNpcEcologyTextId summaryId)
        {
            RecordFact(
                factId,
                actorId,
                targetId,
                roomId,
                heatDelta,
                CampusNpcEcologyTextCatalog.Get(summaryId),
                true,
                true);
        }

        private void RecordFact(
            string factId,
            string actorId,
            string targetId,
            string roomId,
            int heatDelta,
            string summary,
            bool countAsDailyEvent,
            bool writeToEventLog)
        {
            gossipHeat = Mathf.Clamp(gossipHeat + heatDelta, 0, 100);
            currentSummary = string.IsNullOrWhiteSpace(summary) ? factId : summary;
            nextFactSerial++;
            recentFacts.Add(new CampusNpcEventFact(
                nextFactSerial,
                factId,
                NormalizeId(actorId),
                NormalizeId(targetId),
                NormalizeId(roomId),
                gossipHeat,
                currentSummary));

            if (recentFacts.Count > MaxRecentFacts)
            {
                recentFacts.RemoveRange(0, recentFacts.Count - MaxRecentFacts);
            }

            if (countAsDailyEvent)
            {
                dailyEcologyEventCount++;
            }

            if (writeToEventLog && bootstrap != null && bootstrap.EventLog != null && dailyEcologyEventCount <= 8)
            {
                bootstrap.EventLog.AddLog(CampusNpcEcologyTextCatalog.Format(
                    CampusNpcEcologyTextId.EventLogLine,
                    currentSummary,
                    gossipHeat));
            }
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
