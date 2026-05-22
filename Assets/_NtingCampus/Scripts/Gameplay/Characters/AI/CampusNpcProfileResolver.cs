namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcProfileResolver
    {
        public static CampusNpcPersonalProfile Build(
            CampusNpcAiRuntime npc,
            CampusCharacterRuntime runtime,
            CampusCharacterData data)
        {
            CampusNpcPersonalProfile profile = new CampusNpcPersonalProfile();
            profile.Reset(data);
            if (npc == null || data == null || npc.WorldService == null)
            {
                return profile;
            }

            CampusNpcCommonProfileResolver.Build(profile, data, npc.WorldService, npc.RosterService);
            switch (data.Role)
            {
                case CampusCharacterRole.Teacher:
                    CampusTeacherProfileResolver.Build(profile, runtime, npc.WorldService, npc.RosterService);
                    break;
                case CampusCharacterRole.Staff:
                    CampusStaffProfileResolver.Build(profile, runtime, npc.WorldService, npc.RosterService);
                    break;
                default:
                    CampusStudentProfileResolver.Build(profile, runtime, npc.WorldService, npc.RosterService);
                    break;
            }

            return profile;
        }
    }
}
