using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Describes the visual tiles used to convert logical wall cells into cutaway 2D wall layers.
    /// </summary>
    [CreateAssetMenu(menuName = "Nting Campus/Wall Render Profile", fileName = "CampusWallRenderProfile")]
    public sealed class CampusWallRenderProfile : ScriptableObject
    {
        public string ProfileId = "DefaultPrototypeWall";
        public Texture2D FaceSourceTexture;
        public Texture2D CapSourceTexture;
        public Material FaceMaterial;
        public Material CapMaterial;
        public Material EdgeMaterial;
        public TileBase LogicTile;
        public TileBase DefaultCapTile;
        public TileBase[] CapTilesByExposedMask = new TileBase[16];
        public TileBase SouthFaceTile;
        public TileBase WestSideTile;
        public TileBase EastSideTile;
        public TileBase BothSideTile;
        public TileBase EndNorth;
        public TileBase EndEast;
        public TileBase EndSouth;
        public TileBase EndWest;
        public TileBase OuterCornerNE;
        public TileBase OuterCornerNW;
        public TileBase OuterCornerSE;
        public TileBase OuterCornerSW;
        public TileBase InnerCornerNE;
        public TileBase InnerCornerNW;
        public TileBase InnerCornerSE;
        public TileBase InnerCornerSW;
        public TileBase TJunctionNorth;
        public TileBase TJunctionEast;
        public TileBase TJunctionSouth;
        public TileBase TJunctionWest;
        public TileBase Cross;

        public TileBase GetLogicTile()
        {
            return LogicTile != null ? LogicTile : DefaultCapTile;
        }

        public TileBase GetCapTile(int mask)
        {
            int index = Mathf.Clamp(mask, 0, 15);
            if (CapTilesByExposedMask != null && index < CapTilesByExposedMask.Length && CapTilesByExposedMask[index] != null)
            {
                return CapTilesByExposedMask[index];
            }

            return DefaultCapTile;
        }

        public TileBase GetFrontFaceTile()
        {
            return SouthFaceTile != null ? SouthFaceTile : DefaultCapTile;
        }

        public TileBase GetWestSideTile()
        {
            return WestSideTile != null ? WestSideTile : GetFrontFaceTile();
        }

        public TileBase GetEastSideTile()
        {
            return EastSideTile != null ? EastSideTile : GetFrontFaceTile();
        }

        public TileBase GetBothSideTile()
        {
            return BothSideTile != null ? BothSideTile : GetFrontFaceTile();
        }

    }
}
