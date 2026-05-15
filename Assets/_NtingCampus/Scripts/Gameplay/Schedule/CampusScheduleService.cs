using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Schedule
{
    [DisallowMultipleComponent]
    public sealed class CampusScheduleService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;

        public CampusTimeController TimeController => timeController;
        public bool IsNightActionWindow => timeController != null && timeController.AllowsNightFreeAction;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            timeController = bootstrap != null ? bootstrap.TimeController : null;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;

            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
                timeController.SegmentChanged += HandleSegmentChanged;
                timeController.DailySettlementStarted -= HandleDailySettlementStarted;
                timeController.DailySettlementStarted += HandleDailySettlementStarted;
            }

            ApplyCurrentSegment();
        }

        public bool IsClassSession(CampusTimeSegment segment)
        {
            switch (segment)
            {
                case CampusTimeSegment.MorningReading:
                case CampusTimeSegment.MorningClass1:
                case CampusTimeSegment.MorningClass2:
                case CampusTimeSegment.MorningClass3:
                case CampusTimeSegment.MorningClass4:
                case CampusTimeSegment.AfternoonClass1:
                case CampusTimeSegment.AfternoonClass2:
                case CampusTimeSegment.AfternoonClass3:
                case CampusTimeSegment.AfternoonClass4:
                case CampusTimeSegment.EveningStudy1:
                case CampusTimeSegment.EveningStudy2:
                case CampusTimeSegment.EveningStudy3:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsClassSessionNow()
        {
            return timeController != null && IsClassSession(timeController.CurrentSegment);
        }

        private void OnDestroy()
        {
            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
                timeController.DailySettlementStarted -= HandleDailySettlementStarted;
            }
        }

        private void HandleSegmentChanged(CampusTimeSegment _, CampusTimeSegment __)
        {
            ApplyCurrentSegment();
        }

        private void HandleDailySettlementStarted(CampusGameDate _)
        {
            if (bootstrap != null && bootstrap.GameState != null)
            {
                bootstrap.GameState.AdvanceToNextDay();
            }

            if (rosterService == null)
            {
                return;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                runtime.Data.SetState(CampusCharacterState.Normal);
            }
        }

        private void ApplyCurrentSegment()
        {
            if (timeController == null || worldService == null || rosterService == null)
            {
                return;
            }

            CampusTimeSegment currentSegment = timeController.CurrentSegment;
            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                CampusRoomType targetRoomType = ResolveTargetRoomType(runtime.Data, currentSegment);
                CampusGameplayRoom targetRoom = worldService.FindFirstUsableRoom(targetRoomType);
                if (targetRoom == null)
                {
                    continue;
                }

                Vector3 offset = ResolveRoomOffset(runtime.Data);
                runtime.transform.position = targetRoom.WorldCenter + offset;
                runtime.Data.SetCurrentRoom(targetRoom.RoomId);

                if (runtime.Data.Role == CampusCharacterRole.Student && IsClassSession(currentSegment))
                {
                    runtime.Data.SetState(CampusCharacterState.Normal);
                }
            }
        }

        private static CampusRoomType ResolveTargetRoomType(CampusCharacterData data, CampusTimeSegment currentSegment)
        {
            if (data == null)
            {
                return CampusRoomType.Unknown;
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                if (currentSegment == CampusTimeSegment.NightFree ||
                    currentSegment == CampusTimeSegment.DormReturn ||
                    currentSegment == CampusTimeSegment.DormCheck ||
                    currentSegment == CampusTimeSegment.LightsOut)
                {
                    return CampusRoomType.Office;
                }

                return IsInstructionalSegment(currentSegment)
                    ? CampusRoomType.Classroom
                    : CampusRoomType.Office;
            }

            switch (currentSegment)
            {
                case CampusTimeSegment.WakeUp:
                case CampusTimeSegment.LightsOut:
                case CampusTimeSegment.DormReturn:
                case CampusTimeSegment.DormCheck:
                case CampusTimeSegment.NightFree:
                    return CampusRoomType.Dormitory;
                case CampusTimeSegment.LunchBreak:
                case CampusTimeSegment.DinnerBreak:
                case CampusTimeSegment.MorningBreak1:
                case CampusTimeSegment.MorningExerciseBreak:
                case CampusTimeSegment.MorningBreak2:
                case CampusTimeSegment.AfternoonBreak1:
                case CampusTimeSegment.AfternoonBreak2:
                case CampusTimeSegment.AfternoonBreak3:
                case CampusTimeSegment.EveningBreak1:
                case CampusTimeSegment.EveningBreak2:
                    return CampusRoomType.CommonActivityZone;
                default:
                    return CampusRoomType.Classroom;
            }
        }

        private static bool IsInstructionalSegment(CampusTimeSegment currentSegment)
        {
            switch (currentSegment)
            {
                case CampusTimeSegment.MorningReading:
                case CampusTimeSegment.MorningClass1:
                case CampusTimeSegment.MorningClass2:
                case CampusTimeSegment.MorningClass3:
                case CampusTimeSegment.MorningClass4:
                case CampusTimeSegment.AfternoonClass1:
                case CampusTimeSegment.AfternoonClass2:
                case CampusTimeSegment.AfternoonClass3:
                case CampusTimeSegment.AfternoonClass4:
                case CampusTimeSegment.EveningStudy1:
                case CampusTimeSegment.EveningStudy2:
                case CampusTimeSegment.EveningStudy3:
                    return true;
                default:
                    return false;
            }
        }

        private static Vector3 ResolveRoomOffset(CampusCharacterData data)
        {
            if (data == null)
            {
                return Vector3.zero;
            }

            if (data.IsPlayerControlled)
            {
                return new Vector3(-0.6f, -0.4f, 0f);
            }

            return data.Role == CampusCharacterRole.Teacher
                ? new Vector3(0.4f, 0.7f, 0f)
                : new Vector3(0.8f, -0.2f, 0f);
        }
    }
}
