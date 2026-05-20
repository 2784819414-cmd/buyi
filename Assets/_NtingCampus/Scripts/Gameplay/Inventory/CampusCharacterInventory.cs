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
}
