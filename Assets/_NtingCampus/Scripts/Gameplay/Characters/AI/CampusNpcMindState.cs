using System;
using UnityEngine;

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
        public string FocusRoomId = string.Empty;
        public Vector3 FocusPosition;
        public float FocusExpiresAt = -1f;

        public bool HasActiveFocus => Time.time < FocusExpiresAt && !string.IsNullOrWhiteSpace(FocusRoomId);

        public void ClearFocus()
        {
            FocusRoomId = string.Empty;
            FocusPosition = Vector3.zero;
            FocusExpiresAt = -1f;
        }

        public void RememberFocus(string roomId, Vector3 position, float seconds)
        {
            FocusRoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            FocusPosition = position;
            FocusExpiresAt = Time.time + Mathf.Max(0.5f, seconds);
        }
    }
}
