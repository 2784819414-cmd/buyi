using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Canteen;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcActionRequirementCatalog
    {
        public const string CanOrderMeal = "CanOrderMeal";

        private static readonly Dictionary<string, Func<CampusCharacterRuntime, bool>> Requirements =
            new Dictionary<string, Func<CampusCharacterRuntime, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                [CanOrderMeal] = CampusCanteenMealRules.CanOrderMeal
            };

        public static bool IsKnownRequirement(string requirementId)
        {
            string normalizedId = NormalizeId(requirementId);
            return !string.IsNullOrEmpty(normalizedId) && Requirements.ContainsKey(normalizedId);
        }

        public static bool PassesAll(CampusCharacterRuntime actor, string[] requirementIds)
        {
            if (requirementIds == null || requirementIds.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < requirementIds.Length; i++)
            {
                if (!Passes(actor, requirementIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Passes(CampusCharacterRuntime actor, string requirementId)
        {
            string normalizedId = NormalizeId(requirementId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return false;
            }

            if (Requirements.TryGetValue(normalizedId, out Func<CampusCharacterRuntime, bool> predicate))
            {
                return predicate(actor);
            }

            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
