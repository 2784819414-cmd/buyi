using System;
using System.Collections;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using UnityEngine;

namespace NtingCampus.Gameplay.Skeleton
{
    [DisallowMultipleComponent]
    public sealed class CampusMischiefConsequenceController : MonoBehaviour
    {
        private const int RecentConsequenceLogLimit = 10;
        private const float SensitiveAreaRewardMultiplier = 0.8f;
        private const int SensitiveAreaExtraHeat = 2;
        private const float BottleCapLockoutSeconds = 20f;

        private static readonly string[] AreaOrder =
        {
            CampusMischiefAreaNames.CampusShop,
            CampusMischiefAreaNames.Canteen,
            CampusMischiefAreaNames.Library,
            CampusMischiefAreaNames.OutsideDelivery,
            CampusMischiefAreaNames.Classroom,
            CampusMischiefAreaNames.SkewerStand
        };

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusMischiefActionController actionController;
        [SerializeField] private List<CampusMischiefAreaState> areaStates = new List<CampusMischiefAreaState>();

        private readonly Dictionary<string, CampusMischiefAreaState> areasByName =
            new Dictionary<string, CampusMischiefAreaState>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, float> actionDisabledUntil =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> recentConsequenceLogs = new List<string>(RecentConsequenceLogLimit);
        private readonly HashSet<string> sensitiveAreaLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool heat30Logged;
        private bool heat60Logged;

        public IReadOnlyList<CampusMischiefAreaState> AreaStates => areaStates;

        public IReadOnlyList<string> RecentConsequenceLogs => recentConsequenceLogs;

        public void Initialize(CampusGameBootstrap targetBootstrap, CampusMischiefActionController targetActionController)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            actionController = targetActionController != null ? targetActionController : GetComponent<CampusMischiefActionController>();
            EnsureAreas();
            RefreshAreaFlags();
        }

        public CampusMischiefAreaState GetAreaState(string areaName)
        {
            EnsureAreas();
            string normalizedAreaName = NormalizeAreaName(areaName);
            if (!areasByName.TryGetValue(normalizedAreaName, out CampusMischiefAreaState areaState))
            {
                areaState = new CampusMischiefAreaState(normalizedAreaName);
                areaStates.Add(areaState);
                areasByName[normalizedAreaName] = areaState;
            }

            areaState.RefreshRiskFlags();
            return areaState;
        }

        public bool IsAreaSensitive(string areaName)
        {
            return GetAreaState(areaName).IsTemporarilyHot;
        }

        public bool IsActionTemporarilyDisabled(string functionId, out string unavailableText)
        {
            unavailableText = string.Empty;
            string normalizedFunctionId = NormalizeFunctionId(functionId);
            if (!actionDisabledUntil.TryGetValue(normalizedFunctionId, out float disabledUntil))
            {
                return false;
            }

            float remainingSeconds = disabledUntil - Time.time;
            if (remainingSeconds <= 0f)
            {
                actionDisabledUntil.Remove(normalizedFunctionId);
                return false;
            }

            unavailableText = "暂时被盯上 " + Mathf.CeilToInt(remainingSeconds) + "秒";
            return true;
        }

        public int ResolveDivinePowerReward(string areaName, int rawAmount, CampusEventLog eventLog)
        {
            int normalizedAmount = Mathf.Max(0, rawAmount);
            if (normalizedAmount <= 0)
            {
                return 0;
            }

            if (!IsAreaSensitive(areaName))
            {
                return normalizedAmount;
            }

            int adjustedAmount = Mathf.Max(1, Mathf.RoundToInt(normalizedAmount * SensitiveAreaRewardMultiplier));
            if (adjustedAmount < normalizedAmount)
            {
                AddGameplayLog(eventLog, "该区域暂时敏感，神力收益从 " + normalizedAmount + " 降到 " + adjustedAmount + "。");
            }

            return adjustedAmount;
        }

