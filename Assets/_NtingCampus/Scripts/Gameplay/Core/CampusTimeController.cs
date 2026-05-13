using System;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    /// <summary>
    /// 管理游戏日期、校园时段和时段内计时。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampusTimeController : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusGameDate initialDate = CampusGameDate.DefaultStartDate;
        [SerializeField] private CampusGameDate currentDate = CampusGameDate.DefaultStartDate;
        [SerializeField] private CampusTimeSegment currentSegment = CampusTimeSegment.WakeUp;
        [SerializeField, Min(0f)] private float segmentElapsedMinutes;
        [SerializeField] private bool autoAdvance = true;
        [SerializeField] private bool syncWithDayNightSpeed = true;
        [SerializeField, Min(0f)] private float fallbackTimeScale = 1f;
        [SerializeField] private CampusDayNightController dayNightController;

        private bool isInitialized;

        /// <summary>
        /// 时段变化事件。
        /// </summary>
        public event Action<CampusTimeSegment, CampusTimeSegment> SegmentChanged;

        /// <summary>
        /// 每日结算开始事件。
        /// </summary>
        public event Action<CampusGameDate> DailySettlementStarted;

        /// <summary>
        /// 游戏日期变化事件。
        /// </summary>
        public event Action<CampusGameDate> GameDateChanged;

        /// <summary>
        /// 当前游戏日期。
        /// </summary>
        public CampusGameDate CurrentDate => currentDate;

        /// <summary>
        /// 当前游戏日期显示文本。
        /// </summary>
        public string CurrentDateText => currentDate.ToDisplayString();

        /// <summary>
        /// 当前校园时段。
        /// </summary>
        public CampusTimeSegment CurrentSegment => currentSegment;

        /// <summary>
        /// 当前校园时段中文名。
        /// </summary>
        public string CurrentSegmentName => CampusTimeSchedule.GetChineseName(currentSegment);

        /// <summary>
        /// 当前校园时段时间标签。
        /// </summary>
        public string CurrentTimeLabel => CampusTimeSchedule.GetTimeLabel(currentSegment);

        /// <summary>
        /// 当前校内时间文本。
        /// </summary>
        public string CurrentClockText => CampusTimeSchedule.GetClockText(currentSegment, segmentElapsedMinutes);

        /// <summary>
        /// 当前时段内已推进的分钟数。
        /// </summary>
        public float SegmentElapsedMinutes => segmentElapsedMinutes;

        /// <summary>
        /// 当前时段总分钟数。
        /// </summary>
        public float SegmentDurationMinutes => CampusTimeSchedule.GetDurationMinutes(currentSegment);

        /// <summary>
        /// 当前时段进度。
        /// </summary>
        public float SegmentProgress01 => SegmentDurationMinutes <= 0f ? 1f : Mathf.Clamp01(segmentElapsedMinutes / SegmentDurationMinutes);

        /// <summary>
        /// 当前时间倍率。
        /// </summary>
        public float TimeScale => ResolveTimeScale();

        /// <summary>
        /// 当前是否处于夜间自由行动阶段。
        /// </summary>
        public bool IsNightFree => currentSegment == CampusTimeSegment.NightFree;

        /// <summary>
        /// 当前是否允许夜间自由行动玩法接入。
        /// </summary>
        public bool AllowsNightFreeAction => IsNightFree;

        /// <summary>
        /// 绑定玩法入口并重置到开局日期和起床时段。
        /// </summary>
        public void InitializeTimeSystem(CampusGameBootstrap owner, bool writeInitialSegmentLog)
        {
            bootstrap = owner;
            ResolveReferences();
            NormalizeDates();
            currentDate = initialDate;
            currentSegment = CampusTimeSegment.WakeUp;
            segmentElapsedMinutes = 0f;
            isInitialized = true;

            if (writeInitialSegmentLog)
            {
                WriteSegmentLog(currentSegment);
            }
        }

        /// <summary>
        /// 推进指定游戏分钟数。
        /// </summary>
        public void AdvanceMinutes(float minutes)
        {
            if (minutes <= 0f)
            {
                return;
            }

            EnsureInitialized();

            int guard = 0;
            float remainingMinutes = minutes;
            while (remainingMinutes > 0f && guard < 128)
            {
                guard++;
                float duration = Mathf.Max(0.01f, SegmentDurationMinutes);
                float remainingInSegment = Mathf.Max(0f, duration - segmentElapsedMinutes);
                if (remainingMinutes < remainingInSegment)
                {
                    segmentElapsedMinutes += remainingMinutes;
                    return;
                }

                remainingMinutes -= remainingInSegment;
                CompleteCurrentSegment();
            }
        }

        /// <summary>
        /// 直接切换到指定时段。
        /// </summary>
        public void SetSegment(CampusTimeSegment segment, bool writeLog)
        {
            EnsureInitialized();

            if (currentSegment == segment)
            {
                segmentElapsedMinutes = 0f;
                return;
            }

            CampusTimeSegment previousSegment = currentSegment;
            currentSegment = segment;
            segmentElapsedMinutes = 0f;
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
            fallbackTimeScale = Mathf.Max(0f, fallbackTimeScale);
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
            if (syncWithDayNightSpeed && dayNightController == null)
            {
                dayNightController = FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }
        }

        private float ResolveTimeScale()
        {
            if (syncWithDayNightSpeed)
            {
                if (dayNightController == null)
                {
                    dayNightController = FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
                }

                if (dayNightController != null)
                {
                    return Mathf.Max(0f, dayNightController.DaySpeedMultiplier);
                }
            }

            return Mathf.Max(0f, fallbackTimeScale);
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

            if (currentSegment == CampusTimeSegment.PreWakeSettlement)
            {
                currentDate.AddDays(1);
                GameDateChanged?.Invoke(currentDate);
            }

            currentSegment = nextSegment;
            segmentElapsedMinutes = 0f;
            SegmentChanged?.Invoke(previousSegment, currentSegment);

            if (currentSegment == CampusTimeSegment.PreWakeSettlement)
            {
                DailySettlementStarted?.Invoke(currentDate);
            }

            WriteSegmentLog(currentSegment);
        }

        private void WriteSegmentLog(CampusTimeSegment segment)
        {
            CampusEventLog eventLog = bootstrap != null ? bootstrap.EventLog : null;
            if (eventLog == null)
            {
                return;
            }

            eventLog.AddLog(CampusTimeSchedule.GetSegmentLogMessage(segment, CurrentDateText));
        }
    }
}
