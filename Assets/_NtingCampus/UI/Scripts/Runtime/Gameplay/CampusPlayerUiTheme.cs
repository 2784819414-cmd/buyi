using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public sealed class CampusPlayerUiTheme
    {
        private static CampusPlayerUiTheme instance;

        private Texture2D overlayTexture;
        private Texture2D panelTexture;
        private Texture2D sectionTexture;
        private Texture2D optionTexture;
        private Texture2D optionSelectedTexture;
        private Texture2D primaryButtonTexture;
        private Texture2D secondaryButtonTexture;
        private Texture2D selectedPillTexture;
        private Texture2D idlePillTexture;
        private Texture2D progressTrackTexture;
        private Texture2D progressFillTexture;

        public GUIStyle Panel { get; private set; }
        public GUIStyle Title { get; private set; }
        public GUIStyle Subtitle { get; private set; }
        public GUIStyle SectionCard { get; private set; }
        public GUIStyle SectionHeader { get; private set; }
        public GUIStyle SectionMeta { get; private set; }
        public GUIStyle OptionButton { get; private set; }
        public GUIStyle OptionButtonSelected { get; private set; }
        public GUIStyle PrimaryButton { get; private set; }
        public GUIStyle SecondaryButton { get; private set; }
        public GUIStyle StatusLabel { get; private set; }
        public GUIStyle ProgressTrack { get; private set; }
        public GUIStyle ProgressFill { get; private set; }
        public GUIStyle PillButton { get; private set; }
        public GUIStyle PillButtonSelected { get; private set; }
        public GUIStyle OverlayLabel { get; private set; }

        public static CampusPlayerUiTheme Instance => instance ??= Build();

        public void DrawOverlay()
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            GUI.DrawTexture(new Rect(0f, 0f, 4096f, 4096f), overlayTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;
        }

        private static CampusPlayerUiTheme Build()
        {
            CampusPlayerUiTheme theme = new CampusPlayerUiTheme();
            theme.overlayTexture = CreateTexture(new Color(0.05f, 0.04f, 0.03f, 1f));
            theme.panelTexture = CreateTexture(new Color(0.17f, 0.12f, 0.09f, 0.97f));
            theme.sectionTexture = CreateTexture(new Color(0.21f, 0.16f, 0.11f, 0.98f));
            theme.optionTexture = CreateTexture(new Color(0.25f, 0.19f, 0.13f, 1f));
            theme.optionSelectedTexture = CreateTexture(new Color(0.44f, 0.28f, 0.14f, 1f));
            theme.primaryButtonTexture = CreateTexture(new Color(0.98f, 0.73f, 0.30f, 1f));
            theme.secondaryButtonTexture = CreateTexture(new Color(0.24f, 0.18f, 0.12f, 1f));
            theme.selectedPillTexture = CreateTexture(new Color(0.56f, 0.34f, 0.16f, 1f));
            theme.idlePillTexture = CreateTexture(new Color(0.19f, 0.14f, 0.10f, 1f));
            theme.progressTrackTexture = CreateTexture(new Color(0.18f, 0.13f, 0.09f, 1f));
            theme.progressFillTexture = CreateTexture(new Color(0.98f, 0.73f, 0.30f, 1f));

            theme.Panel = new GUIStyle(GUI.skin.box)
            {
                normal = { background = theme.panelTexture, textColor = Color.white },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(28, 28, 28, 28)
            };

            theme.Title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            theme.Subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = true,
                normal = { textColor = new Color(0.86f, 0.78f, 0.68f, 1f) }
            };

            theme.SectionCard = new GUIStyle(GUI.skin.box)
            {
                normal = { background = theme.sectionTexture, textColor = Color.white },
                padding = new RectOffset(18, 18, 16, 16)
            };

            theme.SectionHeader = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            theme.SectionMeta = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.74f, 0.63f, 0.50f, 1f) }
            };

            theme.OptionButton = BuildButtonStyle(theme.optionTexture, Color.white, 16, TextAnchor.MiddleLeft, new RectOffset(20, 20, 12, 12));
            theme.OptionButtonSelected = BuildButtonStyle(theme.optionSelectedTexture, Color.white, 16, TextAnchor.MiddleLeft, new RectOffset(20, 20, 12, 12));
            theme.PrimaryButton = BuildButtonStyle(theme.primaryButtonTexture, Color.white, 18, TextAnchor.MiddleCenter, new RectOffset(20, 20, 14, 14));
            theme.SecondaryButton = BuildButtonStyle(theme.secondaryButtonTexture, new Color(0.92f, 0.95f, 0.98f, 1f), 16, TextAnchor.MiddleCenter, new RectOffset(18, 18, 12, 12));
            theme.PillButton = BuildButtonStyle(theme.idlePillTexture, new Color(0.84f, 0.88f, 0.93f, 1f), 15, TextAnchor.MiddleCenter, new RectOffset(16, 16, 10, 10));
            theme.PillButtonSelected = BuildButtonStyle(theme.selectedPillTexture, Color.white, 15, TextAnchor.MiddleCenter, new RectOffset(16, 16, 10, 10));

            theme.StatusLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.98f, 0.73f, 0.30f, 1f) }
            };

            theme.ProgressTrack = new GUIStyle(GUI.skin.box)
            {
                normal = { background = theme.progressTrackTexture }
            };

            theme.ProgressFill = new GUIStyle(GUI.skin.box)
            {
                normal = { background = theme.progressFillTexture }
            };

            theme.OverlayLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.95f, 0.90f, 0.82f, 1f) }
            };

            return theme;
        }

        private static GUIStyle BuildButtonStyle(Texture2D background, Color textColor, int fontSize, TextAnchor alignment, RectOffset padding)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                alignment = alignment,
                wordWrap = true,
                padding = padding,
                margin = new RectOffset(0, 0, 0, 0),
                normal = { background = background, textColor = textColor },
                hover = { background = background, textColor = textColor },
                active = { background = background, textColor = textColor },
                focused = { background = background, textColor = textColor }
            };
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }
    }
}

