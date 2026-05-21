namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusTeacherAiController : CampusRoleNpcAiControllerBase
    {
        public override CampusCharacterRole Role => CampusCharacterRole.Teacher;
        protected override float DecisionIntervalSeconds => 0.85f;

        public override string BuildInteractiveLine()
        {
            return Npc != null && CampusNpcScheduleFacts.IsClassSession(Npc.Segment)
                ? CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.TeacherClassInProgress)
                : CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.TeacherBackOfficeInteractive);
        }

        public override string ResolveIntentLine(CampusNpcIntentKind kind)
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

        protected override void BuildRoleProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusCharacterData data)
        {
            CampusTeacherProfileResolver.Build(profile, runtime, Npc.WorldService, Npc.RosterService);
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

            return CampusNpcIntentActions.Common(Npc, "TeacherFree");
        }
    }
}
