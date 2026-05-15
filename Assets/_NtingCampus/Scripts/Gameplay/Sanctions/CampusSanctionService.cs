using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using UnityEngine;

namespace NtingCampus.Gameplay.Sanctions
{
    public enum CampusSanctionLevel
    {
        None = 0,
        Warning = 1,
        Reprimand = 2,
        OfficePunishment = 3
    }

    [DisallowMultipleComponent]
    public sealed class CampusSanctionService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private CampusSanctionLevel lastIssuedLevel = CampusSanctionLevel.None;

        public CampusSanctionLevel LastIssuedLevel => lastIssuedLevel;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            scheduleService = bootstrap != null ? bootstrap.ScheduleService : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;

            if (gameplayEventHub != null)
            {
                gameplayEventHub.PrankResolved -= HandlePrankResolved;
                gameplayEventHub.PrankResolved += HandlePrankResolved;
            }
        }

        private void OnDestroy()
        {
            if (gameplayEventHub != null)
            {
                gameplayEventHub.PrankResolved -= HandlePrankResolved;
            }
        }

        private void HandlePrankResolved(CampusPrankResolvedEvent eventData)
        {
            if (!eventData.DetectedByTeacher || bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            CampusCharacterRuntime playerRuntime = rosterService != null ? rosterService.FindRuntime(eventData.ActorId) : null;
            if (playerRuntime == null || playerRuntime.Data == null)
            {
                return;
            }

            int warningCount = bootstrap.GameState.DailyWarningCount + 1;
            bootstrap.GameState.SetDailyWarningCount(warningCount);
            bootstrap.GameState.AddTeacherAlertness(4);
            bootstrap.GameState.AddCampusOrder(-3);

            CampusSanctionLevel level = ResolveLevel(warningCount);
            lastIssuedLevel = level;
            ApplySanctionState(playerRuntime, level);

            gameplayEventHub?.PublishSanctionIssued(new CampusSanctionIssuedEvent(
                playerRuntime.CharacterId,
                eventData.RoomId,
                level,
                warningCount));

            WriteSanctionLog(playerRuntime, level, warningCount);
        }

        private void ApplySanctionState(CampusCharacterRuntime playerRuntime, CampusSanctionLevel level)
        {
            switch (level)
            {
                case CampusSanctionLevel.Warning:
                    playerRuntime.Data.SetState(CampusCharacterState.Nervous);
                    break;
                case CampusSanctionLevel.Reprimand:
                    playerRuntime.Data.SetState(CampusCharacterState.Reprimanded);
                    break;
                case CampusSanctionLevel.OfficePunishment:
                    playerRuntime.Data.SetState(CampusCharacterState.Punished);
                    MovePlayerToOffice(playerRuntime);
                    break;
                default:
                    playerRuntime.Data.SetState(CampusCharacterState.Normal);
                    break;
            }
        }

        private void MovePlayerToOffice(CampusCharacterRuntime playerRuntime)
        {
            CampusGameplayRoom officeRoom = worldService != null ? worldService.FindFirstUsableRoom(CampusRoomType.Office) : null;
            if (officeRoom == null)
            {
                return;
            }

            playerRuntime.transform.position = officeRoom.WorldCenter + new Vector3(-0.25f, -0.15f, 0f);
            playerRuntime.Data.SetCurrentRoom(officeRoom.RoomId);
        }

        private void WriteSanctionLog(CampusCharacterRuntime playerRuntime, CampusSanctionLevel level, int warningCount)
        {
            if (bootstrap == null || bootstrap.EventLog == null || playerRuntime == null || playerRuntime.Data == null)
            {
                return;
            }

            switch (level)
            {
                case CampusSanctionLevel.Warning:
                    bootstrap.EventLog.AddLog("[Sanction] " + playerRuntime.Data.DisplayName + " received a verbal warning.");
                    break;
                case CampusSanctionLevel.Reprimand:
                    bootstrap.EventLog.AddLog("[Sanction] " + playerRuntime.Data.DisplayName + " was reprimanded in class.");
                    break;
                case CampusSanctionLevel.OfficePunishment:
                    bootstrap.EventLog.AddLog("[Sanction] " + playerRuntime.Data.DisplayName + " was sent to the office.");
                    break;
            }

            bootstrap.EventLog.AddLog("[Sanction] Daily warnings = " + warningCount + ".");
        }

        private static CampusSanctionLevel ResolveLevel(int warningCount)
        {
            if (warningCount <= 1)
            {
                return CampusSanctionLevel.Warning;
            }

            if (warningCount == 2)
            {
                return CampusSanctionLevel.Reprimand;
            }

            return CampusSanctionLevel.OfficePunishment;
        }
    }
}
