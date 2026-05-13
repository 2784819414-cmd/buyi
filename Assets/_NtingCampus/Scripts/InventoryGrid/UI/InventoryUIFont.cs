using UnityEngine;

namespace NtingCampus.InventoryGrid
{
    public static class InventoryUIFont
    {
        private static Font cachedFont;

        public static Font DefaultFont
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
                    14);

                if (cachedFont == null)
                {
                    Debug.LogWarning("Inventory UI font load failed: no usable runtime font was found.");
                }

                return cachedFont;
            }
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
}
