using System;
using NtingCampus.Gameplay.Sanctions;

namespace NtingCampus.Gameplay.TheftConsequences
{
    public enum CampusTheftIncidentKind
    {
        ObservedProtectedItemMove = 0,
        ContrabandFound = 1
    }

    public enum CampusTheftConsequenceSeverity
    {
        None = 0,
        Minor = 1,
        Moderate = 2,
        Severe = 3
    }

    [Serializable]
    public sealed class CampusTheftIncidentRecord
    {
        public CampusTheftIncidentKind Kind;
        public string ActorId = string.Empty;
        public string WitnessId = string.Empty;
        public string OwnerId = string.Empty;
        public string ItemInstanceId = string.Empty;
        public string ItemDefinitionId = string.Empty;
        public string ItemDisplayName = string.Empty;
        public string SourceContainerId = string.Empty;
        public string TargetContainerId = string.Empty;
        public string RoomId = string.Empty;
        public int Day;
        public int BaseItemValue;
        public int EvidenceValue;
        public int WitnessWeight;
        public int RoomSensitivity;
        public bool OfficialWitness;
        public bool FoundOnActor;
    }

    public readonly struct CampusTheftConsequenceResult
    {
        public CampusTheftConsequenceResult(
            CampusTheftConsequenceSeverity severity,
            int severityScore,
            int suspicionDelta,
            int evidenceDelta,
            int recordDelta,
            int rumorDelta,
            int crackdownDelta,
            int teacherAlertnessDelta,
            int campusOrderDelta,
            int campusChaosDelta,
            int compensationAmount,
            bool confiscateEvidence,
            CampusSanctionLevel sanctionLevel)
        {
            Severity = severity;
            SeverityScore = Math.Max(0, severityScore);
            SuspicionDelta = Math.Max(0, suspicionDelta);
            EvidenceDelta = Math.Max(0, evidenceDelta);
            RecordDelta = Math.Max(0, recordDelta);
            RumorDelta = Math.Max(0, rumorDelta);
            CrackdownDelta = Math.Max(0, crackdownDelta);
            TeacherAlertnessDelta = Math.Max(0, teacherAlertnessDelta);
            CampusOrderDelta = campusOrderDelta;
            CampusChaosDelta = Math.Max(0, campusChaosDelta);
            CompensationAmount = Math.Max(0, compensationAmount);
            ConfiscateEvidence = confiscateEvidence;
            SanctionLevel = sanctionLevel;
        }

        public CampusTheftConsequenceSeverity Severity { get; }
        public int SeverityScore { get; }
        public int SuspicionDelta { get; }
        public int EvidenceDelta { get; }
        public int RecordDelta { get; }
        public int RumorDelta { get; }
        public int CrackdownDelta { get; }
        public int TeacherAlertnessDelta { get; }
        public int CampusOrderDelta { get; }
        public int CampusChaosDelta { get; }
        public int CompensationAmount { get; }
        public bool ConfiscateEvidence { get; }
        public CampusSanctionLevel SanctionLevel { get; }
    }
}
