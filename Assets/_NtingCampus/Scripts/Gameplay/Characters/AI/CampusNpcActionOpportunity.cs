using System;
using System.Collections.Generic;
using Nting.Storage;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public sealed class CampusNpcActionOpportunity
    {
        private readonly Func<CampusCharacterRuntime, bool> canUse;

        public CampusNpcActionOpportunity(
            string actionId,
            CampusCharacterAction action,
            Vector3 targetPosition,
            string roomId,
            float stopDistance,
            float score,
            Func<CampusCharacterRuntime, bool> canUse = null)
            : this(
                actionId,
                action,
                targetPosition,
                roomId,
                stopDistance,
                score,
                CampusNpcIntentKind.Roam,
                actionId,
                canUse)
        {
        }

        public CampusNpcActionOpportunity(
            string actionId,
            CampusCharacterAction action,
            Vector3 targetPosition,
            string roomId,
            float stopDistance,
            float score,
            CampusNpcIntentKind defaultIntentKind,
            string defaultIntentLabel,
            Func<CampusCharacterRuntime, bool> canUse = null)
            : this(
                actionId,
                action,
                targetPosition,
                roomId,
                stopDistance,
                score,
                defaultIntentKind,
                defaultIntentLabel,
                true,
                0f,
                canUse)
        {
        }

        public CampusNpcActionOpportunity(
            string actionId,
            CampusCharacterAction action,
            Vector3 targetPosition,
            string roomId,
            float stopDistance,
            float score,
            CampusNpcIntentKind defaultIntentKind,
            string defaultIntentLabel,
            bool usesNavigation,
            float holdSeconds,
            Func<CampusCharacterRuntime, bool> canUse = null)
        {
            ActionId = string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
            Action = action;
            TargetPosition = targetPosition;
            RoomId = roomId ?? string.Empty;
            StopDistance = Mathf.Max(0.02f, stopDistance);
            Score = score;
            DefaultIntentKind = defaultIntentKind;
            DefaultIntentLabel = string.IsNullOrWhiteSpace(defaultIntentLabel)
                ? ActionId
                : defaultIntentLabel.Trim();
            UsesNavigation = usesNavigation;
            HoldSeconds = Mathf.Max(0f, holdSeconds);
            this.canUse = canUse;
        }

        public string ActionId { get; }
        public CampusCharacterAction Action { get; }
        public Vector3 TargetPosition { get; }
        public string RoomId { get; }
        public float StopDistance { get; }
        public float Score { get; }
        public CampusNpcIntentKind DefaultIntentKind { get; }
        public string DefaultIntentLabel { get; }
        public bool UsesNavigation { get; }
        public float HoldSeconds { get; }

        public UnityEngine.Object Target => Action != null ? Action.Target : null;

        public static CampusNpcActionOpportunity MoveTo(
            string actionId,
            CampusCharacterAction action,
            Vector3 targetPosition,
            string roomId,
            float stopDistance,
            float score,
            CampusNpcIntentKind intentKind,
            string intentLabel,
            Func<CampusCharacterRuntime, bool> canUse = null)
        {
            return new CampusNpcActionOpportunity(
                actionId,
                action,
                targetPosition,
                roomId,
                stopDistance,
                score,
                intentKind,
                intentLabel,
                canUse);
        }

        public static CampusNpcActionOpportunity HoldAt(
            string actionId,
            CampusCharacterAction action,
            Vector3 targetPosition,
            string roomId,
            float score,
            CampusNpcIntentKind intentKind,
            string intentLabel,
            float holdSeconds = 0f,
            Func<CampusCharacterRuntime, bool> canUse = null)
        {
            return new CampusNpcActionOpportunity(
                actionId,
                action,
                targetPosition,
                roomId,
                0.02f,
                score,
                intentKind,
                intentLabel,
                false,
                holdSeconds,
                canUse);
        }

        public bool CanUse(CampusCharacterRuntime actor)
        {
            return actor != null && (canUse == null || canUse(actor));
        }

        public bool TryExecute(CampusCharacterRuntime actor)
        {
            if (!CanUse(actor))
            {
                return false;
            }

            return CampusCharacterActionExecutor.TryExecute(actor, Action, out StorageTransferResult _);
        }

        public CampusNpcIntent ToIntent(CampusNpcIntentKind kind, string label)
        {
            CampusNpcIntent intent = UsesNavigation
                ? CampusNpcIntent.Move(
                    kind,
                    label,
                    RoomId,
                    TargetPosition,
                    StopDistance)
                : CampusNpcIntent.Hold(kind, label, HoldSeconds);
            if (!UsesNavigation)
            {
                intent.RoomId = RoomId;
                intent.TargetPosition = TargetPosition;
                intent.StopDistance = StopDistance;
            }

            intent.ActionOpportunity = this;
            return intent;
        }

        public CampusNpcIntent ToIntent()
        {
            return ToIntent(DefaultIntentKind, DefaultIntentLabel);
        }
    }

    internal static class CampusNpcActionOpportunitySelector
    {
        public static bool TryChooseBest(
            CampusNpcAiRuntime npc,
            List<CampusNpcActionOpportunity> opportunities,
            out CampusNpcActionOpportunity selected)
        {
            selected = null;
            if (npc == null || npc.Runtime == null || opportunities == null || opportunities.Count == 0)
            {
                return false;
            }

            float bestScore = float.MinValue;
            for (int i = 0; i < opportunities.Count; i++)
            {
                CampusNpcActionOpportunity opportunity = opportunities[i];
                if (opportunity == null || !opportunity.CanUse(npc.Runtime))
                {
                    continue;
                }

                float score = opportunity.Score + ResolveStableTieBreak(npc, opportunity);
                if (selected == null || score > bestScore)
                {
                    selected = opportunity;
                    bestScore = score;
                }
            }

            return selected != null;
        }

        private static float ResolveStableTieBreak(
            CampusNpcAiRuntime npc,
            CampusNpcActionOpportunity opportunity)
        {
            string key = (opportunity != null ? opportunity.ActionId : string.Empty) + ":" + (npc != null ? npc.PersonalSeed : 0);
            return CampusNpcStableIds.PositiveModulo(CampusNpcStableIds.Hash(key), 1000) * 0.0001f;
        }
    }
}
