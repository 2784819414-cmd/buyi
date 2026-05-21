namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusStudentAiController : CampusRoleNpcAiControllerBase
    {
        public override CampusCharacterRole Role => CampusCharacterRole.Student;
        protected override float DecisionIntervalSeconds => 0.75f;

        public override string BuildInteractiveLine()
        {
            if (Npc != null && CampusNpcScheduleFacts.IsClassSession(Npc.Segment))
            {
                return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentNeedsDesk);
            }

            return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentDeciding);
        }

        public override string ResolveIntentLine(CampusNpcIntentKind kind)
        {
            switch (kind)
            {
                case CampusNpcIntentKind.AttendAssignedDesk:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentBackToDesk);
                case CampusNpcIntentKind.RestInDorm:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentBackToDorm);
                case CampusNpcIntentKind.Roam:
                    return CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.StudentHeadingOut);
                default:
                    return string.Empty;
            }
        }

        protected override void BuildRoleProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusCharacterData data)
        {
            CampusStudentProfileResolver.Build(profile, runtime, Npc.WorldService, Npc.RosterService);
        }

        protected override CampusNpcIntent ChooseRoleIntent()
        {
            if (TryChooseOpportunity(CampusNpcOpportunityQuery.Required(), out CampusNpcIntent requiredIntent))
            {
                return requiredIntent;
            }

            if (CampusNpcEcologyPresetCatalog.TryResolveDefaultIntent(Npc, out CampusNpcIntent configuredIntent))
            {
                return configuredIntent;
            }

            if (TryChooseOpportunity(CampusNpcOpportunityQuery.FreeMovement(), out CampusNpcIntent roamingIntent))
            {
                return roamingIntent;
            }

            return CampusNpcIntentActions.Common(Npc, "FreeRoam");
        }
    }
}
