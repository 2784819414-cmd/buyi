using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
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
            }

            RefreshRuntimeRoomBindings();
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
                return CampusRoomType.Office;
            }

            if (CampusNpcScheduleFacts.IsClassSession(segment))
            {
                return CampusRoomType.Classroom;
            }

            if (CampusNpcScheduleFacts.IsDormWindow(segment))
            {
                return CampusRoomType.Dormitory;
            }

            return CampusRoomType.CommonActivityZone;
        }

        private void OnDestroy()
        {
            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
            }
        }

        private void HandleSegmentChanged(CampusTimeSegment previousSegment, CampusTimeSegment currentSegment)
        {
            RefreshRuntimeRoomBindings();
        }

        private void RefreshRuntimeRoomBindings()
        {
            if (timeController == null || worldService == null || rosterService == null)
            {
                return;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                SyncRuntimeRoomBinding(runtime);
            }
        }

        private void SyncRuntimeRoomBinding(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            CampusCharacterCurrentRoomTracker.SyncRuntime(runtime, worldService);
        }
    }
}

