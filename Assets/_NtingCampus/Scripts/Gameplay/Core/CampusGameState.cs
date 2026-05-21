using System;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    [Serializable]
    public sealed class CampusGameState
    {
        public const int StatMin = 0;
        public const int StatMax = 100;

        [SerializeField] private CampusGameMode currentMode = CampusGameMode.StudentBody;
        [SerializeField, Min(1)] private int day = 1;
        [SerializeField, Range(StatMin, StatMax)] private int campusOrder = 70;
        [SerializeField, Range(StatMin, StatMax)] private int campusChaos = 20;
        [SerializeField, Range(StatMin, StatMax)] private int teacherAlertness = 15;
        [SerializeField, Range(StatMin, StatMax)] private int divineInterest = 25;
        [SerializeField, Range(StatMin, StatMax)] private int playerSuspicion;
        [SerializeField, Min(0)] private int dailyWarningCount;
        [SerializeField] private bool shrineRoomUnlocked;
        [SerializeField] private bool landExpansionUnlocked;

        public CampusGameMode CurrentMode => currentMode;
        public int Day => day;
        public int CampusOrder => campusOrder;
        public int CampusChaos => campusChaos;
        public int TeacherAlertness => teacherAlertness;
        public int DivineInterest => divineInterest;
        public int PlayerSuspicion => playerSuspicion;
        public int DailyWarningCount => dailyWarningCount;
        public bool ShrineRoomUnlocked => shrineRoomUnlocked;
        public bool LandExpansionUnlocked => landExpansionUnlocked;

        public void Reset(CampusGameStateInitialization initialization)
        {
            currentMode = CampusGameMode.StudentBody;
            day = Mathf.Max(1, initialization.InitialDay);
            campusOrder = ClampStat(initialization.InitialCampusOrder);
            campusChaos = ClampStat(initialization.InitialCampusChaos);
            teacherAlertness = ClampStat(initialization.InitialTeacherAlertness);
            divineInterest = ClampStat(initialization.InitialDivineInterest);
            playerSuspicion = ClampStat(initialization.InitialPlayerSuspicion);
            dailyWarningCount = Mathf.Max(0, initialization.InitialDailyWarningCount);
            shrineRoomUnlocked = initialization.InitialShrineRoomUnlocked;
            landExpansionUnlocked = initialization.InitialLandExpansionUnlocked;
        }

        public void SetMode(CampusGameMode mode)
        {
            currentMode = mode;
        }

        public void SetDay(int value)
        {
            day = Mathf.Max(1, value);
        }

        public void AdvanceToNextDay()
        {
            day = day >= int.MaxValue ? int.MaxValue : day + 1;
            dailyWarningCount = 0;
        }

        public void SetCampusOrder(int value)
        {
            campusOrder = ClampStat(value);
        }

        public void AddCampusOrder(int delta)
        {
            SetCampusOrder(campusOrder + delta);
        }

        public void SetCampusChaos(int value)
        {
            campusChaos = ClampStat(value);
        }

        public void AddCampusChaos(int delta)
        {
            SetCampusChaos(campusChaos + delta);
        }

        public void SetTeacherAlertness(int value)
        {
            teacherAlertness = ClampStat(value);
        }

        public void AddTeacherAlertness(int delta)
        {
            SetTeacherAlertness(teacherAlertness + delta);
        }

        public void SetDivineInterest(int value)
        {
            divineInterest = ClampStat(value);
        }

        public void AddDivineInterest(int delta)
        {
            SetDivineInterest(divineInterest + delta);
        }

        public void SetPlayerSuspicion(int value)
        {
            playerSuspicion = ClampStat(value);
        }

        public void AddPlayerSuspicion(int delta)
        {
            SetPlayerSuspicion(playerSuspicion + delta);
        }

        public void SetDailyWarningCount(int value)
        {
            dailyWarningCount = Mathf.Max(0, value);
        }

        public void AddDailyWarningCount(int delta)
        {
            dailyWarningCount = Mathf.Max(0, dailyWarningCount + delta);
        }

        public void UnlockShrineRoom()
        {
            shrineRoomUnlocked = true;
        }

        public void SetShrineRoomUnlocked(bool unlocked)
        {
            shrineRoomUnlocked = unlocked;
        }

        public void UnlockLandExpansion()
        {
            landExpansionUnlocked = true;
        }

        public void SetLandExpansionUnlocked(bool unlocked)
        {
            landExpansionUnlocked = unlocked;
        }

        private static int ClampStat(int value)
        {
            return Mathf.Clamp(value, StatMin, StatMax);
        }
    }

    [Serializable]
    public struct CampusGameStateInitialization
    {
        [Min(1)] public int InitialDay;
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialCampusOrder;
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialCampusChaos;
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialTeacherAlertness;
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialDivineInterest;
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialPlayerSuspicion;
        [Min(0)] public int InitialDailyWarningCount;
        public bool InitialShrineRoomUnlocked;
        public bool InitialLandExpansionUnlocked;

        public static CampusGameStateInitialization CreateDefault(int initialDay)
        {
            return new CampusGameStateInitialization
            {
                InitialDay = Mathf.Max(1, initialDay),
                InitialCampusOrder = 70,
                InitialCampusChaos = 20,
                InitialTeacherAlertness = 15,
                InitialDivineInterest = 25,
                InitialPlayerSuspicion = 0,
                InitialDailyWarningCount = 0,
                InitialShrineRoomUnlocked = false,
                InitialLandExpansionUnlocked = false
            };
        }
    }
}
