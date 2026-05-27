using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nting.AIPrefabGuard.Editor
{
    public sealed class AIPrefabGuardWindow : EditorWindow
    {
        private readonly BaselineChangeScanner scanner = new BaselineChangeScanner();
        private Vector2 scrollPosition;
        private ScanResult scanResult;
        private PromptTemplateKind promptTemplateKind;
        private string lastExportPath = string.Empty;
        private bool isScanning;
        private AIPrefabGuardLanguage language = AIPrefabGuardLanguage.English;
        private int selectedFindingIndex;

        [MenuItem("Tools/Nting/AI Prefab Guard")]
        public static void OpenWindow()
        {
            var window = GetWindow<AIPrefabGuardWindow>("AI Prefab Guard");
            window.minSize = new Vector2(760f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            language = (AIPrefabGuardLanguage)EditorPrefs.GetInt("Nting.AIPrefabGuard.Language", (int)AIPrefabGuardLanguage.English);
            scanResult = scanner.GetInitialResult(GetProjectPath());
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintWhileScanning;
        }

        private void OnGUI()
        {
            DrawHeader();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawTrustBoundarySection();
            DrawScanSection();
            DrawNaturalLanguageSummarySection();
            DrawSummarySection();
            DrawRiskReviewSection();
            DrawOutputSection();
            DrawHintSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("AI Prefab Guard", EditorStyles.boldLabel);
                var nextLanguage = (AIPrefabGuardLanguage)EditorGUILayout.EnumPopup(language, GUILayout.Width(110f));
                if (nextLanguage != language)
                {
                    language = nextLanguage;
                    EditorPrefs.SetInt("Nting.AIPrefabGuard.Language", (int)language);
                }
            }

            EditorGUILayout.LabelField(
                T("Local baseline review for Unity asset changes after AI edits.", "本地基线风险检查：发现并解释 AI 修改后的 Unity 资源变动。"),
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);
        }

        private void DrawTrustBoundarySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(T("Trust Boundary", "信任边界"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    T(
                        "Local scan only. Baseline metadata is stored under Library/AIPrefabGuard. No upload, no AI API calls, and no automatic asset repair or rewrite.",
                        "只进行本地扫描。基线元数据保存在 Library/AIPrefabGuard。不会上传项目，不调用 AI API，也不会自动修复或重写资源。"),
                    EditorStyles.wordWrappedLabel);
            }
        }

        private void DrawScanSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(T("Baseline Scan", "基线扫描"), EditorStyles.boldLabel);
                DrawReadonlyLine(T("Baseline", "基线状态"), GetBaselineStateText());
                DrawReadonlyLine(T("Baseline Path", "基线路径"), EmptyFallback(scanResult.Environment.BaselinePath, "N/A"));
                DrawReadonlyLine(T("Baseline Updated", "基线更新时间"), FormatUtc(scanResult.Environment.BaselineUpdatedAtUtc));
                DrawReadonlyLine(T("Baseline File Count", "基线文件数"), scanResult.Environment.BaselineFileCount.ToString());
                DrawReadonlyLine(T("Last Scan", "最近扫描"), scanResult.ScannedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                DrawReadonlyLine(T("Status", "状态"), isScanning ? T("Scanning...", "正在扫描...") : scanResult.Environment.Message);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(isScanning))
                    {
                        if (GUILayout.Button(T("Scan Changes Since Baseline", "扫描基线后的改动"), GUILayout.Height(28f)))
                        {
                            StartScan();
                        }
                    }

                    using (new EditorGUI.DisabledScope(isScanning))
                    {
                        if (GUILayout.Button(T("Accept Current State As Baseline", "确认当前状态为新基线"), GUILayout.Height(28f)))
                        {
                            AcceptBaseline();
                        }
                    }

                    using (new EditorGUI.DisabledScope(isScanning))
                    {
                        if (GUILayout.Button(T("Reset Baseline", "重置基线"), GUILayout.Width(110f), GUILayout.Height(28f)))
                        {
                            ResetBaseline();
                        }
                    }

                    if (GUILayout.Button(T("Refresh Assets", "刷新资源"), GUILayout.Width(100f), GUILayout.Height(28f)))
                    {
                        AssetDatabase.Refresh();
                    }
                }
            }
        }

        private void DrawNaturalLanguageSummarySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(AIPrefabGuardText.RiskSummaryTitle(language), EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(NaturalLanguageRiskSummaryBuilder.Build(scanResult, language), GetSummaryMessageType());
            }
        }

        private MessageType GetSummaryMessageType()
        {
            if (scanResult.Environment.Message.StartsWith("Scan failed:", StringComparison.OrdinalIgnoreCase))
            {
                return MessageType.Error;
            }

            return scanResult.Findings.Count > 0 ? MessageType.Warning : MessageType.Info;
        }

        private void DrawSummarySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(T("Summary", "摘要"), EditorStyles.boldLabel);
                DrawReadonlyLine(T("Risk Compared With Baseline", "相对基线风险等级"), scanResult.OverallRisk.ToString());
                DrawReadonlyLine(T("High-risk Unity files found", "高风险 Unity 文件数"), scanResult.Findings.Count.ToString());
                DrawReadonlyLine(T("Very High count", "Very High 数量"), scanResult.VeryHighCount.ToString());
                DrawReadonlyLine(T("High count", "High 数量"), scanResult.HighCount.ToString());
                DrawReadonlyLine(T("Files changed since baseline", "相对基线改动文件数"), scanResult.ChangedFiles.Count.ToString());
            }
        }

        private void DrawRiskReviewSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(T("Risk Review", "风险复核"), EditorStyles.boldLabel);

                if (scanResult.Findings.Count == 0)
                {
                    EditorGUILayout.HelpBox(AIPrefabGuardText.NoHighRiskFiles(language), MessageType.Info);
                    return;
                }

                if (selectedFindingIndex >= scanResult.Findings.Count)
                {
                    selectedFindingIndex = 0;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Min(360f, position.width * 0.45f))))
                    {
                        for (var index = 0; index < scanResult.Findings.Count; index++)
                        {
                            DrawFindingRow(index, scanResult.Findings[index]);
                        }
                    }

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        DrawFindingDetails(scanResult.Findings[selectedFindingIndex]);
                    }
                }
            }
        }

        private void DrawFindingRow(int index, RiskFinding finding)
        {
            var label = string.Format("[{0}] {1}", finding.RiskLevel, finding.File.RelativePath);
            var wasSelected = selectedFindingIndex == index;
            var isSelected = GUILayout.Toggle(wasSelected, label, "Button", GUILayout.Height(28f));
            if (isSelected && !wasSelected)
            {
                selectedFindingIndex = index;
            }
        }

        private void DrawFindingDetails(RiskFinding finding)
        {
            EditorGUILayout.LabelField(finding.File.RelativePath, EditorStyles.boldLabel);
            DrawReadonlyLine("Risk", finding.RiskLevel.ToString());
            DrawReadonlyLine("Type", finding.FileType.ToString());
            DrawReadonlyLine("Change", finding.File.ChangeKind.ToString());
            DrawReadonlyLine(T("Baseline Status", "基线差异状态"), finding.File.StatusCode);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(T("Reason", "原因"), finding.Reason, EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(T("Manual Verification Checklist", "人工复核清单"), EditorStyles.boldLabel);
            foreach (var item in finding.Checklist)
            {
                EditorGUILayout.LabelField("- " + item, EditorStyles.wordWrappedLabel);
            }

            if (finding.Insights.Count > 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(T("Semantic Notes", "语义提示"), EditorStyles.boldLabel);
                foreach (var insight in finding.Insights)
                {
                    EditorGUILayout.LabelField("- " + insight, EditorStyles.wordWrappedLabel);
                }
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!finding.CanPing))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(70f)))
                    {
                        AssetLocator.Ping(scanResult.Environment, finding);
                    }
                }

                using (new EditorGUI.DisabledScope(!finding.CanOpen))
                {
                    if (GUILayout.Button("Open", GUILayout.Width(70f)))
                    {
                        AssetLocator.Open(scanResult.Environment, finding);
                    }
                }

                if (GUILayout.Button(T("Copy Codex Prompt For This File", "复制此文件的 Codex 提示词")))
                {
                    EditorGUIUtility.systemCopyBuffer = ReworkPromptBuilder.BuildForFinding(finding, scanResult, promptTemplateKind, language);
                }
            }

            if (!finding.CanOpen)
            {
                EditorGUILayout.HelpBox(AIPrefabGuardText.DeletedFileCannotOpen(language), MessageType.Info);
            }
        }

        private void DrawOutputSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(T("Output", "输出"), EditorStyles.boldLabel);
                promptTemplateKind = (PromptTemplateKind)EditorGUILayout.EnumPopup(T("Prompt Template", "提示词模板"), promptTemplateKind);

                using (new EditorGUILayout.VerticalScope())
                {
                    if (GUILayout.Button(AIPrefabGuardText.CopyMarkdownReport(language), GUILayout.Height(26f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = MarkdownReportBuilder.Build(scanResult, promptTemplateKind, language);
                    }

                    if (GUILayout.Button(AIPrefabGuardText.ExportMarkdownReport(language), GUILayout.Height(26f)))
                    {
                        ExportReport();
                    }

                    if (GUILayout.Button(AIPrefabGuardText.CopyCodexPrompt(language), GUILayout.Height(26f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = ReworkPromptBuilder.Build(scanResult, promptTemplateKind, language);
                    }
                }

                if (!string.IsNullOrEmpty(lastExportPath))
                {
                    EditorGUILayout.LabelField(T("Last Export", "最近导出"), lastExportPath, EditorStyles.wordWrappedLabel);
                }
            }
        }

        private void DrawHintSection()
        {
            if (scanResult.Environment.Message.StartsWith("Scan failed:", StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox(
                    T(
                        "Scan failed before results could be generated. Review the status message above, refresh assets, then scan again.",
                        "扫描失败，未能生成结果。请查看上方状态信息，刷新资源后再扫描。"),
                    MessageType.Error);
                return;
            }

            if (scanResult.ChangedFiles.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    T(
                        "Current project Assets match the local baseline. After AI edits, click Scan Changes Since Baseline to review new asset risks.",
                        "当前 Assets 状态与本地基线一致。AI 修改后，点击“扫描基线后的改动”即可复查新的资源风险。"),
                    MessageType.Info);
                return;
            }

            if (scanResult.Findings.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    T(
                        "Changes were found since the baseline, but no high-risk Unity asset files were detected. Review code/runtime behavior, then accept the state as the new baseline if it is correct.",
                        "检测到相对基线的改动，但没有高风险 Unity 资源文件。请继续检查代码和运行表现；确认无误后可把当前状态设为新基线。"),
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                T(
                    "High-risk Unity asset changes were found since the baseline. Review them manually in Unity before accepting the current state as the new baseline.",
                    "发现相对基线的高风险 Unity 资源改动。请先在 Unity 中人工复核，再把当前状态确认成新基线。"),
                MessageType.Warning);
        }

        private void StartScan()
        {
            var projectPath = GetProjectPath();
            isScanning = true;
            lastExportPath = string.Empty;
            try
            {
                scanResult = scanner.Scan(projectPath);
                selectedFindingIndex = 0;
            }
            catch (Exception exception)
            {
                scanResult = new ScanResult(
                    GitScanEnvironment.Baseline(
                        GetProjectPath(),
                        string.Empty,
                        false,
                        string.Empty,
                        string.Empty,
                        0,
                        "Scan failed: " + exception.Message),
                    Array.Empty<GitChangedFile>(),
                    Array.Empty<RiskFinding>(),
                    DateTime.Now);
            }
            finally
            {
                isScanning = false;
                Repaint();
            }
        }

        private void AcceptBaseline()
        {
            var shouldContinue = scanResult.ChangedFiles.Count == 0 || EditorUtility.DisplayDialog(
                T("Accept New Baseline", "确认新基线"),
                T(
                    "This will mark the current Assets state as reviewed and trusted. Future scans will compare against this new baseline.",
                    "这会把当前 Assets 状态标记为已复核可信。之后扫描会以这个新基线作为对比。"),
                T("Accept", "确认"),
                T("Cancel", "取消"));

            if (!shouldContinue)
            {
                return;
            }

            scanResult = scanner.AcceptCurrentStateAsBaseline(GetProjectPath());
            selectedFindingIndex = 0;
            lastExportPath = string.Empty;
            Repaint();
        }

        private void ResetBaseline()
        {
            var shouldContinue = EditorUtility.DisplayDialog(
                T("Reset Baseline", "重置基线"),
                T(
                    "This will discard the saved baseline and immediately create a new one from the current Assets state.",
                    "这会丢弃已保存的基线，并立刻用当前 Assets 状态创建新基线。"),
                T("Reset", "重置"),
                T("Cancel", "取消"));

            if (!shouldContinue)
            {
                return;
            }

            scanResult = scanner.ResetBaseline(GetProjectPath());
            selectedFindingIndex = 0;
            lastExportPath = string.Empty;
            Repaint();
        }

        private void RepaintWhileScanning()
        {
        }

        private void ExportReport()
        {
            var defaultDirectory = GetDefaultExportDirectory();
            var defaultName = "AI-Prefab-Guard-RiskReport.md";
            var path = EditorUtility.SaveFilePanel("Export Markdown Report", defaultDirectory, defaultName, "md");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, MarkdownReportBuilder.Build(scanResult, promptTemplateKind, language));
            lastExportPath = path;
        }

        private string GetBaselineStateText()
        {
            return scanResult.Environment.HasBaseline
                ? T("Baseline ready", "基线已就绪")
                : T("No baseline", "暂无基线");
        }

        private string T(string english, string chinese)
        {
            return AIPrefabGuardText.Get(language, english, chinese);
        }

        private static void DrawReadonlyLine(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(170f));
                EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static string GetDefaultExportDirectory()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrEmpty(documents) ? GetProjectPath() : documents;
        }

        private static string GetProjectPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string EmptyFallback(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string FormatUtc(string utcText)
        {
            if (string.IsNullOrEmpty(utcText))
            {
                return "N/A";
            }

            DateTime parsed;
            return DateTime.TryParse(utcText, out parsed)
                ? parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : utcText;
        }
    }
}
