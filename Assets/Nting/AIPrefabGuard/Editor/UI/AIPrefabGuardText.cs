namespace Nting.AIPrefabGuard.Editor
{
    public static class AIPrefabGuardText
    {
        public static string Get(AIPrefabGuardLanguage language, string english, string chinese)
        {
            return language == AIPrefabGuardLanguage.Chinese ? chinese : english;
        }

        public static string RiskSummaryTitle(AIPrefabGuardLanguage language)
        {
            return Get(language, "Current Risk", "当前风险说明");
        }

        public static string CopyMarkdownReport(AIPrefabGuardLanguage language)
        {
            return Get(language, "Copy Markdown Risk Report", "复制 Markdown 风险报告");
        }

        public static string ExportMarkdownReport(AIPrefabGuardLanguage language)
        {
            return Get(language, "Export Markdown Report", "导出 Markdown 报告");
        }

        public static string CopyCodexPrompt(AIPrefabGuardLanguage language)
        {
            return Get(language, "Copy Codex AI Rework Prompt", "复制给 Codex AI 的重改提示词");
        }

        public static string DeletedFileCannotOpen(AIPrefabGuardLanguage language)
        {
            return Get(
                language,
                "This file is deleted or missing on disk, so Open is disabled. Review the previous baseline, Unity references, and project history manually.",
                "该文件已删除或磁盘上不存在，因此不能 Open。请手动检查上一份基线、Unity 引用和项目历史。");
        }

        public static string NoHighRiskFiles(AIPrefabGuardLanguage language)
        {
            return Get(
                language,
                "No high-risk Unity asset files are currently listed. If changes exist since the baseline, continue reviewing code and runtime behavior before accepting the new baseline.",
                "当前没有列出高风险 Unity 资源文件。如果相对基线仍有改动，确认新基线前仍建议检查代码和运行表现。");
        }
    }
}
