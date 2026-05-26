using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusConfigDrivenNpcAiController : ICampusNpcAiController
    {
        private const float DecisionIntervalSeconds = 0.8f;

        private CampusNpcAiRuntime npc;

        public CampusCharacterRole Role => npc != null && npc.Data != null
            ? npc.Data.Role
            : CampusCharacterRole.Student;

        public void Bind(CampusNpcAiRuntime runtime)
        {
            npc = runtime;
        }

        public CampusNpcPersonalProfile BuildProfile()
        {
            return CampusNpcProfileResolver.Build(npc, npc != null ? npc.Runtime : null, npc != null ? npc.Data : null);
        }

        public void Tick()
        {
            npc?.TickCurrentIntent();
            if (npc == null || !npc.IsDecisionDue)
            {
                return;
            }

            CampusNpcIntent nextIntent = ChooseIntent();
            npc.ApplyIntent(nextIntent, ResolveIntentLine);
            npc.ScheduleNextDecision(DecisionIntervalSeconds);
        }

        public CampusNpcIntent ChooseIntent()
        {
            if (npc == null || npc.Data == null)
            {
                return CampusNpcIntent.Idle("NoData");
            }

            if (npc.Data.State == CampusCharacterState.Punished)
            {
                return CampusNpcIntentActions.OfficeDesk(npc, "Punished");
            }

            if (ShouldContinueCurrentHold())
            {
                return npc.Mind.CurrentIntent;
            }

            if (CampusNpcEcologyPresetCatalog.TryResolveScheduledIntent(npc, out CampusNpcIntent scheduledIntent))
            {
                return scheduledIntent;
            }

            return CampusNpcIntent.Idle("NoScheduledConfigIntent");
        }

        public string BuildInteractiveLine()
        {
            return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.GenericScheduledBusy);
        }

        public string ResolveIntentLine(CampusNpcIntentKind kind)
        {
            switch (kind)
            {
                case CampusNpcIntentKind.AttendAssignedDesk:
                case CampusNpcIntentKind.TeachAssignedClass:
                case CampusNpcIntentKind.AttendPrimaryWorkstation:
                case CampusNpcIntentKind.ReturnToOfficeDesk:
                case CampusNpcIntentKind.RestInDorm:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.GenericHeadingToTask);
                default:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.GenericHeadingOut);
            }
        }

        private bool ShouldContinueCurrentHold()
        {
            return npc != null &&
                   npc.Mind != null &&
                   npc.Mind.CurrentIntent != null &&
                   !npc.Mind.CurrentIntent.UsesNavigation &&
                   npc.Mind.CurrentIntent.HoldSeconds > 0f &&
                   Time.time < npc.Mind.IntentHoldUntil;
        }
    }
}
