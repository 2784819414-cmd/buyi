using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Sanctions;
using UnityEngine;

namespace NtingCampus.Gameplay.Events
{
    public readonly struct CampusPrankAttemptedEvent
    {
        public CampusPrankAttemptedEvent(
            CampusPrankType prankType,
            string actorId,
            string targetId,
            string roomId,
            bool duringClassSession)
        {
            PrankType = prankType;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            DuringClassSession = duringClassSession;
        }

        public CampusPrankType PrankType { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string RoomId { get; }
        public bool DuringClassSession { get; }
    }

    public readonly struct CampusPrankResolvedEvent
    {
        public CampusPrankResolvedEvent(
            CampusPrankType prankType,
            string actorId,
            string targetId,
            string roomId,
            bool succeeded,
            bool detectedByTeacher,
            int divinePowerReward)
        {
            PrankType = prankType;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            Succeeded = succeeded;
            DetectedByTeacher = detectedByTeacher;
            DivinePowerReward = divinePowerReward;
        }

        public CampusPrankType PrankType { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string RoomId { get; }
        public bool Succeeded { get; }
        public bool DetectedByTeacher { get; }
        public int DivinePowerReward { get; }
    }

    public readonly struct CampusSanctionIssuedEvent
    {
        public CampusSanctionIssuedEvent(
            string actorId,
            string roomId,
            CampusSanctionLevel sanctionLevel,
            int warningCount)
        {
            ActorId = actorId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            SanctionLevel = sanctionLevel;
            WarningCount = warningCount;
        }

        public string ActorId { get; }
        public string RoomId { get; }
        public CampusSanctionLevel SanctionLevel { get; }
        public int WarningCount { get; }
    }

    [DisallowMultipleComponent]
    public sealed class CampusGameplayEventHub : MonoBehaviour
    {
        private const int MaxRecentEvents = 20;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private List<string> recentEventIds = new List<string>(MaxRecentEvents);

        public IReadOnlyList<string> RecentEventIds => recentEventIds;
        public event System.Action<CampusPrankAttemptedEvent> PrankAttempted;
        public event System.Action<CampusPrankResolvedEvent> PrankResolved;
        public event System.Action<CampusSanctionIssuedEvent> SanctionIssued;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
        }

        public void Record(string eventId)
        {
            string normalizedId = string.IsNullOrWhiteSpace(eventId) ? "gameplay.unknown" : eventId.Trim();
            recentEventIds.Add(normalizedId);
            if (recentEventIds.Count > MaxRecentEvents)
            {
                recentEventIds.RemoveRange(0, recentEventIds.Count - MaxRecentEvents);
            }

            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog("[Event] " + normalizedId);
            }
        }

        public void PublishPrankAttempted(CampusPrankAttemptedEvent eventData)
        {
            Record("prank.attempted." + Normalize(eventData.PrankType));
            PrankAttempted?.Invoke(eventData);
        }

        public void PublishPrankResolved(CampusPrankResolvedEvent eventData)
        {
            string suffix = eventData.Succeeded ? "success" : "failure";
            Record("prank.resolved." + Normalize(eventData.PrankType) + "." + suffix);
            PrankResolved?.Invoke(eventData);
        }

        public void PublishSanctionIssued(CampusSanctionIssuedEvent eventData)
        {
            Record("sanction.issued." + Normalize(eventData.SanctionLevel));
            SanctionIssued?.Invoke(eventData);
        }

        private static string Normalize(CampusPrankType prankType)
        {
            return prankType.ToString().ToLowerInvariant();
        }

        private static string Normalize(CampusSanctionLevel sanctionLevel)
        {
            return sanctionLevel.ToString().ToLowerInvariant();
        }
    }
}
