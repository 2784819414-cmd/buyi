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
            graphic.SetStyle(fill, border, 1.2f, 7f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.interactable = interactable;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f, 1f);
            colors.pressedColor = new Color(0.78f, 0.86f, 0.9f, 1f);
            colors.disabledColor = new Color(0.44f, 0.48f, 0.5f, 0.75f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text text = CreateText("Label", buttonObject.transform, label, 16, TextAnchor.MiddleCenter, StoragePalette.TextPrimary);
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            return button;
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
        public static readonly Color Dimmer = new Color(0f, 0f, 0f, 0.56f);
        public static readonly Color Window = new Color(0.055f, 0.067f, 0.078f, 0.98f);
        public static readonly Color WindowBorder = new Color(0.29f, 0.38f, 0.44f, 0.96f);
        public static readonly Color Panel = new Color(0.085f, 0.102f, 0.118f, 0.96f);
        public static readonly Color PanelRaised = new Color(0.105f, 0.124f, 0.142f, 0.98f);
        public static readonly Color PanelBorder = new Color(0.21f, 0.29f, 0.34f, 0.92f);
        public static readonly Color Slot = new Color(0.065f, 0.078f, 0.094f, 0.96f);
        public static readonly Color SlotHover = new Color(0.095f, 0.12f, 0.142f, 0.98f);
        public static readonly Color SlotOccupied = new Color(0.045f, 0.052f, 0.062f, 0.98f);
        public static readonly Color SlotBorder = new Color(0.235f, 0.305f, 0.36f, 0.92f);
        public static readonly Color TextPrimary = new Color(0.88f, 0.92f, 0.94f, 1f);
        public static readonly Color TextSecondary = new Color(0.55f, 0.64f, 0.69f, 1f);
        public static readonly Color TextMuted = new Color(0.38f, 0.45f, 0.49f, 1f);
        public static readonly Color Accent = new Color(0.62f, 0.79f, 0.88f, 1f);
        public static readonly Color Valid = new Color(0.22f, 0.62f, 0.43f, 0.7f);
        public static readonly Color Invalid = new Color(0.58f, 0.18f, 0.18f, 0.72f);
        public static readonly Color Warning = new Color(0.78f, 0.57f, 0.28f, 1f);
    }
}
