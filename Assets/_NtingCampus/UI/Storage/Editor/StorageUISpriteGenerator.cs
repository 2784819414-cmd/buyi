#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Nting.Storage;

namespace Nting.Storage.EditorTools
{
    public static class StorageUISpriteGenerator
    {
        private const string OutputDirectory = "Assets/_NtingCampus/UI/Storage/Art/Generated";

        private static readonly SpriteSpec[] Specs =
        {
            new SpriteSpec("panel_main.png", 96, 96, 16f, 1.5f, new Vector4(18f, 18f, 18f, 18f), StoragePalette.Window, StoragePalette.WindowBorder, new Color(1f, 1f, 1f, 0.06f), new Color(0f, 0f, 0f, 0.14f), false),
            new SpriteSpec("panel_card.png", 80, 80, 12f, 1f, new Vector4(14f, 14f, 14f, 14f), StoragePalette.PanelRaised, StoragePalette.PanelBorder, new Color(1f, 1f, 1f, 0.05f), new Color(0f, 0f, 0f, 0.12f), false),
            new SpriteSpec("panel_header.png", 96, 48, 10f, 0f, new Vector4(12f, 12f, 10f, 10f), StoragePalette.PanelHeader, Color.clear, new Color(1f, 1f, 1f, 0.05f), new Color(0f, 0f, 0f, 0.08f), false),
            new SpriteSpec("slot_normal.png", 48, 48, 7f, 1.2f, new Vector4(8f, 8f, 8f, 8f), StoragePalette.Slot, StoragePalette.SlotBorder, StoragePalette.SlotTopEdge, new Color(0f, 0f, 0f, 0.14f), false),
            new SpriteSpec("slot_hover.png", 48, 48, 7f, 1.5f, new Vector4(8f, 8f, 8f, 8f), StoragePalette.SlotHover, StoragePalette.SlotHoverBorder, StoragePalette.AccentDim, new Color(0f, 0f, 0f, 0.1f), false),
            new SpriteSpec("slot_valid.png", 48, 48, 7f, 1.6f, new Vector4(8f, 8f, 8f, 8f), StoragePalette.Valid, StoragePalette.ValidBorder, new Color(StoragePalette.ValidBorder.r, StoragePalette.ValidBorder.g, StoragePalette.ValidBorder.b, 0.26f), new Color(0f, 0f, 0f, 0.08f), false),
            new SpriteSpec("slot_invalid.png", 48, 48, 7f, 1.6f, new Vector4(8f, 8f, 8f, 8f), StoragePalette.Invalid, StoragePalette.InvalidBorder, new Color(StoragePalette.InvalidBorder.r, StoragePalette.InvalidBorder.g, StoragePalette.InvalidBorder.b, 0.24f), new Color(0f, 0f, 0f, 0.1f), false),
            new SpriteSpec("tab_normal.png", 96, 36, 9f, 0f, new Vector4(10f, 10f, 8f, 8f), StoragePalette.TabNormal, Color.clear, new Color(1f, 1f, 1f, 0.04f), new Color(0f, 0f, 0f, 0.1f), false),
            new SpriteSpec("tab_selected.png", 96, 36, 9f, 1f, new Vector4(10f, 10f, 8f, 8f), StoragePalette.TabSelected, StoragePalette.SlotHoverBorder, new Color(1f, 1f, 1f, 0.05f), new Color(0f, 0f, 0f, 0.08f), false),
            new SpriteSpec("button_normal.png", 96, 40, 9f, 0f, new Vector4(10f, 10f, 10f, 10f), StoragePalette.ButtonNormal, Color.clear, new Color(1f, 1f, 1f, 0.04f), new Color(0f, 0f, 0f, 0.1f), false),
            new SpriteSpec("button_hover.png", 96, 40, 9f, 0f, new Vector4(10f, 10f, 10f, 10f), StoragePalette.ButtonHover, Color.clear, new Color(1f, 1f, 1f, 0.05f), new Color(0f, 0f, 0f, 0.08f), false),
            new SpriteSpec("button_pressed.png", 96, 40, 9f, 0f, new Vector4(10f, 10f, 10f, 10f), StoragePalette.ButtonPressed, Color.clear, new Color(0f, 0f, 0f, 0.1f), new Color(1f, 1f, 1f, 0.03f), false),
            new SpriteSpec("divider_line.png", 64, 4, 0f, 0f, new Vector4(1f, 1f, 1f, 1f), StoragePalette.Divider, Color.clear, Color.clear, Color.clear, false)
        };

