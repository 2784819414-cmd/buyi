using System.Collections.Generic;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public enum CampusGameplayHudTextId
    {
        Suspicion = 0,
        TeacherAlertness = 1,
        WarningCount = 2,
        LowRisk = 3,
        MediumRisk = 4,
        HighRisk = 5,
        UnknownArea = 6,
        Floor = 7,
        Facing = 8,
        Money = 9,
        Stamina = 10,
        Backpack = 11,
        NoBackpack = 12,
        North = 13,
        South = 14,
        West = 15,
        East = 16,
        WarningSubtitleSafe = 17,
        WarningSubtitleRisky = 18,
        WeightUnit = 19,
        EquippedBackpack = 20,
        PendingCheckout = 21,
        PendingCheckoutSummary = 22,
        ReadyToPay = 23,
        NotEnoughMoney = 24
    }

    public static class CampusGameplayHudTextCatalog
    {
        private static readonly Dictionary<CampusGameplayHudTextId, Entry> Entries = new()
        {
            { CampusGameplayHudTextId.Suspicion, new Entry("可疑度", "Suspicion") },
            { CampusGameplayHudTextId.TeacherAlertness, new Entry("警戒", "Alertness") },
            { CampusGameplayHudTextId.WarningCount, new Entry("警告", "Warnings") },
            { CampusGameplayHudTextId.LowRisk, new Entry("低风险", "Low Risk") },
            { CampusGameplayHudTextId.MediumRisk, new Entry("中风险", "Medium Risk") },
            { CampusGameplayHudTextId.HighRisk, new Entry("高风险", "High Risk") },
            { CampusGameplayHudTextId.UnknownArea, new Entry("未知区域", "Unknown Area") },
            { CampusGameplayHudTextId.Floor, new Entry("楼层", "Floor") },
            { CampusGameplayHudTextId.Facing, new Entry("朝向", "Facing") },
            { CampusGameplayHudTextId.Money, new Entry("金钱", "Money") },
            { CampusGameplayHudTextId.Stamina, new Entry("体力", "Stamina") },
            { CampusGameplayHudTextId.Backpack, new Entry("背包", "Backpack") },
            { CampusGameplayHudTextId.NoBackpack, new Entry("未装备", "Not Equipped") },
            { CampusGameplayHudTextId.North, new Entry("北", "North") },
            { CampusGameplayHudTextId.South, new Entry("南", "South") },
            { CampusGameplayHudTextId.West, new Entry("西", "West") },
            { CampusGameplayHudTextId.East, new Entry("东", "East") },
            { CampusGameplayHudTextId.WarningSubtitleSafe, new Entry("保持安静，低调行事。", "Stay quiet. Keep a low profile.") },
            { CampusGameplayHudTextId.WarningSubtitleRisky, new Entry("注意视线与纪律风险。", "Watch line of sight and discipline risk.") },
            { CampusGameplayHudTextId.WeightUnit, new Entry("千克", "kg") },
            { CampusGameplayHudTextId.EquippedBackpack, new Entry("已装备", "Equipped") },
            { CampusGameplayHudTextId.PendingCheckout, new Entry("\u5f85\u7ed3\u7b97", "Pending Checkout") },
            { CampusGameplayHudTextId.PendingCheckoutSummary, new Entry("{0} \u4ef6 / {1}", "{0} items / {1}") },
            { CampusGameplayHudTextId.ReadyToPay, new Entry("\u524d\u5f80\u6536\u94f6\u53f0\u7ed3\u8d26", "Go to checkout") },
            { CampusGameplayHudTextId.NotEnoughMoney, new Entry("\u4f59\u989d\u4e0d\u8db3", "Not enough money") }
        };

        public static string Get(CampusGameplayHudTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusGameplayHudTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return entry.Get(language);
        }

        public static string Format(CampusGameplayHudTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }
}