        public void HandleActionTriggered(
            CampusMischiefActionDefinition definition,
            int previousDailyCount,
            CampusMischiefActionController sourceActionController)
        {
            actionController = sourceActionController != null ? sourceActionController : actionController;
            EnsureAreas();

            CampusMischiefAreaState areaState = GetAreaState(definition.AreaName);
            bool wasSensitive = areaState.IsTemporarilyHot;

            switch (definition.FunctionId)
            {
                case CampusMischiefActionController.FunctionIds.PassNote:
                    HandlePassNote(definition, areaState);
                    break;

                case CampusMischiefActionController.FunctionIds.StealFriedChicken:
                    HandleStealFriedChicken(definition, areaState);
                    break;

                case CampusMischiefActionController.FunctionIds.StealDelivery:
                    HandleStealDelivery(definition, areaState);
                    break;

                case CampusMischiefActionController.FunctionIds.TwistBottleCaps:
                    HandleTwistBottleCaps(definition, previousDailyCount, areaState);
                    break;

                case CampusMischiefActionController.FunctionIds.ConfuseZhuzi:
                    HandleConfuseZhuzi(definition, previousDailyCount, areaState);
                    break;

                default:
                    areaState.RegisterIncident(definition.DisplayName, 1);
                    break;
            }

            if (wasSensitive && actionController != null)
            {
                actionController.AddMischiefHeat(SensitiveAreaExtraHeat);
                WriteConsequenceLog("敏感区域里继续惹事，热度上升得更快。");
            }

            WriteAreaThresholdLog(areaState);
            WriteHeatThresholdLogs();
        }

        private void HandlePassNote(
            CampusMischiefActionDefinition definition,
            CampusMischiefAreaState areaState)
        {
            areaState.RegisterIncident(definition.DisplayName, 1);

            int heat = actionController != null ? actionController.MischiefHeat : 0;
            float chance = Mathf.Clamp(0.12f + heat * 0.006f, 0.12f, 0.55f);
            if (UnityEngine.Random.value <= chance)
            {
                WriteConsequenceLog("老师看了你一眼。");
                areaState.AddSuspicion(3);
                AddHeat(2);
            }
        }

        private void HandleStealFriedChicken(
            CampusMischiefActionDefinition definition,
            CampusMischiefAreaState areaState)
        {
            bool clerkBusy = actionController != null &&
                             actionController.IsActorInState(CampusSkeletonActorRole.CanteenClerk, CampusSkeletonActorStates.Busy);
            if (clerkBusy)
            {
                areaState.RegisterIncident(definition.DisplayName, 2);
                return;
            }

            areaState.RegisterIncident(definition.DisplayName, 5);
            WriteConsequenceLog("食堂店员觉得熟食柜少了点什么。");
            AddHeat(3);
        }

        private void HandleStealDelivery(
            CampusMischiefActionDefinition definition,
            CampusMischiefAreaState areaState)
        {
            areaState.RegisterIncident(definition.DisplayName, 8);
            AddHeat(5);
            StartCoroutine(WriteDelayedConsequenceLog(3f, "失主去年级部告状，手机先被收了。"));
            StartCoroutine(WriteDelayedConsequenceLog(6f, "年级部要求严查外卖，没人提外卖为什么会丢。"));
        }

        private void HandleTwistBottleCaps(
            CampusMischiefActionDefinition definition,
            int previousDailyCount,
            CampusMischiefAreaState areaState)
        {
            int newCount = previousDailyCount + 1;
            areaState.RegisterIncident(definition.DisplayName, 6);

            if (newCount >= 2)
            {
                WriteConsequenceLog("超市店员开始检查饮料货架。");
            }

            if (newCount >= 3)
            {
                WriteConsequenceLog("这排饮料暂时被盯上了。");
            }

            if (newCount >= 4)
            {
                actionDisabledUntil[definition.FunctionId] = Time.time + BottleCapLockoutSeconds;
            }
        }

        private void HandleConfuseZhuzi(
            CampusMischiefActionDefinition definition,
            int previousDailyCount,
            CampusMischiefAreaState areaState)
        {
            int newCount = previousDailyCount + 1;
            areaState.RegisterIncident(definition.DisplayName, 4);
            StartCoroutine(WriteDelayedConsequenceLog(3f, "登记老师开始怀疑今天的书架不太对。"));

            if (newCount >= 2)
            {
                WriteConsequenceLog("柱子把你的话当成了新规定。");
            }
        }

