using System;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public readonly struct CampusEconomyHudSnapshot : IEquatable<CampusEconomyHudSnapshot>
    {
        public CampusEconomyHudSnapshot(
            int playerMoney,
            int divinePower,
            int pendingCheckoutCount,
            int pendingCheckoutTotal,
            bool canAffordCheckout,
            bool showCheckoutPanel)
        {
            PlayerMoney = playerMoney;
            DivinePower = divinePower;
            PendingCheckoutCount = pendingCheckoutCount;
            PendingCheckoutTotal = pendingCheckoutTotal;
            CanAffordCheckout = canAffordCheckout;
            ShowCheckoutPanel = showCheckoutPanel;
        }

        public int PlayerMoney { get; }
        public int DivinePower { get; }
        public int PendingCheckoutCount { get; }
        public int PendingCheckoutTotal { get; }
        public bool CanAffordCheckout { get; }
        public bool ShowCheckoutPanel { get; }

        public bool Equals(CampusEconomyHudSnapshot other)
        {
            return PlayerMoney == other.PlayerMoney &&
                   DivinePower == other.DivinePower &&
                   PendingCheckoutCount == other.PendingCheckoutCount &&
                   PendingCheckoutTotal == other.PendingCheckoutTotal &&
                   CanAffordCheckout == other.CanAffordCheckout &&
                   ShowCheckoutPanel == other.ShowCheckoutPanel;
        }

        public override bool Equals(object obj)
        {
            return obj is CampusEconomyHudSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PlayerMoney;
                hashCode = (hashCode * 397) ^ DivinePower;
                hashCode = (hashCode * 397) ^ PendingCheckoutCount;
                hashCode = (hashCode * 397) ^ PendingCheckoutTotal;
                hashCode = (hashCode * 397) ^ CanAffordCheckout.GetHashCode();
                hashCode = (hashCode * 397) ^ ShowCheckoutPanel.GetHashCode();
                return hashCode;
            }
        }
    }
}

