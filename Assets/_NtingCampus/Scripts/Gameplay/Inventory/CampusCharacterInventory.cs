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
            StorageContainerModel backpackEquipmentSlot,
            StorageContainerModel backpack)
        {
            Owner = owner;
            Hands = hands ?? System.Array.Empty<StorageContainerModel>();
            Pockets = pockets ?? System.Array.Empty<StorageContainerModel>();
            BackpackEquipmentSlot = backpackEquipmentSlot;
            Backpack = backpack;
        }

        public CampusCharacterRuntime Owner { get; }
        public StorageContainerModel[] Hands { get; }
        public StorageContainerModel[] Pockets { get; }
        public StorageContainerModel BackpackEquipmentSlot { get; }
        public StorageContainerModel Backpack { get; }
        public StorageItemModel EquippedBackpack => CampusBackpackInventoryUtility.ResolveEquippedBackpack(BackpackEquipmentSlot);
        public bool HasBackpack => Backpack != null && EquippedBackpack != null;
    }

    public static class CampusBackpackInventoryUtility
    {
        public static StorageContainerModel ResolveEquipmentSlot(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return null;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(runtime, false);
            return inventory != null ? inventory.BackpackEquipmentSlot : null;
        }

        public static StorageItemModel ResolveEquippedBackpack(StorageContainerModel equipmentSlot)
        {
            return equipmentSlot != null &&
                   equipmentSlot.Items != null &&
                   equipmentSlot.Items.Count > 0
                ? equipmentSlot.Items[0]
                : null;
        }

        public static StorageItemModel ResolveEquippedBackpack(CampusCharacterRuntime runtime)
        {
            return ResolveEquippedBackpack(ResolveEquipmentSlot(runtime));
        }

        public static int BuildBackpackStateHash(CampusCharacterRuntime runtime)
        {
            return BuildEquipmentSlotHash(ResolveEquipmentSlot(runtime));
        }

        public static int BuildEquipmentSlotHash(StorageContainerModel equipmentSlot)
        {
            StorageItemModel item = ResolveEquippedBackpack(equipmentSlot);
            unchecked
            {
                int hash = 17;
                hash = CombineHash(hash, equipmentSlot != null ? equipmentSlot.Id : string.Empty);
                hash = CombineHash(hash, item != null ? item.InstanceId : string.Empty);
                hash = CombineHash(hash, item != null ? item.DefinitionId : string.Empty);
                return hash;
            }
        }

        private static int CombineHash(int hash, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return hash * 31;
            }

            for (int i = 0; i < value.Length; i++)
            {
                hash = hash * 31 + value[i];
            }

            return hash;
        }
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
                hash = CombineHash(hash, container.IsCarriedInventory ? 1 : 0);
                hash = CombineHash(hash, (int)container.AccessPolicy);

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
                hash = CombineHash(hash, BuildEvidenceHash(item.Evidence));
                return hash;
            }
        }

        private static int BuildEvidenceHash(StorageItemEvidenceState evidence)
        {
            if (evidence == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                hash = CombineHash(hash, (int)evidence.LegalState);
                hash = CombineHash(hash, evidence.OwnerId);
                hash = CombineHash(hash, evidence.SourceContainerId);
                hash = CombineHash(hash, evidence.SourceRoomId);
                hash = CombineHash(hash, evidence.SourceLocation);
                hash = CombineHash(hash, evidence.AllowTaking ? 1 : 0);
                hash = CombineHash(hash, evidence.StolenDuringSession ? 1 : 0);
                hash = CombineHash(hash, evidence.SuspicionRisk);
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
