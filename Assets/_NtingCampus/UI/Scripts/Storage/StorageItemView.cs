using NtingCampus.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageItemView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private enum ItemAlertBadgeState
        {
            None = 0,
            PendingProtectedTransfer = 1,
            StolenEvidence = 2
        }

        private const float DefaultCornerRadius = 8f;
        private const float DragScale = 1.045f;

        public StorageBoxGraphic Background;
        public Text NameText;
        public Text SizeText;
        public CanvasGroup CanvasGroup;

        private StorageBoxGraphic accentBar;
        private StorageBoxGraphic topRule;
        private StorageBoxGraphic sizePlate;
        private StorageBoxGraphic statusBadge;
        private Text statusBadgeText;
        private Image iconImage;
        private StorageItemModel item;
        private StorageGridUI ownerGrid;
        private StorageWindowUI window;
        private RectTransform rectTransform;
        private bool hovered;
        private bool dragging;
        private bool hiddenWhileDragging;
        private bool presentationVisible = true;

        public StorageItemModel Item => item;

        public StorageGridUI OwnerGrid => ownerGrid;

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

        private void Awake()
        {
            EnsureVisual();
        }

        public void Bind(StorageItemModel model, StorageGridUI grid, StorageWindowUI ownerWindow)
        {
            item = model;
            ownerGrid = grid;
            window = ownerWindow;
            dragging = false;
            hovered = false;
            hiddenWhileDragging = false;
            EnsureVisual();
            UpdateCanvasState();
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            EnsureVisual();
            if (item == null)
            {
                ClearVisual();
                return;
            }

            if (!presentationVisible)
            {
                ClearVisual();
                Background.raycastTarget = true;
                return;
            }

            bool hasIcon = StorageItemIconUtility.Resolve(item) != null;
            ApplyContainerVisual(hasIcon);
            ApplyContentVisual(hasIcon);
            ApplyStatusBadge(ResolveBadgeState(item, ownerGrid != null ? ownerGrid.Container : null));
        }

        public void SetRaycastEnabled(bool enabled)
        {
            EnsureVisual();
            Background.raycastTarget = enabled;
            CanvasGroup.blocksRaycasts = enabled;
        }

        public void SetDragVisual(bool isDragging)
        {
            EnsureVisual();
            dragging = isDragging;
            RectTransform.localScale = isDragging ? new Vector3(DragScale, DragScale, 1f) : Vector3.one;
            UpdateCanvasState();
            RefreshVisual();
        }

        public void SetHiddenWhileDragging(bool hidden)
        {
            EnsureVisual();
            hiddenWhileDragging = hidden;
            UpdateCanvasState();
        }

        public void SetPresentationVisible(bool visible)
        {
            presentationVisible = visible;
            RefreshVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (item == null || window == null)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                window.TryRotateItem(this);
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            window.SelectItem(item, ownerGrid);
            if (eventData.clickCount >= 2)
            {
                window.TryQuickTransfer(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || window == null)
            {
                return;
            }

            window.HideItemTooltip();
            window.DragController.BeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (window != null)
            {
                window.DragController.Drag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (window != null)
            {
                window.DragController.EndDrag(eventData);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            RefreshVisual();
            if (window != null)
            {
                window.ShowItemTooltip(item, ownerGrid, eventData.position);
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (window != null)
            {
                window.MoveItemTooltip(eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            RefreshVisual();
            if (window != null)
            {
                window.HideItemTooltip();
            }
        }

        private void EnsureVisual()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            if (Background == null)
            {
                Background = GetComponent<StorageBoxGraphic>();
            }

            if (Background == null)
            {
                Background = gameObject.AddComponent<StorageBoxGraphic>();
            }

            if (CanvasGroup == null)
            {
                CanvasGroup = GetComponent<CanvasGroup>();
            }

            if (CanvasGroup == null)
            {
                CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            accentBar = EnsureFixedChildBox(accentBar, "AccentBar", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(6f, -10f));
            topRule = EnsureStretchChildBox(topRule, "TopRule", new Vector2(9f, -9f), new Vector2(-9f, 5f));
            iconImage = EnsureIconImage(iconImage, "IconImage");
            sizePlate = EnsureFixedChildBox(sizePlate, "SizePlate", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -5f), new Vector2(38f, 18f));
            statusBadge = EnsureFixedChildBox(statusBadge, "StatusBadge", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(20f, 20f));
            statusBadgeText = EnsureBadgeText(statusBadgeText, statusBadge.transform);

            if (NameText == null)
            {
                NameText = CreateChildText("NameText", TextAnchor.MiddleCenter, 15, StoragePalette.TextPrimary);
                RectTransform textRect = NameText.rectTransform;
                textRect.offsetMin = new Vector2(13f, 5f);
                textRect.offsetMax = new Vector2(-7f, -14f);
            }

            if (SizeText == null)
            {
                SizeText = StorageUIUtility.CreateText("SizeText", sizePlate.transform, string.Empty, 10, TextAnchor.MiddleCenter, StoragePalette.TextSecondary);
                SizeText.horizontalOverflow = HorizontalWrapMode.Overflow;
                SizeText.verticalOverflow = VerticalWrapMode.Truncate;
                SizeText.rectTransform.offsetMin = Vector2.zero;
                SizeText.rectTransform.offsetMax = Vector2.zero;
            }

            ApplyVisualSiblingOrder();
        }

        private void UpdateCanvasState()
        {
            if (CanvasGroup == null)
            {
                return;
            }

            CanvasGroup.alpha = hiddenWhileDragging
                ? 0f
                : dragging
                    ? 0.94f
                    : 1f;
        }

        private void ClearVisual()
        {
            Background.SetStyle(Color.clear, Color.clear, 0f, DefaultCornerRadius);
            NameText.text = string.Empty;
            NameText.gameObject.SetActive(false);
            SizeText.text = string.Empty;
            iconImage.gameObject.SetActive(false);
            accentBar.gameObject.SetActive(false);
            topRule.gameObject.SetActive(false);
            sizePlate.gameObject.SetActive(false);
            statusBadge.gameObject.SetActive(false);
        }

        private void ApplyContainerVisual(bool hasIcon)
        {
            Color theme = item.ThemeColor;
            if (hasIcon)
            {
                ApplyIconFirstContainerVisual(theme);
                return;
            }

            ApplyTextFallbackContainerVisual(theme);
        }

        private void ApplyIconFirstContainerVisual(Color theme)
        {
            Color border = dragging
                ? StoragePalette.SlotHoverBorder
                : hovered
                    ? Color.Lerp(theme, StoragePalette.Accent, 0.42f)
                    : Color.clear;

            Color fill = Color.Lerp(StoragePalette.ItemBase, theme, 0.06f);
            fill.a = dragging ? 0.14f : hovered ? 0.08f : 0.02f;

            Background.SetStyle(fill, border, dragging ? 1.5f : hovered ? 1.1f : 0f, DefaultCornerRadius);
            Background.raycastTarget = true;

            accentBar.gameObject.SetActive(false);
            topRule.gameObject.SetActive(false);
            bool showSizePlate = !UsesSingleItemSlotLayout();
            sizePlate.gameObject.SetActive(showSizePlate);
            if (showSizePlate)
            {
                sizePlate.SetStyle(new Color(0.12f, 0.14f, 0.14f, 0.72f), Color.clear, 0f, 5f);
            }
        }

        private void ApplyTextFallbackContainerVisual(Color theme)
        {
            Color fill = Color.Lerp(StoragePalette.ItemBase, theme, dragging ? 0.24f : hovered ? 0.16f : 0.1f);
            Color border = dragging
                ? StoragePalette.SlotHoverBorder
                : hovered
                    ? Color.Lerp(theme, StoragePalette.PanelInnerBorder, 0.58f)
                    : StoragePalette.PanelBorder;

            Background.SetStyle(fill, border, dragging ? 1.8f : hovered ? 1.4f : 1f, DefaultCornerRadius);
            Background.raycastTarget = true;

            accentBar.gameObject.SetActive(true);
            accentBar.SetStyle(Color.Lerp(theme, StoragePalette.Accent, hovered || dragging ? 0.24f : 0.1f), Color.clear, 0f, 4f);

            topRule.gameObject.SetActive(true);
            topRule.SetStyle(new Color(1f, 1f, 1f, hovered || dragging ? 0.14f : 0.06f), Color.clear, 0f, 4f);

            bool showSizePlate = !UsesSingleItemSlotLayout();
            sizePlate.gameObject.SetActive(showSizePlate);
            if (showSizePlate)
            {
                sizePlate.SetStyle(StoragePalette.ItemPlate, Color.clear, 0f, 5f);
            }
        }

        private void ApplyContentVisual(bool hasIcon)
        {
            ConfigureSizePlate(hasIcon);

            NameText.gameObject.SetActive(!hasIcon);
            NameText.color = StoragePalette.TextPrimary;
            NameText.text = hasIcon ? string.Empty : item.GetDisplayName();

            bool showSizePlate = !UsesSingleItemSlotLayout();
            SizeText.color = StoragePalette.TextSecondary;
            SizeText.text = showSizePlate ? item.CurrentWidth + "x" + item.CurrentHeight : string.Empty;
            SizeText.gameObject.SetActive(showSizePlate);

            iconImage.gameObject.SetActive(hasIcon);
            if (!hasIcon)
            {
                iconImage.sprite = null;
                return;
            }

            iconImage.sprite = item.Icon;
            iconImage.color = Color.white;
        }

        private void ConfigureSizePlate(bool hasIcon)
        {
            bool showSizePlate = !UsesSingleItemSlotLayout();
            RectTransform sizePlateRect = sizePlate.GetComponent<RectTransform>();
            sizePlateRect.sizeDelta = hasIcon
                ? new Vector2(34f, 16f)
                : new Vector2(38f, 18f);

            SizeText.fontSize = hasIcon ? 9 : 10;
            sizePlate.gameObject.SetActive(showSizePlate);

            RectTransform iconRect = iconImage.rectTransform;
            Vector2 iconInset = ResolveIconInset();
            iconRect.offsetMin = iconInset;
            iconRect.offsetMax = -iconInset;
        }

        private Vector2 ResolveIconInset()
        {
            if (item == null)
            {
                return new Vector2(6f, 6f);
            }

            if (UsesSingleItemSlotLayout())
            {
                return new Vector2(4f, 4f);
            }

            int widestSide = Mathf.Max(item.CurrentWidth, item.CurrentHeight);
            int smallestSide = Mathf.Min(item.CurrentWidth, item.CurrentHeight);
            float edgeInset = widestSide >= 3 ? 2f : widestSide == 2 ? 3f : 4f;
            float verticalInset = smallestSide >= 2 ? edgeInset : edgeInset + 0.5f;
            return new Vector2(edgeInset, verticalInset);
        }

        private bool UsesSingleItemSlotLayout()
        {
            return ownerGrid != null &&
                   ownerGrid.Container != null &&
                   ownerGrid.Container.IsSingleItemSlot;
        }

        private void ApplyVisualSiblingOrder()
        {
            if (accentBar != null)
            {
                accentBar.transform.SetAsFirstSibling();
            }

            if (topRule != null)
            {
                topRule.transform.SetAsLastSibling();
            }

            if (iconImage != null)
            {
                iconImage.transform.SetAsLastSibling();
            }

            if (NameText != null)
            {
                NameText.transform.SetAsLastSibling();
            }

            if (sizePlate != null)
            {
                sizePlate.transform.SetAsLastSibling();
            }

            if (statusBadge != null)
            {
                statusBadge.transform.SetAsLastSibling();
            }
        }

        private Text CreateChildText(string objectName, TextAnchor alignment, int size, Color color)
        {
            Text text = StorageUIUtility.CreateText(objectName, transform, string.Empty, size, alignment, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static ItemAlertBadgeState ResolveBadgeState(
            StorageItemModel item,
            StorageContainerModel container)
        {
            if (item == null)
            {
                return ItemAlertBadgeState.None;
            }

            if (CampusProtectedTransferState.ShouldDisplayPendingCheckout(item, container))
            {
                return ItemAlertBadgeState.PendingProtectedTransfer;
            }

            return item.IsStolenEvidence
                ? ItemAlertBadgeState.StolenEvidence
                : ItemAlertBadgeState.None;
        }

        private void ApplyStatusBadge(ItemAlertBadgeState state)
        {
            if (statusBadge == null || statusBadgeText == null)
            {
                return;
            }

            if (state == ItemAlertBadgeState.None)
            {
                statusBadge.gameObject.SetActive(false);
                statusBadgeText.text = string.Empty;
                return;
            }

            statusBadge.gameObject.SetActive(true);
            statusBadgeText.text = "!";
            switch (state)
            {
                case ItemAlertBadgeState.PendingProtectedTransfer:
                    statusBadge.SetStyle(
                        new Color(0.42f, 0.31f, 0.08f, 0.94f),
                        new Color(1f, 0.88f, 0.42f, 0.78f),
                        0.8f,
                        4f);
                    statusBadgeText.color = StoragePalette.Warning;
                    return;
                case ItemAlertBadgeState.StolenEvidence:
                    statusBadge.SetStyle(
                        new Color(0.58f, 0.12f, 0.1f, 0.94f),
                        new Color(1f, 0.78f, 0.65f, 0.7f),
                        0.8f,
                        4f);
                    statusBadgeText.color = Color.white;
                    return;
                default:
                    statusBadge.gameObject.SetActive(false);
                    statusBadgeText.text = string.Empty;
                    return;
            }
        }

        private Image EnsureIconImage(Image image, string objectName)
        {
            if (image == null)
            {
                Transform child = transform.Find(objectName);
                if (child != null)
                {
                    image = child.GetComponent<Image>();
                }
            }

            if (image == null)
            {
                GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imageObject.transform.SetParent(transform, false);
                image = imageObject.GetComponent<Image>();
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 6f);
            rect.offsetMax = new Vector2(-6f, -6f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.gameObject.SetActive(false);
            return image;
        }

        private Text EnsureBadgeText(Text text, Transform parent)
        {
            if (text == null && parent != null)
            {
                Transform child = parent.Find("Text");
                if (child != null)
                {
                    text = child.GetComponent<Text>();
                }
            }

            if (text == null)
            {
                text = StorageUIUtility.CreateText("Text", parent, string.Empty, 13, TextAnchor.MiddleCenter, Color.white);
            }

            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        private StorageBoxGraphic EnsureStretchChildBox(StorageBoxGraphic box, string objectName, Vector2 offsetMin, Vector2 offsetMax)
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

        private StorageBoxGraphic EnsureFixedChildBox(StorageBoxGraphic box, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
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
                RectTransform childRect = StorageUIUtility.CreateBox(
                    objectName,
                    transform,
                    anchorMin,
                    anchorMax,
                    pivot,
                    position,
                    size,
                    Color.clear,
                    Color.clear,
                    0f,
                    0f);
                box = childRect.GetComponent<StorageBoxGraphic>();
            }

            RectTransform rect = box.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            box.raycastTarget = false;
            return box;
        }
    }
}
