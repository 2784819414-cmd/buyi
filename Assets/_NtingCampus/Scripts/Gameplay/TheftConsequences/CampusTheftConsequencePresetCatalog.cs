using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Sanctions;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.TheftConsequences
{
    internal sealed class CampusTheftConsequencePresetData
    {
        public int DefaultItemValue = 8;
        public int HeldEvidenceBonus = 16;
        public int OfficialWitnessEvidenceBonus = 12;
        public int PriorRecordWeight = 1;
        public int CrackdownWeight = 1;
        public readonly Dictionary<string, int> WitnessWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> RoomSensitivity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> ItemValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<CampusTheftSeverityBandPreset> SeverityBands = new List<CampusTheftSeverityBandPreset>();
        public readonly Dictionary<CampusTheftConsequenceSeverity, CampusTheftConsequenceRulePreset> Rules =
            new Dictionary<CampusTheftConsequenceSeverity, CampusTheftConsequenceRulePreset>();
    }

    internal sealed class CampusTheftSeverityBandPreset
    {
        public CampusTheftConsequenceSeverity Severity;
        public int MinScore;
    }

    internal sealed class CampusTheftConsequenceRulePreset
    {
        public CampusTheftConsequenceSeverity Severity;
        public int SuspicionDelta;
        public int EvidenceDelta;
        public int RecordDelta;
        public int RumorDelta;
        public int CrackdownDelta;
        public int TeacherAlertnessDelta;
        public int CampusOrderDelta;
        public int CampusChaosDelta;
        public int CompensationAmount;
        public bool ConfiscateEvidence;
        public CampusSanctionLevel SanctionLevel;
    }

    internal static class CampusTheftConsequencePresetCatalog
    {
        private const string PresetFileName = "TheftConsequencePresets.json";

        private static CampusTheftConsequencePresetData cachedData;

        public static CampusTheftConsequencePresetData Data =>
            cachedData ?? (cachedData = LoadData());

        public static int ResolveWitnessWeight(CampusCharacterData witnessData, bool ownerWitness)
        {
            CampusTheftConsequencePresetData data = Data;
            if (ownerWitness && TryGet(data.WitnessWeights, "owner", out int ownerWeight))
            {
                return ownerWeight;
            }

            if (witnessData == null)
            {
                return TryGet(data.WitnessWeights, "unknown", out int unknownWeight) ? unknownWeight : 0;
            }

            if (witnessData.Role == CampusCharacterRole.Teacher &&
                TryGet(data.WitnessWeights, "teacher", out int teacherWeight))
            {
                return teacherWeight;
            }

            if (witnessData.Role == CampusCharacterRole.Staff &&
                TryGet(data.WitnessWeights, "staff", out int staffWeight))
            {
                return staffWeight;
            }

            if (witnessData.HasTrait(CampusCharacterTrait.Tattletale) &&
                TryGet(data.WitnessWeights, "tattletale", out int tattletaleWeight))
            {
                return tattletaleWeight;
            }

            if (witnessData.HasTrait(CampusCharacterTrait.GoodStudent) &&
                TryGet(data.WitnessWeights, "good_student", out int goodStudentWeight))
            {
                return goodStudentWeight;
            }

            return TryGet(data.WitnessWeights, "student", out int studentWeight) ? studentWeight : 0;
        }

        public static int ResolveRoomSensitivity(CampusGameplayRoom room, string roomId)
        {
            CampusTheftConsequencePresetData data = Data;
            if (room != null)
            {
                string roomTypeKey = room.RoomType.ToString();
                if (TryGet(data.RoomSensitivity, roomTypeKey, out int roomTypeValue))
                {
                    return roomTypeValue;
                }

                if (!string.IsNullOrWhiteSpace(room.RoomId) &&
                    TryGet(data.RoomSensitivity, room.RoomId, out int roomIdValue))
                {
                    return roomIdValue;
                }
            }

            if (!string.IsNullOrWhiteSpace(roomId) &&
                TryGet(data.RoomSensitivity, roomId, out int directRoomValue))
            {
                return directRoomValue;
            }

            return TryGet(data.RoomSensitivity, "default", out int fallback) ? fallback : 0;
        }

        public static int ResolveItemValue(string definitionId, int eventRisk)
        {
            CampusTheftConsequencePresetData data = Data;
            if (!string.IsNullOrWhiteSpace(definitionId) &&
                TryGet(data.ItemValues, definitionId, out int itemValue))
            {
                return itemValue;
            }

            return Mathf.Max(1, Mathf.Max(data.DefaultItemValue, eventRisk / 2));
        }

        public static CampusTheftConsequenceRulePreset ResolveRule(CampusTheftConsequenceSeverity severity)
        {
            CampusTheftConsequencePresetData data = Data;
            return data.Rules.TryGetValue(severity, out CampusTheftConsequenceRulePreset rule)
                ? rule
                : data.Rules[CampusTheftConsequenceSeverity.Minor];
        }

        public static CampusTheftConsequenceSeverity ResolveSeverity(int score)
        {
            CampusTheftConsequencePresetData data = Data;
            CampusTheftConsequenceSeverity severity = CampusTheftConsequenceSeverity.Minor;
            for (int i = 0; i < data.SeverityBands.Count; i++)
            {
                CampusTheftSeverityBandPreset band = data.SeverityBands[i];
                if (band != null && score >= band.MinScore)
                {
                    severity = band.Severity;
                }
            }

            return severity;
        }

        private static CampusTheftConsequencePresetData LoadData()
        {
            CampusTheftConsequencePresetData data = BuildBuiltInData();
            if (!CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                Debug.LogWarning("[CampusTheftConsequencePresetCatalog] Missing preset file: " + PresetFileName + ". Built-in theft consequence defaults are active.");
                return data;
            }

            try
            {
                CampusTheftConsequencePresetFile file = JsonUtility.FromJson<CampusTheftConsequencePresetFile>(json);
                ParseFile(data, file);
                Validate(data);
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusTheftConsequencePresetCatalog] Failed to parse " + PresetFileName + ": " + exception.Message);
                return data;
            }
        }

        private static void ParseFile(CampusTheftConsequencePresetData data, CampusTheftConsequencePresetFile file)
        {
            if (file == null)
            {
                return;
            }

            data.DefaultItemValue = Mathf.Max(1, file.DefaultItemValue);
            data.HeldEvidenceBonus = Mathf.Max(0, file.HeldEvidenceBonus);
            data.OfficialWitnessEvidenceBonus = Mathf.Max(0, file.OfficialWitnessEvidenceBonus);
            data.PriorRecordWeight = Mathf.Max(0, file.PriorRecordWeight);
            data.CrackdownWeight = Mathf.Max(0, file.CrackdownWeight);
            ParseIntRows(data.WitnessWeights, file.WitnessWeights);
            ParseIntRows(data.RoomSensitivity, file.RoomSensitivity);
            ParseIntRows(data.ItemValues, file.ItemValues);
            ParseBands(data, file.SeverityBands);
            ParseRules(data, file.ConsequenceRules);
        }

        private static void ParseIntRows(Dictionary<string, int> target, List<CampusTheftIntPresetRow> rows)
        {
            if (target == null || rows == null)
            {
                return;
            }

            target.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                CampusTheftIntPresetRow row = rows[i];
                string id = NormalizeId(row != null ? row.Id : string.Empty);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                target[id] = Mathf.Max(0, row.Value);
            }
        }

        private static void ParseBands(CampusTheftConsequencePresetData data, List<CampusTheftSeverityBandFileRow> rows)
        {
            if (rows == null)
            {
                return;
            }

            data.SeverityBands.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                CampusTheftSeverityBandFileRow row = rows[i];
                if (row == null || !TryParseSeverity(row.Severity, out CampusTheftConsequenceSeverity severity))
                {
                    continue;
                }

                data.SeverityBands.Add(new CampusTheftSeverityBandPreset
                {
                    Severity = severity,
                    MinScore = Mathf.Max(0, row.MinScore)
                });
            }
        }

        private static void ParseRules(CampusTheftConsequencePresetData data, List<CampusTheftConsequenceRuleFileRow> rows)
        {
            if (rows == null)
            {
                return;
            }

            data.Rules.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                CampusTheftConsequenceRuleFileRow row = rows[i];
                if (row == null || !TryParseSeverity(row.Severity, out CampusTheftConsequenceSeverity severity))
                {
                    continue;
                }

                data.Rules[severity] = new CampusTheftConsequenceRulePreset
                {
                    Severity = severity,
                    SuspicionDelta = Mathf.Max(0, row.SuspicionDelta),
                    EvidenceDelta = Mathf.Max(0, row.EvidenceDelta),
                    RecordDelta = Mathf.Max(0, row.RecordDelta),
                    RumorDelta = Mathf.Max(0, row.RumorDelta),
                    CrackdownDelta = Mathf.Max(0, row.CrackdownDelta),
                    TeacherAlertnessDelta = Mathf.Max(0, row.TeacherAlertnessDelta),
                    CampusOrderDelta = row.CampusOrderDelta,
                    CampusChaosDelta = Mathf.Max(0, row.CampusChaosDelta),
                    CompensationAmount = Mathf.Max(0, row.CompensationAmount),
                    ConfiscateEvidence = row.ConfiscateEvidence,
                    SanctionLevel = ParseSanctionLevel(row.SanctionLevel)
                };
            }
        }

        private static void Validate(CampusTheftConsequencePresetData data)
        {
            if (data.SeverityBands.Count == 0)
            {
                AddBuiltInBands(data);
            }

            data.SeverityBands.Sort((left, right) => left.MinScore.CompareTo(right.MinScore));
            EnsureRule(data, CampusTheftConsequenceSeverity.Minor);
            EnsureRule(data, CampusTheftConsequenceSeverity.Moderate);
            EnsureRule(data, CampusTheftConsequenceSeverity.Severe);
        }

        private static CampusTheftConsequencePresetData BuildBuiltInData()
        {
            CampusTheftConsequencePresetData data = new CampusTheftConsequencePresetData();
            data.WitnessWeights["unknown"] = 0;
            data.WitnessWeights["student"] = 4;
            data.WitnessWeights["good_student"] = 7;
            data.WitnessWeights["tattletale"] = 9;
            data.WitnessWeights["staff"] = 14;
            data.WitnessWeights["teacher"] = 18;
            data.WitnessWeights["owner"] = 12;

            data.RoomSensitivity["default"] = 2;
            data.RoomSensitivity[CampusRoomType.RetailArea.ToString()] = 10;
            data.RoomSensitivity[CampusRoomType.ServiceArea.ToString()] = 9;
            data.RoomSensitivity[CampusRoomType.Library.ToString()] = 12;
            data.RoomSensitivity[CampusRoomType.Outdoor.ToString()] = 7;
            data.RoomSensitivity[CampusRoomType.Office.ToString()] = 14;

            AddBuiltInBands(data);
            data.Rules[CampusTheftConsequenceSeverity.Minor] = new CampusTheftConsequenceRulePreset
            {
                Severity = CampusTheftConsequenceSeverity.Minor,
                SuspicionDelta = 5,
                EvidenceDelta = 2,
                RecordDelta = 0,
                RumorDelta = 1,
                CrackdownDelta = 0,
                TeacherAlertnessDelta = 1,
                CampusOrderDelta = -1,
                CampusChaosDelta = 1,
                CompensationAmount = 2,
                ConfiscateEvidence = true,
                SanctionLevel = CampusSanctionLevel.None
            };
            data.Rules[CampusTheftConsequenceSeverity.Moderate] = new CampusTheftConsequenceRulePreset
            {
                Severity = CampusTheftConsequenceSeverity.Moderate,
                SuspicionDelta = 12,
                EvidenceDelta = 6,
                RecordDelta = 1,
                RumorDelta = 4,
                CrackdownDelta = 2,
                TeacherAlertnessDelta = 3,
                CampusOrderDelta = -3,
                CampusChaosDelta = 3,
                CompensationAmount = 6,
                ConfiscateEvidence = true,
                SanctionLevel = CampusSanctionLevel.Warning
            };
            data.Rules[CampusTheftConsequenceSeverity.Severe] = new CampusTheftConsequenceRulePreset
            {
                Severity = CampusTheftConsequenceSeverity.Severe,
                SuspicionDelta = 22,
                EvidenceDelta = 12,
                RecordDelta = 2,
                RumorDelta = 8,
                CrackdownDelta = 5,
                TeacherAlertnessDelta = 6,
                CampusOrderDelta = -6,
                CampusChaosDelta = 6,
                CompensationAmount = 12,
                ConfiscateEvidence = true,
                SanctionLevel = CampusSanctionLevel.Reprimand
            };
            return data;
        }

        private static void AddBuiltInBands(CampusTheftConsequencePresetData data)
        {
            data.SeverityBands.Clear();
            data.SeverityBands.Add(new CampusTheftSeverityBandPreset { Severity = CampusTheftConsequenceSeverity.Minor, MinScore = 0 });
            data.SeverityBands.Add(new CampusTheftSeverityBandPreset { Severity = CampusTheftConsequenceSeverity.Moderate, MinScore = 35 });
            data.SeverityBands.Add(new CampusTheftSeverityBandPreset { Severity = CampusTheftConsequenceSeverity.Severe, MinScore = 70 });
        }

        private static void EnsureRule(CampusTheftConsequencePresetData data, CampusTheftConsequenceSeverity severity)
        {
            if (!data.Rules.ContainsKey(severity))
            {
                data.Rules[severity] = BuildBuiltInData().Rules[severity];
            }
        }

        private static bool TryGet(Dictionary<string, int> values, string id, out int value)
        {
            value = 0;
            return values != null &&
                   !string.IsNullOrWhiteSpace(id) &&
                   values.TryGetValue(id.Trim(), out value);
        }

        private static bool TryParseSeverity(string value, out CampusTheftConsequenceSeverity severity)
        {
            return Enum.TryParse(NormalizeId(value), true, out severity) &&
                   severity != CampusTheftConsequenceSeverity.None;
        }

        private static CampusSanctionLevel ParseSanctionLevel(string value)
        {
            return Enum.TryParse(NormalizeId(value), true, out CampusSanctionLevel level)
                ? level
                : CampusSanctionLevel.None;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        [Serializable]
        private sealed class CampusTheftConsequencePresetFile
        {
            public int DefaultItemValue = 8;
            public int HeldEvidenceBonus = 16;
            public int OfficialWitnessEvidenceBonus = 12;
            public int PriorRecordWeight = 1;
            public int CrackdownWeight = 1;
            public List<CampusTheftIntPresetRow> WitnessWeights = new List<CampusTheftIntPresetRow>();
            public List<CampusTheftIntPresetRow> RoomSensitivity = new List<CampusTheftIntPresetRow>();
            public List<CampusTheftIntPresetRow> ItemValues = new List<CampusTheftIntPresetRow>();
            public List<CampusTheftSeverityBandFileRow> SeverityBands = new List<CampusTheftSeverityBandFileRow>();
            public List<CampusTheftConsequenceRuleFileRow> ConsequenceRules = new List<CampusTheftConsequenceRuleFileRow>();
        }

        [Serializable]
        private sealed class CampusTheftIntPresetRow
        {
            public string Id = string.Empty;
            public int Value = 0;
        }

        [Serializable]
        private sealed class CampusTheftSeverityBandFileRow
        {
            public string Severity = string.Empty;
            public int MinScore = 0;
        }

        [Serializable]
        private sealed class CampusTheftConsequenceRuleFileRow
        {
            public string Severity = string.Empty;
            public int SuspicionDelta = 0;
            public int EvidenceDelta = 0;
            public int RecordDelta = 0;
            public int RumorDelta = 0;
            public int CrackdownDelta = 0;
            public int TeacherAlertnessDelta = 0;
            public int CampusOrderDelta = 0;
            public int CampusChaosDelta = 0;
            public int CompensationAmount = 0;
            public bool ConfiscateEvidence = false;
            public string SanctionLevel = string.Empty;
        }
    }
}
