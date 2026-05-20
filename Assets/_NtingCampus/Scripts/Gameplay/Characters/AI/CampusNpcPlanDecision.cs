namespace NtingCampus.Gameplay.Characters
{
    public readonly struct CampusNpcPlanDecision
    {
        public readonly CampusNpcIntent Intent;

        public CampusNpcPlanDecision(CampusNpcIntent intent)
        {
            Intent = intent;
        }
    }
}
