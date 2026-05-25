using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusHudHandSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private CampusCharacterRuntime actor;
        [SerializeField] private StorageContainerModel handContainer;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private RectTransform sourceRect;
        [SerializeField] private RectTransform tooltipRoot;

        private StorageItemTooltipUI tooltip;
        private Image ghostImage;
        private RectTransform ghostRect;

        private Camera EventCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        public void Configure(
            CampusCharacterRuntime targetActor,
            StorageContainerModel targetHandContainer,
            Canvas targetCanvas,
            RectTransform targetDragLayer,
            RectTransform targetSourceRect,
            StorageItemTooltipUI targetTooltip,
            RectTransform targetTooltipRoot,
            bool enabled)
        {
            actor = targetActor;
            handContainer = targetHandContainer;
            canvas = targetCanvas;
            dragLayer = targetDragLayer;
            sourceRect = targetSourceRect;
            tooltip = targetTooltip;
            tooltipRoot = targetTooltipRoot;
            gameObject.SetActive(enabled);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            StorageItemModel item = ResolveHeldItem();
            if (item == null || dragLayer == null)
            {
                return;
            }

            HideTooltip();
            CreateGhost(item);
            UpdateGhostPosition(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateGhostPosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            bool shouldDrop = ghostImage != null &&
                              sourceRect != null &&
                              !RectTransformUtility.RectangleContainsScreenPoint(
                                  sourceRect,
                                  eventData.position,
                                  EventCamera);
            DestroyGhost();
            if (!shouldDrop)
            {
                return;
            }

            StorageItemModel item = ResolveHeldItem();
            if (item == null)
            {
                return;
            }

            CampusInventoryActionExecutor.TryDropItemToGround(
                actor,
                item,
                handContainer,
                actor != null ? actor.gameObject : gameObject,
                out _);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowTooltip(eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            MoveTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        private StorageItemModel ResolveHeldItem()
        {
            return handContainer != null &&
                   handContainer.Items != null &&
                   handContainer.Items.Count > 0
                ? handContainer.Items[0]
                : null;
        }

        private void CreateGhost(StorageItemModel item)
        {
            DestroyGhost();
            GameObject ghostObject = new GameObject("HandSlotDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghostObject.transform.SetParent(dragLayer, false);

            ghostRect = ghostObject.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(ghostRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ghostRect.anchoredPosition = Vector2.zero;
            ghostRect.sizeDelta = new Vector2(64f, 64f);
            ghostRect.SetAsLastSibling();

            ghostImage = ghostObject.GetComponent<Image>();
            ghostImage.raycastTarget = false;
            ghostImage.preserveAspect = true;
            ghostImage.sprite = StorageItemIconUtility.Resolve(item);
            ghostImage.color = ghostImage.sprite != null ? Color.white : Color.Lerp(item.ThemeColor, Color.white, 0.15f);
        }

        private void UpdateGhostPosition(Vector2 screenPosition)
        {
            if (ghostRect == null || dragLayer == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer,
                screenPosition,
                EventCamera,
                out Vector2 localPoint);
            ghostRect.anchoredPosition = localPoint;
        }

        private void ShowTooltip(Vector2 screenPosition)
        {
            StorageItemModel item = ResolveHeldItem();
            if (tooltip == null || tooltipRoot == null || item == null)
            {
                return;
            }

            tooltip.ShowAnchoredCompact(item, handContainer, tooltipRoot, sourceRect, EventCamera);
        }

        private void MoveTooltip()
        {
            if (tooltip != null && tooltipRoot != null && sourceRect != null)
            {
                tooltip.MoveAnchored(tooltipRoot, sourceRect, EventCamera);
            }
        }

        private void HideTooltip()
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
        }

        private void DestroyGhost()
        {
            if (ghostRect != null)
            {
                Destroy(ghostRect.gameObject);
            }

            ghostImage = null;
            ghostRect = null;
        }
    }
}
