using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    [DisallowMultipleComponent]
    public sealed class CampusStoreShelfDefinition : MonoBehaviour
    {
        public string CategoryId = "general";
        [Min(1)] public int TargetItemCount = 6;
        public bool AutoRestock = true;
        public List<string> ItemDefinitionIds = new List<string>();

        public bool HasExplicitItemDefinitions => ItemDefinitionIds != null && ItemDefinitionIds.Count > 0;

        public string ResolveCategoryId()
        {
            return string.IsNullOrWhiteSpace(CategoryId) ? "general" : CategoryId.Trim();
        }

        public int ResolveTargetItemCount(int fallback)
        {
            return Mathf.Max(1, TargetItemCount > 0 ? TargetItemCount : fallback);
        }

        private void OnValidate()
        {
            CategoryId = ResolveCategoryId();
            TargetItemCount = Mathf.Max(1, TargetItemCount);
            if (ItemDefinitionIds == null)
            {
                ItemDefinitionIds = new List<string>();
            }
        }
    }
}
