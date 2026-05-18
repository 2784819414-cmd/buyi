using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Keeps wall visuals and placed objects in the same Y-sorted render band.
    /// </summary>
    public static class CampusRenderSortingUtility
    {
        public const string GroundShadowSortingLayerName = "Nting Ground";
        public const int FloorOffset = 0;
        public const int WallLogicOffset = 90;
        public const int WallFaceOffset = 120;
        public const int WallSideOffset = 121;
        public const int WallCapOffset = 122;
        public const int WallVisualOverlayOffset = 123;
        public const int SharedWallObjectOffset = WallFaceOffset;
        public const int OverlayOffset = 220;
        public const int CollisionDebugOffset = 420;

        private static readonly Vector3 TopDownSortAxis = new Vector3(0f, 1f, 0f);
        private const float CameraSortRefreshIntervalSeconds = 1f;
        private static bool globalTransparencySortConfigured;
        private static float nextCameraSortRefreshTime;

        public static void ConfigureTopDownTransparencySort()
        {
            if (!globalTransparencySortConfigured ||
                GraphicsSettings.transparencySortMode != TransparencySortMode.CustomAxis ||
                GraphicsSettings.transparencySortAxis != TopDownSortAxis)
            {
                GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
                GraphicsSettings.transparencySortAxis = TopDownSortAxis;
                globalTransparencySortConfigured = true;
            }

            if (Application.isPlaying && Time.unscaledTime < nextCameraSortRefreshTime)
            {
                return;
            }

            nextCameraSortRefreshTime = Application.isPlaying
                ? Time.unscaledTime + CameraSortRefreshIntervalSeconds
                : 0f;

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                camera.transparencySortMode = TransparencySortMode.CustomAxis;
                camera.transparencySortAxis = TopDownSortAxis;
            }
        }

        public static void ApplyFloorSorting(CampusFloorRoot floor, int sortingBase)
        {
            if (floor == null)
            {
                return;
            }

            ConfigureTopDownTransparencySort();

            int sortingLayerId = ResolveSortingLayerId(floor);
            int groundSortingLayerId = ResolveGroundShadowSortingLayerId(sortingLayerId);
            SetTilemapSorting(floor.FloorTilemap, groundSortingLayerId, sortingBase + FloorOffset, false);
            SetTilemapSorting(floor.WallLogicTilemap, sortingLayerId, sortingBase + WallLogicOffset, true);
            SetTilemapSorting(floor.WallFaceTilemap, sortingLayerId, sortingBase + WallFaceOffset, true);
            SetTilemapSorting(floor.WallSideTilemap, sortingLayerId, sortingBase + WallSideOffset, true);
            SetTilemapSorting(floor.WallCapTilemap, sortingLayerId, sortingBase + WallCapOffset, true);
            SetTilemapSorting(floor.WallOverlayTilemap, sortingLayerId, sortingBase + WallVisualOverlayOffset, true);
            SetMeshSorting(floor.WallMeshRoot, sortingLayerId, sortingBase + WallFaceOffset);
            SetTilemapSorting(floor.OverlayTilemap, sortingLayerId, sortingBase + OverlayOffset, false);
            SetTilemapSorting(floor.CollisionDebugTilemap, sortingLayerId, sortingBase + CollisionDebugOffset, false);

            ApplyObjectSortingGroups(floor.PropsRoot, sortingLayerId, sortingBase + SharedWallObjectOffset);
            ApplyObjectSortingGroups(floor.StairsRoot, sortingLayerId, sortingBase + SharedWallObjectOffset);
        }

        public static int[] GetGroundShadowSortingLayerIds(CampusFloorRoot floor)
        {
            int fallbackSortingLayerId = ResolveSortingLayerId(floor);
            return new[] { ResolveGroundShadowSortingLayerId(fallbackSortingLayerId) };
        }

        public static int ResolveGroundShadowSortingLayerId(int fallbackSortingLayerId)
        {
            return TryGetGroundShadowSortingLayerId(out int groundSortingLayerId)
                ? groundSortingLayerId
                : fallbackSortingLayerId;
        }

        public static bool TryGetGroundShadowSortingLayerId(out int id)
        {
            return TryGetSortingLayerIdByName(GroundShadowSortingLayerName, out id);
        }

        public static void ApplyObjectSortingGroups(Transform root, int sortingLayerId, int sortingOrder)
        {
            if (root == null)
            {
                return;
            }

            CampusPlacedObject[] objects = root.GetComponentsInChildren<CampusPlacedObject>(true);
            if (objects.Length == 0)
            {
                ApplyRendererDefaults(root, sortingLayerId);
                return;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                CampusPlacedObject placed = objects[i];
                if (placed == null)
                {
                    continue;
                }

                SortingGroup group = placed.GetComponent<SortingGroup>();
                if (group == null)
                {
                    group = placed.gameObject.AddComponent<SortingGroup>();
                }

                group.sortingLayerID = sortingLayerId;
                group.sortingOrder = sortingOrder + placed.SortingOrderOffset;
                ApplyRendererDefaults(placed.transform, sortingLayerId);
            }
        }

        private static void SetTilemapSorting(Tilemap tilemap, int sortingLayerId, int sortingOrder, bool ySorted)
        {
            if (tilemap == null)
            {
                return;
            }

            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
            }

            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            if (ySorted)
            {
                renderer.mode = TilemapRenderer.Mode.Individual;
                renderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;
            }
        }

        private static void SetMeshSorting(Transform root, int sortingLayerId, int sortingOrder)
        {
            if (root == null)
            {
                return;
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder;
            }
        }

        private static int ResolveSortingLayerId(CampusFloorRoot floor)
        {
            if (TryGetSortingLayerId(floor.WallFaceTilemap, out int id))
            {
                return id;
            }

            if (TryGetSortingLayerId(floor.WallCapTilemap, out id))
            {
                return id;
            }

            return SortingLayer.NameToID("Default");
        }

        private static bool TryGetSortingLayerId(Tilemap tilemap, out int id)
        {
            id = 0;
            if (tilemap == null)
            {
                return false;
            }

            Renderer renderer = tilemap.GetComponent<Renderer>();
            if (renderer == null)
            {
                return false;
            }

            id = renderer.sortingLayerID;
            return true;
        }

        private static bool TryGetSortingLayerIdByName(string layerName, out int id)
        {
            SortingLayer[] layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == layerName)
                {
                    id = layers[i].id;
                    return true;
                }
            }

            id = 0;
            return false;
        }

        private static void ApplyRendererDefaults(Transform root, int sortingLayerId)
        {
            if (root == null)
            {
                return;
            }

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingLayerID = sortingLayerId;
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }
    }
}
