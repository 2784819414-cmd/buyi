using System;
using System.Collections.Generic;
using System.IO;

namespace Nting.AIPrefabGuard.Editor
{
    public static class PackageHygieneValidator
    {
        private static readonly string[] ForbiddenDirectoryNames =
        {
            ".git",
            ".aiprefabguard-tools",
            "Library",
            "Logs",
            "Temp",
            "Obj",
            "Build",
            "Builds",
            "UserSettings"
        };

        private static readonly string[] ForbiddenExtensions =
        {
            ".zip",
            ".7z",
            ".rar",
            ".unitypackage"
        };

        public static IReadOnlyList<PackageHygieneIssue> Validate(string packageRoot)
        {
            var issues = new List<PackageHygieneIssue>();
            if (string.IsNullOrEmpty(packageRoot) || !Directory.Exists(packageRoot))
            {
                issues.Add(new PackageHygieneIssue(packageRoot, "Package root does not exist."));
                return issues;
            }

            foreach (var directory in Directory.GetDirectories(packageRoot, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(directory);
                foreach (var forbidden in ForbiddenDirectoryNames)
                {
                    if (string.Equals(name, forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new PackageHygieneIssue(ToForwardSlashPath(directory), "Forbidden local/generated directory is inside the package."));
                    }
                }
            }

            foreach (var file in Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file);
                foreach (var forbidden in ForbiddenExtensions)
                {
                    if (string.Equals(extension, forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new PackageHygieneIssue(ToForwardSlashPath(file), "Archive/package file should not be bundled in the package."));
                    }
                }
            }

            return issues;
        }

        private static string ToForwardSlashPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
