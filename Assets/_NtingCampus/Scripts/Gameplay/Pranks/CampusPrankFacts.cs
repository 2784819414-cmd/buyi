using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    public readonly struct CampusPrankTarget
    {
        public CampusPrankTarget(
            string payload,
            UnityEngine.Object interactTarget,
            Vector3 position,
            string roomId,
            float stopDistance)
        {
            Payload = payload ?? string.Empty;
            InteractTarget = interactTarget;
            Position = position;
            RoomId = roomId ?? string.Empty;
            StopDistance = Mathf.Max(0.08f, stopDistance);
        }

        public string Payload { get; }
        public UnityEngine.Object InteractTarget { get; }
        public Vector3 Position { get; }
        public string RoomId { get; }
        public float StopDistance { get; }
        public bool IsValid => InteractTarget != null && !string.IsNullOrWhiteSpace(Payload);
    }

    internal sealed class CampusPrankFacts
    {
        private readonly List<CampusPrankTarget> targets = new List<CampusPrankTarget>();
        private readonly HashSet<int> targetInstanceIds = new HashSet<int>();

        public IReadOnlyList<CampusPrankTarget> Targets => targets;

        public void Refresh(CampusWorldService worldService)
        {
            targets.Clear();
            targetInstanceIds.Clear();
            RefreshInteractionSpots(worldService);
            RefreshPlacedPrankObjects(worldService);
        }

        public bool TryFindTarget(
            CampusCharacterRuntime actor,
            Predicate<string> payloadFilter,
            out CampusPrankTarget target)
        {
            target = default;
            if (actor == null || targets.Count == 0)
            {
                return false;
            }

            float bestDistance = float.MaxValue;
            Vector3 actorPosition = actor.transform.position;
            for (int i = 0; i < targets.Count; i++)
            {
                CampusPrankTarget candidate = targets[i];
                if (!candidate.IsValid ||
                    payloadFilter != null && !payloadFilter(candidate.Payload))
                {
                    continue;
                }

                float distance = (candidate.Position - actorPosition).sqrMagnitude;
                if (!target.IsValid || distance < bestDistance)
                {
                    target = candidate;
                    bestDistance = distance;
                }
            }

            return target.IsValid;
        }

        private void RefreshInteractionSpots(CampusWorldService worldService)
        {
            CampusPrankInteractionSpot[] spots = UnityEngine.Object.FindObjectsByType<CampusPrankInteractionSpot>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < spots.Length; i++)
            {
                CampusPrankInteractionSpot spot = spots[i];
                if (spot == null || !spot.isActiveAndEnabled)
                {
                    continue;
                }

                UnityEngine.Object interactTarget = ResolveInteractTarget(spot.gameObject, spot.PrankPayload);
                AddTarget(
                    spot.PrankPayload,
                    interactTarget,
                    spot.transform.position,
                    ResolveRoomId(worldService, spot.transform),
                    spot.InteractionRadius);
            }
        }

        private void RefreshPlacedPrankObjects(CampusWorldService worldService)
        {
            CampusPrankPlacedObject[] placedPranks = UnityEngine.Object.FindObjectsByType<CampusPrankPlacedObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < placedPranks.Length; i++)
            {
                CampusPrankPlacedObject placedPrank = placedPranks[i];
                if (placedPrank == null || !placedPrank.isActiveAndEnabled)
                {
                    continue;
                }

                UnityEngine.Object interactTarget = ResolveInteractTarget(placedPrank.gameObject, placedPrank.Payload);
                AddTarget(
                    placedPrank.Payload,
                    interactTarget,
                    placedPrank.transform.position,
                    ResolveRoomId(worldService, placedPrank.transform),
                    0.2f);
            }
        }

        private void AddTarget(
            string payload,
            UnityEngine.Object interactTarget,
            Vector3 position,
            string roomId,
            float stopDistance)
        {
            if (interactTarget == null ||
                string.IsNullOrWhiteSpace(payload) ||
                !CampusPrankCatalog.TryGetByPayload(payload, out _))
            {
                return;
            }

            int instanceId = interactTarget.GetInstanceID();
            if (!targetInstanceIds.Add(instanceId))
            {
                return;
            }

            targets.Add(new CampusPrankTarget(payload.Trim(), interactTarget, position, roomId, stopDistance));
        }

        private static UnityEngine.Object ResolveInteractTarget(GameObject source, string payload)
        {
            CampusInteractionAnchor[] anchors = source != null
                ? source.GetComponentsInChildren<CampusInteractionAnchor>(true)
                : Array.Empty<CampusInteractionAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                CampusInteractionAnchor anchor = anchors[i];
                if (anchor == null ||
                    !CampusInteractionActionIds.Equals(anchor.ActionId, CampusInteractionActionIds.PrankExecute))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(anchor.Payload) ||
                    string.Equals(anchor.Payload, payload, StringComparison.OrdinalIgnoreCase))
                {
                    return anchor;
                }
            }

            return source;
        }

        private static string ResolveRoomId(CampusWorldService worldService, Transform target)
        {
            if (target == null || worldService == null)
            {
                return string.Empty;
            }

            int floorIndex = 1;
            CampusPlacedObject placedObject = target.GetComponentInParent<CampusPlacedObject>();
            if (placedObject != null)
            {
                floorIndex = Mathf.Max(1, placedObject.FloorIndex);
            }
            else
            {
                CampusRuntimeGameplayOverlayEntity entity = target.GetComponentInParent<CampusRuntimeGameplayOverlayEntity>();
                if (entity != null)
                {
                    floorIndex = entity.FloorIndex;
                }
            }

            CampusGameplayRoom room = worldService.FindRoomForPosition(floorIndex, target.position);
            return room != null ? room.RoomId : string.Empty;
        }
    }
}
