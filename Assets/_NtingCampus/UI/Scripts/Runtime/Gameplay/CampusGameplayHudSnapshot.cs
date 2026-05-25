using System;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public readonly struct CampusGameplayHudSnapshot : IEquatable<CampusGameplayHudSnapshot>
    {
        public CampusGameplayHudSnapshot(
            string dateText,
            string weekdayText,
            string segmentText,
            string clockText,
            int suspicionPercent,
            string riskLabel,
            int teacherAlertness,
            int warningCount,
            string areaName,
            string floorLabel,
            string headingLabel,
            string areaSubtitle,
            int money,
            int staminaCurrent,
            int staminaMax,
            string backpackStatus,
            int pendingCheckoutCount,
            int pendingCheckoutTotal,
            bool canAffordCheckout)
        {
            DateText = dateText ?? string.Empty;
            WeekdayText = weekdayText ?? string.Empty;
            SegmentText = segmentText ?? string.Empty;
            ClockText = clockText ?? string.Empty;
            SuspicionPercent = suspicionPercent;
            RiskLabel = riskLabel ?? string.Empty;
            TeacherAlertness = teacherAlertness;
            WarningCount = warningCount;
            AreaName = areaName ?? string.Empty;
            FloorLabel = floorLabel ?? string.Empty;
            HeadingLabel = headingLabel ?? string.Empty;
            AreaSubtitle = areaSubtitle ?? string.Empty;
            Money = money;
            StaminaCurrent = staminaCurrent;
            StaminaMax = staminaMax;
            BackpackStatus = backpackStatus ?? string.Empty;
            PendingCheckoutCount = Math.Max(0, pendingCheckoutCount);
            PendingCheckoutTotal = Math.Max(0, pendingCheckoutTotal);
            CanAffordCheckout = canAffordCheckout;
        }

        public string DateText { get; }
        public string WeekdayText { get; }
        public string SegmentText { get; }
        public string ClockText { get; }
        public int SuspicionPercent { get; }
        public string RiskLabel { get; }
        public int TeacherAlertness { get; }
        public int WarningCount { get; }
        public string AreaName { get; }
        public string FloorLabel { get; }
        public string HeadingLabel { get; }
        public string AreaSubtitle { get; }
        public int Money { get; }
        public int StaminaCurrent { get; }
        public int StaminaMax { get; }
        public string BackpackStatus { get; }
        public int PendingCheckoutCount { get; }
        public int PendingCheckoutTotal { get; }
        public bool CanAffordCheckout { get; }
        public bool ShowPendingCheckout => PendingCheckoutCount > 0;

        public bool Equals(CampusGameplayHudSnapshot other)
        {
            return DateText == other.DateText &&
                   WeekdayText == other.WeekdayText &&
                   SegmentText == other.SegmentText &&
                   ClockText == other.ClockText &&
                   SuspicionPercent == other.SuspicionPercent &&
                   RiskLabel == other.RiskLabel &&
                   TeacherAlertness == other.TeacherAlertness &&
                   WarningCount == other.WarningCount &&
                   AreaName == other.AreaName &&
                   FloorLabel == other.FloorLabel &&
                   HeadingLabel == other.HeadingLabel &&
                   AreaSubtitle == other.AreaSubtitle &&
                   Money == other.Money &&
                   StaminaCurrent == other.StaminaCurrent &&
                   StaminaMax == other.StaminaMax &&
                   BackpackStatus == other.BackpackStatus &&
                   PendingCheckoutCount == other.PendingCheckoutCount &&
                   PendingCheckoutTotal == other.PendingCheckoutTotal &&
                   CanAffordCheckout == other.CanAffordCheckout;
        }

        public override bool Equals(object obj)
        {
            return obj is CampusGameplayHudSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = DateText.GetHashCode();
                hashCode = (hashCode * 397) ^ WeekdayText.GetHashCode();
                hashCode = (hashCode * 397) ^ SegmentText.GetHashCode();
                hashCode = (hashCode * 397) ^ ClockText.GetHashCode();
                hashCode = (hashCode * 397) ^ SuspicionPercent;
                hashCode = (hashCode * 397) ^ RiskLabel.GetHashCode();
                hashCode = (hashCode * 397) ^ TeacherAlertness;
                hashCode = (hashCode * 397) ^ WarningCount;
                hashCode = (hashCode * 397) ^ AreaName.GetHashCode();
                hashCode = (hashCode * 397) ^ FloorLabel.GetHashCode();
                hashCode = (hashCode * 397) ^ HeadingLabel.GetHashCode();
                hashCode = (hashCode * 397) ^ AreaSubtitle.GetHashCode();
                hashCode = (hashCode * 397) ^ Money;
                hashCode = (hashCode * 397) ^ StaminaCurrent;
                hashCode = (hashCode * 397) ^ StaminaMax;
                hashCode = (hashCode * 397) ^ BackpackStatus.GetHashCode();
                hashCode = (hashCode * 397) ^ PendingCheckoutCount;
                hashCode = (hashCode * 397) ^ PendingCheckoutTotal;
                hashCode = (hashCode * 397) ^ CanAffordCheckout.GetHashCode();
                return hashCode;
            }
        }
    }
}
