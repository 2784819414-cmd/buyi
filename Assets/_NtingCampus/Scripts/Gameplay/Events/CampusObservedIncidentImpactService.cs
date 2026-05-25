using System;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampus.Gameplay.Events
{
    [DisallowMultipleComponent]
    public sealed class CampusObservedIncidentImpactService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private CampusRosterService rosterService;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;

            if (gameplayEventHub != null)
            {
                gameplayEventHub.ItemTheftObserved -= HandleItemTheftObserved;
                gameplayEventHub.ItemTheftObserved += HandleItemTheftObserved;
            }
        }

        private void OnDestroy()
        {
            if (gameplayEventHub != null)
            {
                gameplayEventHub.ItemTheftObserved -= HandleItemTheftObserved;
            }
        }

        private void HandleItemTheftObserved(CampusItemTheftObservedEvent eventData)
        {
            if (bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            bool officialWitness = eventData.ShouldIssueSanction;
            if (IsPlayerActor(eventData.ActorId))
            {
                int suspicion = officialWitness
                    ? eventData.SuspicionAmount
                    : Mathf.Max(1, eventData.SuspicionAmount / 2);
                bootstrap.GameState.AddPlayerSuspicion(suspicion);
            }

            bootstrap.GameState.AddCampusChaos(officialWitness ? 4 : 2);
            bootstrap.GameState.AddCampusOrder(officialWitness ? -3 : -1);
            if (officialWitness)
            {
                bootstrap.GameState.AddTeacherAlertness(4);
            }

            if (bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatProtectedItemObserved(
                    CampusLanguageState.CurrentLanguage,
                    eventData.ItemDisplayName));
            }
        }

        private bool IsPlayerActor(string actorId)
        {
            return rosterService != null &&
                   rosterService.PlayerRuntime != null &&
                   IsSameActor(rosterService.PlayerRuntime.CharacterId, actorId);
        }

        private static bool IsSameActor(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
