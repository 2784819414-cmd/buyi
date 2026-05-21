namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusStaffAiController : CampusRoleNpcAiControllerBase
    {
        public override CampusCharacterRole Role => CampusCharacterRole.Staff;
        protected override float DecisionIntervalSeconds => 0.80f;

        public override string BuildInteractiveLine()
        {
            CampusNpcIntentKind kind = Npc != null && Npc.Mind != null && Npc.Mind.CurrentIntent != null
                ? Npc.Mind.CurrentIntent.Kind
                : CampusNpcIntentKind.Idle;
            switch (kind)
            {
                case CampusNpcIntentKind.AttendPrimaryWorkstation:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffAtWorkstation);
                default:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffHeadingOut);
            }
        }

        public override string ResolveIntentLine(CampusNpcIntentKind kind)
        {
            switch (kind)
            {
                case CampusNpcIntentKind.AttendPrimaryWorkstation:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffGoingWorkstation);
                case CampusNpcIntentKind.Roam:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StaffHeadingOut);
                default:
                    return string.Empty;
            }
        }

        protected override void BuildRoleProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusCharacterData data)
        {
            CampusStaffProfileResolver.Build(profile, runtime, Npc.WorldService, Npc.RosterService);
        }

        protected override CampusNpcIntent ChooseRoleIntent()
        {
            if (TryChooseOpportunity(CampusNpcOpportunityQuery.Required(), out CampusNpcIntent requiredIntent))
            {
                return requiredIntent;
            }

            if (TryChooseOpportunity(CampusNpcOpportunityQuery.Duty(), out CampusNpcIntent dutyIntent))
            {
                return dutyIntent;
            }

            if (CampusNpcEcologyPresetCatalog.TryResolveDefaultIntent(Npc, out CampusNpcIntent configuredIntent))
            {
                return configuredIntent;
            }

            if (TryChooseOpportunity(CampusNpcOpportunityQuery.FreeMovement(), out CampusNpcIntent roamingIntent))
            {
                return roamingIntent;
            }

            return CampusNpcIntentActions.Common(Npc, "StaffFree");
        }
    }
}
