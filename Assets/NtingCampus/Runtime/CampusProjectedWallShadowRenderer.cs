using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CampusProjectedWallShadowRenderer : MonoBehaviour
    {
        private static readonly ProfilerMarker RebuildFromWallLogicMarker = new ProfilerMarker("CampusProjectedWallShadowRenderer.RebuildFromWallLogic");
        private static readonly ProfilerMarker UpdateShadowMeshMarker = new ProfilerMarker("CampusProjectedWallShadowRenderer.UpdateShadowMesh");
        private const string RendererName = "Projected Wall Shadows";
        private const string ShadowShaderName = "Nting Campus/2D/Projected Shadow Unlit";
        private const string ShadowShaderResourcePath = "Shaders/CampusProjectedShadowUnlit";
        private const string ShadowMaterialAssetPath = "Assets/NtingCampus/Materials/CampusProjectedShadow.mat";
        private const float EdgeProjectionDotThreshold = 0.05f;
        private const float DefaultMinShadowLength = 0.3f;
        private const float DefaultMaxShadowLength = 1.15f;
        private const float PreviousDefaultMinShadowLength = 0.24f;
        private const float PreviousDefaultMaxShadowLength = 0.95f;
        private const float PreviousMinShadowLength = 0.06f;
        private const float PreviousMaxShadowLength = 1.10f;
        private const float LegacyMinShadowLength = 0.20f;
        private const float LegacyMaxShadowLength = 3.20f;
        private const float DefaultAlphaMultiplier = 3.4f;
        private const float CurrentDefaultAlphaMultiplierBeforeLengthen = 3.0f;
        private const float PreviousDefaultAlphaMultiplier = 2.25f;
        private const float PreviousAlphaMultiplier = 1.35f;
        private const float LegacyAlphaMultiplier = 1.0f;
        private const float WallBottomHalfWidth = 0.330f;
        private const float PreviousDefaultShadowSourceWidth = 0.410f;
        private const float DefaultShadowSourceWidth = WallBottomHalfWidth * 2f;
        private const float LegacyShadowSourceWidth = 0.33f;
        private const float WallCellHalf = 0.5f;
        private const float EdgeMergeTolerance = 0.0005f;
        private const float ShadowChunkWorldSize = 16f;
        private const float TopologyRebuildDelaySeconds = 0.08f;
        private const float RuntimeShadowRefreshInterval = 0.1f;
        private const float RuntimeDirectionAngleThresholdDegrees = 2f;
        private const float RuntimeLengthFactorThreshold = 0.03f;
        private const float RuntimeOpacityFactorThreshold = 0.03f;
        private const float RuntimeSettingColorThreshold = 0.03f;
        private const float SunTintShadowAlphaMultiplier = 0.96f;

        [SerializeField] private CampusFloorRoot floor;
        [SerializeField] private CampusDayNightController dayNight;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Material shadowMaterial;

        [SerializeField] private float minShadowLength = DefaultMinShadowLength;
        [SerializeField] private float maxShadowLength = DefaultMaxShadowLength;
        [SerializeField] private float shadowWidth = DefaultShadowSourceWidth;
        [SerializeField] private float alphaMultiplier = DefaultAlphaMultiplier;
        [SerializeField] private float rebuildDirectionThreshold = 0.01f;
        [SerializeField] private bool autoFindDayNight = true;
        [SerializeField] private bool pointLightsCanBrightenProjectedShadows = true;
        [SerializeField, Range(0f, 1f)] private float pointLightFillStrength = 0.65f;
        [SerializeField, Range(0.01f, 2f)] private float pointLightFillIntensityScale = 0.75f;
        [SerializeField, Min(1)] private int maxFillPointLights = 12;

        private bool runtimeSunShadowSettingsActive;
        private bool runtimeSunShadowEnabled = true;
        private float runtimeSunShadowMaxLength = DefaultMaxShadowLength;
        private float runtimeSunShadowAlpha = 1f;
        private bool runtimeScaleLengthByDayNight = true;
        private Color runtimeSunShadowColor = Color.black;
        private float runtimePointLightFillStrength = 0.65f;
        private float runtimePointLightFillIntensityScale = 0.75f;
        private int runtimeMaxFillPointLights = 12;

        private static Material runtimeFallbackMaterial;

        private readonly List<ShadowEdge> cachedEdges = new List<ShadowEdge>();
        private readonly List<ShadowSourceRect> sourceRects = new List<ShadowSourceRect>();
        private readonly List<Vector2> coveredIntervals = new List<Vector2>();
        private readonly List<NtingCustomShadowSystem.ShadowLightInfo> fillPointLights = new List<NtingCustomShadowSystem.ShadowLightInfo>(16);
        private readonly List<Vector3> vertices = new List<Vector3>(256);
        private readonly List<int> triangles = new List<int>(384);
        private readonly List<Color> colors = new List<Color>(256);
        private readonly Dictionary<Vector2Int, ShadowMeshChunk> shadowChunks = new Dictionary<Vector2Int, ShadowMeshChunk>();
        private readonly HashSet<Vector2Int> pendingChunkUpdates = new HashSet<Vector2Int>();

        private Mesh shadowMesh;
        private bool topologyDirty = true;
        private bool topologyBuilt;
        private int fillLightsSignature;
        private int lastAppliedFillLightsSignature = int.MinValue;
        private Vector2 lastShadowDirection = Vector2.zero;
        private float lastShadowLengthFactor = -1f;
        private float lastShadowOpacityFactor = -1f;
        private float lastResolvedShadowOpacity = -1f;
        private Color lastResolvedShadowColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
        private bool forceMeshUpdate = true;
        private int cachedTopologyVersion = -1;
        private bool pendingFullChunkRebuild = true;
        private float nextRuntimeShadowRefreshTime;

        public static CampusProjectedWallShadowRenderer EnsureForFloor(CampusFloorRoot targetFloor, bool rebuildIfNeeded = true)
        {
            if (targetFloor == null)
            {
                return null;
            }

            Transform parent = targetFloor.Grid != null ? targetFloor.Grid.transform : targetFloor.transform;
            CampusProjectedWallShadowRenderer renderer = parent.GetComponentInChildren<CampusProjectedWallShadowRenderer>(true);
            if (renderer == null)
            {
                Transform existing = parent.Find(RendererName);
                GameObject rendererObject = existing != null ? existing.gameObject : new GameObject(RendererName);
                rendererObject.transform.SetParent(parent, false);
                rendererObject.transform.localPosition = Vector3.zero;
                rendererObject.transform.localRotation = Quaternion.identity;
                rendererObject.transform.localScale = Vector3.one;
                renderer = rendererObject.AddComponent<CampusProjectedWallShadowRenderer>();
            }

            renderer.floor = targetFloor;
            renderer.ResolveReferences();
            if (rebuildIfNeeded)
            {
                renderer.RebuildFromWallLogicIfNeeded();
            }

            return renderer;
        }

        public static void MarkTopologyDirtyForFloor(CampusFloorRoot targetFloor)
        {
            if (targetFloor == null)
            {
                return;
            }

            Transform parent = targetFloor.Grid != null ? targetFloor.Grid.transform : targetFloor.transform;
            CampusProjectedWallShadowRenderer renderer = parent != null
                ? parent.GetComponentInChildren<CampusProjectedWallShadowRenderer>(true)
                : null;
            if (renderer != null)
            {
                renderer.MarkTopologyDirty();
            }
        }

        public static void ClearForFloor(CampusFloorRoot targetFloor)
        {
            if (targetFloor == null)
            {
                return;
            }

            Transform parent = targetFloor.Grid != null ? targetFloor.Grid.transform : targetFloor.transform;
            CampusProjectedWallShadowRenderer renderer = parent.GetComponentInChildren<CampusProjectedWallShadowRenderer>(true);
            if (renderer != null)
            {
                renderer.ClearMesh();
            }
        }

        public bool ApplyRuntimeSunShadowSettings(
            bool enabled,
            float maxLengthWorld,
            float alpha,
            bool scaleLengthByDayNight,
            Color color,
            float fillStrength,
            float fillIntensityScale,
            int maxFillLights)
        {
            float clampedLength = Mathf.Max(0f, maxLengthWorld);
            float clampedAlpha = Mathf.Clamp01(alpha);
            float clampedFillStrength = Mathf.Clamp01(fillStrength);
            float clampedFillIntensity = Mathf.Max(0.01f, fillIntensityScale);
            int clampedMaxFillLights = Mathf.Max(1, maxFillLights);
            color.a = 1f;

            bool changed = !runtimeSunShadowSettingsActive ||
                runtimeSunShadowEnabled != enabled ||
                HasSignificantValueChange(clampedLength, runtimeSunShadowMaxLength, RuntimeLengthFactorThreshold) ||
                HasSignificantValueChange(clampedAlpha, runtimeSunShadowAlpha, RuntimeOpacityFactorThreshold) ||
                runtimeScaleLengthByDayNight != scaleLengthByDayNight ||
                HasSignificantColorChange(color, runtimeSunShadowColor, RuntimeSettingColorThreshold) ||
                HasSignificantValueChange(clampedFillStrength, runtimePointLightFillStrength, RuntimeOpacityFactorThreshold) ||
                HasSignificantValueChange(clampedFillIntensity, runtimePointLightFillIntensityScale, RuntimeOpacityFactorThreshold) ||
                runtimeMaxFillPointLights != clampedMaxFillLights;

            runtimeSunShadowSettingsActive = true;
            runtimeSunShadowEnabled = enabled;
            runtimeSunShadowMaxLength = clampedLength;
            runtimeSunShadowAlpha = clampedAlpha;
            runtimeScaleLengthByDayNight = scaleLengthByDayNight;
            runtimeSunShadowColor = color;
            runtimePointLightFillStrength = clampedFillStrength;
            runtimePointLightFillIntensityScale = clampedFillIntensity;
            runtimeMaxFillPointLights = clampedMaxFillLights;

            if (changed)
            {
                forceMeshUpdate = true;
            }

            return changed;
        }

        public void RefreshRuntimeSunShadowNow()
        {
            ResolveReferences();
            RebuildFromWallLogicIfNeeded(true);
            UpdateShadowMesh(true);
        }

        public void RefreshRuntimeSunShadowIfNeeded()
        {
            ResolveReferences();
            RebuildFromWallLogicIfNeeded(true);
            UpdateShadowMesh(false);
        }

        public void MarkTopologyDirty()
        {
            topologyDirty = true;
            cachedTopologyVersion = -1;
            forceMeshUpdate = true;
        }

        public void RebuildFromWallLogicIfNeeded(bool allowDeferredWait = false)
        {
            RebuildFromWallLogic();
        }

        public void RebuildFromWallLogic()
        {
            using (RebuildFromWallLogicMarker.Auto())
            {
                RefreshTopologyFromCache();
                UpdateShadowMesh(false);
            }
        }

        public bool ApplyProjectedFillLights(IReadOnlyList<NtingCustomShadowSystem.ShadowLightInfo> pointLights, int pointLightCount)
        {
            int maxLights = ResolveMaxFillPointLights();
            int count = pointLights != null ? Mathf.Min(pointLightCount, pointLights.Count, maxLights) : 0;
            int signature = ComputeFillLightsSignature(pointLights, count);
            if (signature == fillLightsSignature)
            {
                return false;
            }

            fillPointLights.Clear();
            for (int i = 0; i < count; i++)
            {
                fillPointLights.Add(pointLights[i]);
            }

            fillPointLights.Sort(CompareFillLights);
            fillLightsSignature = signature;
            return true;
        }

        private void RefreshTopologyFromCache()
        {
            ResolveReferences();
            CampusWallShadowTopologyCache.FloorTopologyData topology = floor != null
                ? CampusWallShadowTopologyCache.EnsureBuilt(floor)
                : null;

            if (topology == null)
            {
                topologyDirty = false;
                topologyBuilt = true;
                cachedTopologyVersion = -1;
                pendingChunkUpdates.Clear();
                pendingFullChunkRebuild = true;
                return;
            }

            if (!topologyDirty && topologyBuilt && cachedTopologyVersion == topology.Version)
            {
                return;
            }

            pendingChunkUpdates.Clear();
            foreach (Vector2Int chunkKey in topology.LastUpdatedChunks)
            {
                pendingChunkUpdates.Add(chunkKey);
            }

            pendingFullChunkRebuild = !topologyBuilt || pendingChunkUpdates.Count == 0 || pendingChunkUpdates.Count >= topology.ActiveChunks.Count;
            cachedTopologyVersion = topology.Version;
            topologyDirty = false;
            topologyBuilt = true;
            forceMeshUpdate = true;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            MarkTopologyDirty();
            forceMeshUpdate = true;
        }

        private void OnDisable()
        {
            ClearMesh();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && NtingCustomShadowSystem.HasActiveSystemInstance())
            {
                return;
            }

            ResolveReferences();
            RebuildFromWallLogicIfNeeded(true);
            UpdateShadowMesh(false);
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
                normal));
        }

        private static int CompareIntervals(Vector2 a, Vector2 b)
        {
            return a.x.CompareTo(b.x);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= EdgeMergeTolerance;
        }

        private static int CompareFillLights(NtingCustomShadowSystem.ShadowLightInfo left, NtingCustomShadowSystem.ShadowLightInfo right)
        {
            int intensity = right.SortWeight.CompareTo(left.SortWeight);
            return intensity != 0 ? intensity : left.InstanceId.CompareTo(right.InstanceId);
        }

        private void ResolveReferences()
        {
            if (floor == null)
            {
                floor = GetComponentInParent<CampusFloorRoot>();
            }

            NormalizeShadowLengthDefaults();

            if (autoFindDayNight && dayNight == null)
            {
                dayNight = FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }

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
                shadowMesh = new Mesh { name = "Campus Projected Wall Shadow Mesh" };
                shadowMesh.MarkDynamic();
            }

            meshFilter.sharedMesh = shadowMesh;
            meshRenderer.sharedMaterial = ResolveShadowMaterial();
            meshRenderer.enabled = false;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.allowOcclusionWhenDynamic = false;
            ApplySorting();
        }

        private CampusMapRoot GetMapRoot()
        {
            if (floor != null)
            {
                CampusMapRoot root = floor.GetComponentInParent<CampusMapRoot>();
                if (root != null)
                {
                    return root;
                }
            }

            return Object.FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
        }

        private void ApplySorting()
        {
            if (meshRenderer == null)
            {
                return;
            }

            CampusMapRoot root = floor != null ? floor.GetComponentInParent<CampusMapRoot>() : null;
            int sortingStep = root != null ? root.SortingOrderStepPerFloor : 1000;
            int floorIndex = floor != null ? floor.FloorIndex : 1;
            meshRenderer.sortingLayerID = ResolveGroundSortingLayerId();
            meshRenderer.sortingOrder = floorIndex * sortingStep + CampusRenderSortingUtility.FloorOffset + 1;
        }

        private int ResolveGroundSortingLayerId()
        {
            int fallbackLayerId = SortingLayer.NameToID("Default");
            if (CampusRenderSortingUtility.TryGetGroundShadowSortingLayerId(out int groundLayerId))
            {
                return groundLayerId;
            }

            return fallbackLayerId;
        }

        private void NormalizeShadowLengthDefaults()
        {
            bool usesLegacyLengths =
                Mathf.Approximately(minShadowLength, LegacyMinShadowLength) &&
                Mathf.Approximately(maxShadowLength, LegacyMaxShadowLength);
            bool usesPreviousLengths =
                Mathf.Approximately(minShadowLength, PreviousMinShadowLength) &&
                Mathf.Approximately(maxShadowLength, PreviousMaxShadowLength);
            bool usesPreviousDefaultLengths =
                Mathf.Approximately(minShadowLength, PreviousDefaultMinShadowLength) &&
                Mathf.Approximately(maxShadowLength, PreviousDefaultMaxShadowLength);
            if (usesLegacyLengths || usesPreviousLengths || usesPreviousDefaultLengths)
            {
                minShadowLength = DefaultMinShadowLength;
                maxShadowLength = DefaultMaxShadowLength;
            }

            minShadowLength = Mathf.Clamp(minShadowLength, 0.01f, 8f);
            maxShadowLength = Mathf.Clamp(maxShadowLength, minShadowLength + 0.01f, 8f);

            if (Mathf.Approximately(shadowWidth, LegacyShadowSourceWidth) ||
                Mathf.Approximately(shadowWidth, PreviousDefaultShadowSourceWidth))
            {
                shadowWidth = DefaultShadowSourceWidth;
            }

            shadowWidth = Mathf.Clamp(shadowWidth, 0.02f, DefaultShadowSourceWidth);
            if (Mathf.Approximately(alphaMultiplier, CurrentDefaultAlphaMultiplierBeforeLengthen) ||
                Mathf.Approximately(alphaMultiplier, PreviousDefaultAlphaMultiplier) ||
                Mathf.Approximately(alphaMultiplier, LegacyAlphaMultiplier) ||
                Mathf.Approximately(alphaMultiplier, PreviousAlphaMultiplier))
            {
                alphaMultiplier = DefaultAlphaMultiplier;
            }

            alphaMultiplier = Mathf.Clamp(alphaMultiplier, 0.1f, 4f);
        }

        private Material ResolveShadowMaterial()
        {
            if (shadowMaterial != null)
            {
                return shadowMaterial;
            }

#if UNITY_EDITOR
            shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialAssetPath);
            if (shadowMaterial == null)
            {
                Shader editorShader = ResolveShadowShader();
                if (editorShader != null)
                {
                    shadowMaterial = new Material(editorShader) { name = "CampusProjectedShadow" };
                    AssetDatabase.CreateAsset(shadowMaterial, ShadowMaterialAssetPath);
                    AssetDatabase.SaveAssets();
                }
            }

            if (shadowMaterial != null)
            {
                return shadowMaterial;
            }
