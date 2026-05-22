namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcAiControllerFactory
    {
        public static ICampusNpcAiController Create(CampusCharacterData data)
        {
            return new CampusConfigDrivenNpcAiController();
        }
    }
}
