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

        private StorageItemModel item;
        private StorageGridUI ownerGrid;
        private StorageWindowUI window;
        private RectTransform rectTransform;
        private bool hovered;

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

            Color fill = Color.Lerp(item.ThemeColor, new Color(0.07f, 0.083f, 0.095f, 1f), 0.42f);
            Color border = hovered ? StoragePalette.Accent : Color.Lerp(item.ThemeColor, StoragePalette.Accent, 0.28f);
            Background.SetStyle(fill, border, hovered ? 1.8f : 1.2f, 7f);
            Background.raycastTarget = true;

            NameText.text = item.DisplayName;
            SizeText.text = item.CurrentWidth + "x" + item.CurrentHeight;
        }

        public void SetRaycastEnabled(bool enabled)
        {
            EnsureVisual();
            Background.raycastTarget = enabled;
            CanvasGroup.blocksRaycasts = enabled;
        }

        public void SetDragVisual(bool dragging)
        {
            EnsureVisual();
            CanvasGroup.alpha = dragging ? 0.86f : 1f;
            RectTransform.localScale = dragging ? new Vector3(1.025f, 1.025f, 1f) : Vector3.one;
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

            if (NameText == null)
            {
                NameText = CreateChildText("NameText", TextAnchor.MiddleCenter, 15, StoragePalette.TextPrimary);
                RectTransform textRect = NameText.rectTransform;
                textRect.offsetMin = new Vector2(5f, 13f);
                textRect.offsetMax = new Vector2(-5f, -9f);
            }

            if (SizeText == null)
            {
                SizeText = CreateChildText("SizeText", TextAnchor.UpperRight, 11, StoragePalette.TextSecondary);
                RectTransform sizeRect = SizeText.rectTransform;
                sizeRect.offsetMin = new Vector2(4f, 3f);
                sizeRect.offsetMax = new Vector2(-5f, -3f);
            }
        }

        private Text CreateChildText(string objectName, TextAnchor alignment, int size, Color color)
        {
            Text text = StorageUIUtility.CreateText(objectName, transform, string.Empty, size, alignment, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
    }
}
