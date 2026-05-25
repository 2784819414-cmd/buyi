using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeImportLibrary
    {
        internal const string RuntimeImportFolder = "CampusRuntimeImports";
        internal const string FloorImportFolder = "Floors";
        internal const string WallImportFolder = "Walls";
        internal const string ObjectImportFolder = "Objects";
        internal const string ObjectSettingsFolder = "ObjectSettings";
        internal const string ObjectDefinitionsFile = "ObjectDefinitions.json";
        internal const string RoomImportFile = "Rooms.txt";
        internal const string RoomPrefabFolder = "RoomPrefabs";

        internal static string GetPersistentImportRootFolder()
        {
            return Path.Combine(Application.persistentDataPath, RuntimeImportFolder);
        }

        internal static string GetFloorImportFolder(string importRoot)
        {
            return Path.Combine(importRoot, FloorImportFolder);
        }

        internal static string GetWallImportFolder(string importRoot)
        {
            return Path.Combine(importRoot, WallImportFolder);
        }

        internal static string GetObjectImportFolder(string importRoot)
        {
            return Path.Combine(importRoot, ObjectImportFolder);
        }

        internal static string GetObjectSettingsRootFolder(string importRoot)
        {
            return Path.Combine(importRoot, ObjectSettingsFolder);
        }

        internal static string GetObjectSettingsFolder(string importRoot, string objectId)
        {
            return Path.Combine(GetObjectSettingsRootFolder(importRoot), SanitizeFileName(objectId));
        }

        internal static string GetObjectSettingsPath(string importRoot, string objectId)
        {
            return Path.Combine(GetObjectSettingsFolder(importRoot, objectId), "settings.json");
        }

        internal static string GetObjectDefinitionsPath(string importRoot)
        {
            return Path.Combine(importRoot, ObjectDefinitionsFile);
        }

        internal static string GetRoomImportFile(string importRoot)
        {
            return Path.Combine(importRoot, RoomImportFile);
        }

        internal static string GetRoomPrefabFolder(string importRoot)
        {
            return Path.Combine(importRoot, RoomPrefabFolder);
        }

        internal static void EnsureFolders(string importRoot)
        {
            Directory.CreateDirectory(importRoot);
            Directory.CreateDirectory(GetFloorImportFolder(importRoot));
            Directory.CreateDirectory(GetWallImportFolder(importRoot));
            Directory.CreateDirectory(GetObjectImportFolder(importRoot));
            Directory.CreateDirectory(GetObjectSettingsRootFolder(importRoot));
            Directory.CreateDirectory(GetRoomPrefabFolder(importRoot));

            string roomFile = GetRoomImportFile(importRoot);
            if (!File.Exists(roomFile))
            {
                File.WriteAllText(roomFile, "# One room per line: RoomName or RoomName,Count\n", Encoding.UTF8);
            }
        }

        internal static string NormalizeSerializedPath(string path, string importRoot)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = NormalizeClipboardPath(path).Replace('\\', '/');
            if (!Path.IsPathRooted(normalized))
            {
                const string importFolderPrefix = RuntimeImportFolder + "/";
                if (normalized.StartsWith(importFolderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(importFolderPrefix.Length);
                }

                return normalized.TrimStart('/');
            }

            string root = Path.GetFullPath(importRoot).Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(root.Length + 1);
            }

            return Path.GetFileName(normalized);
        }

        internal static string ResolveContentPath(string path, string importRoot)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = NormalizeClipboardPath(path);
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            normalized = normalized.Replace('\\', '/');
            const string importFolderPrefix = RuntimeImportFolder + "/";
            if (normalized.StartsWith(importFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(importFolderPrefix.Length);
            }

            string relativePath = normalized.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(importRoot, relativePath);
        }

        internal static string[] GetImageFiles(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return Array.Empty<string>();
            }

            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(folder, "*.png"));
            files.AddRange(Directory.GetFiles(folder, "*.jpg"));
            files.AddRange(Directory.GetFiles(folder, "*.jpeg"));
            files.AddRange(Directory.GetFiles(folder, "*.bmp"));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files.ToArray();
        }

        internal static bool IsSupportedImage(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase);
        }

        internal static string FindImagePathByName(string folder, string assetName)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(assetName))
            {
                return string.Empty;
            }

            string[] files = GetImageFiles(folder);
            for (int i = 0; i < files.Length; i++)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(files[i]), assetName, StringComparison.OrdinalIgnoreCase))
                {
                    return files[i];
                }
            }

            return string.Empty;
        }

        internal static int ImportFiles(string[] paths, string targetFolder, bool requireImage)
        {
            if (paths == null || paths.Length == 0)
            {
                return 0;
            }

            Directory.CreateDirectory(targetFolder);
            int count = 0;
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (Directory.Exists(path))
                {
                    MirrorDirectory(path, Path.Combine(targetFolder, Path.GetFileName(path)), false);
                    count++;
                    continue;
                }

                if (!File.Exists(path) || (requireImage && !IsSupportedImage(path)))
                {
                    continue;
                }

                string destination = MakeUniquePath(Path.Combine(targetFolder, Path.GetFileName(path)));
                File.Copy(path, destination, false);
                count++;
            }

            return count;
        }

        internal static int ImportClipboardImages(string clipboardText, string targetFolder)
        {
            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                return 0;
            }

            Directory.CreateDirectory(targetFolder);
            int count = 0;
            string[] candidates = clipboardText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < candidates.Length; i++)
            {
                string normalized = NormalizeClipboardPath(candidates[i]);
                if (!File.Exists(normalized) || !IsSupportedImage(normalized))
                {
                    continue;
                }

                File.Copy(normalized, MakeUniquePath(Path.Combine(targetFolder, Path.GetFileName(normalized))), false);
                count++;
            }

            return count;
        }

        internal static string MakeUniquePath(string path)
        {
            string folder = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string candidate = path;
            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder ?? string.Empty, fileName + "_" + suffix + extension);
                suffix++;
            }

            return candidate;
        }

        internal static bool AreSamePath(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            string left = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string right = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal static void MirrorDirectory(string source, string destination, bool deleteExtra)
        {
            if (!Directory.Exists(source))
            {
                return;
            }

            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string target = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, target, true);
            }

            string[] directories = Directory.GetDirectories(source);
            for (int i = 0; i < directories.Length; i++)
            {
                string directory = directories[i];
                string target = Path.Combine(destination, Path.GetFileName(directory));
                MirrorDirectory(directory, target, deleteExtra);
            }

            if (!deleteExtra)
            {
                return;
            }

            foreach (string destinationFile in Directory.GetFiles(destination))
            {
                string sourceFile = Path.Combine(source, Path.GetFileName(destinationFile));
                if (!File.Exists(sourceFile))
                {
                    File.Delete(destinationFile);
                }
            }

            foreach (string destinationDirectory in Directory.GetDirectories(destination))
            {
                string sourceDirectory = Path.Combine(source, Path.GetFileName(destinationDirectory));
                if (!Directory.Exists(sourceDirectory))
                {
                    Directory.Delete(destinationDirectory, true);
                }
            }
        }

        internal static string BackupImportRoot(string importRoot)
        {
            if (!Directory.Exists(importRoot))
            {
                return string.Empty;
            }

            string backup = importRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            MirrorDirectory(importRoot, backup, false);
            return backup;
        }

        internal static bool HasImportContent(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return false;
            }

            string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string extension = Path.GetExtension(file);
                if (string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = Path.GetFileName(file);
                if (string.Equals(name, RoomImportFile, StringComparison.OrdinalIgnoreCase))
                {
                    string text = File.ReadAllText(file, Encoding.UTF8);
                    if (text.StartsWith("# One room per line", StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                return true;
            }

            return false;
        }

        internal static string NormalizeClipboardPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string value = path.Trim().Trim('"');
            if (value.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                value = Uri.UnescapeDataString(value.Substring("file:///".Length));
            }

            return value.Replace('/', Path.DirectorySeparatorChar);
        }

        internal static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "_";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = value.Trim();
            for (int i = 0; i < invalid.Length; i++)
            {
                sanitized = sanitized.Replace(invalid[i], '_');
            }

            sanitized = sanitized.Replace('/', '_').Replace('\\', '_');
            return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
        }
    }
}