        private void AddHeat(int amount)
        {
            if (actionController != null)
            {
                actionController.AddMischiefHeat(amount);
            }
        }

        private void WriteHeatThresholdLogs()
        {
            int heat = actionController != null ? actionController.MischiefHeat : 0;
            if (!heat30Logged && heat >= 30)
            {
                heat30Logged = true;
                WriteConsequenceLog("今天校园里不太安生。");
            }

            if (!heat60Logged && heat >= 60)
            {
                heat60Logged = true;
                WriteConsequenceLog("老师们开始觉得有人在故意找事。");
            }
        }

        private void WriteAreaThresholdLog(CampusMischiefAreaState areaState)
        {
            if (areaState == null ||
                !areaState.IsTemporarilyHot ||
                sensitiveAreaLogged.Contains(areaState.AreaName))
            {
                return;
            }

            sensitiveAreaLogged.Add(areaState.AreaName);
            WriteConsequenceLog(ResolveAreaDisplayName(areaState.AreaName) + "暂时变得敏感。");
        }

        private IEnumerator WriteDelayedConsequenceLog(float delaySeconds, string message)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));
            WriteConsequenceLog(message);
            WriteHeatThresholdLogs();
        }

        private void WriteConsequenceLog(string message)
        {
            CampusEventLog eventLog = ResolveEventLog();
            AddGameplayLog(eventLog, message);
            recentConsequenceLogs.Add(message);
            int extraCount = recentConsequenceLogs.Count - RecentConsequenceLogLimit;
            if (extraCount > 0)
            {
                recentConsequenceLogs.RemoveRange(0, extraCount);
            }
        }

        private static void AddGameplayLog(CampusEventLog eventLog, string message)
        {
            if (eventLog != null)
            {
                eventLog.AddLog(message);
            }
        }

        private CampusEventLog ResolveEventLog()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            return bootstrap != null ? bootstrap.EventLog : null;
        }

        private void EnsureAreas()
        {
            if (areaStates == null)
            {
                areaStates = new List<CampusMischiefAreaState>();
            }

            areasByName.Clear();
            for (int i = areaStates.Count - 1; i >= 0; i--)
            {
                CampusMischiefAreaState areaState = areaStates[i];
                if (areaState == null || string.IsNullOrWhiteSpace(areaState.AreaName))
                {
                    areaStates.RemoveAt(i);
                    continue;
                }

                areaState.RefreshRiskFlags();
                areasByName[areaState.AreaName] = areaState;
            }

            for (int i = 0; i < AreaOrder.Length; i++)
            {
                string areaName = AreaOrder[i];
                if (areasByName.ContainsKey(areaName))
                {
                    continue;
                }

                CampusMischiefAreaState areaState = new CampusMischiefAreaState(areaName);
                areaStates.Add(areaState);
                areasByName[areaName] = areaState;
            }
        }

        private void RefreshAreaFlags()
        {
            EnsureAreas();
            for (int i = 0; i < areaStates.Count; i++)
            {
                areaStates[i]?.RefreshRiskFlags();
            }
        }

        private static string ResolveAreaDisplayName(string areaName)
        {
            switch (areaName)
            {
                case CampusMischiefAreaNames.CampusShop:
                    return "学校超市";
                case CampusMischiefAreaNames.Canteen:
                    return "食堂";
                case CampusMischiefAreaNames.Library:
                    return "图书馆";
                case CampusMischiefAreaNames.OutsideDelivery:
                    return "校外外卖灰区";
                case CampusMischiefAreaNames.Classroom:
                    return "教室";
                case CampusMischiefAreaNames.SkewerStand:
                    return "烤面筋摊";
                default:
                    return string.IsNullOrWhiteSpace(areaName) ? "该区域" : areaName.Trim();
            }
        }

        private static string NormalizeAreaName(string areaName)
        {
            return string.IsNullOrWhiteSpace(areaName) ? "Unknown" : areaName.Trim();
        }

        private static string NormalizeFunctionId(string functionId)
        {
            return string.IsNullOrWhiteSpace(functionId) ? string.Empty : functionId.Trim();
        }
    }
}
