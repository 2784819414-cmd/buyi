using System;
using NtingCampus.UI.Runtime.Gameplay;
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
        [SerializeField] private CampusTimeSpeedMode lastActiveSpeedMode = CampusTimeSpeedMode.Normal;
        [SerializeField, Range(0f, 200f)] private float lastActiveCustomTimeScale = 1f;

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
        public bool AutoAdvanceEnabled => autoAdvance;
        public bool IsTimePaused => speedMode == CampusTimeSpeedMode.Paused;
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
            RememberActiveSpeedSettings(speedMode);
            SyncDayNightClock();

            if (writeInitialSegmentLog)
            {
                WriteSegmentLog(currentSegment);
            }
        }

        public void SetSpeedMode(CampusTimeSpeedMode mode)
        {
            if (mode != CampusTimeSpeedMode.Paused)
            {
                RememberActiveSpeedSettings(mode);
            }

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
            RememberActiveSpeedSettings(CampusTimeSpeedMode.Custom);
        }

        public void SetAutoAdvanceEnabled(bool enabled)
        {
            autoAdvance = enabled;
        }

        public void TogglePauseTime(bool writeLog)
        {
            if (IsTimePaused)
            {
                ResumeTime(writeLog);
                return;
            }

            PauseTime(writeLog);
        }

        public void PauseTime(bool writeLog)
        {
            EnsureInitialized();
            if (IsTimePaused)
            {
                return;
            }

            RememberActiveSpeedSettings(speedMode);

            speedMode = CampusTimeSpeedMode.Paused;
            if (writeLog)
            {
                WriteTimePauseLog();
            }
        }

        public void ResumeTime(bool writeLog)
        {
            EnsureInitialized();
            if (!IsTimePaused)
            {
                return;
            }

            CampusTimeSpeedMode resumeMode = lastActiveSpeedMode == CampusTimeSpeedMode.Paused
                ? CampusTimeSpeedMode.Normal
                : lastActiveSpeedMode;
            speedMode = resumeMode;
            if (resumeMode == CampusTimeSpeedMode.Custom)
            {
                customTimeScale = Mathf.Clamp(lastActiveCustomTimeScale, 0f, 200f);
            }

            if (writeLog)
            {
                WriteTimeResumeLog();
            }
        }

        public bool TrySetDateAndClock(
            int year,
            int month,
            int day,
            int hour,
            int minute,
            bool writeLog,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (hour < 0 || hour > 23)
            {
                errorMessage = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidHour);
                return false;
            }

            if (minute < 0 || minute > 59)
            {
                errorMessage = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidMinute);
                return false;
            }

            CampusGameDate targetDate = new CampusGameDate(year, month, day);
            if (targetDate.Year != year || targetDate.Month != month || targetDate.Day != day)
            {
                errorMessage = CampusPlayerUiTextCatalog.Get(CampusPlayerUiTextId.TimeTestInvalidDate);
                return false;
            }

            SetDateAndClock(targetDate, hour * 60 + minute, writeLog);
            return true;
        }

        public void SetDateAndClock(CampusGameDate targetDate, int minuteOfDay, bool writeLog)
        {
            EnsureInitialized();

            CampusGameDate normalizedDate = targetDate;
            normalizedDate.Set(targetDate.Year, targetDate.Month, targetDate.Day);
            int normalizedMinute = CampusTimeSchedule.NormalizeMinuteOfDay(minuteOfDay);
            if (!CampusTimeSchedule.TryResolveSegmentAtMinute(
                    normalizedMinute,
                    out CampusTimeSegment targetSegment,
                    out float targetElapsedMinutes))
            {
                return;
            }

            CampusGameDate previousDate = currentDate;
            CampusTimeSegment previousSegment = currentSegment;
            int previousDayCount = bootstrap != null && bootstrap.GameState != null
                ? bootstrap.GameState.Day
                : 1;

            currentDate = normalizedDate;
            currentSegment = targetSegment;
            segmentElapsedMinutes = Mathf.Clamp(
                targetElapsedMinutes,
                0f,
                Mathf.Max(0f, SegmentDurationMinutes));

            ApplyAdjustedDayCount(previousDate, previousDayCount, normalizedDate);
            SyncDayNightClock();

            if (!AreSameDate(previousDate, normalizedDate))
            {
                GameDateChanged?.Invoke(currentDate);
            }

            if (previousSegment != currentSegment)
            {
                SegmentChanged?.Invoke(previousSegment, currentSegment);
            }

            if (writeLog)
            {
                WriteTimeAdjustmentLog();
            }
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
            lastActiveCustomTimeScale = Mathf.Clamp(lastActiveCustomTimeScale, 0f, 200f);
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

        private void WriteTimeAdjustmentLog()
        {
            CampusEventLog eventLog = bootstrap != null ? bootstrap.EventLog : null;
            if (eventLog == null)
            {
                return;
            }

            eventLog.AddLog(CampusCoreTextCatalog.Format(
                CampusCoreTextId.TestTimeAdjusted,
                CurrentDateText,
                CurrentClockText,
                CurrentSegmentName));
        }

        private void WriteTimePauseLog()
        {
            CampusEventLog eventLog = bootstrap != null ? bootstrap.EventLog : null;
            if (eventLog == null)
            {
                return;
            }

            eventLog.AddLog(CampusCoreTextCatalog.Get(CampusCoreTextId.TestTimePaused));
        }

        private void WriteTimeResumeLog()
        {
            CampusEventLog eventLog = bootstrap != null ? bootstrap.EventLog : null;
            if (eventLog == null)
            {
                return;
            }

            eventLog.AddLog(CampusCoreTextCatalog.Format(
                CampusCoreTextId.TestTimeResumed,
                CampusGameplayDebugTextCatalog.FormatSpeedMode(
                    CampusLanguageState.CurrentLanguage,
                    speedMode)));
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

        private void ApplyAdjustedDayCount(CampusGameDate previousDate, int previousDayCount, CampusGameDate targetDate)
        {
            if (bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            int dayDelta = (targetDate.ToDateTime() - previousDate.ToDateTime()).Days;
            bootstrap.GameState.SetDay(previousDayCount + dayDelta);
        }

        private void RememberActiveSpeedSettings(CampusTimeSpeedMode mode)
        {
            if (mode == CampusTimeSpeedMode.Paused)
            {
                return;
            }

            lastActiveSpeedMode = mode;
            if (mode == CampusTimeSpeedMode.Custom)
            {
                lastActiveCustomTimeScale = Mathf.Clamp(customTimeScale, 0f, 200f);
            }
        }

        private static bool AreSameDate(CampusGameDate left, CampusGameDate right)
        {
            return left.Year == right.Year &&
                   left.Month == right.Month &&
                   left.Day == right.Day;
        }
    }
}

