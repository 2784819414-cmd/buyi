using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
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
            return CampusNpcScheduleFacts.IsClassSession(segment);
        }

        public bool IsClassSessionNow()
        {
            return timeController != null && IsClassSession(timeController.CurrentSegment);
        }

        public CampusRoomType GetScheduledRoomType(CampusCharacterData data)
        {
            if (data == null || timeController == null)
            {
                return CampusRoomType.Unknown;
            }

            CampusTimeSegment segment = timeController.CurrentSegment;
            if (data.State == CampusCharacterState.Punished)
            {
                return CampusRoomType.Office;
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                return CampusNpcScheduleFacts.IsClassSession(segment)
                    ? CampusRoomType.Classroom
                    : CampusRoomType.Office;
            }

            if (data.Role == CampusCharacterRole.Staff)
            {
                if ((data.StaffDuty & CampusStaffDuty.StoreOwner) != 0 ||
                    (data.StaffDuty & CampusStaffDuty.BookstoreOwner) != 0)
                {
                    return CampusRoomType.Store;
                }

                if ((data.StaffDuty & CampusStaffDuty.DeliveryWatcher) != 0)
                {
                    return CampusRoomType.Outdoor;
                }

                return CampusRoomType.Canteen;
            }

            if (CampusNpcScheduleFacts.IsClassSession(segment))
            {
                return CampusRoomType.Classroom;
            }

            if (CampusNpcScheduleFacts.IsDormWindow(segment))
            {
                return CampusRoomType.Dormitory;
            }

            if (CampusNpcScheduleFacts.IsMealPeak(segment))
            {
                return CampusRoomType.Canteen;
            }

            return CampusRoomType.CommonActivityZone;
        }

        private void OnDestroy()
        {
            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
                timeController.DailySettlementStarted -= HandleDailySettlementStarted;
            }
        }

        private void HandleSegmentChanged(CampusTimeSegment previousSegment, CampusTimeSegment currentSegment)
        {
            ApplyCurrentSegment();
        }

        private void HandleDailySettlementStarted(CampusGameDate date)
        {
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
                runtime.Data.SetSleepiness(Mathf.Clamp(runtime.Data.Sleepiness + 15, 0, 100));
            }
        }

        private void ApplyCurrentSegment()
        {
            if (timeController == null || worldService == null || rosterService == null)
            {
                return;
            }

            bool classSession = IsClassSession(timeController.CurrentSegment);
            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                SyncRuntimeRoomBinding(runtime);
                if (runtime.Data.Role == CampusCharacterRole.Student && classSession)
                {
                    runtime.Data.SetState(CampusCharacterState.Normal);
                }
            }
        }

        private void SyncRuntimeRoomBinding(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null || worldService == null)
            {
                return;
            }

            int floorIndex = ResolveFloorIndex(runtime);
            CampusGameplayRoom actualRoom = worldService.FindRoomForPosition(floorIndex, runtime.transform.position);
            runtime.Data.SetCurrentRoom(actualRoom != null ? actualRoom.RoomId : string.Empty);
        }

        private static int ResolveFloorIndex(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return 1;
            }

            if (CampusRuntimeGameplayOverlayLoader.TryGetManagedEntity(runtime, out CampusRuntimeGameplayOverlayEntity entity))
            {
                return entity.FloorIndex;
            }

            CampusSceneCharacterDefinition sceneCharacter = runtime.GetComponent<CampusSceneCharacterDefinition>();
            return sceneCharacter != null ? sceneCharacter.FloorIndex : 1;
        }
    }
}
