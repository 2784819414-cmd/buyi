using System;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    /// <summary>
    /// 保存玩法核心的基础资源状态。
    /// </summary>
    [Serializable]
    public sealed class CampusResourceState
    {
        [SerializeField, Min(0)] private int money = 500;
        [SerializeField, Min(0)] private int divinePower;

        /// <summary>
        /// 当前金钱。
        /// </summary>
        public int Money => money;

        /// <summary>
        /// 当前神力。
        /// </summary>
        public int DivinePower => divinePower;

        /// <summary>
        /// 使用指定数值重置金钱和神力。
        /// </summary>
        public void Reset(int initialMoney, int initialDivinePower)
        {
            money = Mathf.Max(0, initialMoney);
            divinePower = Mathf.Max(0, initialDivinePower);
        }

        /// <summary>
        /// 增加金钱，非正数会被忽略。
        /// </summary>
        public void AddMoney(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            money = AddClamped(money, amount);
        }

        /// <summary>
        /// 尝试扣除金钱，余额不足时返回 false。
        /// </summary>
        public bool SpendMoney(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (money < amount)
            {
                return false;
            }

            money -= amount;
            return true;
        }

        /// <summary>
        /// 增加神力，非正数会被忽略。
        /// </summary>
        public void AddDivinePower(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            divinePower = AddClamped(divinePower, amount);
        }

        /// <summary>
        /// 尝试扣除神力，余额不足时返回 false。
        /// </summary>
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

            divinePower -= amount;
            return true;
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
