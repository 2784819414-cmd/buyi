using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using NtingCampus.UI.Runtime.Gameplay;
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

    public readonly struct CampusSanctionRequest
    {
        public CampusSanctionRequest(
            CampusCharacterRuntime actorRuntime,
            string roomId,
            CampusSanctionLevel level,
            string reasonLog)
        {
            ActorRuntime = actorRuntime;
            RoomId = roomId ?? string.Empty;
            Level = level;
            ReasonLog = reasonLog ?? string.Empty;
        }

        public CampusCharacterRuntime ActorRuntime { get; }
        public string RoomId { get; }
        public CampusSanctionLevel Level { get; }
        public string ReasonLog { get; }
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
                gameplayEventHub.ActorSkipClass -= HandleActorSkipClass;
                gameplayEventHub.ActorSkipClass += HandleActorSkipClass;
            }
        }

        private void OnDestroy()
        {
            if (gameplayEventHub != null)
            {
                gameplayEventHub.ActorSkipClass -= HandleActorSkipClass;
            }
        }

        private void HandleActorSkipClass(CampusActorSkipClassEvent eventData)
        {
            if (!eventData.DetectedByTeacher || bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            CampusCharacterRuntime actorRuntime = rosterService != null ? rosterService.FindRuntime(eventData.ActorId) : null;
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return;
            }

            IssueDetectedRuleBreak(
                actorRuntime,
                eventData.FromRoomId,
                CampusCharacterTextCatalog.FormatSanctionReason(
                    CampusLanguageState.CurrentLanguage,
                    CampusSanctionReasonId.SkippingClassObserved));
        }

        public void IssueSanction(CampusSanctionRequest request)
        {
            if (request.ActorRuntime == null ||
                request.ActorRuntime.Data == null ||
                request.Level == CampusSanctionLevel.None ||
                bootstrap == null ||
                bootstrap.GameState == null)
            {
                return;
            }

            IssueDetectedRuleBreak(request.ActorRuntime, request.RoomId, request.ReasonLog, request.Level);
        }

        private void IssueDetectedRuleBreak(CampusCharacterRuntime actorRuntime, string roomId, string prefaceLog)
        {
            IssueDetectedRuleBreak(actorRuntime, roomId, prefaceLog, CampusSanctionLevel.None);
        }

        private void IssueDetectedRuleBreak(
            CampusCharacterRuntime actorRuntime,
            string roomId,
            string prefaceLog,
            CampusSanctionLevel requestedLevel)
        {
            if (actorRuntime == null || actorRuntime.Data == null || bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(prefaceLog) && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(prefaceLog);
            }

            int warningCount = bootstrap.GameState.DailyWarningCount + 1;
            bootstrap.GameState.SetDailyWarningCount(warningCount);
            bootstrap.GameState.AddTeacherAlertness(4);
            bootstrap.GameState.AddCampusOrder(-3);

            CampusSanctionLevel level = requestedLevel == CampusSanctionLevel.None
                ? ResolveLevel(warningCount)
                : requestedLevel;
            lastIssuedLevel = level;
            ApplySanctionState(actorRuntime, level);

            gameplayEventHub?.PublishSanctionIssued(new CampusSanctionIssuedEvent(
                actorRuntime.CharacterId,
                roomId,
                level,
                warningCount));

            WriteSanctionLog(actorRuntime, level, warningCount);
        }

        private void ApplySanctionState(CampusCharacterRuntime actorRuntime, CampusSanctionLevel level)
        {
            switch (level)
            {
                case CampusSanctionLevel.Warning:
                    CampusCharacterEffects.TrySetState(
                        actorRuntime,
                        CampusCharacterState.Nervous,
                        CampusCharacterEffectSource.Sanction);
                    break;
                case CampusSanctionLevel.Reprimand:
                    CampusCharacterEffects.TrySetState(
                        actorRuntime,
                        CampusCharacterState.Reprimanded,
                        CampusCharacterEffectSource.Sanction);
                    break;
                case CampusSanctionLevel.OfficePunishment:
                    CampusCharacterEffects.TrySetState(
                        actorRuntime,
                        CampusCharacterState.Punished,
                        CampusCharacterEffectSource.Sanction);
                    MoveActorToOffice(actorRuntime);
                    break;
                default:
                    CampusCharacterEffects.TrySetState(
                        actorRuntime,
                        CampusCharacterState.Normal,
                        CampusCharacterEffectSource.Sanction);
                    break;
            }
        }

        private void MoveActorToOffice(CampusCharacterRuntime actorRuntime)
        {
            CampusGameplayRoom officeRoom = worldService != null ? worldService.FindFirstUsableRoom(CampusRoomType.Office) : null;
            if (officeRoom == null)
            {
                return;
            }

            CampusCharacterEffects.TryMoveToRoom(
                actorRuntime,
                officeRoom,
                new Vector3(-0.25f, -0.15f, 0f),
                CampusCharacterEffectSource.Sanction);
        }

        private void WriteSanctionLog(CampusCharacterRuntime actorRuntime, CampusSanctionLevel level, int warningCount)
        {
            if (bootstrap == null || bootstrap.EventLog == null || actorRuntime == null || actorRuntime.Data == null)
            {
                return;
            }

            switch (level)
            {
                case CampusSanctionLevel.Warning:
                    bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatSanctionIssued(
                        CampusLanguageState.CurrentLanguage,
                        actorRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage),
                        level));
                    break;
                case CampusSanctionLevel.Reprimand:
                    bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatSanctionIssued(
                        CampusLanguageState.CurrentLanguage,
                        actorRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage),
                        level));
                    break;
                case CampusSanctionLevel.OfficePunishment:
                    bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatSanctionIssued(
                        CampusLanguageState.CurrentLanguage,
                        actorRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage),
                        level));
                    break;
            }

            bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatDailyWarnings(
                CampusLanguageState.CurrentLanguage,
                warningCount));
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

