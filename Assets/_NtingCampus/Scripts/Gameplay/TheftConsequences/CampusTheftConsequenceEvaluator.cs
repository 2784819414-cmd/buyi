using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Sanctions;
using UnityEngine;

namespace NtingCampus.Gameplay.TheftConsequences
{
    internal static class CampusTheftConsequenceEvaluator
    {
        public static CampusTheftConsequenceResult Evaluate(
            CampusTheftIncidentRecord incident,
            CampusGameState gameState)
        {
            if (incident == null)
            {
                return default;
            }

            CampusTheftConsequencePresetData preset = CampusTheftConsequencePresetCatalog.Data;
            int priorRecord = gameState != null ? gameState.PlayerTheftRecord : 0;
            int crackdown = gameState != null ? gameState.CampusCrackdown : 0;
            int evidence = incident.EvidenceValue +
                           (incident.FoundOnActor ? preset.HeldEvidenceBonus : 0) +
                           (incident.OfficialWitness ? preset.OfficialWitnessEvidenceBonus : 0);
            int score = incident.BaseItemValue +
                        evidence +
                        incident.WitnessWeight +
                        incident.RoomSensitivity +
                        priorRecord * preset.PriorRecordWeight +
                        crackdown * preset.CrackdownWeight;

            CampusTheftConsequenceSeverity severity = CampusTheftConsequencePresetCatalog.ResolveSeverity(score);
            CampusTheftConsequenceRulePreset rule = CampusTheftConsequencePresetCatalog.ResolveRule(severity);
            CampusSanctionLevel sanctionLevel = incident.OfficialWitness || incident.FoundOnActor
                ? rule.SanctionLevel
                : CampusSanctionLevel.None;

            return new CampusTheftConsequenceResult(
                severity,
                Mathf.Max(0, score),
                rule.SuspicionDelta,
                rule.EvidenceDelta + Mathf.Max(0, evidence / 12),
                rule.RecordDelta,
                rule.RumorDelta,
                rule.CrackdownDelta,
                rule.TeacherAlertnessDelta,
                rule.CampusOrderDelta,
                rule.CampusChaosDelta,
                rule.CompensationAmount,
                rule.ConfiscateEvidence,
                sanctionLevel);
        }
    }
}
