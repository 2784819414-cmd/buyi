using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeGameplayActorPresetCatalog
    {
        private static CampusRuntimeGameplayActorPreset[] cachedPresets;

        internal static CampusRuntimeGameplayActorPreset[] Presets
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

        private static readonly CampusRuntimeGameplayActorPreset[] BuiltInPresets =
        {
            new CampusRuntimeGameplayActorPreset(
                new CampusLocalizedTextEntry("\u5b66\u751f", "Student", "\u5b78\u751f", "\u0423\u0447\u0435\u043d\u0438\u043a", "\u751f\u5f92"),
                new CampusLocalizedTextEntry("\u5b66\u751f", "Student", "\u5b78\u751f", "\u0423\u0447\u0435\u043d\u0438\u043a", "\u751f\u5f92"),
                "student",
                CampusCharacterRole.Student,
                CampusTeacherDuty.None,
                CampusStaffDuty.None,
                "class_1",
                40,
                20,
                60,
                new[] { CampusCharacterTrait.Ordinary },
                new Color(0.38f, 0.56f, 0.83f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                new CampusLocalizedTextEntry("\u6363\u86cb\u5b66\u751f", "Troublemaker", "\u6417\u86cb\u5b78\u751f", "\u041d\u0430\u0440\u0443\u0448\u0438\u0442\u0435\u043b\u044c", "\u554f\u984c\u5150"),
                new CampusLocalizedTextEntry("\u6363\u86cb\u5b66\u751f", "Troublemaker", "\u6417\u86cb\u5b78\u751f", "\u041d\u0430\u0440\u0443\u0448\u0438\u0442\u0435\u043b\u044c", "\u554f\u984c\u5150"),
                "student_trouble",
                CampusCharacterRole.Student,
                CampusTeacherDuty.None,
                CampusStaffDuty.None,
                "class_1",
                28,
                68,
                45,
                new[] { CampusCharacterTrait.Troublemaker },
                new Color(0.62f, 0.42f, 0.66f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                new CampusLocalizedTextEntry("\u597d\u5b66\u751f", "Good Student", "\u597d\u5b78\u751f", "\u041e\u0442\u043b\u0438\u0447\u043d\u0438\u043a", "\u512a\u7b49\u751f"),
                new CampusLocalizedTextEntry("\u597d\u5b66\u751f", "GoodStudent", "\u597d\u5b78\u751f", "\u041e\u0442\u043b\u0438\u0447\u043d\u0438\u043a", "\u512a\u7b49\u751f"),
                "student_good",
                CampusCharacterRole.Student,
                CampusTeacherDuty.None,
                CampusStaffDuty.None,
                "class_1",
                24,
                12,
                80,
                new[] { CampusCharacterTrait.GoodStudent },
                new Color(0.42f, 0.62f, 0.48f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                new CampusLocalizedTextEntry("\u8bed\u6587\u8001\u5e08", "World Teacher", "\u8a9e\u6587\u8001\u5e2b", "\u0423\u0447\u0438\u0442\u0435\u043b\u044c \u044f\u0437\u044b\u043a\u0430", "\u56fd\u8a9e\u6559\u5e2b"),
                new CampusLocalizedTextEntry("\u8bed\u6587\u8001\u5e08", "WorldTeacher", "\u8a9e\u6587\u8001\u5e2b", "\u0423\u0447\u0438\u0442\u0435\u043b\u044c \u044f\u0437\u044b\u043a\u0430", "\u56fd\u8a9e\u6559\u5e2b"),
                "teacher_world",
                CampusCharacterRole.Teacher,
                CampusTeacherDuty.WorldLanguageTeacher,
                CampusStaffDuty.None,
                "class_1",
                10,
                0,
                160,
                new[] { CampusCharacterTrait.Ordinary },
                new Color(0.48f, 0.48f, 0.52f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                new CampusLocalizedTextEntry("\u6570\u5b66\u8001\u5e08", "Math Teacher", "\u6578\u5b78\u8001\u5e2b", "\u0423\u0447\u0438\u0442\u0435\u043b\u044c \u043c\u0430\u0442\u0435\u043c\u0430\u0442\u0438\u043a\u0438", "\u6570\u5b66\u6559\u5e2b"),
                new CampusLocalizedTextEntry("\u6570\u5b66\u8001\u5e08", "MathTeacher", "\u6578\u5b78\u8001\u5e2b", "\u0423\u0447\u0438\u0442\u0435\u043b\u044c \u043c\u0430\u0442\u0435\u043c\u0430\u0442\u0438\u043a\u0438", "\u6570\u5b66\u6559\u5e2b"),
                "teacher_math",
                CampusCharacterRole.Teacher,
                CampusTeacherDuty.MathTeacher,
                CampusStaffDuty.None,
                "class_1",
                10,
                0,
                160,
                new[] { CampusCharacterTrait.Ordinary },
                new Color(0.42f, 0.42f, 0.48f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                new CampusLocalizedTextEntry("\u804c\u5458", "Staff", "\u8077\u54e1", "\u0421\u043e\u0442\u0440\u0443\u0434\u043d\u0438\u043a", "\u8077\u54e1"),
                new CampusLocalizedTextEntry("\u804c\u5458", "Staff", "\u8077\u54e1", "\u0421\u043e\u0442\u0440\u0443\u0434\u043d\u0438\u043a", "\u8077\u54e1"),
                "staff",
                CampusCharacterRole.Staff,
                CampusTeacherDuty.None,
                CampusStaffDuty.None,
                string.Empty,
                10,
                0,
                120,
                new[] { CampusCharacterTrait.Ordinary },
                new Color(0.65f, 0.54f, 0.32f, 1f))
        };

        private static CampusRuntimeGameplayActorPreset[] LoadPresets()
        {
            if (!CampusRuntimeModPresetStore.TryReadJson("GameplayActorPresets.json", out string json))
            {
                return BuiltInPresets;
            }

            try
            {
                GameplayActorPresetFile file = JsonUtility.FromJson<GameplayActorPresetFile>(json);
                if (file == null || file.Presets == null || file.Presets.Count == 0)
                {
                    return BuiltInPresets;
                }

                List<CampusRuntimeGameplayActorPreset> loaded = new List<CampusRuntimeGameplayActorPreset>();
                for (int i = 0; i < file.Presets.Count; i++)
                {
                    GameplayActorPresetRecord record = file.Presets[i];
                    if (record == null)
                    {
                        continue;
                    }

                    CampusLocalizedTextEntry label = BuildTextEntry(
                        record.ChineseLabel,
                        record.EnglishLabel,
                        record.TraditionalChineseLabel,
                        record.RussianLabel,
                        record.JapaneseLabel);
                    if (!label.HasAnyText)
                    {
                        continue;
                    }

                    string english = !string.IsNullOrWhiteSpace(label.English) ? label.English : label.Get(CampusDisplayLanguage.English);
                    string actorIdPrefix = string.IsNullOrWhiteSpace(record.ActorIdPrefix)
                        ? NormalizeActorIdPrefix(english)
                        : record.ActorIdPrefix.Trim();
                    if (string.IsNullOrWhiteSpace(actorIdPrefix))
                    {
                        continue;
                    }

                    CampusLocalizedTextEntry namePrefix = BuildTextEntry(
                        record.ChineseDisplayName,
                        record.EnglishDisplayName,
                        record.TraditionalChineseDisplayName,
                        record.RussianDisplayName,
                        record.JapaneseDisplayName);
                    if (!namePrefix.HasAnyText)
                    {
                        namePrefix = label;
                    }

                    loaded.Add(new CampusRuntimeGameplayActorPreset(
                        label,
                        namePrefix,
                        actorIdPrefix,
                        ParseEnum(record.Role, CampusCharacterRole.Student),
                        ParseEnum(record.TeacherDuty, CampusTeacherDuty.None),
                        ParseEnum(record.StaffDuty, CampusStaffDuty.None),
                        string.IsNullOrWhiteSpace(record.ClassId) ? string.Empty : record.ClassId.Trim(),
                        record.Sleepiness,
                        record.Mischief,
                        record.InitialMoney,
                        ParseTraits(record.Traits),
                        CampusRuntimeModPresetStore.ParseColor(record.Color, new Color(0.5f, 0.6f, 0.75f, 1f))));
                }

                return loaded.Count > 0 ? loaded.ToArray() : BuiltInPresets;
            }
            catch (Exception exception)
            {
                CampusRuntimePresetLogTextCatalog.Warning(
                    CampusRuntimePresetLogTextId.FailedToLoadGameplayActorPresets,
                    exception.Message);
                return BuiltInPresets;
            }
        }

        private static CampusCharacterTrait[] ParseTraits(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new[] { CampusCharacterTrait.Ordinary };
            }

            List<CampusCharacterTrait> traits = new List<CampusCharacterTrait>();
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]) &&
                    Enum.TryParse(values[i].Trim(), true, out CampusCharacterTrait trait))
                {
                    traits.Add(trait);
                }
            }

            return traits.Count > 0 ? traits.ToArray() : new[] { CampusCharacterTrait.Ordinary };
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Enum.TryParse(value.Trim(), true, out TEnum parsed)
                ? parsed
                : fallback;
        }

        private static CampusLocalizedTextEntry BuildTextEntry(
            string chinese,
            string english,
            string traditionalChinese,
            string russian,
            string japanese)
        {
            return new CampusLocalizedTextEntry(
                Clean(chinese),
                Clean(english),
                Clean(traditionalChinese),
                Clean(russian),
                Clean(japanese));
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeActorIdPrefix(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
        }

        [Serializable]
        private sealed class GameplayActorPresetFile
        {
            public List<GameplayActorPresetRecord> Presets = new List<GameplayActorPresetRecord>();
        }

        [Serializable]
        private sealed class GameplayActorPresetRecord
        {
            public string ChineseLabel = string.Empty;
            public string EnglishLabel = string.Empty;
            public string TraditionalChineseLabel = string.Empty;
            public string RussianLabel = string.Empty;
            public string JapaneseLabel = string.Empty;
            public string ChineseDisplayName = string.Empty;
            public string EnglishDisplayName = string.Empty;
            public string TraditionalChineseDisplayName = string.Empty;
            public string RussianDisplayName = string.Empty;
            public string JapaneseDisplayName = string.Empty;
            public string ActorIdPrefix = string.Empty;
            public string Role = string.Empty;
            public string TeacherDuty = string.Empty;
            public string StaffDuty = string.Empty;
            public string ClassId = string.Empty;
            public int Sleepiness = 40;
            public int Mischief = 20;
            public int InitialMoney = NtingCampus.Gameplay.Economy.CampusCharacterEconomyDefaults.UseRoleDefaultMoney;
            public string[] Traits = Array.Empty<string>();
            public string Color = string.Empty;
        }
    }
}
