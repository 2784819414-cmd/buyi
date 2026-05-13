using System;
using UnityEngine;

namespace NtingCampus.InventoryGrid
{
    [Serializable]
    public class ItemInstance
    {
        public string instanceId;
        public ItemDefinition definition;
        public bool rotated;
        public int stackCount = 1;

        public int CurrentWidth
        {
            get
            {
                if (definition == null)
                {
                    return 0;
                }

                return rotated ? definition.height : definition.width;
            }
        }

        public int CurrentHeight
        {
            get
            {
                if (definition == null)
                {
                    return 0;
                }

                return rotated ? definition.width : definition.height;
            }
        }

        public float TotalWeight
        {
            get
            {
                return definition == null ? 0f : definition.weight * Mathf.Max(1, stackCount);
            }
        }

        public ItemInstance()
        {
        }

        public ItemInstance(ItemDefinition definition, string instanceId = null, int stackCount = 1)
        {
            this.definition = definition;
            this.instanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
            this.stackCount = Mathf.Max(1, stackCount);
        }

        public void Rotate()
        {
            if (!CanRotate())
            {
                return;
            }

            rotated = !rotated;
        }

        public bool CanRotate()
        {
            return definition != null && definition.canRotate;
        }

        public void NormalizeStackCount()
        {
            if (definition == null)
            {
                stackCount = Mathf.Max(1, stackCount);
                return;
            }

            int max = definition.stackable ? Mathf.Max(1, definition.maxStack) : 1;
            stackCount = Mathf.Clamp(stackCount, 1, max);
        }
    }
}
