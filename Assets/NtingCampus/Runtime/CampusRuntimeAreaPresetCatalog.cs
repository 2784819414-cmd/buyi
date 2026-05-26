using System;
using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal sealed class CampusRuntimeAreaPreset
    {
        public readonly string RoomName;
        public readonly CampusLocalizedTextEntry Label;
        public readonly int RequiredCount;

        public CampusRuntimeAreaPreset(
            string roomName,
            CampusLocalizedTextEntry label,
            int requiredCount)
        {
            RoomName = roomName;
            Label = label.HasAnyText
                ? label
                : new CampusLocalizedTextEntry(roomName, roomName);
            RequiredCount = requiredCount < 0 ? 0 : requiredCount;
        }
    }

    internal static class CampusRuntimeAreaPresetCatalog
    {
        private static CampusRuntimeAreaPreset[] cachedPresets;

        internal static CampusRuntimeAreaPreset[] Presets
        {
            get
            {
                if (cachedPresets == null)
                {
                    cachedPresets = LoadPresets();
                }

                return cachedPresets;
            }
        }

        private static readonly CampusRuntimeAreaPreset[] BuiltInPresets =
        {
            new CampusRuntimeAreaPreset("Classroom", new CampusLocalizedTextEntry("\u6559\u5ba4", "Classroom", "\u6559\u5ba4", "\u041a\u043b\u0430\u0441\u0441", "\u6559\u5ba4"), 2),
            new CampusRuntimeAreaPreset("Corridor", new CampusLocalizedTextEntry("\u8d70\u5eca", "Corridor", "\u8d70\u5eca", "\u041a\u043e\u0440\u0438\u0434\u043e\u0440", "\u5eca\u4e0b"), 1),
            new CampusRuntimeAreaPreset("Office", new CampusLocalizedTextEntry("\u529e\u516c\u5ba4", "Office", "\u8fa6\u516c\u5ba4", "\u041a\u0430\u0431\u0438\u043d\u0435\u0442", "\u8077\u54e1\u5ba4"), 1),
            new CampusRuntimeAreaPreset("CommonActivityZone", new CampusLocalizedTextEntry("\u516c\u5171\u6d3b\u52a8\u533a", "Common Activity Zone", "\u516c\u5171\u6d3b\u52d5\u5340", "\u041e\u0431\u0449\u0430\u044f \u0437\u043e\u043d\u0430", "\u5171\u7528\u6d3b\u52d5\u30a8\u30ea\u30a2"), 1),
            new CampusRuntimeAreaPreset("Outdoor", new CampusLocalizedTextEntry("\u5ba4\u5916", "Outdoor", "\u5ba4\u5916", "\u0423\u043b\u0438\u0446\u0430", "\u5c4b\u5916"), 1),
            new CampusRuntimeAreaPreset("Dormitory", new CampusLocalizedTextEntry("\u5bbf\u820d", "Dormitory", "\u5bbf\u820d", "\u041e\u0431\u0449\u0435\u0436\u0438\u0442\u0438\u0435", "\u5bee"), 0),
            new CampusRuntimeAreaPreset("Restroom", new CampusLocalizedTextEntry("\u536b\u751f\u95f4", "Restroom", "\u885b\u751f\u9593", "\u0422\u0443\u0430\u043b\u0435\u0442", "\u30c8\u30a4\u30ec"), 0),
            new CampusRuntimeAreaPreset("Library", new CampusLocalizedTextEntry("\u56fe\u4e66\u9986", "Library", "\u5716\u66f8\u9928", "\u0411\u0438\u0431\u043b\u0438\u043e\u0442\u0435\u043a\u0430", "\u56f3\u66f8\u5ba4"), 0),
            new CampusRuntimeAreaPreset("Stairwell", new CampusLocalizedTextEntry("\u697c\u68af\u95f4", "Stairwell", "\u6a13\u68af\u9593", "\u041b\u0435\u0441\u0442\u043d\u0438\u0446\u0430", "\u968e\u6bb5\u5ba4"), 0),
            new CampusRuntimeAreaPreset("HumanResources", new CampusLocalizedTextEntry("\u4eba\u4e8b\u5904", "Human Resources", "\u4eba\u4e8b\u8655", "\u041e\u0442\u0434\u0435\u043b \u043a\u0430\u0434\u0440\u043e\u0432", "\u4eba\u4e8b\u8ab2"), 0),
            new CampusRuntimeAreaPreset("ShrineRoom", new CampusLocalizedTextEntry("\u795e\u9f9b\u5ba4", "Shrine Room", "\u795e\u9f95\u5ba4", "\u041a\u043e\u043c\u043d\u0430\u0442\u0430 \u0441\u0432\u044f\u0442\u044b\u043d\u0438", "\u795e\u68da\u5ba4"), 0)
        };

        internal static bool TryResolveRoomName(string roomName, out string presetRoomName)
        {
            presetRoomName = string.Empty;
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return false;
            }

            string key = NormalizeKey(roomName);
            CampusRuntimeAreaPreset[] presets = Presets;
            for (int i = 0; i < presets.Length; i++)
            {
                CampusRuntimeAreaPreset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                if (key == NormalizeKey(preset.RoomName) ||
                    MatchesLabelKey(key, preset.Label))
                {
                    presetRoomName = preset.RoomName;
                    return true;
                }
            }

            return TryResolveKnownAlias(key, out presetRoomName);
        }

        internal static CampusRuntimeAreaPreset GetPreset(string roomName)
        {
            if (!TryResolveRoomName(roomName, out string presetRoomName))
            {
                return null;
            }

            CampusRuntimeAreaPreset[] presets = Presets;
            for (int i = 0; i < presets.Length; i++)
            {
                CampusRuntimeAreaPreset preset = presets[i];
                if (preset != null &&
                    string.Equals(preset.RoomName, presetRoomName, StringComparison.OrdinalIgnoreCase))
                {
                    return preset;
                }
            }

            return null;
        }

        private static CampusRuntimeAreaPreset[] LoadPresets()
        {
            if (!CampusRuntimeModPresetStore.TryReadJson("AreaPresets.json", out string json))
            {
                return BuiltInPresets;
            }

            try
            {
                AreaPresetFile file = JsonUtility.FromJson<AreaPresetFile>(json);
                if (file == null || file.Presets == null || file.Presets.Count == 0)
                {
                    return BuiltInPresets;
                }

                List<CampusRuntimeAreaPreset> loaded = new List<CampusRuntimeAreaPreset>();
                for (int i = 0; i < file.Presets.Count; i++)
                {
                    AreaPresetRecord record = file.Presets[i];
                    if (record == null || string.IsNullOrWhiteSpace(record.RoomName))
                    {
                        continue;
                    }

                    loaded.Add(new CampusRuntimeAreaPreset(
                        record.RoomName.Trim(),
                        BuildTextEntry(
                            record.ChineseLabel,
                            record.EnglishLabel,
                            record.TraditionalChineseLabel,
                            record.RussianLabel,
                            record.JapaneseLabel,
                            record.RoomName),
                        record.RequiredCount));
                }

                return loaded.Count > 0 ? loaded.ToArray() : BuiltInPresets;
            }
            catch (Exception exception)
            {
                CampusRuntimePresetLogTextCatalog.Warning(
                    CampusRuntimePresetLogTextId.FailedToLoadAreaPresets,
                    exception.Message);
                return BuiltInPresets;
            }
        }

        private static bool TryResolveKnownAlias(string key, out string presetRoomName)
        {
            if (ContainsToken(key, "\u6559\u5ba4", "class"))
            {
                presetRoomName = "Classroom";
                return true;
            }

            if (ContainsToken(key, "\u8d70\u5eca", "\u8fc7\u9053", "corridor", "hall"))
            {
                presetRoomName = "Corridor";
                return true;
            }

            if (ContainsToken(key, "\u529e\u516c\u5ba4", "\u6559\u5e08", "office", "teacher"))
            {
                presetRoomName = "Office";
                return true;
            }

            if (ContainsToken(key, "\u516c\u5171", "\u6d3b\u52a8", "common", "activity"))
            {
                presetRoomName = "CommonActivityZone";
                return true;
            }

            if (ContainsToken(key, "\u5ba4\u5916", "\u6821\u5916", "\u64cd\u573a", "outdoor", "outside"))
            {
                presetRoomName = "Outdoor";
                return true;
            }

            if (ContainsToken(key, "\u5bbf\u820d", "dorm"))
            {
                presetRoomName = "Dormitory";
                return true;
            }

            if (ContainsToken(key, "\u536b\u751f\u95f4", "\u5395\u6240", "\u6d17\u624b\u95f4", "restroom", "toilet", "bath"))
            {
                presetRoomName = "Restroom";
                return true;
            }

            if (ContainsToken(key, "\u56fe\u4e66\u9986", "library"))
            {
                presetRoomName = "Library";
                return true;
            }

            if (ContainsToken(key, "\u697c\u68af", "stair"))
            {
                presetRoomName = "Stairwell";
                return true;
            }

            if (ContainsToken(key, "\u4eba\u4e8b", "humanresources", "hr"))
            {
                presetRoomName = "HumanResources";
                return true;
            }

            if (ContainsToken(key, "\u795e\u9f9b", "shrine"))
            {
                presetRoomName = "ShrineRoom";
                return true;
            }

            presetRoomName = string.Empty;
            return false;
        }

        private static bool MatchesLabelKey(string key, CampusLocalizedTextEntry label)
        {
            return key == NormalizeKey(label.Chinese) ||
                   key == NormalizeKey(label.English) ||
                   key == NormalizeKey(label.TraditionalChinese) ||
                   key == NormalizeKey(label.Russian) ||
                   key == NormalizeKey(label.Japanese);
        }

        private static CampusLocalizedTextEntry BuildTextEntry(
            string chinese,
            string english,
            string traditionalChinese,
            string russian,
            string japanese,
            string fallback)
        {
            string cleanFallback = Clean(fallback);
            return new CampusLocalizedTextEntry(
                FirstNonEmpty(chinese, cleanFallback),
                FirstNonEmpty(english, cleanFallback),
                Clean(traditionalChinese),
                Clean(russian),
                Clean(japanese));
        }

        private static string FirstNonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static bool ContainsToken(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && value.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        private sealed class AreaPresetFile
        {
            public List<AreaPresetRecord> Presets = new List<AreaPresetRecord>();
        }

        [Serializable]
        private sealed class AreaPresetRecord
        {
            public string RoomName = string.Empty;
            public string ChineseLabel = string.Empty;
            public string EnglishLabel = string.Empty;
            public string TraditionalChineseLabel = string.Empty;
            public string RussianLabel = string.Empty;
            public string JapaneseLabel = string.Empty;
            public int RequiredCount = 0;
        }
    }
}
