namespace NtingCampus.Gameplay.Characters
{
    public readonly struct CampusNpcPlanDecision
    {
        public readonly CampusNpcIntent Intent;
        public readonly bool StartsDeliveryOrder;

        public CampusNpcPlanDecision(CampusNpcIntent intent, bool startsDeliveryOrder = false)
        {
            Intent = intent;
            StartsDeliveryOrder = startsDeliveryOrder;
        }
    }
}
