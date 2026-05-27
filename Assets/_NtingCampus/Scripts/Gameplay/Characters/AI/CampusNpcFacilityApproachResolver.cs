using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcFacilityApproachResolver
    {
        public static Vector3 ResolveApproachPosition(
            CampusNpcAiRuntime npc,
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record)
        {
            Vector3 facilityPosition = CampusNpcFacilitySelector.PositionOf(record);
            if (record == null || RequiresExactNavigation(record))
            {
                return facilityPosition;
            }

            CampusPlacedObject placedObject = record.PlacedObject;
            CampusFloorRoot floor = placedObject != null
                ? placedObject.GetComponentInParent<CampusFloorRoot>()
                : null;
            if (floor == null || floor.Grid == null)
            {
                return facilityPosition;
            }

            Vector3Int facilityCell = record.Cell;
            facilityCell.z = 0;
            if (!placedObject.BlocksMovement &&
                CampusGridNavigationAgent.IsNavigationCellWalkable(floor, facilityCell))
            {
                return facilityPosition;
            }

            if (TryFindApproachCell(
                    floor,
                    room,
                    BuildFootprintBounds(placedObject),
                    ResolveActorCell(npc, floor, facilityCell),
                    3,
                    true,
                    out Vector3Int approachCell))
            {
                Vector3 position = floor.Grid.GetCellCenterWorld(approachCell);
                position.z = facilityPosition.z;
                return position;
            }

            return facilityPosition;
        }

        public static Vector3 ResolveCellApproachPosition(
            CampusNpcAiRuntime npc,
            CampusFloorRoot floor,
            CampusGameplayRoom room,
            Vector3Int targetCell,
            float z,
            int maxRadius,
            bool excludeTargetCell)
        {
            targetCell.z = 0;
            if (floor == null || floor.Grid == null)
            {
                return new Vector3(targetCell.x + 0.5f, targetCell.y + 0.5f, z);
            }

            if (TryFindApproachCell(
                    floor,
                    room,
                    new BoundsInt(targetCell, Vector3Int.one),
                    ResolveActorCell(npc, floor, targetCell),
                    maxRadius,
                    excludeTargetCell,
                    out Vector3Int approachCell))
            {
                Vector3 position = floor.Grid.GetCellCenterWorld(approachCell);
                position.z = z;
                return position;
            }

            return new Vector3(targetCell.x + 0.5f, targetCell.y + 0.5f, z);
        }

        public static bool RequiresExactNavigation(CampusGameplayRoom.FacilityRecord record)
        {
            if (record == null)
            {
                return false;
            }

            switch (record.FacilityType)
            {
                case CampusFacilityType.WorkerStandPoint:
                case CampusFacilityType.WaitingPoint:
                case CampusFacilityType.PickupPoint:
                case CampusFacilityType.DropPoint:
                case CampusFacilityType.SeatPoint:
                    return true;
                default:
                    return false;
            }
        }

        private static Vector3Int ResolveActorCell(
            CampusNpcAiRuntime npc,
            CampusFloorRoot floor,
            Vector3Int fallbackCell)
        {
            Vector3Int cell = npc != null && npc.Runtime != null && floor != null && floor.Grid != null
                ? floor.Grid.WorldToCell(npc.Runtime.transform.position)
                : fallbackCell;
            cell.z = 0;
            return cell;
        }

        private static bool TryFindApproachCell(
            CampusFloorRoot floor,
            CampusGameplayRoom room,
            BoundsInt targetBounds,
            Vector3Int actorCell,
            int maxRadius,
            bool excludeTargetBounds,
            out Vector3Int approachCell)
        {
            approachCell = targetBounds.position;
            if (floor == null || targetBounds.size.x <= 0 || targetBounds.size.y <= 0)
            {
                return false;
            }

            float bestScore = float.PositiveInfinity;
            for (int radius = 0; radius <= Mathf.Max(0, maxRadius); radius++)
            {
                for (int y = targetBounds.yMin - radius; y < targetBounds.yMax + radius; y++)
                {
                    for (int x = targetBounds.xMin - radius; x < targetBounds.xMax + radius; x++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (radius > 0 && IsInsideExpandedInterior(cell, targetBounds, radius))
                        {
                            continue;
                        }

                        if ((excludeTargetBounds && targetBounds.Contains(cell)) ||
                            (room != null && !room.ContainsCell(cell)) ||
                            !CampusGridNavigationAgent.IsNavigationCellWalkable(floor, cell))
                        {
                            continue;
                        }

                        float score = DistanceToBounds(cell, targetBounds) * 3f +
                                      Mathf.Abs(cell.x - actorCell.x) +
                                      Mathf.Abs(cell.y - actorCell.y);
                        if (score >= bestScore)
                        {
                            continue;
                        }

                        approachCell = cell;
                        bestScore = score;
                    }
                }

                if (bestScore < float.PositiveInfinity)
                {
                    return true;
                }
            }

            return false;
        }

        private static BoundsInt BuildFootprintBounds(CampusPlacedObject placedObject)
        {
            Vector3Int origin = placedObject != null ? placedObject.Cell : Vector3Int.zero;
            origin.z = 0;
            Vector2Int footprint = placedObject != null
                ? placedObject.RotatedFootprintSize
                : Vector2Int.one;
            return new BoundsInt(
                origin,
                new Vector3Int(
                    Mathf.Max(1, footprint.x),
                    Mathf.Max(1, footprint.y),
                    1));
        }

        private static bool IsInsideExpandedInterior(Vector3Int cell, BoundsInt footprint, int radius)
        {
            return cell.x > footprint.xMin - radius &&
                   cell.x < footprint.xMax + radius - 1 &&
                   cell.y > footprint.yMin - radius &&
                   cell.y < footprint.yMax + radius - 1;
        }

        private static float DistanceToBounds(Vector3Int cell, BoundsInt bounds)
        {
            int dx = cell.x < bounds.xMin
                ? bounds.xMin - cell.x
                : cell.x >= bounds.xMax ? cell.x - bounds.xMax + 1 : 0;
            int dy = cell.y < bounds.yMin
                ? bounds.yMin - cell.y
                : cell.y >= bounds.yMax ? cell.y - bounds.yMax + 1 : 0;
            return dx + dy;
        }
    }
}
