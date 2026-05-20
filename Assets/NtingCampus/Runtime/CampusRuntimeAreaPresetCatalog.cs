using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal sealed class CampusRuntimeAreaPreset
    {
        public readonly string RoomName;
        public readonly string ChineseLabel;
        public readonly string EnglishLabel;
        public readonly int RequiredCount;

        public CampusRuntimeAreaPreset(
            string roomName,
            string chineseLabel,
            string englishLabel,
            int requiredCount)
        {
            RoomName = roomName;
            ChineseLabel = chineseLabel;
            EnglishLabel = englishLabel;
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
            new CampusRuntimeAreaPreset("Classroom", "\u6559\u5ba4", "Classroom", 2),
            new CampusRuntimeAreaPreset("Corridor", "\u8d70\u5eca", "Corridor", 1),
            new CampusRuntimeAreaPreset("Office", "\u529e\u516c\u5ba4", "Office", 1),
            new CampusRuntimeAreaPreset("CommonActivityZone", "\u516c\u5171\u6d3b\u52a8\u533a", "Common Activity Zone", 1),
            new CampusRuntimeAreaPreset("Canteen", "\u98df\u5802", "Canteen", 1),
            new CampusRuntimeAreaPreset("Store", "\u8d85\u5e02", "Store", 1),
            new CampusRuntimeAreaPreset("Outdoor", "\u5ba4\u5916", "Outdoor", 1),
            new CampusRuntimeAreaPreset("Dormitory", "\u5bbf\u820d", "Dormitory", 0),
            new CampusRuntimeAreaPreset("Restroom", "\u536b\u751f\u95f4", "Restroom", 0),
            new CampusRuntimeAreaPreset("Library", "\u56fe\u4e66\u9986", "Library", 0),
            new CampusRuntimeAreaPreset("Stairwell", "\u697c\u68af\u95f4", "Stairwell", 0),
            new CampusRuntimeAreaPreset("HumanResources", "\u4eba\u4e8b\u5904", "Human Resources", 0),
            new CampusRuntimeAreaPreset("ShrineRoom", "\u795e\u9f9b\u5ba4", "Shrine Room", 0)
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
                    key == NormalizeKey(preset.EnglishLabel) ||
                    key == NormalizeKey(preset.ChineseLabel))
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
                        string.IsNullOrWhiteSpace(record.ChineseLabel) ? record.RoomName.Trim() : record.ChineseLabel.Trim(),
                        string.IsNullOrWhiteSpace(record.EnglishLabel) ? record.RoomName.Trim() : record.EnglishLabel.Trim(),
                        record.RequiredCount));
                }

                return loaded.Count > 0 ? loaded.ToArray() : BuiltInPresets;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusRuntimeAreaPresetCatalog] Failed to load AreaPresets.json: " + exception.Message);
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

            if (ContainsToken(key, "\u98df\u5802", "\u9910\u5385", "canteen", "dining"))
            {
                presetRoomName = "Canteen";
                return true;
            }

            if (ContainsToken(key, "\u8d85\u5e02", "\u5546\u5e97", "\u5c0f\u5356", "shop", "store", "market"))
            {
                presetRoomName = "Store";
                return true;
            }

            if (ContainsToken(key, "\u5ba4\u5916", "\u6821\u5916", "\u64cd\u573a", "\u5916\u5356", "outdoor", "outside", "delivery"))
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
            public int RequiredCount = 0;
        }
    }
}
