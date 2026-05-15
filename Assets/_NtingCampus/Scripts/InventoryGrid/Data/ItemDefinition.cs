using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.InventoryGrid
{
    [CreateAssetMenu(menuName = "Nting Campus/Inventory Grid/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        public Sprite icon;

        public int width = 1;
        public int height = 1;
        public bool canRotate = true;

        public float weight;
        public float suspicion;
        public float smell;
        public float noise;

        public bool stackable;
        public int maxStack = 1;

        public List<string> forbiddenTags = new List<string>();

        public bool HasEvidenceRisk
        {
            get
            {
                return suspicion > 0f ||
                       smell > 0f ||
                       noise > 0f ||
                       (forbiddenTags != null && forbiddenTags.Count > 0);
            }
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            weight = Mathf.Max(0f, weight);
            suspicion = Mathf.Max(0f, suspicion);
            smell = Mathf.Max(0f, smell);
            noise = Mathf.Max(0f, noise);
            maxStack = stackable ? Mathf.Max(1, maxStack) : 1;
        }
    }
}
