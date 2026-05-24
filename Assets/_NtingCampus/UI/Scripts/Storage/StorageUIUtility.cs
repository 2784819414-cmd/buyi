using UnityEngine;
using UnityEngine.UI;

namespace Nting.Storage
{
    public static class StorageUIUtility
    {
        private static Font cachedFont;

        public static Font Font
        {
            get
            {
                if (cachedFont != null)
                {
                    return cachedFont;
                }

                cachedFont = LoadBuiltInFont("LegacyRuntime.ttf");
                if (cachedFont != null)
                {
                    return cachedFont;
                }

                cachedFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
                    16);
                return cachedFont;
            }
        }

        public static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return gameObject;
        }

        public static StorageBoxGraphic AddBox(GameObject gameObject, Color fill, Color border, float borderWidth, float radius)
        {
            StorageBoxGraphic box = gameObject.GetComponent<StorageBoxGraphic>();
            if (box == null)
            {
                box = gameObject.AddComponent<StorageBoxGraphic>();
            }

            box.SetStyle(fill, border, borderWidth, radius);
            return box;
        }

        public static RectTransform CreateBox(
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
            GameObject boxObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(StorageBoxGraphic));
            RectTransform rect = boxObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchor(rect, anchorMin, anchorMax, pivot);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            StorageBoxGraphic box = boxObject.GetComponent<StorageBoxGraphic>();
            box.raycastTarget = raycastTarget;
            box.SetStyle(fill, border, borderWidth, radius);
            return rect;
        }

        public static RectTransform CreateStretchBox(
            string name,
            Transform parent,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color fill,
            Color border,
            float borderWidth,
            float radius,
            bool raycastTarget = false)
        {
            RectTransform rect = CreateBox(
                name,
                parent,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                fill,
                border,
                borderWidth,
                radius,
                raycastTarget);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string value,
            int size,
            TextAnchor alignment,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction action,
            Color fill,
            Color border,
            bool interactable = true)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(StorageBoxGraphic), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = Vector2.zero;

            StorageBoxGraphic graphic = buttonObject.GetComponent<StorageBoxGraphic>();
            graphic.SetStyle(fill, border, 1f, 7f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.interactable = interactable;
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            ApplyButtonTransition(button);

            Text text = CreateText("Label", buttonObject.transform, label, 15, TextAnchor.MiddleCenter, StoragePalette.TextPrimary);
            text.fontStyle = FontStyle.Bold;
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            return button;
        }

        public static void StyleButton(
            Button button,
            Color fill,
            Color border,
            float borderWidth,
            float radius,
            Color textColor)
        {
            if (button == null)
            {
                return;
            }

            StorageBoxGraphic graphic = button.GetComponent<StorageBoxGraphic>();
            if (graphic != null)
            {
                graphic.SetStyle(fill, border, borderWidth, radius);
            }

            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = textColor;
            }
        }

        public static void ApplyButtonTransition(Button button)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.84f, 0.86f, 0.88f, 1f);
            colors.disabledColor = new Color(0.46f, 0.48f, 0.52f, 0.58f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        public static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
        }

        public static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static RectTransform CreateDivider(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform divider = CreateBox(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, -y),
                new Vector2(width, height),
                StoragePalette.Divider,
                Color.clear,
                0f,
                0f);
            return divider;
        }

        private static Font LoadBuiltInFont(string fontName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(fontName);
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }
    }

    public static class StoragePalette
    {
        public static readonly Color Dimmer = new Color(0.03f, 0.04f, 0.05f, 0.42f);
        public static readonly Color Window = new Color(0.05f, 0.07f, 0.09f, 0.76f);
        public static readonly Color WindowBorder = new Color(0.78f, 0.72f, 0.60f, 0.28f);
        public static readonly Color WindowShadow = new Color(0.01f, 0.02f, 0.03f, 0.58f);
        public static readonly Color Panel = new Color(0.07f, 0.09f, 0.12f, 0.62f);
        public static readonly Color PanelRaised = new Color(0.10f, 0.12f, 0.16f, 0.70f);
        public static readonly Color PanelHeader = new Color(0.13f, 0.16f, 0.20f, 0.68f);
        public static readonly Color PanelBorder = new Color(0.82f, 0.76f, 0.64f, 0.22f);
        public static readonly Color PanelInnerBorder = new Color(0.96f, 0.80f, 0.48f, 0.12f);
        public static readonly Color Divider = new Color(0.92f, 0.84f, 0.62f, 0.14f);
        public static readonly Color Slot = new Color(0.09f, 0.11f, 0.14f, 0.74f);
        public static readonly Color SlotHover = new Color(0.12f, 0.15f, 0.19f, 0.82f);
        public static readonly Color SlotOccupied = new Color(0.07f, 0.09f, 0.12f, 0.78f);
        public static readonly Color SlotBorder = new Color(0.72f, 0.68f, 0.58f, 0.16f);
        public static readonly Color SlotHoverBorder = new Color(0.95f, 0.81f, 0.50f, 0.66f);
        public static readonly Color SlotTopEdge = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color SlotMark = new Color(0.95f, 0.81f, 0.50f, 0.12f);
        public static readonly Color TextPrimary = new Color(0.97f, 0.97f, 0.95f, 1f);
        public static readonly Color TextSecondary = new Color(0.77f, 0.79f, 0.82f, 1f);
        public static readonly Color TextMuted = new Color(0.52f, 0.57f, 0.62f, 1f);
        public static readonly Color Accent = new Color(0.95f, 0.81f, 0.50f, 1f);
        public static readonly Color AccentDim = new Color(0.95f, 0.81f, 0.50f, 0.22f);
        public static readonly Color Valid = new Color(0.10f, 0.22f, 0.16f, 0.84f);
        public static readonly Color ValidBorder = new Color(0.55f, 0.84f, 0.70f, 0.92f);
        public static readonly Color Invalid = new Color(0.26f, 0.10f, 0.10f, 0.84f);
        public static readonly Color InvalidBorder = new Color(0.96f, 0.50f, 0.45f, 0.92f);
        public static readonly Color Warning = new Color(0.97f, 0.53f, 0.45f, 1f);
        public static readonly Color Paper = new Color(0.92f, 0.84f, 0.62f, 1f);
        public static readonly Color PaperDim = new Color(0.74f, 0.69f, 0.58f, 0.88f);
        public static readonly Color TabNormal = new Color(0.10f, 0.12f, 0.16f, 0.62f);
        public static readonly Color TabSelected = new Color(0.16f, 0.18f, 0.23f, 0.72f);
        public static readonly Color ButtonNormal = new Color(0.12f, 0.14f, 0.18f, 0.68f);
        public static readonly Color ButtonHover = new Color(0.16f, 0.19f, 0.24f, 0.76f);
        public static readonly Color ButtonPressed = new Color(0.09f, 0.11f, 0.14f, 0.82f);
        public static readonly Color ItemBase = new Color(0.12f, 0.15f, 0.18f, 0.72f);
        public static readonly Color ItemPlate = new Color(0.05f, 0.06f, 0.08f, 0.76f);
    }
}
