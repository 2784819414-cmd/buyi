using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Delivery;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Retail;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public readonly struct CampusCharacterActionContext
    {
        public CampusCharacterActionContext(
            CampusCharacterRuntime actor,
            string actionId,
            string payload,
            UnityEngine.Object target)
        {
            Actor = actor;
            ActionId = string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
            Payload = payload ?? string.Empty;
            Target = target;
        }

        public CampusCharacterRuntime Actor { get; }
        public string ActionId { get; }
        public string Payload { get; }
        public UnityEngine.Object Target { get; }
    }

    public static class CampusCharacterActionUtility
    {
        public static CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            return actor != null
                ? actor.GetComponentInParent<CampusCharacterRuntime>()
                : null;
        }

        public static bool TryResolveActorRuntime(GameObject actor, out CampusCharacterRuntime runtime)
        {
            runtime = ResolveActorRuntime(actor);
            return runtime != null;
        }

        public static bool IdEquals(string actionId, string expected)
        {
            return string.Equals(
                NormalizeId(actionId),
                NormalizeId(expected),
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IdIsAny(string actionId, params string[] expectedIds)
        {
            if (expectedIds == null)
            {
                return false;
            }

            for (int i = 0; i < expectedIds.Length; i++)
            {
                if (IdEquals(actionId, expectedIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static StorageTransferResult Result(bool succeeded, string message)
        {
            return succeeded
                ? Success(message)
                : StorageTransferResult.Fail(message ?? string.Empty);
        }

        public static StorageTransferResult Success(string message = null)
        {
            return new StorageTransferResult(true, false, false, message ?? string.Empty, string.Empty);
        }

        public static bool TryResolveComponentTarget<T>(UnityEngine.Object target, out T component)
            where T : UnityEngine.Component
        {
            component = null;
            if (target is T direct)
            {
                component = direct;
                return true;
            }

            if (target is GameObject gameObject)
            {
                component = gameObject.GetComponent<T>();
                return component != null;
            }

            if (target is UnityEngine.Component sourceComponent)
            {
                component = sourceComponent.GetComponent<T>();
                return component != null;
            }

            return false;
        }

        public static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public interface ICampusCharacterActionProvider
    {
        string ProviderId { get; }
        bool TryExecute(CampusCharacterActionContext context, out StorageTransferResult result);
    }

    public static class CampusCharacterActionRegistry
    {
        private static readonly List<ICampusCharacterActionProvider> Providers =
            new List<ICampusCharacterActionProvider>();

        private static bool builtInsRegistered;

        public static void Register(ICampusCharacterActionProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            string providerId = provider.ProviderId ?? string.Empty;
            bool hasProviderId = !string.IsNullOrWhiteSpace(providerId);
            for (int i = 0; i < Providers.Count; i++)
            {
                ICampusCharacterActionProvider existing = Providers[i];
                if (ReferenceEquals(existing, provider) ||
                    hasProviderId &&
                    string.Equals(existing.ProviderId ?? string.Empty, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    Providers[i] = provider;
                    return;
                }
            }

            Providers.Add(provider);
        }

        public static void Unregister(ICampusCharacterActionProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            Providers.Remove(provider);
        }

        public static bool TryExecute(CampusCharacterActionContext context, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (context.Actor == null || string.IsNullOrWhiteSpace(context.ActionId))
            {
                return false;
            }

            EnsureBuiltInsRegistered();
            for (int i = 0; i < Providers.Count; i++)
            {
                ICampusCharacterActionProvider provider = Providers[i];
                if (provider != null && provider.TryExecute(context, out result))
                {
                    return result.Succeeded;
                }
            }

            result = StorageTransferResult.Fail(string.Empty);
            return false;
        }

        private static void EnsureBuiltInsRegistered()
        {
            if (builtInsRegistered)
            {
                return;
            }

            builtInsRegistered = true;
            CampusBuiltInCharacterActionProviders.Install();
        }
    }

    internal static class CampusBuiltInCharacterActionProviders
    {
        public static void Install()
        {
            CampusCharacterActionRegistry.Register(CampusRetailCharacterActionProvider.Instance);
            CampusCharacterActionRegistry.Register(CampusProtectedTransferClearanceCharacterActionProvider.Instance);
            CampusCharacterActionRegistry.Register(CampusDeliveryCharacterActionProvider.Instance);
            CampusCharacterActionRegistry.Register(CampusConfiguredCharacterActionProvider.Instance);
        }
    }

    [Serializable]
    public sealed class CampusConfiguredActionPayload
    {
        public string Mode = string.Empty;
        public string ItemDefinitionId = string.Empty;
        [Min(1)] public int Count = 1;
        public string EventKind = string.Empty;
        public string TargetObjectId = string.Empty;
        public string TargetRoomId = string.Empty;
        public string RoomId = string.Empty;
        public string SourceLocation = string.Empty;
        public string OwnerId = string.Empty;
        public bool ForceIllegal;
        public bool AllowProtectedTake;
        public bool SuppressNpcDetection;
        public int SuspicionRiskOverride = -1;
        public int PlayerSuspicionDelta;
        public int PlayerTheftEvidenceDelta;
        public int PlayerTheftRecordDelta;
        public int CampusRumorDelta;
        public int CampusCrackdownDelta;
        public int CampusChaosDelta;
        public int CampusOrderDelta;
        public int TeacherAlertnessDelta;
        public int AreaAlertDelta;
        public int AreaBagCheckDelta;
        public int AreaPatrolDelta;
        public bool LockHighValueGoods;
        public bool MoveDeliverySpot;

        public static bool TryParse(string payload, out CampusConfiguredActionPayload result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            try
            {
                result = JsonUtility.FromJson<CampusConfiguredActionPayload>(payload);
            }
            catch (ArgumentException)
            {
                result = null;
            }

            if (result == null || string.IsNullOrWhiteSpace(result.Mode))
            {
                return false;
            }

            result.Normalize();
            return true;
        }

        public void Normalize()
        {
            Mode = Clean(Mode);
            ItemDefinitionId = Clean(ItemDefinitionId);
            EventKind = Clean(EventKind);
            TargetObjectId = Clean(TargetObjectId);
            TargetRoomId = Clean(TargetRoomId);
            RoomId = Clean(RoomId);
            SourceLocation = Clean(SourceLocation);
            OwnerId = Clean(OwnerId);
            Count = Mathf.Max(1, Count);
            SuspicionRiskOverride = SuspicionRiskOverride < 0 ? -1 : SuspicionRiskOverride;
            PlayerSuspicionDelta = ClampDelta(PlayerSuspicionDelta);
            PlayerTheftEvidenceDelta = ClampDelta(PlayerTheftEvidenceDelta);
            PlayerTheftRecordDelta = ClampDelta(PlayerTheftRecordDelta);
            CampusRumorDelta = ClampDelta(CampusRumorDelta);
            CampusCrackdownDelta = ClampDelta(CampusCrackdownDelta);
            CampusChaosDelta = ClampDelta(CampusChaosDelta);
            CampusOrderDelta = ClampDelta(CampusOrderDelta);
            TeacherAlertnessDelta = ClampDelta(TeacherAlertnessDelta);
            AreaAlertDelta = ClampDelta(AreaAlertDelta);
            AreaBagCheckDelta = ClampDelta(AreaBagCheckDelta);
            AreaPatrolDelta = ClampDelta(AreaPatrolDelta);
        }

        private static int ClampDelta(int value)
        {
            return Mathf.Clamp(value, -CampusGameState.StatMax, CampusGameState.StatMax);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal sealed class CampusConfiguredCharacterActionProvider : ICampusCharacterActionProvider
    {
        public static readonly CampusConfiguredCharacterActionProvider Instance =
            new CampusConfiguredCharacterActionProvider();

        public string ProviderId => "campus_configured_character_actions";

        public bool TryExecute(CampusCharacterActionContext context, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (context.Actor == null ||
                !CampusConfiguredActionPayload.TryParse(context.Payload, out CampusConfiguredActionPayload payload) ||
                !TargetPassesFilters(context.Target, payload))
            {
                return false;
            }

            if (CampusCharacterActionUtility.IdEquals(payload.Mode, "TakeFromTargetContainer") ||
                CampusCharacterActionUtility.IdEquals(payload.Mode, "TakeFromProtectedContainer"))
            {
                return TryTakeFromTargetContainer(context, payload, out result);
            }

            if (CampusCharacterActionUtility.IdEquals(payload.Mode, "RecordEvent") ||
                CampusCharacterActionUtility.IdEquals(payload.Mode, "ApplyState"))
            {
                ApplyConfiguredState(
                    context,
                    payload,
                    null,
                    ResolveRoomId(context.Actor, payload, null),
                    true);
                result = CampusCharacterActionUtility.Success();
                return true;
            }

            return false;
        }

        private static bool TryTakeFromTargetContainer(
            CampusCharacterActionContext context,
            CampusConfiguredActionPayload payload,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
            if (!TryResolveSourceContainer(context.Target, out StorageContainerModel source))
            {
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(context.Actor, true);
            StorageContainerModel[] targets = BuildCarryTargets(inventory);
            if (targets.Length == 0)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingTargetContainer));
                return true;
            }

            int movedCount = 0;
            string sourceRoomId = ResolveRoomId(context.Actor, payload, source);
            StorageItemModel lastItem = null;
            StorageTransferResult lastResult = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItem));
            for (int i = 0; i < payload.Count; i++)
            {
                StorageItemModel item = ResolveSourceItem(source, payload.ItemDefinitionId);
                if (item == null)
                {
                    break;
                }

                StorageTransferContext transferContext = BuildTransferContext(context.Actor, payload, source);
                if (!CampusInventoryActionExecutor.TryTransferItemToFirstFit(
                        context.Actor,
                        item,
                        source,
                        targets,
                        transferContext,
                        out lastResult))
                {
                    break;
                }

                movedCount++;
                lastItem = item;
            }

            if (movedCount <= 0)
            {
                result = lastResult.Succeeded
                    ? StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItem))
                    : lastResult;
                return true;
            }

            ApplyConfiguredState(context, payload, lastItem, sourceRoomId, lastResult.Succeeded, false);
            result = lastResult;
            return true;
        }

        private static StorageTransferContext BuildTransferContext(
            CampusCharacterRuntime actor,
            CampusConfiguredActionPayload payload,
            StorageContainerModel source)
        {
            StorageTransferContext context =
                StorageTransferContext.ForActor(actor.gameObject, StorageTransferReason.ScriptedTake);
            context.ForceIllegal = payload.ForceIllegal;
            context.AllowProtectedTake = payload.AllowProtectedTake;
            context.SuppressNpcDetection = payload.SuppressNpcDetection;
            context.SuspicionRiskOverride = payload.SuspicionRiskOverride;
            context.SourceLocation = string.IsNullOrWhiteSpace(payload.SourceLocation)
                ? source != null ? source.DisplayName : string.Empty
                : payload.SourceLocation;
            context.OwnerId = string.IsNullOrWhiteSpace(payload.OwnerId)
                ? source != null ? source.OwnerId : string.Empty
                : payload.OwnerId;
            context.RoomId = ResolveRoomId(actor, payload, source);
            return context;
        }

        private static void ApplyConfiguredState(
            CampusCharacterActionContext context,
            CampusConfiguredActionPayload payload,
            StorageItemModel item,
            string roomId,
            bool succeeded,
            bool applyConfiguredGlobalState = true)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusGameState state = bootstrap != null ? bootstrap.GameState : null;
            if (state != null)
            {
                if (applyConfiguredGlobalState)
                {
                    state.AddPlayerSuspicion(payload.PlayerSuspicionDelta);
                    state.AddPlayerTheftEvidence(payload.PlayerTheftEvidenceDelta);
                    state.AddPlayerTheftRecord(payload.PlayerTheftRecordDelta);
                    state.AddCampusRumor(payload.CampusRumorDelta);
                    state.AddCampusCrackdown(payload.CampusCrackdownDelta);
                    state.AddCampusChaos(payload.CampusChaosDelta);
                    state.AddCampusOrder(payload.CampusOrderDelta);
                    state.AddTeacherAlertness(payload.TeacherAlertnessDelta);
                }

                state.ApplyAreaDelta(
                    roomId,
                    payload.AreaAlertDelta,
                    payload.AreaBagCheckDelta,
                    payload.AreaPatrolDelta,
                    payload.LockHighValueGoods,
                    payload.MoveDeliverySpot);
            }

            CampusGameplayEventHub eventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;
            eventHub?.PublishConfiguredAction(new CampusConfiguredActionEvent(
                context.ActionId,
                context.Actor != null ? context.Actor.CharacterId : string.Empty,
                ResolveTargetId(context.Target),
                roomId,
                item != null ? item.InstanceId : string.Empty,
                item != null ? item.DefinitionId : payload.ItemDefinitionId,
                item != null ? item.GetDisplayName() : string.Empty,
                payload.OwnerId,
                payload.SourceLocation,
                string.IsNullOrWhiteSpace(payload.EventKind) ? payload.Mode : payload.EventKind,
                payload.PlayerSuspicionDelta,
                payload.PlayerTheftEvidenceDelta,
                payload.CampusRumorDelta,
                payload.CampusCrackdownDelta,
                payload.AreaAlertDelta,
                succeeded));
        }

        private static bool TryResolveSourceContainer(
            UnityEngine.Object target,
            out StorageContainerModel container)
        {
            container = null;
            if (!TryResolveComponentTarget(target, out CampusProtectedStockContainer stock) ||
                !TryResolveComponentTarget(target, out CampusPlacedObject placedObject))
            {
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                return false;
            }

            container = memory.GetOrCreateContainer(
                stock.ResolveStableContainerId(placedObject),
                placedObject.DisplayName,
                placedObject.LocalizedDisplayNameOverride,
                placedObject.NormalizedStorageSize.x,
                placedObject.NormalizedStorageSize.y,
                placedObject.NormalizedStorageMaxWeight);
            return stock.ConfigureContainer(memory, placedObject, container);
        }

        private static bool TargetPassesFilters(UnityEngine.Object target, CampusConfiguredActionPayload payload)
        {
            if (!string.IsNullOrWhiteSpace(payload.TargetObjectId))
            {
                if (!TryResolveComponentTarget(target, out CampusPlacedObject placedObject) ||
                    !CampusCharacterActionUtility.IdEquals(placedObject.ObjectId, payload.TargetObjectId))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(payload.TargetRoomId))
            {
                string roomId = ResolveTargetRoomId(target);
                if (!CampusCharacterActionUtility.IdEquals(roomId, payload.TargetRoomId))
                {
                    return false;
                }
            }

            return true;
        }

        private static StorageItemModel ResolveSourceItem(StorageContainerModel source, string definitionId)
        {
            if (source == null || source.Items == null)
            {
                return null;
            }

            for (int i = 0; i < source.Items.Count; i++)
            {
                StorageItemModel item = source.Items[i];
                if (item == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definitionId) ||
                    string.Equals(item.DefinitionId, definitionId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static StorageContainerModel[] BuildCarryTargets(CampusCharacterInventory inventory)
        {
            if (inventory == null)
            {
                return Array.Empty<StorageContainerModel>();
            }

            List<StorageContainerModel> targets = new List<StorageContainerModel>();
            AddTargets(targets, inventory.Hands);
            AddTargets(targets, inventory.Pockets);
            if (inventory.Backpack != null)
            {
                targets.Add(inventory.Backpack);
            }

            return targets.ToArray();
        }

        private static void AddTargets(List<StorageContainerModel> targets, StorageContainerModel[] containers)
        {
            if (targets == null || containers == null)
            {
                return;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                if (containers[i] != null)
                {
                    targets.Add(containers[i]);
                }
            }
        }

        private static string ResolveRoomId(
            CampusCharacterRuntime actor,
            CampusConfiguredActionPayload payload,
            StorageContainerModel source)
        {
            if (payload != null && !string.IsNullOrWhiteSpace(payload.RoomId))
            {
                return payload.RoomId;
            }

            if (source != null && !string.IsNullOrWhiteSpace(source.RoomId))
            {
                return source.RoomId;
            }

            return CampusProtectedTransferState.ResolveActorCurrentRoomId(actor);
        }

        private static string ResolveTargetRoomId(UnityEngine.Object target)
        {
            if (TryResolveComponentTarget(target, out CampusPlacedObject placedObject))
            {
                CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
                CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
                if (worldService != null)
                {
                    var room = worldService.FindRoomForPosition(
                        placedObject.FloorIndex,
                        placedObject.transform.position);
                    return room != null ? room.RoomId : string.Empty;
                }
            }

            return string.Empty;
        }

        private static string ResolveTargetId(UnityEngine.Object target)
        {
            if (TryResolveComponentTarget(target, out CampusPlacedObject placedObject) &&
                !string.IsNullOrWhiteSpace(placedObject.ObjectId))
            {
                return placedObject.ObjectId.Trim();
            }

            if (target is GameObject gameObject)
            {
                return gameObject.name;
            }

            return target is Component component ? component.gameObject.name : string.Empty;
        }

        private static bool TryResolveComponentTarget<T>(UnityEngine.Object target, out T component)
            where T : Component
        {
            component = null;
            if (target is T direct)
            {
                component = direct;
                return true;
            }

            GameObject gameObject = null;
            if (target is GameObject directGameObject)
            {
                gameObject = directGameObject;
            }
            else if (target is Component sourceComponent)
            {
                gameObject = sourceComponent.gameObject;
            }

            if (gameObject == null)
            {
                return false;
            }

            component = gameObject.GetComponentInParent<T>();
            if (component != null)
            {
                return true;
            }

            component = gameObject.GetComponentInChildren<T>();
            return component != null;
        }
    }
}
