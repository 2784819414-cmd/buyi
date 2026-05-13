using System;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    /// <summary>
    /// 保存玩法核心的基础进度状态。
    /// </summary>
    [Serializable]
    public sealed class CampusGameState
    {
        [SerializeField, Min(1)] private int day = 1;

        /// <summary>
        /// 当前游戏天数。
        /// </summary>
        public int Day => day;

        /// <summary>
        /// 使用指定天数重置当前游戏天数。
        /// </summary>
        public void Reset(int initialDay)
        {
            day = Mathf.Max(1, initialDay);
        }

        /// <summary>
        /// 设置当前游戏天数，最小为 1。
        /// </summary>
        public void SetDay(int value)
        {
            day = Mathf.Max(1, value);
        }
    }
}
