using System;
using System.Collections.Generic;

namespace NtingCampus.InventoryGrid
{
    [Serializable]
    public class InventoryContainerSaveData
    {
        public string containerId;
        public List<PlacedItemSaveData> items = new List<PlacedItemSaveData>();
    }

    [Serializable]
    public class PlacedItemSaveData
    {
        public string itemDefinitionId;
        public string instanceId;
        public int x;
        public int y;
        public bool rotated;
        public int stackCount;
    }
}
