using UnityEngine;
using UnityEngine.EventSystems;

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageDragController : MonoBehaviour
    {
        public StorageWindowUI Window;
        public RectTransform DragLayer;
        public Canvas Canvas;

        private StorageItemView currentView;
        private StorageGridUI sourceGrid;
        private int sourceX;
        private int sourceY;
        private Vector2 grabOffset;
        private StorageGridUI previewGrid;
        private StorageGridUI currentTargetGrid;
        private Vector2Int currentTargetCell;
        private bool currentPlacementValid;

        public bool IsDragging => currentView != null;

        private Camera EventCamera => Canvas != null && Canvas.renderMode != RenderMode.ScreenSpaceOverlay ? Canvas.worldCamera : null;

        public void BeginDrag(StorageItemView view, PointerEventData eventData)
        {
            if (view == null || view.Item == null || view.OwnerGrid == null || DragLayer == null)
            {
                return;
            }

            currentView = view;
            sourceGrid = view.OwnerGrid;
            sourceX = view.Item.X;
            sourceY = view.Item.Y;
            currentTargetGrid = null;
            currentPlacementValid = false;

            Window.SelectItem(view.Item);
            sourceGrid.SetItemsRaycast(false);

            RectTransform itemRect = view.RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(itemRect, eventData.position, EventCamera, out Vector2 itemLocalPoint);
            Rect itemRectBounds = itemRect.rect;
            grabOffset = new Vector2(
                itemLocalPoint.x - itemRectBounds.xMin,
                itemRectBounds.yMax - itemLocalPoint.y);

            itemRect.SetParent(DragLayer, false);
            StorageUIUtility.SetAnchor(itemRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            itemRect.SetAsLastSibling();
            view.SetDragVisual(true);
            UpdateDraggedPosition(eventData.position);
            UpdatePreview(eventData.position);
        }

        public void Drag(PointerEventData eventData)
        {
            if (currentView == null)
            {
                return;
            }

            UpdateDraggedPosition(eventData.position);
            UpdatePreview(eventData.position);
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (currentView == null)
            {
                return;
            }

            StorageItemModel item = currentView.Item;
            bool placed = currentPlacementValid &&
                          currentTargetGrid != null &&
                          TryCommitPlacement(item, currentTargetGrid, currentTargetCell);

            if (!placed)
            {
                item.X = sourceX;
                item.Y = sourceY;
                Window.ShowStatus("目标空间不足", true);
            }
            else
            {
                Window.ShowStatus("已移动 " + item.DisplayName, false);
            }

            GameObject draggedObject = currentView.gameObject;
            StorageGridUI gridToRestore = sourceGrid;

            ClearPreview();
            currentView.SetDragVisual(false);
            if (gridToRestore != null)
            {
                gridToRestore.SetItemsRaycast(true);
            }

            currentView = null;
            sourceGrid = null;
            currentTargetGrid = null;
            currentPlacementValid = false;
            if (draggedObject != null)
            {
                draggedObject.SetActive(false);
                Destroy(draggedObject);
            }

            Window.RefreshAllGrids();
            Window.SelectItem(item);
        }

        public void CancelDrag()
        {
            if (currentView == null)
            {
                return;
            }

            GameObject draggedObject = currentView.gameObject;
            StorageGridUI gridToRestore = sourceGrid;

            currentView.Item.X = sourceX;
            currentView.Item.Y = sourceY;
            ClearPreview();
            currentView.SetDragVisual(false);
            if (gridToRestore != null)
            {
                gridToRestore.SetItemsRaycast(true);
            }

            currentView = null;
            sourceGrid = null;
            currentTargetGrid = null;
            currentPlacementValid = false;
            if (draggedObject != null)
            {
                draggedObject.SetActive(false);
                Destroy(draggedObject);
            }

            Window.RefreshAllGrids();
        }

        private void UpdateDraggedPosition(Vector2 screenPosition)
        {
            if (currentView == null || DragLayer == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, screenPosition, EventCamera, out Vector2 localPoint);
            Rect dragRect = DragLayer.rect;
            currentView.RectTransform.anchoredPosition = new Vector2(
                localPoint.x - grabOffset.x - dragRect.xMin,
                localPoint.y + grabOffset.y - dragRect.yMax);
        }

        private void UpdatePreview(Vector2 pointerScreenPosition)
        {
            ClearPreview();

            StorageGridUI grid = StorageGridUI.FindGridUnderPointer(pointerScreenPosition, EventCamera);
            currentTargetGrid = grid;
            currentPlacementValid = false;

            if (grid == null || currentView == null)
            {
                return;
            }

            Vector2 topLeftScreenPoint = GetDraggedItemTopLeftScreenPoint();
            if (!grid.TryGetCellFromScreenPoint(topLeftScreenPoint, EventCamera, out Vector2Int cell))
            {
                return;
            }

            currentTargetCell = cell;
            currentPlacementValid = grid.CanPlace(currentView.Item, cell.x, cell.y);
            grid.HighlightFootprint(cell.x, cell.y, currentView.Item.CurrentWidth, currentView.Item.CurrentHeight, currentPlacementValid);
            previewGrid = grid;
        }

        private bool TryCommitPlacement(StorageItemModel item, StorageGridUI targetGrid, Vector2Int cell)
        {
            if (targetGrid == sourceGrid)
            {
                return targetGrid.PlaceItem(item, cell.x, cell.y);
            }

            if (!targetGrid.PlaceItem(item, cell.x, cell.y))
            {
                return false;
            }

            if (sourceGrid.RemoveItem(item))
            {
                return true;
            }

            targetGrid.RemoveItem(item);
            sourceGrid.PlaceItem(item, sourceX, sourceY);
            return false;
        }

        private Vector2 GetDraggedItemTopLeftScreenPoint()
        {
            Vector3[] corners = new Vector3[4];
            currentView.RectTransform.GetWorldCorners(corners);
            return RectTransformUtility.WorldToScreenPoint(EventCamera, corners[1]);
        }

        private void ClearPreview()
        {
            if (previewGrid != null)
            {
                previewGrid.ClearHighlight();
                previewGrid = null;
            }
        }
    }
}
