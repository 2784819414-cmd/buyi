using System.Collections.Generic;
using UnityEngine;
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

        private Mesh shadowMesh;
        private Vector2 lastShadowDirection = Vector2.zero;
        private float lastShadowLengthFactor = -1f;
        private float lastShadowOpacityFactor = -1f;
        private bool forceMeshUpdate = true;

        public static CampusProjectedWallShadowRenderer EnsureForFloor(CampusFloorRoot targetFloor)
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
            renderer.RebuildFromWallLogic();
            return renderer;
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
                !Mathf.Approximately(runtimeSunShadowMaxLength, clampedLength) ||
                !Mathf.Approximately(runtimeSunShadowAlpha, clampedAlpha) ||
                runtimeScaleLengthByDayNight != scaleLengthByDayNight ||
                runtimeSunShadowColor != color ||
                !Mathf.Approximately(runtimePointLightFillStrength, clampedFillStrength) ||
                !Mathf.Approximately(runtimePointLightFillIntensityScale, clampedFillIntensity) ||
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
            UpdateShadowMesh(true);
        }

        public void RebuildFromWallLogic()
        {
            ResolveReferences();
            cachedEdges.Clear();
            sourceRects.Clear();

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                ClearMesh();
                return;
            }

            wallLogic.CompressBounds();
            BoundsInt bounds = wallLogic.cellBounds;
            Vector3 cellSize = floor != null && floor.Grid != null ? floor.Grid.cellSize : Vector3.one;
            float sourceHalfX = Mathf.Min(WallBottomHalfWidth, Mathf.Max(0.01f, Mathf.Abs(shadowWidth) * 0.5f));
            float sourceHalfY = Mathf.Min(WallBottomHalfWidth, Mathf.Max(0.01f, Mathf.Abs(shadowWidth) * 0.5f));

            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!wallLogic.HasTile(cell))
                {
                    continue;
                }

                Vector3 center = wallLogic.GetCellCenterWorld(cell);
                int connectionMask = CampusWallTileUtility.GetConnectionMask(wallLogic, cell);
                AddWallBaseSourceRects(center, cellSize, connectionMask, sourceHalfX, sourceHalfY);
            }

            for (int i = 0; i < sourceRects.Count; i++)
            {
                AddSourceRectVisibleEdges(sourceRects[i]);
            }

            forceMeshUpdate = true;
            UpdateShadowMesh(true);
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (cachedEdges.Count == 0)
            {
                RebuildFromWallLogic();
                return;
            }

            forceMeshUpdate = true;
        }

        private void OnDisable()
        {
            ClearMesh();
        }

        private void LateUpdate()
        {
            ResolveReferences();
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
                dayNight = CampusDayNightController.EnsureSceneController(GetMapRoot());
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
            meshRenderer.enabled = true;
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
            bool fillEnabled = pointLightsCanBrightenProjectedShadows && ResolveFillStrength() > 0f;
            bool directionChanged = Vector2.SqrMagnitude(direction - lastShadowDirection) > rebuildDirectionThreshold * rebuildDirectionThreshold;
            bool valuesChanged = !Mathf.Approximately(lengthFactor, lastShadowLengthFactor) || !Mathf.Approximately(opacityFactor, lastShadowOpacityFactor);
            if (!force && !forceMeshUpdate && !directionChanged && !valuesChanged && !fillEnabled)
            {
                return;
            }

            lastShadowDirection = direction;
            lastShadowLengthFactor = lengthFactor;
            lastShadowOpacityFactor = opacityFactor;
            forceMeshUpdate = false;

            if (runtimeSunShadowSettingsActive && (!runtimeSunShadowEnabled || runtimeSunShadowMaxLength <= 0.001f || runtimeSunShadowAlpha <= 0.001f))
            {
                shadowMesh.Clear(false);
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }

                return;
            }

            float shadowLength = ResolveShadowLength(lengthFactor);
            float opacity = ResolveShadowOpacity(opacityFactor);
            Color shadowColor = ResolveShadowColor(opacity);
            Vector3 offset = new Vector3(direction.x, direction.y, 0f) * shadowLength;
            Vector3 localOffset = transform.InverseTransformVector(offset);
            if (fillEnabled && opacity > 0.001f)
            {
                CollectProjectedFillLights();
            }
            else
            {
                fillPointLights.Clear();
            }

            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            Color far = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0f);
            for (int i = 0; i < cachedEdges.Count; i++)
            {
                ShadowEdge edge = cachedEdges[i];
                if (Vector2.Dot(edge.Normal, direction) <= EdgeProjectionDotThreshold)
                {
                    continue;
                }

                int index = vertices.Count;
                Vector3 edgeA = edge.A;
                Vector3 edgeB = edge.B;
                Vector3 farB = edgeB + localOffset;
                Vector3 farA = edgeA + localOffset;
                vertices.Add(edgeA);
                vertices.Add(edgeB);
                vertices.Add(farB);
                vertices.Add(farA);

                Color nearA = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * ResolveProjectedFillMultiplier(edgeA));
                Color nearB = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * ResolveProjectedFillMultiplier(edgeB));
                colors.Add(nearA);
                colors.Add(nearB);
                colors.Add(far);
                colors.Add(far);

                triangles.Add(index);
                triangles.Add(index + 1);
                triangles.Add(index + 2);
                triangles.Add(index);
                triangles.Add(index + 2);
                triangles.Add(index + 3);
            }

            shadowMesh.Clear(false);
            if (vertices.Count == 0)
            {
                return;
            }

            shadowMesh.SetVertices(vertices);
            shadowMesh.SetColors(colors);
            shadowMesh.SetTriangles(triangles, 0, true);
            shadowMesh.RecalculateBounds();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
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
            color.a = opacity;
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

        private void CollectProjectedFillLights()
        {
            fillPointLights.Clear();
            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (!NtingCustomShadowSystem.IsPointLightUsableForCustomShadow(light, true))
                {
                    continue;
                }

                fillPointLights.Add(NtingCustomShadowSystem.CreateShadowLightInfo(light));
            }

            fillPointLights.Sort(CompareFillLights);
            int maxLights = ResolveMaxFillPointLights();
            if (fillPointLights.Count > maxLights)
            {
                fillPointLights.RemoveRange(maxLights, fillPointLights.Count - maxLights);
            }
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

        private void ClearMesh()
        {
            cachedEdges.Clear();
            sourceRects.Clear();
            vertices.Clear();
            triangles.Clear();
            colors.Clear();
            if (shadowMesh != null)
            {
                shadowMesh.Clear(false);
            }

            forceMeshUpdate = true;
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
