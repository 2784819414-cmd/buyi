using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nting.AIPrefabGuard.Editor
{
    public static class AssetLocator
    {
        public static bool Ping(GitScanEnvironment environment, RiskFinding finding)
        {
            var asset = LoadAsset(environment, finding, out _);
            if (asset == null)
            {
                return false;
            }

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
            return true;
        }

        public static bool Open(GitScanEnvironment environment, RiskFinding finding)
        {
            var asset = LoadAsset(environment, finding, out _);
            return asset != null && AssetDatabase.OpenAsset(asset);
        }

        public static string GetUnityAssetPath(GitScanEnvironment environment, RiskFinding finding)
        {
            LoadAsset(environment, finding, out var assetPath);
            return assetPath;
        }

        private static UnityEngine.Object LoadAsset(GitScanEnvironment environment, RiskFinding finding, out string assetPath)
        {
            assetPath = ToUnityAssetPath(environment, finding);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            return AssetDatabase.LoadMainAssetAtPath(assetPath);
        }

        private static string ToUnityAssetPath(GitScanEnvironment environment, RiskFinding finding)
        {
            if (environment == null || finding == null || finding.File == null)
            {
                return string.Empty;
            }

            var relativePath = finding.File.RelativePath;
            if (finding.FileType == RiskFileType.Meta && relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Substring(0, relativePath.Length - ".meta".Length);
            }

            if (relativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return relativePath;
            }

            if (string.IsNullOrEmpty(environment.RepositoryPath) || string.IsNullOrEmpty(environment.ProjectPath))
            {
                return string.Empty;
            }

            var absolutePath = Path.GetFullPath(Path.Combine(environment.RepositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var projectPath = Path.GetFullPath(environment.ProjectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!absolutePath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var projectRelativePath = absolutePath.Substring(projectPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return projectRelativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }
}
