using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NtingCampus.InventoryGrid
{
    [DisallowMultipleComponent]
    public sealed class InventoryItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public PlacedItem placedItem;
        public InventoryGridView ownerGrid;
        public InventoryRoundedBoxGraphic backgroundGraphic;
        public InventoryRoundedBoxGraphic accentGraphic;
        public Image iconImage;
        public Text stackCountText;
        public Text labelText;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private InventoryGridView dragSourceGrid;
        private InventoryGridView previewGrid;
        private Transform originalParent;
        private Vector2 originalAnchoredPosition;
        private Coroutine feedbackRoutine;

        private void Awake()
        {
            EnsureReferences();
        }

        public void Bind(PlacedItem placedItem, InventoryGridView ownerGrid)
        {
            EnsureReferences();
            this.placedItem = placedItem;
            this.ownerGrid = ownerGrid;
            RefreshVisuals();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!HasValidBinding())
            {
                return;
            }

            dragSourceGrid = ownerGrid;
            originalParent = transform.parent;
            originalAnchoredPosition = rectTransform.anchoredPosition;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.88f;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                transform.SetParent(canvas.transform, true);
                transform.SetAsLastSibling();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!HasValidBinding())
            {
                return;
            }

            rectTransform.position = eventData.position;
            InventoryGridView targetGrid = InventoryGridView.FindGridUnderPointer(eventData, out Vector2Int gridPosition);
            if (previewGrid != null && previewGrid != targetGrid)
            {
                previewGrid.ClearPlacementPreview();
            }

            previewGrid = targetGrid;
            if (targetGrid == null)
            {
                return;
            }

            PlacedItem ignoreItem = targetGrid == dragSourceGrid ? placedItem : null;
            bool valid = targetGrid.CanPreviewPlace(placedItem.item, gridPosition, ignoreItem);
            targetGrid.ShowPlacementPreview(placedItem.item, gridPosition, valid);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!HasValidBinding())
            {
                return;
            }

            InventoryGridView targetGrid = InventoryGridView.FindGridUnderPointer(eventData, out Vector2Int gridPosition);
            if (previewGrid != null)
            {
                previewGrid.ClearPlacementPreview();
                previewGrid = null;
            }

            bool placed = false;
            if (targetGrid != null)
            {
                placed = TryDropIntoGrid(targetGrid, gridPosition);
            }

            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            transform.SetParent(originalParent, false);
            rectTransform.anchoredPosition = originalAnchoredPosition;

            if (placed)
            {
                RefreshAllRelatedGrids(targetGrid);
            }
            else
            {
                PlayInvalidFeedback();
            }

            dragSourceGrid = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!HasValidBinding())
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                TryRotate();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (eventData.clickCount >= 2 || IsShiftPressed())
            {
                InventoryWindowController controller = ownerGrid.WindowController != null
                    ? ownerGrid.WindowController
                    : GetComponentInParent<InventoryWindowController>();
                if (controller == null || !controller.TryQuickTransfer(placedItem, ownerGrid))
                {
                    Debug.LogWarning("Inventory quick transfer failed: no target container could accept the item.");
                    PlayInvalidFeedback();
                }
            }
        }

        private bool TryDropIntoGrid(InventoryGridView targetGrid, Vector2Int gridPosition)
        {
            if (targetGrid == null || targetGrid.Container == null || dragSourceGrid == null || dragSourceGrid.Container == null)
            {
                Debug.LogWarning("Inventory drop failed: source or target container is not bound.");
                return false;
            }

            if (targetGrid == dragSourceGrid)
            {
                return dragSourceGrid.Container.TryMoveItem(placedItem, gridPosition.x, gridPosition.y);
            }

            if (!targetGrid.Container.TryPlaceItem(placedItem.item, gridPosition.x, gridPosition.y))
            {
                return false;
            }

            if (dragSourceGrid.Container.RemoveItem(placedItem))
            {
                return true;
            }

            PlacedItem rollbackItem = targetGrid.Container.FindPlacedItem(placedItem.item);
            if (rollbackItem != null)
            {
                targetGrid.Container.RemoveItem(rollbackItem);
            }

            Debug.LogWarning("Inventory drop failed: source remove failed, target placement rolled back.");
            return false;
        }

        private void TryRotate()
        {
            if (ownerGrid == null || ownerGrid.Container == null)
            {
                Debug.LogWarning("Inventory rotate failed: owner grid is not bound.");
                PlayInvalidFeedback();
                return;
            }

            if (ownerGrid.Container.TryRotateItem(placedItem))
            {
                RefreshAllRelatedGrids(ownerGrid);
            }
            else
            {
                PlayInvalidFeedback();
            }
        }

        private void RefreshAllRelatedGrids(InventoryGridView targetGrid)
        {
            InventoryWindowController controller = ownerGrid != null ? ownerGrid.WindowController : null;
            if (controller == null && targetGrid != null)
            {
                controller = targetGrid.WindowController;
            }

            if (controller != null)
            {
                controller.RefreshAll();
                return;
            }

            if (ownerGrid != null)
            {
                ownerGrid.Refresh();
            }

            if (targetGrid != null && targetGrid != ownerGrid)
            {
                targetGrid.Refresh();
            }
        }

        private void RefreshVisuals()
        {
            EnsureReferences();
            if (!HasValidBinding())
            {
                return;
            }

            ItemDefinition definition = placedItem.item.definition;
            Color itemColor = GetFallbackItemColor(definition.itemId);
            Color borderColor = Color.Lerp(itemColor, Color.white, 0.34f);
            Color accentColor = ResolveRiskAccent(definition);

            backgroundGraphic.FillColor = new Color(itemColor.r * 0.68f, itemColor.g * 0.68f, itemColor.b * 0.68f, 0.96f);
            backgroundGraphic.BorderColor = borderColor;
            backgroundGraphic.BorderWidth = 1.4f;
            backgroundGraphic.CornerRadius = 7f;
            backgroundGraphic.CornerSegments = 5;
            backgroundGraphic.SetAllDirty();

            if (accentGraphic != null)
            {
                accentGraphic.FillColor = accentColor;
                accentGraphic.BorderColor = Color.clear;
                accentGraphic.BorderWidth = 0f;
                accentGraphic.CornerRadius = 3f;
                accentGraphic.SetAllDirty();
            }

            iconImage.enabled = definition.icon != null;
            iconImage.sprite = definition.icon;
            iconImage.color = Color.white;

            if (labelText != null)
            {
                labelText.text = string.IsNullOrWhiteSpace(definition.displayName) ? definition.itemId : definition.displayName;
                labelText.enabled = definition.icon == null;
            }

            if (stackCountText != null)
            {
                bool showStack = definition.stackable && placedItem.item.stackCount > 1;
                stackCountText.enabled = showStack;
                stackCountText.text = showStack ? placedItem.item.stackCount.ToString() : string.Empty;
            }
        }

        private bool HasValidBinding()
        {
            return placedItem != null &&
                   placedItem.item != null &&
                   placedItem.item.definition != null &&
                   ownerGrid != null;
        }

        private void PlayInvalidFeedback()
        {
            EnsureReferences();
            if (!isActiveAndEnabled || backgroundGraphic == null)
            {
                return;
            }

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(FlashInvalid());
        }

        private IEnumerator FlashInvalid()
        {
            Color originalFill = backgroundGraphic.FillColor;
            Color originalBorder = backgroundGraphic.BorderColor;
            backgroundGraphic.FillColor = new Color(0.72f, 0.12f, 0.12f, 0.98f);
            backgroundGraphic.BorderColor = new Color(1f, 0.42f, 0.34f, 1f);
            backgroundGraphic.SetAllDirty();
            yield return new WaitForSeconds(0.08f);
            backgroundGraphic.FillColor = originalFill;
            backgroundGraphic.BorderColor = originalBorder;
            backgroundGraphic.SetAllDirty();
            feedbackRoutine = null;
        }

        private void EnsureReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (iconImage == null)
            {
                Transform iconTransform = transform.Find("Icon");
                if (iconTransform != null)
                {
                    iconImage = iconTransform.GetComponent<Image>();
                }
            }

            if (iconImage == null)
            {
                GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.SetParent(transform, false);
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(7f, 9f);
                iconRect.offsetMax = new Vector2(-7f, -9f);
                iconImage = iconObject.GetComponent<Image>();
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;
            }

            if (backgroundGraphic == null)
            {
                backgroundGraphic = GetComponent<InventoryRoundedBoxGraphic>();
            }

            if (backgroundGraphic == null)
            {
                backgroundGraphic = gameObject.AddComponent<InventoryRoundedBoxGraphic>();
            }

            backgroundGraphic.raycastTarget = true;

            if (accentGraphic == null)
            {
                Transform accentTransform = transform.Find("RiskAccent");
                if (accentTransform != null)
                {
                    accentGraphic = accentTransform.GetComponent<InventoryRoundedBoxGraphic>();
                }
            }

            if (accentGraphic == null)
            {
                GameObject accentObject = new GameObject("RiskAccent", typeof(RectTransform), typeof(InventoryRoundedBoxGraphic));
                RectTransform accentRect = accentObject.GetComponent<RectTransform>();
                accentRect.SetParent(transform, false);
                accentRect.anchorMin = new Vector2(0f, 1f);
                accentRect.anchorMax = new Vector2(1f, 1f);
                accentRect.pivot = new Vector2(0.5f, 1f);
                accentRect.offsetMin = new Vector2(6f, -7f);
                accentRect.offsetMax = new Vector2(-6f, -3f);
                accentGraphic = accentObject.GetComponent<InventoryRoundedBoxGraphic>();
                accentGraphic.raycastTarget = false;
            }

            Font font = InventoryUIFont.DefaultFont;
            if (labelText == null)
            {
                labelText = CreateTextChild("Label", TextAnchor.MiddleCenter, 11, font);
                labelText.raycastTarget = false;
            }

            if (stackCountText == null)
            {
                stackCountText = CreateTextChild("StackCount", TextAnchor.LowerRight, 12, font);
                stackCountText.raycastTarget = false;
            }
        }

        private Text CreateTextChild(string childName, TextAnchor alignment, int fontSize, Font font)
        {
            GameObject textObject = new GameObject(childName, typeof(RectTransform), typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(3f, 3f);
            textRect.offsetMax = new Vector2(-3f, -3f);

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Color GetFallbackItemColor(string itemId)
        {
            int hash = string.IsNullOrWhiteSpace(itemId) ? 17 : itemId.GetHashCode();
            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.38f, 0.78f);
        }

        private static Color ResolveRiskAccent(ItemDefinition definition)
        {
            if (definition == null)
            {
                return new Color(0.42f, 0.68f, 0.82f, 1f);
            }

            float risk = Mathf.Max(definition.suspicion, definition.smell, definition.noise);
            if (risk >= 0.65f)
            {
                return new Color(1f, 0.36f, 0.24f, 1f);
            }

            if (risk >= 0.25f)
            {
                return new Color(1f, 0.75f, 0.28f, 1f);
            }

            return new Color(0.37f, 0.78f, 0.82f, 1f);
        }

        private static bool IsShiftPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }
    }
}
