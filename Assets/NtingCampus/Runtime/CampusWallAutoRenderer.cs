using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Rebuilds wall visuals from logical wall cells. The logic tilemap remains authoritative;
    /// the final presentation is a generated trapezoid mesh layer.
    /// </summary>
    public static class CampusWallAutoRenderer
    {
        private static readonly bool enableWallSelfShadowCasters = false;
        private static readonly bool useProjectedWallShadowMesh = true;
        private static readonly ProfilerMarker RebuildFloorMarker = new ProfilerMarker("CampusWallAutoRenderer.RebuildFloor");
        private static readonly ProfilerMarker RebuildChangedCellsMarker = new ProfilerMarker("CampusWallAutoRenderer.RebuildChangedCells");
        private static readonly ProfilerMarker RebuildMeshMarker = new ProfilerMarker("CampusWallAutoRenderer.RebuildMesh");
        private static readonly ProfilerMarker RebuildCollisionMarker = new ProfilerMarker("CampusWallAutoRenderer.RebuildCollision");
        private static readonly ProfilerMarker MarkShadowTopologyMarker = new ProfilerMarker("CampusWallAutoRenderer.MarkShadowTopology");
        private static readonly ProfilerMarker ApplyDebugViewMarker = new ProfilerMarker("CampusWallAutoRenderer.ApplyDebugView");

        public static void RebuildAll(CampusMapRoot root, CampusWallRenderProfile profile)
        {
            RebuildAll(root, null, profile);
        }

        public static void RebuildAll(CampusMapRoot root, CampusWallVisualCatalog catalog, CampusWallRenderProfile fallbackProfile)
        {
            if (root == null)
            {
                return;
            }

            root.RebuildFloorReferences();
            for (int i = 0; i < root.Floors.Count; i++)
            {
                RebuildFloor(root.Floors[i], catalog, fallbackProfile);
            }
        }

        public static void RebuildFloor(CampusFloorRoot floor, CampusWallRenderProfile profile)
        {
            RebuildFloor(floor, null, profile);
        }

        public static void RebuildFloor(CampusFloorRoot floor, CampusWallVisualCatalog catalog, CampusWallRenderProfile fallbackProfile)
        {
            using (RebuildFloorMarker.Auto())
            {
            if (floor == null)
            {
                return;
            }

            bool rebuilt;
            using (RebuildMeshMarker.Auto())
            {
                rebuilt = CampusWallMeshRenderer.RebuildFloor(floor, catalog, fallbackProfile);
            }

            if (rebuilt)
            {
                using (RebuildCollisionMarker.Auto())
                {
                    CampusWallCollisionRenderer.RebuildFloor(floor);
                }
                ClearLegacyTileVisuals(floor);
                if (enableWallSelfShadowCasters)
                {
                    CampusDynamicShadowUtility.RebuildWallShadowCasters(floor);
                }
                else
                {
                    CampusDynamicShadowUtility.ClearWallShadowCasters(floor);
                }

                if (useProjectedWallShadowMesh)
                {
                    CampusDynamicShadowUtility.ClearWallGroundShadowCasters(floor);
                    using (MarkShadowTopologyMarker.Auto())
                    {
                        CampusWallShadowTopologyCache.RebuildFloor(floor);
                        CampusProjectedWallShadowRenderer renderer = CampusProjectedWallShadowRenderer.EnsureForFloor(floor, false);
                        if (renderer != null)
                        {
                            renderer.RebuildFromWallLogicIfNeeded();
                        }

                        NtingWallPointShadowRenderer pointRenderer = NtingWallPointShadowRenderer.EnsureForFloor(floor, false);
                        if (pointRenderer != null)
                        {
                            pointRenderer.RebuildBaseRectsIfNeeded();
                        }
                    }
                }
                else
                {
                    CampusProjectedWallShadowRenderer.ClearForFloor(floor);
                    CampusDynamicShadowUtility.RebuildWallGroundShadowCasters(floor);
                }

                floor.MarkUsedBoundsDirty();
                floor.RefreshUsedBounds();
            }
            }
        }

        public static void RebuildChangedCells(
            CampusFloorRoot floor,
            IReadOnlyList<Vector3Int> changedCells,
            CampusWallVisualCatalog catalog,
            CampusWallRenderProfile fallbackProfile)
        {
            using (RebuildChangedCellsMarker.Auto())
            {
            if (floor == null)
            {
                return;
            }

            bool rebuilt;
            HashSet<Vector2Int> dirtyChunks = CampusWallChunkSystem.CollectAffectedChunks(changedCells);
            HashSet<Vector2Int> buildDataChunks = CampusWallChunkSystem.ExpandChunks(dirtyChunks, 1);
            CampusWallChunkBuildData buildData = CampusWallChunkBuildData.Capture(CampusWallTileUtility.GetWallLogicTilemap(floor), buildDataChunks);
            using (RebuildMeshMarker.Auto())
            {
                rebuilt = CampusWallMeshRenderer.RebuildChunks(floor, catalog, fallbackProfile, dirtyChunks, buildData);
            }

            if (rebuilt)
            {
                using (RebuildCollisionMarker.Auto())
                {
                    CampusWallCollisionRenderer.RebuildChunks(floor, dirtyChunks, buildData);
                }
                if (useProjectedWallShadowMesh)
                {
                    using (MarkShadowTopologyMarker.Auto())
                    {
                        CampusWallShadowTopologyCache.RebuildChunks(floor, dirtyChunks, buildData);
                        CampusProjectedWallShadowRenderer renderer = CampusProjectedWallShadowRenderer.EnsureForFloor(floor, false);
                        if (renderer != null)
                        {
                            renderer.RebuildFromWallLogicIfNeeded();
                        }

                        NtingWallPointShadowRenderer pointRenderer = NtingWallPointShadowRenderer.EnsureForFloor(floor, false);
                        if (pointRenderer != null)
                        {
                            pointRenderer.RebuildBaseRectsIfNeeded();
                        }
                    }
                }
                else
                {
                    CampusProjectedWallShadowRenderer.ClearForFloor(floor);
                    CampusDynamicShadowUtility.RebuildWallGroundShadowCasters(floor);
                }

                floor.MarkUsedBoundsDirty();
            }
            }
        }

        public static void ClearVisualLayers(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            ClearLegacyTileVisuals(floor);
            CampusWallMeshRenderer.ClearFloor(floor);
            CampusWallCollisionRenderer.ClearFloor(floor);
            CampusDynamicShadowUtility.ClearWallShadowCasters(floor);
            CampusDynamicShadowUtility.ClearWallGroundShadowCasters(floor);
            CampusWallShadowTopologyCache.ClearForFloor(floor);
            CampusProjectedWallShadowRenderer.ClearForFloor(floor);
            NtingWallPointShadowRenderer.ClearForFloor(floor);
        }

        public static void ApplyDebugView(CampusFloorRoot floor, CampusWallDebugView view)
        {
            using (ApplyDebugViewMarker.Auto())
            {
            if (floor == null)
            {
                return;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            bool hasWallMeshVisuals = HasWallMeshVisuals(floor);
            bool showLogicFallback = view == CampusWallDebugView.ShowFinalWallVisuals &&
                                     !hasWallMeshVisuals &&
                                     HasWallLogicTiles(wallLogic);
            bool showLogic = view == CampusWallDebugView.ShowWallLogicOnly || view == CampusWallDebugView.ShowBoth || showLogicFallback;
            bool showVisuals = view == CampusWallDebugView.ShowFinalWallVisuals || view == CampusWallDebugView.ShowBoth;
            CampusWallTileUtility.SetTilemapVisible(wallLogic, showLogic);
            CampusWallTileUtility.SetTilemapVisible(floor.WallCapTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallFaceTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallSideTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallOverlayTilemap, false);
            CampusWallMeshRenderer.SetVisible(floor, showVisuals);
            }
        }

        public static void ApplyFinalWallVisualState(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            CampusWallTileUtility.SetTilemapVisible(wallLogic, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallCapTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallFaceTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallSideTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallOverlayTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.CollisionDebugTilemap, false);
            CampusWallMeshRenderer.SetVisible(floor, true);
        }

        private static bool HasWallLogicTiles(Tilemap wallLogic)
        {
            if (wallLogic == null)
            {
                return false;
            }

            BoundsInt bounds = wallLogic.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (wallLogic.HasTile(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasWallMeshVisuals(CampusFloorRoot floor)
        {
            return floor != null &&
                   floor.WallMeshRoot != null &&
                   floor.WallMeshRoot.GetComponentInChildren<CampusWallMeshVisual>(true) != null;
        }

        private static void ClearLegacyTileVisuals(CampusFloorRoot floor)
        {
            ClearTilemap(floor.WallCapTilemap);
            ClearTilemap(floor.WallFaceTilemap);
            ClearTilemap(floor.WallSideTilemap);
            ClearTilemap(floor.WallOverlayTilemap);
            CampusDynamicShadowUtility.RemoveFixedWallShadowTilemaps(floor);
        }

        private static void ClearTilemap(Tilemap tilemap)
        {
            if (tilemap != null)
            {
                tilemap.ClearAllTiles();
            }
        }
    }

    public static class CampusWallCollisionRenderer
    {
        private const string CollisionRootName = "墙体碰撞";
        private const float WallHalfWidth = 0.33f;
        private const float WallHalfLength = 0.5f;
        private const float MergeTolerance = 0.0001f;
        private static readonly List<Rect> collisionMergeScratch = new List<Rect>(128);

        private static readonly Dictionary<int, WallCollisionRegistry> registries = new Dictionary<int, WallCollisionRegistry>();

        public static void EnsureForFloor(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            RemoveLegacyWallTilemapColliders(wallLogic);
            EnsureCollisionRoot(floor);
        }

        public static bool RebuildFloor(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return false;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                ClearFloor(floor);
                return false;
            }

            Transform root = EnsureCollisionRoot(floor);
            if (root == null)
            {
                return false;
            }

            RemoveLegacyWallTilemapColliders(wallLogic);
            WallCollisionRegistry registry = GetOrCreateRegistry(root);
            registry.ClearAll();

            HashSet<Vector2Int> chunks = CampusWallChunkSystem.CollectActiveChunks(wallLogic);
            HashSet<Vector2Int> buildDataChunks = CampusWallChunkSystem.ExpandChunks(chunks, 1);
            CampusWallChunkBuildData buildData = CampusWallChunkBuildData.Capture(wallLogic, buildDataChunks);

            bool builtAny = false;
            foreach (Vector2Int chunk in chunks)
            {
                builtAny |= RebuildChunk(root, chunk.x, chunk.y, registry, buildData);
            }

            root.gameObject.SetActive(builtAny);
            return builtAny;
        }

        public static bool RebuildChunksForCells(CampusFloorRoot floor, IReadOnlyList<Vector3Int> changedCells)
        {
            if (floor == null || changedCells == null || changedCells.Count == 0)
            {
                return false;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                ClearFloor(floor);
                return false;
            }

            Transform root = EnsureCollisionRoot(floor);
            if (root == null)
            {
                return false;
            }

            RemoveLegacyWallTilemapColliders(wallLogic);
            WallCollisionRegistry registry = GetOrCreateRegistry(root);
            HashSet<Vector2Int> chunks = CampusWallChunkSystem.CollectAffectedChunks(changedCells);
            return RebuildChunks(floor, chunks);
        }

        public static bool RebuildChunks(CampusFloorRoot floor, IReadOnlyCollection<Vector2Int> chunks)
        {
            if (floor == null || chunks == null || chunks.Count == 0)
            {
                return false;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                ClearFloor(floor);
                return false;
            }

            Transform root = EnsureCollisionRoot(floor);
            if (root == null)
            {
                return false;
            }

            RemoveLegacyWallTilemapColliders(wallLogic);
            WallCollisionRegistry registry = GetOrCreateRegistry(root);
            CampusWallChunkBuildData buildData = CampusWallChunkBuildData.Capture(wallLogic, chunks);
            return RebuildChunks(floor, chunks, buildData, root, registry);
        }

        internal static bool RebuildChunks(CampusFloorRoot floor, IReadOnlyCollection<Vector2Int> chunks, CampusWallChunkBuildData buildData)
        {
            if (floor == null || chunks == null || chunks.Count == 0)
            {
                return false;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                ClearFloor(floor);
                return false;
            }

            Transform root = EnsureCollisionRoot(floor);
            if (root == null)
            {
                return false;
            }

            RemoveLegacyWallTilemapColliders(wallLogic);
            WallCollisionRegistry registry = GetOrCreateRegistry(root);
            return RebuildChunks(floor, chunks, buildData, root, registry);
        }

        private static bool RebuildChunks(
            CampusFloorRoot floor,
            IReadOnlyCollection<Vector2Int> chunks,
            CampusWallChunkBuildData buildData,
            Transform root,
            WallCollisionRegistry registry)
        {
            if (floor == null || chunks == null || root == null || registry == null)
            {
                return false;
            }

            bool builtAny = false;
            foreach (Vector2Int chunk in chunks)
            {
                builtAny |= RebuildChunk(root, chunk.x, chunk.y, registry, buildData);
            }

            root.gameObject.SetActive(registry.ChunkCount > 0);
            return builtAny || chunks.Count > 0;
        }

        public static void ClearFloor(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            Transform root = FindCollisionRoot(floor);
            if (root == null)
            {
                return;
            }

            WallCollisionRegistry registry = GetOrCreateRegistry(root);
            registry.ClearAll();
            root.gameObject.SetActive(false);
        }

        private static bool RebuildChunk(Transform root, int chunkX, int chunkY, WallCollisionRegistry registry, CampusWallChunkBuildData buildData)
        {
            List<Rect> rects = registry.GetScratchRects();
            Vector2Int chunkKey = new Vector2Int(chunkX, chunkY);
            if (buildData != null && buildData.TryGetChunk(chunkKey, out CampusWallChunkBuildData.ChunkData chunkData))
            {
                List<CampusWallChunkBuildData.CellData> cells = chunkData.Cells;
                for (int i = 0; i < cells.Count; i++)
                {
                    CampusWallChunkBuildData.CellData cellData = cells[i];
                    Vector3 localCenter = root.InverseTransformPoint(cellData.WorldCenter);
                    AddCellCollisionRects(rects, localCenter, cellData.ConnectionMask);
                }
            }

            MergeRects(rects);
            if (rects.Count == 0)
            {
                registry.RemoveChunk(chunkX, chunkY);
                return false;
            }

            registry.ApplyChunk(root, chunkX, chunkY, rects);
            return true;
        }

        private static void AddCellCollisionRects(List<Rect> rects, Vector3 localCenter, int connectionMask)
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

            AddRect(rects, localCenter, -WallHalfWidth, -WallHalfWidth, WallHalfWidth, WallHalfWidth);
            if (eastArm)
            {
                AddRect(rects, localCenter, WallHalfWidth, -WallHalfWidth, WallHalfLength, WallHalfWidth);
            }

            if (westArm)
            {
                AddRect(rects, localCenter, -WallHalfLength, -WallHalfWidth, -WallHalfWidth, WallHalfWidth);
            }

            if (northArm)
            {
                AddRect(rects, localCenter, -WallHalfWidth, WallHalfWidth, WallHalfWidth, WallHalfLength);
            }

            if (southArm)
            {
                AddRect(rects, localCenter, -WallHalfWidth, -WallHalfLength, WallHalfWidth, -WallHalfWidth);
            }
        }

        private static void AddRect(List<Rect> rects, Vector3 center, float minX, float minY, float maxX, float maxY)
        {
            rects.Add(Rect.MinMaxRect(
                center.x + minX,
                center.y + minY,
                center.x + maxX,
                center.y + maxY));
        }

        private static void MergeRects(List<Rect> rects)
        {
            if (rects == null || rects.Count <= 1)
            {
                return;
            }

            rects.Sort(CompareRectsByRowThenX);
            collisionMergeScratch.Clear();
            Rect current = rects[0];
            for (int i = 1; i < rects.Count; i++)
            {
                Rect next = rects[i];
                if (CanMergeHorizontally(current, next))
                {
                    current = Union(current, next);
                    continue;
                }

                collisionMergeScratch.Add(current);
                current = next;
            }

            collisionMergeScratch.Add(current);
            rects.Clear();
            rects.AddRange(collisionMergeScratch);

            rects.Sort(CompareRectsByColumnThenY);
            collisionMergeScratch.Clear();
            current = rects[0];
            for (int i = 1; i < rects.Count; i++)
            {
                Rect next = rects[i];
                if (CanMergeVertically(current, next))
                {
                    current = Union(current, next);
                    continue;
                }

                collisionMergeScratch.Add(current);
                current = next;
            }

            collisionMergeScratch.Add(current);
            rects.Clear();
            rects.AddRange(collisionMergeScratch);
            collisionMergeScratch.Clear();
        }

        private static int CompareRectsByRowThenX(Rect a, Rect b)
        {
            int compare = a.yMin.CompareTo(b.yMin);
            if (compare != 0)
            {
                return compare;
            }

            compare = a.yMax.CompareTo(b.yMax);
            if (compare != 0)
            {
                return compare;
            }

            compare = a.xMin.CompareTo(b.xMin);
            if (compare != 0)
            {
                return compare;
            }

            return a.xMax.CompareTo(b.xMax);
        }

        private static int CompareRectsByColumnThenY(Rect a, Rect b)
        {
            int compare = a.xMin.CompareTo(b.xMin);
            if (compare != 0)
            {
                return compare;
            }

            compare = a.xMax.CompareTo(b.xMax);
            if (compare != 0)
            {
                return compare;
            }

            compare = a.yMin.CompareTo(b.yMin);
            if (compare != 0)
            {
                return compare;
            }

            return a.yMax.CompareTo(b.yMax);
        }

        private static bool CanMergeHorizontally(Rect a, Rect b)
        {
            return Approximately(a.yMin, b.yMin) &&
                   Approximately(a.yMax, b.yMax) &&
                   (Approximately(a.xMax, b.xMin) || Approximately(b.xMax, a.xMin) || Intersects(a.xMin, a.xMax, b.xMin, b.xMax));
        }

        private static bool CanMergeVertically(Rect a, Rect b)
        {
            return Approximately(a.xMin, b.xMin) &&
                   Approximately(a.xMax, b.xMax) &&
                   (Approximately(a.yMax, b.yMin) || Approximately(b.yMax, a.yMin) || Intersects(a.yMin, a.yMax, b.yMin, b.yMax));
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));
        }

        private static bool Intersects(float minA, float maxA, float minB, float maxB)
        {
            return maxA >= minB - MergeTolerance && maxB >= minA - MergeTolerance;
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= MergeTolerance;
        }

        private static Transform EnsureCollisionRoot(CampusFloorRoot floor)
        {
            Transform parent = floor != null && floor.Grid != null ? floor.Grid.transform : floor != null ? floor.transform : null;
            if (parent == null)
            {
                return null;
            }

            Transform root = CampusObjectNames.FindDirectChild(parent, CollisionRootName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(CollisionRootName);
                root = rootObject.transform;
                root.SetParent(parent, false);
            }

            root.name = CollisionRootName;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            return root;
        }

        private static Transform FindCollisionRoot(CampusFloorRoot floor)
        {
            Transform parent = floor != null && floor.Grid != null ? floor.Grid.transform : floor != null ? floor.transform : null;
            return parent != null ? CampusObjectNames.FindDirectChild(parent, CollisionRootName) : null;
        }

        private static void RemoveLegacyWallTilemapColliders(Tilemap wallLogic)
        {
            if (wallLogic == null)
            {
                return;
            }

            DestroyComponent(wallLogic.GetComponent<TilemapCollider2D>());
            DestroyComponent(wallLogic.GetComponent<CompositeCollider2D>());
            DestroyComponent(wallLogic.GetComponent<Rigidbody2D>());
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(component);
            }
            else
            {
                Object.DestroyImmediate(component);
            }
        }

        private static WallCollisionRegistry GetOrCreateRegistry(Transform root)
        {
            int rootId = root.GetInstanceID();
            if (!registries.TryGetValue(rootId, out WallCollisionRegistry registry))
            {
                registry = new WallCollisionRegistry();
                registries.Add(rootId, registry);
            }

            return registry;
        }

        private sealed class WallCollisionRegistry
        {
            private readonly Dictionary<Vector2Int, ChunkColliderGroup> chunks = new Dictionary<Vector2Int, ChunkColliderGroup>();
            private readonly List<Rect> scratchRects = new List<Rect>(64);
            private readonly Stack<ChunkColliderGroup> pooledChunks = new Stack<ChunkColliderGroup>();

            public int ChunkCount => chunks.Count;

            public List<Rect> GetScratchRects()
            {
                scratchRects.Clear();
                return scratchRects;
            }

            public void ApplyChunk(Transform root, int chunkX, int chunkY, List<Rect> rects)
            {
                Vector2Int key = new Vector2Int(chunkX, chunkY);
                if (!chunks.TryGetValue(key, out ChunkColliderGroup chunk))
                {
                    chunk = pooledChunks.Count > 0 ? pooledChunks.Pop() : ChunkColliderGroup.Create(root, key);
                    chunk.Attach(root, key);
                    chunks.Add(key, chunk);
                }

                chunk.Apply(rects);
            }

            public void RemoveChunk(int chunkX, int chunkY)
            {
                Vector2Int key = new Vector2Int(chunkX, chunkY);
                if (!chunks.TryGetValue(key, out ChunkColliderGroup chunk))
                {
                    return;
                }

                chunk.Release();
                pooledChunks.Push(chunk);
                chunks.Remove(key);
            }

            public void ClearAll()
            {
                foreach (ChunkColliderGroup chunk in chunks.Values)
                {
                    chunk.Release();
                    pooledChunks.Push(chunk);
                }

                chunks.Clear();
            }
        }

        private sealed class ChunkColliderGroup
        {
            private readonly GameObject gameObject;
            private readonly List<BoxCollider2D> colliders = new List<BoxCollider2D>();

            private ChunkColliderGroup(GameObject gameObject)
            {
                this.gameObject = gameObject;
            }

            public static ChunkColliderGroup Create(Transform parent, Vector2Int key)
            {
                GameObject chunkObject = new GameObject("WallCollision_Chunk_" + key.x + "_" + key.y);
                chunkObject.transform.SetParent(parent, false);
                chunkObject.transform.localPosition = Vector3.zero;
                chunkObject.transform.localRotation = Quaternion.identity;
                chunkObject.transform.localScale = Vector3.one;
                return new ChunkColliderGroup(chunkObject);
            }

            public void Attach(Transform parent, Vector2Int key)
            {
                gameObject.name = "WallCollision_Chunk_" + key.x + "_" + key.y;
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.localPosition = Vector3.zero;
                gameObject.transform.localRotation = Quaternion.identity;
                gameObject.transform.localScale = Vector3.one;
                gameObject.SetActive(true);
            }

            public void Apply(List<Rect> rects)
            {
                EnsureColliderCount(rects.Count);
                for (int i = 0; i < colliders.Count; i++)
                {
                    BoxCollider2D collider = colliders[i];
                    if (i >= rects.Count)
                    {
                        collider.enabled = false;
                        continue;
                    }

                    Rect rect = rects[i];
                    collider.enabled = true;
                    collider.offset = rect.center;
                    collider.size = rect.size;
                }

                gameObject.SetActive(rects.Count > 0);
            }

            public void Release()
            {
                for (int i = 0; i < colliders.Count; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = false;
                    }
                }

                gameObject.SetActive(false);
                gameObject.transform.SetParent(null, false);
            }

            private void EnsureColliderCount(int count)
            {
                while (colliders.Count < count)
                {
                    colliders.Add(gameObject.AddComponent<BoxCollider2D>());
                }
            }
        }
    }
}
