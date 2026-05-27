using System;
using System.Collections.Generic;
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
        [SerializeField, Range(StatMin, StatMax)] private int playerTheftEvidence;
        [SerializeField, Range(StatMin, StatMax)] private int playerTheftRecord;
        [SerializeField, Range(StatMin, StatMax)] private int campusRumor;
        [SerializeField, Range(StatMin, StatMax)] private int campusCrackdown;
        [SerializeField, Min(0)] private int dailyWarningCount;
        [SerializeField] private bool shrineRoomUnlocked;
        [SerializeField] private bool landExpansionUnlocked;
        [SerializeField] private List<CampusAreaRuntimeState> areaStates =
            new List<CampusAreaRuntimeState>();

        public CampusGameMode CurrentMode => currentMode;
        public int Day => day;
        public int CampusOrder => campusOrder;
        public int CampusChaos => campusChaos;
        public int TeacherAlertness => teacherAlertness;
        public int DivineInterest => divineInterest;
        public int PlayerSuspicion => playerSuspicion;
        public int PlayerTheftEvidence => playerTheftEvidence;
        public int PlayerTheftRecord => playerTheftRecord;
        public int CampusRumor => campusRumor;
        public int CampusCrackdown => campusCrackdown;
        public int DailyWarningCount => dailyWarningCount;
        public bool ShrineRoomUnlocked => shrineRoomUnlocked;
        public bool LandExpansionUnlocked => landExpansionUnlocked;
        public IReadOnlyList<CampusAreaRuntimeState> AreaStates => areaStates;

        public void Reset(CampusGameStateInitialization initialization)
        {
            currentMode = CampusGameMode.StudentBody;
            day = Mathf.Max(1, initialization.InitialDay);
            campusOrder = ClampStat(initialization.InitialCampusOrder);
            campusChaos = ClampStat(initialization.InitialCampusChaos);
            teacherAlertness = ClampStat(initialization.InitialTeacherAlertness);
            divineInterest = ClampStat(initialization.InitialDivineInterest);
            playerSuspicion = ClampStat(initialization.InitialPlayerSuspicion);
            playerTheftEvidence = ClampStat(initialization.InitialPlayerTheftEvidence);
            playerTheftRecord = ClampStat(initialization.InitialPlayerTheftRecord);
            campusRumor = ClampStat(initialization.InitialCampusRumor);
            campusCrackdown = ClampStat(initialization.InitialCampusCrackdown);
            dailyWarningCount = Mathf.Max(0, initialization.InitialDailyWarningCount);
            shrineRoomUnlocked = initialization.InitialShrineRoomUnlocked;
            landExpansionUnlocked = initialization.InitialLandExpansionUnlocked;
            areaStates.Clear();
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

        public void SetPlayerTheftEvidence(int value)
        {
            playerTheftEvidence = ClampStat(value);
        }

        public void AddPlayerTheftEvidence(int delta)
        {
            SetPlayerTheftEvidence(playerTheftEvidence + delta);
        }

        public void SetPlayerTheftRecord(int value)
        {
            playerTheftRecord = ClampStat(value);
        }

        public void AddPlayerTheftRecord(int delta)
        {
            SetPlayerTheftRecord(playerTheftRecord + delta);
        }

        public void SetCampusRumor(int value)
        {
            campusRumor = ClampStat(value);
        }

        public void AddCampusRumor(int delta)
        {
            SetCampusRumor(campusRumor + delta);
        }

        public void SetCampusCrackdown(int value)
        {
            campusCrackdown = ClampStat(value);
        }

        public void AddCampusCrackdown(int delta)
        {
            SetCampusCrackdown(campusCrackdown + delta);
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

        public CampusAreaRuntimeState GetOrCreateAreaState(string roomId)
        {
            string normalizedRoomId = NormalizeId(roomId);
            if (string.IsNullOrEmpty(normalizedRoomId))
            {
                normalizedRoomId = "unknown";
            }

            for (int i = 0; i < areaStates.Count; i++)
            {
                CampusAreaRuntimeState existing = areaStates[i];
                if (existing != null &&
                    string.Equals(existing.RoomId, normalizedRoomId, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            CampusAreaRuntimeState created = new CampusAreaRuntimeState(normalizedRoomId);
            areaStates.Add(created);
            return created;
        }

        public void ApplyAreaDelta(
            string roomId,
            int alertDelta,
            int bagCheckDelta,
            int patrolDelta,
            bool lockHighValueGoods,
            bool moveDeliverySpot)
        {
            if (alertDelta == 0 &&
                bagCheckDelta == 0 &&
                patrolDelta == 0 &&
                !lockHighValueGoods &&
                !moveDeliverySpot)
            {
                return;
            }

            CampusAreaRuntimeState state = GetOrCreateAreaState(roomId);
            state.AddAlert(alertDelta);
            state.AddBagCheck(bagCheckDelta);
            state.AddPatrolPressure(patrolDelta);
            if (lockHighValueGoods)
            {
                state.SetHighValueGoodsLocked(true);
            }

            if (moveDeliverySpot)
            {
                state.SetDeliverySpotMoved(true);
            }
        }

        public void DecayAreaStates(int amount)
        {
            int decay = Mathf.Max(0, amount);
            if (decay == 0)
            {
                return;
            }

            for (int i = 0; i < areaStates.Count; i++)
            {
                areaStates[i]?.Decay(decay);
            }
        }

        private static int ClampStat(int value)
        {
            return Mathf.Clamp(value, StatMin, StatMax);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [Serializable]
    public sealed class CampusAreaRuntimeState
    {
        [SerializeField] private string roomId;
        [SerializeField, Range(CampusGameState.StatMin, CampusGameState.StatMax)] private int alertLevel;
        [SerializeField, Range(CampusGameState.StatMin, CampusGameState.StatMax)] private int bagCheckLevel;
        [SerializeField, Range(CampusGameState.StatMin, CampusGameState.StatMax)] private int patrolPressure;
        [SerializeField] private bool highValueGoodsLocked;
        [SerializeField] private bool deliverySpotMoved;

        public CampusAreaRuntimeState(string roomId)
        {
            this.roomId = string.IsNullOrWhiteSpace(roomId) ? "unknown" : roomId.Trim();
        }

        public string RoomId => roomId;
        public int AlertLevel => alertLevel;
        public int BagCheckLevel => bagCheckLevel;
        public int PatrolPressure => patrolPressure;
        public bool HighValueGoodsLocked => highValueGoodsLocked;
        public bool DeliverySpotMoved => deliverySpotMoved;

        public void AddAlert(int delta)
        {
            alertLevel = Clamp(alertLevel + delta);
        }

        public void AddBagCheck(int delta)
        {
            bagCheckLevel = Clamp(bagCheckLevel + delta);
        }

        public void AddPatrolPressure(int delta)
        {
            patrolPressure = Clamp(patrolPressure + delta);
        }

        public void SetHighValueGoodsLocked(bool locked)
        {
            highValueGoodsLocked = locked;
        }

        public void SetDeliverySpotMoved(bool moved)
        {
            deliverySpotMoved = moved;
        }

        public void Decay(int amount)
        {
            int decay = Mathf.Max(0, amount);
            alertLevel = Clamp(alertLevel - decay);
            bagCheckLevel = Clamp(bagCheckLevel - decay);
            patrolPressure = Clamp(patrolPressure - decay);
            if (alertLevel == 0 && bagCheckLevel == 0 && patrolPressure == 0)
            {
                highValueGoodsLocked = false;
                deliverySpotMoved = false;
            }
        }

        private static int Clamp(int value)
        {
            return Mathf.Clamp(value, CampusGameState.StatMin, CampusGameState.StatMax);
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
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialPlayerTheftEvidence;
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialPlayerTheftRecord;
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialCampusRumor;
        [Range(CampusGameState.StatMin, CampusGameState.StatMax)] public int InitialCampusCrackdown;
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
                InitialPlayerTheftEvidence = 0,
                InitialPlayerTheftRecord = 0,
                InitialCampusRumor = 0,
                InitialCampusCrackdown = 0,
                InitialDailyWarningCount = 0,
                InitialShrineRoomUnlocked = false,
                InitialLandExpansionUnlocked = false
            };
        }
    }
}
