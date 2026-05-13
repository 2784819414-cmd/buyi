using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NtingWallPointShadowRenderer : MonoBehaviour
    {
        public const string RendererName = "Nting Point Light Wall Shadows";

        private const float WallCellHalf = 0.5f;
        private const float WallBottomHalfWidth = 0.330f;
        private const float MinSize = 0.01f;
        private const float MergeTolerance = 0.0005f;
        private const string ShadowShaderName = "Nting Campus/2D/Projected Shadow Unlit";
        private const string ShadowShaderResourcePath = "Shaders/CampusProjectedShadowUnlit";

        [SerializeField] private CampusFloorRoot floor;

        private readonly List<ShadowRect> baseRects = new List<ShadowRect>(256);
        private readonly List<ShadowRect> shadowRects = new List<ShadowRect>(256);
        private readonly List<float> yEdges = new List<float>(512);
        private readonly List<Interval> intervals = new List<Interval>(128);
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<int> triangles = new List<int>(1536);
        private readonly List<Color> colors = new List<Color>(1024);

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh shadowMesh;
        private static Material sharedShadowMaterial;

        public static NtingWallPointShadowRenderer EnsureForFloor(CampusFloorRoot targetFloor)
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
            renderer.RebuildBaseRects();
            return renderer;
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
                return;
            }

            if (baseRects.Count == 0)
            {
                RebuildBaseRects();
            }

            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            int count = Mathf.Min(pointLightCount, pointLights.Length, Mathf.Max(0, maxPointLights));
            for (int lightIndex = 0; lightIndex < count; lightIndex++)
            {
                NtingCustomShadowSystem.ShadowLightInfo light = pointLights[lightIndex];
                float lightIntensity = Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(light.Intensity * Mathf.Max(0.001f, intensityScale)));
                float lightAlpha = Mathf.Clamp01(shadowAlpha * light.ShadowAlphaWeight * lightIntensity);
                if (lightAlpha <= 0.001f)
                {
                    continue;
                }

                BuildLightShadowRects(light, pointLights, count, maxShadowLengthWorld, pointLightFillStrength, pointLightFillIntensityScale);
                if (shadowRects.Count == 0)
                {
                    continue;
                }

                Color lightShadowColor = NtingCustomShadowSystem.ResolvePointLightShadowColor(shadowColor, light);
                Color color = new Color(lightShadowColor.r, lightShadowColor.g, lightShadowColor.b, lightAlpha);
                BuildUnionQuads(color);
            }

            ApplyMeshData();
            if (shadowMesh == null || shadowMesh.vertexCount == 0)
            {
                SetVisible(false);
                return;
            }

            meshRenderer.sharedMaterial = ResolveShadowMaterial();
            meshRenderer.sortingLayerID = sortingLayerId;
            meshRenderer.sortingOrder = sortingOrder;
            SetVisible(true);
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
        }

        private void RebuildBaseRects()
        {
            baseRects.Clear();
            CampusFloorRoot targetFloor = ResolveFloor();
            Tilemap wallLogic = targetFloor != null ? CampusWallTileUtility.GetWallLogicTilemap(targetFloor) : null;
            if (wallLogic == null)
            {
                ClearMesh();
                return;
            }

            wallLogic.CompressBounds();
            Vector2 cellSize = targetFloor != null && targetFloor.Grid != null
                ? new Vector2(Mathf.Abs(targetFloor.Grid.cellSize.x), Mathf.Abs(targetFloor.Grid.cellSize.y))
                : Vector2.one;
            BoundsInt bounds = wallLogic.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!wallLogic.HasTile(cell))
                {
                    continue;
                }

                AddWallCellRects(wallLogic, cell, CampusWallTileUtility.GetConnectionMask(wallLogic, cell), cellSize);
            }
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

            AddWallBaseRect(wallLogic, cell, -WallBottomHalfWidth, -WallBottomHalfWidth, WallBottomHalfWidth, WallBottomHalfWidth, cellSize);

            if (eastArm)
            {
                AddWallBaseRect(wallLogic, cell, WallBottomHalfWidth, -WallBottomHalfWidth, WallCellHalf, WallBottomHalfWidth, cellSize);
            }

            if (westArm)
            {
                AddWallBaseRect(wallLogic, cell, -WallCellHalf, -WallBottomHalfWidth, -WallBottomHalfWidth, WallBottomHalfWidth, cellSize);
            }

            if (northArm)
            {
                AddWallBaseRect(wallLogic, cell, -WallBottomHalfWidth, WallBottomHalfWidth, WallBottomHalfWidth, WallCellHalf, cellSize);
            }

            if (southArm)
            {
                AddWallBaseRect(wallLogic, cell, -WallBottomHalfWidth, -WallCellHalf, WallBottomHalfWidth, -WallBottomHalfWidth, cellSize);
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

        private void BuildLightShadowRects(
            NtingCustomShadowSystem.ShadowLightInfo light,
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int pointLightCount,
            float maxShadowLengthWorld,
            float pointLightFillStrength,
            float pointLightFillIntensityScale)
        {
            shadowRects.Clear();
            yEdges.Clear();
            Vector2 lightPosition = light.Position;
            for (int i = 0; i < baseRects.Count; i++)
            {
                ShadowRect baseRect = baseRects[i];
                Vector2 center = baseRect.Center;
                Vector2 direction = center - lightPosition;
                float distance = direction.magnitude;
                if (distance <= 0.001f || distance >= light.Radius)
                {
                    continue;
                }

                float distance01 = Mathf.Clamp01(distance / light.Radius);
                Vector2 normalized = direction / distance;
                float shadowLength = Mathf.Max(0f, maxShadowLengthWorld * light.ShadowLengthWeight * Mathf.Lerp(0.15f, 1f, distance01) * Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(light.Intensity)));
                if (shadowLength <= MinSize)
                {
                    continue;
                }

                Vector2 offset = normalized * (shadowLength * 0.5f);
                Vector2 stretch = new Vector2(Mathf.Abs(normalized.x) * shadowLength, Mathf.Abs(normalized.y) * shadowLength);
                ShadowRect shadowRect = new ShadowRect(
                    baseRect.MinX + offset.x - stretch.x * 0.5f,
                    baseRect.MinY + offset.y - stretch.y * 0.5f,
                    baseRect.MaxX + offset.x + stretch.x * 0.5f,
                    baseRect.MaxY + offset.y + stretch.y * 0.5f);

                if (!shadowRect.IsValid)
                {
                    continue;
                }

                float rangeFactor = 1f - distance01;
                float visibleRangeFactor = Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(rangeFactor));
                float fillMultiplier = NtingCustomShadowSystem.ResolvePointLightFillMultiplier(
                    pointLights,
                    pointLightCount,
                    light.InstanceId,
                    shadowRect.Center,
                    pointLightFillStrength,
                    pointLightFillIntensityScale);
                float alphaMultiplier = Mathf.Clamp01(visibleRangeFactor * fillMultiplier);
                if (alphaMultiplier <= 0.001f)
                {
                    continue;
                }

                shadowRects.Add(shadowRect.WithAlpha(alphaMultiplier));
                yEdges.Add(shadowRect.MinY);
                yEdges.Add(shadowRect.MaxY);
            }

            yEdges.Sort();
            RemoveNearDuplicateEdges(yEdges);
        }

        private void BuildUnionQuads(Color color)
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
                        intervals.Add(new Interval(rect.MinX, rect.MaxX, rect.Alpha));
                    }
                }

                if (intervals.Count == 0)
                {
                    continue;
                }

                intervals.Sort(CompareIntervals);
                float xMin = intervals[0].Min;
                float xMax = intervals[0].Max;
                float alphaMultiplier = intervals[0].Alpha;
                for (int intervalIndex = 1; intervalIndex < intervals.Count; intervalIndex++)
                {
                    Interval interval = intervals[intervalIndex];
                    if (interval.Min <= xMax + MergeTolerance)
                    {
                        xMax = Mathf.Max(xMax, interval.Max);
                        alphaMultiplier = Mathf.Max(alphaMultiplier, interval.Alpha);
                        continue;
                    }

                    AddQuad(xMin, yMin, xMax, yMax, color, alphaMultiplier);
                    xMin = interval.Min;
                    xMax = interval.Max;
                    alphaMultiplier = interval.Alpha;
                }

                AddQuad(xMin, yMin, xMax, yMax, color, alphaMultiplier);
            }
        }

        private void AddQuad(float xMin, float yMin, float xMax, float yMax, Color color, float alphaMultiplier)
        {
            if (xMax - xMin <= MergeTolerance || yMax - yMin <= MergeTolerance)
            {
                return;
            }

            Color quadColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * alphaMultiplier));
            int start = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(new Vector3(xMin, yMin, 0f)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(xMin, yMax, 0f)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(xMax, yMax, 0f)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(xMax, yMin, 0f)));
            colors.Add(quadColor);
            colors.Add(quadColor);
            colors.Add(quadColor);
            colors.Add(quadColor);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void ApplyMeshData()
        {
            if (shadowMesh == null)
            {
                shadowMesh = new Mesh
                {
                    name = "NtingWallPointShadow_Mesh_Runtime",
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

        private void ClearMesh()
        {
            if (shadowMesh != null)
            {
                shadowMesh.Clear();
            }

            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
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

        private readonly struct ShadowRect
        {
            public readonly float MinX;
            public readonly float MinY;
            public readonly float MaxX;
            public readonly float MaxY;
            public readonly float Alpha;

            public ShadowRect(float minX, float minY, float maxX, float maxY, float alpha = 1f)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
                Alpha = Mathf.Clamp01(alpha);
            }

            public Vector2 Center => new Vector2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

            public bool IsValid => MaxX - MinX > MinSize && MaxY - MinY > MinSize;

            public ShadowRect WithAlpha(float alpha)
            {
                return new ShadowRect(MinX, MinY, MaxX, MaxY, alpha);
            }
        }

        private readonly struct Interval
        {
            public readonly float Min;
            public readonly float Max;
            public readonly float Alpha;

            public Interval(float min, float max, float alpha)
            {
                Min = min;
                Max = max;
                Alpha = Mathf.Clamp01(alpha);
            }
        }
    }
}
