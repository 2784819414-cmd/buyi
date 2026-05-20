using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusTeacherAiController : ICampusNpcAiController
    {
        private const float DecisionIntervalSeconds = 0.85f;

        private readonly List<CampusNpcActionOpportunity> opportunityScratch =
            new List<CampusNpcActionOpportunity>(4);

        private CampusNpcAiRuntime npc;

        public CampusCharacterRole Role => CampusCharacterRole.Teacher;

        public void Bind(CampusNpcAiRuntime runtime)
        {
            npc = runtime;
        }

        public CampusNpcPersonalProfile BuildProfile()
        {
            CampusNpcPersonalProfile profile = new CampusNpcPersonalProfile();
            CampusCharacterRuntime runtime = npc != null ? npc.Runtime : null;
            CampusCharacterData data = npc != null ? npc.Data : null;
            profile.Reset(data);
            if (data == null || npc == null || npc.WorldService == null)
            {
                return profile;
            }

            CampusNpcCommonProfileResolver.Build(profile, data, npc.WorldService, npc.RosterService);
            CampusTeacherProfileResolver.Build(profile, runtime, npc.WorldService, npc.RosterService);
            return profile;
        }

        public void Tick()
        {
            npc?.TickCurrentIntent();
            ThinkAndActIfDue();
        }

        public CampusNpcPlanDecision ChooseIntent()
        {
            if (npc == null || npc.Data == null)
            {
                return new CampusNpcPlanDecision(CampusNpcIntent.Idle("NoData"));
            }

            if (npc.Data.State == CampusCharacterState.Punished)
            {
                return new CampusNpcPlanDecision(CampusNpcIntentActions.OfficeDesk(npc, "Punished"));
            }

            if (ShouldContinueCurrentHold())
            {
                return new CampusNpcPlanDecision(npc.Mind.CurrentIntent);
            }

            if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.Required(), out CampusNpcIntent requiredIntent))
            {
                return new CampusNpcPlanDecision(requiredIntent);
            }

            if (CampusNpcScheduleFacts.IsClassSession(npc.Segment))
            {
                return new CampusNpcPlanDecision(CampusNpcIntentActions.TeacherPodium(npc, "Teach"));
            }

            if (CampusNpcScheduleFacts.IsTeacherOfficeWindow(npc.Segment))
            {
                return new CampusNpcPlanDecision(CampusNpcIntentActions.OfficeDesk(npc, "Office"));
            }

            if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.FreeMovement(), out CampusNpcIntent freeIntent))
            {
                return new CampusNpcPlanDecision(freeIntent);
            }

            return new CampusNpcPlanDecision(CampusNpcIntentActions.Common(npc, "TeacherFree"));
        }

        public string BuildInteractiveLine()
        {
            return npc != null && CampusNpcScheduleFacts.IsClassSession(npc.Segment)
                ? CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.TeacherClassInProgress)
                : CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.TeacherBackOfficeInteractive);
        }

        public string ResolveIntentLine(CampusNpcIntentKind kind)
        {
            switch (kind)
            {
                case CampusNpcIntentKind.TeachAssignedClass:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.TeacherHeadingToClass);
                case CampusNpcIntentKind.ReturnToOfficeDesk:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.TeacherBackOffice);
                case CampusNpcIntentKind.Roam:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.TeacherHeadingOut);
                default:
                    return string.Empty;
            }
        }

        private void ThinkAndActIfDue()
        {
            if (npc == null || !npc.IsDecisionDue)
            {
                return;
            }

            CampusNpcPlanDecision decision = ChooseIntent();
            npc.ApplyIntent(decision.Intent, ResolveIntentLine);
            npc.ScheduleNextDecision(DecisionIntervalSeconds);
        }

        private bool ShouldContinueCurrentHold()
        {
            return npc != null &&
                   npc.Mind != null &&
                   npc.Mind.CurrentIntent != null &&
                   !npc.Mind.CurrentIntent.UsesNavigation &&
                   npc.Mind.CurrentIntent.HoldSeconds > 0f &&
                   UnityEngine.Time.time < npc.Mind.IntentHoldUntil;
        }

        private bool TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery query, out CampusNpcIntent intent)
        {
            intent = null;
            if (!CampusNpcOpportunityRegistry.TryChoose(npc, query, opportunityScratch, out CampusNpcActionOpportunity opportunity))
            {
                return false;
            }

            intent = opportunity.ToIntent();
            return true;
        }
    }
}
