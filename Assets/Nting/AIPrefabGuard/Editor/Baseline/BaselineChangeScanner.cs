using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nting.AIPrefabGuard.Editor
{
    public sealed class BaselineChangeScanner
    {
        private readonly BaselineStore baselineStore = new BaselineStore();
        private readonly HighRiskFileClassifier classifier = new HighRiskFileClassifier();

        public ScanResult Scan(string projectPath)
        {
            projectPath = Path.GetFullPath(projectPath);
            var baseline = baselineStore.LoadOrCreate(projectPath);
            var current = baselineStore.CreateSnapshot(projectPath, baseline.createdAtUtc);
            var changedFiles = BuildChanges(projectPath, baseline, current);
            var findings = classifier.Classify(changedFiles);

            return new ScanResult(
                CreateEnvironment(projectPath, baseline, string.Format("Baseline scan completed. {0} changed file(s) found since baseline.", changedFiles.Count)),
                changedFiles,
                findings,
                DateTime.Now);
        }

        public ScanResult AcceptCurrentStateAsBaseline(string projectPath)
        {
            projectPath = Path.GetFullPath(projectPath);
            var baseline = baselineStore.AcceptCurrentState(projectPath);
            return new ScanResult(
                CreateEnvironment(projectPath, baseline, "Current project state accepted as the new baseline."),
                Array.Empty<GitChangedFile>(),
                Array.Empty<RiskFinding>(),
                DateTime.Now);
        }

        public ScanResult ResetBaseline(string projectPath)
        {
            projectPath = Path.GetFullPath(projectPath);
            baselineStore.Reset(projectPath);
            var baseline = baselineStore.LoadOrCreate(projectPath);
            return new ScanResult(
                CreateEnvironment(projectPath, baseline, "Baseline reset to current project state."),
                Array.Empty<GitChangedFile>(),
                Array.Empty<RiskFinding>(),
                DateTime.Now);
        }

        public ScanResult GetInitialResult(string projectPath)
        {
            projectPath = Path.GetFullPath(projectPath);
            var baseline = baselineStore.LoadOrCreate(projectPath);
            return new ScanResult(
                CreateEnvironment(projectPath, baseline, "Baseline ready. Scan changes after AI edits."),
                Array.Empty<GitChangedFile>(),
                Array.Empty<RiskFinding>(),
                DateTime.Now);
        }

        private static IReadOnlyList<GitChangedFile> BuildChanges(string projectPath, BaselineSnapshot baseline, BaselineSnapshot current)
        {
            var baselineMap = ToMap(baseline);
            var currentMap = ToMap(current);
            var changes = new List<GitChangedFile>();

            foreach (var currentEntry in currentMap.Values)
            {
                if (!baselineMap.TryGetValue(currentEntry.relativePath, out var baselineEntry))
                {
                    changes.Add(new GitChangedFile(currentEntry.relativePath, string.Empty, "A", GitChangeKind.Added, true));
                    continue;
                }

                if (!string.Equals(currentEntry.sha256, baselineEntry.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add(new GitChangedFile(currentEntry.relativePath, string.Empty, "M", GitChangeKind.Modified, true));
                }
            }

            foreach (var baselineEntry in baselineMap.Values)
            {
                if (!currentMap.ContainsKey(baselineEntry.relativePath))
                {
                    changes.Add(new GitChangedFile(baselineEntry.relativePath, string.Empty, "D", GitChangeKind.Deleted, false));
                }
            }

            return changes
                .OrderBy(change => change.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, BaselineFileEntry> ToMap(BaselineSnapshot snapshot)
        {
            return (snapshot.files ?? Array.Empty<BaselineFileEntry>())
                .GroupBy(entry => entry.relativePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        private GitScanEnvironment CreateEnvironment(string projectPath, BaselineSnapshot baseline, string message)
        {
            return GitScanEnvironment.Baseline(
                projectPath,
                baselineStore.GetBaselinePath(projectPath),
                true,
                baseline.createdAtUtc,
                baseline.updatedAtUtc,
                baseline.FileCount,
                message);
        }
    }
}
