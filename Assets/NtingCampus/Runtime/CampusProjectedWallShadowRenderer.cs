using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
        private const string LegacyObjectFallbackCasterName = "Dynamic Object ShadowCaster 2D";
        private const string ShadowShaderName = "Nting Campus/2D/Projected Shadow Unlit";
        private const string ShadowShaderResourcePath = "Shaders/CampusProjectedShadowUnlit";
        private const string ShadowMaterialAssetPath = "Assets/NtingCampus/Materials/CampusProjectedShadow.mat";
        private const float EdgeProjectionDotThreshold = 0.05f;
        private const float DefaultMinShadowLength = 0.24f;
        private const float DefaultMaxShadowLength = 0.95f;
        private const float PreviousMinShadowLength = 0.06f;
        private const float PreviousMaxShadowLength = 1.10f;
        private const float LegacyMinShadowLength = 0.20f;
        private const float LegacyMaxShadowLength = 3.20f;
        private const float DefaultAlphaMultiplier = 2.25f;
        private const float PreviousAlphaMultiplier = 1.35f;
        private const float LegacyAlphaMultiplier = 1.0f;
        private const float WallTopHalfWidth = 0.205f;
        private const float DefaultShadowSourceWidth = WallTopHalfWidth * 2f;
        private const float LegacyShadowSourceWidth = 0.33f;
        private const float WallCellHalf = 0.5f;
        private const float ObjectSourceInsetFromCellEdge = WallCellHalf - WallTopHalfWidth;
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

        private static Material runtimeFallbackMaterial;

        private readonly List<ShadowEdge> cachedEdges = new List<ShadowEdge>();
        private readonly List<ShadowSourceRect> sourceRects = new List<ShadowSourceRect>();
        private readonly List<Vector2> coveredIntervals = new List<Vector2>();
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
            float sourceHalfX = Mathf.Min(WallTopHalfWidth, Mathf.Max(0.01f, Mathf.Abs(shadowWidth) * 0.5f));
            float sourceHalfY = Mathf.Min(WallTopHalfWidth, Mathf.Max(0.01f, Mathf.Abs(shadowWidth) * 0.5f));

            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!wallLogic.HasTile(cell))
                {
                    continue;
                }

                Vector3 center = wallLogic.GetCellCenterWorld(cell);
                int connectionMask = CampusWallTileUtility.GetConnectionMask(wallLogic, cell);
                AddWallTopSourceRects(center, cellSize, connectionMask, sourceHalfX, sourceHalfY);
            }

            AddPlacedObjectSourceRects(cellSize);

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

        private void AddPlacedObjectSourceRects(Vector3 cellSize)
        {
            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusPlacedObject[] placedObjects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            float scaleX = Mathf.Abs(cellSize.x);
            float scaleY = Mathf.Abs(cellSize.y);
            for (int i = 0; i < placedObjects.Length; i++)
            {
                CampusPlacedObject placed = placedObjects[i];
                if (placed == null)
                {
                    continue;
                }

                RemoveLegacyObjectShadowCaster(placed.transform);
                if (!placed.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2Int footprint = placed.RotatedFootprintSize;
                float halfWidth = Mathf.Max(WallTopHalfWidth * scaleX, footprint.x * scaleX * 0.5f - ObjectSourceInsetFromCellEdge * scaleX);
                float halfHeight = Mathf.Max(WallTopHalfWidth * scaleY, footprint.y * scaleY * 0.5f - ObjectSourceInsetFromCellEdge * scaleY);
                Vector3 center = placed.transform.position;
                sourceRects.Add(new ShadowSourceRect(
                    center.x - halfWidth,
                    center.y - halfHeight,
                    center.x + halfWidth,
                    center.y + halfHeight));
                RemoveDuplicateSourceRectAtEnd();
            }
        }

        private static void RemoveLegacyObjectShadowCaster(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform fallback = root.Find(LegacyObjectFallbackCasterName);
            if (fallback != null)
            {
                DestroyShadowObject(fallback.gameObject);
            }
        }

        private static void DestroyShadowObject(GameObject target)
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

        private void AddWallTopSourceRects(Vector3 center, Vector3 cellSize, int connectionMask, float sourceHalfX, float sourceHalfY)
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
            if (usesLegacyLengths || usesPreviousLengths)
            {
                minShadowLength = DefaultMinShadowLength;
                maxShadowLength = DefaultMaxShadowLength;
            }

            minShadowLength = Mathf.Clamp(minShadowLength, 0.01f, 8f);
            maxShadowLength = Mathf.Clamp(maxShadowLength, minShadowLength + 0.01f, 8f);

            if (Mathf.Approximately(shadowWidth, LegacyShadowSourceWidth))
            {
                shadowWidth = DefaultShadowSourceWidth;
            }

            shadowWidth = Mathf.Clamp(shadowWidth, 0.02f, DefaultShadowSourceWidth);
            if (Mathf.Approximately(alphaMultiplier, LegacyAlphaMultiplier) ||
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
            bool directionChanged = Vector2.SqrMagnitude(direction - lastShadowDirection) > rebuildDirectionThreshold * rebuildDirectionThreshold;
            bool valuesChanged = !Mathf.Approximately(lengthFactor, lastShadowLengthFactor) || !Mathf.Approximately(opacityFactor, lastShadowOpacityFactor);
            if (!force && !forceMeshUpdate && !directionChanged && !valuesChanged)
            {
                return;
            }

            lastShadowDirection = direction;
            lastShadowLengthFactor = lengthFactor;
            lastShadowOpacityFactor = opacityFactor;
            forceMeshUpdate = false;

            float shadowLength = Mathf.Lerp(minShadowLength, maxShadowLength, Mathf.Clamp01(lengthFactor));
            float opacity = Mathf.Clamp01(opacityFactor * alphaMultiplier);
            Vector3 offset = new Vector3(direction.x, direction.y, 0f) * shadowLength;

            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            Color near = new Color(0f, 0f, 0f, opacity);
            Color far = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < cachedEdges.Count; i++)
            {
                ShadowEdge edge = cachedEdges[i];
                if (Vector2.Dot(edge.Normal, direction) <= EdgeProjectionDotThreshold)
                {
                    continue;
                }

                int index = vertices.Count;
                vertices.Add(edge.A);
                vertices.Add(edge.B);
                vertices.Add(edge.B + offset);
                vertices.Add(edge.A + offset);

                colors.Add(near);
                colors.Add(near);
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
