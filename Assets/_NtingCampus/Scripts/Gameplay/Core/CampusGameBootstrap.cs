using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    /// <summary>
    /// 《不义校园》V0.1 玩法核心总入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampusGameBootstrap : MonoBehaviour
    {
        [SerializeField, Min(1)] private int initialDay = 1;
        [SerializeField, Min(0)] private int initialMoney = 500;
        [SerializeField, Min(0)] private int initialDivinePower;
        [SerializeField] private bool showDebugPanel = true;
        [SerializeField] private CampusGameState gameState = new CampusGameState();
        [SerializeField] private CampusResourceState resourceState = new CampusResourceState();
        [SerializeField] private CampusEventLog eventLog = new CampusEventLog();

        /// <summary>
        /// 当前场景中的玩法入口实例。
        /// </summary>
        public static CampusGameBootstrap Instance { get; private set; }

        /// <summary>
        /// 当前游戏基础状态。
        /// </summary>
        public CampusGameState GameState => gameState;

        /// <summary>
        /// 当前资源状态。
        /// </summary>
        public CampusResourceState ResourceState => resourceState;

        /// <summary>
        /// 当前事件日志。
        /// </summary>
        public CampusEventLog EventLog => eventLog;

        /// <summary>
        /// 使用初始配置重置玩法状态，并写入初始化日志。
        /// </summary>
        public void InitializeGameplay()
        {
            gameState = new CampusGameState();
            gameState.Reset(initialDay);

            resourceState = new CampusResourceState();
            resourceState.Reset(initialMoney, initialDivinePower);

            eventLog = new CampusEventLog();
            eventLog.AddLog("Gameplay Core 初始化完成：Day=" + gameState.Day +
                            ", Money=" + resourceState.Money +
                            ", DivinePower=" + resourceState.DivinePower);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("场景中存在多个 CampusGameBootstrap，当前实例不会重新初始化玩法状态。");
                return;
            }

            Instance = this;
            InitializeGameplay();
            EnsureDebugPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnValidate()
        {
            initialDay = Mathf.Max(1, initialDay);
            initialMoney = Mathf.Max(0, initialMoney);
            initialDivinePower = Mathf.Max(0, initialDivinePower);
        }

        private void EnsureDebugPanel()
        {
            if (!showDebugPanel)
            {
                return;
            }

            CampusGameplayDebugPanel debugPanel = GetComponent<CampusGameplayDebugPanel>();
            if (debugPanel == null)
            {
                debugPanel = gameObject.AddComponent<CampusGameplayDebugPanel>();
            }

            debugPanel.Bind(this);
        }
    }
}
