namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcAiControllerFactory
    {
        public static ICampusNpcAiController Create(CampusCharacterData data)
        {
            CampusCharacterRole role = data != null ? data.Role : CampusCharacterRole.Student;
            switch (role)
            {
                case CampusCharacterRole.Teacher:
                    return new CampusTeacherAiController();
                case CampusCharacterRole.Staff:
                    return new CampusStaffAiController();
                default:
                    return new CampusStudentAiController();
            }
        }
    }
}
