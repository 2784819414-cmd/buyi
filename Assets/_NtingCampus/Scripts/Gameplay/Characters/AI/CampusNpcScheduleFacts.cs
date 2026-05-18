using NtingCampus.Gameplay.Core;

namespace NtingCampus.Gameplay.Characters
{
    public static class CampusNpcScheduleFacts
    {
        public static bool IsClassSession(CampusTimeSegment segment)
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

        public static bool IsBreak(CampusTimeSegment segment)
        {
            switch (segment)
            {
                case CampusTimeSegment.MorningBreak1:
                case CampusTimeSegment.MorningExerciseBreak:
                case CampusTimeSegment.MorningBreak2:
                case CampusTimeSegment.AfternoonBreak1:
                case CampusTimeSegment.AfternoonBreak2:
                case CampusTimeSegment.AfternoonBreak3:
                case CampusTimeSegment.EveningBreak1:
                case CampusTimeSegment.EveningBreak2:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMealPeak(CampusTimeSegment segment)
        {
            return segment == CampusTimeSegment.BreakfastPrep ||
                   segment == CampusTimeSegment.LunchBreak ||
                   segment == CampusTimeSegment.DinnerBreak;
        }

        public static bool IsStudentDeliveryOrderWindow(CampusTimeSegment segment)
        {
            return segment == CampusTimeSegment.MorningBreak2 ||
                   segment == CampusTimeSegment.LunchBreak ||
                   segment == CampusTimeSegment.AfternoonBreak2 ||
                   segment == CampusTimeSegment.DinnerBreak ||
                   segment == CampusTimeSegment.EveningBreak1;
        }

        public static bool IsStudentFreeMovementWindow(CampusTimeSegment segment)
        {
            return IsBreak(segment) ||
                   IsMealPeak(segment) ||
                   segment == CampusTimeSegment.WakeUp ||
                   segment == CampusTimeSegment.DormReturn ||
                   segment == CampusTimeSegment.NightFree;
        }

        public static bool IsDormWindow(CampusTimeSegment segment)
        {
            return segment == CampusTimeSegment.DormReturn ||
                   segment == CampusTimeSegment.DormCheck ||
                   segment == CampusTimeSegment.LightsOut ||
                   segment == CampusTimeSegment.NightFree ||
                   segment == CampusTimeSegment.PreWakeSettlement ||
                   segment == CampusTimeSegment.WakeUp;
        }

        public static bool IsTeacherOfficeWindow(CampusTimeSegment segment)
        {
            return !IsClassSession(segment) && !IsDormWindow(segment);
        }

        public static bool IsCanteenShiftActive(CampusTimeSegment segment)
        {
            return segment == CampusTimeSegment.BreakfastPrep ||
                   segment == CampusTimeSegment.MorningBreak2 ||
                   segment == CampusTimeSegment.LunchBreak ||
                   segment == CampusTimeSegment.AfternoonBreak2 ||
                   segment == CampusTimeSegment.DinnerBreak;
        }

        public static bool IsStoreOpen(CampusTimeSegment segment)
        {
            return IsBreak(segment) ||
                   segment == CampusTimeSegment.LunchBreak ||
                   segment == CampusTimeSegment.DinnerBreak ||
                   segment == CampusTimeSegment.EveningBreak1 ||
                   segment == CampusTimeSegment.EveningBreak2;
        }

        public static bool IsStoreStocktakeWindow(CampusTimeSegment segment)
        {
            return segment == CampusTimeSegment.EveningStudy1 ||
                   segment == CampusTimeSegment.EveningStudy2 ||
                   segment == CampusTimeSegment.EveningStudy3;
        }

        public static bool IsStaffOffDuty(CampusTimeSegment segment)
        {
            return segment == CampusTimeSegment.LightsOut ||
                   segment == CampusTimeSegment.NightFree ||
                   segment == CampusTimeSegment.PreWakeSettlement;
        }
    }
}
