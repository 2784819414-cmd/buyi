using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Nting.AIPrefabGuard.Editor
{
    public sealed class BaselineStore
    {
        private const string BaselineRelativePath = "Library/AIPrefabGuard/baseline.json";

        public string GetBaselinePath(string projectPath)
        {
            return Path.Combine(projectPath, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public bool HasBaseline(string projectPath)
        {
            return File.Exists(GetBaselinePath(projectPath));
        }

        public BaselineSnapshot LoadOrCreate(string projectPath)
        {
            if (HasBaseline(projectPath))
            {
                return Load(projectPath);
            }

            var snapshot = CreateSnapshot(projectPath, string.Empty);
            Save(projectPath, snapshot);
            return snapshot;
        }

        public BaselineSnapshot Load(string projectPath)
        {
            var path = GetBaselinePath(projectPath);
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonUtility.FromJson<BaselineSnapshot>(File.ReadAllText(path, Encoding.UTF8));
        }

        public BaselineSnapshot AcceptCurrentState(string projectPath)
        {
            var existing = Load(projectPath);
            var createdAtUtc = existing == null || string.IsNullOrEmpty(existing.createdAtUtc)
                ? DateTime.UtcNow.ToString("o")
                : existing.createdAtUtc;
            var snapshot = CreateSnapshot(projectPath, createdAtUtc);
            Save(projectPath, snapshot);
            return snapshot;
        }

        public void Reset(string projectPath)
        {
            var path = GetBaselinePath(projectPath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public BaselineSnapshot CreateSnapshot(string projectPath, string existingCreatedAtUtc)
        {
            var now = DateTime.UtcNow.ToString("o");
            var snapshot = new BaselineSnapshot
            {
                version = 1,
                projectPath = projectPath ?? string.Empty,
                createdAtUtc = string.IsNullOrEmpty(existingCreatedAtUtc) ? now : existingCreatedAtUtc,
                updatedAtUtc = now,
                files = EnumerateAssetFiles(projectPath)
                    .Select(path => CreateEntry(projectPath, path))
                    .OrderBy(entry => entry.relativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };

            return snapshot;
        }

        public IReadOnlyList<string> EnumerateAssetFiles(string projectPath)
        {
            var assetsPath = Path.Combine(projectPath, "Assets");
            if (!Directory.Exists(assetsPath))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(assetsPath, "*", SearchOption.AllDirectories)
                .Where(path => !ShouldExclude(projectPath, path))
                .Select(path => ToProjectRelativePath(projectPath, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool ShouldExclude(string projectPath, string absolutePath)
        {
            var relative = ToProjectRelativePath(projectPath, absolutePath);
            if (relative.EndsWith(".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                relative.EndsWith("Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return relative.StartsWith("Assets/Nting/AIPrefabGuard/", StringComparison.OrdinalIgnoreCase);
        }

        private static BaselineFileEntry CreateEntry(string projectPath, string relativePath)
        {
            var absolutePath = Path.Combine(projectPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var info = new FileInfo(absolutePath);
            return new BaselineFileEntry(
                relativePath,
                info.Exists ? info.Length : 0,
                info.Exists ? info.LastWriteTimeUtc.Ticks : 0,
                info.Exists ? ComputeSha256(absolutePath) : string.Empty);
        }

        private static string ComputeSha256(string absolutePath)
        {
            using (var stream = File.OpenRead(absolutePath))
            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private void Save(string projectPath, BaselineSnapshot snapshot)
        {
            var path = GetBaselinePath(projectPath);
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(snapshot, true), Encoding.UTF8);
        }

        private static string ToProjectRelativePath(string projectPath, string absolutePath)
        {
            var fullProjectPath = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(absolutePath);
            return fullPath.Substring(fullProjectPath.Length + 1).Replace('\\', '/');
        }
    }
}
