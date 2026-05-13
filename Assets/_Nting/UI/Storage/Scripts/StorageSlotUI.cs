using UnityEngine;
using UnityEngine.EventSystems;

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public int X;
        public int Y;
        public StorageGridUI OwnerGrid;
        public StorageBoxGraphic Background;

        private bool hovered;
        private bool previewing;

        private void Awake()
        {
            EnsureVisual();
        }

        public void Bind(StorageGridUI ownerGrid, int x, int y)
        {
            OwnerGrid = ownerGrid;
            X = x;
            Y = y;
            EnsureVisual();
            SetNormal(false);
        }

        public void SetNormal(bool occupied)
        {
            previewing = false;
            EnsureVisual();
            Color fill = occupied ? StoragePalette.SlotOccupied : hovered ? StoragePalette.SlotHover : StoragePalette.Slot;
            Background.SetStyle(fill, StoragePalette.SlotBorder, 1f, 4f);
        }

        public void SetPreview(bool valid)
        {
            previewing = true;
            EnsureVisual();
            Background.SetStyle(valid ? StoragePalette.Valid : StoragePalette.Invalid, valid ? StoragePalette.Accent : new Color(0.86f, 0.28f, 0.24f, 1f), 1.4f, 4f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (!previewing)
            {
                SetNormal(OwnerGrid != null && OwnerGrid.IsCellOccupied(X, Y));
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (!previewing)
            {
                SetNormal(OwnerGrid != null && OwnerGrid.IsCellOccupied(X, Y));
            }
        }

        private void EnsureVisual()
        {
            if (Background == null)
            {
                Background = GetComponent<StorageBoxGraphic>();
            }

            if (Background == null)
            {
                Background = gameObject.AddComponent<StorageBoxGraphic>();
            }

            Background.raycastTarget = true;
        }
    }
}
