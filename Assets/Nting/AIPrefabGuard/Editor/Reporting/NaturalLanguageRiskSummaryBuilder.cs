using System.Linq;
using System.Text;

namespace Nting.AIPrefabGuard.Editor
{
    public static class NaturalLanguageRiskSummaryBuilder
    {
        public static string Build(ScanResult result)
        {
            return Build(result, AIPrefabGuardLanguage.English);
        }

        public static string Build(ScanResult result, AIPrefabGuardLanguage language)
        {
            return language == AIPrefabGuardLanguage.Chinese ? BuildChinese(result) : BuildEnglish(result);
        }

        private static string BuildEnglish(ScanResult result)
        {
            if (result == null)
            {
                return "No scan result is available yet. Open AI Prefab Guard to create a local baseline, then scan after AI edits.";
            }

            if (IsScanFailed(result))
            {
                return "The baseline scan failed before a reliable result could be generated. Review the scan status, refresh assets, then scan again.";
            }

            if (result.ChangedFiles.Count == 0)
            {
                return "Current risk is Low. The current Assets state matches the local baseline, so no Unity asset changes need risk review right now.";
            }

            if (result.Findings.Count == 0)
            {
                return string.Format(
                    "Current risk is Low. {0} file(s) changed since the local baseline, but no .prefab, .unity, .meta, .asset, or .asmdef high-risk Unity asset files were detected. Continue reviewing code changes and runtime behavior before accepting the new baseline.",
                    result.ChangedFiles.Count);
            }

            var builder = new StringBuilder();
            builder.AppendFormat(
                "Current risk is {0}. {1} file(s) changed since the local baseline, including {2} high-risk Unity asset file(s) that should be manually reviewed before accepting the new baseline.",
                result.OverallRisk,
                result.ChangedFiles.Count,
                result.Findings.Count);

            builder.AppendFormat(
                " Very High: {0}; High: {1}.",
                result.VeryHighCount,
                result.HighCount);

            var topFindings = result.Findings.Take(3).ToList();
            if (topFindings.Count > 0)
            {
                builder.Append(" Review these first: ");
                builder.Append(string.Join("; ", topFindings.Select(finding => string.Format("{0} ({1}, {2}, {3})", finding.File.RelativePath, finding.FileType, finding.RiskLevel, finding.File.ChangeKind))));
                builder.Append(".");
            }

            builder.Append(" Recommended action: inspect these files in Unity, check Missing Script warnings and Console output, and run the smallest relevant Play Mode flow. Findings are risk signals, not proof that a change is wrong.");
            return builder.ToString();
        }

        private static string BuildChinese(ScanResult result)
        {
            if (result == null)
            {
                return "还没有扫描结果。打开 AI Prefab Guard 会先创建本地基线，AI 修改后再扫描即可。";
            }

            if (IsScanFailed(result))
            {
                return "基线扫描失败，暂时不能给出可靠结论。请查看扫描状态，刷新资源后再扫描。";
            }

            if (result.ChangedFiles.Count == 0)
            {
                return "当前风险为 Low。当前 Assets 状态与本地基线一致，因此目前没有需要复查的 Unity 资源风险。";
            }

            if (result.Findings.Count == 0)
            {
                return string.Format(
                    "当前风险为 Low。相对本地基线有 {0} 个文件改动，但没有检测到 .prefab、.unity、.meta、.asset 或 .asmdef 这类高风险 Unity 资源文件。确认新基线前仍建议继续检查代码改动和运行表现。",
                    result.ChangedFiles.Count);
            }

            var builder = new StringBuilder();
            builder.AppendFormat(
                "当前风险为 {0}。相对本地基线共有 {1} 个文件改动，其中 {2} 个是建议确认新基线前人工复查的 Unity 高风险资源文件。",
                result.OverallRisk,
                result.ChangedFiles.Count,
                result.Findings.Count);

            builder.AppendFormat(
                " Very High {0} 个，High {1} 个。",
                result.VeryHighCount,
                result.HighCount);

            var topFindings = result.Findings.Take(3).ToList();
            if (topFindings.Count > 0)
            {
                builder.Append(" 建议优先检查：");
                builder.Append(string.Join("；", topFindings.Select(finding => string.Format("{0}（{1}，{2}，{3}）", finding.File.RelativePath, finding.FileType, finding.RiskLevel, finding.File.ChangeKind))));
                builder.Append("。");
            }

            builder.Append(" 建议动作：在 Unity 中检查这些文件的层级、组件、引用、Missing Script、Console 输出和最小相关 Play Mode 流程。这里的发现是风险信号，不代表改动一定错误。");
            return builder.ToString();
        }

        private static bool IsScanFailed(ScanResult result)
        {
            return result.Environment != null &&
                result.Environment.Message != null &&
                result.Environment.Message.StartsWith("Scan failed:", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
