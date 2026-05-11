using System;
using UnityEditor;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public sealed class CampusPropTextureImportPostprocessor : AssetPostprocessor
    {
        private static readonly string[] PropTextureRoots =
        {
            "Assets/NtingCampus/Tiles/Source/Props/",
            "Assets/NtingCampus/Textures/Props/"
        };

        private void OnPreprocessTexture()
        {
            if (!IsPropTexturePath(assetPath))
            {
                return;
            }

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
        }

        private static bool IsPropTexturePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = 0; i < PropTextureRoots.Length; i++)
            {
                if (path.StartsWith(PropTextureRoots[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
