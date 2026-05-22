using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;

namespace NtingCampus.Gameplay.Canteen
{
    internal static class CampusCanteenMealRules
    {
        private const string LunchBoxDefinitionId = "lunch_box";

        public static bool CanOrderMeal(CampusCharacterRuntime actor)
        {
            return actor != null &&
                   !HasLunchBoxInHands(actor) &&
                   !HasAnyNonLunchItemInHands(actor);
        }

        public static bool HasLunchBoxInHands(CampusCharacterRuntime actor)
        {
            return FindHeldItem(actor, item =>
                item != null &&
                string.Equals(item.DefinitionId, LunchBoxDefinitionId, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public static bool HasAnyNonLunchItemInHands(CampusCharacterRuntime actor)
        {
            return FindHeldItem(actor, item =>
                item != null &&
                !string.Equals(item.DefinitionId, LunchBoxDefinitionId, StringComparison.OrdinalIgnoreCase)) != null;
        }

        private static StorageItemModel FindHeldItem(
            CampusCharacterRuntime actor,
            Predicate<StorageItemModel> predicate)
        {
            if (actor == null || predicate == null)
            {
                return null;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            StorageContainerModel[] hands = inventory.Hands;
            for (int i = 0; i < hands.Length; i++)
            {
                StorageContainerModel container = hands[i];
                if (container == null || container.Items == null)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < container.Items.Count; itemIndex++)
                {
                    StorageItemModel item = container.Items[itemIndex];
                    if (predicate(item))
                    {
                        return item;
                    }
                }
            }

            return null;
        }
    }
}
