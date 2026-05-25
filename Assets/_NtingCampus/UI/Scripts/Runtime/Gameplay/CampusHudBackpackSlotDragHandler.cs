using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusHudBackpackSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private CampusCharacterRuntime actor;
        [SerializeField] private StorageContainerModel equipmentSlot;
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
            StorageContainerModel targetEquipmentSlot,
            Canvas targetCanvas,
            RectTransform targetDragLayer,
            RectTransform targetSourceRect,
            StorageItemTooltipUI targetTooltip,
            RectTransform targetTooltipRoot)
        {
            actor = targetActor;
            equipmentSlot = targetEquipmentSlot;
            canvas = targetCanvas;
            dragLayer = targetDragLayer;
            sourceRect = targetSourceRect;
            tooltip = targetTooltip;
            tooltipRoot = targetTooltipRoot;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            StorageItemModel backpack = CampusBackpackInventoryUtility.ResolveEquippedBackpack(equipmentSlot);
            if (backpack == null || dragLayer == null)
            {
                return;
            }

            HideTooltip();
            CreateGhost(backpack);
            UpdateGhostPosition(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateGhostPosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            RectTransform slotRect = sourceRect != null ? sourceRect : transform as RectTransform;
            bool shouldDrop = ghostImage != null &&
                              slotRect != null &&
                              !RectTransformUtility.RectangleContainsScreenPoint(
                                  slotRect,
                                  eventData.position,
                                  EventCamera);
            DestroyGhost();
            if (!shouldDrop)
            {
                return;
            }

            CampusBackpackEquipmentService.TryDropEquippedBackpack(
                actor,
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

        private void CreateGhost(StorageItemModel backpack)
        {
            DestroyGhost();
            GameObject ghostObject = new GameObject("BackpackDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghostObject.transform.SetParent(dragLayer, false);
            ghostRect = ghostObject.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(ghostRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ghostRect.anchoredPosition = Vector2.zero;
            ghostRect.sizeDelta = new Vector2(64f, 64f);
            ghostRect.SetAsLastSibling();

            ghostImage = ghostObject.GetComponent<Image>();
            ghostImage.raycastTarget = false;
            ghostImage.preserveAspect = true;
            ghostImage.sprite = StorageItemIconUtility.Resolve(backpack);
            ghostImage.color = ghostImage.sprite != null ? Color.white : Color.Lerp(backpack.ThemeColor, Color.white, 0.15f);
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
            StorageItemModel backpack = CampusBackpackInventoryUtility.ResolveEquippedBackpack(equipmentSlot);
            if (tooltip == null || tooltipRoot == null || backpack == null)
            {
                return;
            }

            tooltip.ShowAnchoredCompact(backpack, equipmentSlot, tooltipRoot, sourceRect, EventCamera);
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
