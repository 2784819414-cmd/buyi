using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal enum CampusNpcInventorySightKind
    {
        None = 0,
        DirectSight = 1,
        Peripheral = 2,
        Heard = 3
    }

    internal sealed class CampusNpcInventoryAwareness
    {
        private const float ReadIntervalSeconds = 0.34f;
        private const int ReadFrameSlots = 13;

        private float nextReadTime;
        private int readFrameSlot = -1;
        private int lastSeenSerial;
        private bool initializedCursor;
        private readonly HashSet<string> observedHeldEvidence =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Observe(CampusNpcAiRuntime npc)
        {
            if (!CanRead(npc))
            {
                return;
            }

            if (ObserveRecentProtectedMoves(npc))
            {
                return;
            }

            ObserveVisibleHeldEvidence(npc);
        }

        private bool ObserveRecentProtectedMoves(CampusNpcAiRuntime npc)
        {
            IReadOnlyList<CampusProtectedItemMovedEvent> events = npc.EventHub.RecentProtectedItemMoves;
            int newestCheckedSerial = lastSeenSerial;
            for (int i = 0; i < events.Count; i++)
            {
                CampusProtectedItemMovedEvent eventData = events[i];
                if (eventData.Serial <= lastSeenSerial)
                {
                    continue;
                }

                newestCheckedSerial = Mathf.Max(newestCheckedSerial, eventData.Serial);
                if (TryNotice(npc, eventData, out CampusNpcInventorySightKind sightKind))
                {
                    lastSeenSerial = newestCheckedSerial;
                    bool reported = React(npc, CampusNpcInventoryObservation.FromProtectedMove(eventData), sightKind);
                    if (reported)
                    {
                        RememberHeldEvidenceIfVisible(eventData.ActorId, eventData.ItemInstanceId, eventData.TargetContainerId);
                    }

                    return true;
                }
            }

            lastSeenSerial = newestCheckedSerial;
            return false;
        }

        private bool ObserveVisibleHeldEvidence(CampusNpcAiRuntime npc)
        {
            if (npc == null || npc.RosterService == null || npc.Data == null)
            {
                return false;
            }

            CampusNpcVisionProfile vision = npc.VisionProfile ?? new CampusNpcVisionProfile();
            IReadOnlyList<CampusCharacterRuntime> runtimes =
                npc.RosterService.Index.GetVisibleActorCandidates(
                    npc.Data.CurrentRoomId,
                    vision.RequireSameRoom);
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime actorRuntime = runtimes[i];
                if (!TryBuildVisibleHeldObservation(
                        npc,
                        actorRuntime,
                        out CampusNpcInventoryObservation observation))
                {
                    continue;
                }

                string observationKey = BuildHeldEvidenceKey(observation.ActorId, observation.ItemInstanceId);
                if (observedHeldEvidence.Contains(observationKey))
                {
                    continue;
                }

                if (React(npc, observation, CampusNpcInventorySightKind.DirectSight))
                {
                    observedHeldEvidence.Add(observationKey);
                }

                return true;
            }

            return false;
        }

        private bool CanRead(CampusNpcAiRuntime npc)
        {
            if (npc == null || npc.EventHub == null || npc.Data == null || npc.Runtime == null)
            {
                return false;
            }

            if (!initializedCursor)
            {
                lastSeenSerial = npc.EventHub.LatestProtectedItemMoveSerial;
                initializedCursor = true;
            }

            float now = UnityEngine.Time.time;
            if (readFrameSlot < 0)
            {
                readFrameSlot = CampusNpcStableIds.PositiveModulo(npc.PersonalSeed * 7 + 5, ReadFrameSlots);
            }

            if (now < nextReadTime || UnityEngine.Time.frameCount % ReadFrameSlots != readFrameSlot)
            {
                return false;
            }

            nextReadTime = now + ReadIntervalSeconds + ResolveReadOffset(npc.PersonalSeed + Mathf.FloorToInt(now * 19f));
            return true;
        }

        private bool TryNotice(
            CampusNpcAiRuntime npc,
            CampusProtectedItemMovedEvent eventData,
            out CampusNpcInventorySightKind sightKind)
        {
            sightKind = CampusNpcInventorySightKind.None;
            if (IsSameActor(npc.Runtime.CharacterId, eventData.ActorId))
            {
                return false;
            }

            CampusNpcVisionProfile vision = npc.VisionProfile ?? new CampusNpcVisionProfile();
            if (vision.RequireSameRoom && !IsSameRoom(npc, eventData.RoomId))
            {
                return false;
            }

            Vector2 observerPosition = npc.Runtime.transform.position;
            Vector2 eventPosition = eventData.WorldPosition;
            Vector2 toEvent = eventPosition - observerPosition;
            float distance = toEvent.magnitude;
            if (distance <= 0.001f)
            {
                sightKind = CampusNpcInventorySightKind.Peripheral;
                return RollNotice(npc, eventData, sightKind, 1f);
            }

            if (distance <= vision.ViewDistance && IsInsideViewCone(npc.FacingState, toEvent, vision.ViewAngle))
            {
                sightKind = CampusNpcInventorySightKind.DirectSight;
                if (CampusCharacterInventoryService.IsHandContainerId(eventData.TargetContainerId))
                {
                    return true;
                }

                return RollNotice(npc, eventData, sightKind, 0.78f);
            }

            if (distance <= vision.PeripheralDistance)
            {
                sightKind = CampusNpcInventorySightKind.Peripheral;
                return RollNotice(npc, eventData, sightKind, 0.42f);
            }

            if (vision.CanHearBehind && distance <= vision.HearingDistance)
            {
                sightKind = CampusNpcInventorySightKind.Heard;
                return RollNotice(npc, eventData, sightKind, 0.22f);
            }

            return false;
        }

        private bool TryBuildVisibleHeldObservation(
            CampusNpcAiRuntime npc,
            CampusCharacterRuntime actorRuntime,
            out CampusNpcInventoryObservation observation)
        {
            observation = default;
            if (npc == null ||
                actorRuntime == null ||
                actorRuntime.Data == null ||
                IsSameActor(npc.Runtime.CharacterId, actorRuntime.CharacterId))
            {
                return false;
            }

            CampusNpcVisionProfile vision = npc.VisionProfile ?? new CampusNpcVisionProfile();
            string roomId = ResolveActorRoomId(npc, actorRuntime);
            if (vision.RequireSameRoom && !IsSameRoom(npc, roomId))
            {
                return false;
            }

            Vector2 observerPosition = npc.Runtime.transform.position;
            Vector2 actorPosition = actorRuntime.transform.position;
            Vector2 toActor = actorPosition - observerPosition;
            if (toActor.magnitude > vision.ViewDistance ||
                !IsInsideViewCone(npc.FacingState, toActor, vision.ViewAngle))
            {
                return false;
            }

            if (!TryFindHeldEvidence(actorRuntime, out StorageItemModel item, out StorageContainerModel container))
            {
                return false;
            }

            string itemInstanceId = ResolveInstanceId(item);
            if (observedHeldEvidence.Contains(BuildHeldEvidenceKey(actorRuntime.CharacterId, itemInstanceId)))
            {
                return false;
            }

            observation = CampusNpcInventoryObservation.FromHeldEvidence(
                actorRuntime.CharacterId,
                item,
                container,
                roomId,
                actorRuntime.transform.position);
            return true;
        }

        private bool React(
            CampusNpcAiRuntime npc,
            CampusNpcInventoryObservation observation,
            CampusNpcInventorySightKind sightKind)
        {
            if (!ShouldReport(npc.Data, observation, sightKind))
            {
                return false;
            }

            int suspicion = ResolveSuspicion(npc.Data, observation, sightKind);
            bool shouldIssueSanction = npc.Data.Role == CampusCharacterRole.Teacher ||
                                       npc.Data.Role == CampusCharacterRole.Staff;

            ApplyPersonalReaction(npc, observation, shouldIssueSanction);
            if (shouldIssueSanction)
            {
                npc.ReactToObservedTheft(observation.WorldPosition, observation.RoomId);
            }

            npc.EventHub.PublishItemTheftObserved(new CampusItemTheftObservedEvent(
                observation.ActorId,
                npc.Runtime.CharacterId,
                observation.OwnerId,
                observation.ItemInstanceId,
                observation.ItemDefinitionId,
                observation.ItemDisplayName,
                observation.SourceContainerId,
                observation.TargetContainerId,
                observation.RoomId,
                suspicion,
                shouldIssueSanction));

            return true;
        }

        private static void ApplyPersonalReaction(
            CampusNpcAiRuntime npc,
            CampusNpcInventoryObservation observation,
            bool officialWitness)
        {
            if (npc.Data != null && !string.IsNullOrWhiteSpace(observation.ActorId))
            {
                npc.Data.AddRelationshipSuspicion(observation.ActorId, officialWitness ? 12 : 7);
                npc.Data.AddRelationshipTrust(observation.ActorId, officialWitness ? -5 : -3);
            }
        }

        private static bool ShouldReport(
            CampusCharacterData data,
            CampusNpcInventoryObservation observation,
            CampusNpcInventorySightKind sightKind)
        {
            if (data == null)
            {
                return false;
            }

            if (IsSameActor(data.Id, observation.OwnerId))
            {
                return true;
            }

            if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
            {
                return sightKind != CampusNpcInventorySightKind.Heard ||
                       data.HasTrait(CampusCharacterTrait.Tattletale);
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                return true;
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return sightKind == CampusNpcInventorySightKind.DirectSight;
            }

            return false;
        }

        private static int ResolveSuspicion(
            CampusCharacterData data,
            CampusNpcInventoryObservation observation,
            CampusNpcInventorySightKind sightKind)
        {
            int amount = Mathf.Max(1, observation.SuspicionRisk);
            switch (sightKind)
            {
                case CampusNpcInventorySightKind.DirectSight:
                    amount += 10;
                    break;
                case CampusNpcInventorySightKind.Peripheral:
                    amount += 5;
                    break;
                case CampusNpcInventorySightKind.Heard:
                    amount += 2;
                    break;
            }

            if (data != null && data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                amount += 4;
            }

            return Mathf.Clamp(amount, 1, 60);
        }

        private static bool TryFindHeldEvidence(
            CampusCharacterRuntime actorRuntime,
            out StorageItemModel item,
            out StorageContainerModel container)
        {
            item = null;
            container = null;
            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actorRuntime, false);
            StorageContainerModel[] hands = inventory.Hands;
            for (int i = 0; i < hands.Length; i++)
            {
                StorageContainerModel hand = hands[i];
                if (hand == null || hand.Items == null)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < hand.Items.Count; itemIndex++)
                {
                    StorageItemModel candidate = hand.Items[itemIndex];
                    if (candidate != null && candidate.IsStolenEvidence)
                    {
                        item = candidate;
                        container = hand;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string ResolveActorRoomId(CampusNpcAiRuntime npc, CampusCharacterRuntime actorRuntime)
        {
            CampusGameplayRoom room = npc.WorldService != null ? npc.WorldService.FindRoomForRuntime(actorRuntime) : null;
            if (room != null)
            {
                return room.RoomId;
            }

            return actorRuntime != null && actorRuntime.Data != null
                ? actorRuntime.Data.CurrentRoomId
                : string.Empty;
        }

        private void RememberHeldEvidenceIfVisible(string actorId, string itemInstanceId, string targetContainerId)
        {
            if (!CampusCharacterInventoryService.IsHandContainerId(targetContainerId))
            {
                return;
            }

            observedHeldEvidence.Add(BuildHeldEvidenceKey(actorId, itemInstanceId));
        }

        private static string BuildHeldEvidenceKey(string actorId, string itemInstanceId)
        {
            return (actorId ?? string.Empty).Trim() + "|" + (itemInstanceId ?? string.Empty).Trim();
        }

        private static string ResolveInstanceId(StorageItemModel item)
        {
            return item == null
                ? string.Empty
                : !string.IsNullOrWhiteSpace(item.InstanceId)
                    ? item.InstanceId
                    : item.Id;
        }

        private static string ResolveItemName(StorageItemModel item)
        {
            if (item == null)
            {
                return StorageTextCatalog.Get(StorageTextId.ItemFallback);
            }

            string displayName = item.GetDisplayName();
                return !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : !string.IsNullOrWhiteSpace(item.DefinitionId)
                    ? item.DefinitionId
                    : StorageTextCatalog.Get(StorageTextId.ItemFallback);
        }

        private readonly struct CampusNpcInventoryObservation
        {
            private CampusNpcInventoryObservation(
                string actorId,
                string ownerId,
                string itemInstanceId,
                string itemDefinitionId,
                string itemDisplayName,
                string sourceContainerId,
                string targetContainerId,
                string roomId,
                Vector3 worldPosition,
                int suspicionRisk)
            {
                ActorId = actorId ?? string.Empty;
                OwnerId = ownerId ?? string.Empty;
                ItemInstanceId = itemInstanceId ?? string.Empty;
                ItemDefinitionId = itemDefinitionId ?? string.Empty;
                ItemDisplayName = itemDisplayName ?? string.Empty;
                SourceContainerId = sourceContainerId ?? string.Empty;
                TargetContainerId = targetContainerId ?? string.Empty;
                RoomId = roomId ?? string.Empty;
                WorldPosition = worldPosition;
                SuspicionRisk = Mathf.Max(1, suspicionRisk);
            }

            public string ActorId { get; }
            public string OwnerId { get; }
            public string ItemInstanceId { get; }
            public string ItemDefinitionId { get; }
            public string ItemDisplayName { get; }
            public string SourceContainerId { get; }
            public string TargetContainerId { get; }
            public string RoomId { get; }
            public Vector3 WorldPosition { get; }
            public int SuspicionRisk { get; }

            public static CampusNpcInventoryObservation FromProtectedMove(CampusProtectedItemMovedEvent eventData)
            {
                return new CampusNpcInventoryObservation(
                    eventData.ActorId,
                    eventData.OwnerId,
                    eventData.ItemInstanceId,
                    eventData.ItemDefinitionId,
                    eventData.ItemDisplayName,
                    eventData.SourceContainerId,
                    eventData.TargetContainerId,
                    eventData.RoomId,
                    eventData.WorldPosition,
                    eventData.SuspicionRisk);
            }

            public static CampusNpcInventoryObservation FromHeldEvidence(
                string actorId,
                StorageItemModel item,
                StorageContainerModel container,
                string roomId,
                Vector3 worldPosition)
            {
                string resolvedRoomId = !string.IsNullOrWhiteSpace(roomId)
                    ? roomId
                    : item != null ? item.SourceRoomId : string.Empty;
                return new CampusNpcInventoryObservation(
                    actorId,
                    item != null ? item.OwnerId : string.Empty,
                    ResolveInstanceId(item),
                    item != null ? item.DefinitionId : string.Empty,
                    ResolveItemName(item),
                    item != null ? item.SourceContainerId : string.Empty,
                    container != null ? container.Id : string.Empty,
                    resolvedRoomId,
                    worldPosition,
                    item != null ? item.SuspicionRisk : 1);
            }
        }

        private static bool RollNotice(
            CampusNpcAiRuntime npc,
            CampusProtectedItemMovedEvent eventData,
            CampusNpcInventorySightKind sightKind,
            float baseChance)
        {
            float riskBonus = Mathf.Clamp01(eventData.SuspicionRisk / 80f);
            float sightBonus = sightKind == CampusNpcInventorySightKind.DirectSight
                ? 0.16f
                : sightKind == CampusNpcInventorySightKind.Peripheral
                    ? 0.06f
                    : 0f;
            float chance = Mathf.Clamp01((baseChance + riskBonus + sightBonus) *
                                         Mathf.Max(0.05f, npc.VisionProfile.AttentionMultiplier));
            int roll = CampusNpcStableIds.PositiveModulo(
                CampusNpcStableIds.Hash(npc.Runtime.CharacterId + ":" + eventData.Serial + ":inventory_sight"),
                1000);
            return roll < Mathf.RoundToInt(chance * 1000f);
        }

        private static bool IsInsideViewCone(
            CampusCharacterFacingState facingState,
            Vector2 toTarget,
            float viewAngle)
        {
            Vector2 forward = facingState != null ? facingState.Forward : Vector2.down;
            return Vector2.Angle(forward, toTarget) <= Mathf.Max(1f, viewAngle) * 0.5f;
        }

        private static bool IsSameRoom(CampusNpcAiRuntime npc, string roomId)
        {
            return string.IsNullOrWhiteSpace(roomId) ||
                   string.Equals(npc.Data.CurrentRoomId, roomId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameActor(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static float ResolveReadOffset(int seed)
        {
            return Mathf.Lerp(0.02f, 0.20f, CampusNpcStableIds.PositiveModulo(seed * 41, 100) / 99f);
        }
    }
}
