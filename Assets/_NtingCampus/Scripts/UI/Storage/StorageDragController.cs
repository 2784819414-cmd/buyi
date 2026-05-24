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
        private StorageItemView dragGhostView;
        private StorageGridUI sourceGrid;
        private int sourceX;
        private int sourceY;
        private Vector2 dragPointerOffset;
        private Vector2Int grabCellOffset;
        private StorageGridUI previewGrid;
        private StorageGridUI currentTargetGrid;
        private Vector2Int currentTargetCell;
        private bool currentPlacementValid;

        public bool IsDragging => currentView != null;

        private Camera EventCamera => Canvas != null && Canvas.renderMode != RenderMode.ScreenSpaceOverlay ? Canvas.worldCamera : null;

        public void BeginDrag(StorageItemView view, PointerEventData eventData)
        {
            if (view == null || view.Item == null || view.OwnerGrid == null || DragLayer == null || Window == null)
            {
                return;
            }

            currentView = view;
            sourceGrid = view.OwnerGrid;
            sourceX = view.Item.X;
            sourceY = view.Item.Y;
            currentTargetGrid = null;
            currentPlacementValid = false;

            Window.SelectItem(view.Item, sourceGrid);
            view.SetRaycastEnabled(false);
            view.SetHiddenWhileDragging(true);

            Vector2 dragSize = ResolveDragGhostSize(view);
            Vector2 itemLocalPoint = ResolveSourceLocalPoint(view.RectTransform, eventData.position);
            dragPointerOffset = ResolveDragPointerOffset(view.RectTransform, itemLocalPoint, dragSize);
            grabCellOffset = ResolveGrabCellOffset(view, itemLocalPoint);

            dragGhostView = CreateDragGhost(view, dragSize);
            if (dragGhostView == null)
            {
                currentView = null;
                sourceGrid = null;
                dragPointerOffset = Vector2.zero;
                grabCellOffset = Vector2Int.zero;
                view.SetRaycastEnabled(true);
                view.SetHiddenWhileDragging(false);
                return;
            }

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
            if (currentView == null || Window == null)
            {
                return;
            }

            StorageItemModel item = currentView.Item;
            bool placedInGrid = currentPlacementValid &&
                                currentTargetGrid != null &&
                                TryCommitPlacement(item, currentTargetGrid, currentTargetCell);

            bool droppedToGround = !placedInGrid &&
                                   currentTargetGrid == null &&
                                   Window.TryDropItemToGround(item, sourceGrid);

            if (!placedInGrid && !droppedToGround)
            {
                item.X = sourceX;
                item.Y = sourceY;
                if (currentTargetGrid != null)
                {
                    Window.ShowStatus(StorageTextCatalog.Get(StorageTextId.TargetSpaceInsufficient), true);
                }
            }
            else if (placedInGrid)
            {
                Window.ShowStatus(StorageTextCatalog.Format(StorageTextId.MovedItem, item.GetDisplayName()), false);
            }

            CleanupDraggedView();
            Window.RefreshAllGrids();
            Window.SelectItem(droppedToGround ? null : item);
        }

        public void CancelDrag()
        {
            if (currentView == null)
            {
                return;
            }

            currentView.Item.X = sourceX;
            currentView.Item.Y = sourceY;
            CleanupDraggedView();
            if (Window != null)
            {
                Window.RefreshAllGrids();
            }
        }

        private void CleanupDraggedView()
        {
            if (currentView == null)
            {
                return;
            }

            GameObject draggedObject = dragGhostView != null ? dragGhostView.gameObject : null;
            StorageItemView sourceView = currentView;

            ClearPreview();
            if (sourceView != null)
            {
                sourceView.SetRaycastEnabled(true);
                sourceView.SetHiddenWhileDragging(false);
            }

            dragGhostView = null;
            currentView = null;
            sourceGrid = null;
            currentTargetGrid = null;
            currentPlacementValid = false;
            dragPointerOffset = Vector2.zero;
            grabCellOffset = Vector2Int.zero;

            if (draggedObject != null)
            {
                draggedObject.SetActive(false);
                Destroy(draggedObject);
            }
        }

        private void UpdateDraggedPosition(Vector2 screenPosition)
        {
            if (dragGhostView == null || DragLayer == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, screenPosition, EventCamera, out Vector2 localPoint);
            dragGhostView.RectTransform.anchoredPosition = localPoint - dragPointerOffset;
        }

        private void UpdatePreview(Vector2 pointerScreenPosition)
        {
            ClearPreview();

            StorageGridUI pointerGrid = StorageGridUI.FindGridUnderPointer(pointerScreenPosition, EventCamera);
            StorageGridUI overlapHandGrid = dragGhostView != null
                ? StorageGridUI.FindSingleItemSlotOverlapping(dragGhostView.RectTransform, EventCamera)
                : null;
            StorageGridUI grid = ResolveTargetGrid(pointerGrid, overlapHandGrid);
            currentTargetGrid = grid;
            currentPlacementValid = false;

            if (grid == null || currentView == null)
            {
                return;
            }

            if (grid.IsSingleItemSlot)
            {
                currentTargetCell = Vector2Int.zero;
                currentPlacementValid = grid.CanPlace(currentView.Item, 0, 0);
                grid.HighlightFootprint(0, 0, 1, 1, currentPlacementValid);
                previewGrid = grid;
                return;
            }

            if (!grid.TryGetCellFromScreenPoint(pointerScreenPosition, EventCamera, out Vector2Int pointerCell))
            {
                return;
            }

            Vector2Int cell = new Vector2Int(
                pointerCell.x - grabCellOffset.x,
                pointerCell.y - grabCellOffset.y);
            currentTargetCell = cell;
            currentPlacementValid = grid.CanPlace(currentView.Item, cell.x, cell.y);
            grid.HighlightFootprint(cell.x, cell.y, currentView.Item.CurrentWidth, currentView.Item.CurrentHeight, currentPlacementValid);
            previewGrid = grid;
        }

        private static StorageGridUI ResolveTargetGrid(StorageGridUI pointerGrid, StorageGridUI overlapHandGrid)
        {
            if (overlapHandGrid == null)
            {
                return pointerGrid;
            }

            if (pointerGrid == null || pointerGrid.IsSingleItemSlot)
            {
                return overlapHandGrid;
            }

            return pointerGrid;
        }

        private bool TryCommitPlacement(StorageItemModel item, StorageGridUI targetGrid, Vector2Int cell)
        {
            if (targetGrid == null || item == null)
            {
                return false;
            }

            bool moved = Window.TryMoveItem(
                item,
                sourceGrid,
                targetGrid,
                cell.x,
                cell.y);
            return moved;
        }

        private StorageItemView CreateDragGhost(StorageItemView sourceView, Vector2 dragSize)
        {
            if (sourceView == null || DragLayer == null)
            {
                return null;
            }

            GameObject ghostObject = Instantiate(sourceView.gameObject, DragLayer);
            ghostObject.name = sourceView.gameObject.name + "_DragGhost";

            StorageItemView ghostView = ghostObject.GetComponent<StorageItemView>();
            if (ghostView == null)
            {
                Destroy(ghostObject);
                return null;
            }

            RectTransform ghostRect = ghostView.RectTransform;
            ghostView.Bind(sourceView.Item, sourceView.OwnerGrid, Window);
            ConfigureDragGhostRect(ghostRect, dragSize);
            ghostView.SetRaycastEnabled(false);
            ghostView.SetDragVisual(true);
            return ghostView;
        }

        private void ConfigureDragGhostRect(RectTransform ghostRect, Vector2 size)
        {
            if (ghostRect == null || DragLayer == null)
            {
                return;
            }

            ghostRect.SetParent(DragLayer, false);
            StorageUIUtility.SetAnchor(ghostRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ghostRect.anchoredPosition = Vector2.zero;
            ghostRect.sizeDelta = size;
            ghostRect.SetAsLastSibling();
        }

        private Vector2 ResolveDragGhostSize(StorageItemView sourceView)
        {
            if (sourceView == null || sourceView.Item == null)
            {
                return Vector2.zero;
            }

            StorageGridUI grid = sourceView.OwnerGrid;
            StorageContainerModel container = grid != null ? grid.Container : null;
            if (grid != null && container != null && container.IsSingleItemSlot)
            {
                return grid.GetGridPixelSize(sourceView.Item.CurrentWidth, sourceView.Item.CurrentHeight);
            }

            RectTransform sourceRect = sourceView.RectTransform;
            return sourceRect != null ? sourceRect.sizeDelta : Vector2.zero;
        }

        private static Vector2 ResolveSourceLocalPoint(RectTransform sourceRect, Vector2 screenPosition)
        {
            if (sourceRect == null)
            {
                return Vector2.zero;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                sourceRect,
                screenPosition,
                ResolveEventCamera(sourceRect),
                out Vector2 localPoint);
            return localPoint;
        }

        private static Vector2 ResolveDragPointerOffset(RectTransform sourceRect, Vector2 sourceLocalPoint, Vector2 dragSize)
        {
            if (sourceRect == null || dragSize == Vector2.zero)
            {
                return Vector2.zero;
            }

            Rect source = sourceRect.rect;
            float normalizedX = source.width > 0f
                ? Mathf.Clamp01((sourceLocalPoint.x - source.xMin) / source.width)
                : 0.5f;
            float normalizedY = source.height > 0f
                ? Mathf.Clamp01((sourceLocalPoint.y - source.yMin) / source.height)
                : 0.5f;

            return new Vector2(
                (normalizedX - 0.5f) * dragSize.x,
                (normalizedY - 0.5f) * dragSize.y);
        }

        private static Camera ResolveEventCamera(RectTransform rect)
        {
            Canvas sourceCanvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
            return sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? sourceCanvas.worldCamera
                : null;
        }

        private Vector2Int ResolveGrabCellOffset(StorageItemView view, Vector2 itemLocalPoint)
        {
            if (view == null || view.Item == null || view.OwnerGrid == null || view.OwnerGrid.Container == null)
            {
                return Vector2Int.zero;
            }

            if (view.OwnerGrid.Container.IsSingleItemSlot)
            {
                return Vector2Int.zero;
            }

            Rect rect = view.RectTransform.rect;
            float fromLeft = Mathf.Clamp(itemLocalPoint.x - rect.xMin, 0f, rect.width);
            float fromTop = Mathf.Clamp(rect.yMax - itemLocalPoint.y, 0f, rect.height);
            float step = view.OwnerGrid.CellSize + view.OwnerGrid.CellSpacing;
            if (step <= 0f)
            {
                return Vector2Int.zero;
            }

            int x = Mathf.Clamp(Mathf.FloorToInt(fromLeft / step), 0, view.Item.CurrentWidth - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(fromTop / step), 0, view.Item.CurrentHeight - 1);
            return new Vector2Int(x, y);
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
