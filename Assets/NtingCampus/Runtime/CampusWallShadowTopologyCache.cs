using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    internal static class CampusWallShadowTopologyCache
    {
        private const float WallBottomHalfWidth = 0.330f;
        private const float WallCellHalf = 0.5f;
        private const float EdgeMergeTolerance = 0.0005f;

        private static readonly Dictionary<int, FloorTopologyData> floorTopologies = new Dictionary<int, FloorTopologyData>();

        internal sealed class FloorTopologyData
        {
            internal readonly Dictionary<Vector2Int, List<WallShadowEdge>> ChunkEdges = new Dictionary<Vector2Int, List<WallShadowEdge>>();
            internal readonly HashSet<Vector2Int> ActiveChunks = new HashSet<Vector2Int>();
            internal readonly HashSet<Vector2Int> LastUpdatedChunks = new HashSet<Vector2Int>();
            private readonly ChunkBuildContext scratchContext = new ChunkBuildContext();
            private readonly List<List<WallShadowEdge>> edgeListPool = new List<List<WallShadowEdge>>();

            internal int Version { get; private set; }
            internal bool Built { get; private set; }

            internal void MarkUpdated()
            {
                Built = true;
                Version++;
            }

            internal void Clear()
            {
                foreach (List<WallShadowEdge> edges in ChunkEdges.Values)
                {
                    ReturnEdgeList(edges);
                }

                ChunkEdges.Clear();
                ActiveChunks.Clear();
                LastUpdatedChunks.Clear();
                Built = false;
                Version++;
            }

            internal List<WallShadowEdge> RentEdgeList()
            {
                int lastIndex = edgeListPool.Count - 1;
                if (lastIndex >= 0)
                {
                    List<WallShadowEdge> edges = edgeListPool[lastIndex];
                    edgeListPool.RemoveAt(lastIndex);
                    edges.Clear();
                    return edges;
                }

                return new List<WallShadowEdge>(128);
            }

            internal void ReturnEdgeList(List<WallShadowEdge> edges)
            {
                if (edges == null)
                {
                    return;
                }

                edges.Clear();
                edgeListPool.Add(edges);
            }

            internal ChunkBuildContext GetScratchContext()
            {
                scratchContext.Clear();
                return scratchContext;
            }
        }

        internal readonly struct WallShadowEdge
        {
            internal readonly Vector3 WorldA;
            internal readonly Vector3 WorldB;
            internal readonly Vector2 Center;
            internal readonly Vector2 Normal;

            internal WallShadowEdge(Vector3 worldA, Vector3 worldB, Vector2 normal)
            {
                WorldA = worldA;
                WorldB = worldB;
                Center = ((Vector2)worldA + (Vector2)worldB) * 0.5f;
                Normal = normal;
            }
        }

        internal readonly struct ShadowSourceRect
        {
            internal readonly float MinX;
            internal readonly float MinY;
            internal readonly float MaxX;
            internal readonly float MaxY;

            internal ShadowSourceRect(float minX, float minY, float maxX, float maxY)
            {
                MinX = Mathf.Min(minX, maxX);
                MinY = Mathf.Min(minY, maxY);
                MaxX = Mathf.Max(minX, maxX);
                MaxY = Mathf.Max(minY, maxY);
            }

            internal bool SameAs(ShadowSourceRect other)
            {
                return Approximately(MinX, other.MinX) &&
                       Approximately(MinY, other.MinY) &&
                       Approximately(MaxX, other.MaxX) &&
                       Approximately(MaxY, other.MaxY);
            }
        }

        internal sealed class ChunkBuildContext
        {
            internal readonly List<ShadowSourceRect> SourceRects = new List<ShadowSourceRect>(96);
            internal readonly List<Vector2> CoveredIntervals = new List<Vector2>(96);
            internal readonly List<WallShadowEdge> Edges = new List<WallShadowEdge>(128);

            internal void Clear()
            {
                SourceRects.Clear();
                CoveredIntervals.Clear();
                Edges.Clear();
            }
        }

        internal static FloorTopologyData EnsureBuilt(CampusFloorRoot floor)
        {
            FloorTopologyData data = GetOrCreateData(floor);
            if (!data.Built)
            {
                RebuildFloor(floor);
            }

            return data;
        }

        internal static FloorTopologyData RebuildFloor(CampusFloorRoot floor)
        {
            FloorTopologyData data = GetOrCreateData(floor);
            Tilemap wallLogic = floor != null ? CampusWallTileUtility.GetWallLogicTilemap(floor) : null;
            if (floor == null || wallLogic == null)
            {
                data.Clear();
                return data;
            }

            wallLogic.CompressBounds();
            BoundsInt bounds = wallLogic.cellBounds;
            foreach (List<WallShadowEdge> edges in data.ChunkEdges.Values)
            {
                data.ReturnEdgeList(edges);
            }

            data.ChunkEdges.Clear();
            data.LastUpdatedChunks.Clear();
            data.ActiveChunks.Clear();

            if (bounds.size.x <= 0 || bounds.size.y <= 0)
            {
                data.MarkUpdated();
                return data;
            }

            int minChunkX = CampusWallChunkSystem.GetChunkCoord(bounds.xMin);
            int maxChunkX = CampusWallChunkSystem.GetChunkCoord(bounds.xMax - 1);
            int minChunkY = CampusWallChunkSystem.GetChunkCoord(bounds.yMin);
            int maxChunkY = CampusWallChunkSystem.GetChunkCoord(bounds.yMax - 1);

            for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
            {
                for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                {
                    RebuildChunk(data, wallLogic, chunkX, chunkY);
                    data.LastUpdatedChunks.Add(new Vector2Int(chunkX, chunkY));
                }
            }

            data.MarkUpdated();
            return data;
        }

        internal static FloorTopologyData RebuildCells(CampusFloorRoot floor, IReadOnlyList<Vector3Int> changedCells)
        {
            if (changedCells == null || changedCells.Count == 0)
            {
                return EnsureBuilt(floor);
            }

            HashSet<Vector2Int> dirtyChunks = CampusWallChunkSystem.CollectAffectedChunks(changedCells);
            return RebuildChunks(floor, dirtyChunks);
        }

        internal static FloorTopologyData RebuildChunks(CampusFloorRoot floor, IReadOnlyCollection<Vector2Int> dirtyChunks)
        {
            return RebuildChunks(floor, dirtyChunks, null);
        }

        internal static FloorTopologyData RebuildChunks(CampusFloorRoot floor, IReadOnlyCollection<Vector2Int> dirtyChunks, CampusWallChunkBuildData buildData)
        {
            FloorTopologyData data = GetOrCreateData(floor);
            Tilemap wallLogic = floor != null ? CampusWallTileUtility.GetWallLogicTilemap(floor) : null;
            if (floor == null || wallLogic == null)
            {
                data.Clear();
                return data;
            }

            if (dirtyChunks == null || dirtyChunks.Count == 0)
            {
                return EnsureBuilt(floor);
            }

            if (!data.Built)
            {
                return RebuildFloor(floor);
            }

            data.LastUpdatedChunks.Clear();

            foreach (Vector2Int chunk in dirtyChunks)
            {
                RebuildChunk(data, wallLogic, chunk.x, chunk.y, buildData);
                data.LastUpdatedChunks.Add(chunk);
            }

            data.MarkUpdated();
            return data;
        }

        internal static void ClearForFloor(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            floorTopologies.Remove(floor.GetInstanceID());
        }

        private static FloorTopologyData GetOrCreateData(CampusFloorRoot floor)
        {
            int key = floor != null ? floor.GetInstanceID() : 0;
            if (!floorTopologies.TryGetValue(key, out FloorTopologyData data))
            {
                data = new FloorTopologyData();
                floorTopologies.Add(key, data);
            }

            return data;
        }

        private static void RebuildChunk(FloorTopologyData data, Tilemap wallLogic, int chunkX, int chunkY)
        {
            RebuildChunk(data, wallLogic, chunkX, chunkY, null);
        }

        private static void RebuildChunk(FloorTopologyData data, Tilemap wallLogic, int chunkX, int chunkY, CampusWallChunkBuildData buildData)
        {
            ChunkBuildContext context = data.GetScratchContext();
            Vector3 cellSize = wallLogic.layoutGrid != null ? wallLogic.layoutGrid.cellSize : Vector3.one;
            float sourceHalfX = Mathf.Min(WallBottomHalfWidth, Mathf.Max(0.01f, WallBottomHalfWidth));
            float sourceHalfY = Mathf.Min(WallBottomHalfWidth, Mathf.Max(0.01f, WallBottomHalfWidth));
            int minX = chunkX * CampusWallChunkSystem.ChunkSize - 1;
            int minY = chunkY * CampusWallChunkSystem.ChunkSize - 1;
            int maxX = (chunkX + 1) * CampusWallChunkSystem.ChunkSize;
            int maxY = (chunkY + 1) * CampusWallChunkSystem.ChunkSize;

            if (buildData != null)
            {
                for (int sourceChunkY = chunkY - 1; sourceChunkY <= chunkY + 1; sourceChunkY++)
                {
                    for (int sourceChunkX = chunkX - 1; sourceChunkX <= chunkX + 1; sourceChunkX++)
                    {
                        if (!buildData.TryGetChunk(new Vector2Int(sourceChunkX, sourceChunkY), out CampusWallChunkBuildData.ChunkData chunkData))
                        {
                            continue;
                        }

                        List<CampusWallChunkBuildData.CellData> cells = chunkData.Cells;
                        for (int i = 0; i < cells.Count; i++)
                        {
                            CampusWallChunkBuildData.CellData cellData = cells[i];
                            Vector3Int cell = cellData.Cell;
                            if (cell.x < minX || cell.x > maxX || cell.y < minY || cell.y > maxY)
                            {
                                continue;
                            }

                            AddWallBaseSourceRects(context.SourceRects, cellData.WorldCenter, cellSize, cellData.ConnectionMask, sourceHalfX, sourceHalfY);
                        }
                    }
                }
            }
            else
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (!wallLogic.HasTile(cell))
                        {
                            continue;
                        }

                        Vector3 center = wallLogic.GetCellCenterWorld(cell);
                        int connectionMask = CampusWallTileUtility.GetConnectionMask(wallLogic, cell);
                        AddWallBaseSourceRects(context.SourceRects, center, cellSize, connectionMask, sourceHalfX, sourceHalfY);
                    }
                }
            }

            for (int i = 0; i < context.SourceRects.Count; i++)
            {
                AddSourceRectVisibleEdges(context, wallLogic, chunkX, chunkY, context.SourceRects[i]);
            }

            Vector2Int chunkKey = new Vector2Int(chunkX, chunkY);
            if (context.Edges.Count == 0)
            {
                if (data.ChunkEdges.TryGetValue(chunkKey, out List<WallShadowEdge> emptyEdges))
                {
                    data.ReturnEdgeList(emptyEdges);
                    data.ChunkEdges.Remove(chunkKey);
                }

                data.ActiveChunks.Remove(chunkKey);
                return;
            }

            if (!data.ChunkEdges.TryGetValue(chunkKey, out List<WallShadowEdge> chunkEdges))
            {
                chunkEdges = data.RentEdgeList();
                data.ChunkEdges[chunkKey] = chunkEdges;
            }
            else
            {
                chunkEdges.Clear();
            }

            chunkEdges.AddRange(context.Edges);
            data.ActiveChunks.Add(chunkKey);
        }

        private static void AddWallBaseSourceRects(List<ShadowSourceRect> rects, Vector3 center, Vector3 cellSize, int connectionMask, float sourceHalfX, float sourceHalfY)
        {
            bool northConnected = (connectionMask & CampusWallTileUtility.NorthMask) != 0;
            bool eastConnected = (connectionMask & CampusWallTileUtility.EastMask) != 0;
            bool southConnected = (connectionMask & CampusWallTileUtility.SouthMask) != 0;
            bool westConnected = (connectionMask & CampusWallTileUtility.WestMask) != 0;

            int connectionCount =
                (northConnected ? 1 : 0) +
                (eastConnected ? 1 : 0) +
                (southConnected ? 1 : 0) +
                (westConnected ? 1 : 0);

            bool northArm = northConnected;
            bool eastArm = eastConnected;
            bool southArm = southConnected;
            bool westArm = westConnected;

            if (connectionCount == 0)
            {
                eastArm = true;
                westArm = true;
            }
            else if (connectionCount == 1)
            {
                if (northConnected || southConnected)
                {
                    northArm = true;
                    southArm = true;
                }
                else
                {
                    eastArm = true;
                    westArm = true;
                }
            }

            AddSourceRect(rects, center, cellSize, -sourceHalfX, -sourceHalfY, sourceHalfX, sourceHalfY);
            if (eastArm)
            {
                AddSourceRect(rects, center, cellSize, sourceHalfX, -sourceHalfY, WallCellHalf, sourceHalfY);
            }

            if (westArm)
            {
                AddSourceRect(rects, center, cellSize, -WallCellHalf, -sourceHalfY, -sourceHalfX, sourceHalfY);
            }

            if (northArm)
            {
                AddSourceRect(rects, center, cellSize, -sourceHalfX, sourceHalfY, sourceHalfX, WallCellHalf);
            }

            if (southArm)
            {
                AddSourceRect(rects, center, cellSize, -sourceHalfX, -WallCellHalf, sourceHalfX, -sourceHalfY);
            }
        }

        private static void AddSourceRect(List<ShadowSourceRect> rects, Vector3 center, Vector3 cellSize, float minX, float minY, float maxX, float maxY)
        {
            float scaleX = Mathf.Abs(cellSize.x);
            float scaleY = Mathf.Abs(cellSize.y);
            rects.Add(new ShadowSourceRect(
                center.x + minX * scaleX,
                center.y + minY * scaleY,
                center.x + maxX * scaleX,
                center.y + maxY * scaleY));
            RemoveDuplicateSourceRectAtEnd(rects);
        }

        private static void RemoveDuplicateSourceRectAtEnd(List<ShadowSourceRect> rects)
        {
            int lastIndex = rects.Count - 1;
            if (lastIndex <= 0)
            {
                return;
            }

            ShadowSourceRect last = rects[lastIndex];
            for (int i = 0; i < lastIndex; i++)
            {
                if (rects[i].SameAs(last))
                {
                    rects.RemoveAt(lastIndex);
                    return;
                }
            }
        }

        private static void AddSourceRectVisibleEdges(ChunkBuildContext context, Tilemap wallLogic, int ownerChunkX, int ownerChunkY, ShadowSourceRect rect)
        {
            AddEdgeSegments(context, wallLogic, ownerChunkX, ownerChunkY, rect.MinX, rect.MaxX, rect.MaxY, true, new Vector2(0f, 1f), rect);
            AddEdgeSegments(context, wallLogic, ownerChunkX, ownerChunkY, rect.MaxX, rect.MinX, rect.MinY, true, new Vector2(0f, -1f), rect);
            AddEdgeSegments(context, wallLogic, ownerChunkX, ownerChunkY, rect.MinY, rect.MaxY, rect.MaxX, false, new Vector2(1f, 0f), rect);
            AddEdgeSegments(context, wallLogic, ownerChunkX, ownerChunkY, rect.MaxY, rect.MinY, rect.MinX, false, new Vector2(-1f, 0f), rect);
        }

        private static void AddEdgeSegments(ChunkBuildContext context, Tilemap wallLogic, int ownerChunkX, int ownerChunkY, float start, float end, float fixedCoord, bool horizontal, Vector2 normal, ShadowSourceRect owner)
        {
            float edgeStart = Mathf.Min(start, end);
            float edgeEnd = Mathf.Max(start, end);
            context.CoveredIntervals.Clear();

            for (int i = 0; i < context.SourceRects.Count; i++)
            {
                ShadowSourceRect other = context.SourceRects[i];
                if (other.SameAs(owner))
                {
                    continue;
                }

                if (TryGetCoveredInterval(other, fixedCoord, horizontal, normal, edgeStart, edgeEnd, out Vector2 interval))
                {
                    context.CoveredIntervals.Add(interval);
                }
            }

            if (context.CoveredIntervals.Count == 0)
            {
                AddEdgeSegment(context.Edges, wallLogic, ownerChunkX, ownerChunkY, edgeStart, edgeEnd, fixedCoord, horizontal, normal);
                return;
            }

            context.CoveredIntervals.Sort(CompareIntervals);
            float cursor = edgeStart;
            for (int i = 0; i < context.CoveredIntervals.Count; i++)
            {
                Vector2 interval = context.CoveredIntervals[i];
                if (interval.y <= cursor + EdgeMergeTolerance)
                {
                    cursor = Mathf.Max(cursor, interval.y);
                    continue;
                }

                if (interval.x > cursor + EdgeMergeTolerance)
                {
                    AddEdgeSegment(context.Edges, wallLogic, ownerChunkX, ownerChunkY, cursor, Mathf.Min(interval.x, edgeEnd), fixedCoord, horizontal, normal);
                }

                cursor = Mathf.Max(cursor, interval.y);
                if (cursor >= edgeEnd - EdgeMergeTolerance)
                {
                    break;
                }
            }

            if (cursor < edgeEnd - EdgeMergeTolerance)
            {
                AddEdgeSegment(context.Edges, wallLogic, ownerChunkX, ownerChunkY, cursor, edgeEnd, fixedCoord, horizontal, normal);
            }
        }

        private static bool TryGetCoveredInterval(ShadowSourceRect other, float fixedCoord, bool horizontal, Vector2 normal, float edgeStart, float edgeEnd, out Vector2 interval)
        {
            interval = Vector2.zero;
            bool sharesOppositeSide;
            float otherStart;
            float otherEnd;

            if (horizontal)
            {
                sharesOppositeSide = normal.y > 0f
                    ? Approximately(other.MinY, fixedCoord)
                    : Approximately(other.MaxY, fixedCoord);
                otherStart = other.MinX;
                otherEnd = other.MaxX;
            }
            else
            {
                sharesOppositeSide = normal.x > 0f
                    ? Approximately(other.MinX, fixedCoord)
                    : Approximately(other.MaxX, fixedCoord);
                otherStart = other.MinY;
                otherEnd = other.MaxY;
            }

            if (!sharesOppositeSide)
            {
                return false;
            }

            float overlapStart = Mathf.Max(edgeStart, otherStart);
            float overlapEnd = Mathf.Min(edgeEnd, otherEnd);
            if (overlapEnd <= overlapStart + EdgeMergeTolerance)
            {
                return false;
            }

            interval = new Vector2(overlapStart, overlapEnd);
            return true;
        }

        private static void AddEdgeSegment(List<WallShadowEdge> edges, Tilemap wallLogic, int ownerChunkX, int ownerChunkY, float start, float end, float fixedCoord, bool horizontal, Vector2 normal)
        {
            if (end <= start + EdgeMergeTolerance)
            {
                return;
            }

            Vector3 worldA;
            Vector3 worldB;
            if (horizontal)
            {
                worldA = new Vector3(start, fixedCoord, 0f);
                worldB = new Vector3(end, fixedCoord, 0f);
            }
            else
            {
                worldA = new Vector3(fixedCoord, start, 0f);
                worldB = new Vector3(fixedCoord, end, 0f);
            }

            Vector2 center = ((Vector2)worldA + (Vector2)worldB) * 0.5f;
            Vector3Int ownerCell = wallLogic.WorldToCell(center);
            if (CampusWallChunkSystem.GetChunkCoord(ownerCell.x) != ownerChunkX || CampusWallChunkSystem.GetChunkCoord(ownerCell.y) != ownerChunkY)
            {
                return;
            }

            edges.Add(new WallShadowEdge(worldA, worldB, normal));
        }

        private static int CompareIntervals(Vector2 a, Vector2 b)
        {
            return a.x.CompareTo(b.x);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= EdgeMergeTolerance;
        }

    }
}
