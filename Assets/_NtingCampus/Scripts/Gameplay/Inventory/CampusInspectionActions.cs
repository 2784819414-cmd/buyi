using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    internal sealed class CampusInspectionActions
    {
        private readonly CampusInspectionService service;

        public CampusInspectionActions(CampusInspectionService service)
        {
            this.service = service;
        }

        public bool TryForceQuestioning(CampusCharacterRuntime requestedTarget, out string message)
        {
            if (!service.Facts.TryResolveInspectionTarget(
                    requestedTarget,
                    out CampusCharacterRuntime targetRuntime,
                    out CampusGameplayRoom room,
                    out message))
            {
                return false;
            }

            bool hasContraband = CampusContrabandService.TryFindCarriedContraband(targetRuntime, out _, out _);
            service.Facts.TryFindBestInspector(room, targetRuntime, false, out CampusCharacterRuntime questioner);
            int pressure = service.Facts.ResolveQuestioningPressure(room, questioner, targetRuntime, hasContraband);
            service.SetQuestioningCooldown(service.QuestioningCooldownSeconds);
            HandleQuestioning(targetRuntime, questioner, room, pressure, false);
            message = CampusInspectionTextCatalog.Format(
                CampusInspectionTextId.ForcedQuestioning,
                questioner != null
                    ? CampusInspectionService.ResolveRuntimeName(questioner)
                    : CampusInspectionTextCatalog.Get(CampusInspectionTextId.DebugInspector),
                pressure);
            return true;
        }

        public bool TryForceSearch(CampusCharacterRuntime requestedTarget, out string message)
        {
            if (!service.Facts.TryResolveInspectionTarget(
                    requestedTarget,
                    out CampusCharacterRuntime targetRuntime,
                    out CampusGameplayRoom room,
                    out message))
            {
                return false;
            }

            bool hasContraband = CampusContrabandService.TryFindCarriedContraband(
                targetRuntime,
                out StorageItemModel contrabandItem,
                out StorageContainerModel contrabandContainer);
            service.Facts.TryFindBestInspector(room, targetRuntime, true, out CampusCharacterRuntime inspector);
            int pressure = service.Facts.ResolveSearchPressure(room, inspector, targetRuntime, contrabandItem);
            service.SetSearchCooldown(service.SearchCooldownSeconds);
            service.HoldQuestioningCooldown(5f);
            HandleSearch(targetRuntime, inspector, room, contrabandItem, contrabandContainer, pressure);
            message = hasContraband
                ? CampusInspectionTextCatalog.Format(
                    CampusInspectionTextId.ForcedSearchFound,
                    CampusInspectionService.ResolveItemName(contrabandItem))
                : CampusInspectionTextCatalog.Get(CampusInspectionTextId.ForcedSearchNoContraband);
            return true;
        }

        public bool TryBuildNpcProactiveOpportunity(
            CampusCharacterRuntime npcRuntime,
            CampusCharacterRuntime requestedTarget,
            out CampusInspectionNpcOpportunity opportunity)
        {
            opportunity = default;
            if (!TryResolveNpcProactiveContext(
                    npcRuntime,
                    requestedTarget,
                    out CampusCharacterRuntime targetRuntime,
                    out CampusGameplayRoom room,
                    out bool hasContraband,
                    out StorageItemModel contrabandItem,
                    out _,
                    out bool authority,
                    out bool tattletale,
                    out _))
            {
                return false;
            }

            if (authority)
            {
                int searchPressure = service.Facts.ResolveSearchPressure(room, npcRuntime, targetRuntime, contrabandItem);
                if (hasContraband && searchPressure >= 45)
                {
                    opportunity = CampusInspectionNpcOpportunity.Search(
                        targetRuntime,
                        room,
                        70f + searchPressure * 0.35f);
                    return true;
                }

                int questioningPressure = service.Facts.ResolveQuestioningPressure(
                    room,
                    npcRuntime,
                    targetRuntime,
                    hasContraband);
                if (questioningPressure >= 34)
                {
                    opportunity = CampusInspectionNpcOpportunity.Question(
                        targetRuntime,
                        room,
                        45f + questioningPressure * 0.3f);
                    return true;
                }
            }

            int reportPressure = ResolveNpcProactivePressure(npcRuntime, room, targetRuntime, hasContraband);
            if (tattletale && reportPressure >= 42)
            {
                opportunity = CampusInspectionNpcOpportunity.Report(
                    targetRuntime,
                    room,
                    42f + reportPressure * 0.25f);
                return true;
            }

            return false;
        }

        public bool TryNpcProactiveInspection(
            CampusCharacterRuntime npcRuntime,
            CampusCharacterRuntime requestedTarget,
            out string line)
        {
            line = string.Empty;
            if (!TryResolveNpcProactiveContext(
                    npcRuntime,
                    requestedTarget,
                    out CampusCharacterRuntime targetRuntime,
                    out CampusGameplayRoom room,
                    out bool hasContraband,
                    out StorageItemModel contrabandItem,
                    out StorageContainerModel contrabandContainer,
                    out bool authority,
                    out bool tattletale,
                    out string npcId))
            {
                return false;
            }

            if (authority)
            {
                int searchPressure = service.Facts.ResolveSearchPressure(room, npcRuntime, targetRuntime, contrabandItem);
                if (hasContraband && searchPressure >= 45 && Roll(searchPressure))
                {
                    service.SetNpcProactiveCooldown(npcId, Mathf.Max(12f, service.SearchCooldownSeconds));
                    service.HoldSearchCooldown(Mathf.Min(6f, service.SearchCooldownSeconds));
                    service.MarkProactiveInspection();
                    HandleSearch(targetRuntime, npcRuntime, room, contrabandItem, contrabandContainer, searchPressure);
                    line = CampusInspectionTextCatalog.Get(CampusInspectionTextId.OpenYourBagLine);
                    return true;
                }

                int questioningPressure = service.Facts.ResolveQuestioningPressure(
                    room,
                    npcRuntime,
                    targetRuntime,
                    hasContraband);
                if (questioningPressure >= 34 && Roll(questioningPressure))
                {
                    service.SetNpcProactiveCooldown(npcId, Mathf.Max(10f, service.QuestioningCooldownSeconds));
                    service.HoldQuestioningCooldown(Mathf.Min(5f, service.QuestioningCooldownSeconds));
                    service.MarkProactiveInspection();
                    HandleQuestioning(targetRuntime, npcRuntime, room, questioningPressure, false);
                    line = CampusInspectionTextCatalog.Get(CampusInspectionTextId.ShowCarryingLine);
                    return true;
                }
            }

            if (tattletale &&
                ResolveNpcProactivePressure(npcRuntime, room, targetRuntime, hasContraband) >= 42 &&
                Roll(hasContraband ? 72 : 42))
            {
                service.SetNpcProactiveCooldown(npcId, 24f);
                service.MarkProactiveInspection();
                HandleTattletaleReport(npcRuntime, targetRuntime, room, hasContraband);
                line = hasContraband
                    ? CampusInspectionTextCatalog.Get(CampusInspectionTextId.TattletaleContrabandLine)
                    : CampusInspectionTextCatalog.Get(CampusInspectionTextId.TattletaleSuspiciousLine);
                return true;
            }

            return false;
        }

        private bool TryResolveNpcProactiveContext(
            CampusCharacterRuntime npcRuntime,
            CampusCharacterRuntime requestedTarget,
            out CampusCharacterRuntime targetRuntime,
            out CampusGameplayRoom room,
            out bool hasContraband,
            out StorageItemModel contrabandItem,
            out StorageContainerModel contrabandContainer,
            out bool authority,
            out bool tattletale,
            out string npcId)
        {
            targetRuntime = null;
            room = null;
            hasContraband = false;
            contrabandItem = null;
            contrabandContainer = null;
            authority = false;
            tattletale = false;
            npcId = string.Empty;

            if (!service.Facts.TryResolveInspectionTarget(requestedTarget, out targetRuntime, out room, out _) ||
                npcRuntime == null ||
                npcRuntime.Data == null ||
                npcRuntime.Data.IsPlayerControlled ||
                string.Equals(npcRuntime.CharacterId, targetRuntime.CharacterId, System.StringComparison.OrdinalIgnoreCase) ||
                !service.Facts.IsRuntimeInRoom(npcRuntime, room))
            {
                return false;
            }

            npcId = CampusInspectionService.ResolveRuntimeId(npcRuntime);
            if (!service.IsNpcProactiveCooldownReady(npcId))
            {
                return false;
            }

            float distance = Vector2.Distance(npcRuntime.transform.position, targetRuntime.transform.position);
            if (distance > Mathf.Min(service.MaxInspectionDistance, 1.8f))
            {
                return false;
            }

            hasContraband = CampusContrabandService.TryFindCarriedContraband(
                targetRuntime,
                out contrabandItem,
                out contrabandContainer);
            authority = CampusInspectionFacts.IsAuthority(npcRuntime);
            tattletale = npcRuntime.Data.HasTrait(CampusCharacterTrait.Tattletale);
            return authority || tattletale;
        }

        private void HandleSearch(
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime,
            CampusGameplayRoom room,
            StorageItemModel contrabandItem,
            StorageContainerModel contrabandContainer,
            int pressure)
        {
            service.MarkSearch();
            bool foundContraband = contrabandItem != null;
            HandleQuestioning(actorRuntime, inspectorRuntime, room, pressure, foundContraband);
            if (!foundContraband)
            {
                string summary = CampusInspectionTextCatalog.Format(CampusInspectionTextId.SearchFoundNothing, pressure);
                service.SetInspectionSummary(summary);
                service.WriteInspectionLog(CampusInspectionTextCatalog.Format(CampusInspectionTextId.InspectionLogLine, summary));
                return;
            }

            ApplyContrabandConsequences(actorRuntime, inspectorRuntime, room, contrabandItem, pressure);
            bool confiscated = TryConfiscateContraband(
                contrabandItem,
                contrabandContainer,
                room,
                actorRuntime,
                inspectorRuntime,
                out string confiscationMessage);
            if (!confiscated)
            {
                service.WriteInspectionLog(CampusInspectionTextCatalog.Format(
                    CampusInspectionTextId.InspectionLogLine,
                    CampusInspectionTextCatalog.Format(CampusInspectionTextId.ConfiscationFailed, confiscationMessage)));
            }

            service.GameplayEventHub?.PublishContrabandFound(new CampusContrabandFoundEvent(
                CampusInspectionService.ResolveRuntimeId(actorRuntime),
                CampusInspectionService.ResolveRuntimeId(inspectorRuntime),
                room != null ? room.RoomId : string.Empty,
                CampusInspectionService.ResolveInstanceId(contrabandItem),
                contrabandItem.DefinitionId,
                CampusInspectionService.ResolveItemName(contrabandItem),
                contrabandContainer != null ? contrabandContainer.Id : string.Empty,
                pressure,
                CampusInspectionFacts.IsAuthority(inspectorRuntime)));
        }

        private void HandleQuestioning(
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime,
            CampusGameplayRoom room,
            int pressure,
            bool foundContraband)
        {
            service.MarkQuestioning();
            string actorId = CampusInspectionService.ResolveRuntimeId(actorRuntime);
            string inspectorId = CampusInspectionService.ResolveRuntimeId(inspectorRuntime);
            service.GameplayEventHub?.PublishInventoryQuestioned(new CampusInventoryQuestionedEvent(
                actorId,
                inspectorId,
                room != null ? room.RoomId : string.Empty,
                pressure,
                foundContraband));

            if (actorRuntime != null && actorRuntime.Data != null)
            {
                actorRuntime.Data.SetState(CampusCharacterState.Nervous);
            }

            if (inspectorRuntime != null && inspectorRuntime.Data != null && !string.IsNullOrWhiteSpace(actorId))
            {
                inspectorRuntime.Data.AddRelationshipSuspicion(actorId, foundContraband ? 8 : 2);
                inspectorRuntime.Data.AddMood(foundContraband ? -2 : -1);
            }

            if (!foundContraband)
            {
                int suspicionNudge = Mathf.Clamp(pressure / 45, 0, 2);
                if (suspicionNudge > 0)
                {
                    service.AddPlayerSuspicionIfTargetIsPlayer(actorRuntime, suspicionNudge);
                }
            }

            string summary = CampusInspectionTextCatalog.Format(
                CampusInspectionTextId.QuestioningSummary,
                string.IsNullOrWhiteSpace(inspectorId)
                    ? CampusInspectionTextCatalog.Get(CampusInspectionTextId.DebugInspector)
                    : inspectorId,
                actorId,
                pressure,
                foundContraband
                    ? CampusGameplayDebugTextCatalog.Get(CampusLanguageState.CurrentLanguage, CampusGameplayDebugTextId.Yes)
                    : CampusGameplayDebugTextCatalog.Get(CampusLanguageState.CurrentLanguage, CampusGameplayDebugTextId.No));
            service.SetInspectionSummary(summary);
            service.WriteInspectionLog(CampusInspectionTextCatalog.Format(CampusInspectionTextId.InspectionLogLine, summary));
        }

        private void ApplyContrabandConsequences(
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime,
            CampusGameplayRoom room,
            StorageItemModel contrabandItem,
            int pressure)
        {
            CampusGameBootstrap bootstrap = service.Bootstrap;
            if (bootstrap != null && bootstrap.GameState != null)
            {
                int suspicion = Mathf.Clamp(10 + pressure / 5 + Mathf.Max(0, contrabandItem.SuspicionRisk), 8, 40);
                service.AddPlayerSuspicionIfTargetIsPlayer(actorRuntime, suspicion);
                bootstrap.GameState.AddTeacherAlertness(Mathf.Clamp(3 + pressure / 25, 3, 7));
                bootstrap.GameState.AddCampusChaos(3);
                bootstrap.GameState.AddCampusOrder(-4);
            }

            if (actorRuntime != null && actorRuntime.Data != null)
            {
                CampusInspectionService.AddMemoryIfMissing(actorRuntime.Data, CampusCharacterMemoryId.FoundContraband);
            }

            if (inspectorRuntime != null && inspectorRuntime.Data != null)
            {
                CampusInspectionService.AddMemoryIfMissing(inspectorRuntime.Data, CampusCharacterMemoryId.FoundContraband);
            }

            service.MarkContrabandFound();
            string summary = CampusInspectionTextCatalog.Format(
                CampusInspectionTextId.ContrabandFoundSummary,
                CampusInspectionService.ResolveItemName(contrabandItem),
                room != null ? room.RoomId : CampusInspectionTextCatalog.Get(CampusInspectionTextId.UnknownRoom),
                pressure);
            service.SetInspectionSummary(summary);
            service.WriteInspectionLog(CampusInspectionTextCatalog.Format(CampusInspectionTextId.InspectionLogLine, summary));
        }

        private bool TryConfiscateContraband(
            StorageItemModel item,
            StorageContainerModel source,
            CampusGameplayRoom room,
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime,
            out string message)
        {
            message = string.Empty;
            if (item == null)
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.MissingItem);
                return false;
            }

            if (source == null)
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.MissingSourceContainer);
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.StorageMemoryUnavailable);
                return false;
            }

            StorageContainerModel evidenceContainer = CampusContrabandService.GetOrCreateConfiscatedContainer(memory, room);
            if (evidenceContainer == null)
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.CouldNotCreateConfiscatedContainer);
                return false;
            }

            if (!CampusContrabandService.TryFindConfiscationSpace(evidenceContainer, item, out Vector2Int targetPosition))
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.NoEvidenceStorageSpace);
                return false;
            }

            if (!StorageTransferService.TryMove(
                    item,
                    source,
                    evidenceContainer,
                    targetPosition.x,
                    targetPosition.y,
                    out _,
                    out string moveError))
            {
                message = moveError;
                return false;
            }

            item.AllowTaking = false;
            if (item.LegalState == StorageItemLegalState.Unknown || item.LegalState == StorageItemLegalState.Personal)
            {
                item.LegalState = StorageItemLegalState.Suspicious;
            }

            PublishConfiscationTransfer(item, source, evidenceContainer, room, actorRuntime, inspectorRuntime);
            message = CampusInspectionTextCatalog.Format(
                CampusInspectionTextId.ConfiscatedToContainer,
                CampusInspectionService.ResolveItemName(item),
                evidenceContainer.Id);
            service.MarkConfiscatedItem();
            service.SetInspectionSummary(message);
            service.WriteInspectionLog(CampusInspectionTextCatalog.Format(CampusInspectionTextId.InspectionLogLine, message));
            return true;
        }

        private void PublishConfiscationTransfer(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel evidenceContainer,
            CampusGameplayRoom room,
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime)
        {
            if (service.GameplayEventHub == null || item == null || evidenceContainer == null)
            {
                return;
            }

            service.GameplayEventHub.PublishItemTransferred(new CampusItemTransferredEvent(
                CampusInspectionService.ResolveRuntimeId(actorRuntime),
                CampusInspectionService.ResolveInstanceId(item),
                item.DefinitionId,
                CampusInspectionService.ResolveItemName(item),
                source != null ? source.Id : string.Empty,
                evidenceContainer.Id,
                room != null ? room.RoomId : string.Empty,
                StorageTransferReason.InspectionConfiscation,
                false,
                inspectorRuntime != null));
        }

        private int ResolveNpcProactivePressure(
            CampusCharacterRuntime npcRuntime,
            CampusGameplayRoom targetRoom,
            CampusCharacterRuntime targetRuntime,
            bool hasContraband)
        {
            if (npcRuntime == null || npcRuntime.Data == null)
            {
                return 0;
            }

            int pressure = service.Facts.ResolveNpcVigilancePressure(npcRuntime).Value;
            pressure += Mathf.RoundToInt((service.Facts.ResolveAreaSearchPressure(targetRoom).Value +
                                          service.Facts.ResolveAreaQuestioningPressure(targetRoom).Value) * 0.35f);
            CampusGameBootstrap bootstrap = service.Bootstrap;
            if (bootstrap != null && bootstrap.GameState != null)
            {
                pressure += Mathf.RoundToInt(bootstrap.GameState.TeacherAlertness * 0.25f);
            }

            pressure += Mathf.RoundToInt(service.Facts.ResolveTargetSuspicion(npcRuntime, targetRuntime) * 0.45f);

            string targetId = CampusInspectionService.ResolveRuntimeId(targetRuntime);
            if (targetRuntime != null &&
                targetRuntime.Data != null &&
                targetRuntime.Data.IsPlayerControlled &&
                !string.IsNullOrWhiteSpace(targetId))
            {
                pressure += Mathf.RoundToInt(npcRuntime.Data.GetRelationshipSuspicion(targetId) * 0.35f);
            }

            if (hasContraband)
            {
                pressure += 22;
            }

            if (npcRuntime.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                pressure += 8;
            }

            return Mathf.Clamp(pressure, 0, 100);
        }

        private void HandleTattletaleReport(
            CampusCharacterRuntime reporterRuntime,
            CampusCharacterRuntime targetRuntime,
            CampusGameplayRoom room,
            bool hasContraband)
        {
            string reporterId = CampusInspectionService.ResolveRuntimeId(reporterRuntime);
            string targetId = CampusInspectionService.ResolveRuntimeId(targetRuntime);
            service.MarkTattletaleReport();
            if (reporterRuntime != null && reporterRuntime.Data != null && !string.IsNullOrWhiteSpace(targetId))
            {
                reporterRuntime.Data.AddRelationshipSuspicion(targetId, hasContraband ? 12 : 7);
                reporterRuntime.Data.AddRelationshipTrust(targetId, -3);
                CampusInspectionService.AddMemoryIfMissing(reporterRuntime.Data, CampusCharacterMemoryId.WarnedAboutActor);
            }

            CampusGameBootstrap bootstrap = service.Bootstrap;
            if (bootstrap != null && bootstrap.GameState != null)
            {
                bootstrap.GameState.AddTeacherAlertness(hasContraband ? 5 : 3);
                service.AddPlayerSuspicionIfTargetIsPlayer(targetRuntime, hasContraband ? 4 : 2);
                bootstrap.GameState.AddCampusChaos(1);
            }

            string summary = CampusInspectionTextCatalog.Format(
                hasContraband ? CampusInspectionTextId.ReportedForContraband : CampusInspectionTextId.ReportedAsSuspicious,
                reporterId,
                targetId,
                room != null ? room.RoomId : "-");
            service.SetInspectionSummary(summary);
            service.WriteInspectionLog(CampusInspectionTextCatalog.Format(CampusInspectionTextId.InspectionLogLine, summary));
        }

        private static bool Roll(int pressure)
        {
            return pressure > 0 && UnityEngine.Random.value <= Mathf.Clamp01(pressure / 100f);
        }
    }
}
