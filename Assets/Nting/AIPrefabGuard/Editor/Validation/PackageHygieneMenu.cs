using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nting.AIPrefabGuard.Editor
{
    public static class PackageHygieneMenu
    {
        private const string PackageRoot = "Packages/com.nting.ai-prefab-guard";

        [MenuItem("Tools/Nting/AI Prefab Guard/Validate Package Hygiene")]
        public static void ValidatePackageHygiene()
        {
            var issues = PackageHygieneValidator.Validate(PackageRoot);
            if (issues.Count == 0)
            {
                Debug.Log("AI Prefab Guard package hygiene check passed.");
                EditorUtility.DisplayDialog("AI Prefab Guard", "Package hygiene check passed.", "OK");
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Package hygiene check found issues:");
            foreach (var issue in issues)
            {
                builder.AppendLine("- " + issue.Path + ": " + issue.Message);
            }

            Debug.LogWarning(builder.ToString());
            EditorUtility.DisplayDialog("AI Prefab Guard", builder.ToString(), "OK");
        }
    }
}
