using UnityEditor;
using UnityEngine;

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
                        fontSize = 10
                    };
                }

                return miniPreview;
            }
        }
    }
}
