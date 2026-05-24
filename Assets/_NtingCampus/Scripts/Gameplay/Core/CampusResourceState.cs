using System;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    [Serializable]
    public sealed class CampusResourceState
    {
        [SerializeField, Min(0)] private int divinePower;

        public event Action<int> DivinePowerChanged;

        public int DivinePower => divinePower;

        public void Reset(int initialDivinePower)
        {
            SetDivinePower(Mathf.Max(0, initialDivinePower));
        }

        public void AddDivinePower(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SetDivinePower(AddClamped(divinePower, amount));
        }

        public bool SpendDivinePower(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (divinePower < amount)
            {
                return false;
            }

            SetDivinePower(divinePower - amount);
            return true;
        }

        private void SetDivinePower(int value)
        {
            int normalizedValue = Mathf.Max(0, value);
            if (divinePower == normalizedValue)
            {
                return;
            }

            divinePower = normalizedValue;
            DivinePowerChanged?.Invoke(divinePower);
        }

        private static int AddClamped(int current, int amount)
        {
            if (amount > int.MaxValue - current)
            {
                return int.MaxValue;
            }

            return current + amount;
        }
    }
}
