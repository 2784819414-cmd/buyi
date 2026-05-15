using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageItemView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public StorageBoxGraphic Background;
        public Text NameText;
        public Text SizeText;
        public CanvasGroup CanvasGroup;

        private StorageBoxGraphic accentBar;
        private StorageBoxGraphic topRule;
        private StorageBoxGraphic sizePlate;
        private StorageItemModel item;
        private StorageGridUI ownerGrid;
        private StorageWindowUI window;
        private RectTransform rectTransform;
        private bool hovered;
        private bool dragging;

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
            EnsureVisual();
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            EnsureVisual();
            if (item == null)
            {
                NameText.text = string.Empty;
                SizeText.text = string.Empty;
                return;
            }

            Color theme = item.ThemeColor;
            Color fill = Color.Lerp(StoragePalette.ItemBase, theme, dragging ? 0.24f : hovered ? 0.16f : 0.1f);
            Color border = dragging
                ? StoragePalette.SlotHoverBorder
                : hovered
                    ? Color.Lerp(theme, StoragePalette.PanelInnerBorder, 0.58f)
                    : StoragePalette.PanelBorder;
            Background.SetStyle(fill, border, dragging ? 1.8f : hovered ? 1.4f : 1f, 8f);
            Background.raycastTarget = true;

            accentBar.SetStyle(Color.Lerp(theme, StoragePalette.Accent, hovered || dragging ? 0.24f : 0.1f), Color.clear, 0f, 4f);
            topRule.SetStyle(new Color(1f, 1f, 1f, hovered || dragging ? 0.14f : 0.06f), Color.clear, 0f, 4f);
            sizePlate.SetStyle(StoragePalette.ItemPlate, Color.clear, 0f, 5f);

            NameText.text = item.DisplayName;
            SizeText.text = item.CurrentWidth + "x" + item.CurrentHeight;
            NameText.color = StoragePalette.TextPrimary;
            SizeText.color = StoragePalette.TextSecondary;
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
            CanvasGroup.alpha = isDragging ? 0.94f : 1f;
            RectTransform.localScale = isDragging ? new Vector3(1.045f, 1.045f, 1f) : Vector3.one;
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

            window.SelectItem(item);
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
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            RefreshVisual();
        }

        private void EnsureVisual()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            StorageUIUtility.SetAnchor(rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

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

            accentBar = EnsureFixedChildBox(accentBar, "AccentBar", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(6f, -10f));
            topRule = EnsureStretchChildBox(topRule, "TopRule", new Vector2(9f, -9f), new Vector2(-9f, 5f));
            sizePlate = EnsureFixedChildBox(sizePlate, "SizePlate", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -5f), new Vector2(38f, 18f));

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
        }

        private Text CreateChildText(string objectName, TextAnchor alignment, int size, Color color)
        {
            Text text = StorageUIUtility.CreateText(objectName, transform, string.Empty, size, alignment, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
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
