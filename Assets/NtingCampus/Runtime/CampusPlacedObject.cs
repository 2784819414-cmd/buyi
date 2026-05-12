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
        public bool AllowRotation;
        public Sprite Rotation0Sprite;
        public Sprite Rotation90Sprite;
        public Sprite Rotation180Sprite;
        public Sprite Rotation270Sprite;
        public int Rotation90;
        public int SortingOrderOffset;
        public bool BlocksMovement;
        public bool BlocksSight;
        public bool IsInteractable;

        private Sprite runtimeDefaultSprite;

        public Vector2Int NormalizedFootprintSize => NormalizeFootprintSize(FootprintSize);

        public Vector2Int RotatedFootprintSize => RotateFootprintSize(FootprintSize, Rotation90);

        private void Awake()
        {
            CacheDefaultSprite();
            if (AllowRotation || HasDirectionalSprites())
            {
                ApplyRotationVisualState();
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                CacheDefaultSprite();
                if (AllowRotation || HasDirectionalSprites())
                {
                    ApplyRotationVisualState();
                }
            }
        }

        public int ResolveAllowedRotation90(int requestedRotation90)
        {
            return AllowRotation ? NormalizeRotation90(requestedRotation90) : 0;
        }

        public void ApplyPlacementRotation(int requestedRotation90)
        {
            Rotation90 = ResolveAllowedRotation90(requestedRotation90);
            ApplyRotationVisualState();
        }

        public void ApplyRotationVisualState()
        {
            Rotation90 = ResolveAllowedRotation90(Rotation90);
            transform.localRotation = Quaternion.identity;
            ApplyColliderFootprintSize();
            ApplyDirectionalSprite();
        }

        public Sprite ResolveSpriteForRotation(int requestedRotation90, out bool usesAuthoredDirectionalSprite, out int effectiveRotation90)
        {
            effectiveRotation90 = ResolveAllowedRotation90(requestedRotation90);
            Sprite directionalSprite = GetDirectionalSprite(effectiveRotation90);
            usesAuthoredDirectionalSprite = directionalSprite != null;
            if (usesAuthoredDirectionalSprite)
            {
                return directionalSprite;
            }

            SpriteRenderer renderer = GetPrimarySpriteRenderer();
            return Rotation0Sprite != null ? Rotation0Sprite : (renderer != null ? renderer.sprite : null);
        }

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

        public static int NormalizeRotation90(int rotation90)
        {
            return ((rotation90 % 4) + 4) % 4;
        }

        public static Vector2Int RotateFootprintSize(Vector2Int size, int rotation90)
        {
            Vector2Int normalized = NormalizeFootprintSize(size);
            int turns = NormalizeRotation90(rotation90);
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

        private void CacheDefaultSprite()
        {
            SpriteRenderer renderer = GetPrimarySpriteRenderer();
            if (renderer != null && runtimeDefaultSprite == null && !IsConfiguredDirectionalSprite(renderer.sprite))
            {
                runtimeDefaultSprite = renderer.sprite;
            }
        }

        private void ApplyColliderFootprintSize()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Vector2Int footprint = RotatedFootprintSize;
                box.size = new Vector2(footprint.x, footprint.y);
            }
        }

        private void ApplyDirectionalSprite()
        {
            SpriteRenderer renderer = GetPrimarySpriteRenderer();
            if (renderer == null)
            {
                return;
            }

            Sprite baseSprite = Rotation0Sprite != null ? Rotation0Sprite : (runtimeDefaultSprite != null ? runtimeDefaultSprite : renderer.sprite);
            Sprite directionalSprite = GetDirectionalSprite(Rotation90);
            bool usesAuthoredDirectionSprite = directionalSprite != null;
            renderer.sprite = usesAuthoredDirectionSprite ? directionalSprite : baseSprite;

            Transform visualTransform = renderer.transform;
            if (visualTransform != null)
            {
                visualTransform.localRotation = usesAuthoredDirectionSprite || !AllowRotation
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 0f, Rotation90 * 90f);
            }
        }

        private SpriteRenderer GetPrimarySpriteRenderer()
        {
            return GetComponentInChildren<SpriteRenderer>(true);
        }

        private Sprite GetDirectionalSprite(int rotation90)
        {
            switch (NormalizeRotation90(rotation90))
            {
                case 0:
                    return Rotation0Sprite;
                case 1:
                    return Rotation90Sprite;
                case 2:
                    return Rotation180Sprite;
                case 3:
                    return Rotation270Sprite;
                default:
                    return null;
            }
        }

        private bool IsConfiguredDirectionalSprite(Sprite sprite)
        {
            return sprite != null &&
                   (sprite == Rotation0Sprite ||
                    sprite == Rotation90Sprite ||
                    sprite == Rotation180Sprite ||
                    sprite == Rotation270Sprite);
        }

        private bool HasDirectionalSprites()
        {
            return Rotation0Sprite != null ||
                   Rotation90Sprite != null ||
                   Rotation180Sprite != null ||
                   Rotation270Sprite != null;
        }
    }
}
