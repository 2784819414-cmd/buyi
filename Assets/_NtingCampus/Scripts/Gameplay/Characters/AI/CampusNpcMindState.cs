using System;
namespace NtingCampus.Gameplay.Characters
{
    [Serializable]
    public sealed class CampusNpcMindState
    {
        public CampusNpcIntent CurrentIntent = CampusNpcIntent.Idle("Idle");
        public CampusNpcDeliveryState DeliveryState;
        public float IntentHoldUntil = -1f;
        public float DeliveryReadyAt = -1f;
        public float NextDeliveryOrderAllowedAt = -1f;
        public float NextStoreVisitAllowedAt = -1f;
    }
}
