using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampus.Gameplay.TheftConsequences
{
    internal static class CampusTheftConsequenceTextCatalog
    {
        public static string FormatIncidentRecorded(
            CampusDisplayLanguage language,
            CampusTheftIncidentRecord incident,
            CampusTheftConsequenceResult result)
        {
            string itemName = incident != null && !string.IsNullOrWhiteSpace(incident.ItemDisplayName)
                ? incident.ItemDisplayName
                : Resolve(language, "未知物品", "unknown item");
            return Resolve(
                language,
                "[盗窃] 事件已记录：" + itemName + "，严重度=" + FormatSeverity(language, result.Severity) + "，分值=" + result.SeverityScore + "。",
                "[Theft] Incident recorded: " + itemName + ", severity=" + FormatSeverity(language, result.Severity) + ", score=" + result.SeverityScore + ".");
        }

        public static string FormatConsequenceApplied(CampusDisplayLanguage language, CampusTheftConsequenceResult result)
        {
            return Resolve(
                language,
                "[盗窃] 嫌疑+" + result.SuspicionDelta + "，证据+" + result.EvidenceDelta + "，前科+" + result.RecordDelta + "，风声+" + result.RumorDelta + "，严打+" + result.CrackdownDelta + "。",
                "[Theft] Suspicion +" + result.SuspicionDelta + ", evidence +" + result.EvidenceDelta + ", record +" + result.RecordDelta + ", rumor +" + result.RumorDelta + ", crackdown +" + result.CrackdownDelta + ".");
        }

        public static string FormatConfiscated(CampusDisplayLanguage language, string itemName)
        {
            string resolvedName = string.IsNullOrWhiteSpace(itemName)
                ? Resolve(language, "赃物", "evidence")
                : itemName;
            return Resolve(
                language,
                "[盗窃] " + resolvedName + " 被收作证物。",
                "[Theft] " + resolvedName + " was confiscated as evidence.");
        }

        public static string FormatCompensation(CampusDisplayLanguage language, int amount, bool paid)
        {
            return paid
                ? Resolve(
                    language,
                    "[盗窃] 赔偿已扣除：" + amount + "。",
                    "[Theft] Compensation paid: " + amount + ".")
                : Resolve(
                    language,
                    "[盗窃] 赔偿要求未能扣除：" + amount + "。",
                    "[Theft] Compensation could not be paid: " + amount + ".");
        }

        private static string FormatSeverity(CampusDisplayLanguage language, CampusTheftConsequenceSeverity severity)
        {
            switch (severity)
            {
                case CampusTheftConsequenceSeverity.Severe:
                    return Resolve(language, "重度", "severe");
                case CampusTheftConsequenceSeverity.Moderate:
                    return Resolve(language, "中度", "moderate");
                case CampusTheftConsequenceSeverity.Minor:
                    return Resolve(language, "轻度", "minor");
                default:
                    return Resolve(language, "无", "none");
            }
        }

        private static string Resolve(CampusDisplayLanguage language, string chinese, string english)
        {
            return CampusDisplayLanguageCatalog.Resolve(language, chinese, english);
        }
    }
}
