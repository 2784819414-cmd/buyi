using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeObjectSettingsStore
    {
        internal static string GetFolder(string importRoot, string objectId)
        {
            return CampusRuntimeImportLibrary.GetObjectSettingsFolder(importRoot, objectId);
        }

        internal static string GetPath(string importRoot, string objectId)
        {
            return CampusRuntimeImportLibrary.GetObjectSettingsPath(importRoot, objectId);
        }

        internal static string Save(string importRoot, CampusRuntimeObjectSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.ObjectId))
            {
                return string.Empty;
            }

            Normalize(settings, importRoot, settings.ObjectId);
            string folder = GetFolder(importRoot, settings.ObjectId);
            Directory.CreateDirectory(folder);
            string path = GetPath(importRoot, settings.ObjectId);
            File.WriteAllText(path, JsonUtility.ToJson(settings, true), Encoding.UTF8);
            return path;
        }

        internal static CampusRuntimeObjectSettings Load(string importRoot, string objectId, Action<string> logWarning)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            string normalizedObjectId = objectId.Trim();
            string path = GetPath(importRoot, normalizedObjectId);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                CampusRuntimeObjectSettings settings =
                    JsonUtility.FromJson<CampusRuntimeObjectSettings>(File.ReadAllText(path, Encoding.UTF8));
                Normalize(settings, importRoot, normalizedObjectId);
                return settings;
            }
            catch (Exception exception)
            {
                if (logWarning != null)
                {
                    logWarning("Failed to load object settings '" + path + "': " + exception.Message);
                }

                return null;
            }
        }

        internal static bool DeleteFolder(string importRoot, string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            string folder = GetFolder(importRoot, objectId);
            if (!Directory.Exists(folder))
            {
                return false;
            }

            Directory.Delete(folder, true);
            return true;
        }

        internal static string CopyDirectionSprite(
            string importRoot,
            string objectId,
            int rotation90Index,
            string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            string folder = GetFolder(importRoot, objectId);
            Directory.CreateDirectory(folder);
            string prefix = "rotation_" + (rotation90Index * 90);
            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".png";
            }

            string targetPath = Path.Combine(folder, prefix + extension.ToLowerInvariant());
            string sourceFullPath = Path.GetFullPath(sourcePath);
            string targetFullPath = Path.GetFullPath(targetPath);
            if (string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return CampusRuntimeImportLibrary.NormalizeSerializedPath(targetPath, importRoot);
            }

            string[] existing = Directory.GetFiles(folder, prefix + ".*");
            for (int i = 0; i < existing.Length; i++)
            {
                if (!string.Equals(Path.GetFullPath(existing[i]), sourceFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(existing[i]);
                }
            }

            File.Copy(sourcePath, targetPath, true);
            return CampusRuntimeImportLibrary.NormalizeSerializedPath(targetPath, importRoot);
        }

        internal static void Normalize(CampusRuntimeObjectSettings settings, string importRoot, string fallbackObjectId)
        {
            if (settings == null)
            {
                return;
            }

            settings.ObjectId = string.IsNullOrWhiteSpace(settings.ObjectId)
                ? (string.IsNullOrWhiteSpace(fallbackObjectId) ? string.Empty : fallbackObjectId.Trim())
                : settings.ObjectId.Trim();
            settings.TypeId = string.IsNullOrWhiteSpace(settings.TypeId) ? string.Empty : settings.TypeId.Trim();
            settings.DisplayNameOverride = string.IsNullOrWhiteSpace(settings.DisplayNameOverride)
                ? string.Empty
                : settings.DisplayNameOverride.Trim();
            settings.FootprintSize = CampusPlacedObject.NormalizeFootprintSize(settings.FootprintSize);
            settings.VisualScale = CampusPlacedObject.NormalizeVisualScale(settings.VisualScale);
            settings.StorageSize = CampusPlacedObject.NormalizeStorageSize(settings.StorageSize);
            settings.StorageMaxWeight = CampusPlacedObject.NormalizeStorageMaxWeight(settings.StorageMaxWeight);
            settings.CustomInteractionAnchorRadius =
                CampusPlacedObject.NormalizeInteractionAnchorRadius(settings.CustomInteractionAnchorRadius);
            settings.CustomInteractionPromptText = string.IsNullOrWhiteSpace(settings.CustomInteractionPromptText)
                ? string.Empty
                : settings.CustomInteractionPromptText.Trim();
            settings.CustomInteractionAnchors =
                CampusPlacedObject.CloneInteractionAnchors(settings.CustomInteractionAnchors);
            settings.Rotation0SpritePath =
                CampusRuntimeImportLibrary.NormalizeSerializedPath(settings.Rotation0SpritePath, importRoot);
            settings.Rotation90SpritePath =
                CampusRuntimeImportLibrary.NormalizeSerializedPath(settings.Rotation90SpritePath, importRoot);
            settings.Rotation180SpritePath =
                CampusRuntimeImportLibrary.NormalizeSerializedPath(settings.Rotation180SpritePath, importRoot);
            settings.Rotation270SpritePath =
                CampusRuntimeImportLibrary.NormalizeSerializedPath(settings.Rotation270SpritePath, importRoot);
            settings.RetailShelf = settings.RetailShelf ?? new CampusRuntimeRetailShelfData();
            CampusRuntimeObjectAuthoring.NormalizeRetailShelfData(settings.RetailShelf);
            settings.ProtectedStockContainer =
                settings.ProtectedStockContainer ?? new CampusRuntimeProtectedStockContainerData();
            CampusRuntimeObjectAuthoring.NormalizeProtectedStockContainerData(settings.ProtectedStockContainer);
        }
    }
}
