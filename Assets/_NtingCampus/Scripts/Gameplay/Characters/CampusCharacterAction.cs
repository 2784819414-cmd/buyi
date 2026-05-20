using Nting.Storage;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public enum CampusCharacterActionKind
    {
        None = 0,
        PressInteract = 10,
        PickUpDroppedItem = 20,
        TransferItem = 30,
        TransferItemToFirstFit = 31,
        DropItemToGround = 40,
        OpenInventoryView = 50,
        RunCommand = 100
    }

    public interface ICampusCharacterActionCommand
    {
        bool TryExecute(CampusCharacterRuntime actor, out StorageTransferResult result);
    }

    public sealed class CampusCharacterAction
    {
        private CampusCharacterAction(CampusCharacterActionKind kind)
        {
            Kind = kind;
        }

        public CampusCharacterActionKind Kind { get; private set; }
        public Object Target { get; private set; }
        public StorageItemModel Item { get; private set; }
        public StorageContainerModel SourceContainer { get; private set; }
        public StorageContainerModel TargetContainer { get; private set; }
        public StorageContainerModel[] TargetContainers { get; private set; }
        public int TargetX { get; private set; }
        public int TargetY { get; private set; }
        public bool IncludeBackpack { get; private set; }
        public GameObject GroundDropContext { get; private set; }
        public ICampusCharacterActionCommand Command { get; private set; }

        public static CampusCharacterAction PressInteract(Object target)
        {
            return new CampusCharacterAction(CampusCharacterActionKind.PressInteract)
            {
                Target = target
            };
        }

        public static CampusCharacterAction PickUpDroppedItem(CampusDroppedStorageItem target)
        {
            return new CampusCharacterAction(CampusCharacterActionKind.PickUpDroppedItem)
            {
                Target = target
            };
        }

        public static CampusCharacterAction TransferItem(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y)
        {
            return new CampusCharacterAction(CampusCharacterActionKind.TransferItem)
            {
                Item = item,
                SourceContainer = source,
                TargetContainer = target,
                TargetX = x,
                TargetY = y
            };
        }

        public static CampusCharacterAction TransferItemToFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets)
        {
            return new CampusCharacterAction(CampusCharacterActionKind.TransferItemToFirstFit)
            {
                Item = item,
                SourceContainer = source,
                TargetContainers = targets
            };
        }

        public static CampusCharacterAction DropItemToGround(
            StorageItemModel item,
            StorageContainerModel source,
            GameObject groundDropContext)
        {
            return new CampusCharacterAction(CampusCharacterActionKind.DropItemToGround)
            {
                Item = item,
                SourceContainer = source,
                GroundDropContext = groundDropContext
            };
        }

        public static CampusCharacterAction OpenInventoryView(
            StorageContainerModel externalContainer,
            GameObject groundDropContext,
            bool includeBackpack)
        {
            return new CampusCharacterAction(CampusCharacterActionKind.OpenInventoryView)
            {
                TargetContainer = externalContainer,
                GroundDropContext = groundDropContext,
                IncludeBackpack = includeBackpack
            };
        }

        public static CampusCharacterAction RunCommand(ICampusCharacterActionCommand command)
        {
            return new CampusCharacterAction(CampusCharacterActionKind.RunCommand)
            {
                Command = command
            };
        }
    }
}
