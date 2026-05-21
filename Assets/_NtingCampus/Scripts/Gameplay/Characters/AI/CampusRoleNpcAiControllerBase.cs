using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal abstract class CampusRoleNpcAiControllerBase : ICampusNpcAiController
    {
        private readonly List<CampusNpcActionOpportunity> opportunityScratch =
            new List<CampusNpcActionOpportunity>(4);

        protected CampusNpcAiRuntime Npc { get; private set; }

        public abstract CampusCharacterRole Role { get; }
        protected abstract float DecisionIntervalSeconds { get; }

        public void Bind(CampusNpcAiRuntime runtime)
        {
            Npc = runtime;
        }

        public CampusNpcPersonalProfile BuildProfile()
        {
            CampusNpcPersonalProfile profile = new CampusNpcPersonalProfile();
            CampusCharacterRuntime runtime = Npc != null ? Npc.Runtime : null;
            CampusCharacterData data = Npc != null ? Npc.Data : null;
            profile.Reset(data);
            if (data == null || Npc == null || Npc.WorldService == null)
            {
                return profile;
            }

            CampusNpcCommonProfileResolver.Build(profile, data, Npc.WorldService, Npc.RosterService);
            BuildRoleProfile(profile, runtime, data);
            return profile;
        }

        public void Tick()
        {
            Npc?.TickCurrentIntent();
            if (Npc == null || !Npc.IsDecisionDue)
            {
                return;
            }

            CampusNpcIntent nextIntent = ChooseIntent();
            Npc.ApplyIntent(nextIntent, ResolveIntentLine);
            Npc.ScheduleNextDecision(DecisionIntervalSeconds);
        }

        public CampusNpcIntent ChooseIntent()
        {
            if (Npc == null || Npc.Data == null)
            {
                return CampusNpcIntent.Idle("NoData");
            }

            if (Npc.Data.State == CampusCharacterState.Punished)
            {
                return CampusNpcIntentActions.OfficeDesk(Npc, "Punished");
            }

            if (ShouldContinueCurrentHold())
            {
                return Npc.Mind.CurrentIntent;
            }

            return ChooseRoleIntent();
        }

        public abstract string BuildInteractiveLine();
        public abstract string ResolveIntentLine(CampusNpcIntentKind kind);

        // Role-specific AI extension stays in two hooks:
        // build role profile facts, then choose the fallback/default intent order.
        protected abstract void BuildRoleProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusCharacterData data);

        protected abstract CampusNpcIntent ChooseRoleIntent();

        protected bool TryChooseOpportunity(CampusNpcOpportunityQuery query, out CampusNpcIntent intent)
        {
            intent = null;
            if (Npc == null)
            {
                return false;
            }

            if (!CampusNpcOpportunityRegistry.TryChoose(
                    Npc,
                    query,
                    opportunityScratch,
                    out CampusNpcActionOpportunity opportunity))
            {
                return false;
            }

            intent = opportunity.ToIntent();
            return true;
        }

        private bool ShouldContinueCurrentHold()
        {
            return Npc != null &&
                   Npc.Mind != null &&
                   Npc.Mind.CurrentIntent != null &&
                   !Npc.Mind.CurrentIntent.UsesNavigation &&
                   Npc.Mind.CurrentIntent.HoldSeconds > 0f &&
                   Time.time < Npc.Mind.IntentHoldUntil;
        }
    }
}
