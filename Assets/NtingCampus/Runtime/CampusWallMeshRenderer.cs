using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Builds real 3D wall model pieces from the wall logic tilemap.
    /// Each generated cell model is made from trapezoidal prisms.
    /// </summary>
    public static class CampusWallMeshRenderer
    {
        private const int ReservedLegacySurface = 0;
        private const int WallSouthSurface = 1;
        private const int WallEastSurface = 2;
        private const int WallNorthSurface = 3;
        private const int WallWestSurface = 4;
        private const int CapSurface = 5;
        private const int EdgeSurface = 6;
        private const int SurfaceCount = 7;

        private const float HalfCell = 0.5f;
        private const float TopHalfWidth = 0.205f;
        private const float BottomHalfWidth = 0.330f;
        private const float HorizontalCenterYOffset = 0.0975f;
        private const float HorizontalSouthBottomHalfWidth = 0.430f;
        private const float HorizontalNorthBottomHalfWidth = 0.235f;
        private const float TopDepth = -0.015f;
        private const float BaseDepth = 0.46f;
        private const float WallTextureDensity = 1.45f;
        private const string WallTwoSidedMeshLitShaderName = "Nting Campus/2D/Wall Mesh Lit Two Sided";
        private const string WallTwoSidedMeshLitResourcePath = "Shaders/CampusWallMesh2D-Lit-TwoSided";
        private const string WallLitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";
        private const string WallMeshLitShaderName = "Universal Render Pipeline/2D/Mesh2D-Lit-Default";

        private static readonly Color WallColor = new Color(0.70f, 0.60f, 0.52f, 1f);
        private static readonly Color CapColor = new Color(0.86f, 0.83f, 0.74f, 1f);
        private static readonly Color EdgeColor = new Color(0.045f, 0.040f, 0.035f, 1f);
        private static readonly Vector2 FallbackWallLightDirection = new Vector2(-0.52f, 0.85f).normalized;
        private const float FallbackWallLightWeight = 0.25f;
        private const float SunWallLightWeight = 12f;
        private const float PointWallLightWeight = 1f;

        public static bool RebuildFloor(CampusFloorRoot floor, CampusWallVisualCatalog catalog, CampusWallRenderProfile fallbackProfile)
        {
            if (floor == null)
            {
                return false;
            }

            Tilemap logic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            CampusWallRenderProfile defaultProfile = catalog != null
                ? catalog.GetProfileOrDefault(fallbackProfile)
                : fallbackProfile;
            if (logic == null || defaultProfile == null)
            {
                return false;
            }

            Transform meshRoot = EnsureMeshRoot(floor);
            ClearMeshRoot(meshRoot);

            logic.CompressBounds();
            Dictionary<CampusWallRenderProfile, WallMeshBatch> batches = new Dictionary<CampusWallRenderProfile, WallMeshBatch>();
            BoundsInt bounds = logic.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!logic.HasTile(cell))
                {
                    continue;
                }

                TileBase logicTile = logic.GetTile(cell);
                CampusWallRenderProfile profile = catalog != null
                    ? catalog.GetProfileForLogicTile(logicTile, defaultProfile)
                    : defaultProfile;
                if (profile == null)
                {
                    continue;
                }

                if (!batches.TryGetValue(profile, out WallMeshBatch batch))
                {
                    batch = new WallMeshBatch(profile, CreateMaterials(profile));
                    batches.Add(profile, batch);
                }

                int connectionMask = CampusWallTileUtility.GetConnectionMask(logic, cell);
                Vector3 localCenter = meshRoot.InverseTransformPoint(logic.GetCellCenterWorld(cell));
                AddCellPrisms(batch.Builder, connectionMask, localCenter);
                batch.CellCount++;
            }

            foreach (WallMeshBatch batch in batches.Values)
            {
                CreateBatchModel(meshRoot, floor, batch);
            }

            meshRoot.gameObject.SetActive(true);
            return true;
        }

        public static Transform EnsureMeshRoot(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return null;
            }

            Transform parent = floor.Grid != null ? floor.Grid.transform : floor.transform;
            if (floor.WallMeshRoot != null)
            {
                floor.WallMeshRoot.name = CampusObjectNames.WallMeshRoot;
                if (floor.WallMeshRoot.parent != parent && parent != null)
                {
                    floor.WallMeshRoot.SetParent(parent, false);
                }

                return floor.WallMeshRoot;
            }

            Transform existing = CampusObjectNames.FindDirectChild(parent, CampusObjectNames.WallMeshRoot, CampusObjectNames.LegacyWallMeshRoot);
            if (existing == null)
            {
                GameObject rootObject = new GameObject(CampusObjectNames.WallMeshRoot);
                rootObject.transform.SetParent(parent, false);
                existing = rootObject.transform;
            }

            existing.name = CampusObjectNames.WallMeshRoot;
            existing.localPosition = Vector3.zero;
            existing.localRotation = Quaternion.identity;
            existing.localScale = Vector3.one;
            floor.WallMeshRoot = existing;
            return existing;
        }

        public static void ClearFloor(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            Transform root = floor.WallMeshRoot != null ? floor.WallMeshRoot : EnsureMeshRoot(floor);
            ClearMeshRoot(root);
        }

        public static void SetVisible(CampusFloorRoot floor, bool visible)
        {
            Transform root = floor != null ? floor.WallMeshRoot : null;
            if (root != null)
            {
                root.gameObject.SetActive(visible);
            }
        }

        public static void ApplyTint(CampusFloorRoot floor, Color tint)
        {
            Transform root = floor != null ? floor.WallMeshRoot : null;
            if (root == null)
            {
                return;
            }

            CampusWallMeshVisual[] visuals = root.GetComponentsInChildren<CampusWallMeshVisual>(true);
            for (int i = 0; i < visuals.Length; i++)
            {
                CampusWallMeshVisual visual = visuals[i];
                if (visual == null)
                {
                    continue;
                }

                visual.ApplyTint(tint);
            }
        }

        private static void CreateBatchModel(Transform root, CampusFloorRoot floor, WallMeshBatch batch)
        {
            if (root == null || batch == null || !batch.Builder.HasGeometry)
            {
                return;
            }

            string profileName = batch.Profile != null ? batch.Profile.name : "Fallback";
            GameObject model = new GameObject("WallModel_Batch_" + profileName);
            model.transform.SetParent(root, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            MeshFilter filter = model.AddComponent<MeshFilter>();
            MeshRenderer renderer = model.AddComponent<MeshRenderer>();
            filter.sharedMesh = batch.Builder.BuildMesh(model.name);
            renderer.sharedMaterials = batch.Materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            CampusWallMeshVisual visual = model.AddComponent<CampusWallMeshVisual>();
            visual.Cell = Vector3Int.zero;
            visual.ConnectionMask = -1;
            visual.Profile = batch.Profile;
            visual.BaseMaterialColors = CaptureMaterialColors(batch.Materials);
            visual.CellCount = batch.CellCount;
            ApplyRendererSorting(floor, renderer);
            visual.ApplyDynamicLightingNow();
        }

        private static void AddCellPrisms(WallPrismBuilder builder, int connectionMask, Vector3 localCenter)
        {
            builder.SetOrigin(localCenter);
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
            float centerTopMinY = hasHorizontalArm ? HorizontalCenterYOffset - TopHalfWidth : -TopHalfWidth;
            float centerTopMaxY = hasHorizontalArm ? HorizontalCenterYOffset + TopHalfWidth : TopHalfWidth;
            float centerBottomMinY = hasHorizontalArm ? HorizontalCenterYOffset - HorizontalSouthBottomHalfWidth : -BottomHalfWidth;
            float centerBottomMaxY = hasHorizontalArm ? HorizontalCenterYOffset + HorizontalNorthBottomHalfWidth : BottomHalfWidth;
            float horizontalTopMinY = HorizontalCenterYOffset - TopHalfWidth;
            float horizontalTopMaxY = HorizontalCenterYOffset + TopHalfWidth;
            float horizontalBottomMinY = HorizontalCenterYOffset - HorizontalSouthBottomHalfWidth;
            float horizontalBottomMaxY = HorizontalCenterYOffset + HorizontalNorthBottomHalfWidth;

            builder.AddPrism(
                -TopHalfWidth,
                centerTopMinY,
                TopHalfWidth,
                centerTopMaxY,
                -BottomHalfWidth,
                centerBottomMinY,
                BottomHalfWidth,
                centerBottomMaxY,
                !northArm,
                !eastArm,
                !southArm,
                !westArm);

            if (eastArm)
            {
                builder.AddPrism(
                    TopHalfWidth,
                    horizontalTopMinY,
                    HalfCell,
                    horizontalTopMaxY,
                    BottomHalfWidth,
                    horizontalBottomMinY,
                    HalfCell,
                    horizontalBottomMaxY,
                    true,
                    !eastConnected,
                    true,
                    false);
            }

            if (westArm)
            {
                builder.AddPrism(
                    -HalfCell,
                    horizontalTopMinY,
                    -TopHalfWidth,
                    horizontalTopMaxY,
                    -HalfCell,
                    horizontalBottomMinY,
                    -BottomHalfWidth,
                    horizontalBottomMaxY,
                    true,
                    false,
                    true,
                    !westConnected);
            }

            if (northArm)
            {
                builder.AddPrism(
                    -TopHalfWidth,
                    centerTopMaxY,
                    TopHalfWidth,
                    HalfCell,
                    -BottomHalfWidth,
                    centerBottomMaxY,
                    BottomHalfWidth,
                    HalfCell,
                    !northConnected,
                    true,
                    false,
                    true);
            }

            if (southArm)
            {
                builder.AddPrism(
                    -TopHalfWidth,
                    -HalfCell,
                    TopHalfWidth,
                    centerTopMinY,
                    -BottomHalfWidth,
                    -HalfCell,
                    BottomHalfWidth,
                    centerBottomMinY,
                    false,
                    true,
                    !southConnected,
                    true);
            }

        }

        private static Material[] CreateMaterials(CampusWallRenderProfile profile)
        {
            Texture faceTexture = profile != null ? profile.FaceSourceTexture : null;
            Texture capTexture = profile != null && profile.CapSourceTexture != null ? profile.CapSourceTexture : faceTexture;
            return new[]
            {
                null,
                ResolveMaterial(profile != null ? profile.FaceMaterial : null, "WallModel_WallSouth", WallColor, faceTexture, true),
                ResolveMaterial(profile != null ? profile.FaceMaterial : null, "WallModel_WallEast", WallColor, faceTexture, true),
                ResolveMaterial(profile != null ? profile.FaceMaterial : null, "WallModel_WallNorth", WallColor, faceTexture, true),
                ResolveMaterial(profile != null ? profile.FaceMaterial : null, "WallModel_WallWest", WallColor, faceTexture, true),
                ResolveMaterial(profile != null ? profile.CapMaterial : null, "WallModel_Cap", CapColor, capTexture, true),
                ResolveMaterial(profile != null ? profile.EdgeMaterial : null, "WallModel_Edge", EdgeColor, null, true)
            };
        }

        internal static Color GetDynamicFaceColor(Vector2 faceNormal, Vector2 lightDirection)
        {
            return ScaleRgb(WallColor, ResolveFaceBrightness(faceNormal, lightDirection));
        }

        internal static float ResolveDynamicFaceBrightness(Vector2 faceNormal, Vector3 worldReference)
        {
            return CaptureWallLightingSnapshot().ResolveFaceBrightness(faceNormal, worldReference);
        }

        internal static Vector2 ResolveDynamicLightDirection(Vector3 worldReference)
        {
            return CaptureWallLightingSnapshot().ResolveDirection(worldReference);
        }

        internal static WallLightingSnapshot CaptureWallLightingSnapshot()
        {
            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Array.Sort(lights, CompareLightId);
            List<WallLightInfo> capturedLights = new List<WallLightInfo>(lights.Length);
            int signature = 17;
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light == null || !light.isActiveAndEnabled || light.lightType == Light2D.LightType.Global || light.intensity <= 0f)
                {
                    continue;
                }

                bool isSun = CampusObjectNames.MatchesAny(light.gameObject.name, CampusObjectNames.SunLight2D, CampusObjectNames.LegacySunLight2D);
                float radius = light.lightType == Light2D.LightType.Point ? Mathf.Max(0.001f, light.pointLightOuterRadius) : 1f;
                Vector3 position = light.transform.position;
                capturedLights.Add(new WallLightInfo(position, light.lightType, light.intensity, radius, isSun));

                unchecked
                {
                    signature = signature * 31 + light.GetInstanceID();
                    signature = signature * 31 + (int)light.lightType;
                    signature = signature * 31 + Quantize(position.x);
                    signature = signature * 31 + Quantize(position.y);
                    signature = signature * 31 + Quantize(position.z);
                    signature = signature * 31 + Quantize(light.intensity);
                    signature = signature * 31 + Quantize(radius);
                    signature = signature * 31 + (isSun ? 1 : 0);
                }
            }

            return new WallLightingSnapshot(capturedLights.ToArray(), signature);
        }

        internal static bool IsWallSurface(int surface)
        {
            return surface == WallSouthSurface || surface == WallEastSurface || surface == WallNorthSurface || surface == WallWestSurface;
        }

        private static int CompareLightId(Light2D a, Light2D b)
        {
            int left = a != null ? a.GetInstanceID() : 0;
            int right = b != null ? b.GetInstanceID() : 0;
            return left.CompareTo(right);
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        private static Vector2 ResolveDynamicLightDirection(WallLightInfo[] lights, Vector3 worldReference)
        {
            Vector2 accumulated = FallbackWallLightDirection * FallbackWallLightWeight;
            for (int i = 0; i < lights.Length; i++)
            {
                WallLightInfo light = lights[i];
                Vector2 toLight = (Vector2)(light.Position - worldReference);
                if (toLight.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                float distance = Mathf.Max(0.001f, toLight.magnitude);
                float rangeWeight = light.LightType == Light2D.LightType.Point ? Mathf.Clamp01(1f - distance / light.Radius) : 1f;
                if (!light.IsSun && light.LightType == Light2D.LightType.Point && rangeWeight <= 0f)
                {
                    continue;
                }

                float lightWeight = light.IsSun
                    ? SunWallLightWeight
                    : Mathf.Lerp(0.05f, PointWallLightWeight, rangeWeight);
                float weight = Mathf.Max(0f, light.Intensity) * lightWeight;
                if (weight <= 0f)
                {
                    continue;
                }

                accumulated += toLight.normalized * weight;
            }

            if (accumulated.sqrMagnitude <= 0.0001f)
            {
                return FallbackWallLightDirection;
            }

            return accumulated.normalized;
        }

        private static float ResolveFaceBrightness(Vector2 faceNormal, Vector2 lightDirection)
        {
            if (faceNormal.sqrMagnitude <= 0.0001f)
            {
                return 1f;
            }

            Vector2 direction = lightDirection.sqrMagnitude > 0.0001f ? lightDirection.normalized : FallbackWallLightDirection;
            float facing = Vector2.Dot(faceNormal.normalized, direction);
            float brightness = 0.78f + Mathf.Max(0f, facing) * 0.12f + Mathf.Min(0f, facing) * 0.10f;
            return Mathf.Clamp(brightness, 0.64f, 0.92f);
        }

        private static Vector2 ResolveSurfaceNormal(int surface)
        {
            switch (surface)
            {
                case WallSouthSurface:
                    return new Vector2(0f, -1f);
                case WallEastSurface:
                    return new Vector2(1f, 0f);
                case WallNorthSurface:
                    return new Vector2(0f, 1f);
                case WallWestSurface:
                    return new Vector2(-1f, 0f);
                default:
                    return Vector2.zero;
            }
        }

        internal sealed class WallLightingSnapshot
        {
            private readonly WallLightInfo[] lights;

            public WallLightingSnapshot(WallLightInfo[] lights, int signature)
            {
                this.lights = lights ?? new WallLightInfo[0];
                Signature = signature;
            }

            public int Signature { get; }

            public Vector2 ResolveDirection(Vector3 worldReference)
            {
                return ResolveDynamicLightDirection(lights, worldReference);
            }

            public float ResolveFaceBrightness(Vector2 faceNormal, Vector3 worldReference)
            {
                return CampusWallMeshRenderer.ResolveFaceBrightness(faceNormal, ResolveDirection(worldReference));
            }
        }

        internal readonly struct WallLightInfo
        {
            public WallLightInfo(Vector3 position, Light2D.LightType lightType, float intensity, float radius, bool isSun)
            {
                Position = position;
                LightType = lightType;
                Intensity = intensity;
                Radius = Mathf.Max(0.001f, radius);
                IsSun = isSun;
            }

            public readonly Vector3 Position;
            public readonly Light2D.LightType LightType;
            public readonly float Intensity;
            public readonly float Radius;
            public readonly bool IsSun;
        }

        internal static bool EnsureWallModelMaterials(MeshRenderer renderer, CampusWallRenderProfile profile)
        {
            if (renderer == null)
            {
                return false;
            }

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            if (materials == null || materials.Length < SurfaceCount)
            {
                System.Array.Resize(ref materials, SurfaceCount);
                changed = true;
            }

            Texture faceTexture = profile != null ? profile.FaceSourceTexture : null;
            Texture capTexture = profile != null && profile.CapSourceTexture != null ? profile.CapSourceTexture : faceTexture;
            Shader targetShader = ResolveWallModelShader(false);
            for (int materialIndex = WallSouthSurface; materialIndex < SurfaceCount; materialIndex++)
            {
                Material material = materials[materialIndex];
                Texture targetTexture = ResolveSurfaceTexture(materialIndex, material, faceTexture, capTexture);
                Color targetColor = ResolveSurfaceBaseColor(materialIndex);
                string targetName = ResolveSurfaceMaterialName(materialIndex);

                if (material == null)
                {
                    materials[materialIndex] = CreateMaterial(targetName, targetColor, targetTexture);
                    changed = true;
                    continue;
                }

                if (!NeedsWallMaterialRefresh(material, targetShader, targetTexture))
                {
                    continue;
                }

                if (material.hideFlags == HideFlags.None || !material.name.StartsWith("WallModel_"))
                {
                    material = new Material(material)
                    {
                        name = targetName,
                        hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
                    };
                    materials[materialIndex] = material;
                }

                ConfigureMaterial(material, targetColor, targetTexture, false);
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }

            return changed;
        }

        private static Texture ResolveSurfaceTexture(int materialIndex, Material material, Texture faceTexture, Texture capTexture)
        {
            if (materialIndex == CapSurface)
            {
                return capTexture != null ? capTexture : GetMaterialMainTexture(material);
            }

            if (materialIndex == EdgeSurface)
            {
                return null;
            }

            return faceTexture != null ? faceTexture : GetMaterialMainTexture(material);
        }

        private static Texture GetMaterialMainTexture(Material material)
        {
            if (material == null || !material.HasProperty("_MainTex"))
            {
                return null;
            }

            Texture texture = material.mainTexture;
            return texture == Texture2D.whiteTexture ? null : texture;
        }

        private static Color ResolveSurfaceBaseColor(int materialIndex)
        {
            switch (materialIndex)
            {
                case CapSurface:
                    return CapColor;
                case EdgeSurface:
                    return EdgeColor;
                default:
                    return WallColor;
            }
        }

        private static string ResolveSurfaceMaterialName(int materialIndex)
        {
            switch (materialIndex)
            {
                case WallSouthSurface:
                    return "WallModel_WallSouth";
                case WallEastSurface:
                    return "WallModel_WallEast";
                case WallNorthSurface:
                    return "WallModel_WallNorth";
                case WallWestSurface:
                    return "WallModel_WallWest";
                case CapSurface:
                    return "WallModel_Cap";
                case EdgeSurface:
                    return "WallModel_Edge";
                default:
                    return "WallModel_Surface";
            }
        }

        private static bool NeedsWallMaterialRefresh(Material material, Shader targetShader, Texture targetTexture)
        {
            if (material == null)
            {
                return true;
            }

            if (targetShader != null && material.shader != targetShader)
            {
                return true;
            }

            Texture currentTexture = GetMaterialMainTexture(material);
            if (targetTexture != null && currentTexture != targetTexture)
            {
                return true;
            }

            return targetTexture == null && currentTexture != null;
        }

        private static Color ScaleRgb(Color color, float multiplier)
        {
            return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
        }

        private static Material ResolveMaterial(Material assignedMaterial, string generatedName, Color color, Texture texture, bool cloneAssigned = false)
        {
            bool shadow = generatedName.Contains("Shadow");
            if (assignedMaterial != null)
            {
                Texture assignedTexture = texture != null ? texture : assignedMaterial.mainTexture;
                if (cloneAssigned)
                {
                    Material material = new Material(assignedMaterial)
                    {
                        name = generatedName,
                        hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
                    };
                    ConfigureMaterial(material, color, assignedTexture, shadow);
                    return material;
                }

                ConfigureMaterial(assignedMaterial, color, assignedTexture, shadow);
                return assignedMaterial;
            }

            return CreateMaterial(generatedName, color, texture);
        }

        private static Material CreateMaterial(string name, Color color, Texture texture)
        {
            bool shadow = name.Contains("Shadow");
            Shader shader = ResolveWallModelShader(shadow);
            color.a = shadow ? color.a : 1f;
            Material material = new Material(shader)
            {
                name = name,
                color = color,
                mainTexture = texture != null ? texture : Texture2D.whiteTexture,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                renderQueue = shadow ? 3000 : -1
            };
            ConfigureMaterial(material, color, texture, shadow);
            return material;
        }

        private static Shader ResolveWallModelShader(bool shadow)
        {
            Shader shader = null;
            if (!shadow)
            {
                shader = Shader.Find(WallTwoSidedMeshLitShaderName);
                if (shader == null)
                {
                    shader = Resources.Load<Shader>(WallTwoSidedMeshLitResourcePath);
                }
                if (shader == null)
                {
                    shader = Shader.Find(WallMeshLitShaderName);
                }
                if (shader == null)
                {
                    shader = Shader.Find(WallLitShaderName);
                }
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return shader;
        }

        private static void ConfigureMaterial(Material material, Color color, Texture texture, bool shadow)
        {
            if (material == null)
            {
                return;
            }

            if (ShouldUseGeneratedWallShader(material.shader, shadow))
            {
                material.shader = ResolveWallModelShader(shadow);
            }

            color.a = shadow ? color.a : 1f;
            material.renderQueue = shadow ? 3000 : -1;
            ApplyWallMaterialColor(material, color);

            ConfigureTextureProperty(material, "_BaseMap", texture);
            ConfigureTextureProperty(material, "_MainTex", texture);
            ConfigureTextureProperty(material, "_MaskTex", Texture2D.whiteTexture);

            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", 0);
            }

            ConfigureBlendState(material, shadow);
        }

        private static bool ShouldUseGeneratedWallShader(Shader shader, bool shadow)
        {
            if (shadow)
            {
                return false;
            }

            if (shader == null)
            {
                return true;
            }

            string shaderName = shader.name;
            return shaderName == "Sprites/Default" ||
                   shaderName == WallTwoSidedMeshLitShaderName ||
                   shaderName == WallLitShaderName ||
                   shaderName == WallMeshLitShaderName ||
                   shaderName == "Universal Render Pipeline/Unlit" ||
                   shaderName == "Unlit/Texture" ||
                   shaderName == "Universal Render Pipeline/Lit" ||
                   shaderName == "Universal Render Pipeline/Simple Lit";
        }

        private static void ConfigureTextureProperty(Material material, string propertyName, Texture texture)
        {
            if (material == null || !material.HasProperty(propertyName))
            {
                return;
            }

            material.SetTexture(propertyName, texture != null ? texture : Texture2D.whiteTexture);
            material.SetTextureScale(propertyName, Vector2.one);
        }

        private static void ConfigureBlendState(Material material, bool shadow)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", shadow ? 1f : 0f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", shadow ? (int)UnityEngine.Rendering.BlendMode.SrcAlpha : (int)UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", shadow ? (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (int)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", shadow ? 0 : 1);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            if (!shadow)
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
            }
        }

        internal static void ApplyWallMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            material.color = color;
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_White"))
            {
                material.SetColor("_White", color);
            }
        }

        private static Color[] CaptureMaterialColors(Material[] materials)
        {
            Color[] colors = new Color[materials != null ? materials.Length : 0];
            for (int i = 0; i < colors.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    colors[i] = Color.white;
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    colors[i] = material.GetColor("_BaseColor");
                }
                else if (material.HasProperty("_White"))
                {
                    colors[i] = material.GetColor("_White");
                }
                else if (material.HasProperty("_Color"))
                {
                    colors[i] = material.GetColor("_Color");
                }
                else
                {
                    colors[i] = material.color;
                }
            }

            return colors;
        }

        internal static Color GetBaseMaterialColor(int materialIndex, Color[] capturedColors)
        {
            if (materialIndex == WallSouthSurface || materialIndex == WallEastSurface || materialIndex == WallNorthSurface || materialIndex == WallWestSurface)
            {
                return WallColor;
            }

            if (materialIndex == CapSurface)
            {
                return CapColor;
            }

            if (materialIndex == EdgeSurface)
            {
                return EdgeColor;
            }

            if (capturedColors != null && materialIndex >= 0 && materialIndex < capturedColors.Length)
            {
                return capturedColors[materialIndex];
            }

            return Color.white;
        }

        private static void ApplyRendererSorting(CampusFloorRoot floor, MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Renderer reference = floor != null && floor.WallFaceTilemap != null
                ? floor.WallFaceTilemap.GetComponent<Renderer>()
                : null;
            if (reference == null && floor != null && floor.FloorTilemap != null)
            {
                reference = floor.FloorTilemap.GetComponent<Renderer>();
            }

            if (reference != null)
            {
                renderer.sortingLayerID = reference.sortingLayerID;
                renderer.sortingOrder = reference.sortingOrder;
            }
        }

        private static void ClearMeshRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            HashSet<Material> generatedMaterials = new HashSet<Material>();
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        if (material != null && (material.hideFlags & HideFlags.DontSave) != 0)
                        {
                            generatedMaterials.Add(material);
                        }
                    }
                }

                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    DestroyObject(filter.sharedMesh);
                    filter.sharedMesh = null;
                }

                DestroyObject(child.gameObject);
            }

            foreach (Material material in generatedMaterials)
            {
                DestroyObject(material);
            }
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private sealed class WallMeshBatch
        {
            public WallMeshBatch(CampusWallRenderProfile profile, Material[] materials)
            {
                Profile = profile;
                Materials = materials;
            }

            public readonly CampusWallRenderProfile Profile;
            public readonly Material[] Materials;
            public readonly WallPrismBuilder Builder = new WallPrismBuilder();
            public int CellCount;
        }

        private sealed class WallPrismBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<Vector4> lightingData = new List<Vector4>();
            private readonly List<Color> vertexColors = new List<Color>();
            private readonly List<int>[] triangles = CreateTriangleLists();
            private Vector3 origin;

            public bool HasGeometry => vertices.Count > 0;

            public void SetOrigin(Vector3 localCenter)
            {
                origin = localCenter;
            }

            private static List<int>[] CreateTriangleLists()
            {
                List<int>[] lists = new List<int>[SurfaceCount];
                for (int i = 0; i < lists.Length; i++)
                {
                    lists[i] = new List<int>();
                }

                return lists;
            }

            public void AddPrism(
                float topMinX,
                float topMinY,
                float topMaxX,
                float topMaxY,
                float bottomMinX,
                float bottomMinY,
                float bottomMaxX,
                float bottomMaxY,
                bool northFace,
                bool eastFace,
                bool southFace,
                bool westFace)
            {
                Vector3 bSW = origin + new Vector3(bottomMinX, bottomMinY, BaseDepth);
                Vector3 bSE = origin + new Vector3(bottomMaxX, bottomMinY, BaseDepth);
                Vector3 bNE = origin + new Vector3(bottomMaxX, bottomMaxY, BaseDepth);
                Vector3 bNW = origin + new Vector3(bottomMinX, bottomMaxY, BaseDepth);
                Vector3 tSW = origin + new Vector3(topMinX, topMinY, TopDepth);
                Vector3 tSE = origin + new Vector3(topMaxX, topMinY, TopDepth);
                Vector3 tNE = origin + new Vector3(topMaxX, topMaxY, TopDepth);
                Vector3 tNW = origin + new Vector3(topMinX, topMaxY, TopDepth);

                AddQuad(tSW, tSE, tNE, tNW, CapSurface);

                if (southFace)
                {
                    AddQuad(bSE, bSW, tSW, tSE, WallSouthSurface);
                }

                if (northFace)
                {
                    AddQuad(bNW, bNE, tNE, tNW, WallNorthSurface);
                }

                if (eastFace)
                {
                    AddQuad(bNE, bSE, tSE, tNE, WallEastSurface);
                }

                if (westFace)
                {
                    AddQuad(bSW, bNW, tNW, tSW, WallWestSurface);
                }
            }

            public Mesh BuildMesh(string name)
            {
                Mesh mesh = new Mesh
                {
                    name = name
                };
                if (vertices.Count > 65000)
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(1, lightingData);
                mesh.SetColors(vertexColors);
                mesh.subMeshCount = SurfaceCount;
                for (int i = 0; i < SurfaceCount; i++)
                {
                    mesh.SetTriangles(triangles[i], i);
                }

                mesh.MarkDynamic();
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                return mesh;
            }

            private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int surface)
            {
                int index = vertices.Count;
                Vector3 origin = a;
                Vector3 uAxis = b - a;
                if (uAxis.sqrMagnitude <= 0.000001f)
                {
                    uAxis = c - d;
                }

                uAxis = uAxis.sqrMagnitude > 0.000001f ? uAxis.normalized : Vector3.right;
                Vector3 rawV = d - a;
                rawV -= Vector3.Dot(rawV, uAxis) * uAxis;
                if (rawV.sqrMagnitude <= 0.000001f)
                {
                    rawV = c - b;
                    rawV -= Vector3.Dot(rawV, uAxis) * uAxis;
                }

                Vector3 vAxis = rawV.sqrMagnitude > 0.000001f ? rawV.normalized : Vector3.up;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);

                uvs.Add(ToSurfaceUv(a, origin, uAxis, vAxis));
                uvs.Add(ToSurfaceUv(b, origin, uAxis, vAxis));
                uvs.Add(ToSurfaceUv(c, origin, uAxis, vAxis));
                uvs.Add(ToSurfaceUv(d, origin, uAxis, vAxis));

                Vector2 faceNormal = ResolveSurfaceNormal(surface);
                Vector4 lightData = new Vector4(faceNormal.x, faceNormal.y, surface, 0f);
                lightingData.Add(lightData);
                lightingData.Add(lightData);
                lightingData.Add(lightData);
                lightingData.Add(lightData);

                vertexColors.Add(Color.white);
                vertexColors.Add(Color.white);
                vertexColors.Add(Color.white);
                vertexColors.Add(Color.white);

                triangles[surface].Add(index);
                triangles[surface].Add(index + 1);
                triangles[surface].Add(index + 2);
                triangles[surface].Add(index);
                triangles[surface].Add(index + 2);
                triangles[surface].Add(index + 3);
            }

            private static Vector2 ToSurfaceUv(Vector3 point, Vector3 origin, Vector3 uAxis, Vector3 vAxis)
            {
                Vector3 relative = point - origin;
                return new Vector2(
                    Vector3.Dot(relative, uAxis) * WallTextureDensity,
                    Vector3.Dot(relative, vAxis) * WallTextureDensity);
            }
        }
    }

    [ExecuteAlways]
    public sealed class CampusWallMeshVisual : MonoBehaviour
    {
        public Vector3Int Cell;
        public int ConnectionMask;
        public CampusWallRenderProfile Profile;
        public Color[] BaseMaterialColors;
        public int CellCount;
        public Color Tint = Color.white;

        private MeshRenderer cachedRenderer;
        private MeshFilter cachedFilter;
        private readonly List<Vector3> meshVertices = new List<Vector3>();
        private readonly List<Vector4> meshLightingData = new List<Vector4>();
        private readonly List<Color> meshColors = new List<Color>();
        private int lastLightingSignature = int.MinValue;
        private Color lastTint = new Color(float.NaN, float.NaN, float.NaN, float.NaN);

        private void OnEnable()
        {
            ApplyDynamicLightingNow(true);
        }

        private void OnValidate()
        {
            ApplyDynamicLightingNow(true);
        }

        private void LateUpdate()
        {
            ApplyDynamicLightingNow();
        }

        public void ApplyTint(Color tint)
        {
            Tint = tint;
            ApplyDynamicLightingNow(true);
        }

        public void ApplyDynamicLightingNow()
        {
            ApplyDynamicLightingNow(false);
        }

        private void ApplyDynamicLightingNow(bool force)
        {
            MeshRenderer renderer = ResolveRenderer();
            MeshFilter filter = ResolveFilter();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (renderer == null || mesh == null)
            {
                return;
            }

            bool materialChanged = CampusWallMeshRenderer.EnsureWallModelMaterials(renderer, Profile);
            CampusWallMeshRenderer.WallLightingSnapshot lighting = CampusWallMeshRenderer.CaptureWallLightingSnapshot();
            if (!force && !materialChanged && lighting.Signature == lastLightingSignature && Approximately(Tint, lastTint))
            {
                return;
            }

            lastLightingSignature = lighting.Signature;
            lastTint = Tint;
            ApplyBaseMaterialColors(renderer);
            ApplyVertexLighting(mesh, lighting);
        }

        private void ApplyBaseMaterialColors(MeshRenderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (materials[materialIndex] == null)
                {
                    continue;
                }

                Color color = CampusWallMeshRenderer.GetBaseMaterialColor(materialIndex, BaseMaterialColors);
                color = new Color(color.r * Tint.r, color.g * Tint.g, color.b * Tint.b, color.a * Tint.a);
                renderer.SetPropertyBlock(null, materialIndex);
                CampusWallMeshRenderer.ApplyWallMaterialColor(materials[materialIndex], color);
            }
        }

        private void ApplyVertexLighting(Mesh mesh, CampusWallMeshRenderer.WallLightingSnapshot lighting)
        {
            mesh.GetVertices(meshVertices);
            mesh.GetUVs(1, meshLightingData);
            if (meshVertices.Count == 0)
            {
                return;
            }

            if (meshLightingData.Count != meshVertices.Count)
            {
                SetUniformVertexColor(mesh, Color.white, meshVertices.Count);
                return;
            }

            Transform meshTransform = transform;
            meshColors.Clear();
            for (int vertexIndex = 0; vertexIndex < meshVertices.Count; vertexIndex++)
            {
                Vector4 data = meshLightingData[vertexIndex];
                int surface = Mathf.RoundToInt(data.z);
                if (CampusWallMeshRenderer.IsWallSurface(surface))
                {
                    Vector2 faceNormal = new Vector2(data.x, data.y);
                    Vector3 worldPosition = meshTransform.TransformPoint(meshVertices[vertexIndex]);
                    float brightness = lighting.ResolveFaceBrightness(faceNormal, worldPosition);
                    meshColors.Add(new Color(brightness, brightness, brightness, 1f));
                }
                else
                {
                    meshColors.Add(Color.white);
                }
            }

            mesh.SetColors(meshColors);
        }

        private void SetUniformVertexColor(Mesh mesh, Color color, int count)
        {
            meshColors.Clear();
            for (int i = 0; i < count; i++)
            {
                meshColors.Add(color);
            }

            mesh.SetColors(meshColors);
        }

        private MeshRenderer ResolveRenderer()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<MeshRenderer>();
            }

            return cachedRenderer;
        }

        private MeshFilter ResolveFilter()
        {
            if (cachedFilter == null)
            {
                cachedFilter = GetComponent<MeshFilter>();
            }

            return cachedFilter;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f &&
                   Mathf.Abs(a.g - b.g) < 0.001f &&
                   Mathf.Abs(a.b - b.b) < 0.001f &&
                   Mathf.Abs(a.a - b.a) < 0.001f;
        }
    }
}
