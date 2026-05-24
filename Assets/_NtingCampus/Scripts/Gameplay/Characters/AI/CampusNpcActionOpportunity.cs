using System;
using Nting.Storage;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public sealed class CampusNpcActionOpportunity
    {
        public CampusNpcActionOpportunity(
            string actionId,
            CampusCharacterAction action,
            Vector3 targetPosition,
            string roomId,
            float stopDistance,
            float score,
            string targetId = "")
            : this(
                actionId,
                action,
                targetPosition,
                roomId,
                stopDistance,
                score,
                CampusNpcIntentKind.Roam,
                actionId,
                true,
                0f,
                0f,
                false,
                targetId)
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
            string targetId = "")
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
                0f,
                false,
                targetId)
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
            float arrivalHoldSeconds,
            bool requireExactDestination,
            string targetId = "")
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
            ArrivalHoldSeconds = Mathf.Max(0f, arrivalHoldSeconds);
            RequireExactDestination = requireExactDestination;
            TargetId = string.IsNullOrWhiteSpace(targetId) ? string.Empty : targetId.Trim();
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
        public float ArrivalHoldSeconds { get; }
        public bool RequireExactDestination { get; }
        public string TargetId { get; }
        public string ScheduleEntryId { get; private set; } = string.Empty;
        public string ActionChainId { get; private set; } = string.Empty;
        public int ChainStepIndex { get; private set; }
        public int ChainStepCount { get; private set; }
        public bool AdvancesActionChain { get; private set; } = true;

        public UnityEngine.Object Target => Action != null ? Action.Target : null;

        internal void AssignActionChain(
            string scheduleEntryId,
            string actionChainId,
            int stepIndex,
            int stepCount,
            bool advancesActionChain)
        {
            ScheduleEntryId = string.IsNullOrWhiteSpace(scheduleEntryId) ? string.Empty : scheduleEntryId.Trim();
            ActionChainId = string.IsNullOrWhiteSpace(actionChainId) ? string.Empty : actionChainId.Trim();
            ChainStepIndex = Mathf.Max(0, stepIndex);
            ChainStepCount = Mathf.Max(1, stepCount);
            AdvancesActionChain = advancesActionChain;
        }

        public static CampusNpcActionOpportunity MoveTo(
            string actionId,
            CampusCharacterAction action,
            Vector3 targetPosition,
            string roomId,
            float stopDistance,
            float score,
            CampusNpcIntentKind intentKind,
            string intentLabel,
            bool requireExactDestination = false,
            string targetId = "")
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
                true,
                0f,
                0f,
                requireExactDestination,
                targetId);
        }

        public static CampusNpcActionOpportunity MoveToAndHold(
            string actionId,
            CampusCharacterAction action,
            Vector3 targetPosition,
            string roomId,
            float stopDistance,
            float score,
            CampusNpcIntentKind intentKind,
            string intentLabel,
            float arrivalHoldSeconds,
            bool requireExactDestination = false,
            string targetId = "")
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
                true,
                0f,
                arrivalHoldSeconds,
                requireExactDestination,
                targetId);
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
            string targetId = "")
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
                0f,
                false,
                targetId);
        }

        public bool TryExecute(CampusCharacterRuntime actor)
        {
            if (actor == null)
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
                    StopDistance,
                    RequireExactDestination)
                : CampusNpcIntent.Hold(kind, label, HoldSeconds);
            if (!UsesNavigation)
            {
                intent.RoomId = RoomId;
                intent.TargetPosition = TargetPosition;
                intent.StopDistance = StopDistance;
            }

            intent.ActionId = ActionId;
            intent.ActionOpportunity = this;
            return intent;
        }

        public CampusNpcIntent ToIntent()
        {
            return ToIntent(DefaultIntentKind, DefaultIntentLabel);
        }
    }
}
