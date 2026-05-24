using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public static class CampusUiVisualTheme
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public static readonly Color Background = new Color(0.12f, 0.13f, 0.12f, 1f);
        public static readonly Color BackgroundDeep = new Color(0.10f, 0.11f, 0.10f, 1f);
        public static readonly Color BackgroundWarmGlow = new Color(1f, 0.78f, 0.42f, 0.16f);
        public static readonly Color BackgroundCoolGlow = new Color(0.50f, 0.78f, 0.82f, 0.14f);
        public static readonly Color Overlay = new Color(0.04f, 0.04f, 0.04f, 0.62f);
        public static readonly Color Panel = new Color(0.23f, 0.20f, 0.17f, 0.94f);
        public static readonly Color PanelRaised = new Color(0.35f, 0.28f, 0.22f, 0.98f);
        public static readonly Color PanelSoft = new Color(0.29f, 0.25f, 0.20f, 0.88f);
        public static readonly Color PanelDim = new Color(0.16f, 0.17f, 0.14f, 0.78f);
        public static readonly Color SurfaceDark = new Color(0.18f, 0.18f, 0.15f, 0.92f);
        public static readonly Color SurfaceSoft = new Color(0.24f, 0.23f, 0.18f, 0.82f);
        public static readonly Color Border = new Color(0.93f, 0.68f, 0.28f, 0.88f);
        public static readonly Color BorderSoft = new Color(0.76f, 0.56f, 0.30f, 0.62f);
        public static readonly Color BorderMuted = new Color(0.48f, 0.38f, 0.24f, 0.44f);
        public static readonly Color Accent = new Color(1f, 0.72f, 0.20f, 1f);
        public static readonly Color AccentSoft = new Color(1f, 0.78f, 0.36f, 0.24f);
        public static readonly Color AccentAlt = new Color(0.39f, 0.72f, 0.75f, 1f);
        public static readonly Color Success = new Color(0.52f, 0.84f, 0.58f, 1f);
        public static readonly Color Warning = new Color(1f, 0.66f, 0.28f, 1f);
        public static readonly Color Danger = new Color(0.95f, 0.41f, 0.31f, 1f);
        public static readonly Color TextPrimary = new Color(0.99f, 0.96f, 0.91f, 1f);
        public static readonly Color TextSecondary = new Color(0.88f, 0.82f, 0.72f, 1f);
        public static readonly Color TextMuted = new Color(0.74f, 0.66f, 0.54f, 1f);
        public static readonly Color TextGold = new Color(1f, 0.89f, 0.60f, 1f);
        public static readonly Color Glow = new Color(1f, 0.76f, 0.30f, 0.18f);

        public static readonly Color DangerSoft = new Color(0.34f, 0.16f, 0.13f, 0.82f);
        public static readonly Color SuccessSoft = new Color(0.13f, 0.28f, 0.17f, 0.82f);
        public static readonly Color AccentSoftFill = new Color(0.52f, 0.35f, 0.14f, 0.72f);
        public static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.34f);

        public static Font Font => Nting.Storage.StorageUIUtility.Font;
    }
}
