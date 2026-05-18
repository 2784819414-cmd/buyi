using System;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [Serializable]
    public sealed class CampusNpcIntent
    {
        public CampusNpcIntentKind Kind;
        public string Label = string.Empty;
        public string RoomId = string.Empty;
        public Vector3 TargetPosition;
        public float StopDistance = 0.14f;
        public float HoldSeconds;
        public bool UsesNavigation = true;

        public static CampusNpcIntent Idle(string label)
        {
            return new CampusNpcIntent
            {
                Kind = CampusNpcIntentKind.Idle,
                Label = label ?? string.Empty,
                UsesNavigation = false,
                StopDistance = 0.14f
            };
        }

        public static CampusNpcIntent Move(
            CampusNpcIntentKind kind,
            string label,
            string roomId,
            Vector3 targetPosition,
            float stopDistance = 0.14f)
        {
            return new CampusNpcIntent
            {
                Kind = kind,
                Label = label ?? string.Empty,
                RoomId = roomId ?? string.Empty,
                TargetPosition = targetPosition,
                StopDistance = Mathf.Max(0.02f, stopDistance),
                UsesNavigation = true
            };
        }

        public static CampusNpcIntent Hold(
            CampusNpcIntentKind kind,
            string label,
            float holdSeconds)
        {
            return new CampusNpcIntent
            {
                Kind = kind,
                Label = label ?? string.Empty,
                HoldSeconds = Mathf.Max(0f, holdSeconds),
                StopDistance = 0.14f,
                UsesNavigation = false
            };
        }

        public bool SameTargetAs(CampusNpcIntent other)
        {
            if (other == null)
            {
                return false;
            }

            return Kind == other.Kind &&
                   string.Equals(RoomId ?? string.Empty, other.RoomId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   Vector2.SqrMagnitude((Vector2)(TargetPosition - other.TargetPosition)) <= 0.04f &&
                   Mathf.Abs(StopDistance - other.StopDistance) <= 0.03f;
        }
    }
}
