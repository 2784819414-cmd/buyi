using System;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    [Serializable]
    public sealed class CampusCharacterEconomyState
    {
        [SerializeField, Min(0)] private int money;

        public event Action<int> MoneyChanged;

        public int Money => money;

        public void Reset(int initialMoney)
        {
            SetMoney(Mathf.Max(0, initialMoney));
        }

        public void SetMoney(int value)
        {
            int normalizedValue = Mathf.Max(0, value);
            if (money == normalizedValue)
            {
                return;
            }

            money = normalizedValue;
            MoneyChanged?.Invoke(money);
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SetMoney(amount > int.MaxValue - money
                ? int.MaxValue
                : money + amount);
        }

        public bool TrySpendMoney(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (money < amount)
            {
                return false;
            }

            SetMoney(money - amount);
            return true;
        }
    }

    internal static class CampusCharacterEconomyDefaults
    {
        public const int UseRoleDefaultMoney = -1;

        public static int ResolveInitialMoney(
            NtingCampus.Gameplay.Characters.CampusCharacterRole role,
            bool isPlayerControlled,
            int configuredInitialMoney)
        {
            if (configuredInitialMoney >= 0)
            {
                return configuredInitialMoney;
            }

            if (isPlayerControlled)
            {
                return 500;
            }

            return role switch
            {
                NtingCampus.Gameplay.Characters.CampusCharacterRole.Teacher => 160,
                NtingCampus.Gameplay.Characters.CampusCharacterRole.Staff => 120,
                _ => 60
            };
        }
    }
}
