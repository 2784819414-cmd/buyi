using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NtingCampus.InventoryGrid
{
    [DisallowMultipleComponent]
    public sealed class InventoryGridView : MonoBehaviour
    {
        public InventoryContainerRuntime container;
        public RectTransform gridRoot;
        public InventoryCellView cellPrefab;
        public InventoryItemView itemPrefab;

        public float cellSize = 48f;
        public float cellSpacing = 2f;

        private static readonly List<InventoryGridView> ActiveGrids = new List<InventoryGridView>();

        private readonly List<InventoryCellView> cells = new List<InventoryCellView>();
        private RectTransform cellLayer;
        private RectTransform itemLayer;
        private InventoryWindowController windowController;
        private InventoryRoundedBoxGraphic gridChrome;

        public InventoryContainerRuntime Container => container;

        public InventoryWindowController WindowController => windowController;

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

        public void SetWindowController(InventoryWindowController controller)
        {
            windowController = controller;
        }

        public void Bind(InventoryContainerRuntime container)
        {
            this.container = container;
            Refresh();
        }

        public void Refresh()
        {
            EnsureRootAndLayers();
            ClearLayer(cellLayer);
            ClearLayer(itemLayer);
            cells.Clear();

            if (container == null || container.definition == null)
            {
                Debug.LogWarning("Inventory grid refresh failed: container is not bound.");
                return;
            }

            float width = GetGridPixelWidth(container.definition.width);
            float height = GetGridPixelHeight(container.definition.height);
            gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            cellLayer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            cellLayer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            itemLayer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            itemLayer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            ApplyGridChrome();

            for (int y = 0; y < container.definition.height; y++)
            {
                for (int x = 0; x < container.definition.width; x++)
                {
                    InventoryCellView cell = CreateCellView(cellLayer);
                    ConfigureCellTransform(cell.GetComponent<RectTransform>(), x, y);
                    cell.Bind(x, y);
                    cells.Add(cell);
                }
            }

            SetBaseCellStates();

            if (container.items == null)
            {
                return;
            }

            for (int i = 0; i < container.items.Count; i++)
            {
                PlacedItem placedItem = container.items[i];
                if (placedItem == null || placedItem.item == null || placedItem.item.definition == null)
                {
                    continue;
                }

                InventoryItemView itemView = CreateItemView(itemLayer);
                RectTransform itemRect = itemView.GetComponent<RectTransform>();
                ConfigureItemTransform(itemRect, placedItem);
                itemView.Bind(placedItem, this);
            }
        }

        public Vector2Int ScreenToGridPosition(Vector2 screenPosition)
        {
            EnsureRootAndLayers();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screenPosition, null, out Vector2 localPoint);
            return LocalToGridPosition(localPoint);
        }

        public bool TryGetGridPositionFromPointer(PointerEventData eventData, out Vector2Int gridPos)
        {
            gridPos = default;
            if (eventData == null || gridRoot == null || container == null || container.definition == null)
            {
                return false;
            }

            Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, eventData.position, eventCamera, out Vector2 localPoint))
            {
                return false;
            }

            gridPos = LocalToGridPosition(localPoint);
            return gridPos.x >= 0 &&
                   gridPos.y >= 0 &&
                   gridPos.x < container.definition.width &&
                   gridPos.y < container.definition.height;
        }

        public void ShowPlacementPreview(ItemInstance item, Vector2Int gridPos, bool valid)
        {
            if (container == null || container.definition == null || item == null)
            {
                return;
            }

            SetBaseCellStates();
            for (int y = 0; y < item.CurrentHeight; y++)
            {
                for (int x = 0; x < item.CurrentWidth; x++)
                {
                    InventoryCellView cell = GetCell(gridPos.x + x, gridPos.y + y);
                    if (cell == null)
                    {
                        continue;
                    }

                    if (valid)
                    {
                        cell.SetValidPreview();
                    }
                    else
                    {
                        cell.SetInvalidPreview();
                    }
                }
            }
        }

        public void ClearPlacementPreview()
        {
            SetBaseCellStates();
        }

        public bool CanPreviewPlace(ItemInstance item, Vector2Int gridPos, PlacedItem ignoreItem = null)
        {
            if (container == null || container.definition == null || item == null)
            {
                return false;
            }

            if (!container.IsAreaFree(item, gridPos.x, gridPos.y, ignoreItem))
            {
                return false;
            }

            return container.CanAcceptWeight(item, ignoreItem);
        }

        public static InventoryGridView FindGridUnderPointer(PointerEventData eventData, out Vector2Int gridPos)
        {
            gridPos = default;
            for (int i = ActiveGrids.Count - 1; i >= 0; i--)
            {
                InventoryGridView grid = ActiveGrids[i];
                if (grid == null || !grid.isActiveAndEnabled)
                {
                    continue;
                }

                if (grid.TryGetGridPositionFromPointer(eventData, out gridPos))
                {
                    return grid;
                }
            }

            return null;
        }

        private void EnsureRootAndLayers()
        {
            if (gridRoot == null)
            {
                gridRoot = transform as RectTransform;
            }

            if (gridRoot == null)
            {
                gridRoot = gameObject.AddComponent<RectTransform>();
            }

            ConfigureTopLeftRect(gridRoot);

            if (cellLayer == null)
            {
                cellLayer = FindOrCreateLayer("Cells");
            }

            if (itemLayer == null)
            {
                itemLayer = FindOrCreateLayer("Items");
            }
        }

        private RectTransform FindOrCreateLayer(string layerName)
        {
            Transform existing = gridRoot.Find(layerName);
            if (existing != null && existing is RectTransform existingRect)
            {
                ConfigureTopLeftRect(existingRect);
                return existingRect;
            }

            GameObject layerObject = new GameObject(layerName, typeof(RectTransform));
            RectTransform rectTransform = layerObject.GetComponent<RectTransform>();
            rectTransform.SetParent(gridRoot, false);
            ConfigureTopLeftRect(rectTransform);
            return rectTransform;
        }

        private static void ConfigureTopLeftRect(RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
        }

        private InventoryCellView CreateCellView(RectTransform parent)
        {
            InventoryCellView cell;
            if (cellPrefab != null)
            {
                cell = Instantiate(cellPrefab, parent);
            }
            else
            {
                GameObject cellObject = new GameObject("Cell", typeof(RectTransform), typeof(InventoryRoundedBoxGraphic), typeof(InventoryCellView));
                cellObject.transform.SetParent(parent, false);
                cell = cellObject.GetComponent<InventoryCellView>();
                cell.background = cellObject.GetComponent<InventoryRoundedBoxGraphic>();
            }

            cell.gameObject.SetActive(true);
            return cell;
        }

        private InventoryItemView CreateItemView(RectTransform parent)
        {
            InventoryItemView itemView;
            if (itemPrefab != null)
            {
                itemView = Instantiate(itemPrefab, parent);
            }
            else
            {
                GameObject itemObject = new GameObject("Item", typeof(RectTransform), typeof(InventoryRoundedBoxGraphic), typeof(CanvasGroup), typeof(InventoryItemView));
                itemObject.transform.SetParent(parent, false);
                itemView = itemObject.GetComponent<InventoryItemView>();
            }

            itemView.gameObject.SetActive(true);
            return itemView;
        }

        private void ConfigureCellTransform(RectTransform rectTransform, int x, int y)
        {
            ConfigureTopLeftRect(rectTransform);
            rectTransform.anchoredPosition = new Vector2(x * GetStride(), -y * GetStride());
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize);
        }

        private void ConfigureItemTransform(RectTransform rectTransform, PlacedItem placedItem)
        {
            ConfigureTopLeftRect(rectTransform);
            rectTransform.anchoredPosition = new Vector2(placedItem.x * GetStride(), -placedItem.y * GetStride());
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, GetItemPixelWidth(placedItem.item));
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, GetItemPixelHeight(placedItem.item));
        }

        private float GetStride()
        {
            return cellSize + cellSpacing;
        }

        private float GetGridPixelWidth(int gridWidth)
        {
            return gridWidth <= 0 ? 0f : gridWidth * cellSize + Mathf.Max(0, gridWidth - 1) * cellSpacing;
        }

        private float GetGridPixelHeight(int gridHeight)
        {
            return gridHeight <= 0 ? 0f : gridHeight * cellSize + Mathf.Max(0, gridHeight - 1) * cellSpacing;
        }

        private float GetItemPixelWidth(ItemInstance item)
        {
            return item.CurrentWidth * cellSize + Mathf.Max(0, item.CurrentWidth - 1) * cellSpacing;
        }

        private float GetItemPixelHeight(ItemInstance item)
        {
            return item.CurrentHeight * cellSize + Mathf.Max(0, item.CurrentHeight - 1) * cellSpacing;
        }

        private Vector2Int LocalToGridPosition(Vector2 localPoint)
        {
            int x = Mathf.FloorToInt(localPoint.x / GetStride());
            int y = Mathf.FloorToInt(-localPoint.y / GetStride());
            return new Vector2Int(x, y);
        }

        private InventoryCellView GetCell(int x, int y)
        {
            if (container == null || container.definition == null || x < 0 || y < 0 ||
                x >= container.definition.width || y >= container.definition.height)
            {
                return null;
            }

            int index = y * container.definition.width + x;
            return index >= 0 && index < cells.Count ? cells[index] : null;
        }

        private void SetBaseCellStates()
        {
            for (int i = 0; i < cells.Count; i++)
            {
                InventoryCellView cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                if (container != null && container.IsCellOccupied(cell.x, cell.y))
                {
                    cell.SetOccupied();
                }
                else
                {
                    cell.SetNormal();
                }
            }
        }

        private static void ClearLayer(RectTransform layer)
        {
            if (layer == null)
            {
                return;
            }

            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                Transform child = layer.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void ApplyGridChrome()
        {
            if (gridChrome == null)
            {
                gridChrome = gridRoot.GetComponent<InventoryRoundedBoxGraphic>();
            }

            if (gridChrome == null)
            {
                gridChrome = gridRoot.gameObject.AddComponent<InventoryRoundedBoxGraphic>();
            }

            gridChrome.raycastTarget = false;
            gridChrome.FillColor = new Color(0.055f, 0.066f, 0.078f, 0.82f);
            gridChrome.BorderColor = new Color(0.28f, 0.36f, 0.42f, 0.95f);
            gridChrome.BorderWidth = 2f;
            gridChrome.CornerRadius = 8f;
            gridChrome.CornerSegments = 5;
            gridChrome.SetAllDirty();
        }
    }
}
