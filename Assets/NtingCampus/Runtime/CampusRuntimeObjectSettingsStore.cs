using System;
using System.IO;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeObjectSettingsStore
    {
        internal static string GetFolder(string importRoot, string objectId)
        {
            return CampusRuntimeImportLibrary.GetObjectSettingsFolder(importRoot, objectId);
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
            settings.LocalizedDisplayNameOverride = settings.LocalizedDisplayNameOverride.HasAnyText
                ? settings.LocalizedDisplayNameOverride
                : default;
            settings.FootprintSize = CampusPlacedObject.NormalizeFootprintSize(settings.FootprintSize);
            settings.VisualScale = CampusPlacedObject.NormalizeVisualScale(settings.VisualScale);
            settings.StorageSize = CampusPlacedObject.NormalizeStorageSize(settings.StorageSize);
            settings.StorageMaxWeight = CampusPlacedObject.NormalizeStorageMaxWeight(settings.StorageMaxWeight);
            settings.CustomInteractionAnchorRadius =
                CampusPlacedObject.NormalizeInteractionAnchorRadius(settings.CustomInteractionAnchorRadius);
            settings.CustomInteractionPromptText = string.IsNullOrWhiteSpace(settings.CustomInteractionPromptText)
                ? string.Empty
                : settings.CustomInteractionPromptText.Trim();
            settings.LocalizedCustomInteractionPromptText = settings.LocalizedCustomInteractionPromptText.HasAnyText
                ? settings.LocalizedCustomInteractionPromptText
                : default;
            settings.InteractionPresetEid =
                CampusRuntimeObjectAuthoring.NormalizeInteractionPresetEid(settings.InteractionPresetEid);
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
            settings.CanStackOnPlacedObjects =
                settings.CanStackOnPlacedObjects ||
                CampusRuntimeObjectAuthoring.IsStackableFacilityObject(
                    CampusRuntimeObjectAuthoring.TryParseFacilityType(
                        settings.TypeId,
                        out CampusFacilityType facilityType)
                        ? facilityType
                        : CampusFacilityType.Unknown);
            ApplyStoredDirectionSprites(settings, importRoot, settings.ObjectId);
            settings.RetailShelf = settings.RetailShelf ?? new CampusRuntimeRetailShelfData();
            CampusRuntimeObjectAuthoring.NormalizeRetailShelfData(settings.RetailShelf);
            settings.ProtectedStockContainer =
                settings.ProtectedStockContainer ?? new CampusRuntimeProtectedStockContainerData();
            CampusRuntimeObjectAuthoring.NormalizeProtectedStockContainerData(settings.ProtectedStockContainer);
        }

        private static void ApplyStoredDirectionSprites(
            CampusRuntimeObjectSettings settings,
            string importRoot,
            string objectId)
        {
            if (settings == null || string.IsNullOrWhiteSpace(importRoot) || string.IsNullOrWhiteSpace(objectId))
            {
                return;
            }

            bool hasRotation0 = ApplyStoredDirectionSprite(
                importRoot,
                objectId,
                0,
                ref settings.OverrideRotation0Sprite,
                ref settings.Rotation0SpritePath);
            bool hasRotation90 = ApplyStoredDirectionSprite(
                importRoot,
                objectId,
                90,
                ref settings.OverrideRotation90Sprite,
                ref settings.Rotation90SpritePath);
            bool hasRotation180 = ApplyStoredDirectionSprite(
                importRoot,
                objectId,
                180,
                ref settings.OverrideRotation180Sprite,
                ref settings.Rotation180SpritePath);
            bool hasRotation270 = ApplyStoredDirectionSprite(
                importRoot,
                objectId,
                270,
                ref settings.OverrideRotation270Sprite,
                ref settings.Rotation270SpritePath);

            if (hasRotation90 || hasRotation180 || hasRotation270)
            {
                settings.OverrideAllowRotation = true;
                settings.AllowRotation = true;
            }
        }

        private static bool ApplyStoredDirectionSprite(
            string importRoot,
            string objectId,
            int degrees,
            ref bool hasOverride,
            ref string spritePath)
        {
            if (hasOverride && !string.IsNullOrWhiteSpace(spritePath))
            {
                return true;
            }

            string folder = GetFolder(importRoot, objectId);
            if (!Directory.Exists(folder))
            {
                return false;
            }

            string[] files = Directory.GetFiles(folder, "rotation_" + degrees + ".*");
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (!CampusRuntimeImportLibrary.IsSupportedImage(file))
                {
                    continue;
                }

                hasOverride = true;
                spritePath = CampusRuntimeImportLibrary.NormalizeSerializedPath(file, importRoot);
                return true;
            }

            return false;
        }
    }
}
