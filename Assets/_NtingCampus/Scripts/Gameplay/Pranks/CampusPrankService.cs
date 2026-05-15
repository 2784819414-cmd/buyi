using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    public enum CampusPrankType
    {
        Unknown = 0,
        PassNote = 1
    }

    [DisallowMultipleComponent]
    public sealed class CampusPrankService : MonoBehaviour, ICampusInteractionActionHandler
    {
        public const string PassNotePayload = CampusPrankPayloadIds.PassNote;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField, Min(0.1f)] private float prankCooldownSeconds = 1.25f;
        [SerializeField, Min(1)] private int basePassNoteReward = 5;

        [SerializeField] private string currentPrompt = "Move into a classroom during class and press E to pass a note.";
        [SerializeField] private int dailyPassNoteCount;
        [SerializeField] private float lastPrankTime = -999f;

        public string CurrentPrompt => currentPrompt;
        public int DailyPassNoteCount => dailyPassNoteCount;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            scheduleService = bootstrap != null ? bootstrap.ScheduleService : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;

            if (bootstrap != null && bootstrap.TimeController != null)
            {
                bootstrap.TimeController.DailySettlementStarted -= HandleDailySettlementStarted;
                bootstrap.TimeController.DailySettlementStarted += HandleDailySettlementStarted;
            }

            RefreshPrompt();
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.TimeController != null)
            {
                bootstrap.TimeController.DailySettlementStarted -= HandleDailySettlementStarted;
            }
        }

        private void Update()
        {
            RefreshPrompt();
        }

        private void HandleDailySettlementStarted(CampusGameDate _)
        {
            dailyPassNoteCount = 0;
        }

        public bool SupportsPayload(string payload)
        {
            return string.Equals(payload, CampusPrankPayloadIds.PassNote, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(payload, CampusPrankPayloadIds.ConfuseBooks, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(payload, CampusPrankPayloadIds.StealDelivery, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(payload, CampusPrankPayloadIds.StealFriedChicken, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(payload, CampusPrankPayloadIds.TwistBottleCaps, System.StringComparison.OrdinalIgnoreCase);
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            string normalizedActionId = CampusInteractionActionIds.Normalize(actionId);
            if (!CampusInteractionActionIds.Equals(normalizedActionId, CampusInteractionActionIds.PrankExecute))
            {
                return false;
            }

            if (!string.Equals(payload, PassNotePayload, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryExecutePassNote(actor);
        }

        private bool TryExecutePassNote(GameObject actor)
        {
            if (Time.time - lastPrankTime < prankCooldownSeconds)
            {
                WriteLog("[Prank] Pass note is still cooling down.");
                return false;
            }

            CampusCharacterRuntime playerRuntime = ResolveActorRuntime(actor);
            if (playerRuntime == null || playerRuntime.Data == null)
            {
                WriteLog("[Prank] No formal player runtime is available.");
                return false;
            }

            if (scheduleService == null || !scheduleService.IsClassSessionNow())
            {
                WriteLog("[Prank] Passing notes only counts during class sessions.");
                return false;
            }

            CampusGameplayRoom classroom = worldService != null ? worldService.FindRoomForRuntime(playerRuntime) : null;
            if (classroom == null || classroom.RoomType != CampusRoomType.Classroom)
            {
                WriteLog("[Prank] You need to be inside a formal classroom.");
                return false;
            }

            CampusCharacterRuntime targetStudent = FindTargetStudent(playerRuntime, classroom.RoomId);
            if (targetStudent == null || targetStudent.Data == null)
            {
                WriteLog("[Prank] No nearby student is available to receive the note.");
                return false;
            }

            CampusCharacterRuntime teacherRuntime = FindTeacherInRoom(classroom.RoomId);
            gameplayEventHub?.PublishPrankAttempted(new CampusPrankAttemptedEvent(
                CampusPrankType.PassNote,
                playerRuntime.CharacterId,
                targetStudent.CharacterId,
                classroom.RoomId,
                true));

            int reward = ResolvePassNoteReward();
            bool detected = teacherRuntime != null && RollTeacherDetection();
            bool succeeded = !detected;

            playerRuntime.Data.AddMemory("passed_note_today");
            targetStudent.Data.AddMemory("received_note_from_player");
            if (teacherRuntime != null && teacherRuntime.Data != null)
            {
                teacherRuntime.Data.AddMemory(detected ? "caught_note_passing" : "saw_restless_classroom");
            }

            if (succeeded)
            {
                bootstrap.ResourceState.AddDivinePower(reward);
                bootstrap.GameState.AddCampusChaos(4);
                bootstrap.GameState.AddDivineInterest(5);
                bootstrap.GameState.AddTeacherAlertness(1);
                bootstrap.GameState.UnlockShrineRoom();
                WriteLog("[Prank] You passed the note cleanly. Divine Power +" + reward + ".");
            }
            else
            {
                reward = 0;
                bootstrap.GameState.AddCampusChaos(6);
                bootstrap.GameState.AddTeacherAlertness(6);
                bootstrap.GameState.AddDivineInterest(2);
                WriteLog("[Prank] The teacher noticed the note passing.");
            }

            gameplayEventHub?.PublishPrankResolved(new CampusPrankResolvedEvent(
                CampusPrankType.PassNote,
                playerRuntime.CharacterId,
                targetStudent.CharacterId,
                classroom.RoomId,
                succeeded,
                detected,
                reward));

            dailyPassNoteCount++;
            lastPrankTime = Time.time;
            RefreshPrompt();
            return true;
        }

        private CampusCharacterRuntime FindTargetStudent(CampusCharacterRuntime playerRuntime, string roomId)
        {
            if (rosterService == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Student))
            {
                if (runtime == null || runtime == playerRuntime || runtime.Data == null)
                {
                    continue;
                }

                if (string.Equals(runtime.Data.CurrentRoomId, roomId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private CampusCharacterRuntime FindTeacherInRoom(string roomId)
        {
            if (rosterService == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Teacher))
            {
                if (runtime != null &&
                    runtime.Data != null &&
                    string.Equals(runtime.Data.CurrentRoomId, roomId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private int ResolvePassNoteReward()
        {
            switch (dailyPassNoteCount)
            {
                case 0:
                    return basePassNoteReward;
                case 1:
                    return Mathf.Max(1, Mathf.RoundToInt(basePassNoteReward * 0.7f));
                default:
                    return Mathf.Max(1, Mathf.RoundToInt(basePassNoteReward * 0.4f));
            }
        }

        private bool RollTeacherDetection()
        {
            int alertness = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.TeacherAlertness : 0;
            float detectionChance = Mathf.Clamp01(0.15f + alertness / 100f * 0.55f);
            return Random.value < detectionChance;
        }

        private void RefreshPrompt()
        {
            if (scheduleService == null || !scheduleService.IsClassSessionNow())
            {
                currentPrompt = "Pass Note: wait for a class segment, then press E at the classroom prank spot.";
                return;
            }

            currentPrompt = "Pass Note available: press E at the classroom prank spot.";
        }

        private CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            if (actor != null)
            {
                CampusCharacterRuntime directRuntime = actor.GetComponent<CampusCharacterRuntime>();
                if (directRuntime != null)
                {
                    return directRuntime;
                }

                CampusPlayerCharacter playerCharacter = actor.GetComponent<CampusPlayerCharacter>();
                if (playerCharacter != null && playerCharacter.CharacterRuntime != null)
                {
                    return playerCharacter.CharacterRuntime;
                }
            }

            return rosterService != null ? rosterService.PlayerRuntime : null;
        }

        private void WriteLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
            }
        }
    }
}
