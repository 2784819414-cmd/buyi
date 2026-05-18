using System;
using System.Collections.Generic;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CampusCharacterBodyController))]
    public sealed class CampusGridNavigationAgent : MonoBehaviour
    {
        private const int MaxPathSearchIterations = 2400;
        private const float FloorBlockageCacheRefreshSeconds = 0.75f;
        private const float MinReplanIntervalSeconds = 0.18f;

        private static readonly List<CampusGridNavigationAgent> ActiveAgents =
            new List<CampusGridNavigationAgent>();

        private static readonly List<CampusPlacedObject> PlacedObjectScratch =
            new List<CampusPlacedObject>();

        private static readonly Dictionary<CampusFloorRoot, FloorBlockageCache> FloorBlockageCaches =
            new Dictionary<CampusFloorRoot, FloorBlockageCache>();

        private static readonly List<CampusDoor3D> DoorScratch = new List<CampusDoor3D>();

        private static readonly Dictionary<string, int> ReservedDestinationOwnerByKey =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        [SerializeField] private CampusCharacterBodyController bodyController;
        [SerializeField] private CampusMapRoot mapRoot;
        [SerializeField, Min(0.05f)] private float moveSpeed = 1.35f;
        [SerializeField, Min(1)] private int floorIndex = 1;
        [SerializeField, Min(0.02f)] private float arrivalDistance = 0.14f;
        [SerializeField, Min(0.02f)] private float waypointArrivalDistance = 0.16f;
        [SerializeField, Min(0.05f)] private float replanIntervalSeconds = 0.8f;
        [SerializeField, Min(0.1f)] private float separationRadius = 0.62f;
        [SerializeField, Min(0f)] private float separationWeight = 0.82f;
        [SerializeField, Min(0.05f)] private float wallAvoidanceDistance = 0.62f;
        [SerializeField, Min(0f)] private float wallAvoidanceWeight = 1.15f;
        [SerializeField, Min(0f)] private float wallAdjacencyCost = 9f;
        [SerializeField, Min(0f)] private float blockedCornerCost = 4f;
        [SerializeField, Min(0.05f)] private float movementSampleIntervalSeconds = 0.25f;
        [SerializeField, Min(0.1f)] private float movementStuckTimeoutSeconds = 0.9f;
        [SerializeField, Min(0.005f)] private float movementStuckDistance = 0.035f;
        [SerializeField] private string debugMoveReason = string.Empty;
        [SerializeField] private bool hasDestination;
        [SerializeField] private bool hasReachablePath = true;
        [SerializeField] private bool isMoving;
        [SerializeField] private Vector3 destination;
        [SerializeField] private Vector3 resolvedDestination;
        [SerializeField] private Vector3 waypointPosition;

        private readonly List<Vector3Int> pathCells = new List<Vector3Int>();
        private int pathCellIndex;
        private int personalSeed = 1;
        private int recoverySerial;
        private Vector3Int lastPathStartCell;
        private Vector3Int lastPathTargetCell;
        private Vector3 movementSamplePosition;
        private float nextPathReplanTime;
        private float nextMovementSampleTime;
        private float movementStuckStartedAt = -1f;
        private string activeDestinationReservationKey = string.Empty;

        public bool HasDestination => hasDestination;
        public bool HasReachablePath => hasReachablePath;
        public bool IsMoving => isMoving;
        public Vector3 Destination => destination;
        public Vector3 WaypointPosition => waypointPosition;

        private void Awake()
        {
            EnsureSetup();
        }

        private void OnEnable()
        {
            EnsureSetup();
            RegisterActiveAgent(this);
        }

        private void OnDisable()
        {
            UnregisterActiveAgent(this);
            ReleaseDestinationReservation();
        }

        private void OnDestroy()
        {
            UnregisterActiveAgent(this);
            ReleaseDestinationReservation();
        }

        public void Configure(
            float speed,
            int floor,
            int seed,
            float replanSeconds,
            float waypointDistance,
            float stuckTimeout,
            float stuckDistance)
        {
            moveSpeed = Mathf.Max(0.05f, speed);
            floorIndex = Mathf.Max(1, floor);
            personalSeed = Mathf.Max(1, Mathf.Abs(seed));
            replanIntervalSeconds = Mathf.Max(MinReplanIntervalSeconds, replanSeconds);
            waypointArrivalDistance = Mathf.Max(0.02f, waypointDistance);
            movementStuckTimeoutSeconds = Mathf.Max(0.1f, stuckTimeout);
            movementStuckDistance = Mathf.Max(0.005f, stuckDistance);
        }

        public void SetDestination(Vector3 worldPosition, float stopDistance, string reason)
        {
            EnsureSetup();
            worldPosition.z = transform.position.z;
            arrivalDistance = Mathf.Max(0.02f, stopDistance);
            debugMoveReason = reason ?? string.Empty;

            if (!hasDestination || Vector2.Distance(destination, worldPosition) > 0.025f)
            {
                destination = worldPosition;
                resolvedDestination = worldPosition;
                waypointPosition = worldPosition;
                hasDestination = true;
                hasReachablePath = true;
                nextPathReplanTime = 0f;
                movementStuckStartedAt = -1f;
            }

            ReserveDestinationCell(WorldToCell(destination));
        }

        public void ClearDestination()
        {
            hasDestination = false;
            hasReachablePath = true;
            isMoving = false;
            debugMoveReason = string.Empty;
            pathCells.Clear();
            pathCellIndex = 0;
            waypointPosition = transform.position;
            destination = transform.position;
            resolvedDestination = transform.position;
            lastPathStartCell = default;
            lastPathTargetCell = default;
            movementStuckStartedAt = -1f;
            ReleaseDestinationReservation();
            if (bodyController != null)
            {
                bodyController.StopMovement();
            }
        }

        public void HoldPosition()
        {
            isMoving = false;
            movementSamplePosition = transform.position;
            nextMovementSampleTime = Time.time + movementSampleIntervalSeconds;
            movementStuckStartedAt = -1f;
            if (bodyController != null)
            {
                bodyController.StopMovement();
            }
        }

        public void ForceReplan()
        {
            nextPathReplanTime = 0f;
            pathCells.Clear();
            pathCellIndex = 0;
        }

        public static bool IsNavigationCellWalkable(CampusFloorRoot floor, Vector3Int cell)
        {
            cell.z = 0;
            if (floor == null)
            {
                return false;
            }

            bool isDoorPortal = IsDoorPortalCell(floor, cell);
            if (floor.FloorTilemap != null && !floor.FloorTilemap.HasTile(cell) && !isDoorPortal)
            {
                return false;
            }

            if (!isDoorPortal && CampusWallTileUtility.HasWall(CampusWallTileUtility.GetWallLogicTilemap(floor), cell))
            {
                return false;
            }

            HashSet<Vector3Int> blockedObjectCells = GetBlockedObjectCells(floor);
            return isDoorPortal || blockedObjectCells == null || !blockedObjectCells.Contains(cell);
        }

        public static bool IsDoorPortalCell(CampusFloorRoot floor, Vector3Int cell)
        {
            HashSet<Vector3Int> doorCells = GetDoorPortalCells(floor);
            return doorCells != null && doorCells.Contains(new Vector3Int(cell.x, cell.y, 0));
        }

        public void TickNavigation()
        {
            EnsureSetup();
            bodyController.MoveSpeed = moveSpeed;
            bodyController.FloorIndex = floorIndex;
            bodyController.EnsureSetup();

            if (!hasDestination)
            {
                HoldPosition();
                return;
            }

            Vector2 toDestination = (Vector2)(resolvedDestination - transform.position);
            if (toDestination.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                HoldPosition();
                return;
            }

            RefreshPathIfNeeded();
            AdvancePathWaypointIfNeeded();
            TryOpenNearbyDoorPortal();
            if (!hasReachablePath)
            {
                HoldPosition();
                return;
            }

            Vector2 desired = (Vector2)(waypointPosition - transform.position);
            if (desired.sqrMagnitude <= 0.0001f)
            {
                desired = toDestination;
            }

            if (desired.sqrMagnitude <= 0.0001f)
            {
                HoldPosition();
                return;
            }

            if (ShouldRecoverFromStuckMovement())
            {
                RecoverFromBlockedMovement();
                HoldPosition();
                return;
            }

            Vector2 steering = desired.normalized;
            steering += ResolveSeparationVector() * separationWeight;
            steering += ResolveWallAvoidanceVector() * wallAvoidanceWeight;
            if (steering.sqrMagnitude <= 0.0001f)
            {
                steering = desired.normalized;
            }

            isMoving = true;
            bodyController.SetMovementInput(steering.normalized);
        }

        private void EnsureSetup()
        {
            if (bodyController == null)
            {
                bodyController = GetComponent<CampusCharacterBodyController>();
            }

            if (bodyController == null)
            {
                bodyController = gameObject.AddComponent<CampusCharacterBodyController>();
            }

            if (mapRoot == null)
            {
                mapRoot = FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            }

            movementSamplePosition = movementSamplePosition == default ? transform.position : movementSamplePosition;
        }

        private void RefreshPathIfNeeded()
        {
            CampusFloorRoot floor = ResolveCurrentFloor();
            if (floor == null)
            {
                hasReachablePath = false;
                pathCells.Clear();
                return;
            }

            Vector3Int startCell = WorldToCell(transform.position);
            Vector3Int preferredTargetCell = WorldToCell(destination);
            if (!TryResolveNearestDestinationCell(floor, preferredTargetCell, startCell, out Vector3Int targetCell))
            {
                hasReachablePath = false;
                pathCells.Clear();
                pathCellIndex = 0;
                waypointPosition = transform.position;
                movementStuckStartedAt = -1f;
                return;
            }

            ReserveDestinationCell(targetCell);
            resolvedDestination = targetCell == preferredTargetCell ? destination : CellCenterToWorld(targetCell);

            bool targetChanged = targetCell != lastPathTargetCell;
            bool startMovedOffPath = pathCells.Count > 0 && !pathCells.Contains(startCell);
            if (!targetChanged && !startMovedOffPath && Time.time < nextPathReplanTime)
            {
                return;
            }

            nextPathReplanTime = Time.time + ResolvePersonalDelay(
                replanIntervalSeconds * 0.7f,
                replanIntervalSeconds * 1.25f,
                Mathf.FloorToInt(Time.time * 5f));
            lastPathStartCell = startCell;
            lastPathTargetCell = targetCell;
            pathCells.Clear();
            pathCellIndex = 0;

            if (TryBuildPath(floor, startCell, targetCell, pathCells))
            {
                hasReachablePath = true;
                pathCellIndex = Mathf.Min(1, pathCells.Count - 1);
                waypointPosition = ResolveCurrentPathWaypoint();
                return;
            }

            if (TryResolveNearbyRecoveryCell(floor, startCell, targetCell, out Vector3Int recoveryCell) &&
                TryBuildPath(floor, startCell, recoveryCell, pathCells))
            {
                hasReachablePath = true;
                pathCellIndex = Mathf.Min(1, pathCells.Count - 1);
                waypointPosition = ResolveCurrentPathWaypoint();
                movementStuckStartedAt = -1f;
                return;
            }

            hasReachablePath = false;
            waypointPosition = transform.position;
            movementStuckStartedAt = -1f;
        }

        private void AdvancePathWaypointIfNeeded()
        {
            if (pathCells.Count == 0)
            {
                waypointPosition = hasReachablePath ? resolvedDestination : transform.position;
                return;
            }

            while (pathCellIndex < pathCells.Count - 1 &&
                   Vector2.Distance(transform.position, CellCenterToWorld(pathCells[pathCellIndex])) <= waypointArrivalDistance)
            {
                pathCellIndex++;
            }

            waypointPosition = pathCellIndex >= 0 && pathCellIndex < pathCells.Count
                ? ResolveCurrentPathWaypoint()
                : resolvedDestination;
        }

        private Vector3 ResolveCurrentPathWaypoint()
        {
            if (pathCells.Count == 0 || pathCellIndex < 0)
            {
                return resolvedDestination;
            }

            if (pathCellIndex >= pathCells.Count - 1)
            {
                return resolvedDestination;
            }

            return CellCenterToWorld(pathCells[pathCellIndex]);
        }

        private bool TryBuildPath(CampusFloorRoot floor, Vector3Int startCell, Vector3Int targetCell, List<Vector3Int> output)
        {
            output.Clear();
            startCell.z = 0;
            targetCell.z = 0;
            if (!IsWalkableCell(floor, startCell) || !IsWalkableCell(floor, targetCell))
            {
                return false;
            }

            if (startCell == targetCell)
            {
                output.Add(startCell);
                return true;
            }

            int minX = Mathf.Min(startCell.x, targetCell.x) - 24;
            int maxX = Mathf.Max(startCell.x, targetCell.x) + 24;
            int minY = Mathf.Min(startCell.y, targetCell.y) - 24;
            int maxY = Mathf.Max(startCell.y, targetCell.y) + 24;

            floor.RefreshUsedBoundsIfDirty();
            if (floor.UsedBounds.size.x > 0 && floor.UsedBounds.size.y > 0)
            {
                minX = Mathf.Max(minX, floor.UsedBounds.xMin - 3);
                maxX = Mathf.Min(maxX, floor.UsedBounds.xMax + 3);
                minY = Mathf.Max(minY, floor.UsedBounds.yMin - 3);
                maxY = Mathf.Min(maxY, floor.UsedBounds.yMax + 3);
            }

            Dictionary<Vector3Int, PathNode> nodes = new Dictionary<Vector3Int, PathNode>();
            List<PathNode> open = new List<PathNode>();
            HashSet<Vector3Int> closed = new HashSet<Vector3Int>();
            PathNode start = new PathNode(startCell, 0f, Heuristic(startCell, targetCell), null);
            nodes[startCell] = start;
            open.Add(start);

            int iterations = 0;
            while (open.Count > 0 && iterations++ < MaxPathSearchIterations)
            {
                int bestIndex = 0;
                float bestScore = open[0].TotalCost;
                for (int i = 1; i < open.Count; i++)
                {
                    if (open[i].TotalCost < bestScore)
                    {
                        bestScore = open[i].TotalCost;
                        bestIndex = i;
                    }
                }

                PathNode current = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (!closed.Add(current.Cell))
                {
                    continue;
                }

                if (current.Cell == targetCell)
                {
                    ReconstructPath(current, output);
                    return output.Count > 0;
                }

                AddPathNeighbor(floor, current, new Vector3Int(current.Cell.x + 1, current.Cell.y, 0), targetCell, minX, maxX, minY, maxY, nodes, open, closed);
                AddPathNeighbor(floor, current, new Vector3Int(current.Cell.x - 1, current.Cell.y, 0), targetCell, minX, maxX, minY, maxY, nodes, open, closed);
                AddPathNeighbor(floor, current, new Vector3Int(current.Cell.x, current.Cell.y + 1, 0), targetCell, minX, maxX, minY, maxY, nodes, open, closed);
                AddPathNeighbor(floor, current, new Vector3Int(current.Cell.x, current.Cell.y - 1, 0), targetCell, minX, maxX, minY, maxY, nodes, open, closed);
            }

            return false;
        }

        private void AddPathNeighbor(
            CampusFloorRoot floor,
            PathNode current,
            Vector3Int neighbor,
            Vector3Int targetCell,
            int minX,
            int maxX,
            int minY,
            int maxY,
            Dictionary<Vector3Int, PathNode> nodes,
            List<PathNode> open,
            HashSet<Vector3Int> closed)
        {
            if (neighbor.x < minX || neighbor.x > maxX || neighbor.y < minY || neighbor.y > maxY || closed.Contains(neighbor))
            {
                return;
            }

            if (!IsWalkableCell(floor, neighbor))
            {
                return;
            }

            float movementCost = current.CostFromStart +
                                 1f +
                                 PersonalCellCost(neighbor) +
                                 ClearanceCost(floor, neighbor) +
                                 DynamicOccupancyCost(neighbor, targetCell);
            if (nodes.TryGetValue(neighbor, out PathNode existing))
            {
                if (movementCost >= existing.CostFromStart)
                {
                    return;
                }

                existing.CostFromStart = movementCost;
                existing.Parent = current;
                return;
            }

            PathNode node = new PathNode(neighbor, movementCost, Heuristic(neighbor, targetCell), current);
            nodes[neighbor] = node;
            open.Add(node);
        }

        private bool TryResolveNearestDestinationCell(
            CampusFloorRoot floor,
            Vector3Int preferredCell,
            Vector3Int fromCell,
            out Vector3Int result)
        {
            result = default;
            preferredCell.z = 0;
            fromCell.z = 0;
            if (IsSuitableDestinationCell(floor, preferredCell, fromCell))
            {
                result = preferredCell;
                return true;
            }

            bool hasFallback = IsWalkableCell(floor, preferredCell);
            Vector3Int fallback = preferredCell;
            float fallbackScore = hasFallback ? ScoreDestinationCell(floor, preferredCell, preferredCell, fromCell) : float.PositiveInfinity;
            for (int radius = 1; radius <= 12; radius++)
            {
                for (int y = preferredCell.y - radius; y <= preferredCell.y + radius; y++)
                {
                    for (int x = preferredCell.x - radius; x <= preferredCell.x + radius; x++)
                    {
                        if (Mathf.Abs(x - preferredCell.x) != radius && Mathf.Abs(y - preferredCell.y) != radius)
                        {
                            continue;
                        }

                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (!IsWalkableCell(floor, cell))
                        {
                            continue;
                        }

                        float score = ScoreDestinationCell(floor, cell, preferredCell, fromCell);
                        if (!hasFallback || score < fallbackScore)
                        {
                            hasFallback = true;
                            fallback = cell;
                            fallbackScore = score;
                        }

                        if ((IsDestinationReservedByOther(cell) || IsOccupiedByOtherAgent(cell, 0.44f)) &&
                            cell != fromCell)
                        {
                            continue;
                        }

                        result = cell;
                        return true;
                    }
                }
            }

            if (hasFallback)
            {
                result = fallback;
                return true;
            }

            return false;
        }

        private float ScoreDestinationCell(
            CampusFloorRoot floor,
            Vector3Int cell,
            Vector3Int preferredCell,
            Vector3Int fromCell)
        {
            return Mathf.Abs(cell.x - preferredCell.x) * 1.8f +
                   Mathf.Abs(cell.y - preferredCell.y) * 1.8f +
                   Mathf.Abs(cell.x - fromCell.x) +
                   Mathf.Abs(cell.y - fromCell.y) +
                   ClearanceCost(floor, cell) * 0.8f +
                   PersonalCellCost(cell);
        }

        private bool IsSuitableDestinationCell(CampusFloorRoot floor, Vector3Int cell, Vector3Int fromCell)
        {
            if (!IsWalkableCell(floor, cell))
            {
                return false;
            }

            if (cell == fromCell)
            {
                return true;
            }

            return !IsDestinationReservedByOther(cell) && !IsOccupiedByOtherAgent(cell, 0.44f);
        }
        private bool IsWalkableCell(CampusFloorRoot floor, Vector3Int cell)
        {
            cell.z = 0;
            if (floor == null)
            {
                return false;
            }

            if (!IsNavigationCellWalkable(floor, cell))
            {
                return false;
            }

            return !IsDoorPortalCell(floor, cell) || CanTraverseDoorPortalCell(floor, cell);
        }

        private bool CanTraverseDoorPortalCell(CampusFloorRoot floor, Vector3Int cell)
        {
            DoorScratch.Clear();
            GetDoorPortalComponents(floor, DoorScratch);
            bool foundDoor = false;
            for (int i = 0; i < DoorScratch.Count; i++)
            {
                CampusDoor3D door = DoorScratch[i];
                if (door == null)
                {
                    continue;
                }

                CampusPlacedObject placedObject = door.PlacedObject != null
                    ? door.PlacedObject
                    : door.GetComponentInParent<CampusPlacedObject>();
                if (placedObject == null || !placedObject.ContainsCell(cell))
                {
                    continue;
                }

                foundDoor = true;
                if (door.IsOpen || CanOpenDoor(door))
                {
                    DoorScratch.Clear();
                    return true;
                }
            }

            DoorScratch.Clear();
            return !foundDoor;
        }

        private float ClearanceCost(CampusFloorRoot floor, Vector3Int cell)
        {
            cell.z = 0;
            float cost = 0f;
            for (int y = cell.y - 1; y <= cell.y + 1; y++)
            {
                for (int x = cell.x - 1; x <= cell.x + 1; x++)
                {
                    if (x == cell.x && y == cell.y)
                    {
                        continue;
                    }

                    Vector3Int neighbor = new Vector3Int(x, y, 0);
                    if (IsWalkableCell(floor, neighbor))
                    {
                        continue;
                    }

                    bool cardinal = x == cell.x || y == cell.y;
                    cost += cardinal ? wallAdjacencyCost : blockedCornerCost;
                }
            }

            return cost;
        }

        private bool ShouldRecoverFromStuckMovement()
        {
            if (!Application.isPlaying || Time.time < nextMovementSampleTime)
            {
                return false;
            }

            nextMovementSampleTime = Time.time + movementSampleIntervalSeconds;
            float movedSqr = ((Vector2)(transform.position - movementSamplePosition)).sqrMagnitude;
            movementSamplePosition = transform.position;
            if (movedSqr >= movementStuckDistance * movementStuckDistance)
            {
                movementStuckStartedAt = -1f;
                return false;
            }

            if (movementStuckStartedAt < 0f)
            {
                movementStuckStartedAt = Time.time;
                return false;
            }

            return Time.time - movementStuckStartedAt >= movementStuckTimeoutSeconds;
        }

        private void RecoverFromBlockedMovement()
        {
            recoverySerial++;
            CampusFloorRoot floor = ResolveCurrentFloor();
            if (floor == null)
            {
                hasReachablePath = false;
                return;
            }

            Vector3Int startCell = WorldToCell(transform.position);
            Vector3Int targetCell = WorldToCell(destination);
            if (!TryResolveNearbyRecoveryCell(floor, startCell, targetCell, out Vector3Int recoveryCell))
            {
                hasReachablePath = false;
                ForceReplan();
                return;
            }

            pathCells.Clear();
            pathCells.Add(startCell);
            pathCells.Add(recoveryCell);
            pathCellIndex = 1;
            waypointPosition = CellCenterToWorld(recoveryCell);
            hasReachablePath = true;
            nextPathReplanTime = Time.time + ResolvePersonalDelay(0.2f, 0.45f, 307 + recoverySerial);
            movementStuckStartedAt = -1f;
        }

        private bool TryResolveNearbyRecoveryCell(
            CampusFloorRoot floor,
            Vector3Int startCell,
            Vector3Int blockedTargetCell,
            out Vector3Int result)
        {
            result = default;
            startCell.z = 0;
            blockedTargetCell.z = 0;
            float bestScore = float.PositiveInfinity;
            for (int radius = 1; radius <= 8; radius++)
            {
                for (int y = startCell.y - radius; y <= startCell.y + radius; y++)
                {
                    for (int x = startCell.x - radius; x <= startCell.x + radius; x++)
                    {
                        if (Mathf.Abs(x - startCell.x) != radius && Mathf.Abs(y - startCell.y) != radius)
                        {
                            continue;
                        }

                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (!IsWalkableCell(floor, cell) || IsOccupiedByOtherAgent(cell, 0.38f))
                        {
                            continue;
                        }

                        float distanceFromStart = Mathf.Abs(cell.x - startCell.x) + Mathf.Abs(cell.y - startCell.y);
                        float distanceToTarget = Mathf.Abs(cell.x - blockedTargetCell.x) + Mathf.Abs(cell.y - blockedTargetCell.y);
                        float noise = PersonalCellCost(cell);
                        float score = distanceFromStart * 0.65f + distanceToTarget * 0.18f + noise;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            result = cell;
                        }
                    }
                }

                if (bestScore < float.PositiveInfinity && radius >= 2)
                {
                    return true;
                }
            }

            return bestScore < float.PositiveInfinity;
        }

        private Vector2 ResolveSeparationVector()
        {
            Vector2 push = Vector2.zero;
            Vector2 position = (Vector2)transform.position;
            float radius = Mathf.Max(0.1f, separationRadius);
            float radiusSqr = radius * radius;
            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                CampusGridNavigationAgent other = ActiveAgents[i];
                if (other == null || other == this || !other.isActiveAndEnabled || other.floorIndex != floorIndex)
                {
                    continue;
                }

                Vector2 delta = position - (Vector2)other.transform.position;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr <= 0.0001f || distanceSqr >= radiusSqr)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSqr);
                push += delta / distance * (1f - distance / radius);
            }

            return push;
        }

        private Vector2 ResolveWallAvoidanceVector()
        {
            CampusFloorRoot floor = ResolveCurrentFloor();
            if (floor == null)
            {
                return Vector2.zero;
            }

            Vector2 push = Vector2.zero;
            Vector2 position = (Vector2)transform.position;
            Vector3Int centerCell = WorldToCell(position);
            float maxDistance = Mathf.Max(0.05f, wallAvoidanceDistance);
            float maxDistanceSqr = maxDistance * maxDistance;
            for (int y = centerCell.y - 1; y <= centerCell.y + 1; y++)
            {
                for (int x = centerCell.x - 1; x <= centerCell.x + 1; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (IsWalkableCell(floor, cell))
                    {
                        continue;
                    }

                    Vector2 nearest = new Vector2(
                        Mathf.Clamp(position.x, cell.x, cell.x + 1f),
                        Mathf.Clamp(position.y, cell.y, cell.y + 1f));
                    Vector2 delta = position - nearest;
                    if (delta.sqrMagnitude <= 0.0001f)
                    {
                        delta = position - new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                    }

                    float distanceSqr = delta.sqrMagnitude;
                    if (distanceSqr <= 0.0001f || distanceSqr > maxDistanceSqr)
                    {
                        continue;
                    }

                    float distance = Mathf.Sqrt(distanceSqr);
                    push += delta / distance * (1f - distance / maxDistance);
                }
            }

            return push;
        }

        private void TryOpenNearbyDoorPortal()
        {
            CampusFloorRoot floor = ResolveCurrentFloor();
            if (floor == null || pathCells.Count == 0 || pathCellIndex < 0 || pathCellIndex >= pathCells.Count)
            {
                return;
            }

            Vector3Int currentCell = WorldToCell(transform.position);
            Vector3Int waypointCell = pathCells[pathCellIndex];
            if (!IsDoorPortalCell(floor, currentCell) && !IsDoorPortalCell(floor, waypointCell))
            {
                return;
            }

            Vector2 position = (Vector2)transform.position;
            DoorScratch.Clear();
            GetDoorPortalComponents(floor, DoorScratch);
            for (int i = 0; i < DoorScratch.Count; i++)
            {
                CampusDoor3D door = DoorScratch[i];
                if (door == null || door.IsOpen)
                {
                    continue;
                }

                CampusPlacedObject placedObject = door.PlacedObject != null
                    ? door.PlacedObject
                    : door.GetComponentInParent<CampusPlacedObject>();
                if (placedObject == null)
                {
                    continue;
                }

                Vector3Int doorCell = placedObject.Cell;
                doorCell.z = 0;
                if (doorCell != currentCell && doorCell != waypointCell)
                {
                    continue;
                }

                if (!CanOpenDoor(door))
                {
                    continue;
                }

                float radius = Mathf.Max(0.45f, door.AutoOpenRadius + 0.2f);
                if (((Vector2)door.transform.position - position).sqrMagnitude > radius * radius)
                {
                    continue;
                }

                door.Open();
                ForceReplan();
                break;
            }

            DoorScratch.Clear();
        }

        private bool CanOpenDoor(CampusDoor3D door)
        {
            if (door == null || !door.RequiresPermission)
            {
                return true;
            }

            CampusCharacterRuntime runtime = GetComponent<CampusCharacterRuntime>() ?? GetComponentInParent<CampusCharacterRuntime>();
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return false;
            }

            if (door.AllowedCharacterIds != null)
            {
                string characterId = runtime.CharacterId;
                for (int i = 0; i < door.AllowedCharacterIds.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(door.AllowedCharacterIds[i]) &&
                        string.Equals(door.AllowedCharacterIds[i].Trim(), characterId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (door.AllowedRoles != null)
            {
                for (int i = 0; i < door.AllowedRoles.Count; i++)
                {
                    if (door.AllowedRoles[i] == data.Role)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private float DynamicOccupancyCost(Vector3Int cell, Vector3Int targetCell)
        {
            float cost = 0f;
            if (IsDestinationReservedByOther(cell))
            {
                cost += cell == targetCell ? 35f : 7f;
            }

            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                CampusGridNavigationAgent other = ActiveAgents[i];
                if (other == null || other == this || !other.isActiveAndEnabled || other.floorIndex != floorIndex)
                {
                    continue;
                }

                Vector3Int otherCell = WorldToCell(other.transform.position);
                int distance = Mathf.Abs(cell.x - otherCell.x) + Mathf.Abs(cell.y - otherCell.y);
                if (distance == 0)
                {
                    cost += cell == targetCell ? 26f : 9f;
                }
                else if (distance == 1)
                {
                    cost += 2.5f;
                }
            }

            return cost;
        }

        private bool IsOccupiedByOtherAgent(Vector3Int cell, float worldRadius)
        {
            Vector2 center = (Vector2)CellCenterToWorld(cell);
            float radiusSqr = Mathf.Max(0.05f, worldRadius) * Mathf.Max(0.05f, worldRadius);
            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                CampusGridNavigationAgent other = ActiveAgents[i];
                if (other == null || other == this || !other.isActiveAndEnabled || other.floorIndex != floorIndex)
                {
                    continue;
                }

                if (((Vector2)other.transform.position - center).sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void ReserveDestinationCell(Vector3Int cell)
        {
            string key = BuildDestinationReservationKey(floorIndex, cell);
            if (string.Equals(activeDestinationReservationKey, key, StringComparison.OrdinalIgnoreCase))
            {
                ReservedDestinationOwnerByKey[key] = GetInstanceID();
                return;
            }

            ReleaseDestinationReservation();
            activeDestinationReservationKey = key;
            ReservedDestinationOwnerByKey[key] = GetInstanceID();
        }

        private void ReleaseDestinationReservation()
        {
            if (string.IsNullOrWhiteSpace(activeDestinationReservationKey))
            {
                return;
            }

            if (ReservedDestinationOwnerByKey.TryGetValue(activeDestinationReservationKey, out int owner) &&
                owner == GetInstanceID())
            {
                ReservedDestinationOwnerByKey.Remove(activeDestinationReservationKey);
            }

            activeDestinationReservationKey = string.Empty;
        }

        private bool IsDestinationReservedByOther(Vector3Int cell)
        {
            string key = BuildDestinationReservationKey(floorIndex, cell);
            return ReservedDestinationOwnerByKey.TryGetValue(key, out int owner) && owner != GetInstanceID();
        }

        private static string BuildDestinationReservationKey(int floor, Vector3Int cell)
        {
            return floor + "|" + cell.x + "," + cell.y;
        }

        private static void RegisterActiveAgent(CampusGridNavigationAgent agent)
        {
            if (agent == null || ActiveAgents.Contains(agent))
            {
                return;
            }

            ActiveAgents.Add(agent);
        }

        private static void UnregisterActiveAgent(CampusGridNavigationAgent agent)
        {
            if (agent == null)
            {
                return;
            }

            ActiveAgents.Remove(agent);
        }

        private CampusFloorRoot ResolveCurrentFloor()
        {
            if (mapRoot == null)
            {
                mapRoot = FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            }

            return mapRoot != null ? mapRoot.GetFloor(Mathf.Max(1, floorIndex)) : null;
        }

        private static HashSet<Vector3Int> GetBlockedObjectCells(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return null;
            }

            if (!FloorBlockageCaches.TryGetValue(floor, out FloorBlockageCache cache))
            {
                cache = new FloorBlockageCache();
                FloorBlockageCaches[floor] = cache;
            }

            if (!cache.HasBuilt ||
                !Application.isPlaying ||
                Time.time >= cache.NextRefreshTime ||
                cache.PropsRoot != floor.PropsRoot)
            {
                RebuildFloorBlockageCache(floor, cache);
            }

            return cache.BlockedCells;
        }

        private static HashSet<Vector3Int> GetDoorPortalCells(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return null;
            }

            if (!FloorBlockageCaches.TryGetValue(floor, out FloorBlockageCache cache))
            {
                cache = new FloorBlockageCache();
                FloorBlockageCaches[floor] = cache;
            }

            if (!cache.HasBuilt ||
                !Application.isPlaying ||
                Time.time >= cache.NextRefreshTime ||
                cache.PropsRoot != floor.PropsRoot)
            {
                RebuildFloorBlockageCache(floor, cache);
            }

            return cache.DoorPortalCells;
        }

        private static void GetDoorPortalComponents(CampusFloorRoot floor, List<CampusDoor3D> output)
        {
            output.Clear();
            if (floor == null)
            {
                return;
            }

            if (!FloorBlockageCaches.TryGetValue(floor, out FloorBlockageCache cache))
            {
                cache = new FloorBlockageCache();
                FloorBlockageCaches[floor] = cache;
            }

            if (!cache.HasBuilt ||
                !Application.isPlaying ||
                Time.time >= cache.NextRefreshTime ||
                cache.PropsRoot != floor.PropsRoot)
            {
                RebuildFloorBlockageCache(floor, cache);
            }

            output.AddRange(cache.Doors);
        }

        private static void RebuildFloorBlockageCache(CampusFloorRoot floor, FloorBlockageCache cache)
        {
            cache.BlockedCells.Clear();
            cache.DoorPortalCells.Clear();
            cache.Doors.Clear();
            cache.PropsRoot = floor != null ? floor.PropsRoot : null;
            cache.HasBuilt = true;
            cache.NextRefreshTime = Application.isPlaying ? Time.time + FloorBlockageCacheRefreshSeconds : 0f;

            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            PlacedObjectScratch.Clear();
            floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true, PlacedObjectScratch);
            for (int i = 0; i < PlacedObjectScratch.Count; i++)
            {
                CampusPlacedObject placedObject = PlacedObjectScratch[i];
                if (placedObject == null)
                {
                    continue;
                }

                if (IsDoorPlacedObject(placedObject))
                {
                    AddFootprintCells(cache.DoorPortalCells, placedObject);
                    AddDoorComponents(cache.Doors, placedObject);
                    continue;
                }

                if (!placedObject.BlocksMovement)
                {
                    continue;
                }

                AddFootprintCells(cache.BlockedCells, placedObject);
            }

            PlacedObjectScratch.Clear();
        }

        private static void AddFootprintCells(HashSet<Vector3Int> cells, CampusPlacedObject placedObject)
        {
            if (cells == null || placedObject == null)
            {
                return;
            }

            Vector2Int footprint = placedObject.RotatedFootprintSize;
            Vector3Int origin = placedObject.Cell;
            origin.z = 0;
            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    cells.Add(new Vector3Int(origin.x + x, origin.y + y, 0));
                }
            }
        }

        private static bool IsDoorPlacedObject(CampusPlacedObject placedObject)
        {
            if (placedObject == null)
            {
                return false;
            }

            if (placedObject.GetComponentInChildren<CampusDoor3D>(true) != null)
            {
                return true;
            }

            string typeId = placedObject.EffectiveTypeId;
            if (!string.IsNullOrWhiteSpace(typeId) &&
                string.Equals(typeId.Trim(), "Door", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string objectId = placedObject.ObjectId;
            if (!string.IsNullOrWhiteSpace(objectId) &&
                (objectId.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 objectId.IndexOf("\u95e8", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }

        private static void AddDoorComponents(List<CampusDoor3D> doors, CampusPlacedObject placedObject)
        {
            if (doors == null || placedObject == null)
            {
                return;
            }

            CampusDoor3D[] foundDoors = placedObject.GetComponentsInChildren<CampusDoor3D>(true);
            for (int i = 0; i < foundDoors.Length; i++)
            {
                CampusDoor3D door = foundDoors[i];
                if (door != null && !doors.Contains(door))
                {
                    doors.Add(door);
                }
            }
        }

        private float PersonalCellCost(Vector3Int cell)
        {
            return PseudoRandom01(personalSeed + cell.x * 193 + cell.y * 389, 71) * 0.2f;
        }

        private float ResolvePersonalDelay(float minSeconds, float maxSeconds, int salt)
        {
            float min = Mathf.Max(0f, Mathf.Min(minSeconds, maxSeconds));
            float max = Mathf.Max(min, Mathf.Max(minSeconds, maxSeconds));
            return Mathf.Lerp(min, max, PseudoRandom01(personalSeed + Mathf.FloorToInt(Time.time * 23f), salt));
        }

        private static float Heuristic(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static void ReconstructPath(PathNode endNode, List<Vector3Int> output)
        {
            output.Clear();
            PathNode current = endNode;
            while (current != null)
            {
                output.Add(current.Cell);
                current = current.Parent;
            }

            output.Reverse();
        }

        private static Vector3Int WorldToCell(Vector3 worldPosition)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPosition.x),
                Mathf.FloorToInt(worldPosition.y),
                0);
        }

        private Vector3 CellCenterToWorld(Vector3Int cell)
        {
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, transform.position.z);
        }

        private static float PseudoRandom01(int seed, int salt)
        {
            unchecked
            {
                int value = seed;
                value ^= salt * 374761393;
                value = (value << 13) ^ value;
                int mixed = value * (value * value * 15731 + 789221) + 1376312589;
                return (mixed & 0x7fffffff) / 2147483647f;
            }
        }

        private sealed class FloorBlockageCache
        {
            public readonly HashSet<Vector3Int> BlockedCells = new HashSet<Vector3Int>();
            public readonly HashSet<Vector3Int> DoorPortalCells = new HashSet<Vector3Int>();
            public readonly List<CampusDoor3D> Doors = new List<CampusDoor3D>();
            public Transform PropsRoot;
            public float NextRefreshTime;
            public bool HasBuilt;
        }

        private sealed class PathNode
        {
            public PathNode(Vector3Int cell, float costFromStart, float estimatedCost, PathNode parent)
            {
                Cell = cell;
                CostFromStart = costFromStart;
                EstimatedCost = estimatedCost;
                Parent = parent;
            }

            public Vector3Int Cell { get; }
            public float CostFromStart { get; set; }
            public float EstimatedCost { get; }
            public float TotalCost => CostFromStart + EstimatedCost;
            public PathNode Parent { get; set; }
        }
    }
}
