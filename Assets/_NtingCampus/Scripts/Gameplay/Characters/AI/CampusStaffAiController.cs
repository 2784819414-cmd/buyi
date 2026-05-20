using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusStaffAiController : ICampusNpcAiController
    {
        private const float DecisionIntervalSeconds = 0.80f;

        private readonly List<CampusNpcActionOpportunity> opportunityScratch =
            new List<CampusNpcActionOpportunity>(4);

        private CampusNpcAiRuntime npc;

        public CampusCharacterRole Role => CampusCharacterRole.Staff;

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
            CampusStaffProfileResolver.Build(profile, runtime, npc.WorldService, npc.RosterService);
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

            CampusStaffDuty duty = npc.Data != null ? npc.Data.StaffDuty : CampusStaffDuty.None;
            if ((duty & CampusStaffDuty.StoreOwner) != 0 || (duty & CampusStaffDuty.BookstoreOwner) != 0)
            {
                return ChooseStoreIntent();
            }

            if ((duty & CampusStaffDuty.DeliveryWatcher) != 0)
            {
                return ChooseDeliveryWatcherIntent();
            }

            return ChooseCanteenIntent();
        }

        public string BuildInteractiveLine()
        {
            CampusStaffDuty duty = npc != null && npc.Data != null ? npc.Data.StaffDuty : CampusStaffDuty.None;
            if ((duty & CampusStaffDuty.StoreOwner) != 0 || (duty & CampusStaffDuty.BookstoreOwner) != 0)
            {
                return npc != null && CampusNpcScheduleFacts.IsStoreOpen(npc.Segment)
                    ? CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffRegisterOpen)
                    : CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffCheckingShelves);
            }

            if ((duty & CampusStaffDuty.DeliveryWatcher) != 0)
            {
                return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffWatchingDeliveryArea);
            }

            return npc != null && CampusNpcScheduleFacts.IsMealPeak(npc.Segment)
                ? CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffCounterOpen)
                : CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffCoveringWindows);
        }

        public string ResolveIntentLine(CampusNpcIntentKind kind)
        {
            switch (kind)
            {
                case CampusNpcIntentKind.WorkCanteenCounter:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffCounterDuty);
                case CampusNpcIntentKind.CoverCanteenWindows:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffCoveringWindowsIntent);
                case CampusNpcIntentKind.WorkStoreCheckout:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffRegisterDuty);
                case CampusNpcIntentKind.AuditStoreShelves:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffCheckingShelvesIntent);
                case CampusNpcIntentKind.WatchDeliveryPoint:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffWatchingDeliveriesIntent);
                case CampusNpcIntentKind.Roam:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffHeadingOut);
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

        private CampusNpcPlanDecision ChooseStoreIntent()
        {
            if (CampusNpcScheduleFacts.IsStoreOpen(npc.Segment))
            {
                return new CampusNpcPlanDecision(CampusNpcIntentActions.PrimaryWorkstation(
                    npc,
                    CampusNpcIntentKind.WorkStoreCheckout,
                    "StoreCheckout"));
            }

            if (CampusNpcScheduleFacts.IsStoreStocktakeWindow(npc.Segment))
            {
                return new CampusNpcPlanDecision(CampusNpcIntentActions.ShelfAuditPoint(npc));
            }

            if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.FreeMovement(), out CampusNpcIntent freeIntent))
            {
                return new CampusNpcPlanDecision(freeIntent);
            }

            return new CampusNpcPlanDecision(CampusNpcIntentActions.Common(npc, "StoreOffDuty"));
        }

        private CampusNpcPlanDecision ChooseDeliveryWatcherIntent()
        {
            if (!CampusNpcScheduleFacts.IsStaffOffDuty(npc.Segment))
            {
                return new CampusNpcPlanDecision(CampusNpcIntentActions.DeliveryPoint(
                    npc,
                    CampusNpcIntentKind.WatchDeliveryPoint,
                    "WatchDelivery"));
            }

            if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.FreeMovement(), out CampusNpcIntent freeIntent))
            {
                return new CampusNpcPlanDecision(freeIntent);
            }

            return new CampusNpcPlanDecision(CampusNpcIntentActions.Common(npc, "DeliveryOffDuty"));
        }

        private CampusNpcPlanDecision ChooseCanteenIntent()
        {
            if (CampusNpcScheduleFacts.IsMealPeak(npc.Segment))
            {
                if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.Duty(), out CampusNpcIntent dutyIntent))
                {
                    return new CampusNpcPlanDecision(dutyIntent);
                }

                return new CampusNpcPlanDecision(CampusNpcIntentActions.PrimaryWorkstation(
                    npc,
                    CampusNpcIntentKind.WorkCanteenCounter,
                    "CanteenCounter"));
            }

            if (CampusNpcScheduleFacts.IsCanteenShiftActive(npc.Segment))
            {
                if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.Duty(), out CampusNpcIntent dutyIntent))
                {
                    return new CampusNpcPlanDecision(dutyIntent);
                }

                return new CampusNpcPlanDecision(CampusNpcIntentActions.SecondaryWorkstation(
                    npc,
                    CampusNpcIntentKind.CoverCanteenWindows,
                    "CoverCanteen"));
            }

            if (TryChooseRegisteredOpportunity(CampusNpcOpportunityQuery.FreeMovement(), out CampusNpcIntent freeIntent))
            {
                return new CampusNpcPlanDecision(freeIntent);
            }

            return new CampusNpcPlanDecision(CampusNpcIntentActions.Common(npc, "StaffFree"));
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
