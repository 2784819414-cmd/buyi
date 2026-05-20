using System;
using Nting.Storage;
using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    internal sealed class CampusPrankActions
    {
        private readonly CampusPrankService service;

        public CampusPrankActions(CampusPrankService service)
        {
            this.service = service;
        }

        public bool CanExecutePayload(string payload, GameObject actor, out string unavailableReason)
        {
            if (!CampusPrankCatalog.TryGetByPayload(payload, out CampusPrankDefinition definition))
            {
                unavailableReason = CampusPrankTextCatalog.Get(CampusPrankTextId.UnknownFormalPrankPayload);
                return false;
            }

            if (IsPayload(payload, CampusPrankService.PassNotePayload))
            {
                return CanExecuteKnownPayload(ResolvePassNoteUnavailableReason(actor), out unavailableReason);
            }

            if (TryResolveCanteenFood(payload, out _))
            {
                return CanExecuteKnownPayload(ResolveCanteenFoodUnavailableReason(actor), out unavailableReason);
            }

            if (IsPayload(payload, CampusPrankPayloadIds.StealDelivery))
            {
                return CanExecuteKnownPayload(ResolveDeliveryUnavailableReason(actor), out unavailableReason);
            }

            unavailableReason = definition.GetUnsupportedReason(CampusLanguageState.CurrentLanguage);
            return false;
        }

        public bool TryExecutePayload(string payload, GameObject actor)
        {
            if (!CampusPrankCatalog.TryGetByPayload(payload, out CampusPrankDefinition definition))
            {
                service.WriteLog(CampusPrankTextCatalog.Format(CampusPrankTextId.UnknownPrankPayloadLog, payload));
                return false;
            }

            if (IsPayload(payload, CampusPrankService.PassNotePayload))
            {
                return TryExecutePassNote(actor);
            }

            if (TryResolveCanteenFood(payload, out CampusTheftItemSpec foodSpec))
            {
                return TryExecuteCanteenFoodTheft(actor, foodSpec);
            }

            if (IsPayload(payload, CampusPrankPayloadIds.StealDelivery))
            {
                return TryExecuteDeliveryTheft(actor);
            }

            service.WriteLog(CampusPrankTextCatalog.Format(
                CampusPrankTextId.PrankNotWired,
                definition.GetDisplayName(CampusLanguageState.CurrentLanguage)));
            return false;
        }

        private bool TryExecutePassNote(GameObject actor)
        {
            string unavailableReason = ResolvePassNoteUnavailableReason(actor);
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                service.WriteLog(unavailableReason);
                return false;
            }

            if (!PassesCooldown(CampusPrankTextCatalog.Get(CampusPrankTextId.PassNoteActionName)))
            {
                return false;
            }

            CampusCharacterRuntime actorRuntime = service.ResolveActorRuntime(actor);
            CampusGameplayRoom classroom = service.WorldService != null && actorRuntime != null
                ? service.WorldService.FindRoomForRuntime(actorRuntime)
                : null;

            CampusCharacterRuntime targetStudent = FindTargetStudent(actorRuntime, classroom.RoomId);
            if (targetStudent == null || targetStudent.Data == null)
            {
                service.WriteLog(CampusPrankTextCatalog.Get(CampusPrankTextId.NoNearbyStudent));
                return false;
            }

            CampusCharacterRuntime teacherRuntime = FindTeacherInRoom(classroom.RoomId);
            service.GameplayEventHub?.PublishPrankAttempted(new CampusPrankAttemptedEvent(
                CampusPrankType.PassNote,
                actorRuntime.CharacterId,
                targetStudent.CharacterId,
                classroom.RoomId,
                true));

            int reward = ResolvePassNoteReward();
            bool teacherDistracted = service.ClassroomLoopService != null &&
                                     service.ClassroomLoopService.IsTeacherDistractedInRoom(classroom.RoomId);
            bool detected = teacherRuntime != null && RollTeacherDetection(classroom.RoomId);
            bool succeeded = !detected;

            actorRuntime.Data.AddMemory(CampusCharacterMemoryId.PassedNoteToday);
            targetStudent.Data.AddMemory(CampusCharacterMemoryId.ReceivedNoteFromActor);
            if (teacherRuntime != null && teacherRuntime.Data != null)
            {
                teacherRuntime.Data.AddMemory(detected
                    ? CampusCharacterMemoryId.CaughtNotePassing
                    : CampusCharacterMemoryId.SawRestlessClassroom);
            }

            CampusGameBootstrap bootstrap = service.Bootstrap;
            if (succeeded)
            {
                bootstrap.ResourceState.AddDivinePower(reward);
                bootstrap.GameState.AddCampusChaos(4);
                bootstrap.GameState.AddDivineInterest(5);
                bootstrap.GameState.AddTeacherAlertness(1);
                bootstrap.GameState.UnlockShrineRoom();
                string actorName = actorRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage);
                service.WriteLog(teacherDistracted
                    ? CampusPrankTextCatalog.Format(CampusPrankTextId.PassNoteSucceededDistracted, actorName, reward)
                    : CampusPrankTextCatalog.Format(CampusPrankTextId.PassNoteSucceeded, actorName, reward));
            }
            else
            {
                reward = 0;
                bootstrap.GameState.AddCampusChaos(6);
                bootstrap.GameState.AddTeacherAlertness(6);
                bootstrap.GameState.AddDivineInterest(2);
                service.WriteLog(CampusPrankTextCatalog.Get(CampusPrankTextId.TeacherNoticedNote));
            }

            service.GameplayEventHub?.PublishPrankResolved(new CampusPrankResolvedEvent(
                CampusPrankType.PassNote,
                actorRuntime.CharacterId,
                targetStudent.CharacterId,
                classroom.RoomId,
                succeeded,
                detected,
                reward));

            service.MarkPassNoteExecuted();
            return true;
        }

        private bool TryExecuteCanteenFoodTheft(GameObject actor, CampusTheftItemSpec foodSpec)
        {
            string unavailableReason = ResolveCanteenFoodUnavailableReason(actor);
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                service.WriteLog(unavailableReason);
                return false;
            }

            if (!PassesCooldown(foodSpec.DisplayName))
            {
                return false;
            }

            CampusCharacterRuntime actorRuntime = service.ResolveActorRuntime(actor);
            CampusGameplayRoom canteen = service.WorldService.FindRoomForRuntime(actorRuntime);
            CampusCharacterRuntime clerk = service.FindCanteenClerk(canteen.RoomId);
            CampusCanteenClerkState clerkState = service.ResolveCanteenClerkState(canteen, actorRuntime);

            service.GameplayEventHub?.PublishPrankAttempted(new CampusPrankAttemptedEvent(
                CampusPrankType.CanteenFoodTheft,
                actorRuntime.CharacterId,
                clerk != null ? clerk.CharacterId : string.Empty,
                canteen.RoomId,
                service.ScheduleService != null && service.ScheduleService.IsClassSessionNow()));

            CampusCanteenService canteenService = CampusCanteenService.Resolve();
            if (!canteenService.TryStealStockDish(actor, foodSpec.DefinitionId, out string itemError))
            {
                service.WriteLog(itemError);
                return false;
            }

            actorRuntime.Data.AddMemory(CampusCharacterMemoryId.StoleCanteenFood);
            service.WriteLog(CampusPrankTextCatalog.Format(
                CampusPrankTextId.CanteenTheftLog,
                clerkState,
                FormatActorName(actorRuntime),
                foodSpec.DisplayName));
            PublishCanteenResolved(actorRuntime, clerk, canteen, true);
            service.MarkCanteenFoodTheftExecuted();
            return true;
        }

        private bool TryExecuteDeliveryTheft(GameObject actor)
        {
            service.EnsureDeliveryOrder(true);
            string unavailableReason = ResolveDeliveryUnavailableReason(actor);
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                service.WriteLog(unavailableReason);
                return false;
            }

            if (!PassesCooldown(CampusPrankTextCatalog.Get(CampusPrankTextId.DeliveryActionName)))
            {
                return false;
            }

            CampusCharacterRuntime actorRuntime = service.ResolveActorRuntime(actor);
            CampusGameplayRoom outdoorRoom = service.WorldService.FindRoomForRuntime(actorRuntime);
            CampusCharacterRuntime owner = service.RosterService != null
                ? service.RosterService.FindRuntime(service.ActiveDeliveryOwnerId)
                : null;
            CampusTheftItemSpec deliverySpec = CampusTheftItemSpec.CreateDelivery(
                service.ActiveDeliveryItemName,
                service.ActiveDeliveryOwnerId);
            bool detected = service.RollDeliverySuspicion(owner);

            if (!TryGiveStolenItem(actor, deliverySpec, out string itemError))
            {
                service.WriteLog(itemError);
                return false;
            }

            actorRuntime.Data.AddMemory(CampusCharacterMemoryId.DeliveryStolen);
            if (owner != null && owner.Data != null && detected)
            {
                owner.Data.AddMemory(CampusCharacterMemoryId.LostDelivery);
                owner.Data.AddMemory(CampusCharacterMemoryId.ReportedLostDelivery);
                owner.Data.SetState(CampusCharacterState.Nervous);
            }

            service.MarkDeliveryTheftExecuted(detected);
            service.AddSuspicion(
                deliverySpec.SuspicionRisk + (detected ? 16 : 5),
                detected
                    ? CampusPrankTextCatalog.Get(CampusPrankTextId.DeliveryOwnerReportsLossReason)
                    : CampusPrankTextCatalog.Get(CampusPrankTextId.StolenDeliveryReason));
            service.AddDeliveryAlert(detected ? 8 : 4);
            CampusGameBootstrap bootstrap = service.Bootstrap;
            bootstrap.GameState.AddCampusChaos(detected ? 8 : 5);
            bootstrap.GameState.AddCampusOrder(detected ? -4 : -2);
            bootstrap.GameState.AddDivineInterest(5);

            service.GameplayEventHub?.PublishPrankAttempted(new CampusPrankAttemptedEvent(
                CampusPrankType.DeliveryTheft,
                actorRuntime.CharacterId,
                owner != null ? owner.CharacterId : service.ActiveDeliveryOwnerId,
                outdoorRoom != null ? outdoorRoom.RoomId : string.Empty,
                service.ScheduleService != null && service.ScheduleService.IsClassSessionNow()));
            service.GameplayEventHub?.PublishPrankResolved(new CampusPrankResolvedEvent(
                CampusPrankType.DeliveryTheft,
                actorRuntime.CharacterId,
                owner != null ? owner.CharacterId : service.ActiveDeliveryOwnerId,
                outdoorRoom != null ? outdoorRoom.RoomId : string.Empty,
                true,
                false,
                0));

            service.WriteLog(detected
                ? CampusPrankTextCatalog.Format(
                    CampusPrankTextId.DeliveryTakenLikelyReported,
                    FormatActorName(actorRuntime),
                    service.ActiveDeliveryItemName)
                : CampusPrankTextCatalog.Format(
                    CampusPrankTextId.DeliveryTakenBeforeOwner,
                    FormatActorName(actorRuntime),
                    service.ActiveDeliveryItemName));
            return true;
        }

        private string ResolvePassNoteUnavailableReason(GameObject actor)
        {
            CampusCharacterRuntime actorRuntime = service.ResolveActorRuntime(actor);
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.MissingActorRuntime);
            }

            if (service.ScheduleService == null || !service.ScheduleService.IsClassSessionNow())
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.PassNotesOnlyDuringClass);
            }

            CampusGameplayRoom classroom = service.WorldService != null
                ? service.WorldService.FindRoomForRuntime(actorRuntime)
                : null;
            if (classroom == null || classroom.RoomType != CampusRoomType.Classroom)
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.ActorNeedsClassroom);
            }

            return string.Empty;
        }

        private string ResolveCanteenFoodUnavailableReason(GameObject actor)
        {
            CampusCharacterRuntime actorRuntime = service.ResolveActorRuntime(actor);
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.MissingActorRuntime);
            }

            CampusGameplayRoom canteen = service.WorldService != null
                ? service.WorldService.FindRoomForRuntime(actorRuntime)
                : null;
            if (canteen == null || canteen.RoomType != CampusRoomType.Canteen)
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.ActorNeedsCanteen);
            }

            return string.Empty;
        }

        private string ResolveDeliveryUnavailableReason(GameObject actor)
        {
            CampusCharacterRuntime actorRuntime = service.ResolveActorRuntime(actor);
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.MissingActorRuntime);
            }

            service.EnsureDeliveryOrder(false);
            if (service.ActiveDeliveryOrderState != CampusDeliveryOrderState.WaitingPickup ||
                string.IsNullOrWhiteSpace(service.ActiveDeliveryOwnerId))
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.NoDeliveryWaiting);
            }

            CampusGameplayRoom room = service.WorldService != null
                ? service.WorldService.FindRoomForRuntime(actorRuntime)
                : null;
            if (room == null || room.RoomType != CampusRoomType.Outdoor)
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.ActorNeedsOutdoorDelivery);
            }

            if (!CampusPrankService.HasDeliveryDropPoint(room))
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.OutdoorNeedsDeliveryDropPoint);
            }

            return string.Empty;
        }

        private void PublishCanteenResolved(
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime clerk,
            CampusGameplayRoom canteen,
            bool succeeded)
        {
            service.GameplayEventHub?.PublishPrankResolved(new CampusPrankResolvedEvent(
                CampusPrankType.CanteenFoodTheft,
                actorRuntime != null ? actorRuntime.CharacterId : string.Empty,
                clerk != null ? clerk.CharacterId : string.Empty,
                canteen != null ? canteen.RoomId : string.Empty,
                succeeded,
                false,
                0));
        }

        private bool TryGiveStolenItem(GameObject actor, CampusTheftItemSpec spec, out string errorMessage)
        {
            errorMessage = string.Empty;
            StorageMemory memory = StorageMemory.GetOrCreate();
            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            EnsureRuntimeItemDefinition(registry, spec);

            StorageItemModel item = registry.CreateItem(spec.DefinitionId, spec.DefinitionId + "_" + Guid.NewGuid().ToString("N"));
            if (item == null)
            {
                errorMessage = CampusPrankTextCatalog.Format(CampusPrankTextId.FailedCreateStolenItem, spec.DefinitionId);
                return false;
            }

            item.DisplayName = spec.DisplayName;
            item.LocalizedDisplayName = spec.LocalizedDisplayName;
            item.Description = CampusPrankTextCatalog.Format(
                CampusPrankTextId.StolenItemDescription,
                spec.SourceLocation,
                spec.SmellLevel,
                spec.SuspicionRisk);
            item.LocalizedDescription = CampusPrankTextCatalog.Localized(
                CampusPrankTextId.StolenItemDescription,
                spec.SourceLocation,
                spec.SmellLevel,
                spec.SuspicionRisk);
            item.IsUsable = true;
            item.UseActionId = StorageItemUseUtility.ConsumeFoodActionId;
            item.ConsumeOnUse = true;
            item.UseText = CampusPrankTextCatalog.Format(CampusPrankTextId.AteItem, spec.DisplayName);
            item.LocalizedUseText = FormatItemText(CampusPrankTextId.AteItem, spec);
            item.OwnerId = ResolveTheftOwnerId(spec);
            item.SourceLocation = spec.SourceLocation;
            item.SuspicionRisk = spec.SuspicionRisk;
            item.AllowTaking = false;

            StorageTransferContext context = StorageTransferContext.ForActor(actor, StorageTransferReason.PrankTheft);
            context.ForceIllegal = true;
            context.SourceLocation = spec.SourceLocation;
            context.OwnerId = item.OwnerId;
            context.SuspicionRiskOverride = spec.SuspicionRisk;
            CampusInventoryTransferService transferService = CampusInventoryTransferService.Resolve();
            if (transferService.TryPickUpIntoHands(memory, item, context, out StorageTransferResult result))
            {
                return true;
            }

            errorMessage = result.Message;
            return false;
        }

        private static string ResolveTheftOwnerId(CampusTheftItemSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.SourceLocation) &&
                spec.SourceLocation.StartsWith("delivery:", StringComparison.OrdinalIgnoreCase))
            {
                return spec.SourceLocation.Substring("delivery:".Length).Trim();
            }

            return string.IsNullOrWhiteSpace(spec.SourceLocation) ? "campus" : spec.SourceLocation.Trim();
        }

        private static void EnsureRuntimeItemDefinition(StorageItemRegistry registry, CampusTheftItemSpec spec)
        {
            if (registry == null || registry.TryGetDefinition(spec.DefinitionId, out _))
            {
                return;
            }

            StorageItemDefinition definition = ScriptableObject.CreateInstance<StorageItemDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Id = spec.DefinitionId;
            definition.DisplayName = spec.DisplayName;
            definition.LocalizedDisplayName = spec.LocalizedDisplayName;
            definition.Width = spec.Width;
            definition.Height = spec.Height;
            definition.Weight = spec.Weight;
            definition.Description = CampusPrankTextCatalog.Format(CampusPrankTextId.RuntimeStolenItemDescription, spec.SourceLocation);
            definition.LocalizedDescription = CampusPrankTextCatalog.Localized(CampusPrankTextId.RuntimeStolenItemDescription, spec.SourceLocation);
            definition.ThemeColor = spec.ThemeColor;
            definition.IsUsable = true;
            definition.UseActionId = StorageItemUseUtility.ConsumeFoodActionId;
            definition.ConsumeOnUse = true;
            definition.UseText = CampusPrankTextCatalog.Format(CampusPrankTextId.AteItem, spec.DisplayName);
            definition.LocalizedUseText = FormatItemText(CampusPrankTextId.AteItem, spec);
            registry.RegisterRuntimeDefinition(definition);
        }

        private static CampusLocalizedText FormatItemText(CampusPrankTextId id, CampusTheftItemSpec spec)
        {
            return new CampusLocalizedText(
                string.Format(
                    CampusPrankTextCatalog.Get(CampusDisplayLanguage.Chinese, id),
                    spec.LocalizedDisplayName.Get(CampusDisplayLanguage.Chinese, spec.DisplayName)),
                string.Format(
                    CampusPrankTextCatalog.Get(CampusDisplayLanguage.English, id),
                    spec.LocalizedDisplayName.Get(CampusDisplayLanguage.English, spec.DisplayName)));
        }

        private bool PassesCooldown(string actionName)
        {
            if (IsPrankCooldownReady())
            {
                return true;
            }

            service.WriteLog(CampusPrankTextCatalog.Format(CampusPrankTextId.ActionCoolingDown, actionName));
            return false;
        }

        private bool CanExecuteKnownPayload(string resolvedUnavailableReason, out string unavailableReason)
        {
            unavailableReason = resolvedUnavailableReason;
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                return false;
            }

            if (!IsPrankCooldownReady())
            {
                unavailableReason = CampusPrankTextCatalog.Get(CampusPrankTextId.PrankActionCoolingDown);
                return false;
            }

            return true;
        }

        private bool IsPrankCooldownReady()
        {
            return Time.time - service.LastPrankTime >= service.PrankCooldownSeconds;
        }

        private CampusCharacterRuntime FindTargetStudent(CampusCharacterRuntime actorRuntime, string roomId)
        {
            CampusRosterService rosterService = service.RosterService;
            if (rosterService == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Student))
            {
                if (runtime == null || runtime == actorRuntime || runtime.Data == null)
                {
                    continue;
                }

                if (string.Equals(runtime.Data.CurrentRoomId, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private CampusCharacterRuntime FindTeacherInRoom(string roomId)
        {
            CampusRosterService rosterService = service.RosterService;
            if (rosterService == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Teacher))
            {
                if (runtime != null &&
                    runtime.Data != null &&
                    string.Equals(runtime.Data.CurrentRoomId, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private int ResolvePassNoteReward()
        {
            switch (service.DailyPassNoteCount)
            {
                case 0:
                    return service.BasePassNoteReward;
                case 1:
                    return Mathf.Max(1, Mathf.RoundToInt(service.BasePassNoteReward * 0.7f));
                default:
                    return Mathf.Max(1, Mathf.RoundToInt(service.BasePassNoteReward * 0.4f));
            }
        }

        private bool RollTeacherDetection(string roomId)
        {
            CampusGameBootstrap bootstrap = service.Bootstrap;
            int alertness = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.TeacherAlertness : 0;
            float detectionChance = Mathf.Clamp01(0.15f + alertness / 100f * 0.55f);
            if (service.ClassroomLoopService != null && service.ClassroomLoopService.IsTeacherDistractedInRoom(roomId))
            {
                detectionChance *= 0.35f;
            }

            return UnityEngine.Random.value < detectionChance;
        }

        private static string FormatActorName(CampusCharacterRuntime actorRuntime)
        {
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return CampusPrankTextCatalog.Get(CampusPrankTextId.ActorFallback);
            }

            return actorRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage);
        }

        private static bool TryResolveCanteenFood(string payload, out CampusTheftItemSpec spec)
        {
            if (IsPayload(payload, CampusPrankPayloadIds.StealFriedChicken))
            {
                spec = new CampusTheftItemSpec(
                    "canteen_fried_chicken",
                    CampusPrankTextCatalog.Localized(CampusPrankTextId.StolenFriedChicken),
                    "canteen",
                    2,
                    2,
                    0.45f,
                    28,
                    35,
                    new Color(0.78f, 0.46f, 0.24f, 1f));
                return true;
            }

            if (IsPayload(payload, CampusPrankPayloadIds.StealBurger))
            {
                spec = new CampusTheftItemSpec(
                    "canteen_burger",
                    CampusPrankTextCatalog.Localized(CampusPrankTextId.StolenBurger),
                    "canteen",
                    2,
                    2,
                    0.38f,
                    22,
                    26,
                    new Color(0.67f, 0.52f, 0.28f, 1f));
                return true;
            }

            if (IsPayload(payload, CampusPrankPayloadIds.StealOden))
            {
                spec = new CampusTheftItemSpec(
                    "canteen_oden",
                    CampusPrankTextCatalog.Localized(CampusPrankTextId.StolenOdenCup),
                    "canteen",
                    2,
                    2,
                    0.5f,
                    30,
                    42,
                    new Color(0.66f, 0.62f, 0.42f, 1f));
                return true;
            }

            spec = default;
            return false;
        }

        private static bool IsPayload(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private readonly struct CampusTheftItemSpec
        {
            public CampusTheftItemSpec(
                string definitionId,
                CampusLocalizedText localizedDisplayName,
                string sourceLocation,
                int width,
                int height,
                float weight,
                int suspicionRisk,
                int smellLevel,
                Color themeColor)
            {
                DefinitionId = definitionId;
                LocalizedDisplayName = localizedDisplayName;
                DisplayName = localizedDisplayName.ResolvePrimary(definitionId);
                SourceLocation = sourceLocation;
                Width = Mathf.Max(1, width);
                Height = Mathf.Max(1, height);
                Weight = Mathf.Max(0f, weight);
                SuspicionRisk = Mathf.Max(0, suspicionRisk);
                SmellLevel = Mathf.Max(0, smellLevel);
                ThemeColor = themeColor;
            }

            public string DefinitionId { get; }
            public string DisplayName { get; }
            public CampusLocalizedText LocalizedDisplayName { get; }
            public string SourceLocation { get; }
            public int Width { get; }
            public int Height { get; }
            public float Weight { get; }
            public int SuspicionRisk { get; }
            public int SmellLevel { get; }
            public Color ThemeColor { get; }

            public static CampusTheftItemSpec CreateDelivery(string itemName, string ownerId)
            {
                string normalizedItemName = string.IsNullOrWhiteSpace(itemName) ? "delivery" : itemName.Trim();
                return new CampusTheftItemSpec(
                    "stolen_delivery_" + StableId(normalizedItemName),
                    CampusPrankTextCatalog.Localized(CampusPrankTextId.StolenDeliveryItem, normalizedItemName),
                    "delivery:" + ownerId,
                    2,
                    2,
                    0.6f,
                    24,
                    normalizedItemName.Contains("chicken", StringComparison.OrdinalIgnoreCase) ? 38 : 18,
                    new Color(0.54f, 0.42f, 0.30f, 1f));
            }

            private static string StableId(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return "item";
                }

                char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (!char.IsLetterOrDigit(chars[i]))
                    {
                        chars[i] = '_';
                    }
                }

                return new string(chars).Trim('_');
            }
        }
    }
}
