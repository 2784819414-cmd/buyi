using System.Collections.Generic;

namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusConfigDrivenNpcOpportunityProvider : ICampusNpcActionOpportunityProvider
    {
        public static readonly CampusConfigDrivenNpcOpportunityProvider Instance =
            new CampusConfigDrivenNpcOpportunityProvider();

        public string ProviderId => "config_driven_npc_opportunities";

        public bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query)
        {
            return npc.IsValid;
        }

        public void CollectOpportunities(
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results)
        {
            CampusNpcEcologyPresetCatalog.CollectOpportunities(npc, query, results);
        }
    }
}
