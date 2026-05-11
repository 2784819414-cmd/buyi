using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NtingFiniteWallSunShadowRenderer : MonoBehaviour
    {
        public const string RendererName = "Wall SunShadow Proxies";
        public const string ProxyNamePrefix = "WallSunShadowProxy_";

        private const float WallCellHalf = 0.5f;
        private const float WallBottomHalfWidth = 0.330f;
        private const float HorizontalCenterYOffset = 0.0975f;
        private const float HorizontalSouthBottomHalfWidth = 0.430f;
        private const float HorizontalNorthBottomHalfWidth = 0.235f;
        private const float MinSize = 0.01f;
        private const float MergeTolerance = 0.0005f;
        private const float DirectionRebuildTolerance = 0.0004f;
        private const float DefaultWallShadowLength = 0.4f;
        private const float DefaultWallShadowAlpha = 0.6f;
        private const float DefaultNightShadowAlphaFactor = 0.6f;
        private const float LegacyDefaultWallShadowLength = 1.05f;
        private const float LegacyDefaultWallShadowAlpha = 0.16f;
        private const float LegacyDefaultNightShadowAlphaFactor = 0.22f;
        private const float ObjectMatchedDefaultNightShadowAlphaFactor = 0.18f;
        private const float StrongNightShadowAlphaFactor = 0.35f;
        private const float StrongDefaultWallShadowLength = 0.85f;
        private const float StrongDefaultWallShadowAlpha = 0.48f;
        private const float DenseDefaultWallShadowLength = 0.5f;
        private const float DenseDefaultWallShadowAlpha = 0.6f;
        private const float OpaqueDefaultWallShadowAlpha = 1f;
        private const float ObjectMatchedDefaultWallShadowLength = 1f;
        private const float ObjectMatchedDefaultWallShadowAlpha = 0.22f;
        private const float PreviousDefaultWallShadowLength = 0.72f;
        private const float PreviousDefaultWallShadowAlpha = 0.28f;
        private const float PreviousDefaultNightShadowAlphaFactor = 0.24f;

        [SerializeField] private CampusFloorRoot floor;
        public bool enableSunShadow = true;
        public bool useDayNightController = true;
        public Vector2 sunDirection2D = new Vector2(1f, -0.4f);
        [Min(0f)] public float maxWallShadowLengthWorld = DefaultWallShadowLength;
        [Range(0f, 1f)] public float shadowAlpha = DefaultWallShadowAlpha;
        public bool castNightShadow = true;
        [Range(0f, 1f)] public float nightShadowAlphaFactor = DefaultNightShadowAlphaFactor;
        public Color shadowColor = new Color(0.05f, 0.08f, 0.12f, 1f);
        public int sortingOrderOffset = 0;

        private readonly List<ShadowRect> baseRects = new List<ShadowRect>(256);
        private readonly List<ShadowRect> shadowRects = new List<ShadowRect>(256);
        private readonly List<float> yEdges = new List<float>(512);
        private readonly List<Interval> intervals = new List<Interval>(128);
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<int> triangles = new List<int>(1536);
        private readonly List<Color32> colors = new List<Color32>(1024);

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh shadowMesh;
        private CampusDayNightController dayNightController;
        private Vector2 lastBuiltDirection;
        private float lastBuiltLength = -1f;
        private bool meshDirty = true;
        private MaterialPropertyBlock propertyBlock;
        private static Material sharedShadowMaterial;

        public static NtingFiniteWallSunShadowRenderer EnsureForFloor(CampusFloorRoot targetFloor)
        {
            if (targetFloor == null)
            {
                return null;
            }

            Transform parent = ResolveParent(targetFloor);
            if (parent == null)
            {
                return null;
            }

            NtingFiniteWallSunShadowRenderer renderer = FindRenderer(parent);
            if (renderer == null)
            {
                GameObject rendererObject = new GameObject(RendererName);
                rendererObject.transform.SetParent(parent, false);
                renderer = rendererObject.AddComponent<NtingFiniteWallSunShadowRenderer>();
            }

            renderer.gameObject.name = RendererName;
            renderer.floor = targetFloor;
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            renderer.ApplyCurrentWallShadowDefaultsIfNeeded();
            DestroyDuplicateRenderers(parent, renderer);
            renderer.RebuildFromFloor();
            return renderer;
        }

        public static void ClearForFloor(CampusFloorRoot targetFloor)
        {
            Transform parent = ResolveParent(targetFloor);
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == RendererName)
                {
                    DestroyGeneratedObject(child.gameObject);
                }
            }

            NtingFiniteWallSunShadowRenderer[] renderers = parent.GetComponentsInChildren<NtingFiniteWallSunShadowRenderer>(true);
            for (int i = renderers.Length - 1; i >= 0; i--)
            {
                if (renderers[i] != null && renderers[i].gameObject.name != RendererName)
                {
                    DestroyGeneratedObject(renderers[i].gameObject);
                }
            }
        }

        public void RebuildFromFloor()
        {
            EnsureMeshComponents();
            ClearLegacyProxyChildren();
            baseRects.Clear();

            CampusFloorRoot targetFloor = ResolveFloor();
            if (targetFloor == null || targetFloor.Grid == null)
            {
                ClearMesh();
                return;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(targetFloor);
            if (wallLogic == null)
            {
                ClearMesh();
                return;
            }

            wallLogic.CompressBounds();
            BoundsInt bounds = wallLogic.cellBounds;
            Vector2 cellSize = GetGridCellSize(targetFloor.Grid);
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!wallLogic.HasTile(cell))
                {
                    continue;
                }

                AddWallCellRects(wallLogic, cell, CampusWallTileUtility.GetConnectionMask(wallLogic, cell), cellSize);
            }

            meshDirty = true;
            RefreshShadows();
        }

        private void OnEnable()
        {
            if (floor == null)
            {
                floor = GetComponentInParent<CampusFloorRoot>();
            }

            EnsureMeshComponents();
            if (baseRects.Count == 0)
            {
                RebuildFromFloor();
            }
            else
            {
                RefreshShadows();
            }
        }

        private void OnDisable()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (shadowMesh != null)
            {
                DestroyGeneratedObject(shadowMesh);
                shadowMesh = null;
            }
        }

        private void OnValidate()
        {
            maxWallShadowLengthWorld = Mathf.Max(0f, maxWallShadowLengthWorld);
            shadowAlpha = Mathf.Clamp01(shadowAlpha);
            nightShadowAlphaFactor = Mathf.Clamp01(nightShadowAlphaFactor);
            meshDirty = true;
            RefreshShadows();
        }

        private void ApplyCurrentWallShadowDefaultsIfNeeded()
        {
            bool changed = false;
            if (Mathf.Approximately(maxWallShadowLengthWorld, LegacyDefaultWallShadowLength) ||
                Mathf.Approximately(maxWallShadowLengthWorld, StrongDefaultWallShadowLength) ||
                Mathf.Approximately(maxWallShadowLengthWorld, DenseDefaultWallShadowLength) ||
                Mathf.Approximately(maxWallShadowLengthWorld, ObjectMatchedDefaultWallShadowLength) ||
                Mathf.Approximately(maxWallShadowLengthWorld, PreviousDefaultWallShadowLength))
            {
                maxWallShadowLengthWorld = DefaultWallShadowLength;
                changed = true;
            }

            if (Mathf.Approximately(shadowAlpha, LegacyDefaultWallShadowAlpha) ||
                Mathf.Approximately(shadowAlpha, StrongDefaultWallShadowAlpha) ||
                Mathf.Approximately(shadowAlpha, DenseDefaultWallShadowAlpha) ||
                Mathf.Approximately(shadowAlpha, OpaqueDefaultWallShadowAlpha) ||
                Mathf.Approximately(shadowAlpha, ObjectMatchedDefaultWallShadowAlpha) ||
                Mathf.Approximately(shadowAlpha, PreviousDefaultWallShadowAlpha))
            {
                shadowAlpha = DefaultWallShadowAlpha;
                changed = true;
            }

            if (Mathf.Approximately(nightShadowAlphaFactor, LegacyDefaultNightShadowAlphaFactor) ||
                Mathf.Approximately(nightShadowAlphaFactor, ObjectMatchedDefaultNightShadowAlphaFactor) ||
                Mathf.Approximately(nightShadowAlphaFactor, StrongNightShadowAlphaFactor) ||
                Mathf.Approximately(nightShadowAlphaFactor, PreviousDefaultNightShadowAlphaFactor))
            {
                nightShadowAlphaFactor = DefaultNightShadowAlphaFactor;
                changed = true;
            }

            if (changed)
            {
                meshDirty = true;
            }
        }

        private void LateUpdate()
        {
            RefreshShadows();
        }

        private void RefreshShadows()
        {
            EnsureMeshComponents();
            if (meshRenderer == null || baseRects.Count == 0 || !enableSunShadow || maxWallShadowLengthWorld <= 0.001f || shadowAlpha <= 0.001f)
            {
                SetMeshVisible(false);
                return;
            }

            float dayAlpha = ResolveDayAlpha();
            if (dayAlpha <= 0.001f)
            {
                SetMeshVisible(false);
                return;
            }

            Vector2 direction = ResolveSunDirection();
            float shadowLength = Mathf.Max(0f, maxWallShadowLengthWorld);
            if (NeedsMeshRebuild(direction, shadowLength))
            {
                RebuildShadowMesh(direction, shadowLength);
            }

            if (shadowMesh == null || shadowMesh.vertexCount == 0)
            {
                SetMeshVisible(false);
                return;
            }

            Color color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, Mathf.Clamp01(shadowAlpha * dayAlpha));
            ApplyMeshColor(color);
            meshRenderer.sharedMaterial = ResolveShadowMaterial();
            ApplyMaterialColor(color);
            meshRenderer.sortingLayerID = CampusRenderSortingUtility.ResolveSunShadowSortingLayerId(SortingLayer.NameToID("Default"));
            meshRenderer.sortingOrder = ResolveSortingOrder();
            SetMeshVisible(true);
        }

        private bool NeedsMeshRebuild(Vector2 direction, float shadowLength)
        {
            return meshDirty ||
                   shadowMesh == null ||
                   (direction - lastBuiltDirection).sqrMagnitude > DirectionRebuildTolerance ||
                   Mathf.Abs(shadowLength - lastBuiltLength) > MergeTolerance;
        }

        private void RebuildShadowMesh(Vector2 direction, float shadowLength)
        {
            shadowRects.Clear();
            yEdges.Clear();
            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            Vector2 offset = direction * (shadowLength * 0.5f);
            Vector2 stretch = new Vector2(Mathf.Abs(direction.x) * shadowLength, Mathf.Abs(direction.y) * shadowLength);
            for (int i = 0; i < baseRects.Count; i++)
            {
                ShadowRect baseRect = baseRects[i];
                ShadowRect shadowRect = new ShadowRect(
                    baseRect.MinX + offset.x - stretch.x * 0.5f,
                    baseRect.MinY + offset.y - stretch.y * 0.5f,
                    baseRect.MaxX + offset.x + stretch.x * 0.5f,
                    baseRect.MaxY + offset.y + stretch.y * 0.5f);

                if (!shadowRect.IsValid)
                {
                    continue;
                }

                shadowRects.Add(shadowRect);
                AddSortedEdge(yEdges, shadowRect.MinY);
                AddSortedEdge(yEdges, shadowRect.MaxY);
            }

            yEdges.Sort();
            RemoveNearDuplicateEdges(yEdges);
            BuildUnionQuadsFromShadowRects();
            ApplyMeshData();
            lastBuiltDirection = direction;
            lastBuiltLength = shadowLength;
            meshDirty = false;
        }

        private void BuildUnionQuadsFromShadowRects()
        {
            for (int edgeIndex = 0; edgeIndex < yEdges.Count - 1; edgeIndex++)
            {
                float yMin = yEdges[edgeIndex];
                float yMax = yEdges[edgeIndex + 1];
                if (yMax - yMin <= MergeTolerance)
                {
                    continue;
                }

                float yMid = (yMin + yMax) * 0.5f;
                intervals.Clear();
                for (int rectIndex = 0; rectIndex < shadowRects.Count; rectIndex++)
                {
                    ShadowRect rect = shadowRects[rectIndex];
                    if (rect.MinY - MergeTolerance <= yMid && rect.MaxY + MergeTolerance >= yMid)
                    {
                        intervals.Add(new Interval(rect.MinX, rect.MaxX));
                    }
                }

                if (intervals.Count == 0)
                {
                    continue;
                }

                intervals.Sort(CompareIntervals);
                float xMin = intervals[0].Min;
                float xMax = intervals[0].Max;
                for (int intervalIndex = 1; intervalIndex < intervals.Count; intervalIndex++)
                {
                    Interval interval = intervals[intervalIndex];
                    if (interval.Min <= xMax + MergeTolerance)
                    {
                        xMax = Mathf.Max(xMax, interval.Max);
                        continue;
                    }

                    AddQuad(xMin, yMin, xMax, yMax);
                    xMin = interval.Min;
                    xMax = interval.Max;
                }

                AddQuad(xMin, yMin, xMax, yMax);
            }
        }

        private void ApplyMeshData()
        {
            if (shadowMesh == null)
            {
                shadowMesh = new Mesh
                {
                    name = "NtingFiniteWallSunShadow_Mesh_Runtime",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            shadowMesh.Clear();
            if (vertices.Count == 0)
            {
                return;
            }

            shadowMesh.SetVertices(vertices);
            shadowMesh.SetTriangles(triangles, 0);
            shadowMesh.SetColors(colors);
            shadowMesh.RecalculateBounds();
            meshFilter.sharedMesh = shadowMesh;
        }

        private void AddWallCellRects(Tilemap wallLogic, Vector3Int cell, int connectionMask, Vector2 cellSize)
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

            bool hasHorizontalArm = eastArm || westArm;
            float centerBottomMinY = hasHorizontalArm ? HorizontalCenterYOffset - HorizontalSouthBottomHalfWidth : -WallBottomHalfWidth;
            float centerBottomMaxY = hasHorizontalArm ? HorizontalCenterYOffset + HorizontalNorthBottomHalfWidth : WallBottomHalfWidth;
            float horizontalBottomMinY = HorizontalCenterYOffset - HorizontalSouthBottomHalfWidth;
            float horizontalBottomMaxY = HorizontalCenterYOffset + HorizontalNorthBottomHalfWidth;

            AddWallBaseRect(wallLogic, cell, -WallBottomHalfWidth, centerBottomMinY, WallBottomHalfWidth, centerBottomMaxY, cellSize);

            if (eastArm)
            {
                AddWallBaseRect(wallLogic, cell, WallBottomHalfWidth, horizontalBottomMinY, WallCellHalf, horizontalBottomMaxY, cellSize);
            }

            if (westArm)
            {
                AddWallBaseRect(wallLogic, cell, -WallCellHalf, horizontalBottomMinY, -WallBottomHalfWidth, horizontalBottomMaxY, cellSize);
            }

            if (northArm)
            {
                AddWallBaseRect(wallLogic, cell, -WallBottomHalfWidth, centerBottomMaxY, WallBottomHalfWidth, WallCellHalf, cellSize);
            }

            if (southArm)
            {
                AddWallBaseRect(wallLogic, cell, -WallBottomHalfWidth, -WallCellHalf, WallBottomHalfWidth, centerBottomMinY, cellSize);
            }
        }

        private void AddWallBaseRect(Tilemap wallLogic, Vector3Int cell, float minX, float minY, float maxX, float maxY, Vector2 cellSize)
        {
            if (Mathf.Abs(maxX - minX) * cellSize.x <= MinSize || Mathf.Abs(maxY - minY) * cellSize.y <= MinSize)
            {
                return;
            }

            Vector3 cellCenter = wallLogic.GetCellCenterWorld(cell);
            Vector3 bottomLeft = cellCenter + wallLogic.transform.TransformVector(new Vector3(minX * cellSize.x, minY * cellSize.y, 0f));
            Vector3 topRight = cellCenter + wallLogic.transform.TransformVector(new Vector3(maxX * cellSize.x, maxY * cellSize.y, 0f));
            baseRects.Add(new ShadowRect(
                Mathf.Min(bottomLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, topRight.y)));
        }

        private void AddQuad(float xMin, float yMin, float xMax, float yMax)
        {
            if (xMax - xMin <= MergeTolerance || yMax - yMin <= MergeTolerance)
            {
                return;
            }

            int start = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(new Vector3(xMin, yMin, 0f)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(xMin, yMax, 0f)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(xMax, yMax, 0f)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(xMax, yMin, 0f)));
            colors.Add(Color.white);
            colors.Add(Color.white);
            colors.Add(Color.white);
            colors.Add(Color.white);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void ApplyMeshColor(Color color)
        {
            if (shadowMesh == null || shadowMesh.vertexCount == 0)
            {
                return;
            }

            Color32 vertexColor = color;
            colors.Clear();
            for (int i = 0; i < shadowMesh.vertexCount; i++)
            {
                colors.Add(vertexColor);
            }

            shadowMesh.SetColors(colors);
        }

        private void ApplyMaterialColor(Color color)
        {
            if (meshRenderer == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_BaseColor", color);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private Vector2 ResolveSunDirection()
        {
            if (useDayNightController)
            {
                if (dayNightController == null)
                {
                    dayNightController = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
                }

                if (dayNightController != null && dayNightController.SunDirection2D.sqrMagnitude > 0.0001f)
                {
                    sunDirection2D = dayNightController.SunDirection2D;
                }
            }

            if (sunDirection2D.sqrMagnitude <= 0.0001f)
            {
                sunDirection2D = Vector2.down;
            }

            return sunDirection2D.normalized;
        }

        private float ResolveDayAlpha()
        {
            if (!useDayNightController)
            {
                return 1f;
            }

            if (dayNightController == null)
            {
                dayNightController = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }

            if (dayNightController == null)
            {
                return 1f;
            }

            if (dayNightController.GameHour < 6f || dayNightController.GameHour > 18f)
            {
                return castNightShadow ? nightShadowAlphaFactor : 0f;
            }

            return Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(dayNightController.SunLowFactor));
        }

        private int ResolveSortingOrder()
        {
            CampusFloorRoot targetFloor = ResolveFloor();
            if (targetFloor != null && targetFloor.FloorTilemap != null)
            {
                Renderer floorRenderer = targetFloor.FloorTilemap.GetComponent<Renderer>();
                if (floorRenderer != null)
                {
                    return floorRenderer.sortingOrder + sortingOrderOffset;
                }
            }

            return sortingOrderOffset;
        }

        private CampusFloorRoot ResolveFloor()
        {
            if (floor == null)
            {
                floor = GetComponentInParent<CampusFloorRoot>();
            }

            return floor;
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
        }

        private void SetMeshVisible(bool visible)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
            }
        }

        private void ClearMesh()
        {
            if (shadowMesh != null)
            {
                shadowMesh.Clear();
            }

            SetMeshVisible(false);
            meshDirty = true;
        }

        private void ClearLegacyProxyChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null)
                {
                    DestroyGeneratedObject(child.gameObject);
                }
            }
        }

        private static Transform ResolveParent(CampusFloorRoot targetFloor)
        {
            if (targetFloor == null)
            {
                return null;
            }

            return targetFloor.Grid != null ? targetFloor.Grid.transform : targetFloor.transform;
        }

        private static NtingFiniteWallSunShadowRenderer FindRenderer(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == RendererName)
                {
                    NtingFiniteWallSunShadowRenderer renderer = child.GetComponent<NtingFiniteWallSunShadowRenderer>();
                    if (renderer != null)
                    {
                        return renderer;
                    }
                }
            }

            NtingFiniteWallSunShadowRenderer[] renderers = parent.GetComponentsInChildren<NtingFiniteWallSunShadowRenderer>(true);
            return renderers.Length > 0 ? renderers[0] : null;
        }

        private static void DestroyDuplicateRenderers(Transform parent, NtingFiniteWallSunShadowRenderer keep)
        {
            if (parent == null || keep == null)
            {
                return;
            }

            NtingFiniteWallSunShadowRenderer[] renderers = parent.GetComponentsInChildren<NtingFiniteWallSunShadowRenderer>(true);
            for (int i = renderers.Length - 1; i >= 0; i--)
            {
                NtingFiniteWallSunShadowRenderer renderer = renderers[i];
                if (renderer != null && renderer != keep)
                {
                    DestroyGeneratedObject(renderer.gameObject);
                }
            }
        }

        private static Vector2 GetGridCellSize(Grid grid)
        {
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            return new Vector2(Mathf.Max(MinSize, Mathf.Abs(cellSize.x)), Mathf.Max(MinSize, Mathf.Abs(cellSize.y)));
        }

        private static void AddSortedEdge(List<float> target, float edge)
        {
            target.Add(edge);
        }

        private static void RemoveNearDuplicateEdges(List<float> target)
        {
            if (target.Count <= 1)
            {
                return;
            }

            int write = 1;
            float last = target[0];
            for (int read = 1; read < target.Count; read++)
            {
                float value = target[read];
                if (Mathf.Abs(value - last) <= MergeTolerance)
                {
                    continue;
                }

                target[write] = value;
                last = value;
                write++;
            }

            if (write < target.Count)
            {
                target.RemoveRange(write, target.Count - write);
            }
        }

        private static int CompareIntervals(Interval a, Interval b)
        {
            int minCompare = a.Min.CompareTo(b.Min);
            return minCompare != 0 ? minCompare : a.Max.CompareTo(b.Max);
        }

        private static Material ResolveShadowMaterial()
        {
            if (sharedShadowMaterial != null)
            {
                return sharedShadowMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            sharedShadowMaterial = new Material(shader)
            {
                name = "NtingFiniteWallSunShadow_Runtime",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = Texture2D.whiteTexture
            };

            if (sharedShadowMaterial.HasProperty("_Color"))
            {
                sharedShadowMaterial.SetColor("_Color", Color.white);
            }

            if (sharedShadowMaterial.HasProperty("_BaseColor"))
            {
                sharedShadowMaterial.SetColor("_BaseColor", Color.white);
            }

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

        private readonly struct ShadowRect
        {
            public readonly float MinX;
            public readonly float MinY;
            public readonly float MaxX;
            public readonly float MaxY;

            public ShadowRect(float minX, float minY, float maxX, float maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public bool IsValid => MaxX - MinX > MinSize && MaxY - MinY > MinSize;
        }

        private readonly struct Interval
        {
            public readonly float Min;
            public readonly float Max;

            public Interval(float min, float max)
            {
                Min = min;
                Max = max;
            }
        }
    }
}
