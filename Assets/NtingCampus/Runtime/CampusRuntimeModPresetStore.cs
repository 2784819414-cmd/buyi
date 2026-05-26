using System;
using System.IO;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeModPresetStore
    {
        internal const string PresetFolderRelativeToAssets =
            "NtingCampus/UserGeneratedRuntimeContent/CampusRuntimeImports/RuntimePresets";

        internal static bool TryReadJson(string fileName, out string json)
        {
            json = string.Empty;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string path = GetPresetPath(fileName);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                json = File.ReadAllText(path);
                return !string.IsNullOrWhiteSpace(json);
            }
            catch (Exception exception)
            {
                CampusRuntimePresetLogTextCatalog.Warning(
                    CampusRuntimePresetLogTextId.FailedToReadPresetFile,
                    path,
                    exception.Message);
                return false;
            }
        }

        internal static string GetPresetPath(string fileName)
        {
            string assetsPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(assetsPath))
            {
                assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            }

            return Path.Combine(
                assetsPath,
                PresetFolderRelativeToAssets.Replace('/', Path.DirectorySeparatorChar),
                fileName);
        }

        internal static Color ParseColor(string html, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return fallback;
            }

            string value = html.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                value = "#" + value;
            }

            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : fallback;
        }
    }
}
