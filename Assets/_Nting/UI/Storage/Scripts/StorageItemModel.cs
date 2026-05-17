using System;
using UnityEngine;

namespace Nting.Storage
{
    [Serializable]
    public sealed class StorageItemModel
    {
        public string Id;
        public string DefinitionId;
        public string InstanceId;
        public string DisplayName;
        public int Width = 1;
        public int Height = 1;
        public float Weight;
        [TextArea]
        public string Description;
        public int X;
        public int Y;
        public bool Rotated;
        public Color ThemeColor = new Color(0.38f, 0.49f, 0.56f, 1f);
        public Sprite Icon;
        public bool IsUsable;
        public string UseActionId;
        public bool ConsumeOnUse = true;
        public string UseText;

        [NonSerialized]
        public StorageContainerModel CurrentContainer;

        public string CurrentContainerId => CurrentContainer != null ? CurrentContainer.Id : string.Empty;

        public int CurrentWidth => Mathf.Max(1, Width);

        public int CurrentHeight => Mathf.Max(1, Height);

        public void Rotate()
        {
            int previousWidth = Width;
            Width = Height;
            Height = previousWidth;
            Rotated = !Rotated;
        }

        public StorageItemModel CloneForPreview()
        {
            return (StorageItemModel)MemberwiseClone();
        }
    }
}
