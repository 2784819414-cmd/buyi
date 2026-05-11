using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    public static class CampusDynamicShadowUtility
    {
        private const string WallShadowCasterRootName = "Dynamic Wall ShadowCasters 2D";
        private const string WallGroundShadowCasterRootName = "Wall Ground Shadow Casters 2D";
        private const string ObjectFallbackCasterName = "Dynamic Object ShadowCaster 2D";
        private const float GroundShadowTopHalfWidth = 0.205f;
        private const float GroundShadowCellHalf = 0.5f;
        private const float ObjectGroundShadowInsetFromCellEdge = GroundShadowCellHalf - GroundShadowTopHalfWidth;
        private static readonly bool useProjectedObjectShadowMesh = true;
        private static readonly FieldInfo ShadowCasterSortingLayersField = typeof(ShadowCaster2D).GetField("m_ApplyToSortingLayers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ShadowCasterShapePathField = typeof(ShadowCaster2D).GetField("m_ShapePath", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ShadowCasterShapePathHashField = typeof(ShadowCaster2D).GetField("m_ShapePathHash", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ShadowCasterCastingSourceField = typeof(ShadowCaster2D).GetField("m_ShadowCastingSource", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ShadowCasterForceMeshRebuildField = typeof(ShadowCaster2D).GetField("m_ForceShadowMeshRebuild", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void ApplyHighestRuntimeShadowQuality()
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames != null && qualityNames.Length > 0 && QualitySettings.GetQualityLevel() != qualityNames.Length - 1)
            {
                QualitySettings.SetQualityLevel(qualityNames.Length - 1, true);
            }

            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 150f);
            QualitySettings.shadowNearPlaneOffset = Mathf.Min(QualitySettings.shadowNearPlaneOffset, 1f);
        }

        public static void RebuildWallShadowCasters(CampusFloorRoot floor)
        {
            if (floor == null || floor.Grid == null || floor.WallMeshRoot == null)
            {
                return;
            }

            RemoveFixedWallShadowTilemaps(floor);

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                ClearWallShadowCasterRoot(floor.WallMeshRoot);
                return;
            }

            Transform shadowRoot = EnsureWallShadowCasterRoot(floor.WallMeshRoot);
            ClearChildren(shadowRoot);

            wallLogic.CompressBounds();
            BoundsInt bounds = wallLogic.cellBounds;
            Vector3 cellScale = GetGridCellScale(floor.Grid);
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!wallLogic.HasTile(cell))
                {
                    continue;
                }

                GameObject casterObject = new GameObject("WallShadowCaster_" + cell.x + "_" + cell.y);
                Transform casterTransform = casterObject.transform;
                casterTransform.SetParent(shadowRoot, false);
                casterTransform.position = wallLogic.GetCellCenterWorld(cell);
                casterTransform.localRotation = Quaternion.identity;
                casterTransform.localScale = cellScale;
                ConfigureCaster(casterObject.AddComponent<ShadowCaster2D>());
            }
        }

        public static void RebuildWallGroundShadowCasters(CampusFloorRoot floor)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            RemoveFixedWallShadowTilemaps(floor);
            if (!CampusRenderSortingUtility.TryGetGroundShadowSortingLayerId(out int groundSortingLayerId))
            {
                ClearWallGroundShadowCasters(floor);
                return;
            }

            EnsureLightsTargetGroundShadowLayer(groundSortingLayerId);

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                ClearWallGroundShadowCasters(floor);
                return;
            }

            Transform shadowRoot = EnsureWallGroundShadowCasterRoot(floor);
            ClearChildren(shadowRoot);

            int[] groundSortingLayerIds = { groundSortingLayerId };
            wallLogic.CompressBounds();
            BoundsInt bounds = wallLogic.cellBounds;
            Vector2 cellSize = GetGridCellSize(floor.Grid);
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!wallLogic.HasTile(cell))
                {
                    continue;
                }

                int connectionMask = CampusWallTileUtility.GetConnectionMask(wallLogic, cell);
                AddWallGroundShadowCasters(shadowRoot, wallLogic, cell, connectionMask, cellSize, groundSortingLayerIds);
            }
        }

        public static void ClearWallShadowCasters(CampusFloorRoot floor)
        {
            if (floor == null || floor.WallMeshRoot == null)
            {
                return;
            }

            Transform root = floor.WallMeshRoot.Find(WallShadowCasterRootName);
            if (root != null)
            {
                root.gameObject.SetActive(false);
                DestroyObject(root.gameObject);
            }
        }

        public static void ClearWallGroundShadowCasters(CampusFloorRoot floor)
        {
            Transform root = FindWallGroundShadowCasterRoot(floor);
            if (root != null)
            {
                root.gameObject.SetActive(false);
                DestroyObject(root.gameObject);
            }
        }

        public static void EnsureObjectShadowCasters(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            if (useProjectedObjectShadowMesh)
            {
                ClearObjectShadowCasters(floor);
                CampusProjectedWallShadowRenderer.EnsureForFloor(floor);
                return;
            }

            if (floor.PropsRoot != null)
            {
                CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
                for (int i = 0; i < objects.Length; i++)
                {
                    EnsureObjectShadowCasters(objects[i], floor.Grid);
                }
            }

            if (floor.StairsRoot != null)
            {
                CampusStairLink[] stairs = floor.StairsRoot.GetComponentsInChildren<CampusStairLink>(true);
                for (int i = 0; i < stairs.Length; i++)
                {
                    if (stairs[i] != null)
                    {
                        EnsureRendererShadowCasters(stairs[i].gameObject);
                    }
                }
            }
        }

        public static void EnsureObjectShadowCasters(CampusPlacedObject placed, Grid grid)
        {
            if (placed == null)
            {
                return;
            }

            if (useProjectedObjectShadowMesh)
            {
                ClearObjectShadowCasters(placed.gameObject);
                CampusFloorRoot floor = placed.GetComponentInParent<CampusFloorRoot>();
                if (floor == null && grid != null)
                {
                    floor = grid.GetComponentInParent<CampusFloorRoot>();
                }

                if (floor != null)
                {
                    CampusProjectedWallShadowRenderer.EnsureForFloor(floor);
                }

                return;
            }

            RemoveRendererShadowCasters(placed.gameObject);

            Transform fallback = placed.transform.Find(ObjectFallbackCasterName);
            if (fallback == null)
            {
                GameObject fallbackObject = new GameObject(ObjectFallbackCasterName);
                fallback = fallbackObject.transform;
                fallback.SetParent(placed.transform, false);
            }

            fallback.localPosition = Vector3.zero;
            fallback.localRotation = Quaternion.identity;
            fallback.localScale = Vector3.one;
            ShadowCaster2D caster = fallback.GetComponent<ShadowCaster2D>();
            if (caster == null)
            {
                caster = fallback.gameObject.AddComponent<ShadowCaster2D>();
            }

            if (!CampusRenderSortingUtility.TryGetGroundShadowSortingLayerId(out int groundSortingLayerId))
            {
                caster.enabled = false;
                return;
            }

            Vector3[] shapePath = BuildObjectGroundShadowShape(placed, grid);
            ConfigureCaster(caster, new[] { groundSortingLayerId });
            ApplyCasterShape(caster, shapePath);
        }

        public static void ClearObjectShadowCasters(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            if (floor.PropsRoot != null)
            {
                CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
                for (int i = 0; i < objects.Length; i++)
                {
                    if (objects[i] != null)
                    {
                        ClearObjectShadowCasters(objects[i].gameObject);
                    }
                }
            }

            if (floor.StairsRoot != null)
            {
                RemoveRendererShadowCasters(floor.StairsRoot.gameObject);
            }
        }

        private static void ClearObjectShadowCasters(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Transform fallback = root.transform.Find(ObjectFallbackCasterName);
            if (fallback != null)
            {
                fallback.gameObject.SetActive(false);
                DestroyObject(fallback.gameObject);
            }

            RemoveRendererShadowCasters(root);
        }

        public static void ConfigureLightShadows(Light2D light, bool enabled, float intensity, float softness, float softnessFalloff)
        {
            if (light == null)
            {
                return;
            }

            light.shadowsEnabled = enabled;
            light.shadowIntensity = Mathf.Clamp01(intensity);
            light.shadowSoftness = Mathf.Clamp01(softness);
            light.shadowSoftnessFalloffIntensity = Mathf.Clamp01(softnessFalloff);
        }

        public static void RemoveFixedWallShadowTilemaps(CampusFloorRoot floor)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            Tilemap[] tilemaps = floor.Grid.GetComponentsInChildren<Tilemap>(true);
            for (int i = tilemaps.Length - 1; i >= 0; i--)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap == null)
                {
                    continue;
                }

                if (CampusObjectNames.MatchesAny(tilemap.name, CampusObjectNames.WallShadowTilemap, CampusObjectNames.LegacyWallShadowTilemap))
                {
                    DestroyObject(tilemap.gameObject);
                }
            }
        }

        private static Transform EnsureWallShadowCasterRoot(Transform wallMeshRoot)
        {
            Transform root = wallMeshRoot.Find(WallShadowCasterRootName);
            if (root != null)
            {
                return root;
            }

            GameObject rootObject = new GameObject(WallShadowCasterRootName);
            root = rootObject.transform;
            root.SetParent(wallMeshRoot, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            rootObject.AddComponent<CompositeShadowCaster2D>();
            return root;
        }

        private static Transform EnsureWallGroundShadowCasterRoot(CampusFloorRoot floor)
        {
            Transform parent = floor.Grid != null ? floor.Grid.transform : floor.transform;
            Transform root = parent.Find(WallGroundShadowCasterRootName);
            if (root != null)
            {
                root.name = WallGroundShadowCasterRootName;
                root.SetParent(parent, false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
                if (root.GetComponent<CompositeShadowCaster2D>() == null)
                {
                    root.gameObject.AddComponent<CompositeShadowCaster2D>();
                }

                return root;
            }

            GameObject rootObject = new GameObject(WallGroundShadowCasterRootName);
            root = rootObject.transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            rootObject.AddComponent<CompositeShadowCaster2D>();
            return root;
        }

        private static Transform FindWallGroundShadowCasterRoot(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return null;
            }

            Transform parent = floor.Grid != null ? floor.Grid.transform : floor.transform;
            return parent != null ? parent.Find(WallGroundShadowCasterRootName) : null;
        }

        private static void AddWallGroundShadowCasters(Transform shadowRoot, Tilemap wallLogic, Vector3Int cell, int connectionMask, Vector2 cellSize, int[] groundSortingLayerIds)
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

            AddWallGroundShadowRect(shadowRoot, wallLogic, cell, "Center", -GroundShadowTopHalfWidth, -GroundShadowTopHalfWidth, GroundShadowTopHalfWidth, GroundShadowTopHalfWidth, cellSize, groundSortingLayerIds);

            if (eastArm)
            {
                AddWallGroundShadowRect(shadowRoot, wallLogic, cell, "East", GroundShadowTopHalfWidth, -GroundShadowTopHalfWidth, GroundShadowCellHalf, GroundShadowTopHalfWidth, cellSize, groundSortingLayerIds);
            }

            if (westArm)
            {
                AddWallGroundShadowRect(shadowRoot, wallLogic, cell, "West", -GroundShadowCellHalf, -GroundShadowTopHalfWidth, -GroundShadowTopHalfWidth, GroundShadowTopHalfWidth, cellSize, groundSortingLayerIds);
            }

            if (northArm)
            {
                AddWallGroundShadowRect(shadowRoot, wallLogic, cell, "North", -GroundShadowTopHalfWidth, GroundShadowTopHalfWidth, GroundShadowTopHalfWidth, GroundShadowCellHalf, cellSize, groundSortingLayerIds);
            }

            if (southArm)
            {
                AddWallGroundShadowRect(shadowRoot, wallLogic, cell, "South", -GroundShadowTopHalfWidth, -GroundShadowCellHalf, GroundShadowTopHalfWidth, -GroundShadowTopHalfWidth, cellSize, groundSortingLayerIds);
            }
        }

        private static void AddWallGroundShadowRect(Transform shadowRoot, Tilemap wallLogic, Vector3Int cell, string segmentName, float minX, float minY, float maxX, float maxY, Vector2 cellSize, int[] groundSortingLayerIds)
        {
            if (Mathf.Abs(maxX - minX) <= 0.001f || Mathf.Abs(maxY - minY) <= 0.001f)
            {
                return;
            }

            GameObject casterObject = new GameObject("WallGroundShadowCaster_" + cell.x + "_" + cell.y + "_" + segmentName);
            Transform casterTransform = casterObject.transform;
            casterTransform.SetParent(shadowRoot, false);
            casterTransform.position = wallLogic.GetCellCenterWorld(cell);
            casterTransform.localRotation = Quaternion.identity;
            casterTransform.localScale = Vector3.one;

            ShadowCaster2D caster = casterObject.AddComponent<ShadowCaster2D>();
            ConfigureCaster(caster, groundSortingLayerIds);
            ApplyCasterShape(caster, BuildRectShape(minX * cellSize.x, minY * cellSize.y, maxX * cellSize.x, maxY * cellSize.y));
        }

        private static void ClearWallShadowCasterRoot(Transform wallMeshRoot)
        {
            Transform root = wallMeshRoot != null ? wallMeshRoot.Find(WallShadowCasterRootName) : null;
            if (root != null)
            {
                ClearChildren(root);
            }
        }

        public static bool EnsureRendererShadowCasters(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            bool foundRenderer = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is TilemapRenderer)
                {
                    continue;
                }

                foundRenderer = true;
                ShadowCaster2D caster = renderer.GetComponent<ShadowCaster2D>();
                if (caster == null)
                {
                    caster = renderer.gameObject.AddComponent<ShadowCaster2D>();
                }

                if (CampusRenderSortingUtility.TryGetGroundShadowSortingLayerId(out int groundSortingLayerId))
                {
                    ConfigureCaster(caster, new[] { groundSortingLayerId });
                }
                else
                {
                    caster.enabled = false;
                }
            }

            return foundRenderer;
        }

        private static void ConfigureCaster(ShadowCaster2D caster)
        {
            ConfigureCaster(caster, null);
        }

        private static void ConfigureCaster(ShadowCaster2D caster, int[] shadowedSortingLayerIds)
        {
            if (caster == null)
            {
                return;
            }

            try
            {
                caster.enabled = true;
                caster.castingOption = ShadowCaster2D.ShadowCastingOptions.CastShadow;
                caster.alphaCutoff = 0.01f;
                if (shadowedSortingLayerIds != null && !ApplyShadowedSortingLayers(caster, shadowedSortingLayerIds))
                {
                    caster.enabled = false;
                    Debug.LogWarning("[NtingCampus] Disabled wall ground ShadowCaster2D on '" + caster.gameObject.name + "' because the URP sorting-layer field could not be configured.");
                }
            }
            catch (System.Exception exception)
            {
                caster.enabled = false;
                Debug.LogWarning("[NtingCampus] Failed to configure ShadowCaster2D on '" + caster.gameObject.name + "': " + exception.Message);
            }
        }

        private static bool ApplyShadowedSortingLayers(ShadowCaster2D caster, int[] sortingLayerIds)
        {
            if (caster == null || sortingLayerIds == null || sortingLayerIds.Length == 0 || ShadowCasterSortingLayersField == null)
            {
                return false;
            }

            try
            {
                int[] copiedIds = new int[sortingLayerIds.Length];
                System.Array.Copy(sortingLayerIds, copiedIds, sortingLayerIds.Length);
                ShadowCasterSortingLayersField.SetValue(caster, copiedIds);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[NtingCampus] Failed to assign ShadowCaster2D sorting layers on '" + caster.gameObject.name + "': " + exception.Message);
                return false;
            }
        }

        private static bool ApplyCasterShape(ShadowCaster2D caster, Vector3[] shapePath)
        {
            if (caster == null || shapePath == null || shapePath.Length < 3 || ShadowCasterShapePathField == null)
            {
                return false;
            }

            try
            {
                ShadowCasterShapePathField.SetValue(caster, shapePath);
                if (ShadowCasterShapePathHashField != null)
                {
                    ShadowCasterShapePathHashField.SetValue(caster, CalculateShapeHash(shapePath));
                }

                if (ShadowCasterCastingSourceField != null)
                {
                    object shapeEditorValue = System.Enum.ToObject(ShadowCasterCastingSourceField.FieldType, 1);
                    ShadowCasterCastingSourceField.SetValue(caster, shapeEditorValue);
                }

                if (ShadowCasterForceMeshRebuildField != null)
                {
                    ShadowCasterForceMeshRebuildField.SetValue(caster, true);
                }

                if (caster.isActiveAndEnabled)
                {
                    caster.Update();
                }

                return true;
            }
            catch (System.Exception exception)
            {
                caster.enabled = false;
                Debug.LogWarning("[NtingCampus] Failed to assign ShadowCaster2D shape on '" + caster.gameObject.name + "': " + exception.Message);
                return false;
            }
        }

        private static int CalculateShapeHash(Vector3[] shapePath)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < shapePath.Length; i++)
                {
                    hash = hash * 31 + shapePath[i].GetHashCode();
                }

                return hash;
            }
        }

        private static Vector3[] BuildObjectGroundShadowShape(CampusPlacedObject placed, Grid grid)
        {
            Vector2Int footprint = placed != null ? placed.NormalizedFootprintSize : Vector2Int.one;
            Vector2 cellSize = GetGridCellSize(grid);
            float halfWidth = Mathf.Max(GroundShadowTopHalfWidth * cellSize.x, footprint.x * cellSize.x * 0.5f - ObjectGroundShadowInsetFromCellEdge * cellSize.x);
            float halfHeight = Mathf.Max(GroundShadowTopHalfWidth * cellSize.y, footprint.y * cellSize.y * 0.5f - ObjectGroundShadowInsetFromCellEdge * cellSize.y);
            return BuildRectShape(-halfWidth, -halfHeight, halfWidth, halfHeight);
        }

        private static Vector3[] BuildRectShape(float minX, float minY, float maxX, float maxY)
        {
            return new[]
            {
                new Vector3(minX, minY, 0f),
                new Vector3(minX, maxY, 0f),
                new Vector3(maxX, maxY, 0f),
                new Vector3(maxX, minY, 0f)
            };
        }

        private static void RemoveRendererShadowCasters(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                ShadowCaster2D caster = renderer.GetComponent<ShadowCaster2D>();
                if (caster != null)
                {
                    DestroyObject(caster);
                }
            }
        }

        private static void EnsureLightsTargetGroundShadowLayer(int groundSortingLayerId)
        {
            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light == null)
                {
                    continue;
                }

                NormalizeLegacyShadowDefaults(light);
                int[] currentLayers = light.targetSortingLayers;
                if (currentLayers == null || currentLayers.Length == 0)
                {
                    light.targetSortingLayers = GetAllSortingLayerIds();
                }
                else if (!ContainsSortingLayer(currentLayers, groundSortingLayerId))
                {
                    int[] nextLayers = new int[currentLayers.Length + 1];
                    System.Array.Copy(currentLayers, nextLayers, currentLayers.Length);
                    nextLayers[nextLayers.Length - 1] = groundSortingLayerId;
                    light.targetSortingLayers = nextLayers;
                }
            }
        }

        private static void NormalizeLegacyShadowDefaults(Light2D light)
        {
            if (light == null || light.lightType == Light2D.LightType.Global || !light.shadowsEnabled)
            {
                return;
            }

            bool usesLegacyDefaults =
                Mathf.Approximately(light.shadowIntensity, 0.75f) &&
                Mathf.Approximately(light.shadowSoftness, 0.3f) &&
                Mathf.Approximately(light.shadowSoftnessFalloffIntensity, 0.5f);
            if (!usesLegacyDefaults)
            {
                return;
            }

            light.shadowIntensity = 0.45f;
            light.shadowSoftness = 0.75f;
            light.shadowSoftnessFalloffIntensity = 0.85f;
        }

        private static int[] GetAllSortingLayerIds()
        {
            SortingLayer[] layers = SortingLayer.layers;
            int[] ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                ids[i] = layers[i].id;
            }

            return ids;
        }

        private static bool ContainsSortingLayer(int[] layers, int sortingLayerId)
        {
            if (layers == null)
            {
                return false;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == sortingLayerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 GetGridCellScale(Grid grid)
        {
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            return new Vector3(Mathf.Max(0.01f, Mathf.Abs(cellSize.x)), Mathf.Max(0.01f, Mathf.Abs(cellSize.y)), 1f);
        }

        private static Vector2 GetGridCellSize(Grid grid)
        {
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            return new Vector2(Mathf.Max(0.01f, Mathf.Abs(cellSize.x)), Mathf.Max(0.01f, Mathf.Abs(cellSize.y)));
        }

        private static Vector3 GetObjectFallbackScale(CampusPlacedObject placed, Grid grid)
        {
            Vector2Int footprint = placed != null ? placed.RotatedFootprintSize : Vector2Int.one;
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            return new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(cellSize.x) * Mathf.Max(1, footprint.x)),
                Mathf.Max(0.01f, Mathf.Abs(cellSize.y) * Mathf.Max(1, footprint.y)),
                1f);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyObject(parent.GetChild(i).gameObject);
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
    }
}
