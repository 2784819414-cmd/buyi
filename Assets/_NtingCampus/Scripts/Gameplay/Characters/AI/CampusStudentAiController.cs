using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusStudentAiController : ICampusNpcAiController
    {
        private const float DecisionIntervalSeconds = 0.75f;
        private readonly List<CampusNpcActionOpportunity> opportunityScratch =
            new List<CampusNpcActionOpportunity>(4);

        private CampusNpcAiRuntime npc;

        public CampusCharacterRole Role => CampusCharacterRole.Student;

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
            CampusStudentProfileResolver.Build(profile, runtime, npc.WorldService, npc.RosterService);
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

            if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.Required(), out CampusNpcIntent checkoutIntent))
            {
                return new CampusNpcPlanDecision(checkoutIntent);
            }

            if (CampusNpcScheduleFacts.IsClassSession(npc.Segment))
            {
                return new CampusNpcPlanDecision(CampusNpcIntentActions.StudentDesk(npc, "Class"));
            }

            if (CampusNpcScheduleFacts.IsDormWindow(npc.Segment))
            {
                return new CampusNpcPlanDecision(CampusNpcIntentActions.Dorm(npc, "Dorm"));
            }

            if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.FreeMovement(), out CampusNpcIntent storeIntent))
            {
                return new CampusNpcPlanDecision(storeIntent);
            }

            return new CampusNpcPlanDecision(CampusNpcIntentActions.Common(npc, "FreeRoam"));
        }

        public string BuildInteractiveLine()
        {
            if (npc != null && npc.Mind != null && npc.Mind.DeliveryState == CampusNpcDeliveryState.ReadyForPickup)
            {
                return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentDeliveryArrived);
            }

            if (npc != null && CampusNpcScheduleFacts.IsClassSession(npc.Segment))
            {
                return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentNeedsDesk);
            }

            return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentDeciding);
        }

        public string ResolveIntentLine(CampusNpcIntentKind kind)
        {
            switch (kind)
            {
                case CampusNpcIntentKind.AttendAssignedDesk:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentBackToDesk);
                case CampusNpcIntentKind.UsePhoneForDelivery:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentOrderingDelivery);
                case CampusNpcIntentKind.PickupDelivery:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentPickingUpDelivery);
                case CampusNpcIntentKind.BrowseStoreShelf:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentGoingStoreShelf);
                case CampusNpcIntentKind.PayStoreCheckout:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentGoingCheckout);
                case CampusNpcIntentKind.EatCanteenMeal:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentGoingCanteenMeal);
                case CampusNpcIntentKind.RestInDorm:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentBackToDorm);
                case CampusNpcIntentKind.Roam:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentHeadingOut);
                default:
                    return string.Empty;
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
