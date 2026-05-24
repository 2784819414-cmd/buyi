using UnityEditor;
using UnityEngine;
using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Small IMGUI style cache used by the editor window.
    /// </summary>
    public static class CampusMapEditorStyles
    {
        private static GUIStyle header;
        private static GUIStyle helpBox;
        private static GUIStyle miniPreview;

        public static GUIStyle Header
        {
            get
            {
                if (header == null)
                {
                    header = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        normal = { textColor = CampusUiVisualTheme.TextPrimary },
                        contentOffset = new Vector2(0f, 1f),
                        padding = new RectOffset(0, 0, 6, 2)
                    };
                }

                return header;
            }
        }

        public static GUIStyle HelpBox
        {
            get
            {
                if (helpBox == null)
                {
                    helpBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        normal = { background = Texture2D.whiteTexture, textColor = CampusUiVisualTheme.TextPrimary },
                        border = new RectOffset(1, 1, 1, 1),
                        padding = new RectOffset(8, 8, 6, 6)
                    };
                }

                return helpBox;
            }
        }

        public static GUIStyle MiniPreview
        {
            get
            {
                if (miniPreview == null)
                {
                    miniPreview = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        normal = { textColor = CampusUiVisualTheme.TextSecondary },
                        fontSize = 10
                    };
                }

                return miniPreview;
            }
        }
    }
}
