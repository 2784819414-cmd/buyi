using System;
using Nting.Storage;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public static class CampusUiRuntimeBuilder
    {
        public static Canvas CreateScreenCanvas(GameObject host, string canvasName, int sortingOrder)
        {
            if (host == null)
            {
                return null;
            }

            GameObject canvasObject = host.transform.Find(canvasName)?.gameObject;
            if (canvasObject == null)
            {
                canvasObject = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(host.transform, false);
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CampusUiVisualTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.enabled = true;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return canvas;
        }

        public static RectTransform CreateFullScreenPanel(
            string name,
            Transform parent,
            Color fill,
            bool raycastTarget = false)
        {
            RectTransform rect = StorageUIUtility.CreateStretchBox(
                name,
                parent,
                Vector2.zero,
                Vector2.zero,
                fill,
                Color.clear,
                0f,
                0f,
                raycastTarget);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        public static RectTransform CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            Color fill,
            Color border,
            float borderWidth,
            float radius,
            bool raycastTarget = false)
        {
            RectTransform rect = StorageUIUtility.CreateBox(
                name,
                parent,
                anchorMin,
                anchorMax,
                pivot,
                position,
                size,
                fill,
                border,
                borderWidth,
                radius,
                raycastTarget);
            return rect;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string value,
            int size,
            TextAnchor alignment,
            Color color,
            FontStyle style = FontStyle.Normal,
            bool richText = false)
        {
            Text text = StorageUIUtility.CreateText(name, parent, value, size, alignment, color);
            text.font = CampusUiVisualTheme.Font;
            text.fontStyle = style;
            text.supportRichText = richText;
            return text;
        }

        public static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            bool preserveAspect = false,
            bool raycastTarget = false)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction action,
            Color fill,
            Color border,
            float radius = 12f,
            float borderWidth = 1f,
            Color textColor = default,
            int fontSize = 16)
        {
            if (textColor == default)
            {
                textColor = CampusUiVisualTheme.TextPrimary;
            }

            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(StorageBoxGraphic),
                typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            StorageUIUtility.SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            StorageBoxGraphic graphic = buttonObject.GetComponent<StorageBoxGraphic>();
            graphic.SetStyle(fill, border, borderWidth, radius);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.ColorTint;
            StorageUIUtility.ApplyButtonTransition(button);
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            Text text = CreateText("Label", buttonObject.transform, label, fontSize, TextAnchor.MiddleCenter, textColor, FontStyle.Bold);
            text.rectTransform.offsetMin = new Vector2(10f, 4f);
            text.rectTransform.offsetMax = new Vector2(-10f, -4f);
            return button;
        }

        public static InputField CreateInputField(
            string name,
            Transform parent,
            string placeholder,
            int fontSize,
            Color textColor,
            Color placeholderColor)
        {
            GameObject fieldObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(StorageBoxGraphic),
                typeof(InputField));
            RectTransform rect = fieldObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            StorageUIUtility.SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            StorageBoxGraphic background = fieldObject.GetComponent<StorageBoxGraphic>();
            background.SetStyle(CampusUiVisualTheme.PanelRaised, CampusUiVisualTheme.BorderSoft, 1.2f, 12f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(fieldObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(14f, 6f);
            textRect.offsetMax = new Vector2(-14f, -6f);

            Text text = textObject.GetComponent<Text>();
            text.font = CampusUiVisualTheme.Font;
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            placeholderObject.transform.SetParent(fieldObject.transform, false);
            RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.anchorMin = new Vector2(0f, 0f);
            placeholderRect.anchorMax = new Vector2(1f, 1f);
            placeholderRect.offsetMin = new Vector2(14f, 6f);
            placeholderRect.offsetMax = new Vector2(-14f, -6f);

            Text placeholderText = placeholderObject.GetComponent<Text>();
            placeholderText.font = CampusUiVisualTheme.Font;
            placeholderText.fontSize = fontSize;
            placeholderText.color = placeholderColor;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Overflow;
            placeholderText.verticalOverflow = VerticalWrapMode.Truncate;
            placeholderText.raycastTarget = false;
            placeholderText.text = placeholder;

            InputField inputField = fieldObject.GetComponent<InputField>();
            inputField.targetGraphic = background;
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterValidation = InputField.CharacterValidation.None;

            return inputField;
        }

        public static ScrollRect CreateScrollView(
            string name,
            Transform parent,
            Vector2 size,
            out RectTransform viewport,
            out RectTransform content,
            Color fill,
            Color border,
            float borderWidth,
            float radius)
        {
            GameObject rootObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(StorageBoxGraphic), typeof(ScrollRect));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            StorageUIUtility.SetAnchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            root.sizeDelta = size;

            StorageBoxGraphic box = rootObject.GetComponent<StorageBoxGraphic>();
            box.SetStyle(fill, border, borderWidth, radius);

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(root, false);
            StorageUIUtility.SetAnchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-28f, -12f);

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content = contentObject.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = 10f;
            layout.padding = new RectOffset(2, 2, 2, 2);

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            ScrollRect scrollRect = rootObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;

            GameObject scrollbarObject = new GameObject(
                "Scrollbar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(StorageBoxGraphic),
                typeof(Scrollbar));
            RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            scrollbarRect.SetParent(root, false);
            StorageUIUtility.SetAnchor(scrollbarRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
            scrollbarRect.anchoredPosition = new Vector2(-8f, 0f);
            scrollbarRect.sizeDelta = new Vector2(10f, -24f);

            StorageBoxGraphic scrollbarTrack = scrollbarObject.GetComponent<StorageBoxGraphic>();
            scrollbarTrack.SetStyle(CampusUiVisualTheme.SurfaceDark, CampusUiVisualTheme.BorderMuted, 1f, 5f);

            GameObject slidingAreaObject = new GameObject("SlidingArea", typeof(RectTransform));
            RectTransform slidingArea = slidingAreaObject.GetComponent<RectTransform>();
            slidingArea.SetParent(scrollbarRect, false);
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(1f, 1f);
            slidingArea.offsetMax = new Vector2(-1f, -1f);

            GameObject handleObject = new GameObject(
                "Handle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(StorageBoxGraphic));
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.SetParent(slidingArea, false);
            handleRect.anchorMin = new Vector2(0f, 1f);
            handleRect.anchorMax = new Vector2(1f, 1f);
            handleRect.pivot = new Vector2(0.5f, 1f);
            handleRect.sizeDelta = new Vector2(0f, 56f);

            StorageBoxGraphic handleGraphic = handleObject.GetComponent<StorageBoxGraphic>();
            handleGraphic.SetStyle(CampusUiVisualTheme.AccentSoftFill, CampusUiVisualTheme.Accent, 1f, 5f);

            Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleGraphic;

            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            return scrollRect;
        }

        public static void SetStretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            StorageUIUtility.SetAnchor(rect, min, max, pivot);
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

    }
}
