using System;
using System.Text;

namespace Nting.AIPrefabGuard.Editor
{
    public static class MarkdownReportBuilder
    {
        public static string Build(ScanResult result, PromptTemplateKind promptTemplateKind)
        {
            return Build(result, promptTemplateKind, AIPrefabGuardLanguage.English);
        }

        public static string Build(ScanResult result, PromptTemplateKind promptTemplateKind, AIPrefabGuardLanguage language)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# AI Prefab Guard Risk Report");
            builder.AppendLine();
            builder.AppendLine(string.Format("- Generated At: {0}", result != null ? result.ScannedAt.ToString("yyyy-MM-dd HH:mm:ss") : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            builder.AppendLine(string.Format("- Project Path: {0}", result != null ? EmptyFallback(result.Environment.ProjectPath, "N/A") : "N/A"));
            builder.AppendLine(string.Format("- Baseline Path: {0}", result != null ? EmptyFallback(result.Environment.BaselinePath, "N/A") : "N/A"));
            builder.AppendLine(string.Format("- Baseline Created: {0}", result != null ? EmptyFallback(result.Environment.BaselineCreatedAtUtc, "N/A") : "N/A"));
            builder.AppendLine(string.Format("- Baseline Updated: {0}", result != null ? EmptyFallback(result.Environment.BaselineUpdatedAtUtc, "N/A") : "N/A"));
            builder.AppendLine(string.Format("- Baseline File Count: {0}", result != null ? result.Environment.BaselineFileCount : 0));
            builder.AppendLine(string.Format("- Scan Status: {0}", result != null ? EmptyFallback(result.Environment.Message, "N/A") : "N/A"));
            builder.AppendLine(string.Format("- Overall Risk: {0}", result != null ? result.OverallRisk.ToString() : RiskLevel.Low.ToString()));
            builder.AppendLine(string.Format("- Files Changed Since Baseline: {0}", result != null ? result.ChangedFiles.Count : 0));
            builder.AppendLine(string.Format("- High-risk Unity Files: {0}", result != null ? result.Findings.Count : 0));
            builder.AppendLine(string.Format("- Very High Count: {0}", result != null ? result.VeryHighCount : 0));
            builder.AppendLine(string.Format("- High Count: {0}", result != null ? result.HighCount : 0));
            builder.AppendLine();

            builder.AppendLine("## Natural Language Summary");
            builder.AppendLine();
            builder.AppendLine(NaturalLanguageRiskSummaryBuilder.Build(result, language));
            builder.AppendLine();

            builder.AppendLine("## Review Scope");
            builder.AppendLine();
            builder.AppendLine("- This report is generated locally inside the Unity Editor.");
            builder.AppendLine("- The scan compares current Assets/ files against a local baseline stored under Library/AIPrefabGuard.");
            builder.AppendLine("- No project files are uploaded.");
            builder.AppendLine("- No AI API is called by AI Prefab Guard.");
            builder.AppendLine("- The tool does not automatically fix, rewrite, or regenerate Unity assets.");
            builder.AppendLine("- Findings are risk signals for manual review, not proof that a change is wrong.");
            builder.AppendLine("- Deleted or missing files cannot be opened from Unity and should be reviewed through project history or backup context.");
            builder.AppendLine();

            builder.AppendLine("## High-risk Files");
            builder.AppendLine();
            if (result == null || result.Findings.Count == 0)
            {
                builder.AppendLine("No high-risk Unity asset files were detected in the changes since the local baseline.");
            }
            else
            {
                foreach (var finding in result.Findings)
                {
                    builder.AppendLine(string.Format("### {0}", finding.File.RelativePath));
                    builder.AppendLine();
                    builder.AppendLine(string.Format("- Risk: {0}", finding.RiskLevel));
                    builder.AppendLine(string.Format("- Type: {0}", finding.FileType));
                    builder.AppendLine(string.Format("- Baseline Status: {0}", finding.File.StatusCode));
                    builder.AppendLine(string.Format("- Change Kind: {0}", finding.File.ChangeKind));
                    builder.AppendLine(string.Format("- Reason: {0}", finding.Reason));
                    builder.AppendLine();
                    builder.AppendLine("Checklist:");
                    foreach (var item in finding.Checklist)
                    {
                        builder.AppendLine(string.Format("- [ ] {0}", item));
                    }

                    if (finding.Insights.Count > 0)
                    {
                        builder.AppendLine();
                        builder.AppendLine("Notes:");
                        foreach (var insight in finding.Insights)
                        {
                            builder.AppendLine(string.Format("- {0}", insight));
                        }
                    }

                    builder.AppendLine();
                }
            }

            builder.AppendLine("## Manual Verification Plan");
            builder.AppendLine();
            if (result == null || result.Findings.Count == 0)
            {
                builder.AppendLine("- Review AI-generated code changes as usual.");
                builder.AppendLine("- Run the smallest relevant Unity Play Mode flow before accepting the current state as the new baseline.");
            }
            else
            {
                builder.AppendLine("- Start with Very High risk files before High risk files.");
                builder.AppendLine("- For scenes and prefabs, inspect hierarchy, components, serialized references, Missing Script warnings, Console output, and Play Mode behavior.");
                builder.AppendLine("- For meta files, verify GUID and import setting changes were intentional.");
                builder.AppendLine("- For asmdef files, wait for Unity compilation and review assembly references/platform filters.");
                builder.AppendLine("- Only accept the current state as the new baseline after manual review is complete.");
            }
            builder.AppendLine();

            builder.AppendLine("## AI-readable Summary");
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine(string.Format("overall_risk={0}", result != null ? result.OverallRisk.ToString() : RiskLevel.Low.ToString()));
            builder.AppendLine(string.Format("changed_since_baseline_count={0}", result != null ? result.ChangedFiles.Count : 0));
            builder.AppendLine(string.Format("high_risk_file_count={0}", result != null ? result.Findings.Count : 0));
            builder.AppendLine(string.Format("baseline_updated_at={0}", result != null ? EmptyFallback(result.Environment.BaselineUpdatedAtUtc, "N/A") : "N/A"));
            if (result != null)
            {
                foreach (var finding in result.Findings)
                {
                    builder.AppendLine(string.Format("file={0};risk={1};type={2};change={3}", finding.File.RelativePath, finding.RiskLevel, finding.FileType, finding.File.ChangeKind));
                }
            }
            builder.AppendLine("```");
            builder.AppendLine();

            builder.AppendLine("## Codex AI Handoff Prompt");
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine(ReworkPromptBuilder.Build(result, promptTemplateKind, language).TrimEnd());
            builder.AppendLine("```");

            return builder.ToString();
        }

        private static string EmptyFallback(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
