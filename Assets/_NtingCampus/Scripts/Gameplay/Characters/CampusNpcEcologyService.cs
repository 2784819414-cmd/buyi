using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcEcologyService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField, Range(0, 100)] private int gossipHeat;
        [SerializeField, Min(0)] private int dailyEcologyEventCount;
        [SerializeField] private string currentSummary = "NPC ecology waiting for events.";

        private bool subscribed;

        public int GossipHeat => gossipHeat;
        public int DailyEcologyEventCount => dailyEcologyEventCount;
        public string CurrentSummary => currentSummary;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            if (subscribed)
            {
                ReleaseSubscriptions();
            }

            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            timeController = bootstrap != null ? bootstrap.TimeController : null;
            scheduleService = bootstrap != null ? bootstrap.ScheduleService : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;

            EnsureAllEcologyInitialized();
            Subscribe();
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

        private void EnsureAllEcologyInitialized()
        {
            if (rosterService == null)
            {
                return;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime != null && runtime.Data != null)
                {
                    runtime.Data.EnsureEcologyInitialized();
                }
            }
        }

        private void HandleSegmentChanged(CampusTimeSegment previousSegment, CampusTimeSegment currentSegment)
        {
            if (rosterService == null)
            {
                return;
            }

            bool classSession = scheduleService != null && scheduleService.IsClassSession(currentSegment);
            bool socialWindow = IsSocialWindow(currentSegment);
            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                CampusCharacterData data = runtime != null ? runtime.Data : null;
                if (data == null || data.IsPlayerControlled)
                {
                    continue;
                }

                data.EnsureEcologyInitialized();
                if (classSession)
                {
                    data.AddSocialEnergy(data.Role == CampusCharacterRole.Teacher ? -1 : -3);
                    data.AddMood(data.HasTrait(CampusCharacterTrait.GoodStudent) ? 1 : -1);
                }
                else if (socialWindow)
                {
                    data.AddSocialEnergy(data.Role == CampusCharacterRole.Student ? 7 : 3);
                    data.AddMood(data.HasTrait(CampusCharacterTrait.Troublemaker) ? 2 : 1);
                }
                else if (currentSegment == CampusTimeSegment.LightsOut ||
                         currentSegment == CampusTimeSegment.PreWakeSettlement)
                {
                    data.AddSocialEnergy(-4);
                }
            }

            currentSummary = "Segment ecology pulse: " + currentSegment + ".";
        }

        private void HandleDailySettlementStarted(CampusGameDate _)
        {
            if (rosterService != null)
            {
                foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
                {
                    if (runtime != null && runtime.Data != null)
                    {
                        runtime.Data.ApplyDailyEcologyRecovery();
                    }
                }
            }

            gossipHeat = Mathf.Max(0, gossipHeat - 25);
            dailyEcologyEventCount = 0;
            currentSummary = "Daily ecology recovery applied.";
        }

        private void HandlePrankAttempted(CampusPrankAttemptedEvent eventData)
        {
            string actorId = NormalizeActorId(eventData.ActorId);
            ApplyToRoom(eventData.RoomId, runtime =>
            {
                CampusCharacterData data = runtime.Data;
                data.AddMood(data.Role == CampusCharacterRole.Teacher ? -2 : 0);
                if (!CanReactToActor(runtime, actorId))
                {
                    return;
                }

                if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
                {
                    data.AddRelationshipSuspicion(actorId, 7);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.WitnessedActorPrank);
                }
                else if (data.HasTrait(CampusCharacterTrait.Tattletale))
                {
                    data.AddRelationshipSuspicion(actorId, 8);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
                }
                else if (data.HasTrait(CampusCharacterTrait.Troublemaker))
                {
                    data.AddMood(3);
                    data.AddRelationshipTrust(actorId, 2);
                }
            });

            if (!string.IsNullOrEmpty(actorId))
            {
                AddGossip(3);
                RecordEcologyEvent("NPC ecology noticed an actor prank attempt.");
            }
        }

        private void HandlePrankResolved(CampusPrankResolvedEvent eventData)
        {
            string actorId = NormalizeActorId(eventData.ActorId);
            CampusCharacterRuntime targetRuntime = rosterService != null ? rosterService.FindRuntime(eventData.TargetId) : null;
            if (targetRuntime != null &&
                targetRuntime.Data != null &&
                !targetRuntime.Data.IsPlayerControlled &&
                CanReactToActor(targetRuntime, actorId))
            {
                targetRuntime.Data.AddMood(eventData.Succeeded ? -7 : -3);
                targetRuntime.Data.AddRelationshipTrust(actorId, eventData.Succeeded ? -10 : -4);
                targetRuntime.Data.AddRelationshipSuspicion(actorId, eventData.DetectedByTeacher ? 12 : 8);
                AddMemoryIfMissing(targetRuntime.Data, CampusCharacterMemoryId.DistrustsActor);
            }

            ApplyToRoom(eventData.RoomId, runtime =>
            {
                if (!CanReactToActor(runtime, actorId))
                {
                    return;
                }

                CampusCharacterData data = runtime.Data;
                if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
                {
                    data.AddRelationshipSuspicion(actorId, eventData.DetectedByTeacher ? 16 : 6);
                    data.AddMood(eventData.DetectedByTeacher ? -3 : -1);
                }
                else if (data.HasTrait(CampusCharacterTrait.Troublemaker) && eventData.Succeeded)
                {
                    data.AddRelationshipTrust(actorId, eventData.DetectedByTeacher ? 1 : 6);
                    data.AddMood(eventData.DetectedByTeacher ? 1 : 5);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.ImpressedByActor);
                }
                else if (data.HasTrait(CampusCharacterTrait.GoodStudent))
                {
                    data.AddRelationshipTrust(actorId, -4);
                    data.AddRelationshipSuspicion(actorId, 4);
                    data.AddMood(-3);
                }
                else if (data.HasTrait(CampusCharacterTrait.Tattletale))
                {
                    data.AddRelationshipSuspicion(actorId, eventData.DetectedByTeacher ? 8 : 12);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
                }
            });

            if (!string.IsNullOrEmpty(actorId))
            {
                AddGossip(eventData.DetectedByTeacher ? 12 : eventData.Succeeded ? 6 : 3);
                RecordEcologyEvent(eventData.DetectedByTeacher
                    ? "NPC ecology marked the actor as risky after a detected prank."
                    : "NPC ecology shifted after an undetected actor prank.");
            }
        }

        private void HandleSanctionIssued(CampusSanctionIssuedEvent eventData)
        {
            string actorId = NormalizeActorId(eventData.ActorId);
            ApplyToRoom(eventData.RoomId, runtime =>
            {
                CampusCharacterData data = runtime.Data;
                if (data.Role == CampusCharacterRole.Teacher)
                {
                    data.AddMood(2);
                }
                else
                {
                    data.AddMood(data.HasTrait(CampusCharacterTrait.Troublemaker) ? 1 : -4);
                    data.AddSocialEnergy(-4);
                }

                if (CanReactToActor(runtime, actorId))
                {
                    data.AddRelationshipSuspicion(actorId, 5);
                    data.AddRelationshipTrust(actorId, -2);
                }
            });

            if (!string.IsNullOrEmpty(actorId))
            {
                AddGossip(8);
                RecordEcologyEvent("NPC ecology reacted to an actor sanction.");
            }
        }

        private void HandleStudentDozedOff(CampusStudentDozedOffEvent eventData)
        {
            CampusCharacterRuntime sleeper = rosterService != null ? rosterService.FindRuntime(eventData.StudentId) : null;
            if (sleeper != null && sleeper.Data != null)
            {
                sleeper.Data.AddMood(-4);
                sleeper.Data.AddSocialEnergy(-10);
            }

            ApplyToRoom(eventData.RoomId, runtime =>
            {
                CampusCharacterData data = runtime.Data;
                if (string.Equals(runtime.CharacterId, eventData.StudentId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (data.Role == CampusCharacterRole.Teacher)
                {
                    data.AddMood(-3);
                    return;
                }

                if (data.HasTrait(CampusCharacterTrait.Troublemaker))
                {
                    data.AddMood(2);
                }
                else if (data.HasTrait(CampusCharacterTrait.GoodStudent))
                {
                    data.AddMood(-1);
                }
            });

            RecordEcologyEvent("NPC ecology reacted to classroom dozing.");
        }

        private void HandleTeacherDistracted(CampusTeacherDistractedEvent eventData)
        {
            ApplyToRoom(eventData.RoomId, runtime =>
            {
                CampusCharacterData data = runtime.Data;
                if (data.Role != CampusCharacterRole.Student)
                {
                    return;
                }

                if (data.HasTrait(CampusCharacterTrait.Troublemaker))
                {
                    data.AddMood(3);
                    data.AddSocialEnergy(2);
                }
                else if (data.HasTrait(CampusCharacterTrait.GoodStudent))
                {
                    data.AddMood(-1);
                }
            });

            currentSummary = "Teacher distraction changed classroom mood.";
        }

        private void HandleActorSkipClass(CampusActorSkipClassEvent eventData)
        {
            string actorId = NormalizeActorId(eventData.ActorId);
            ApplyToRoom(eventData.FromRoomId, runtime =>
            {
                if (!CanReactToActor(runtime, actorId))
                {
                    return;
                }

                CampusCharacterData data = runtime.Data;
                if (data.Role == CampusCharacterRole.Teacher)
                {
                    data.AddRelationshipSuspicion(actorId, eventData.DetectedByTeacher ? 14 : 7);
                    data.AddMood(eventData.DetectedByTeacher ? 1 : -2);
                    return;
                }

                if (eventData.DetectedByTeacher)
                {
                    data.AddRelationshipTrust(actorId, -3);
                    data.AddRelationshipSuspicion(actorId, 5);
                    data.AddMood(-3);
                }
                else if (data.HasTrait(CampusCharacterTrait.Troublemaker))
                {
                    data.AddRelationshipTrust(actorId, 6);
                    data.AddMood(4);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.ImpressedByActor);
                }
                else if (data.HasTrait(CampusCharacterTrait.Tattletale))
                {
                    data.AddRelationshipSuspicion(actorId, 10);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
                }
            });

            if (!string.IsNullOrEmpty(actorId))
            {
                AddGossip(eventData.DetectedByTeacher ? 12 : 7);
                RecordEcologyEvent(eventData.DetectedByTeacher
                    ? "NPC ecology reacted to detected class skipping."
                    : "NPC ecology reacted to escaped class skipping.");
            }
        }

        private void HandleItemTransferred(CampusItemTransferredEvent eventData)
        {
            if (!eventData.Illegal)
            {
                return;
            }

            string actorId = NormalizeActorId(eventData.ActorId);
            CampusCharacterRuntime actorRuntime = rosterService != null ? rosterService.FindRuntime(actorId) : null;
            if (actorRuntime != null && actorRuntime.Data != null)
            {
                AddMemoryIfMissing(actorRuntime.Data, CampusCharacterMemoryId.TookProtectedItem);
            }

            ApplyToRoom(eventData.RoomId, runtime =>
            {
                if (!CanReactToActor(runtime, actorId))
                {
                    return;
                }

                CampusCharacterData data = runtime.Data;
                if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
                {
                    data.AddRelationshipSuspicion(actorId, eventData.Observed ? 14 : 6);
                    data.AddMood(eventData.Observed ? -2 : -1);
                    return;
                }

                if (data.HasTrait(CampusCharacterTrait.Tattletale))
                {
                    data.AddRelationshipSuspicion(actorId, eventData.Observed ? 12 : 7);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
                }
                else if (data.HasTrait(CampusCharacterTrait.Troublemaker))
                {
                    data.AddRelationshipTrust(actorId, eventData.Observed ? 1 : 4);
                    data.AddMood(2);
                }
                else if (data.HasTrait(CampusCharacterTrait.GoodStudent))
                {
                    data.AddRelationshipSuspicion(actorId, 4);
                    data.AddRelationshipTrust(actorId, -2);
                }
            });

            AddGossip(eventData.Observed ? 10 : 4);
            RecordEcologyEvent(eventData.Observed
                ? "NPC ecology reacted to an observed protected item move."
                : "NPC ecology picked up quiet rumors about missing property.");
        }

        private void HandleItemTheftObserved(CampusItemTheftObservedEvent eventData)
        {
            string actorId = NormalizeActorId(eventData.ActorId);
            CampusCharacterRuntime witness = rosterService != null ? rosterService.FindRuntime(eventData.WitnessId) : null;
            if (witness != null && witness.Data != null && CanReactToActor(witness, actorId))
            {
                witness.Data.AddRelationshipSuspicion(actorId, 18);
                witness.Data.AddRelationshipTrust(actorId, -6);
                AddMemoryIfMissing(witness.Data, CampusCharacterMemoryId.WitnessedTheft);
            }

            CampusCharacterRuntime owner = rosterService != null ? rosterService.FindRuntime(eventData.OwnerId) : null;
            if (owner != null && owner.Data != null && CanReactToActor(owner, actorId))
            {
                owner.Data.AddRelationshipSuspicion(actorId, 16);
                owner.Data.AddRelationshipTrust(actorId, -12);
                owner.Data.AddMood(-6);
                AddMemoryIfMissing(owner.Data, CampusCharacterMemoryId.DistrustsActor);
            }

            AddGossip(14);
            RecordEcologyEvent("NPC ecology escalated after a witnessed item theft.");
        }

        private void HandleInventoryQuestioned(CampusInventoryQuestionedEvent eventData)
        {
            string actorId = NormalizeActorId(eventData.ActorId);
            ApplyToRoom(eventData.RoomId, runtime =>
            {
                if (!CanReactToActor(runtime, actorId))
                {
                    return;
                }

                CampusCharacterData data = runtime.Data;
                if (string.Equals(runtime.CharacterId, eventData.InspectorId, StringComparison.OrdinalIgnoreCase))
                {
                    data.AddRelationshipSuspicion(actorId, eventData.FoundContraband ? 8 : 2);
                    data.AddMood(eventData.FoundContraband ? -1 : 1);
                    return;
                }

                if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
                {
                    data.AddRelationshipSuspicion(actorId, eventData.FoundContraband ? 10 : 3);
                    data.AddMood(eventData.FoundContraband ? -2 : 0);
                }
                else if (data.HasTrait(CampusCharacterTrait.Tattletale))
                {
                    data.AddRelationshipSuspicion(actorId, eventData.FoundContraband ? 12 : 5);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
                }
                else if (data.HasTrait(CampusCharacterTrait.GoodStudent))
                {
                    data.AddRelationshipSuspicion(actorId, eventData.FoundContraband ? 6 : 2);
                    data.AddRelationshipTrust(actorId, eventData.FoundContraband ? -4 : -1);
                }
                else if (data.HasTrait(CampusCharacterTrait.Troublemaker) && !eventData.FoundContraband)
                {
                    data.AddMood(1);
                }
            });

            if (!string.IsNullOrEmpty(actorId))
            {
                AddGossip(eventData.FoundContraband ? 16 : 4);
                RecordEcologyEvent(eventData.FoundContraband
                    ? "NPC ecology reacted to contraband questioning."
                    : "NPC ecology noticed a bag questioning.");
            }
        }

        private void HandleContrabandFound(CampusContrabandFoundEvent eventData)
        {
            string actorId = NormalizeActorId(eventData.ActorId);
            CampusCharacterRuntime actorRuntime = rosterService != null ? rosterService.FindRuntime(actorId) : null;
            if (actorRuntime != null && actorRuntime.Data != null)
            {
                AddMemoryIfMissing(actorRuntime.Data, CampusCharacterMemoryId.FoundContraband);
                actorRuntime.Data.SetState(CampusCharacterState.Nervous);
            }

            CampusCharacterRuntime inspectorRuntime = rosterService != null ? rosterService.FindRuntime(eventData.InspectorId) : null;
            if (inspectorRuntime != null && inspectorRuntime.Data != null && CanReactToActor(inspectorRuntime, actorId))
            {
                inspectorRuntime.Data.AddRelationshipSuspicion(actorId, 16);
                inspectorRuntime.Data.AddRelationshipTrust(actorId, -5);
                AddMemoryIfMissing(inspectorRuntime.Data, CampusCharacterMemoryId.FoundContraband);
            }

            ApplyToRoom(eventData.RoomId, runtime =>
            {
                if (!CanReactToActor(runtime, actorId))
                {
                    return;
                }

                CampusCharacterData data = runtime.Data;
                if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
                {
                    data.AddRelationshipSuspicion(actorId, 12);
                    data.AddMood(-2);
                    return;
                }

                if (data.HasTrait(CampusCharacterTrait.Tattletale))
                {
                    data.AddRelationshipSuspicion(actorId, 15);
                    AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
                    return;
                }

                if (data.HasTrait(CampusCharacterTrait.GoodStudent))
                {
                    data.AddRelationshipSuspicion(actorId, 7);
                    data.AddRelationshipTrust(actorId, -5);
                    data.AddMood(-2);
                }
            });

            AddGossip(18);
            RecordEcologyEvent("NPC ecology escalated after contraband was found.");
        }

        private void ApplyToRoom(string roomId, Action<CampusCharacterRuntime> reaction)
        {
            if (rosterService == null || reaction == null || string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                if (!IsRuntimeInRoom(runtime, roomId))
                {
                    continue;
                }

                runtime.Data.EnsureEcologyInitialized();
                reaction(runtime);
            }
        }

        private bool IsRuntimeInRoom(CampusCharacterRuntime runtime, string roomId)
        {
            if (runtime == null || runtime.Data == null || string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            CampusGameplayRoom room = worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
            string currentRoomId = room != null ? room.RoomId : runtime.Data.CurrentRoomId;
            return string.Equals(currentRoomId, roomId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void AddGossip(int amount)
        {
            gossipHeat = Mathf.Clamp(gossipHeat + amount, 0, 100);
        }

        private void RecordEcologyEvent(string summary)
        {
            dailyEcologyEventCount++;
            currentSummary = summary;
            if (bootstrap != null && bootstrap.EventLog != null && dailyEcologyEventCount <= 8)
            {
                bootstrap.EventLog.AddLog("[NPC] " + summary + " Gossip=" + gossipHeat + ".");
            }
        }

        private static void AddMemoryIfMissing(CampusCharacterData data, CampusCharacterMemoryId memory)
        {
            if (data == null || memory == CampusCharacterMemoryId.None)
            {
                return;
            }

            IReadOnlyList<CampusCharacterMemoryId> memories = data.Memories;
            if (memories != null)
            {
                for (int i = 0; i < memories.Count; i++)
                {
                    if (memories[i] == memory)
                    {
                        return;
                    }
                }
            }

            data.AddMemory(memory);
        }

        private static bool IsSocialWindow(CampusTimeSegment segment)
        {
            switch (segment)
            {
                case CampusTimeSegment.MorningBreak1:
                case CampusTimeSegment.MorningExerciseBreak:
                case CampusTimeSegment.MorningBreak2:
                case CampusTimeSegment.LunchBreak:
                case CampusTimeSegment.AfternoonBreak1:
                case CampusTimeSegment.AfternoonBreak2:
                case CampusTimeSegment.AfternoonBreak3:
                case CampusTimeSegment.DinnerBreak:
                case CampusTimeSegment.EveningBreak1:
                case CampusTimeSegment.EveningBreak2:
                case CampusTimeSegment.NightFree:
                    return true;
                default:
                    return false;
            }
        }

        private static int ResolveHighestRelationshipSuspicion(CampusCharacterData data)
        {
            if (data == null || data.Relationships == null)
            {
                return 0;
            }

            int highest = 0;
            IReadOnlyList<CampusCharacterRelationship> relationships = data.Relationships;
            for (int i = 0; i < relationships.Count; i++)
            {
                CampusCharacterRelationship relationship = relationships[i];
                if (relationship != null && relationship.Suspicion > highest)
                {
                    highest = relationship.Suspicion;
                }
            }

            return highest;
        }

        private static bool CanReactToActor(CampusCharacterRuntime runtime, string actorId)
        {
            return !string.IsNullOrEmpty(actorId) && !IsSameActor(runtime, actorId);
        }

        private static bool IsSameActor(CampusCharacterRuntime runtime, string actorId)
        {
            return runtime != null &&
                   !string.IsNullOrWhiteSpace(runtime.CharacterId) &&
                   string.Equals(runtime.CharacterId, actorId, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeActorId(string actorId)
        {
            return string.IsNullOrWhiteSpace(actorId) ? string.Empty : actorId.Trim();
        }
    }
}
