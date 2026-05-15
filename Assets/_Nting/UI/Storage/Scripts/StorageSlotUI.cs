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

        private StorageBoxGraphic topEdge;
        private StorageBoxGraphic innerShade;
        private StorageBoxGraphic occupiedMark;
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
            Color border = hovered ? StoragePalette.SlotHoverBorder : StoragePalette.SlotBorder;
            Background.SetStyle(fill, border, hovered ? 1.6f : 1.15f, 7f);
            topEdge.SetStyle(Color.clear, Color.clear, 0f, 0f);
            innerShade.SetStyle(new Color(0f, 0f, 0f, occupied ? 0.16f : 0.1f), Color.clear, 0f, 6f);
            occupiedMark.gameObject.SetActive(occupied);
            occupiedMark.SetStyle(StoragePalette.SlotMark, Color.clear, 0f, 0f);
        }

        public void SetPreview(bool valid)
        {
            previewing = true;
            EnsureVisual();

            Background.SetStyle(valid ? StoragePalette.Valid : StoragePalette.Invalid,
                valid ? StoragePalette.ValidBorder : StoragePalette.InvalidBorder,
                1.8f,
                7f);
            topEdge.SetStyle(Color.clear, Color.clear, 0f, 0f);
            innerShade.SetStyle(new Color(1f, 1f, 1f, valid ? 0.045f : 0.02f), Color.clear, 0f, 6f);
            occupiedMark.gameObject.SetActive(false);
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
            topEdge = EnsureChildBox(topEdge, "TopEdge", new Vector2(6f, 38f), new Vector2(-6f, -5f));
            innerShade = EnsureChildBox(innerShade, "InnerShade", new Vector2(5f, 5f), new Vector2(-5f, -5f));
            occupiedMark = EnsureChildBox(occupiedMark, "OccupiedMark", new Vector2(8f, 8f), new Vector2(-28f, -34f));
        }

        private StorageBoxGraphic EnsureChildBox(StorageBoxGraphic box, string objectName, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (box == null)
            {
                Transform child = transform.Find(objectName);
                if (child != null)
                {
                    box = child.GetComponent<StorageBoxGraphic>();
                }
            }

            if (box == null)
            {
                RectTransform childRect = StorageUIUtility.CreateStretchBox(
                    objectName,
                    transform,
                    offsetMin,
                    offsetMax,
                    Color.clear,
                    Color.clear,
                    0f,
                    0f);
                box = childRect.GetComponent<StorageBoxGraphic>();
            }

            RectTransform rect = box.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            box.raycastTarget = false;
            return box;
        }
    }
}
