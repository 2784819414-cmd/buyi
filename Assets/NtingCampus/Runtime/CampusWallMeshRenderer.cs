using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Builds real 3D wall model pieces from the wall logic tilemap.
    /// Each generated cell model is made from isosceles trapezoidal prisms.
    /// </summary>
    public static class CampusWallMeshRenderer
    {
        public const float WallHalfCell = 0.5f;
        public const float WallTopHalfWidth = 0.205f;
        public const float WallBottomHalfWidth = 0.330f;
        public const float WallTopDepth = -0.015f;
        public const float WallBaseDepth = 0.46f;
        public const float HorizontalWallTopYOffset = 0.070f;

        private const int ReservedLegacySurface = 0;
        private const int WallSouthSurface = 1;
        private const int WallEastSurface = 2;
        private const int WallNorthSurface = 3;
        private const int WallWestSurface = 4;
        private const int CapSurface = 5;
        private const int EdgeSurface = 6;
        private const int SurfaceCount = 7;

        private const float HalfCell = WallHalfCell;
        private const float TopHalfWidth = WallTopHalfWidth;
        private const float BottomHalfWidth = WallBottomHalfWidth;
        private const float TopDepth = WallTopDepth;
        private const float BaseDepth = WallBaseDepth;
        private const float HorizontalTopYOffset = HorizontalWallTopYOffset;
        private const float WallTextureDensity = 1.45f;
        private const string WallTwoSidedMeshLitShaderName = "Nting Campus/2D/Wall Mesh Lit Two Sided";
        private const string LegacyWallTwoSidedMeshUnlitShaderName = "Nting Campus/2D/Wall Mesh Unlit Two Sided";
        private const string WallTwoSidedMeshLitResourcePath = "Shaders/CampusWallMesh2D-Lit-TwoSided";
        private const string WallLitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";
        private const string WallMeshLitShaderName = "Universal Render Pipeline/2D/Mesh2D-Lit-Default";
        private const string WallModelChunkNamePrefix = "WallModel_Chunk_";

        private static readonly Color WallColor = new Color(0.70f, 0.60f, 0.52f, 1f);
        private static readonly Color CapColor = new Color(0.86f, 0.83f, 0.74f, 1f);
        private static readonly Color EdgeColor = new Color(0.045f, 0.040f, 0.035f, 1f);
        private static readonly Vector2 FallbackWallLightDirection = new Vector2(-0.52f, 0.85f).normalized;
        private const float FallbackWallLightWeight = 0.25f;
        private const float SunWallLightWeight = 12f;
        private const float PointWallLightWeight = 1f;
        private const int LightingVisibleChunkPadding = 1;
        private static readonly List<WallLightInfo> cachedCapturedLights = new List<WallLightInfo>(32);
        private static readonly List<Light2D> cachedSceneLights = new List<Light2D>(64);
        private static readonly Dictionary<int, WallMeshRootRegistry> meshRootRegistries = new Dictionary<int, WallMeshRootRegistry>();
        private static int cachedLightingFrame = -1;
        private static WallLightingSnapshot cachedLightingSnapshot;

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
            WallMeshRootRegistry registry = GetOrCreateRegistry(meshRoot);
            if (registry == null)
            {
                return false;
            }

            EnsureLightingCoordinator(meshRoot, floor, registry);

            registry.ClearAll(false);
            Dictionary<CampusWallRenderProfile, Material[]> profileMaterials = registry.GetScratchProfileMaterials();
            Dictionary<WallMeshBatchKey, WallMeshBatch> batches = registry.GetScratchChunkBatches();

            logic.CompressBounds();
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

                    WallMeshBatchKey key = new WallMeshBatchKey(profile, CampusWallChunkSystem.GetChunkCoord(cell.x), CampusWallChunkSystem.GetChunkCoord(cell.y));
                if (!batches.TryGetValue(key, out WallMeshBatch batch))
                {
                    if (!profileMaterials.TryGetValue(profile, out Material[] materials))
                    {
                        materials = CreateMaterials(profile);
                        profileMaterials.Add(profile, materials);
                    }

                    batch = registry.GetScratchBatch(profile, materials, key.ChunkX, key.ChunkY);
                    batches.Add(key, batch);
                }

                int connectionMask = CampusWallTileUtility.GetConnectionMask(logic, cell);
                Vector3 localCenter = meshRoot.InverseTransformPoint(logic.GetCellCenterWorld(cell));
                AddCellPrisms(batch.Builder, connectionMask, localCenter);
                batch.CellCount++;
            }

            foreach (WallMeshBatch batch in batches.Values)
            {
                UpdateOrCreateBatchModel(meshRoot, floor, batch, registry);
            }

            MarkAllLightingDirty(meshRoot, registry);
            meshRoot.gameObject.SetActive(true);
            return true;
        }

        public static bool RebuildChunksForCells(CampusFloorRoot floor, CampusWallVisualCatalog catalog, CampusWallRenderProfile fallbackProfile, IEnumerable<Vector3Int> affectedCells)
        {
            if (floor == null || affectedCells == null)
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

            HashSet<Vector2Int> chunks = CampusWallChunkSystem.CollectAffectedChunks(affectedCells);
            return RebuildChunks(floor, catalog, fallbackProfile, chunks);
        }

        public static bool RebuildChunks(CampusFloorRoot floor, CampusWallVisualCatalog catalog, CampusWallRenderProfile fallbackProfile, IReadOnlyCollection<Vector2Int> chunks)
        {
            if (floor == null || chunks == null)
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

            if (chunks.Count == 0)
            {
                return false;
            }

            Transform meshRoot = EnsureMeshRoot(floor);
            if (meshRoot == null)
            {
                return false;
            }

            WallMeshRootRegistry registry = GetOrCreateRegistry(meshRoot);
            if (registry == null)
            {
                return false;
            }

            EnsureLightingCoordinator(meshRoot, floor, registry);
            registry.EnsureInitialized(meshRoot);
            if (registry.HasUnindexedModels)
            {
                return RebuildFloor(floor, catalog, fallbackProfile);
            }

            CampusWallChunkBuildData buildData = CampusWallChunkBuildData.Capture(logic, chunks);
            return RebuildChunks(floor, catalog, fallbackProfile, chunks, buildData, meshRoot, registry);
        }

        internal static bool RebuildChunks(
            CampusFloorRoot floor,
            CampusWallVisualCatalog catalog,
            CampusWallRenderProfile fallbackProfile,
            IReadOnlyCollection<Vector2Int> chunks,
            CampusWallChunkBuildData buildData)
        {
            if (floor == null || chunks == null)
            {
                return false;
            }

            Tilemap logic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            CampusWallRenderProfile defaultProfile = catalog != null
                ? catalog.GetProfileOrDefault(fallbackProfile)
                : fallbackProfile;
            if (logic == null || defaultProfile == null || chunks.Count == 0)
            {
                return false;
            }

            Transform meshRoot = EnsureMeshRoot(floor);
            if (meshRoot == null)
            {
                return false;
            }

            WallMeshRootRegistry registry = GetOrCreateRegistry(meshRoot);
            if (registry == null)
            {
                return false;
            }

            EnsureLightingCoordinator(meshRoot, floor, registry);
            registry.EnsureInitialized(meshRoot);
            if (registry.HasUnindexedModels)
            {
                return RebuildFloor(floor, catalog, fallbackProfile);
            }

            return RebuildChunks(floor, catalog, fallbackProfile, chunks, buildData, meshRoot, registry);
        }

        private static bool RebuildChunks(
            CampusFloorRoot floor,
            CampusWallVisualCatalog catalog,
            CampusWallRenderProfile fallbackProfile,
            IReadOnlyCollection<Vector2Int> chunks,
            CampusWallChunkBuildData buildData,
            Transform meshRoot,
            WallMeshRootRegistry registry)
        {
            CampusWallRenderProfile defaultProfile = catalog != null
                ? catalog.GetProfileOrDefault(fallbackProfile)
                : fallbackProfile;
            Dictionary<CampusWallRenderProfile, Material[]> profileMaterials = registry.GetScratchProfileMaterials();
            foreach (Vector2Int chunk in chunks)
            {
                RebuildChunk(meshRoot, floor, catalog, defaultProfile, chunk.x, chunk.y, profileMaterials, registry, buildData);
            }

            MarkLightingDirty(meshRoot, chunks);
            meshRoot.gameObject.SetActive(true);
            return true;
        }

        private static void RebuildChunk(
            Transform meshRoot,
            CampusFloorRoot floor,
            CampusWallVisualCatalog catalog,
            CampusWallRenderProfile defaultProfile,
            int chunkX,
            int chunkY,
            Dictionary<CampusWallRenderProfile, Material[]> profileMaterials,
            WallMeshRootRegistry registry,
            CampusWallChunkBuildData buildData)
        {
            Dictionary<CampusWallRenderProfile, WallMeshBatch> batches = registry.GetScratchProfileBatches();
            Vector2Int chunkKey = new Vector2Int(chunkX, chunkY);
            if (buildData != null && buildData.TryGetChunk(chunkKey, out CampusWallChunkBuildData.ChunkData chunkData))
            {
                List<CampusWallChunkBuildData.CellData> cells = chunkData.Cells;
                for (int i = 0; i < cells.Count; i++)
                {
                    CampusWallChunkBuildData.CellData cellData = cells[i];
                    TileBase logicTile = cellData.Tile;
                    CampusWallRenderProfile profile = catalog != null
                        ? catalog.GetProfileForLogicTile(logicTile, defaultProfile)
                        : defaultProfile;
                    if (profile == null)
                    {
                        continue;
                    }

                    if (!batches.TryGetValue(profile, out WallMeshBatch batch))
                    {
                        if (!profileMaterials.TryGetValue(profile, out Material[] materials))
                        {
                            materials = registry.GetMaterials(profile);
                            if (materials == null)
                            {
                                materials = CreateMaterials(profile);
                                registry.RegisterMaterials(profile, materials);
                            }

                            profileMaterials.Add(profile, materials);
                        }

                        batch = registry.GetScratchBatch(profile, materials, chunkX, chunkY);
                        batches.Add(profile, batch);
                    }

                    Vector3 localCenter = meshRoot.InverseTransformPoint(cellData.WorldCenter);
                    AddCellPrisms(batch.Builder, cellData.ConnectionMask, localCenter);
                    batch.CellCount++;
                }
            }

            foreach (WallMeshBatch batch in batches.Values)
            {
                UpdateOrCreateBatchModel(meshRoot, floor, batch, registry);
            }

            registry.RemoveMissingChunkModels(chunkX, chunkY, batches);
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
                if (visible)
                {
                    MarkAllLightingDirty(root, GetOrCreateRegistry(root));
                }
            }
        }

        public static void ApplyTint(CampusFloorRoot floor, Color tint)
        {
            Transform root = floor != null ? floor.WallMeshRoot : null;
            if (root == null)
            {
                return;
            }

            WallMeshRootRegistry registry = GetOrCreateRegistry(root);
            if (registry == null)
            {
                return;
            }

            registry.EnsureInitialized(root);
            foreach (Vector2Int chunk in registry.ActiveChunks)
            {
                if (!registry.TryGetChunkVisuals(chunk, out List<CampusWallMeshVisual> visuals))
                {
                    continue;
                }

                for (int i = 0; i < visuals.Count; i++)
                {
                    CampusWallMeshVisual visual = visuals[i];
                    if (visual == null)
                    {
                        continue;
                    }

                    visual.SetTintWithoutRefresh(tint);
                }
            }

            if (Application.isPlaying)
            {
                MarkAllLightingDirty(root, registry);
                return;
            }

            foreach (Vector2Int chunk in registry.ActiveChunks)
            {
                if (!registry.TryGetChunkVisuals(chunk, out List<CampusWallMeshVisual> visuals))
                {
                    continue;
                }

                for (int i = 0; i < visuals.Count; i++)
                {
                    CampusWallMeshVisual visual = visuals[i];
                    if (visual == null)
                    {
                        continue;
                    }

                    visual.ApplyDynamicLightingNow();
                }
            }
        }

        public static Vector3 GetWallFaceCenterLocal(int rotation90)
        {
            GetWallFaceCorners(rotation90, out Vector3 bottomLeft, out Vector3 bottomRight, out Vector3 topRight, out Vector3 topLeft);
            return (bottomLeft + bottomRight + topRight + topLeft) * 0.25f;
        }

        public static void GetWallFaceBasis(int rotation90, out Vector3 widthAxis, out Vector3 heightAxis, out Vector3 normalAxis)
        {
            GetWallFaceCorners(rotation90, out Vector3 bottomLeft, out Vector3 bottomRight, out Vector3 topRight, out Vector3 topLeft);
            widthAxis = (bottomRight - bottomLeft).normalized;

            Vector3 rawHeight = ((topLeft + topRight) - (bottomLeft + bottomRight)) * 0.5f;
            normalAxis = Vector3.Cross(widthAxis, rawHeight).normalized;
            heightAxis = Vector3.Cross(normalAxis, widthAxis).normalized;
            if (Vector3.Dot(heightAxis, rawHeight) < 0f)
            {
                heightAxis = -heightAxis;
                normalAxis = -normalAxis;
            }
        }

        private static void GetWallFaceCorners(int rotation90, out Vector3 bottomLeft, out Vector3 bottomRight, out Vector3 topRight, out Vector3 topLeft)
        {
            switch (NormalizeWallFaceRotation(rotation90))
            {
                case 1:
                    bottomLeft = new Vector3(WallBottomHalfWidth, -WallBottomHalfWidth, WallBaseDepth);
                    bottomRight = new Vector3(WallBottomHalfWidth, WallBottomHalfWidth, WallBaseDepth);
                    topRight = new Vector3(WallTopHalfWidth, WallTopHalfWidth, WallTopDepth);
                    topLeft = new Vector3(WallTopHalfWidth, -WallTopHalfWidth, WallTopDepth);
                    return;
                case 2:
                    bottomLeft = new Vector3(WallBottomHalfWidth, -WallBottomHalfWidth, WallBaseDepth);
                    bottomRight = new Vector3(-WallBottomHalfWidth, -WallBottomHalfWidth, WallBaseDepth);
                    topRight = new Vector3(-WallTopHalfWidth, -WallTopHalfWidth, WallTopDepth);
                    topLeft = new Vector3(WallTopHalfWidth, -WallTopHalfWidth, WallTopDepth);
                    return;
                case 3:
                    bottomLeft = new Vector3(-WallBottomHalfWidth, WallBottomHalfWidth, WallBaseDepth);
                    bottomRight = new Vector3(-WallBottomHalfWidth, -WallBottomHalfWidth, WallBaseDepth);
                    topRight = new Vector3(-WallTopHalfWidth, -WallTopHalfWidth, WallTopDepth);
                    topLeft = new Vector3(-WallTopHalfWidth, WallTopHalfWidth, WallTopDepth);
                    return;
                default:
                    bottomLeft = new Vector3(-WallBottomHalfWidth, WallBottomHalfWidth, WallBaseDepth);
                    bottomRight = new Vector3(WallBottomHalfWidth, WallBottomHalfWidth, WallBaseDepth);
                    topRight = new Vector3(WallTopHalfWidth, WallTopHalfWidth, WallTopDepth);
                    topLeft = new Vector3(-WallTopHalfWidth, WallTopHalfWidth, WallTopDepth);
                    return;
            }
        }

        private static int NormalizeWallFaceRotation(int rotation90)
        {
            int normalized = rotation90 % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        private static void UpdateOrCreateBatchModel(Transform root, CampusFloorRoot floor, WallMeshBatch batch, WallMeshRootRegistry registry)
        {
            if (root == null || batch == null || !batch.Builder.HasGeometry)
            {
                return;
            }

            WallMeshBatchKey key = new WallMeshBatchKey(batch.Profile, batch.ChunkX, batch.ChunkY);
            string profileName = batch.Profile != null ? batch.Profile.name : "Fallback";
            GameObject model = registry.GetModel(key);
            if (model == null)
            {
                model = new GameObject(WallModelChunkNamePrefix + batch.ChunkX + "_" + batch.ChunkY + "_" + profileName);
                model.transform.SetParent(root, false);
            }

            model.name = WallModelChunkNamePrefix + batch.ChunkX + "_" + batch.ChunkY + "_" + profileName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            model.SetActive(true);

            MeshFilter filter = GetOrAddComponent<MeshFilter>(model);
            MeshRenderer renderer = GetOrAddComponent<MeshRenderer>(model);
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                filter.sharedMesh = mesh;
            }

            batch.Builder.BuildMesh(model.name, mesh);
            renderer.sharedMaterials = batch.Materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            CampusWallMeshVisual visual = GetOrAddComponent<CampusWallMeshVisual>(model);
            visual.Cell = Vector3Int.zero;
            visual.ConnectionMask = -1;
            visual.Profile = batch.Profile;
            visual.BaseMaterialColors = CaptureMaterialColors(batch.Materials);
            visual.CellCount = batch.CellCount;
            ApplyRendererSorting(floor, renderer);
            registry.RegisterMaterials(batch.Profile, batch.Materials);
            registry.RegisterModel(batch.Profile, batch.ChunkX, batch.ChunkY, model);
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
            float horizontalTopYOffset = hasHorizontalArm ? HorizontalTopYOffset : 0f;
            float centerTopMinY = -TopHalfWidth + horizontalTopYOffset;
            float centerTopMaxY = TopHalfWidth + horizontalTopYOffset;
            builder.AddPrism(
                -TopHalfWidth,
                centerTopMinY,
                TopHalfWidth,
                centerTopMaxY,
                -BottomHalfWidth,
                -BottomHalfWidth,
                BottomHalfWidth,
                BottomHalfWidth,
                !northArm,
                !eastArm,
                !southArm,
                !westArm);

            if (eastArm)
            {
                builder.AddPrism(
                    TopHalfWidth,
                    centerTopMinY,
                    HalfCell,
                    centerTopMaxY,
                    BottomHalfWidth,
                    -BottomHalfWidth,
                    HalfCell,
                    BottomHalfWidth,
                    true,
                    !eastConnected,
                    true,
                    false);
            }

            if (westArm)
            {
                builder.AddPrism(
                    -HalfCell,
                    centerTopMinY,
                    -TopHalfWidth,
                    centerTopMaxY,
                    -HalfCell,
                    -BottomHalfWidth,
                    -BottomHalfWidth,
                    BottomHalfWidth,
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
                    BottomHalfWidth,
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
                    -BottomHalfWidth,
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

        internal static WallLightingSnapshot CaptureWallLightingSnapshot(bool force = false)
        {
            if (Application.isPlaying && !force && cachedLightingSnapshot != null && cachedLightingFrame == Time.frameCount)
            {
                return cachedLightingSnapshot;
            }

            WallLightingSnapshot snapshot = CaptureWallLightingSnapshotUncached();
            if (Application.isPlaying)
            {
                cachedLightingFrame = Time.frameCount;
                cachedLightingSnapshot = snapshot;
            }

            return snapshot;
        }

        internal static void InvalidateWallLightingSnapshotCache()
        {
            cachedLightingFrame = -1;
            cachedLightingSnapshot = null;
        }

        private static WallLightingSnapshot CaptureWallLightingSnapshotUncached()
        {
            List<Light2D> lights = cachedSceneLights;
            CampusSceneLightUtility.CollectLights(lights, false);
            lights.Sort(CompareLightId);
            cachedCapturedLights.Clear();
            int signature = 17;
            for (int i = 0; i < lights.Count; i++)
            {
                Light2D light = lights[i];
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f || light.lightType == Light2D.LightType.Global)
                {
                    continue;
                }

                bool isSun = CampusObjectNames.MatchesAny(light.gameObject.name, CampusObjectNames.SunLight2D, CampusObjectNames.LegacySunLight2D);
                float radius = light.lightType == Light2D.LightType.Point ? Mathf.Max(0.001f, light.pointLightOuterRadius) : 1f;
                Vector3 position = light.transform.position;
                cachedCapturedLights.Add(new WallLightInfo(position, light.lightType, light.intensity, radius, isSun));

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

            return new WallLightingSnapshot(cachedCapturedLights, cachedCapturedLights.Count, signature);
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

        private static Vector2 ResolveDynamicLightDirection(List<WallLightInfo> lights, int lightCount, Vector3 worldReference)
        {
            Vector2 accumulated = FallbackWallLightDirection * FallbackWallLightWeight;
            int count = lights != null ? Mathf.Min(lightCount, lights.Count) : 0;
            for (int i = 0; i < count; i++)
            {
                WallLightInfo light = lights[i];
                if (light.LightType == Light2D.LightType.Global)
                {
                    continue;
                }

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
            private readonly List<WallLightInfo> lights;
            private readonly int lightCount;

            public WallLightingSnapshot(List<WallLightInfo> lights, int lightCount, int signature)
            {
                this.lights = lights ?? cachedCapturedLights;
                this.lightCount = Mathf.Max(0, lightCount);
                Signature = signature;
            }

            public int Signature { get; }

            public Vector2 ResolveDirection(Vector3 worldReference)
            {
                return ResolveDynamicLightDirection(lights, lightCount, worldReference);
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
            for (int materialIndex = WallSouthSurface; materialIndex < SurfaceCount; materialIndex++)
            {
                Material material = materials[materialIndex];
                Texture targetTexture = ResolveSurfaceTexture(materialIndex, material, faceTexture, capTexture);
                Color targetColor = ResolveSurfaceBaseColor(materialIndex);
                string targetName = ResolveSurfaceMaterialName(materialIndex);
                bool lit = IsWallSurface(materialIndex) || materialIndex == CapSurface || materialIndex == EdgeSurface;
                Shader targetShader = ResolveWallModelShader(false, lit);

                if (material == null)
                {
                    materials[materialIndex] = CreateMaterial(targetName, targetColor, targetTexture, lit);
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

                ConfigureMaterial(material, targetColor, targetTexture, false, lit);
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

        private static Material ResolveMaterial(Material assignedMaterial, string generatedName, Color color, Texture texture, bool cloneAssigned = false, bool lit = true)
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
                    ConfigureMaterial(material, color, assignedTexture, shadow, lit);
                    return material;
                }

                ConfigureMaterial(assignedMaterial, color, assignedTexture, shadow, lit);
                return assignedMaterial;
            }

            return CreateMaterial(generatedName, color, texture, lit);
        }

        private static Material CreateMaterial(string name, Color color, Texture texture, bool lit = true)
        {
            bool shadow = name.Contains("Shadow");
            Shader shader = ResolveWallModelShader(shadow, lit);
            color.a = shadow ? color.a : 1f;
            Material material = new Material(shader)
            {
                name = name,
                color = color,
                mainTexture = texture != null ? texture : Texture2D.whiteTexture,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                renderQueue = shadow ? 3000 : -1
            };
            ConfigureMaterial(material, color, texture, shadow, lit);
            return material;
        }

        private static Shader ResolveWallModelShader(bool shadow, bool lit = true)
        {
            Shader shader = null;
            if (!shadow && lit)
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

        private static void ConfigureMaterial(Material material, Color color, Texture texture, bool shadow, bool lit = true)
        {
            if (material == null)
            {
                return;
            }

            if (ShouldUseGeneratedWallShader(material.shader, shadow, lit))
            {
                material.shader = ResolveWallModelShader(shadow, lit);
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

        private static bool ShouldUseGeneratedWallShader(Shader shader, bool shadow, bool lit = true)
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
                   shaderName == LegacyWallTwoSidedMeshUnlitShaderName ||
                   shaderName == WallLitShaderName ||
                   shaderName == WallMeshLitShaderName ||
                   shaderName == "Universal Render Pipeline/2D/Sprite-Unlit-Default" ||
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

            if (meshRootRegistries.TryGetValue(root.GetInstanceID(), out WallMeshRootRegistry registry))
            {
                registry.ClearAll(false);
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private sealed class WallMeshBatch
        {
            public CampusWallRenderProfile Profile;
            public Material[] Materials;
            public int ChunkX;
            public int ChunkY;
            public readonly WallPrismBuilder Builder = new WallPrismBuilder();
            public int CellCount;

            public void Reset(CampusWallRenderProfile profile, Material[] materials, int chunkX, int chunkY)
            {
                Profile = profile;
                Materials = materials;
                ChunkX = chunkX;
                ChunkY = chunkY;
                CellCount = 0;
                Builder.Clear();
            }
        }

        private readonly struct WallMeshBatchKey : System.IEquatable<WallMeshBatchKey>
        {
            public WallMeshBatchKey(CampusWallRenderProfile profile, int chunkX, int chunkY)
            {
                Profile = profile;
                ChunkX = chunkX;
                ChunkY = chunkY;
            }

            public readonly CampusWallRenderProfile Profile;
            public readonly int ChunkX;
            public readonly int ChunkY;

            public bool Equals(WallMeshBatchKey other)
            {
                return ReferenceEquals(Profile, other.Profile) &&
                       ChunkX == other.ChunkX &&
                       ChunkY == other.ChunkY;
            }

            public override bool Equals(object obj)
            {
                return obj is WallMeshBatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Profile != null ? Profile.GetInstanceID() : 0;
                    hash = (hash * 397) ^ ChunkX;
                    hash = (hash * 397) ^ ChunkY;
                    return hash;
                }
            }
        }

        private sealed class WallMeshRootRegistry
        {
            public readonly Dictionary<CampusWallRenderProfile, Material[]> ProfileMaterials = new Dictionary<CampusWallRenderProfile, Material[]>();
            public readonly Dictionary<WallMeshBatchKey, GameObject> ChunkModels = new Dictionary<WallMeshBatchKey, GameObject>();
            private readonly Dictionary<Vector2Int, List<CampusWallMeshVisual>> chunkVisuals = new Dictionary<Vector2Int, List<CampusWallMeshVisual>>();
            private readonly List<WallMeshBatchKey> chunkKeysToRemove = new List<WallMeshBatchKey>();
            private readonly Dictionary<CampusWallRenderProfile, Material[]> scratchProfileMaterials = new Dictionary<CampusWallRenderProfile, Material[]>();
            private readonly Dictionary<WallMeshBatchKey, WallMeshBatch> scratchChunkBatches = new Dictionary<WallMeshBatchKey, WallMeshBatch>();
            private readonly Dictionary<CampusWallRenderProfile, WallMeshBatch> scratchProfileBatches = new Dictionary<CampusWallRenderProfile, WallMeshBatch>();
            private readonly List<WallMeshBatch> scratchBatchPool = new List<WallMeshBatch>();
            private int scratchBatchCount;
            private bool initialized;
            public bool HasUnindexedModels { get; private set; }

            public void EnsureInitialized(Transform root)
            {
                if (initialized)
                {
                    return;
                }

                ProfileMaterials.Clear();
                ChunkModels.Clear();
                HasUnindexedModels = false;
                if (root == null)
                {
                    initialized = true;
                    return;
                }

                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    CampusWallMeshVisual visual = child.GetComponent<CampusWallMeshVisual>();
                    if (visual == null || visual.Profile == null)
                    {
                        continue;
                    }

                    MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0 && !ProfileMaterials.ContainsKey(visual.Profile))
                    {
                        ProfileMaterials.Add(visual.Profile, renderer.sharedMaterials);
                    }

                    int chunkX;
                    int chunkY;
                    if (!TryReadChunkFromModelName(child.name, out chunkX, out chunkY))
                    {
                        HasUnindexedModels = true;
                        continue;
                    }

                    RegisterModel(visual.Profile, chunkX, chunkY, child.gameObject);
                }

                initialized = true;
            }

            public IEnumerable<Vector2Int> ActiveChunks => chunkVisuals.Keys;

            public Material[] GetMaterials(CampusWallRenderProfile profile)
            {
                if (profile == null)
                {
                    return null;
                }

                if (ProfileMaterials.TryGetValue(profile, out Material[] materials) && materials != null && materials.Length > 0)
                {
                    return materials;
                }

                return null;
            }

            public void RegisterMaterials(CampusWallRenderProfile profile, Material[] materials)
            {
                if (profile == null || materials == null || materials.Length == 0)
                {
                    return;
                }

                ProfileMaterials[profile] = materials;
                initialized = true;
            }

            public void RegisterModel(CampusWallRenderProfile profile, int chunkX, int chunkY, GameObject model)
            {
                if (profile == null || model == null)
                {
                    return;
                }

                WallMeshBatchKey key = new WallMeshBatchKey(profile, chunkX, chunkY);
                if (ChunkModels.TryGetValue(key, out GameObject previousModel) && previousModel != null && previousModel != model)
                {
                    UnregisterVisual(chunkX, chunkY, previousModel.GetComponent<CampusWallMeshVisual>());
                }

                ChunkModels[key] = model;
                CampusWallMeshVisual visual = model.GetComponent<CampusWallMeshVisual>();
                if (visual != null)
                {
                    RegisterVisual(chunkX, chunkY, visual);
                }

                initialized = true;
            }

            public GameObject GetModel(WallMeshBatchKey key)
            {
                if (ChunkModels.TryGetValue(key, out GameObject model))
                {
                    return model;
                }

                return null;
            }

            public void RemoveMissingChunkModels(int chunkX, int chunkY, Dictionary<CampusWallRenderProfile, WallMeshBatch> activeBatches)
            {
                chunkKeysToRemove.Clear();
                foreach (KeyValuePair<WallMeshBatchKey, GameObject> pair in ChunkModels)
                {
                    WallMeshBatchKey key = pair.Key;
                    if (key.ChunkX != chunkX || key.ChunkY != chunkY)
                    {
                        continue;
                    }

                    if (activeBatches != null && activeBatches.ContainsKey(key.Profile))
                    {
                        continue;
                    }

                    chunkKeysToRemove.Add(key);
                }

                for (int i = 0; i < chunkKeysToRemove.Count; i++)
                {
                    WallMeshBatchKey key = chunkKeysToRemove[i];
                    if (ChunkModels.TryGetValue(key, out GameObject model))
                    {
                        UnregisterVisual(key.ChunkX, key.ChunkY, model != null ? model.GetComponent<CampusWallMeshVisual>() : null);
                        DestroyChunkModel(model);
                        ChunkModels.Remove(key);
                    }
                }

                chunkKeysToRemove.Clear();
            }

            public void ClearAll(bool destroyModels)
            {
                if (destroyModels)
                {
                    foreach (GameObject model in ChunkModels.Values)
                    {
                        DestroyChunkModel(model);
                    }
                }

                ChunkModels.Clear();
                ProfileMaterials.Clear();
                chunkVisuals.Clear();
                HasUnindexedModels = false;
                initialized = true;
            }

            public bool TryGetChunkVisuals(Vector2Int chunk, out List<CampusWallMeshVisual> visuals)
            {
                return chunkVisuals.TryGetValue(chunk, out visuals);
            }

            public Dictionary<CampusWallRenderProfile, Material[]> GetScratchProfileMaterials()
            {
                scratchProfileMaterials.Clear();
                return scratchProfileMaterials;
            }

            public Dictionary<WallMeshBatchKey, WallMeshBatch> GetScratchChunkBatches()
            {
                scratchChunkBatches.Clear();
                scratchBatchCount = 0;
                return scratchChunkBatches;
            }

            public Dictionary<CampusWallRenderProfile, WallMeshBatch> GetScratchProfileBatches()
            {
                scratchProfileBatches.Clear();
                scratchBatchCount = 0;
                return scratchProfileBatches;
            }

            public WallMeshBatch GetScratchBatch(CampusWallRenderProfile profile, Material[] materials, int chunkX, int chunkY)
            {
                WallMeshBatch batch;
                if (scratchBatchCount < scratchBatchPool.Count)
                {
                    batch = scratchBatchPool[scratchBatchCount];
                }
                else
                {
                    batch = new WallMeshBatch();
                    scratchBatchPool.Add(batch);
                }

                scratchBatchCount++;
                batch.Reset(profile, materials, chunkX, chunkY);
                return batch;
            }

            private void RegisterVisual(int chunkX, int chunkY, CampusWallMeshVisual visual)
            {
                if (visual == null)
                {
                    return;
                }

                Vector2Int chunk = new Vector2Int(chunkX, chunkY);
                if (!chunkVisuals.TryGetValue(chunk, out List<CampusWallMeshVisual> visuals))
                {
                    visuals = new List<CampusWallMeshVisual>(2);
                    chunkVisuals.Add(chunk, visuals);
                }

                if (!visuals.Contains(visual))
                {
                    visuals.Add(visual);
                }
            }

            private void UnregisterVisual(int chunkX, int chunkY, CampusWallMeshVisual visual)
            {
                if (visual == null)
                {
                    return;
                }

                Vector2Int chunk = new Vector2Int(chunkX, chunkY);
                if (!chunkVisuals.TryGetValue(chunk, out List<CampusWallMeshVisual> visuals))
                {
                    return;
                }

                visuals.Remove(visual);
                if (visuals.Count == 0)
                {
                    chunkVisuals.Remove(chunk);
                }
            }
        }

        private static void EnsureLightingCoordinator(Transform root, CampusFloorRoot floor, WallMeshRootRegistry registry)
        {
            if (root == null || floor == null || registry == null)
            {
                return;
            }

            CampusWallLightingCoordinator coordinator = GetOrAddComponent<CampusWallLightingCoordinator>(root.gameObject);
            coordinator.Bind(floor, registry);
        }

        private static void MarkLightingDirty(Transform root, IReadOnlyCollection<Vector2Int> chunks)
        {
            if (root == null || chunks == null || chunks.Count == 0)
            {
                return;
            }

            CampusWallLightingCoordinator coordinator = root.GetComponent<CampusWallLightingCoordinator>();
            if (coordinator != null)
            {
                coordinator.MarkChunksDirty(chunks);
            }
        }

        private static void MarkAllLightingDirty(Transform root, WallMeshRootRegistry registry)
        {
            if (root == null || registry == null)
            {
                return;
            }

            CampusWallLightingCoordinator coordinator = root.GetComponent<CampusWallLightingCoordinator>();
            if (coordinator != null)
            {
                coordinator.MarkAllChunksDirty();
            }
        }

        private static WallMeshRootRegistry GetOrCreateRegistry(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            int rootId = root.GetInstanceID();
            if (!meshRootRegistries.TryGetValue(rootId, out WallMeshRootRegistry registry))
            {
                registry = new WallMeshRootRegistry();
                meshRootRegistries.Add(rootId, registry);
            }

            return registry;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (target == null)
            {
                return null;
            }

            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private sealed class CampusWallLightingCoordinator : MonoBehaviour
        {
            private const float RuntimeLightingRefreshInterval = 0.1f;

            private CampusFloorRoot floor;
            private WallMeshRootRegistry registry;
            private readonly HashSet<Vector2Int> dirtyChunks = new HashSet<Vector2Int>();
            private readonly HashSet<Vector2Int> visibleChunks = new HashSet<Vector2Int>();
            private readonly HashSet<Vector2Int> previousVisibleChunks = new HashSet<Vector2Int>();
            private readonly HashSet<Vector2Int> refreshChunks = new HashSet<Vector2Int>();
            private float nextRefreshTime;
            private int lastLightingSignature = int.MinValue;

            public void Bind(CampusFloorRoot targetFloor, WallMeshRootRegistry targetRegistry)
            {
                floor = targetFloor;
                registry = targetRegistry;
            }

            public void MarkChunksDirty(IReadOnlyCollection<Vector2Int> chunks)
            {
                if (chunks == null)
                {
                    return;
                }

                foreach (Vector2Int chunk in chunks)
                {
                    dirtyChunks.Add(chunk);
                }
            }

            public void MarkAllChunksDirty()
            {
                if (registry == null)
                {
                    return;
                }

                foreach (Vector2Int chunk in registry.ActiveChunks)
                {
                    dirtyChunks.Add(chunk);
                }
            }

            private void LateUpdate()
            {
                if (!Application.isPlaying || floor == null || registry == null)
                {
                    return;
                }

                if (Time.time < nextRefreshTime && dirtyChunks.Count == 0)
                {
                    return;
                }

                WallLightingSnapshot lighting = CaptureWallLightingSnapshot();
                bool lightingChanged = lighting.Signature != lastLightingSignature;
                bool visibleChanged = RefreshVisibleChunks();
                if (!lightingChanged && dirtyChunks.Count == 0 && !visibleChanged)
                {
                    ScheduleNextRefresh();
                    return;
                }

                if (lightingChanged)
                {
                    lastLightingSignature = lighting.Signature;
                }

                refreshChunks.Clear();
                if (lightingChanged)
                {
                    foreach (Vector2Int chunk in visibleChunks)
                    {
                        refreshChunks.Add(chunk);
                    }
                }
                else
                {
                    foreach (Vector2Int chunk in dirtyChunks)
                    {
                        if (visibleChunks.Contains(chunk))
                        {
                            refreshChunks.Add(chunk);
                        }
                    }

                    foreach (Vector2Int chunk in visibleChunks)
                    {
                        if (!previousVisibleChunks.Contains(chunk))
                        {
                            refreshChunks.Add(chunk);
                        }
                    }
                }

                foreach (Vector2Int chunk in refreshChunks)
                {
                    RefreshChunkLighting(chunk, lighting);
                    dirtyChunks.Remove(chunk);
                }

                ScheduleNextRefresh();
            }

            private void ScheduleNextRefresh()
            {
                nextRefreshTime = Time.time + RuntimeLightingRefreshInterval;
            }

            private bool RefreshVisibleChunks()
            {
                previousVisibleChunks.Clear();
                foreach (Vector2Int chunk in visibleChunks)
                {
                    previousVisibleChunks.Add(chunk);
                }

                visibleChunks.Clear();
                if (registry == null)
                {
                    return false;
                }

                Camera camera = Camera.main;
                Grid grid = floor != null ? floor.Grid : null;
                if (camera == null || grid == null || !camera.orthographic)
                {
                    foreach (Vector2Int chunk in registry.ActiveChunks)
                    {
                        visibleChunks.Add(chunk);
                    }
                }
                else
                {
                    float cameraDistance = Mathf.Abs(camera.transform.position.z - grid.transform.position.z);
                    Vector3 worldA = camera.ScreenToWorldPoint(new Vector3(0f, 0f, cameraDistance));
                    Vector3 worldB = camera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, cameraDistance));
                    Vector3Int cellA = grid.WorldToCell(worldA);
                    Vector3Int cellB = grid.WorldToCell(worldB);
                    int minChunkX = CampusWallChunkSystem.GetChunkCoord(Mathf.Min(cellA.x, cellB.x)) - LightingVisibleChunkPadding;
                    int maxChunkX = CampusWallChunkSystem.GetChunkCoord(Mathf.Max(cellA.x, cellB.x)) + LightingVisibleChunkPadding;
                    int minChunkY = CampusWallChunkSystem.GetChunkCoord(Mathf.Min(cellA.y, cellB.y)) - LightingVisibleChunkPadding;
                    int maxChunkY = CampusWallChunkSystem.GetChunkCoord(Mathf.Max(cellA.y, cellB.y)) + LightingVisibleChunkPadding;
                    for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
                    {
                        for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                        {
                            visibleChunks.Add(new Vector2Int(chunkX, chunkY));
                        }
                    }
                }

                if (visibleChunks.Count != previousVisibleChunks.Count)
                {
                    return true;
                }

                foreach (Vector2Int chunk in visibleChunks)
                {
                    if (!previousVisibleChunks.Contains(chunk))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void RefreshChunkLighting(Vector2Int chunk, WallLightingSnapshot lighting)
            {
                if (registry == null || !registry.TryGetChunkVisuals(chunk, out List<CampusWallMeshVisual> visuals))
                {
                    return;
                }

                for (int i = 0; i < visuals.Count; i++)
                {
                    CampusWallMeshVisual visual = visuals[i];
                    if (visual == null)
                    {
                        continue;
                    }

                    visual.ApplyDynamicLightingNow(lighting, false);
                }
            }
        }

        private static void DestroyChunkModel(GameObject model)
        {
            if (model == null)
            {
                return;
            }

            model.SetActive(false);
            MeshFilter filter = model.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                DestroyObject(filter.sharedMesh);
                filter.sharedMesh = null;
            }

            DestroyObject(model);
        }

        private static bool TryReadChunkFromModelName(string modelName, out int chunkX, out int chunkY)
        {
            chunkX = 0;
            chunkY = 0;
            if (string.IsNullOrEmpty(modelName) || !modelName.StartsWith(WallModelChunkNamePrefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            int xStart = WallModelChunkNamePrefix.Length;
            int xEnd = modelName.IndexOf('_', xStart);
            if (xEnd < 0)
            {
                return false;
            }

            int yStart = xEnd + 1;
            int yEnd = modelName.IndexOf('_', yStart);
            if (yEnd < 0)
            {
                yEnd = modelName.Length;
            }

            return int.TryParse(modelName.Substring(xStart, xEnd - xStart), out chunkX) &&
                   int.TryParse(modelName.Substring(yStart, yEnd - yStart), out chunkY);
        }

        private sealed class WallPrismBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<Vector4> lightingData = new List<Vector4>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector4> tangents = new List<Vector4>();
            private readonly List<Color> vertexColors = new List<Color>();
            private readonly List<int>[] triangles = CreateTriangleLists();
            private Vector3 origin;

            public bool HasGeometry => vertices.Count > 0;

            public void Clear()
            {
                vertices.Clear();
                uvs.Clear();
                lightingData.Clear();
                normals.Clear();
                tangents.Clear();
                vertexColors.Clear();
                for (int i = 0; i < triangles.Length; i++)
                {
                    triangles[i].Clear();
                }
            }

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

            public void BuildMesh(string name, Mesh mesh)
            {
                if (mesh == null)
                {
                    return;
                }

                mesh.name = name;
                mesh.Clear();
                if (vertices.Count > 65000)
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }
                else
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
                }

                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(1, lightingData);
                mesh.SetNormals(normals);
                mesh.SetTangents(tangents);
                mesh.SetColors(vertexColors);
                mesh.subMeshCount = SurfaceCount;
                for (int i = 0; i < SurfaceCount; i++)
                {
                    mesh.SetTriangles(triangles[i], i);
                }

                mesh.MarkDynamic();
                mesh.RecalculateBounds();
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
                Vector3 meshNormal = Vector3.Cross(b - a, d - a);
                if (meshNormal.sqrMagnitude <= 0.000001f)
                {
                    meshNormal = Vector3.back;
                }
                else
                {
                    meshNormal.Normalize();
                }

                Vector4 meshTangent = new Vector4(uAxis.x, uAxis.y, uAxis.z, 1f);
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

                normals.Add(meshNormal);
                normals.Add(meshNormal);
                normals.Add(meshNormal);
                normals.Add(meshNormal);

                tangents.Add(meshTangent);
                tangents.Add(meshTangent);
                tangents.Add(meshTangent);
                tangents.Add(meshTangent);

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
            if (!Application.isPlaying)
            {
                ApplyDynamicLightingNow(true);
            }
        }

        private void OnValidate()
        {
            ApplyDynamicLightingNow(true);
        }

        public void ApplyTint(Color tint)
        {
            Tint = tint;
            ApplyDynamicLightingNow(true);
        }

        public void SetTintWithoutRefresh(Color tint)
        {
            Tint = tint;
        }

        public void ApplyDynamicLightingNow()
        {
            ApplyDynamicLightingNow(false);
        }

        private void ApplyDynamicLightingNow(bool force)
        {
            ApplyDynamicLightingNow(CampusWallMeshRenderer.CaptureWallLightingSnapshot(force), force);
        }

        internal void ApplyDynamicLightingNow(CampusWallMeshRenderer.WallLightingSnapshot lighting, bool force)
        {
            MeshRenderer renderer = ResolveRenderer();
            MeshFilter filter = ResolveFilter();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (renderer == null || mesh == null)
            {
                return;
            }

            bool materialChanged = CampusWallMeshRenderer.EnsureWallModelMaterials(renderer, Profile);
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
