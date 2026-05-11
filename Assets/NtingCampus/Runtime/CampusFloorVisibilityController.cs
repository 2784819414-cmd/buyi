using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Applies layered 2D preview without moving authored floor transforms.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampusFloorVisibilityController : MonoBehaviour
    {
        public CampusMapRoot MapRoot;
        public bool IsPreviewActive { get; private set; }

        private void Reset()
        {
            MapRoot = GetComponent<CampusMapRoot>();
        }

        private void Awake()
        {
            if (MapRoot == null)
            {
                MapRoot = GetComponent<CampusMapRoot>();
            }
        }

        public void PreviewFloor(int floorIndex)
        {
            if (MapRoot == null)
            {
                MapRoot = GetComponent<CampusMapRoot>();
            }

            if (MapRoot == null)
            {
                return;
            }

            MapRoot.CurrentPreviewFloor = Mathf.Max(1, floorIndex);
            RefreshAll();
            IsPreviewActive = true;
        }

        public void SetPlayerFloor(int floorIndex)
        {
            if (MapRoot == null)
            {
                MapRoot = GetComponent<CampusMapRoot>();
            }

            if (MapRoot == null)
            {
                return;
            }

            MapRoot.CurrentPreviewFloor = Mathf.Max(1, floorIndex);
            RefreshAll();
            IsPreviewActive = true;
        }

        public void ResetVisibility()
        {
            if (MapRoot == null)
            {
                MapRoot = GetComponent<CampusMapRoot>();
            }

            if (MapRoot == null)
            {
                return;
            }

            MapRoot.RebuildFloorReferences();
            for (int i = 0; i < MapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = MapRoot.Floors[i];
                if (floor == null)
                {
                    continue;
                }

                floor.gameObject.SetActive(true);
                RestoreRenderState(floor);
            }

            IsPreviewActive = false;
        }

        public void RefreshAll()
        {
            if (MapRoot == null)
            {
                MapRoot = GetComponent<CampusMapRoot>();
            }

            if (MapRoot == null)
            {
                return;
            }

            MapRoot.RebuildFloorReferences();

            int current = Mathf.Max(1, MapRoot.CurrentPreviewFloor);
            for (int i = 0; i < MapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = MapRoot.Floors[i];
                if (floor == null)
                {
                    continue;
                }

                int delta = current - floor.FloorIndex;
                if (delta < 0)
                {
                    floor.gameObject.SetActive(false);
                    continue;
                }

                floor.gameObject.SetActive(true);

                float alpha = delta == 0 ? 1f : Mathf.Pow(MapRoot.LowerFloorAlphaStep, delta);
                float darken = delta == 0 ? 1f : Mathf.Pow(MapRoot.LowerFloorDarkenStep, delta);
                int sortingBase = delta == 0
                    ? floor.FloorIndex * MapRoot.SortingOrderStepPerFloor + 500
                    : floor.FloorIndex * MapRoot.SortingOrderStepPerFloor - delta * 200;

                ApplyAlphaAndDarken(floor, alpha, darken);
                ApplySorting(floor, sortingBase);
            }
        }

        private static void ApplyAlphaAndDarken(CampusFloorRoot floor, float alpha, float darken)
        {
            Color color = new Color(darken, darken, darken, alpha);
            foreach (Tilemap tilemap in floor.Tilemaps)
            {
                if (tilemap != null)
                {
                    tilemap.color = color;
                }
            }

            ApplySpriteColor(floor.PropsRoot, color);
            ApplySpriteColor(floor.StairsRoot, color);
            CampusWallMeshRenderer.ApplyTint(floor, color);
        }

        private static void ApplySpriteColor(Transform root, Color color)
        {
            if (root == null)
            {
                return;
            }

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = color;
                }
            }
        }

        private static void ApplySorting(CampusFloorRoot floor, int sortingBase)
        {
            CampusRenderSortingUtility.ApplyFloorSorting(floor, sortingBase);
        }

        private static void RestoreRenderState(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            floor.CaptureOriginalRenderState();
            foreach (Tilemap tilemap in floor.Tilemaps)
            {
                RestoreTilemapState(tilemap, floor.GetOriginalTilemapColor(tilemap), floor.GetOriginalSortingOrder(tilemap));
            }

            ApplySpriteColor(floor.PropsRoot, Color.white);
            ApplySpriteColor(floor.StairsRoot, Color.white);
            CampusWallMeshRenderer.ApplyTint(floor, Color.white);
            int sortingBase = floor.GetOriginalSortingOrder(floor.FloorTilemap) - CampusRenderSortingUtility.FloorOffset;
            CampusRenderSortingUtility.ApplyFloorSorting(floor, sortingBase);
        }

        private static void RestoreTilemapState(Tilemap tilemap, Color color, int sortingOrder)
        {
            if (tilemap == null)
            {
                return;
            }

            tilemap.color = color;
            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
        }

    }
}