#endif

            if (runtimeFallbackMaterial == null)
            {
                Shader shader = ResolveShadowShader();
                if (shader != null)
                {
                    runtimeFallbackMaterial = new Material(shader) { name = "CampusProjectedShadow_Runtime" };
                }
            }

            return runtimeFallbackMaterial;
        }

        private static Shader ResolveShadowShader()
        {
            Shader shader = Shader.Find(ShadowShaderName);
            if (shader == null)
            {
                shader = Resources.Load<Shader>(ShadowShaderResourcePath);
            }

            return shader;
        }

        private void UpdateShadowMesh(bool force)
        {
            using (UpdateShadowMeshMarker.Auto())
            {
            if (shadowMesh == null)
            {
                return;
            }

            Vector2 direction = dayNight != null ? dayNight.ShadowDirection : Vector2.down;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.down;
            }
            else
            {
                direction.Normalize();
            }

            float lengthFactor = dayNight != null ? dayNight.ShadowLengthFactor : 0.45f;
            float opacityFactor = dayNight != null ? dayNight.ShadowOpacityFactor : 0.08f;
            float resolvedOpacity = ResolveShadowOpacity(opacityFactor);
            Color resolvedShadowColor = runtimeSunShadowSettingsActive ? runtimeSunShadowColor : Color.black;
            bool fillEnabled = pointLightsCanBrightenProjectedShadows && ResolveFillStrength() > 0f;
            bool fillChanged = fillEnabled && fillLightsSignature != lastAppliedFillLightsSignature;
            bool directionChanged = HasSignificantDirectionChange(direction);
            bool topologyChanged = pendingFullChunkRebuild || pendingChunkUpdates.Count > 0;
            bool valuesChanged =
                HasSignificantValueChange(lengthFactor, lastShadowLengthFactor, RuntimeLengthFactorThreshold) ||
                HasSignificantValueChange(opacityFactor, lastShadowOpacityFactor, RuntimeOpacityFactorThreshold) ||
                HasSignificantValueChange(resolvedOpacity, lastResolvedShadowOpacity, RuntimeOpacityFactorThreshold);
            bool colorChanged = HasSignificantColorChange(resolvedShadowColor, lastResolvedShadowColor, RuntimeSettingColorThreshold);
            bool shouldRefresh = force || forceMeshUpdate || directionChanged || valuesChanged || colorChanged || fillChanged || topologyChanged;
            if (!shouldRefresh)
            {
                return;
            }

            bool immediateRefresh = force || forceMeshUpdate;
            if (!immediateRefresh && Application.isPlaying && Time.time < nextRuntimeShadowRefreshTime)
            {
                return;
            }

            ScheduleNextRuntimeShadowRefresh();
            lastShadowDirection = direction;
            lastShadowLengthFactor = lengthFactor;
            lastShadowOpacityFactor = opacityFactor;
            lastResolvedShadowOpacity = resolvedOpacity;
            lastResolvedShadowColor = resolvedShadowColor;
            lastAppliedFillLightsSignature = fillEnabled ? fillLightsSignature : 0;
            forceMeshUpdate = false;

            if (runtimeSunShadowSettingsActive && (!runtimeSunShadowEnabled || runtimeSunShadowMaxLength <= 0.001f || runtimeSunShadowAlpha <= 0.001f))
            {
                shadowMesh.Clear(false);
                ClearShadowChunkMeshes();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }

                return;
            }

            float shadowLength = ResolveShadowLength(lengthFactor);
            float opacity = resolvedOpacity;
            Color shadowColor = ResolveShadowColor(opacity);
            Vector3 offset = new Vector3(direction.x, direction.y, 0f) * shadowLength;
            Vector3 localOffset = transform.InverseTransformVector(offset);
            if (!fillEnabled || opacity <= 0.001f)
            {
                fillPointLights.Clear();
            }

            Color far = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0f);
            CampusWallShadowTopologyCache.FloorTopologyData topology = floor != null
                ? CampusWallShadowTopologyCache.EnsureBuilt(floor)
                : null;
            if (topology == null)
            {
                ClearShadowChunkMeshes();
                pendingChunkUpdates.Clear();
                pendingFullChunkRebuild = false;
                return;
            }

            shadowMesh.Clear(false);
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            bool requiresFullChunkRebuild = force || directionChanged || valuesChanged || colorChanged || fillChanged || pendingFullChunkRebuild;
            if (requiresFullChunkRebuild)
            {
                RebuildAllShadowChunks(topology, localOffset, shadowColor, far, direction);
            }
            else
            {
                RebuildPendingShadowChunks(topology, localOffset, shadowColor, far, direction);
            }

            pendingChunkUpdates.Clear();
            pendingFullChunkRebuild = false;
            }
        }

        private void ScheduleNextRuntimeShadowRefresh()
        {
            if (Application.isPlaying)
            {
                nextRuntimeShadowRefreshTime = Time.time + RuntimeShadowRefreshInterval;
            }
        }

        private bool HasSignificantDirectionChange(Vector2 direction)
        {
            if (lastShadowDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            float threshold = Mathf.Max(RuntimeDirectionAngleThresholdDegrees, rebuildDirectionThreshold);
            return Vector2.Angle(lastShadowDirection, direction) >= threshold;
        }

        private static bool HasSignificantValueChange(float value, float previous, float threshold)
        {
            if (previous < -0.5f || float.IsNaN(previous) || float.IsInfinity(previous))
            {
                return true;
            }

            return Mathf.Abs(value - previous) >= threshold;
        }

        private static bool HasSignificantColorChange(Color color, Color previous, float threshold)
        {
            if (float.IsNaN(previous.r) || float.IsNaN(previous.g) || float.IsNaN(previous.b) || float.IsNaN(previous.a) ||
                float.IsInfinity(previous.r) || float.IsInfinity(previous.g) || float.IsInfinity(previous.b) || float.IsInfinity(previous.a))
            {
                return true;
            }

            return Mathf.Abs(color.r - previous.r) >= threshold ||
                   Mathf.Abs(color.g - previous.g) >= threshold ||
                   Mathf.Abs(color.b - previous.b) >= threshold ||
                   Mathf.Abs(color.a - previous.a) >= threshold;
        }

        private void BeginShadowChunkMeshUpdate(IEnumerable<Vector2Int> chunkKeys)
        {
            foreach (Vector2Int chunkKey in chunkKeys)
            {
                if (shadowChunks.TryGetValue(chunkKey, out ShadowMeshChunk chunk))
                {
                    chunk.ClearData();
                }
            }
        }

        private void RebuildAllShadowChunks(CampusWallShadowTopologyCache.FloorTopologyData topology, Vector3 localOffset, Color shadowColor, Color far, Vector2 direction)
        {
            List<Vector2Int> chunkKeys = new List<Vector2Int>(topology.ActiveChunks);
            BeginShadowChunkMeshUpdate(shadowChunks.Keys);
            foreach (Vector2Int chunkKey in chunkKeys)
            {
                RebuildShadowChunk(topology, chunkKey, localOffset, shadowColor, far, direction);
            }

            ApplyShadowChunkMeshes(chunkKeys);
            ClearUnusedShadowChunkMeshes(topology.ActiveChunks);
        }

        private void RebuildPendingShadowChunks(CampusWallShadowTopologyCache.FloorTopologyData topology, Vector3 localOffset, Color shadowColor, Color far, Vector2 direction)
        {
            if (pendingChunkUpdates.Count == 0)
            {
                return;
            }

            BeginShadowChunkMeshUpdate(pendingChunkUpdates);
            foreach (Vector2Int chunkKey in pendingChunkUpdates)
            {
                RebuildShadowChunk(topology, chunkKey, localOffset, shadowColor, far, direction);
            }

            ApplyShadowChunkMeshes(pendingChunkUpdates);
            foreach (Vector2Int chunkKey in pendingChunkUpdates)
            {
                if (!topology.ActiveChunks.Contains(chunkKey) && shadowChunks.TryGetValue(chunkKey, out ShadowMeshChunk chunk))
                {
                    chunk.ClearMesh();
                }
            }
        }

        private void RebuildShadowChunk(CampusWallShadowTopologyCache.FloorTopologyData topology, Vector2Int chunkKey, Vector3 localOffset, Color shadowColor, Color far, Vector2 direction)
        {
            if (!topology.ChunkEdges.TryGetValue(chunkKey, out List<CampusWallShadowTopologyCache.WallShadowEdge> sourceEdges) || sourceEdges == null)
            {
                if (shadowChunks.TryGetValue(chunkKey, out ShadowMeshChunk emptyChunk))
                {
                    emptyChunk.ClearMesh();
                }

                return;
            }

            ShadowMeshChunk chunk = GetOrCreateShadowChunk(chunkKey);
            for (int i = 0; i < sourceEdges.Count; i++)
            {
                CampusWallShadowTopologyCache.WallShadowEdge sourceEdge = sourceEdges[i];
                if (Vector2.Dot(sourceEdge.Normal, direction) <= EdgeProjectionDotThreshold)
                {
                    continue;
                }

                Vector3 edgeA = transform.InverseTransformPoint(sourceEdge.WorldA);
                Vector3 edgeB = transform.InverseTransformPoint(sourceEdge.WorldB);
                Vector3 farB = edgeB + localOffset;
                Vector3 farA = edgeA + localOffset;
                Color nearA = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * ResolveProjectedFillMultiplier(edgeA));
                Color nearB = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * ResolveProjectedFillMultiplier(edgeB));
                chunk.AddQuad(edgeA, edgeB, farB, farA, nearA, nearB, far, far);
            }
        }

        private ShadowMeshChunk GetOrCreateShadowChunk(Vector2Int chunkKey)
        {
            if (!shadowChunks.TryGetValue(chunkKey, out ShadowMeshChunk chunk))
            {
                chunk = ShadowMeshChunk.Create(transform, chunkKey, "ProjectedWallShadow_Chunk");
                shadowChunks.Add(chunkKey, chunk);
            }

            return chunk;
        }

        private void ApplyShadowChunkMeshes(IEnumerable<Vector2Int> chunkKeys)
        {
            Material material = ResolveShadowMaterial();
            int sortingLayerId = ResolveGroundSortingLayerId();
            int sortingOrder = meshRenderer != null ? meshRenderer.sortingOrder : 0;
            foreach (Vector2Int chunkKey in chunkKeys)
            {
                if (shadowChunks.TryGetValue(chunkKey, out ShadowMeshChunk chunk))
                {
                    chunk.Apply(material, sortingLayerId, sortingOrder);
                }
            }
        }

        private void ClearUnusedShadowChunkMeshes(HashSet<Vector2Int> activeChunks)
        {
            foreach (KeyValuePair<Vector2Int, ShadowMeshChunk> pair in shadowChunks)
            {
                if (!activeChunks.Contains(pair.Key))
                {
                    pair.Value.ClearMesh();
                }
            }
        }

        private void ClearShadowChunkMeshes()
        {
            foreach (ShadowMeshChunk chunk in shadowChunks.Values)
            {
                chunk.ClearMesh();
            }
        }

        private float ResolveShadowLength(float dayNightLengthFactor)
        {
            if (!runtimeSunShadowSettingsActive)
            {
                return Mathf.Lerp(minShadowLength, maxShadowLength, Mathf.Clamp01(dayNightLengthFactor));
            }

            float lengthFactor = runtimeScaleLengthByDayNight ? Mathf.Clamp01(dayNightLengthFactor) : 1f;
            return Mathf.Max(0f, runtimeSunShadowMaxLength * lengthFactor);
        }

        private float ResolveShadowOpacity(float dayNightOpacityFactor)
        {
            if (!runtimeSunShadowSettingsActive)
            {
                return Mathf.Clamp01(dayNightOpacityFactor * alphaMultiplier);
            }

            return Mathf.Clamp01(runtimeSunShadowAlpha);
        }

        private Color ResolveShadowColor(float opacity)
        {
            Color color = runtimeSunShadowSettingsActive ? runtimeSunShadowColor : Color.black;
            color.a = Mathf.Clamp01(opacity * SunTintShadowAlphaMultiplier);
            return color;
        }

        private float ResolveFillStrength()
        {
            return runtimeSunShadowSettingsActive ? runtimePointLightFillStrength : pointLightFillStrength;
        }

        private float ResolveFillIntensityScale()
        {
            return runtimeSunShadowSettingsActive ? runtimePointLightFillIntensityScale : pointLightFillIntensityScale;
        }

        private int ResolveMaxFillPointLights()
        {
            return runtimeSunShadowSettingsActive ? runtimeMaxFillPointLights : maxFillPointLights;
        }

        private float ResolveProjectedFillMultiplier(Vector3 localPosition)
        {
            if (fillPointLights.Count == 0)
            {
                return 1f;
            }

            return NtingCustomShadowSystem.ResolvePointLightFillMultiplier(
                fillPointLights,
                fillPointLights.Count,
                0,
                transform.TransformPoint(localPosition),
                ResolveFillStrength(),
                ResolveFillIntensityScale());
        }

        private static int ComputeFillLightsSignature(IReadOnlyList<NtingCustomShadowSystem.ShadowLightInfo> lights, int count)
        {
            unchecked
            {
                int signature = 17;
                signature = signature * 31 + count;
                if (lights == null)
                {
                    return signature;
                }

                for (int i = 0; i < count; i++)
                {
                    NtingCustomShadowSystem.ShadowLightInfo light = lights[i];
                    signature = signature * 31 + light.InstanceId;
                    signature = signature * 31 + Quantize(light.Position.x);
                    signature = signature * 31 + Quantize(light.Position.y);
                    signature = signature * 31 + Quantize(light.Position.z);
                    signature = signature * 31 + Quantize(light.Radius);
                    signature = signature * 31 + Quantize(light.Intensity);
                    signature = signature * 31 + Quantize(light.ShadowFillWeight);
                }

                return signature;
            }
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        private void ClearMesh()
        {
            vertices.Clear();
            triangles.Clear();
            colors.Clear();
            if (shadowMesh != null)
            {
                shadowMesh.Clear(false);
            }

            ClearShadowChunkMeshes();
            topologyDirty = true;
            topologyBuilt = false;
            cachedTopologyVersion = -1;
            forceMeshUpdate = true;
            lastAppliedFillLightsSignature = int.MinValue;
        }

        private sealed class ShadowMeshChunk
        {
            private readonly MeshFilter filter;
            private readonly MeshRenderer renderer;
            private readonly Mesh mesh;
            private readonly List<Vector3> vertices = new List<Vector3>(256);
            private readonly List<int> triangles = new List<int>(384);
            private readonly List<Color> colors = new List<Color>(256);

            private ShadowMeshChunk(GameObject gameObject, MeshFilter filter, MeshRenderer renderer, Mesh mesh)
            {
                GameObject = gameObject;
                this.filter = filter;
                this.renderer = renderer;
                this.mesh = mesh;
            }

            public GameObject GameObject { get; }

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
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowOcclusionWhenDynamic = false;
                renderer.enabled = false;
                return new ShadowMeshChunk(chunkObject, filter, renderer, mesh);
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

            public void Apply(Material material, int sortingLayerId, int sortingOrder)
            {
                if (vertices.Count == 0 || material == null)
                {
                    ClearMesh();
                    return;
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
            }

            public void ClearMesh()
            {
                mesh.Clear(false);
                renderer.enabled = false;
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
            public readonly Vector2 Normal;

            public ShadowEdge(Vector3 a, Vector3 b, Vector2 normal)
            {
                A = a;
                B = b;
                Normal = normal;
            }
        }
    }
}
