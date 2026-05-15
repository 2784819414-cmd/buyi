using System;

namespace NtingCampus.Gameplay.Characters
{
    [Flags]
    public enum CampusTeacherDuty
    {
        None = 0,
        WorldLanguageTeacher = 1 << 0,
        MathTeacher = 1 << 1,
        HomeroomTeacher = 1 << 2,
        PatrolDirector = 1 << 3
    }
}
