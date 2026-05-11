using UnityEngine;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Metadata for an editor-placed campus object instance.
    /// </summary>
    public sealed class CampusPlacedObject : MonoBehaviour
    {
        public int FloorIndex = 1;
        public string ObjectId;
        public Vector3Int Cell;
        public Vector2Int FootprintSize = Vector2Int.one;
        public int Rotation90;
        public int SortingOrderOffset;
        public bool BlocksMovement;
        public bool BlocksSight;
        public bool IsInteractable;

        public Vector2Int NormalizedFootprintSize => NormalizeFootprintSize(FootprintSize);

        public Vector2Int RotatedFootprintSize => RotateFootprintSize(FootprintSize, Rotation90);

        public void RefreshCellFromTransform(Grid grid)
        {
            if (grid == null)
            {
                return;
            }

            Cell = ResolveCellFromTransform(grid);
        }

        public void ApplyCellToTransform(Grid grid)
        {
            if (grid == null)
            {
                return;
            }

            transform.position = GetFootprintWorldCenter(grid, Cell, RotatedFootprintSize);
        }

        public Vector3Int ResolveCellFromTransform(Grid grid)
        {
            if (grid == null)
            {
                return Vector3Int.zero;
            }

            Vector2Int footprint = RotatedFootprintSize;
            Vector3 cellOffset = new Vector3((footprint.x - 1) * 0.5f, (footprint.y - 1) * 0.5f, 0f);
            Vector3 worldOffset = grid.transform.TransformVector(Vector3.Scale(cellOffset, grid.cellSize));
            return grid.WorldToCell(transform.position - worldOffset);
        }

        public bool ContainsCell(Vector3Int cell)
        {
            Vector2Int footprint = RotatedFootprintSize;
            return cell.z == Cell.z &&
                   cell.x >= Cell.x &&
                   cell.x < Cell.x + footprint.x &&
                   cell.y >= Cell.y &&
                   cell.y < Cell.y + footprint.y;
        }

        public static Vector2Int NormalizeFootprintSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        public static Vector2Int RotateFootprintSize(Vector2Int size, int rotation90)
        {
            Vector2Int normalized = NormalizeFootprintSize(size);
            int turns = ((rotation90 % 4) + 4) % 4;
            return turns == 1 || turns == 3
                ? new Vector2Int(normalized.y, normalized.x)
                : normalized;
        }

        public static Vector3 GetFootprintWorldCenter(Grid grid, Vector3Int anchorCell, Vector2Int rotatedFootprintSize)
        {
            if (grid == null)
            {
                return Vector3.zero;
            }

            Vector2Int footprint = NormalizeFootprintSize(rotatedFootprintSize);
            Vector3Int farCell = new Vector3Int(
                anchorCell.x + footprint.x - 1,
                anchorCell.y + footprint.y - 1,
                anchorCell.z);
            return (grid.GetCellCenterWorld(anchorCell) + grid.GetCellCenterWorld(farCell)) * 0.5f;
        }
    }
}
