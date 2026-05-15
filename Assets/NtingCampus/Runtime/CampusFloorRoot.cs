using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Marks one editable 2D campus floor and stores references used by editor tools and floor preview.
    /// </summary>
    public sealed class CampusFloorRoot : MonoBehaviour
    {
        public int FloorIndex = 1;
        public Grid Grid;
        public Tilemap FloorTilemap;
        public Tilemap WallLogicTilemap;
        public Tilemap WallTilemap;
        public Tilemap WallCapTilemap;
        public Tilemap WallFaceTilemap;
        public Tilemap WallSideTilemap;
        public Tilemap WallOverlayTilemap;
        public Transform WallMeshRoot;
        public Tilemap OverlayTilemap;
        public Tilemap CollisionDebugTilemap;
        public Transform PropsRoot;
        public Transform StairsRoot;
        public bool IsUnlocked = true;
        public BoundsInt UsedBounds;
        [SerializeField, HideInInspector] private bool usedBoundsDirty = true;
        [System.NonSerialized] private bool usedBoundsInitialized;

        [HideInInspector] public Color OriginalFloorTilemapColor = Color.white;
        [HideInInspector] public Color OriginalWallLogicTilemapColor = Color.white;
        [HideInInspector] public Color OriginalWallTilemapColor = Color.white;
        [HideInInspector] public Color OriginalWallCapTilemapColor = Color.white;
        [HideInInspector] public Color OriginalWallFaceTilemapColor = Color.white;
        [HideInInspector] public Color OriginalWallSideTilemapColor = Color.white;
        [HideInInspector] public Color OriginalWallOverlayTilemapColor = Color.white;
        [HideInInspector] public Color OriginalOverlayTilemapColor = Color.white;
        [HideInInspector] public Color OriginalCollisionDebugTilemapColor = Color.white;
        [HideInInspector] public int OriginalFloorSortingOrder;
        [HideInInspector] public int OriginalWallLogicSortingOrder = 90;
        [HideInInspector] public int OriginalWallSortingOrder = 100;
        [HideInInspector] public int OriginalWallCapSortingOrder = 122;
        [HideInInspector] public int OriginalWallFaceSortingOrder = 120;
        [HideInInspector] public int OriginalWallSideSortingOrder = 121;
        [HideInInspector] public int OriginalWallOverlaySortingOrder = 123;
        [HideInInspector] public int OriginalOverlaySortingOrder = 200;
        [HideInInspector] public int OriginalCollisionDebugSortingOrder = 400;
        [HideInInspector] public bool HasCapturedOriginalRenderState;

        public IEnumerable<Tilemap> Tilemaps
        {
            get
            {
                if (FloorTilemap != null)
                {
                    yield return FloorTilemap;
                }

                if (WallTilemap != null)
                {
                    yield return WallTilemap;
                }

                if (WallLogicTilemap != null && WallLogicTilemap != WallTilemap)
                {
                    yield return WallLogicTilemap;
                }

                if (WallFaceTilemap != null)
                {
                    yield return WallFaceTilemap;
                }

                if (WallSideTilemap != null)
                {
                    yield return WallSideTilemap;
                }

                if (WallCapTilemap != null)
                {
                    yield return WallCapTilemap;
                }

                if (WallOverlayTilemap != null)
                {
                    yield return WallOverlayTilemap;
                }

                if (OverlayTilemap != null)
                {
                    yield return OverlayTilemap;
                }

                if (CollisionDebugTilemap != null)
                {
                    yield return CollisionDebugTilemap;
                }
            }
        }

        public void CaptureOriginalRenderState(bool force = false)
        {
            if (HasCapturedOriginalRenderState && !force)
            {
                return;
            }

            CaptureTilemapState(FloorTilemap, ref OriginalFloorTilemapColor, ref OriginalFloorSortingOrder);
            CaptureTilemapState(WallLogicTilemap, ref OriginalWallLogicTilemapColor, ref OriginalWallLogicSortingOrder);
            CaptureTilemapState(WallTilemap, ref OriginalWallTilemapColor, ref OriginalWallSortingOrder);
            CaptureTilemapState(WallFaceTilemap, ref OriginalWallFaceTilemapColor, ref OriginalWallFaceSortingOrder);
            CaptureTilemapState(WallSideTilemap, ref OriginalWallSideTilemapColor, ref OriginalWallSideSortingOrder);
            CaptureTilemapState(WallCapTilemap, ref OriginalWallCapTilemapColor, ref OriginalWallCapSortingOrder);
            CaptureTilemapState(WallOverlayTilemap, ref OriginalWallOverlayTilemapColor, ref OriginalWallOverlaySortingOrder);
            CaptureTilemapState(OverlayTilemap, ref OriginalOverlayTilemapColor, ref OriginalOverlaySortingOrder);
            CaptureTilemapState(CollisionDebugTilemap, ref OriginalCollisionDebugTilemapColor, ref OriginalCollisionDebugSortingOrder);
            HasCapturedOriginalRenderState = true;
        }

        public Color GetOriginalTilemapColor(Tilemap tilemap)
        {
            CaptureOriginalRenderState();
            if (tilemap == FloorTilemap)
            {
                return OriginalFloorTilemapColor;
            }

            if (tilemap == WallTilemap)
            {
                return OriginalWallTilemapColor;
            }

            if (tilemap == WallLogicTilemap)
            {
                return OriginalWallLogicTilemapColor;
            }

            if (tilemap == WallFaceTilemap)
            {
                return OriginalWallFaceTilemapColor;
            }

            if (tilemap == WallSideTilemap)
            {
                return OriginalWallSideTilemapColor;
            }

            if (tilemap == WallCapTilemap)
            {
                return OriginalWallCapTilemapColor;
            }

            if (tilemap == WallOverlayTilemap)
            {
                return OriginalWallOverlayTilemapColor;
            }

            if (tilemap == OverlayTilemap)
            {
                return OriginalOverlayTilemapColor;
            }

            if (tilemap == CollisionDebugTilemap)
            {
                return OriginalCollisionDebugTilemapColor;
            }

            return Color.white;
        }

        public int GetOriginalSortingOrder(Tilemap tilemap)
        {
            CaptureOriginalRenderState();
            if (tilemap == FloorTilemap)
            {
                return OriginalFloorSortingOrder;
            }

            if (tilemap == WallTilemap)
            {
                return OriginalWallSortingOrder;
            }

            if (tilemap == WallLogicTilemap)
            {
                return OriginalWallLogicSortingOrder;
            }

            if (tilemap == WallFaceTilemap)
            {
                return OriginalWallFaceSortingOrder;
            }

            if (tilemap == WallSideTilemap)
            {
                return OriginalWallSideSortingOrder;
            }

            if (tilemap == WallCapTilemap)
            {
                return OriginalWallCapSortingOrder;
            }

            if (tilemap == WallOverlayTilemap)
            {
                return OriginalWallOverlaySortingOrder;
            }

            if (tilemap == OverlayTilemap)
            {
                return OriginalOverlaySortingOrder;
            }

            if (tilemap == CollisionDebugTilemap)
            {
                return OriginalCollisionDebugSortingOrder;
            }

            return 0;
        }

        public Vector3 GetWorldCenter()
        {
            RefreshUsedBoundsIfDirty();

            if (Grid != null && UsedBounds.size != Vector3Int.zero)
            {
                Vector3Int min = UsedBounds.min;
                Vector3Int max = UsedBounds.max;
                Vector3 cellCenter = ((Vector3)min + (Vector3)max) * 0.5f;
                return Grid.CellToWorld(Vector3Int.FloorToInt(cellCenter));
            }

            return transform.position;
        }

        public void MarkUsedBoundsDirty()
        {
            usedBoundsDirty = true;
        }

        public void RefreshUsedBoundsIfDirty()
        {
            if (!usedBoundsInitialized || usedBoundsDirty)
            {
                RefreshUsedBounds();
            }
        }

        public void RefreshUsedBounds()
        {
            bool hasBounds = false;
            BoundsInt combined = new BoundsInt(Vector3Int.zero, Vector3Int.zero);

            foreach (Tilemap tilemap in Tilemaps)
            {
                if (tilemap == null)
                {
                    continue;
                }

                tilemap.CompressBounds();

                if (!HasAnyTile(tilemap))
                {
                    continue;
                }

                BoundsInt bounds = tilemap.cellBounds;
                if (!hasBounds)
                {
                    combined = bounds;
                    hasBounds = true;
                    continue;
                }

                combined = Encapsulate(combined, bounds);
            }

            UsedBounds = hasBounds ? combined : new BoundsInt(Vector3Int.zero, Vector3Int.zero);
            usedBoundsDirty = false;
            usedBoundsInitialized = true;
        }

        public bool HasContentAtCell(Vector3Int cell)
        {
            if (FloorTilemap != null && FloorTilemap.HasTile(cell))
            {
                return true;
            }

            Tilemap wallLogic = WallLogicTilemap != null ? WallLogicTilemap : WallTilemap;
            if (wallLogic != null && wallLogic.HasTile(cell))
            {
                return true;
            }

            if (OverlayTilemap != null && OverlayTilemap.HasTile(cell))
            {
                return true;
            }

            if (PropsRoot != null && PropsRoot.childCount > 0)
            {
                CampusPlacedObject[] objects = PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
                for (int i = 0; i < objects.Length; i++)
                {
                    if (objects[i] != null && objects[i].ContainsCell(cell))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasAnyTile(Tilemap tilemap)
        {
            BoundsInt bounds = tilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (tilemap.HasTile(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CaptureTilemapState(Tilemap tilemap, ref Color color, ref int sortingOrder)
        {
            if (tilemap == null)
            {
                return;
            }

            color = tilemap.color;
            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null)
            {
                sortingOrder = renderer.sortingOrder;
            }
        }

        private static BoundsInt Encapsulate(BoundsInt a, BoundsInt b)
        {
            Vector3Int min = Vector3Int.Min(a.min, b.min);
            Vector3Int max = Vector3Int.Max(a.max, b.max);
            return new BoundsInt(min, max - min);
        }
    }
}
