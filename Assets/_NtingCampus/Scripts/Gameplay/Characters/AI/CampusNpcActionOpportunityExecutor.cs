namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcActionOpportunityExecutor
    {
        public static bool TryCompleteCurrentOpportunity(CampusNpcAiRuntime npc)
        {
            CampusNpcIntent intent = npc != null && npc.Mind != null ? npc.Mind.CurrentIntent : null;
            CampusNpcActionOpportunity opportunity = intent != null ? intent.ActionOpportunity : null;
            if (npc == null ||
                npc.Runtime == null ||
                intent == null ||
                opportunity == null)
            {
                return false;
            }

            if (intent.UsesNavigation)
            {
                if (!npc.Navigator.HasArrived(intent, opportunity.StopDistance + 0.02f))
                {
                    return false;
                }

                if (opportunity.ArrivalHoldSeconds > 0f)
                {
                    BeginArrivalHold(npc, intent, opportunity);
                    return false;
                }
            }
            else if (intent.HoldSeconds > 0f &&
                     npc.Mind.IntentHoldUntil > 0f &&
                     UnityEngine.Time.time < npc.Mind.IntentHoldUntil)
            {
                return false;
            }

            bool completed = opportunity.TryExecute(npc.Runtime);
            intent.ActionOpportunity = null;
            npc.RequestDecisionSoon();

            return completed;
        }

        private static void BeginArrivalHold(
            CampusNpcAiRuntime npc,
            CampusNpcIntent intent,
            CampusNpcActionOpportunity opportunity)
        {
            if (npc == null || npc.Mind == null || intent == null || opportunity == null)
            {
                return;
            }

            npc.Navigator.Clear();
            CampusNpcIntent holdIntent = CampusNpcIntent.Hold(
                intent.Kind,
                intent.Label,
                opportunity.ArrivalHoldSeconds);
            holdIntent.RoomId = intent.RoomId;
            holdIntent.TargetPosition = intent.TargetPosition;
            holdIntent.StopDistance = intent.StopDistance;
            holdIntent.ActionOpportunity = opportunity;
            npc.Mind.CurrentIntent = holdIntent;
            npc.Mind.IntentHoldUntil = UnityEngine.Time.time + opportunity.ArrivalHoldSeconds;
        }
    }
}
