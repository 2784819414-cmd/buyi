using System;
using System.Collections.Generic;
using System.Linq;

namespace Nting.AIPrefabGuard.Editor
{
    public sealed class ScanResult
    {
        public ScanResult(GitScanEnvironment environment, IReadOnlyList<GitChangedFile> changedFiles, IReadOnlyList<RiskFinding> findings, DateTime scannedAt)
        {
            Environment = environment;
            ChangedFiles = changedFiles ?? new List<GitChangedFile>();
            Findings = findings ?? new List<RiskFinding>();
            ScannedAt = scannedAt;
            OverallRisk = RiskSummaryBuilder.GetOverallRisk(Findings);
        }

        public GitScanEnvironment Environment { get; }

        public IReadOnlyList<GitChangedFile> ChangedFiles { get; }

        public IReadOnlyList<RiskFinding> Findings { get; }

        public DateTime ScannedAt { get; }

        public RiskLevel OverallRisk { get; }

        public int VeryHighCount
        {
            get { return Findings.Count(f => f.RiskLevel == RiskLevel.VeryHigh); }
        }

        public int HighCount
        {
            get { return Findings.Count(f => f.RiskLevel == RiskLevel.High); }
        }
    }
}
