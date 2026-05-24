using System.Collections.Generic;

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
        DivinePower = 10,
        Backpack = 11,
        NoBackpack = 12,
        PendingCheckout = 13,
        PendingItems = 14,
        PendingTotal = 15,
        ReadyToPay = 16,
        NotEnoughMoney = 17,
        NoInteraction = 18,
        North = 19,
        South = 20,
        West = 21,
        East = 22,
        WarningSubtitleSafe = 23,
        WarningSubtitleRisky = 24,
        LeftHand = 25,
        RightHand = 26
    }

    public static class CampusGameplayHudTextCatalog
    {
        private readonly struct Entry
        {
            public Entry(string chinese, string english)
            {
                Chinese = chinese;
                English = english;
            }

            public string Chinese { get; }
            public string English { get; }
        }

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
            { CampusGameplayHudTextId.DivinePower, new Entry("神力", "Divine Power") },
            { CampusGameplayHudTextId.Backpack, new Entry("背包", "Backpack") },
            { CampusGameplayHudTextId.NoBackpack, new Entry("未装备", "Not Equipped") },
            { CampusGameplayHudTextId.PendingCheckout, new Entry("待结算", "Pending Checkout") },
            { CampusGameplayHudTextId.PendingItems, new Entry("物品", "Items") },
            { CampusGameplayHudTextId.PendingTotal, new Entry("总价", "Total") },
            { CampusGameplayHudTextId.ReadyToPay, new Entry("余额足够", "Ready To Pay") },
            { CampusGameplayHudTextId.NotEnoughMoney, new Entry("余额不足", "Not Enough Money") },
            { CampusGameplayHudTextId.NoInteraction, new Entry("当前没有可交互目标", "No interactable target") },
            { CampusGameplayHudTextId.North, new Entry("北", "North") },
            { CampusGameplayHudTextId.South, new Entry("南", "South") },
            { CampusGameplayHudTextId.West, new Entry("西", "West") },
            { CampusGameplayHudTextId.East, new Entry("东", "East") },
            { CampusGameplayHudTextId.WarningSubtitleSafe, new Entry("保持安静，低调行事", "Stay quiet. Keep a low profile.") },
            { CampusGameplayHudTextId.WarningSubtitleRisky, new Entry("注意视线与纪律风险", "Watch line of sight and discipline risk.") },
            { CampusGameplayHudTextId.LeftHand, new Entry("左手", "Left Hand") },
            { CampusGameplayHudTextId.RightHand, new Entry("右手", "Right Hand") }
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

            return language switch
            {
                CampusDisplayLanguage.English => entry.English,
                CampusDisplayLanguage.Bilingual => entry.Chinese + " / " + entry.English,
                _ => entry.Chinese
            };
        }
    }
}
