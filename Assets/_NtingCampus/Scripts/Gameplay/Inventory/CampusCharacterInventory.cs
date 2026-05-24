using Nting.Storage;
using NtingCampus.Gameplay.Characters;

namespace NtingCampus.Gameplay.Inventory
{
    public sealed class CampusCharacterInventory
    {
        public CampusCharacterInventory(
            CampusCharacterRuntime owner,
            StorageContainerModel[] hands,
            StorageContainerModel[] pockets,
            StorageContainerModel backpack)
        {
            Owner = owner;
            Hands = hands ?? System.Array.Empty<StorageContainerModel>();
            Pockets = pockets ?? System.Array.Empty<StorageContainerModel>();
            Backpack = backpack;
        }

        public CampusCharacterRuntime Owner { get; }
        public StorageContainerModel[] Hands { get; }
        public StorageContainerModel[] Pockets { get; }
        public StorageContainerModel Backpack { get; }
        public bool HasBackpack => Backpack != null;
    }

    public static class CampusHandInventoryUtility
    {
        public static StorageContainerModel[] ResolveHands(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return System.Array.Empty<StorageContainerModel>();
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(runtime, false);
            return inventory != null && inventory.Hands != null
                ? inventory.Hands
                : System.Array.Empty<StorageContainerModel>();
        }

        public static StorageContainerModel ResolveHandContainer(StorageContainerModel[] hands, int handIndex)
        {
            return hands != null && handIndex >= 0 && handIndex < hands.Length
                ? hands[handIndex]
                : null;
        }

        public static StorageItemModel ResolveHeldItem(StorageContainerModel[] hands, int handIndex)
        {
            StorageContainerModel hand = ResolveHandContainer(hands, handIndex);
            return hand != null && hand.Items != null && hand.Items.Count > 0
                ? hand.Items[0]
                : null;
        }

        public static int BuildHandsStateHash(CampusCharacterRuntime runtime)
        {
            return BuildHandsStateHash(ResolveHands(runtime));
        }

        public static int BuildHandsStateHash(StorageContainerModel[] hands)
        {
            unchecked
            {
                int hash = 17;
                hash = CombineHash(hash, hands != null ? hands.Length : 0);
                if (hands == null)
                {
                    return hash;
                }

                for (int i = 0; i < hands.Length; i++)
                {
                    hash = CombineHash(hash, BuildContainerHash(hands[i]));
                }

                return hash;
            }
        }

        private static int BuildContainerHash(StorageContainerModel container)
        {
            if (container == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                hash = CombineHash(hash, container.Id);
                hash = CombineHash(hash, container.Columns);
                hash = CombineHash(hash, container.Rows);
                hash = CombineHash(hash, container.IsSingleItemSlot ? 1 : 0);

                var items = container.Items;
                hash = CombineHash(hash, items != null ? items.Count : 0);
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        hash = CombineHash(hash, BuildItemHash(items[i]));
                    }
                }

                return hash;
            }
        }

        private static int BuildItemHash(StorageItemModel item)
        {
            if (item == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                hash = CombineHash(hash, item.InstanceId);
                hash = CombineHash(hash, item.DefinitionId);
                hash = CombineHash(hash, item.X);
                hash = CombineHash(hash, item.Y);
                hash = CombineHash(hash, item.Width);
                hash = CombineHash(hash, item.Height);
                hash = CombineHash(hash, item.Rotated ? 1 : 0);
                return hash;
            }
        }

        private static int CombineHash(int hash, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return CombineHash(hash, 0);
            }

            for (int i = 0; i < value.Length; i++)
            {
                hash = CombineHash(hash, value[i]);
            }

            return hash;
        }

        private static int CombineHash(int hash, int value)
        {
            unchecked
            {
                return hash * 31 + value;
            }
        }
    }
}