        [MenuItem("Tools/Nting/Storage UI/Generate UI Sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutputDirectory);

            for (int i = 0; i < Specs.Length; i++)
            {
                SpriteSpec spec = Specs[i];
                Texture2D texture = BuildTexture(spec);
                string path = GetPath(spec.FileName);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.Refresh();

            for (int i = 0; i < Specs.Length; i++)
            {
                ConfigureImporter(Specs[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(StorageTextCatalog.Format(StorageTextId.GeneratedUiSprites, OutputDirectory));
        }

        [InitializeOnLoadMethod]
        private static void GenerateMissingSpritesOnLoad()
        {
            EditorApplication.delayCall += GenerateMissingSprites;
        }

        private static void GenerateMissingSprites()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (int i = 0; i < Specs.Length; i++)
            {
                if (!File.Exists(GetPath(Specs[i].FileName)))
                {
                    GenerateAll();
                    return;
                }
            }
        }

        private static Texture2D BuildTexture(SpriteSpec spec)
        {
            Texture2D texture = new Texture2D(spec.Width, spec.Height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < spec.Height; y++)
            {
                for (int x = 0; x < spec.Width; x++)
                {
                    Color pixel = BuildPixel(spec, x + 0.5f, y + 0.5f);
                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Color BuildPixel(SpriteSpec spec, float x, float y)
        {
            float outerDistance = RoundedRectDistance(x, y, spec.Width, spec.Height, spec.Radius);
            float outerAlpha = Mathf.Clamp01(0.5f - outerDistance);
            if (outerAlpha <= 0f)
            {
                return Color.clear;
            }

            Color fill = spec.Fill;
            if (spec.Top.a > 0f && y > spec.Height - 7f)
            {
                fill = Blend(fill, spec.Top, Mathf.InverseLerp(spec.Height - 7f, spec.Height, y));
            }

            if (spec.Shadow.a > 0f && y < 8f)
            {
                fill = Blend(fill, spec.Shadow, Mathf.InverseLerp(8f, 0f, y));
            }

            if (spec.BottomLine && y < 2f)
            {
                fill = Blend(fill, StoragePalette.Divider, 0.82f);
            }

            if (spec.StrokeWidth > 0f && spec.Border.a > 0f)
            {
                float innerWidth = Mathf.Max(1f, spec.Width - spec.StrokeWidth * 2f);
                float innerHeight = Mathf.Max(1f, spec.Height - spec.StrokeWidth * 2f);
                float innerDistance = RoundedRectDistance(
                    x - spec.StrokeWidth,
                    y - spec.StrokeWidth,
                    innerWidth,
                    innerHeight,
                    Mathf.Max(0f, spec.Radius - spec.StrokeWidth));
                float innerAlpha = Mathf.Clamp01(0.5f - innerDistance);
                float borderAmount = Mathf.Clamp01(outerAlpha - innerAlpha + 0.35f);
                fill = Blend(fill, spec.Border, borderAmount);
            }

            fill.a *= outerAlpha;
            return fill;
        }

        private static float RoundedRectDistance(float x, float y, float width, float height, float radius)
        {
            if (radius <= 0.01f)
            {
                float dx = Mathf.Max(Mathf.Max(-x, x - width), 0f);
                float dy = Mathf.Max(Mathf.Max(-y, y - height), 0f);
                if (dx <= 0f && dy <= 0f)
                {
                    return -Mathf.Min(Mathf.Min(x, width - x), Mathf.Min(y, height - y));
                }

                return Mathf.Sqrt(dx * dx + dy * dy);
            }

            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float px = Mathf.Abs(x - halfWidth) - (halfWidth - radius);
            float py = Mathf.Abs(y - halfHeight) - (halfHeight - radius);
            float outsideX = Mathf.Max(px, 0f);
            float outsideY = Mathf.Max(py, 0f);
            float outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
            float inside = Mathf.Min(Mathf.Max(px, py), 0f);
            return outside + inside - radius;
        }

        private static Color Blend(Color baseColor, Color overlay, float amount)
        {
            float t = Mathf.Clamp01(amount) * overlay.a;
            return new Color(
                Mathf.Lerp(baseColor.r, overlay.r, t),
                Mathf.Lerp(baseColor.g, overlay.g, t),
                Mathf.Lerp(baseColor.b, overlay.b, t),
                Mathf.Lerp(baseColor.a, Mathf.Max(baseColor.a, overlay.a), t));
        }

        private static void ConfigureImporter(SpriteSpec spec)
        {
            string path = GetPath(spec.FileName);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = spec.SpriteBorder;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static string GetPath(string fileName)
        {
            return OutputDirectory + "/" + fileName;
        }

        private struct SpriteSpec
        {
            public readonly string FileName;
            public readonly int Width;
            public readonly int Height;
            public readonly float Radius;
            public readonly float StrokeWidth;
            public readonly Vector4 SpriteBorder;
            public readonly Color Fill;
            public readonly Color Border;
            public readonly Color Top;
            public readonly Color Shadow;
            public readonly bool BottomLine;

            public SpriteSpec(string fileName, int width, int height, float radius, float strokeWidth, Vector4 spriteBorder, Color fill, Color border, Color top, Color shadow, bool bottomLine)
            {
                FileName = fileName;
                Width = width;
                Height = height;
                Radius = radius;
                StrokeWidth = strokeWidth;
                SpriteBorder = spriteBorder;
                Fill = fill;
                Border = border;
                Top = top;
                Shadow = shadow;
                BottomLine = bottomLine;
            }
        }
    }
}
#endif
