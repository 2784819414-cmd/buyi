using System.Collections.Generic;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public enum CampusEconomyUiTextId
    {
        Money = 0,
        DivinePower = 1,
        PendingCheckout = 2,
        PendingItems = 3,
        PendingTotal = 4,
        CanAfford = 5,
        CannotAfford = 6
    }

    public static class CampusEconomyUiTextCatalog
    {
        private readonly struct TextEntry
        {
            public TextEntry(string chinese, string english)
            {
                Chinese = chinese;
                English = english;
            }

            public string Chinese { get; }
            public string English { get; }
        }

        private static readonly Dictionary<CampusEconomyUiTextId, TextEntry> Entries = new()
        {
            { CampusEconomyUiTextId.Money, new TextEntry("金钱", "Money") },
            { CampusEconomyUiTextId.DivinePower, new TextEntry("神力", "Divine Power") },
            { CampusEconomyUiTextId.PendingCheckout, new TextEntry("待结账", "Pending Checkout") },
            { CampusEconomyUiTextId.PendingItems, new TextEntry("商品数", "Items") },
            { CampusEconomyUiTextId.PendingTotal, new TextEntry("总价", "Total") },
            { CampusEconomyUiTextId.CanAfford, new TextEntry("余额足够", "Ready To Pay") },
            { CampusEconomyUiTextId.CannotAfford, new TextEntry("余额不足", "Not Enough Money") }
        };

        public static string Get(CampusEconomyUiTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusEconomyUiTextId id)
        {
            TextEntry entry = Entries.TryGetValue(id, out TextEntry resolved)
                ? resolved
                : new TextEntry(id.ToString(), id.ToString());

            return language switch
            {
                CampusDisplayLanguage.English => entry.English,
                CampusDisplayLanguage.Bilingual => entry.Chinese + " / " + entry.English,
                _ => entry.Chinese
            };
        }
    }
}
