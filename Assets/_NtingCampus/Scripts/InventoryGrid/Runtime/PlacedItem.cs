using System;

namespace NtingCampus.InventoryGrid
{
    [Serializable]
    public class PlacedItem
    {
        public ItemInstance item;
        public int x;
        public int y;

        public PlacedItem()
        {
        }

        public PlacedItem(ItemInstance item, int x, int y)
        {
            this.item = item;
            this.x = x;
            this.y = y;
        }
    }
}
