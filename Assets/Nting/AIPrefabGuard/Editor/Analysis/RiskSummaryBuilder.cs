using System.Collections.Generic;
using System.Linq;

namespace Nting.AIPrefabGuard.Editor
{
    public static class RiskSummaryBuilder
    {
        public static RiskLevel GetOverallRisk(IReadOnlyList<RiskFinding> findings)
        {
            if (findings == null || findings.Count == 0)
            {
                return RiskLevel.Low;
            }

            if (findings.Any(f => f.FileType == RiskFileType.Prefab || f.FileType == RiskFileType.Scene || f.FileType == RiskFileType.Meta))
            {
                return RiskLevel.High;
            }

            return RiskLevel.Medium;
        }
    }
}
