using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NtingWallPointShadowRenderer : MonoBehaviour
    {
        private static readonly ProfilerMarker RebuildBaseRectsMarker = new ProfilerMarker("NtingWallPointShadowRenderer.RebuildBaseRects");
        public const string RendererName = "Nting Point Light Wall Shadows";

        private const float WallCellHalf = 0.5f;
        private const float WallBottomHalfWidth = 0.330f;
        private const float MinSize = 0.01f;
        private const float EdgeProjectionDotThreshold = 0.05f;
        private const float EdgeMergeTolerance = 0.0005f;
        private const float EdgeBucketWorldSize = 4f;
        private const float EdgeQueryPaddingWorld = 1f;
        private const float ShadowChunkWorldSize = 16f;
        private const float TopologyRebuildDelaySeconds = 0.08f;
        private const float WallPointShadowAlphaMultiplier = 1.35f;
        private const int MaxPointWallFillLights = 12;
        private const string ShadowShaderName = "Nting Campus/2D/Projected Shadow Unlit";
        private const string ShadowShaderResourcePath = "Shaders/CampusProjectedShadowUnlit";

        [SerializeField] private CampusFloorRoot floor;

        private readonly List<ShadowSourceRect> sourceRects = new List<ShadowSourceRect>(256);
        private readonly List<ShadowEdge> cachedEdges = new List<ShadowEdge>(512);
        private readonly List<Vector2> coveredIntervals = new List<Vector2>(128);
        private readonly List<int> candidateEdgeIndices = new List<int>(256);
        private readonly Dictionary<Vector2Int, List<int>> edgeBuckets = new Dictionary<Vector2Int, List<int>>();
        private readonly Dictionary<Vector2Int, List<int>> topologyChunkEdgeIndices = new Dictionary<Vector2Int, List<int>>();
        private readonly List<ShadowEdge> candidateEdges = new List<ShadowEdge>(256);
        private readonly Dictionary<Vector2Int, ShadowMeshChunk> shadowChunks = new Dictionary<Vector2Int, ShadowMeshChunk>();

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh shadowMesh;
        private bool topologyDirty = true;
        private bool topologyBuilt;
        private int topologyVersion;
        private int lastRefreshSignature = int.MinValue;
        private int cachedTopologyVersion = -1;
        private CampusWallShadowTopologyCache.FloorTopologyData topologyData;
        private static Material sharedShadowMaterial;

        public static NtingWallPointShadowRenderer EnsureForFloor(CampusFloorRoot targetFloor, bool rebuildIfNeeded = true)
        {
            if (targetFloor == null)
            {
                return null;
            }

            Transform parent = targetFloor.Grid != null ? targetFloor.Grid.transform : targetFloor.transform;
            if (parent == null)
            {
                return null;
            }

            Transform existing = parent.Find(RendererName);
            NtingWallPointShadowRenderer renderer = existing != null ? existing.GetComponent<NtingWallPointShadowRenderer>() : null;
            if (renderer == null)
            {
                GameObject rendererObject = existing != null ? existing.gameObject : new GameObject(RendererName);
                rendererObject.transform.SetParent(parent, false);
                renderer = rendererObject.AddComponent<NtingWallPointShadowRenderer>();
            }

            renderer.floor = targetFloor;
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            renderer.EnsureMeshComponents();
            if (rebuildIfNeeded)
            {
                renderer.RebuildBaseRectsIfNeeded();
            }

            return renderer;
        }

        public static void MarkTopologyDirtyForFloor(CampusFloorRoot targetFloor)
        {
            Transform parent = targetFloor != null && targetFloor.Grid != null ? targetFloor.Grid.transform : targetFloor != null ? targetFloor.transform : null;
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find(RendererName);
            NtingWallPointShadowRenderer renderer = existing != null ? existing.GetComponent<NtingWallPointShadowRenderer>() : null;
            if (renderer != null)
            {
                renderer.MarkTopologyDirty();
            }
        }

        public static void ClearForFloor(CampusFloorRoot targetFloor)
        {
            Transform parent = targetFloor != null && targetFloor.Grid != null ? targetFloor.Grid.transform : targetFloor != null ? targetFloor.transform : null;
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find(RendererName);
            if (existing != null)
            {
                DestroyGeneratedObject(existing.gameObject);
            }
        }

        public void MarkTopologyDirty()
        {
            topologyDirty = true;
            cachedTopologyVersion = -1;
            lastRefreshSignature = int.MinValue;
        }

        public void RebuildBaseRectsIfNeeded(bool allowDeferredWait = false)
        {
            RebuildBaseRects();
        }

        public void RefreshShadows(
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int pointLightCount,
            bool enabled,
            float maxShadowLengthWorld,
            float shadowAlpha,
            float intensityScale,
            float pointLightFillStrength,
            float pointLightFillIntensityScale,
            Color shadowColor,
            int maxPointLights,
            int sortingLayerId,
            int sortingOrder)
        {
            EnsureMeshComponents();
            if (!enabled || pointLights == null || pointLightCount <= 0 || maxShadowLengthWorld <= 0.001f || shadowAlpha <= 0.001f)
            {
                SetVisible(false);
                lastRefreshSignature = int.MinValue;
                return;
            }

            RebuildBaseRectsIfNeeded(true);
            if (topologyData == null || topologyData.ActiveChunks.Count == 0)
            {
                ClearRuntimeMeshes();
                SetVisible(false);
                return;
            }

            int count = Mathf.Min(pointLightCount, pointLights.Length, Mathf.Max(0, maxPointLights));
            int refreshSignature = ComputeRefreshSignature(
                pointLights,
                count,
                maxShadowLengthWorld,
                shadowAlpha,
                intensityScale,
                pointLightFillStrength,
                pointLightFillIntensityScale,
                shadowColor,
                sortingLayerId,
                sortingOrder);
            if (refreshSignature == lastRefreshSignature)
            {
                return;
            }

            lastRefreshSignature = refreshSignature;
            BeginShadowChunkMeshUpdate();

            for (int lightIndex = 0; lightIndex < count; lightIndex++)
            {
                NtingCustomShadowSystem.ShadowLightInfo light = pointLights[lightIndex];
                float lightIntensity = Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(light.Intensity * Mathf.Max(0.001f, intensityScale)));
                float lightAlpha = Mathf.Clamp01(shadowAlpha * light.ShadowAlphaWeight * lightIntensity * WallPointShadowAlphaMultiplier);
                if (lightAlpha <= 0.001f)
                {
                    continue;
                }

                Color lightShadowColor = ResolveWallPointShadowColor(shadowColor, light);
                Color nearColor = new Color(lightShadowColor.r, lightShadowColor.g, lightShadowColor.b, lightAlpha);
                Color farColor = new Color(lightShadowColor.r, lightShadowColor.g, lightShadowColor.b, 0f);

                CollectCandidateEdges(light.Position, light.Radius + maxShadowLengthWorld + EdgeQueryPaddingWorld);
                for (int candidateIndex = 0; candidateIndex < candidateEdges.Count; candidateIndex++)
                {
                    TryAddProjectedShadow(candidateEdges[candidateIndex], light, pointLights, count, maxShadowLengthWorld, pointLightFillStrength, pointLightFillIntensityScale, nearColor, farColor);
                }
            }

            if (!ApplyShadowChunkMeshes(sortingLayerId, sortingOrder))
            {
                SetVisible(false);
                return;
            }

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        private static Color ResolveWallPointShadowColor(Color baseShadowColor, NtingCustomShadowSystem.ShadowLightInfo light)
        {
            return NtingCustomShadowSystem.ResolvePointLightShadowColor(baseShadowColor, light);
        }

        private void RefreshTopologyFromCache()
        {
            CampusFloorRoot targetFloor = ResolveFloor();
            CampusWallShadowTopologyCache.FloorTopologyData topology = targetFloor != null
                ? CampusWallShadowTopologyCache.EnsureBuilt(targetFloor)
                : null;

            if (topology == null)
            {
                cachedEdges.Clear();
                edgeBuckets.Clear();
                topologyData = null;
                topologyDirty = false;
                topologyBuilt = true;
                topologyVersion++;
                cachedTopologyVersion = -1;
                lastRefreshSignature = int.MinValue;
                ClearRuntimeMeshes();
                SetVisible(false);
                return;
            }

            if (!topologyDirty && topologyBuilt && cachedTopologyVersion == topology.Version)
            {
                return;
            }

            topologyData = topology;
            cachedTopologyVersion = topology.Version;
            topologyDirty = false;
            topologyBuilt = true;
            topologyVersion = topology.Version;
            lastRefreshSignature = int.MinValue;
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (shadowMesh != null)
            {
                DestroyGeneratedObject(shadowMesh);
                shadowMesh = null;
            }

            foreach (ShadowMeshChunk chunk in shadowChunks.Values)
            {
                chunk.DestroyMesh();
            }
        }

        private void RebuildBaseRects()
        {
            using (RebuildBaseRectsMarker.Auto())
            {
                RefreshTopologyFromCache();
            }
        }

        private void AddWallBaseSourceRects(Vector3 center, Vector3 cellSize, int connectionMask, float sourceHalfX, float sourceHalfY)
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

            AddSourceRect(center, cellSize, -sourceHalfX, -sourceHalfY, sourceHalfX, sourceHalfY);
            if (eastArm)
            {
                AddSourceRect(center, cellSize, sourceHalfX, -sourceHalfY, WallCellHalf, sourceHalfY);
            }

            if (westArm)
            {
                AddSourceRect(center, cellSize, -WallCellHalf, -sourceHalfY, -sourceHalfX, sourceHalfY);
            }

            if (northArm)
            {
                AddSourceRect(center, cellSize, -sourceHalfX, sourceHalfY, sourceHalfX, WallCellHalf);
            }

            if (southArm)
            {
                AddSourceRect(center, cellSize, -sourceHalfX, -WallCellHalf, sourceHalfX, -sourceHalfY);
            }
        }

        private void AddSourceRect(Vector3 center, Vector3 cellSize, float minX, float minY, float maxX, float maxY)
        {
            float scaleX = Mathf.Abs(cellSize.x);
            float scaleY = Mathf.Abs(cellSize.y);
            sourceRects.Add(new ShadowSourceRect(
                center.x + minX * scaleX,
                center.y + minY * scaleY,
                center.x + maxX * scaleX,
                center.y + maxY * scaleY));
            RemoveDuplicateSourceRectAtEnd();
        }

        private void RemoveDuplicateSourceRectAtEnd()
        {
            int lastIndex = sourceRects.Count - 1;
            if (lastIndex <= 0)
            {
                return;
            }

            ShadowSourceRect last = sourceRects[lastIndex];
            for (int i = 0; i < lastIndex; i++)
            {
                if (sourceRects[i].SameAs(last))
                {
                    sourceRects.RemoveAt(lastIndex);
                    return;
                }
            }
        }

        private void AddSourceRectVisibleEdges(ShadowSourceRect rect)
        {
            AddEdgeSegments(rect.MinX, rect.MaxX, rect.MaxY, true, new Vector2(0f, 1f), rect);
            AddEdgeSegments(rect.MaxX, rect.MinX, rect.MinY, true, new Vector2(0f, -1f), rect);
            AddEdgeSegments(rect.MinY, rect.MaxY, rect.MaxX, false, new Vector2(1f, 0f), rect);
            AddEdgeSegments(rect.MaxY, rect.MinY, rect.MinX, false, new Vector2(-1f, 0f), rect);
        }

        private void AddEdgeSegments(float start, float end, float fixedCoord, bool horizontal, Vector2 normal, ShadowSourceRect owner)
        {
            float edgeStart = Mathf.Min(start, end);
            float edgeEnd = Mathf.Max(start, end);
            coveredIntervals.Clear();

            for (int i = 0; i < sourceRects.Count; i++)
            {
                ShadowSourceRect other = sourceRects[i];
                if (other.SameAs(owner))
                {
                    continue;
                }

                if (TryGetCoveredInterval(other, fixedCoord, horizontal, normal, edgeStart, edgeEnd, out Vector2 interval))
                {
                    coveredIntervals.Add(interval);
                }
            }

            if (coveredIntervals.Count == 0)
            {
                AddEdgeSegment(edgeStart, edgeEnd, fixedCoord, horizontal, normal);
                return;
            }

            coveredIntervals.Sort(CompareIntervals);
            float cursor = edgeStart;
            for (int i = 0; i < coveredIntervals.Count; i++)
            {
                Vector2 interval = coveredIntervals[i];
                if (interval.y <= cursor + EdgeMergeTolerance)
                {
                    cursor = Mathf.Max(cursor, interval.y);
                    continue;
                }

                if (interval.x > cursor + EdgeMergeTolerance)
                {
                    AddEdgeSegment(cursor, Mathf.Min(interval.x, edgeEnd), fixedCoord, horizontal, normal);
                }

                cursor = Mathf.Max(cursor, interval.y);
                if (cursor >= edgeEnd - EdgeMergeTolerance)
                {
                    break;
                }
            }

            if (cursor < edgeEnd - EdgeMergeTolerance)
            {
                AddEdgeSegment(cursor, edgeEnd, fixedCoord, horizontal, normal);
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

        private void AddEdgeSegment(float start, float end, float fixedCoord, bool horizontal, Vector2 normal)
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

            cachedEdges.Add(new ShadowEdge(
                transform.InverseTransformPoint(worldA),
                transform.InverseTransformPoint(worldB),
                worldA,
                worldB,
                normal));
        }

        private void RebuildEdgeBuckets()
        {
            edgeBuckets.Clear();
            for (int edgeIndex = 0; edgeIndex < cachedEdges.Count; edgeIndex++)
            {
                Vector2Int key = ResolveEdgeBucketKey(cachedEdges[edgeIndex].Center);
                if (!edgeBuckets.TryGetValue(key, out List<int> bucket))
                {
                    bucket = new List<int>(16);
                    edgeBuckets.Add(key, bucket);
                }

                bucket.Add(edgeIndex);
            }
        }

        private void CollectCandidateEdges(Vector2 lightPosition, float queryRadius)
        {
            candidateEdges.Clear();
            if (topologyData == null || topologyData.ActiveChunks.Count == 0 || queryRadius <= 0f)
            {
                return;
            }

            int minX = Mathf.FloorToInt((lightPosition.x - queryRadius) / ShadowChunkWorldSize);
            int maxX = Mathf.FloorToInt((lightPosition.x + queryRadius) / ShadowChunkWorldSize);
            int minY = Mathf.FloorToInt((lightPosition.y - queryRadius) / ShadowChunkWorldSize);
            int maxY = Mathf.FloorToInt((lightPosition.y + queryRadius) / ShadowChunkWorldSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!topologyData.ChunkEdges.TryGetValue(new Vector2Int(x, y), out List<CampusWallShadowTopologyCache.WallShadowEdge> chunkEdges))
                    {
                        continue;
                    }

                    for (int i = 0; i < chunkEdges.Count; i++)
                    {
                        CampusWallShadowTopologyCache.WallShadowEdge edge = chunkEdges[i];
                        candidateEdges.Add(new ShadowEdge(
                            transform.InverseTransformPoint(edge.WorldA),
                            transform.InverseTransformPoint(edge.WorldB),
                            edge.WorldA,
                            edge.WorldB,
                            edge.Normal));
                    }
                }
            }
        }

        private static Vector2Int ResolveEdgeBucketKey(Vector2 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / EdgeBucketWorldSize),
                Mathf.FloorToInt(worldPosition.y / EdgeBucketWorldSize));
        }

        private void TryAddProjectedShadow(
            ShadowEdge edge,
            NtingCustomShadowSystem.ShadowLightInfo light,
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int pointLightCount,
            float maxShadowLengthWorld,
            float pointLightFillStrength,
            float pointLightFillIntensityScale,
            Color nearBaseColor,
            Color farColor)
        {
            Vector3 worldA = edge.WorldA;
            Vector3 worldB = edge.WorldB;
            Vector2 edgeCenter = edge.Center;
            Vector2 centerDirection = edgeCenter - (Vector2)light.Position;
            float centerDistance = centerDirection.magnitude;
            if (centerDistance <= 0.001f || centerDistance >= light.Radius)
            {
                return;
            }

            Vector2 normalizedCenterDirection = centerDirection / centerDistance;
            if (Vector2.Dot(edge.Normal, normalizedCenterDirection) <= EdgeProjectionDotThreshold)
            {
                return;
            }

            float distance01 = Mathf.Clamp01(centerDistance / light.Radius);
            float shadowLength = ResolvePointShadowLength(light, maxShadowLengthWorld, distance01);
            if (shadowLength <= MinSize)
            {
                return;
            }

            Vector3 farAWorld = ProjectEndpoint(worldA, light, normalizedCenterDirection, shadowLength);
            Vector3 farBWorld = ProjectEndpoint(worldB, light, normalizedCenterDirection, shadowLength);
            Vector3 farA = transform.InverseTransformPoint(farAWorld);
            Vector3 farB = transform.InverseTransformPoint(farBWorld);

            float alpha = ResolvePointShadowAlphaMultiplier(light, pointLights, pointLightCount, edgeCenter, distance01, pointLightFillStrength, pointLightFillIntensityScale);
            if (alpha <= 0.001f)
            {
                return;
            }

            Color near = new Color(nearBaseColor.r, nearBaseColor.g, nearBaseColor.b, Mathf.Clamp01(nearBaseColor.a * alpha));
            AddShadowChunkQuad(edge.A, edge.B, farB, farA, (worldA + worldB + farBWorld + farAWorld) * 0.25f, near, near, farColor, farColor);
        }

        private static Vector3 ProjectEndpoint(Vector3 worldPosition, NtingCustomShadowSystem.ShadowLightInfo light, Vector2 fallbackDirection, float shadowLength)
        {
            Vector2 direction = (Vector2)worldPosition - (Vector2)light.Position;
            float sqrMagnitude = direction.sqrMagnitude;
            Vector2 normalized = sqrMagnitude > 0.000001f ? direction / Mathf.Sqrt(sqrMagnitude) : fallbackDirection;
            return worldPosition + new Vector3(normalized.x, normalized.y, 0f) * shadowLength;
        }

        private static float ResolvePointShadowLength(NtingCustomShadowSystem.ShadowLightInfo light, float maxShadowLengthWorld, float distance01)
        {
            float rangeLength = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(distance01));
            float intensityLength = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(light.Intensity));
            return Mathf.Max(0f, maxShadowLengthWorld * light.ShadowLengthWeight * rangeLength * intensityLength);
        }

        private float ResolvePointShadowAlphaMultiplier(
            NtingCustomShadowSystem.ShadowLightInfo light,
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int pointLightCount,
            Vector2 sampleWorldPosition,
            float distance01,
            float pointLightFillStrength,
            float pointLightFillIntensityScale)
        {
            float rangeFactor = 1f - distance01;
            float visibleRangeFactor = Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(rangeFactor));
            float fillMultiplier = NtingCustomShadowSystem.ResolvePointLightFillMultiplier(
                pointLights,
                Mathf.Min(pointLightCount, MaxPointWallFillLights),
                light.InstanceId,
                sampleWorldPosition,
                pointLightFillStrength,
                pointLightFillIntensityScale);
            return Mathf.Clamp01(visibleRangeFactor * fillMultiplier);
        }

        private int ComputeRefreshSignature(
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int count,
            float maxShadowLengthWorld,
            float shadowAlpha,
            float intensityScale,
            float pointLightFillStrength,
            float pointLightFillIntensityScale,
            Color shadowColor,
            int sortingLayerId,
            int sortingOrder)
        {
            unchecked
            {
                int signature = 17;
                signature = signature * 31 + topologyVersion;
                signature = signature * 31 + count;
                signature = signature * 31 + Quantize(maxShadowLengthWorld);
                signature = signature * 31 + Quantize(shadowAlpha);
                signature = signature * 31 + Quantize(intensityScale);
                signature = signature * 31 + Quantize(pointLightFillStrength);
                signature = signature * 31 + Quantize(pointLightFillIntensityScale);
                signature = signature * 31 + Quantize(shadowColor.r);
                signature = signature * 31 + Quantize(shadowColor.g);
                signature = signature * 31 + Quantize(shadowColor.b);
                signature = signature * 31 + Quantize(shadowColor.a);
                signature = signature * 31 + sortingLayerId;
                signature = signature * 31 + sortingOrder;

                if (pointLights == null)
                {
                    return signature;
                }

                for (int i = 0; i < count; i++)
                {
                    NtingCustomShadowSystem.ShadowLightInfo light = pointLights[i];
                    signature = signature * 31 + light.InstanceId;
                    signature = signature * 31 + Quantize(light.Position.x);
                    signature = signature * 31 + Quantize(light.Position.y);
                    signature = signature * 31 + Quantize(light.Position.z);
                    signature = signature * 31 + Quantize(light.Radius);
                    signature = signature * 31 + Quantize(light.Intensity);
                    signature = signature * 31 + Quantize(light.ShadowLengthWeight);
                    signature = signature * 31 + Quantize(light.ShadowAlphaWeight);
                    signature = signature * 31 + Quantize(light.ShadowFillWeight);
                    signature = signature * 31 + Quantize(light.ShadowColorTint.r);
                    signature = signature * 31 + Quantize(light.ShadowColorTint.g);
                    signature = signature * 31 + Quantize(light.ShadowColorTint.b);
                }

                return signature;
            }
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        private void BeginShadowChunkMeshUpdate()
        {
            foreach (ShadowMeshChunk chunk in shadowChunks.Values)
            {
                chunk.ClearData();
            }
        }

        private void AddShadowChunkQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 worldCenter, Color colorA, Color colorB, Color colorC, Color colorD)
        {
            Vector2Int chunkKey = new Vector2Int(
                Mathf.FloorToInt(worldCenter.x / ShadowChunkWorldSize),
                Mathf.FloorToInt(worldCenter.y / ShadowChunkWorldSize));
            if (!shadowChunks.TryGetValue(chunkKey, out ShadowMeshChunk chunk))
            {
                chunk = ShadowMeshChunk.Create(transform, chunkKey, "PointWallShadow_Chunk");
                shadowChunks.Add(chunkKey, chunk);
            }

            chunk.AddQuad(a, b, c, d, colorA, colorB, colorC, colorD);
        }

        private bool ApplyShadowChunkMeshes(int sortingLayerId, int sortingOrder)
        {
            bool hasGeometry = false;
            Material material = ResolveShadowMaterial();
            foreach (ShadowMeshChunk chunk in shadowChunks.Values)
            {
                hasGeometry |= chunk.Apply(material, sortingLayerId, sortingOrder);
            }

            return hasGeometry;
        }

        private void ClearShadowChunkMeshes()
        {
            foreach (ShadowMeshChunk chunk in shadowChunks.Values)
            {
                chunk.ClearMesh();
            }
        }

        private void ClearRuntimeMeshes()
        {
            if (shadowMesh != null)
            {
                shadowMesh.Clear(false);
            }

            ClearShadowChunkMeshes();
        }

        private void EnsureMeshComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }

            if (shadowMesh == null)
            {
                shadowMesh = new Mesh
                {
                    name = "NtingWallPointShadow_Mesh_Runtime",
                    hideFlags = HideFlags.HideAndDontSave
                };
                shadowMesh.MarkDynamic();
            }

            meshFilter.sharedMesh = shadowMesh;
            meshRenderer.enabled = false;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.allowOcclusionWhenDynamic = false;
        }

        private void ClearMesh()
        {
            cachedEdges.Clear();
            sourceRects.Clear();
            edgeBuckets.Clear();
            topologyChunkEdgeIndices.Clear();
            topologyData = null;
            candidateEdges.Clear();
            ClearRuntimeMeshes();
            topologyDirty = true;
            topologyBuilt = false;
            cachedTopologyVersion = -1;
            lastRefreshSignature = int.MinValue;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
            }

            if (!visible)
            {
                ClearShadowChunkMeshes();
                lastRefreshSignature = int.MinValue;
            }
        }

        private CampusFloorRoot ResolveFloor()
        {
            if (floor == null)
            {
                floor = GetComponentInParent<CampusFloorRoot>();
            }

            return floor;
        }

        private static int CompareIntervals(Vector2 a, Vector2 b)
        {
            return a.x.CompareTo(b.x);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= EdgeMergeTolerance;
        }

        private static Material ResolveShadowMaterial()
        {
            if (sharedShadowMaterial != null)
            {
                return sharedShadowMaterial;
            }

            Shader shader = Shader.Find(ShadowShaderName);
            if (shader == null)
            {
                shader = Resources.Load<Shader>(ShadowShaderResourcePath);
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            sharedShadowMaterial = new Material(shader)
            {
                name = "NtingWallPointShadow_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedShadowMaterial;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class ShadowMeshChunk
        {
            private readonly MeshFilter filter;
            private readonly MeshRenderer renderer;
            private readonly Mesh mesh;
            private readonly List<Vector3> vertices = new List<Vector3>(256);
            private readonly List<int> triangles = new List<int>(384);
            private readonly List<Color> colors = new List<Color>(256);

            private ShadowMeshChunk(MeshFilter filter, MeshRenderer renderer, Mesh mesh)
            {
                this.filter = filter;
                this.renderer = renderer;
                this.mesh = mesh;
            }

            public static ShadowMeshChunk Create(Transform parent, Vector2Int key, string prefix)
            {
                GameObject chunkObject = new GameObject(prefix + "_" + key.x + "_" + key.y);
                chunkObject.transform.SetParent(parent, false);
                chunkObject.transform.localPosition = Vector3.zero;
                chunkObject.transform.localRotation = Quaternion.identity;
                chunkObject.transform.localScale = Vector3.one;

                MeshFilter filter = chunkObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = chunkObject.AddComponent<MeshRenderer>();
                Mesh mesh = new Mesh
                {
                    name = chunkObject.name + "_Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowOcclusionWhenDynamic = false;
                renderer.enabled = false;
                return new ShadowMeshChunk(filter, renderer, mesh);
            }

            public void ClearData()
            {
                vertices.Clear();
                triangles.Clear();
                colors.Clear();
            }

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colorA, Color colorB, Color colorC, Color colorD)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                colors.Add(colorA);
                colors.Add(colorB);
                colors.Add(colorC);
                colors.Add(colorD);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            public bool Apply(Material material, int sortingLayerId, int sortingOrder)
            {
                if (vertices.Count == 0 || material == null)
                {
                    ClearMesh();
                    return false;
                }

                mesh.Clear(false);
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0, true);
                mesh.RecalculateBounds();
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = true;
                return true;
            }

            public void ClearMesh()
            {
                mesh.Clear(false);
                renderer.enabled = false;
            }

            public void DestroyMesh()
            {
                if (mesh != null)
                {
                    DestroyGeneratedObject(mesh);
                }
            }
        }

        private readonly struct ShadowSourceRect
        {
            public readonly float MinX;
            public readonly float MinY;
            public readonly float MaxX;
            public readonly float MaxY;

            public ShadowSourceRect(float minX, float minY, float maxX, float maxY)
            {
                MinX = Mathf.Min(minX, maxX);
                MinY = Mathf.Min(minY, maxY);
                MaxX = Mathf.Max(minX, maxX);
                MaxY = Mathf.Max(minY, maxY);
            }

            public bool SameAs(ShadowSourceRect other)
            {
                return Approximately(MinX, other.MinX) &&
                       Approximately(MinY, other.MinY) &&
                       Approximately(MaxX, other.MaxX) &&
                       Approximately(MaxY, other.MaxY);
            }
        }

        private readonly struct ShadowEdge
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly Vector3 WorldA;
            public readonly Vector3 WorldB;
            public readonly Vector2 Center;
            public readonly Vector2 Normal;

            public ShadowEdge(Vector3 a, Vector3 b, Vector3 worldA, Vector3 worldB, Vector2 normal)
            {
                A = a;
                B = b;
                WorldA = worldA;
                WorldB = worldB;
                Center = ((Vector2)worldA + (Vector2)worldB) * 0.5f;
                Normal = normal;
            }
        }
    }
}
