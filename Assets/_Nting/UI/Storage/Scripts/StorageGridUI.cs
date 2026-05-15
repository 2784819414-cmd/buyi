using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageGridUI : MonoBehaviour
    {
        private static readonly List<StorageGridUI> ActiveGrids = new List<StorageGridUI>();

        public StorageSlotUI SlotPrefab;
        public StorageItemView ItemPrefab;
        public RectTransform SlotsRoot;
        public RectTransform ItemsRoot;
        public float CellSize = 52f;
        public float CellSpacing = 4f;

        private readonly List<StorageSlotUI> slots = new List<StorageSlotUI>();
        private readonly List<StorageItemView> itemViews = new List<StorageItemView>();
        private StorageContainerModel container;
        private StorageWindowUI window;
        private RectTransform rectTransform;

        public StorageContainerModel Container => container;

        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = GetComponent<RectTransform>();
                }

                return rectTransform;
            }
        }

        private void OnEnable()
        {
            if (!ActiveGrids.Contains(this))
            {
                ActiveGrids.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveGrids.Remove(this);
        }

        public void Bind(StorageContainerModel model, StorageWindowUI ownerWindow)
        {
            container = model;
            window = ownerWindow;
            Rebuild();
        }

        public void Rebuild()
        {
            EnsureRoots();
            ClearChildren(SlotsRoot);
            ClearChildren(ItemsRoot);
            slots.Clear();
            itemViews.Clear();

            if (container == null)
            {
                RectTransform.sizeDelta = Vector2.zero;
                return;
            }

            RectTransform.sizeDelta = GetGridPixelSize(container.Columns, container.Rows);
            SlotsRoot.sizeDelta = RectTransform.sizeDelta;
            ItemsRoot.sizeDelta = RectTransform.sizeDelta;

            for (int y = 0; y < container.Rows; y++)
            {
                for (int x = 0; x < container.Columns; x++)
                {
                    StorageSlotUI slot = CreateSlot(x, y);
                    slots.Add(slot);
                }
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null)
                {
                    CreateItemView(item);
                }
            }

            RefreshSlotStates();
        }

        public bool CanPlace(StorageItemModel item, int x, int y)
        {
            if (container == null)
            {
                return false;
            }

            StorageItemModel ignoreItem = container.Contains(item) ? item : null;
            return container.CanPlace(item, x, y, ignoreItem);
        }

        public bool PlaceItem(StorageItemModel item, int x, int y)
        {
            return container != null && container.PlaceItem(item, x, y);
        }

        public bool RemoveItem(StorageItemModel item)
        {
            return container != null && container.RemoveItem(item);
        }

        public bool FindFirstFit(StorageItemModel item, out Vector2Int position)
        {
            position = default;
            return container != null && container.FindFirstFit(item, out position);
        }

        public bool IsCellOccupied(int x, int y)
        {
            return container != null && container.IsCellOccupied(x, y);
        }

        public bool TryGetCellFromScreenPoint(Vector2 screenPoint, Camera eventCamera, out Vector2Int cell)
        {
            cell = default;
            if (container == null)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, screenPoint, eventCamera, out Vector2 localPoint))
            {
                return false;
            }

            Rect rect = RectTransform.rect;
            float fromLeft = localPoint.x - rect.xMin;
            float fromTop = rect.yMax - localPoint.y;
            float step = CellSize + CellSpacing;
            int x = Mathf.FloorToInt((fromLeft + step * 0.5f) / step);
            int y = Mathf.FloorToInt((fromTop + step * 0.5f) / step);
            cell = new Vector2Int(x, y);
            return true;
        }

        public void HighlightFootprint(int x, int y, int width, int height, bool valid)
        {
            RefreshSlotStates();
            if (container == null)
            {
                return;
            }

            int minX = Mathf.Max(0, x);
            int minY = Mathf.Max(0, y);
            int maxX = Mathf.Min(container.Columns, x + Mathf.Max(1, width));
            int maxY = Mathf.Min(container.Rows, y + Mathf.Max(1, height));

            for (int cellY = minY; cellY < maxY; cellY++)
            {
                for (int cellX = minX; cellX < maxX; cellX++)
                {
                    StorageSlotUI slot = GetSlot(cellX, cellY);
                    if (slot != null)
                    {
                        slot.SetPreview(valid);
                    }
                }
            }
        }

        public void ClearHighlight()
        {
            RefreshSlotStates();
        }

        public void RefreshSlotStates()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                StorageSlotUI slot = slots[i];
                if (slot != null)
                {
                    slot.SetNormal(container != null && container.IsCellOccupied(slot.X, slot.Y));
                }
            }
        }

        public void SetItemsRaycast(bool enabled)
        {
            for (int i = 0; i < itemViews.Count; i++)
            {
                if (itemViews[i] != null)
                {
                    itemViews[i].SetRaycastEnabled(enabled);
                }
            }
        }

        public Vector2 GetGridPixelSize(int columns, int rows)
        {
            return new Vector2(
                Mathf.Max(0, columns) * CellSize + Mathf.Max(0, columns - 1) * CellSpacing,
                Mathf.Max(0, rows) * CellSize + Mathf.Max(0, rows - 1) * CellSpacing);
        }

        public static StorageGridUI FindGridUnderPointer(Vector2 screenPoint, Camera eventCamera)
        {
            for (int i = ActiveGrids.Count - 1; i >= 0; i--)
            {
                StorageGridUI grid = ActiveGrids[i];
                if (grid == null || !grid.isActiveAndEnabled || grid.container == null)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(grid.RectTransform, screenPoint, eventCamera))
                {
                    return grid;
                }
            }

            return null;
        }

        private void EnsureRoots()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            StorageUIUtility.SetAnchor(rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            if (SlotsRoot == null)
            {
                SlotsRoot = CreateLayer("SlotsRoot");
            }

            if (ItemsRoot == null)
            {
                ItemsRoot = CreateLayer("ItemsRoot");
            }
        }

        private RectTransform CreateLayer(string layerName)
        {
            GameObject layer = StorageUIUtility.CreateRectObject(layerName, transform);
            RectTransform rect = layer.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private StorageSlotUI CreateSlot(int x, int y)
        {
            StorageSlotUI slot;
            if (SlotPrefab != null)
            {
                slot = Instantiate(SlotPrefab, SlotsRoot);
                slot.name = "Slot_" + x + "_" + y;
            }
            else
            {
                GameObject slotObject = new GameObject("Slot_" + x + "_" + y, typeof(RectTransform), typeof(CanvasRenderer), typeof(StorageBoxGraphic), typeof(StorageSlotUI));
                slotObject.transform.SetParent(SlotsRoot, false);
                slot = slotObject.GetComponent<StorageSlotUI>();
                slot.Background = slotObject.GetComponent<StorageBoxGraphic>();
            }

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(slotRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            slotRect.sizeDelta = new Vector2(CellSize, CellSize);
            slotRect.anchoredPosition = new Vector2(x * (CellSize + CellSpacing), -y * (CellSize + CellSpacing));
            slot.Bind(this, x, y);
            return slot;
        }

        private void CreateItemView(StorageItemModel item)
        {
            StorageItemView itemView;
            if (ItemPrefab != null)
            {
                itemView = Instantiate(ItemPrefab, ItemsRoot);
                itemView.name = "Item_" + item.DisplayName;
            }
            else
            {
                GameObject itemObject = new GameObject("Item_" + item.DisplayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(StorageBoxGraphic), typeof(CanvasGroup), typeof(StorageItemView));
                itemObject.transform.SetParent(ItemsRoot, false);
                itemView = itemObject.GetComponent<StorageItemView>();
            }

            itemViews.Add(itemView);
            itemView.Bind(item, this, window);
            PositionItemView(itemView);
        }

        private void PositionItemView(StorageItemView itemView)
        {
            RectTransform itemRect = itemView.RectTransform;
            StorageUIUtility.SetAnchor(itemRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            itemRect.sizeDelta = new Vector2(
                itemView.Item.CurrentWidth * CellSize + (itemView.Item.CurrentWidth - 1) * CellSpacing,
                itemView.Item.CurrentHeight * CellSize + (itemView.Item.CurrentHeight - 1) * CellSpacing);
            itemRect.anchoredPosition = new Vector2(
                itemView.Item.X * (CellSize + CellSpacing),
                -itemView.Item.Y * (CellSize + CellSpacing));
            itemView.RefreshVisual();
        }

        private StorageSlotUI GetSlot(int x, int y)
        {
            if (container == null || x < 0 || y < 0 || x >= container.Columns || y >= container.Rows)
            {
                return null;
            }

            int index = y * container.Columns + x;
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
