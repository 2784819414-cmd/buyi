using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    /// <summary>
    /// Play Mode 下显示玩法核心状态的 IMGUI 调试面板。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampusGameplayDebugPanel : MonoBehaviour
    {
        private static readonly Rect PanelRect = new Rect(10f, 10f, 360f, 238f);

        [SerializeField] private CampusGameBootstrap bootstrap;

        /// <summary>
        /// 绑定玩法入口，供调试面板读取状态。
        /// </summary>
        public void Bind(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap;
        }

        private void Awake()
        {
            ResolveBootstrap();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            CampusGameBootstrap targetBootstrap = ResolveBootstrap();
            if (targetBootstrap == null)
            {
                return;
            }

            CampusTimeController timeController = targetBootstrap.TimeController;
            CampusResourceState resourceState = targetBootstrap.ResourceState;
            CampusEventLog eventLog = targetBootstrap.EventLog;

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("GameDate: " + (timeController != null ? timeController.CurrentDateText : "-"));
            GUILayout.Label("Time: " + (timeController != null ? timeController.CurrentClockText : "-"));
            GUILayout.Label("Segment: " + (timeController != null ? timeController.CurrentSegmentName : "-"));
            GUILayout.Label("Schedule: " + (timeController != null ? timeController.CurrentTimeLabel : "-"));
            GUILayout.Label("Money: " + (resourceState != null ? resourceState.Money.ToString() : "-"));
            GUILayout.Label("DivinePower: " + (resourceState != null ? resourceState.DivinePower.ToString() : "-"));
            GUILayout.Label("TimeScale: " + (timeController != null ? timeController.TimeScale.ToString("0.##") + "x" : "-"));
            GUILayout.Space(4f);
            GUILayout.Label("最近事件日志");
            DrawRecentLogs(eventLog);
            GUILayout.EndArea();
        }

        private CampusGameBootstrap ResolveBootstrap()
        {
            if (bootstrap != null)
            {
                return bootstrap;
            }

            bootstrap = GetComponent<CampusGameBootstrap>();
            if (bootstrap != null)
            {
                return bootstrap;
            }

            bootstrap = CampusGameBootstrap.Instance;
            return bootstrap;
        }

        private static void DrawRecentLogs(CampusEventLog eventLog)
        {
            IReadOnlyList<string> entries = eventLog != null ? eventLog.Entries : null;
            int entryCount = entries != null ? entries.Count : 0;
            if (entryCount == 0)
            {
                GUILayout.Label("- 无");
                return;
            }

            int startIndex = Mathf.Max(0, entryCount - 5);
            for (int i = startIndex; i < entryCount; i++)
            {
                GUILayout.Label("- " + entries[i]);
            }
        }
    }
}
