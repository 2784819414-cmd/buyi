using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampus.Gameplay.Delivery
{
    internal enum CampusDeliveryTextId
    {
        AngryMark = 0,
        OrderPlacedLog = 1,
        DeliveryArrivedLog = 2,
        DeliveryStolenLog = 3,
        OrderPlacedConsole = 4
    }

    internal static class CampusDeliveryTextCatalog
    {
        public static string Get(CampusDeliveryTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Format(CampusDeliveryTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        private static string Get(CampusDisplayLanguage language, CampusDeliveryTextId id)
        {
            switch (id)
            {
                case CampusDeliveryTextId.AngryMark:
                    return "💢";
                case CampusDeliveryTextId.OrderPlacedLog:
                    return Resolve(language, "{0} 点了校外外卖。", "{0} ordered outside delivery.");
                case CampusDeliveryTextId.DeliveryArrivedLog:
                    return Resolve(language, "{0} 的外卖送到了。", "{0}'s delivery arrived.");
                case CampusDeliveryTextId.DeliveryStolenLog:
                    return Resolve(language, "{0} 的外卖被拿走了。", "{0}'s delivery was taken.");
                case CampusDeliveryTextId.OrderPlacedConsole:
                    return Resolve(
                        language,
                        "Delivery order placed: actor={0} ({1}), meal={2}, ordered={3}, due={4}.",
                        "Delivery order placed: actor={0} ({1}), meal={2}, ordered={3}, due={4}.");
                default:
                    return string.Empty;
            }
        }

        private static string Resolve(CampusDisplayLanguage language, string chinese, string english)
        {
            return language == CampusDisplayLanguage.English ? english : chinese;
        }
    }
}
