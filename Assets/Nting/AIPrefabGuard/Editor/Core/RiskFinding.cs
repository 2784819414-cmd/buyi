using System.Collections.Generic;

namespace Nting.AIPrefabGuard.Editor
{
    public sealed class RiskFinding
    {
        public RiskFinding(
            GitChangedFile file,
            RiskLevel riskLevel,
            RiskFileType fileType,
            string reason,
            IReadOnlyList<string> checklist,
            IReadOnlyList<string> insights)
        {
            File = file;
            RiskLevel = riskLevel;
            FileType = fileType;
            Reason = reason ?? string.Empty;
            Checklist = checklist ?? new List<string>();
            Insights = insights ?? new List<string>();
        }

        public GitChangedFile File { get; }

        public RiskLevel RiskLevel { get; }

        public RiskFileType FileType { get; }

        public string Reason { get; }

        public IReadOnlyList<string> Checklist { get; }

        public IReadOnlyList<string> Insights { get; }

        public bool CanPing
        {
            get { return File != null && File.ExistsOnDisk; }
        }

        public bool CanOpen
        {
            get { return CanPing && FileType != RiskFileType.Meta; }
        }
    }
}
