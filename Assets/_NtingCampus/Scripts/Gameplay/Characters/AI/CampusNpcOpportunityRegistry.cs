using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public static class CampusNpcOpportunityRegistry
    {
        private static readonly List<ICampusNpcActionOpportunityProvider> Providers =
            new List<ICampusNpcActionOpportunityProvider>();

        private static bool builtInsRegistered;

        public static void Register(ICampusNpcActionOpportunityProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            string providerId = provider.ProviderId ?? string.Empty;
            for (int i = 0; i < Providers.Count; i++)
            {
                ICampusNpcActionOpportunityProvider existing = Providers[i];
                if (ReferenceEquals(existing, provider) ||
                    string.Equals(existing.ProviderId ?? string.Empty, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    Providers[i] = provider;
                    return;
                }
            }

            Providers.Add(provider);
        }

        public static void Unregister(ICampusNpcActionOpportunityProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            Providers.Remove(provider);
        }

        internal static void Collect(
            CampusNpcAiRuntime npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results)
        {
            if (npc == null || results == null)
            {
                return;
            }

            EnsureBuiltInsRegistered();
            CampusNpcOpportunityContext context = new CampusNpcOpportunityContext(npc);
            for (int i = 0; i < Providers.Count; i++)
            {
                ICampusNpcActionOpportunityProvider provider = Providers[i];
                if (provider == null || !provider.CanCollect(context, query))
                {
                    continue;
                }

                provider.CollectOpportunities(context, query, results);
            }
        }

        internal static bool TryChoose(
            CampusNpcAiRuntime npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> scratch,
            out CampusNpcActionOpportunity selected)
        {
            selected = null;
            if (scratch == null)
            {
                return false;
            }

            scratch.Clear();
            Collect(npc, query, scratch);
            bool found = CampusNpcActionOpportunitySelector.TryChooseBest(npc, scratch, out selected);
            if (found)
            {
                MaybeLogSelectedOpportunity(npc, selected);
            }

            return found;
        }

        private static void EnsureBuiltInsRegistered()
        {
            if (builtInsRegistered)
            {
                return;
            }

            builtInsRegistered = true;
            CampusBuiltInNpcOpportunityProviders.Install();
        }

        private static void MaybeLogSelectedOpportunity(
            CampusNpcAiRuntime npc,
            CampusNpcActionOpportunity selected)
        {
            if (!CampusNpcEcologyPresetCatalog.EnableSelectionDebug ||
                npc == null ||
                npc.Data == null ||
                selected == null)
            {
                return;
            }

            Debug.Log(
                "[CampusNpcOpportunity] npc=" + npc.Data.Id +
                " selected=" + selected.ActionId +
                " intent=" + selected.DefaultIntentLabel +
                " score=" + selected.Score.ToString("0.###"));
        }
    }
}
