namespace NtingCampus.Gameplay.Characters
{
    internal interface ICampusNpcAiController
    {
        CampusCharacterRole Role { get; }
        void Bind(CampusNpcAiRuntime runtime);
        CampusNpcPersonalProfile BuildProfile();
        void Tick();
        CampusNpcPlanDecision ChooseIntent();
        string BuildInteractiveLine();
        string ResolveIntentLine(CampusNpcIntentKind kind);
    }
}
