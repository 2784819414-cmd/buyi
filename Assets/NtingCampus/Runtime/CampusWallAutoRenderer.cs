using UnityEngine;
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
            if (floor == null)
            {
                return;
            }

            if (CampusWallMeshRenderer.RebuildFloor(floor, catalog, fallbackProfile))
            {
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
                    CampusProjectedWallShadowRenderer.EnsureForFloor(floor);
                }
                else
                {
                    CampusProjectedWallShadowRenderer.ClearForFloor(floor);
                    CampusDynamicShadowUtility.RebuildWallGroundShadowCasters(floor);
                }

                NtingCustomShadowSystem.EnsureSceneSystem().MarkSceneDirty();
                floor.RefreshUsedBounds();
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
            CampusDynamicShadowUtility.ClearWallShadowCasters(floor);
            CampusDynamicShadowUtility.ClearWallGroundShadowCasters(floor);
            CampusProjectedWallShadowRenderer.ClearForFloor(floor);
            NtingWallPointShadowRenderer.ClearForFloor(floor);
        }

        public static void ApplyDebugView(CampusFloorRoot floor, CampusWallDebugView view)
        {
            if (floor == null)
            {
                return;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            bool showLogicFallback = view == CampusWallDebugView.ShowFinalWallVisuals &&
                                     HasWallLogicTiles(wallLogic) &&
                                     !HasWallMeshVisuals(floor);
            bool showLogic = view == CampusWallDebugView.ShowWallLogicOnly || view == CampusWallDebugView.ShowBoth || showLogicFallback;
            bool showVisuals = view == CampusWallDebugView.ShowFinalWallVisuals || view == CampusWallDebugView.ShowBoth;
            CampusWallTileUtility.SetTilemapVisible(wallLogic, showLogic);
            CampusWallTileUtility.SetTilemapVisible(floor.WallCapTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallFaceTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallSideTilemap, false);
            CampusWallTileUtility.SetTilemapVisible(floor.WallOverlayTilemap, false);
            CampusWallMeshRenderer.SetVisible(floor, showVisuals);
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
}
