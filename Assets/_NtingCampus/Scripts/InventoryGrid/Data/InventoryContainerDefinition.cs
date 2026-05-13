using UnityEngine;

namespace NtingCampus.InventoryGrid
{
    [CreateAssetMenu(menuName = "Nting Campus/Inventory Grid/Container Definition", fileName = "InventoryContainerDefinition")]
    public sealed class InventoryContainerDefinition : ScriptableObject
    {
        public string containerId;
        public string displayName;

        public int width = 1;
        public int height = 1;

        public InventoryContainerType containerType = InventoryContainerType.Custom;

        public bool isPortable;
        public float searchExposure = 1f;
        public float accessSpeed = 1f;
        public float maxWeight = 10f;

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            searchExposure = Mathf.Max(0f, searchExposure);
            accessSpeed = Mathf.Max(0f, accessSpeed);
            maxWeight = Mathf.Max(0f, maxWeight);
        }
    }
}
