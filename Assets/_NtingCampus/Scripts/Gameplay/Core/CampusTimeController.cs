using System;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    [DisallowMultipleComponent]
    public sealed class CampusTimeController : MonoBehaviour
    {
        private const int MaxSegmentTransitionsPerAdvance = 128;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusGameDate initialDate = CampusGameDate.DefaultStartDate;
        [SerializeField] private CampusTimeSegment initialSegment = CampusTimeSegment.MorningClass1;
        [SerializeField] private CampusGameDate currentDate = CampusGameDate.DefaultStartDate;
        [SerializeField] private CampusTimeSegment currentSegment = CampusTimeSegment.WakeUp;
        [SerializeField, Min(0f)] private float segmentElapsedMinutes;
        [SerializeField] private bool autoAdvance = true;
        [SerializeField] private bool syncDayNightClock = true;
        [SerializeField] private CampusTimeSpeedMode speedMode = CampusTimeSpeedMode.Normal;
        [SerializeField, Range(0f, 200f)] private float customTimeScale = 1f;
        [SerializeField] private CampusDayNightController dayNightController;

        private bool isInitialized;

        public event Action<CampusTimeSegment, CampusTimeSegment> SegmentChanged;
        public event Action<CampusGameDate> DailySettlementStarted;
        public event Action<CampusGameDate> GameDateChanged;

        public CampusGameDate CurrentDate => currentDate;
        public string CurrentDateText => currentDate.ToDisplayString(CampusLanguageState.CurrentLanguage);
        public CampusTimeSegment CurrentSegment => currentSegment;
        public string CurrentSegmentName => CampusTimeSchedule.GetDisplayName(CampusLanguageState.CurrentLanguage, currentSegment);
        public string CurrentTimeLabel => CampusTimeSchedule.GetTimeLabel(currentSegment);
        public string CurrentClockText => CampusTimeSchedule.GetClockText(currentSegment, segmentElapsedMinutes);
        public float CurrentGameHour => ResolveCurrentGameHour();
        public float SegmentElapsedMinutes => segmentElapsedMinutes;
        public float SegmentDurationMinutes => CampusTimeSchedule.GetDurationMinutes(currentSegment);
        public float SegmentProgress01 => SegmentDurationMinutes <= 0f ? 1f : Mathf.Clamp01(segmentElapsedMinutes / SegmentDurationMinutes);
        public float TimeScale => ResolveTimeScale();
        public CampusTimeSpeedMode SpeedMode => speedMode;
        public bool IsNightFree => currentSegment == CampusTimeSegment.NightFree;
        public bool AllowsNightFreeAction => IsNightFree;

        public void InitializeTimeSystem(CampusGameBootstrap owner, bool writeInitialSegmentLog)
        {
            bootstrap = owner;
            ResolveReferences();
            NormalizeDates();
            currentDate = initialDate;
            currentSegment = initialSegment;
            segmentElapsedMinutes = 0f;
            isInitialized = true;
            SyncDayNightClock();

            if (writeInitialSegmentLog)
            {
                WriteSegmentLog(currentSegment);
            }
        }

        public void SetSpeedMode(CampusTimeSpeedMode mode)
        {
            speedMode = mode;
            if (speedMode == CampusTimeSpeedMode.Custom)
            {
                customTimeScale = Mathf.Clamp(customTimeScale, 0f, 200f);
            }
        }

        public void SetCustomTimeScale(float scale)
        {
            customTimeScale = Mathf.Clamp(scale, 0f, 200f);
            speedMode = CampusTimeSpeedMode.Custom;
        }

        public void AdvanceMinutes(float minutes)
        {
            if (minutes <= 0f)
            {
                return;
            }

            EnsureInitialized();

            int guard = 0;
            float remainingMinutes = minutes;
            while (remainingMinutes > 0f && guard < MaxSegmentTransitionsPerAdvance)
            {
                guard++;
                float duration = Mathf.Max(0.01f, SegmentDurationMinutes);
                float remainingInSegment = Mathf.Max(0f, duration - segmentElapsedMinutes);
                if (remainingMinutes < remainingInSegment)
                {
                    segmentElapsedMinutes += remainingMinutes;
                    SyncDayNightClock();
                    return;
                }

                remainingMinutes -= remainingInSegment;
                CompleteCurrentSegment();
            }
        }

        public void SetSegment(CampusTimeSegment segment, bool writeLog)
        {
            EnsureInitialized();

            if (currentSegment == segment)
            {
                segmentElapsedMinutes = 0f;
                SyncDayNightClock();
                return;
            }

            EnterSegment(currentSegment, segment, writeLog);
        }

        private void Awake()
        {
            ResolveReferences();
            NormalizeDates();
        }

        private void Start()
        {
            if (!isInitialized)
            {
                InitializeTimeSystem(ResolveBootstrap(), true);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !autoAdvance)
            {
                return;
            }

            AdvanceMinutes(Time.deltaTime * ResolveTimeScale());
        }

        private void OnValidate()
        {
            NormalizeDates();
            segmentElapsedMinutes = Mathf.Max(0f, segmentElapsedMinutes);
            customTimeScale = Mathf.Clamp(customTimeScale, 0f, 200f);
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                InitializeTimeSystem(ResolveBootstrap(), false);
            }
        }

        private CampusGameBootstrap ResolveBootstrap()
        {
            if (bootstrap != null)
            {
                return bootstrap;
            }

            bootstrap = GetComponent<CampusGameBootstrap>();
            if (bootstrap != null)
            {
                return bootstrap;
            }

            bootstrap = CampusGameBootstrap.Instance;
            return bootstrap;
        }

        private void ResolveReferences()
        {
            ResolveBootstrap();
            if (syncDayNightClock && dayNightController == null)
            {
                dayNightController = FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }
        }

        private float ResolveTimeScale()
        {
            switch (speedMode)
            {
                case CampusTimeSpeedMode.Paused:
                    return 0f;
                case CampusTimeSpeedMode.Fast:
                    return 2f;
                case CampusTimeSpeedMode.Custom:
                    return Mathf.Clamp(customTimeScale, 0f, 200f);
                default:
                    return 1f;
            }
        }

        private float ResolveCurrentGameHour()
        {
            int startMinute = CampusTimeSchedule.GetStartMinute(currentSegment);
            float minuteOfDay = Mathf.Repeat(startMinute + segmentElapsedMinutes, 24f * 60f);
            return minuteOfDay / 60f;
        }

        private void SyncDayNightClock()
        {
            if (!syncDayNightClock)
            {
                return;
            }

            if (dayNightController == null)
            {
                dayNightController = FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }

            if (dayNightController != null)
            {
                dayNightController.ApplyExternalGameHour(CurrentGameHour);
            }
        }

        private void NormalizeDates()
        {
            if (!initialDate.TryToDateTime(out _))
            {
                initialDate = CampusGameDate.DefaultStartDate;
            }

            initialDate.Set(initialDate.Year, initialDate.Month, initialDate.Day);

            if (!currentDate.TryToDateTime(out _))
            {
                currentDate = initialDate;
            }

            currentDate.Set(currentDate.Year, currentDate.Month, currentDate.Day);
        }

        private void CompleteCurrentSegment()
        {
            CampusTimeSegment previousSegment = currentSegment;
            CampusTimeSegment nextSegment = CampusTimeSchedule.GetNextSegment(currentSegment);

            if (previousSegment == CampusTimeSegment.PreWakeSettlement)
            {
                AdvanceToNextSchoolDay();
            }

            EnterSegment(previousSegment, nextSegment, true);
        }

        private void EnterSegment(CampusTimeSegment previousSegment, CampusTimeSegment nextSegment, bool writeLog)
        {
            currentSegment = nextSegment;
            segmentElapsedMinutes = 0f;
            SyncDayNightClock();
            SegmentChanged?.Invoke(previousSegment, currentSegment);

            if (currentSegment == CampusTimeSegment.PreWakeSettlement)
            {
                DailySettlementStarted?.Invoke(currentDate);
            }

            if (writeLog)
            {
                WriteSegmentLog(currentSegment);
            }
        }

        private void WriteSegmentLog(CampusTimeSegment segment)
        {
            CampusEventLog eventLog = bootstrap != null ? bootstrap.EventLog : null;
            if (eventLog == null)
            {
                return;
            }

            eventLog.AddLog(CampusTimeSchedule.GetSegmentLogMessage(CampusLanguageState.CurrentLanguage, segment, CurrentDateText));
        }

        private void AdvanceToNextSchoolDay()
        {
            // Calendar date and gameplay day count advance together at the school-day boundary.
            currentDate.AddDays(1);
            if (bootstrap != null && bootstrap.GameState != null)
            {
                bootstrap.GameState.AdvanceToNextDay();
            }

            GameDateChanged?.Invoke(currentDate);
        }
    }
}
