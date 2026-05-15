using System;
using UnityEngine;

namespace NtingCampus.Gameplay.Skeleton
{
    public static class CampusMischiefAreaNames
    {
        public const string CampusShop = "CampusShop";
        public const string Canteen = "Canteen";
        public const string Library = "Library";
        public const string OutsideDelivery = "OutsideDelivery";
        public const string Classroom = "Classroom";
        public const string SkewerStand = "SkewerStand";
    }

    [Serializable]
    public sealed class CampusMischiefAreaState
    {
        public const int SensitiveSuspicionThreshold = 20;

        public string AreaName;
        [Min(0)] public int Suspicion;
        [Min(0)] public int AlertLevel;
        public string LastIncidentName;
        public bool IsTemporarilyHot;

        public CampusMischiefAreaState(string areaName)
        {
            AreaName = NormalizeAreaName(areaName);
        }

        public void RegisterIncident(string incidentName, int suspicionGain)
        {
            LastIncidentName = string.IsNullOrWhiteSpace(incidentName) ? "-" : incidentName.Trim();
            AddSuspicion(suspicionGain);
        }

        public void AddSuspicion(int amount)
        {
            if (amount <= 0)
            {
                RefreshRiskFlags();
                return;
            }

            Suspicion = AddClamped(Suspicion, amount);
            RefreshRiskFlags();
        }

        public void RefreshRiskFlags()
        {
            AlertLevel = ResolveAlertLevel(Suspicion);
            IsTemporarilyHot = Suspicion >= SensitiveSuspicionThreshold;
        }

        private static int ResolveAlertLevel(int suspicion)
        {
            if (suspicion >= 40)
            {
                return 3;
            }

            if (suspicion >= SensitiveSuspicionThreshold)
            {
                return 2;
            }

            return suspicion >= 10 ? 1 : 0;
        }

        private static int AddClamped(int current, int amount)
        {
            if (amount > int.MaxValue - current)
            {
                return int.MaxValue;
            }

            return current + amount;
        }

        private static string NormalizeAreaName(string areaName)
        {
            return string.IsNullOrWhiteSpace(areaName) ? "Unknown" : areaName.Trim();
        }
    }
}
