using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    /// <summary>
    /// 保存玩法核心事件日志。
    /// </summary>
    [Serializable]
    public sealed class CampusEventLog
    {
        private const int MaxEntryCount = 50;

        [SerializeField] private List<string> entries = new List<string>(MaxEntryCount);

        /// <summary>
        /// 当前保留的事件日志。
        /// </summary>
        public IReadOnlyList<string> Entries
        {
            get
            {
                EnsureEntries();
                return entries;
            }
        }

        /// <summary>
        /// 最多保留的事件数量。
        /// </summary>
        public int MaxCount => MaxEntryCount;

        /// <summary>
        /// 添加一条事件日志，并只保留最近 50 条。
        /// </summary>
        public void AddLog(string message)
        {
            EnsureEntries();

            string normalizedMessage = string.IsNullOrWhiteSpace(message)
                ? "(空事件)"
                : message.Trim();

            entries.Add(normalizedMessage);
            TrimOldEntries();
        }

        private void EnsureEntries()
        {
            if (entries == null)
            {
                entries = new List<string>(MaxEntryCount);
            }
        }

        private void TrimOldEntries()
        {
            int extraCount = entries.Count - MaxEntryCount;
            if (extraCount > 0)
            {
                entries.RemoveRange(0, extraCount);
            }
        }
    }
}
