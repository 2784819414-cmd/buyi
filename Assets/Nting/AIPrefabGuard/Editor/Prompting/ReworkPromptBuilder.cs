using System.Text;

namespace Nting.AIPrefabGuard.Editor
{
    public static class ReworkPromptBuilder
    {
        public static string Build(ScanResult result, PromptTemplateKind templateKind)
        {
            return Build(result, templateKind, AIPrefabGuardLanguage.English);
        }

        public static string Build(ScanResult result, PromptTemplateKind templateKind, AIPrefabGuardLanguage language)
        {
            switch (templateKind)
            {
                case PromptTemplateKind.ExplainAssetChanges:
                    return BuildExplainPrompt(result, language);
                case PromptTemplateKind.ManualVerification:
                    return BuildManualVerificationPrompt(result, language);
                default:
                    return BuildConservativeReworkPrompt(result, language);
            }
        }

        public static string BuildForFinding(RiskFinding finding, ScanResult result, PromptTemplateKind templateKind, AIPrefabGuardLanguage language)
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are Codex working on a Unity project.");
            builder.AppendLine();
            builder.AppendLine("AI Prefab Guard selected one high-risk Unity asset change found since the local baseline.");
            builder.AppendLine("Current project risk summary:");
            builder.AppendLine(NaturalLanguageRiskSummaryBuilder.Build(result, language));
            builder.AppendLine();

            if (finding == null)
            {
                builder.AppendLine("No selected finding is available.");
                return builder.ToString();
            }

            builder.AppendLine("Selected high-risk file:");
            builder.AppendLine(string.Format("- Path: {0}", finding.File.RelativePath));
            builder.AppendLine(string.Format("- Risk: {0}", finding.RiskLevel));
            builder.AppendLine(string.Format("- Type: {0}", finding.FileType));
            builder.AppendLine(string.Format("- Change: {0}", finding.File.ChangeKind));
            builder.AppendLine(string.Format("- Reason: {0}", finding.Reason));
            builder.AppendLine();
            builder.AppendLine("Manual verification checklist:");
            foreach (var item in finding.Checklist)
            {
                builder.AppendLine("- " + item);
            }

            if (finding.Insights.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Semantic notes:");
                foreach (var insight in finding.Insights)
                {
                    builder.AppendLine("- " + insight);
                }
            }

            builder.AppendLine();
            builder.AppendLine("Please respond with:");
            builder.AppendLine("- Whether this Unity asset change since the baseline is truly necessary.");
            builder.AppendLine("- A safer alternative that avoids direct Unity serialized asset edits if possible.");
            builder.AppendLine("- The exact files you would change next.");
            builder.AppendLine("- Unity Editor verification steps before merge.");
            builder.AppendLine("- Do not auto-repair or regenerate Unity serialized assets without explaining why it is necessary.");
            builder.AppendLine("- No unrelated refactors.");
            return builder.ToString();
        }

        private static string BuildConservativeReworkPrompt(ScanResult result, AIPrefabGuardLanguage language)
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are Codex working on a Unity project. Please redo or adjust the changes made since the AI Prefab Guard local baseline conservatively.");
            builder.AppendLine();
            builder.AppendLine("Current risk summary from AI Prefab Guard, based on changes since the local baseline:");
            builder.AppendLine(NaturalLanguageRiskSummaryBuilder.Build(result, language));
            builder.AppendLine();
            builder.AppendLine("Constraints:");
            builder.AppendLine("- Prefer C# code changes over direct edits to Unity serialized assets.");
            builder.AppendLine("- Do not modify .prefab, .unity, .asset, .meta, or .asmdef files unless strictly necessary.");
            builder.AppendLine("- Do not auto-repair, rewrite, or regenerate Unity serialized assets just because they are listed as risky.");
            builder.AppendLine("- Avoid unrelated refactors and unnecessary Manager/Provider/Factory abstractions.");
            builder.AppendLine("- If asset changes are required, explain why and list exactly which files must change.");
            builder.AppendLine("- Provide Unity Editor manual verification steps.");
            AppendRiskFiles(builder, result);
            return builder.ToString();
        }

        private static string BuildExplainPrompt(ScanResult result, AIPrefabGuardLanguage language)
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are Codex working on a Unity project.");
            builder.AppendLine();
            builder.AppendLine("Current risk summary from AI Prefab Guard, based on changes since the local baseline:");
            builder.AppendLine(NaturalLanguageRiskSummaryBuilder.Build(result, language));
            builder.AppendLine();
            builder.AppendLine("Explain why each high-risk Unity asset change since the local baseline is necessary.");
            builder.AppendLine();
            builder.AppendLine("For every listed file, include:");
            builder.AppendLine("- Why the file had to change.");
            builder.AppendLine("- Whether the same goal could be achieved through C# code instead.");
            builder.AppendLine("- What Unity Editor checks should be performed before merge.");
            AppendRiskFiles(builder, result);
            return builder.ToString();
        }

        private static string BuildManualVerificationPrompt(ScanResult result, AIPrefabGuardLanguage language)
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are Codex working on a Unity project.");
            builder.AppendLine();
            builder.AppendLine("Current risk summary from AI Prefab Guard, based on changes since the local baseline:");
            builder.AppendLine(NaturalLanguageRiskSummaryBuilder.Build(result, language));
            builder.AppendLine();
            builder.AppendLine("Create a manual Unity verification checklist for these high-risk files changed since the local baseline.");
            builder.AppendLine();
            builder.AppendLine("The checklist should be practical, short, and ordered by risk.");
            builder.AppendLine("Include scene/prefab opening steps, Console checks, and Play Mode checks where relevant.");
            AppendRiskFiles(builder, result);
            return builder.ToString();
        }

        private static void AppendRiskFiles(StringBuilder builder, ScanResult result)
        {
            builder.AppendLine();
            builder.AppendLine("High-risk files detected since the local baseline:");

            if (result == null || result.Findings.Count == 0)
            {
                builder.AppendLine("- None detected by AI Prefab Guard.");
                return;
            }

            foreach (var finding in result.Findings)
            {
                builder.AppendLine(string.Format("- [{0}] {1} ({2}, {3})", finding.RiskLevel, finding.File.RelativePath, finding.FileType, finding.File.ChangeKind));
            }
        }
    }
}
