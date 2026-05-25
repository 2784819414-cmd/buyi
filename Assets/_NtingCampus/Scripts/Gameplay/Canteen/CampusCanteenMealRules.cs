using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Canteen
{
    internal static class CampusCanteenMealRules
    {
        public static bool CanOrderMeal(CampusCharacterRuntime actor)
        {
            return CanReceiveMenuItem(actor) && !HasOwnServedCanteenItem(actor);
        }

        public static bool HasOwnServedCanteenItem(CampusCharacterRuntime actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.CharacterId))
            {
                return false;
            }

            IReadOnlyList<CampusDroppedStorageItem> items = CampusDroppedStorageItemRegistry.ActiveItems;
            for (int i = 0; i < items.Count; i++)
            {
                CampusDroppedStorageItem item = items[i];
                if (CampusCanteenServedItemPlacement.IsServedCanteenItem(item) &&
                    string.Equals(item.OwnerId, actor.CharacterId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanReceiveMenuItem(CampusCharacterRuntime actor)
        {
            return HasFreeHandSlot(actor);
        }

        public static bool HasFreeHandSlot(CampusCharacterRuntime actor)
        {
            StorageContainerModel[] hands = CampusHandInventoryUtility.ResolveHands(actor);
            for (int i = 0; i < hands.Length; i++)
            {
                StorageContainerModel container = hands[i];
                if (container != null && !HasAnyItem(container))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyItem(StorageContainerModel container)
        {
            if (container == null || container.Items == null)
            {
                return false;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                if (container.Items[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
