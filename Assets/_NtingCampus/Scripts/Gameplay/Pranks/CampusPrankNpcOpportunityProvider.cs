using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    internal sealed class CampusPrankNpcOpportunityProvider : ICampusNpcActionOpportunityProvider
    {
        public static CampusPrankNpcOpportunityProvider Instance { get; } =
            new CampusPrankNpcOpportunityProvider();

        public string ProviderId => "prank";

        private CampusPrankNpcOpportunityProvider()
        {
        }

        public bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query)
        {
            return npc.IsValid &&
                   query.Purpose == CampusNpcOpportunityPurpose.FreeMovement &&
                   npc.Data.Role == CampusCharacterRole.Student;
        }

        public void CollectOpportunities(
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results)
        {
            if (results == null ||
                !CanCollect(npc, query) ||
                !ShouldSeekPrank(npc))
            {
                return;
            }

            CampusPrankService prankService = ResolvePrankService(npc);
            if (prankService == null ||
                !prankService.TryFindNpcPrankTarget(npc.Runtime, IsFreeMovementPayload, out CampusPrankTarget target))
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                "prank_" + SanitizeActionId(target.Payload),
                CampusCharacterAction.PressInteract(target.InteractTarget),
                target.Position,
                target.RoomId,
                target.StopDistance,
                ResolveScore(npc),
                CampusNpcIntentKind.Roam,
                "Prank",
                actor => actor != null && prankService.SupportsPayload(target.Payload)));
        }

        private static CampusPrankService ResolvePrankService(CampusNpcOpportunityContext npc)
        {
            if (npc.Bootstrap != null && npc.Bootstrap.PrankService != null)
            {
                return npc.Bootstrap.PrankService;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.PrankService != null)
            {
                return bootstrap.PrankService;
            }

            return UnityEngine.Object.FindFirstObjectByType<CampusPrankService>(FindObjectsInactive.Include);
        }

        private static bool ShouldSeekPrank(CampusNpcOpportunityContext npc)
        {
            if (!npc.IsValid ||
                npc.Data.IsPlayerControlled ||
                npc.Data.State == CampusCharacterState.Punished ||
                npc.Data.State == CampusCharacterState.Sleeping ||
                !CampusNpcScheduleFacts.IsStudentFreeMovementWindow(npc.Segment))
            {
                return false;
            }

            int threshold = 3 + Mathf.Clamp(npc.Data.Mischief / 10, 0, 9);
            if (npc.Data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                threshold += 10;
            }

            if (npc.Data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                threshold -= 5;
            }

            int day = npc.Bootstrap != null && npc.Bootstrap.GameState != null
                ? npc.Bootstrap.GameState.Day
                : 0;
            int roll = CampusNpcStableIds.PositiveModulo(
                CampusNpcStableIds.Hash(npc.Data.Id + ":prank:" + day + ":" + npc.Segment),
                100);
            return roll < Mathf.Clamp(threshold, 0, 35);
        }

        private static float ResolveScore(CampusNpcOpportunityContext npc)
        {
            float score = 38f + Mathf.Clamp(npc.Data.Mischief / 4f, 0f, 18f);
            if (npc.Data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                score += 10f;
            }

            return score;
        }

        private static bool IsFreeMovementPayload(string payload)
        {
            return IsPayload(payload, CampusPrankPayloadIds.StealDelivery) ||
                   IsPayload(payload, CampusPrankPayloadIds.StealFriedChicken) ||
                   IsPayload(payload, CampusPrankPayloadIds.StealBurger) ||
                   IsPayload(payload, CampusPrankPayloadIds.StealOden);
        }

        private static bool IsPayload(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeActionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            string result = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
        }
    }
}
