using System;
using System.Collections;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Skeleton
{
    public readonly struct CampusMischiefActionDefinition
    {
        public readonly string FunctionId;
        public readonly string DisplayName;
        public readonly string AnchorId;
        public readonly string AreaName;
        public readonly int BaseDivinePower;
        public readonly int HeatGain;
        public readonly float CooldownSeconds;

        public CampusMischiefActionDefinition(
            string functionId,
            string displayName,
            string anchorId,
            string areaName,
            int baseDivinePower,
            int heatGain,
            float cooldownSeconds)
        {
            FunctionId = functionId;
            DisplayName = displayName;
            AnchorId = anchorId;
            AreaName = areaName;
            BaseDivinePower = Mathf.Max(0, baseDivinePower);
            HeatGain = Mathf.Max(0, heatGain);
            CooldownSeconds = Mathf.Clamp(cooldownSeconds, 1.5f, 3f);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CampusMischiefActionController : MonoBehaviour, ICampusInteractionActionHandler
    {
        public const string InteractionActionId = "nting.mischief.execute";
        public const string NoAvailableActionName = "-";
        private const string CooldownUnavailableText = "冷却中";

        public static class FunctionIds
        {
            public const string PassNote = "mischief.pass_note";
            public const string StealFriedChicken = "mischief.steal_fried_chicken";
            public const string StealDelivery = "mischief.steal_delivery";
            public const string TwistBottleCaps = "mischief.twist_bottle_caps";
            public const string ConfuseZhuzi = "mischief.confuse_zhuzi";
        }

        private static readonly CampusMischiefActionDefinition[] ActionDefinitionList =
        {
            new CampusMischiefActionDefinition(FunctionIds.PassNote, "传纸条", "ClassroomAnchor", CampusMischiefAreaNames.Classroom, 5, 3, 1.5f),
            new CampusMischiefActionDefinition(FunctionIds.StealFriedChicken, "偷炸鸡", "CanteenAnchor", CampusMischiefAreaNames.Canteen, 12, 8, 2f),
            new CampusMischiefActionDefinition(FunctionIds.StealDelivery, "偷外卖", "OutsideDeliveryAnchor", CampusMischiefAreaNames.OutsideDelivery, 10, 7, 2.5f),
            new CampusMischiefActionDefinition(FunctionIds.TwistBottleCaps, "拧瓶盖", "CampusShopAnchor", CampusMischiefAreaNames.CampusShop, 8, 6, 1.8f),
            new CampusMischiefActionDefinition(FunctionIds.ConfuseZhuzi, "逗柱子乱整理书", "LibraryAnchor", CampusMischiefAreaNames.Library, 6, 4, 2f)
        };

        public static IReadOnlyList<CampusMischiefActionDefinition> ActionDefinitions => ActionDefinitionList;

        [Min(0)] public int MischiefHeat;
        public readonly Dictionary<string, int> DailyActionCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public string CurrentAvailableActionName = NoAvailableActionName;
        public string CurrentAvailableFunctionId = string.Empty;
        public float LastActionTime = -999f;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusMischiefConsequenceController consequenceController;
        [SerializeField, Min(0.1f)] private float playerResolveInterval = 0.75f;

        private readonly Dictionary<string, CampusMischiefActionDefinition> definitionsByFunctionId =
            new Dictionary<string, CampusMischiefActionDefinition>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, CampusMischiefActionPoint> actionPointsByFunctionId =
            new Dictionary<string, CampusMischiefActionPoint>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, float> lastActionTimesByFunctionId =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<CampusSkeletonActorRole, CampusSkeletonActor> actorsByRole =
            new Dictionary<CampusSkeletonActorRole, CampusSkeletonActor>();

        private Transform playerTransform;
        private float nextPlayerResolveTime;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            BuildDefinitionLookup();
            ResolveConsequenceController();
            ResolvePlayerTransform(true);
            RefreshCurrentAvailableAction();
        }

        public void RegisterActionPoint(CampusMischiefActionDefinition definition, Transform anchor, float radius)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(definition.FunctionId))
            {
                return;
            }

            actionPointsByFunctionId[definition.FunctionId] = new CampusMischiefActionPoint
            {
                FunctionId = definition.FunctionId,
                DisplayName = definition.DisplayName,
                Anchor = anchor,
                InteractionAnchor = anchor.GetComponent<CampusInteractionAnchor>(),
                Radius = Mathf.Clamp(radius, 0.5f, 8f)
            };

            RefreshCurrentAvailableAction();
        }

        public void RegisterActor(CampusSkeletonActor actor)
        {
            if (actor == null || actor.Role == CampusSkeletonActorRole.None)
            {
                return;
            }

            actorsByRole[actor.Role] = actor;
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            if (!CampusInteractionActionIds.Equals(actionId, InteractionActionId))
            {
                return false;
            }

            if (anchor == null)
            {
                return false;
            }

            return TryTriggerAction(payload);
        }

        public bool TryTriggerByFunctionId(string functionId)
        {
            return TryTriggerAction(functionId);
        }

        public int GetDailyActionCount(string functionId)
        {
            string normalized = NormalizeFunctionId(functionId);
            return DailyActionCount.TryGetValue(normalized, out int count) ? count : 0;
        }

        public bool TryGetActionDefinition(string functionId, out CampusMischiefActionDefinition definition)
        {
            BuildDefinitionLookup();
            return definitionsByFunctionId.TryGetValue(NormalizeFunctionId(functionId), out definition);
        }

        public CampusMischiefConsequenceController ConsequenceController => ResolveConsequenceController();

        public void ResetDailyMischief()
        {
            MischiefHeat = 0;
            DailyActionCount.Clear();
            lastActionTimesByFunctionId.Clear();
            LastActionTime = -999f;
            RefreshCurrentAvailableAction();
        }

        private void Awake()
        {
            BuildDefinitionLookup();
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            ResolveConsequenceController();
        }

        private void Update()
        {
            ResolvePlayerTransform(false);
            RefreshInteractionAnchorAvailability();
            RefreshCurrentAvailableAction();
        }

        private void BuildDefinitionLookup()
        {
            if (definitionsByFunctionId.Count == ActionDefinitionList.Length)
            {
                return;
            }

            definitionsByFunctionId.Clear();
            for (int i = 0; i < ActionDefinitionList.Length; i++)
            {
                CampusMischiefActionDefinition definition = ActionDefinitionList[i];
                definitionsByFunctionId[definition.FunctionId] = definition;
            }
        }

        private bool TryTriggerAction(string functionId)
        {
            BuildDefinitionLookup();

            string normalizedFunctionId = NormalizeFunctionId(functionId);
            if (!definitionsByFunctionId.TryGetValue(normalizedFunctionId, out CampusMischiefActionDefinition definition))
            {
                return false;
            }

            CampusMischiefConsequenceController consequences = ResolveConsequenceController();
            if (consequences != null && consequences.IsActionTemporarilyDisabled(normalizedFunctionId, out _))
            {
                return false;
            }

            if (!IsCooldownReady(normalizedFunctionId, definition.CooldownSeconds))
            {
                return false;
            }

            CampusGameBootstrap targetBootstrap = ResolveBootstrap();
            if (targetBootstrap == null || targetBootstrap.ResourceState == null || targetBootstrap.EventLog == null)
            {
                return false;
            }

            int previousCount = GetDailyActionCount(normalizedFunctionId);
            ApplyActionEffects(definition, previousCount, targetBootstrap);
            DailyActionCount[normalizedFunctionId] = previousCount + 1;
            AddMischiefHeat(definition.HeatGain);
            LastActionTime = Time.time;
            lastActionTimesByFunctionId[normalizedFunctionId] = Time.time;
            consequences?.HandleActionTriggered(definition, previousCount, this);
            RefreshCurrentAvailableAction();
            return true;
        }

        public void AddMischiefHeat(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MischiefHeat = amount > int.MaxValue - MischiefHeat ? int.MaxValue : MischiefHeat + amount;
        }

        private bool IsCooldownReady(string functionId, float cooldownSeconds)
        {
            if (!lastActionTimesByFunctionId.TryGetValue(functionId, out float previousTime))
            {
                return true;
            }

            return Time.time - previousTime >= cooldownSeconds;
        }

        private void RefreshInteractionAnchorAvailability()
        {
            foreach (KeyValuePair<string, CampusMischiefActionPoint> pair in actionPointsByFunctionId)
            {
                CampusMischiefActionPoint point = pair.Value;
                CampusInteractionAnchor interactionAnchor = point.InteractionAnchor;
                if (interactionAnchor == null && point.Anchor != null)
                {
                    interactionAnchor = point.Anchor.GetComponent<CampusInteractionAnchor>();
                }

                if (interactionAnchor == null)
                {
                    continue;
                }

                bool cooldownReady = true;
                if (definitionsByFunctionId.TryGetValue(point.FunctionId, out CampusMischiefActionDefinition definition))
                {
                    cooldownReady = IsCooldownReady(point.FunctionId, definition.CooldownSeconds);
                }

                string unavailableText = string.Empty;
                CampusMischiefConsequenceController consequences = ResolveConsequenceController();
                bool temporarilyDisabled = consequences != null &&
                                           consequences.IsActionTemporarilyDisabled(point.FunctionId, out unavailableText);

                interactionAnchor.IsAvailable = cooldownReady && !temporarilyDisabled;
                interactionAnchor.UnavailableText = interactionAnchor.IsAvailable
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(unavailableText) ? CooldownUnavailableText : unavailableText;
                interactionAnchor.HideWhenUnavailable = false;
            }
        }

        private void ApplyActionEffects(
            CampusMischiefActionDefinition definition,
            int previousCount,
            CampusGameBootstrap targetBootstrap)
        {
            CampusResourceState resources = targetBootstrap.ResourceState;
            CampusEventLog eventLog = targetBootstrap.EventLog;

            switch (definition.FunctionId)
            {
                case FunctionIds.PassNote:
                    AddDivinePowerReward(resources, eventLog, definition, definition.BaseDivinePower);
                    eventLog.AddLog("你把纸条递了出去。");
                    eventLog.AddLog("神觉得这事有点意思。");
                    break;

                case FunctionIds.StealFriedChicken:
                    AddDivinePowerReward(resources, eventLog, definition, definition.BaseDivinePower);
                    eventLog.AddLog("你顺走了一份炸鸡。");
                    if (TryGetActor(CampusSkeletonActorRole.CanteenClerk, out CampusSkeletonActor canteenClerk) &&
                        canteenClerk.HasState(CampusSkeletonActorStates.Busy))
                    {
                        AddDivinePowerReward(resources, eventLog, definition, 5);
                        eventLog.AddLog("店员忙着打面，没顾上这边。");
                    }
                    break;

                case FunctionIds.StealDelivery:
                    AddDivinePowerReward(resources, eventLog, definition, definition.BaseDivinePower);
                    eventLog.AddLog("校外灰区少了一份外卖。");
                    break;

                case FunctionIds.TwistBottleCaps:
                    int bottleCapReward = ResolveBottleCapReward(definition.BaseDivinePower, previousCount);
                    if (previousCount > 0)
                    {
                        eventLog.AddLog("重复拧瓶盖的神力收益开始下降。");
                    }

                    AddDivinePowerReward(resources, eventLog, definition, bottleCapReward);
                    eventLog.AddLog("你拧开了一排饮料瓶盖。");
                    eventLog.AddLog("再来一瓶被你提前收走了。");
                    break;

                case FunctionIds.ConfuseZhuzi:
                    int zhuziReward = ResolveRepeatedReward(definition.BaseDivinePower, previousCount);
                    if (previousCount > 0)
                    {
                        eventLog.AddLog("柱子这次没让神觉得那么新鲜，收益下降。");
                    }

                    AddDivinePowerReward(resources, eventLog, definition, zhuziReward);
                    eventLog.AddLog("你教柱子按封面颜色整理图书。");
                    eventLog.AddLog("柱子认真地点了点头。");
                    break;
            }
        }

        private void AddDivinePowerReward(
            CampusResourceState resources,
            CampusEventLog eventLog,
            CampusMischiefActionDefinition definition,
            int rawAmount)
        {
            int finalAmount = Mathf.Max(0, rawAmount);
            CampusMischiefConsequenceController consequences = ResolveConsequenceController();
            if (consequences != null)
            {
                finalAmount = consequences.ResolveDivinePowerReward(definition.AreaName, finalAmount, eventLog);
            }

            resources.AddDivinePower(finalAmount);
            eventLog.AddLog("神力 +" + finalAmount + "。");
        }

        private static int ResolveBottleCapReward(int baseReward, int previousCount)
        {
            float multiplier = previousCount <= 0 ? 1f : previousCount == 1 ? 0.7f : 0.4f;
            return Mathf.Max(1, Mathf.RoundToInt(baseReward * multiplier));
        }

        private static int ResolveRepeatedReward(int baseReward, int previousCount)
        {
            float multiplier = previousCount <= 0 ? 1f : previousCount == 1 ? 0.7f : 0.4f;
            return Mathf.Max(1, Mathf.RoundToInt(baseReward * multiplier));
        }

        public bool IsActorInState(CampusSkeletonActorRole role, string expectedState)
        {
            return TryGetActor(role, out CampusSkeletonActor actor) && actor.HasState(expectedState);
        }

        public bool TryGetActor(CampusSkeletonActorRole role, out CampusSkeletonActor actor)
        {
            if (actorsByRole.TryGetValue(role, out actor) && actor != null)
            {
                return true;
            }

            CampusSkeletonActor[] actors = FindObjectsByType<CampusSkeletonActor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < actors.Length; i++)
            {
                CampusSkeletonActor candidate = actors[i];
                if (candidate == null || candidate.Role == CampusSkeletonActorRole.None)
                {
                    continue;
                }

                actorsByRole[candidate.Role] = candidate;
            }

            return actorsByRole.TryGetValue(role, out actor) && actor != null;
        }

        private void RefreshCurrentAvailableAction()
        {
            CurrentAvailableActionName = NoAvailableActionName;
            CurrentAvailableFunctionId = string.Empty;

            if (playerTransform == null || actionPointsByFunctionId.Count == 0)
            {
                return;
            }

            bool hasCandidate = false;
            float bestDistance = float.MaxValue;
            CampusMischiefActionPoint bestPoint = default;
            foreach (KeyValuePair<string, CampusMischiefActionPoint> pair in actionPointsByFunctionId)
            {
                CampusMischiefActionPoint point = pair.Value;
                if (point.Anchor == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(playerTransform.position, point.Anchor.position);
                if (distance > point.Radius)
                {
                    continue;
                }

                if (!hasCandidate || distance < bestDistance)
                {
                    hasCandidate = true;
                    bestDistance = distance;
                    bestPoint = point;
                }
            }

            if (!hasCandidate)
            {
                return;
            }

            CurrentAvailableActionName = bestPoint.DisplayName;
            CurrentAvailableFunctionId = bestPoint.FunctionId;
        }

        private CampusGameBootstrap ResolveBootstrap()
        {
            if (bootstrap != null)
            {
                return bootstrap;
            }

            bootstrap = CampusGameBootstrap.Instance;
            return bootstrap;
        }

        private CampusMischiefConsequenceController ResolveConsequenceController()
        {
            if (consequenceController != null)
            {
                return consequenceController;
            }

            consequenceController = GetComponent<CampusMischiefConsequenceController>();
            if (consequenceController == null)
            {
                consequenceController = FindFirstObjectByType<CampusMischiefConsequenceController>(FindObjectsInactive.Include);
            }

            if (consequenceController != null)
            {
                consequenceController.Initialize(ResolveBootstrap(), this);
            }

            return consequenceController;
        }

        private void ResolvePlayerTransform(bool force)
        {
            if (!force && playerTransform != null)
            {
                return;
            }

            if (!force && Time.time < nextPlayerResolveTime)
            {
                return;
            }

            nextPlayerResolveTime = Time.time + playerResolveInterval;

            CampusTestPlayerController playerController =
                FindFirstObjectByType<CampusTestPlayerController>(FindObjectsInactive.Exclude);
            if (playerController != null)
            {
                playerTransform = playerController.transform;
                return;
            }

            GameObject playerObject = GameObject.FindWithTag("Player");
            playerTransform = playerObject != null ? playerObject.transform : null;
        }

        private static string NormalizeFunctionId(string functionId)
        {
            return string.IsNullOrWhiteSpace(functionId) ? string.Empty : functionId.Trim();
        }

        private struct CampusMischiefActionPoint
        {
            public string FunctionId;
            public string DisplayName;
            public Transform Anchor;
            public CampusInteractionAnchor InteractionAnchor;
            public float Radius;
        }
    }
}
