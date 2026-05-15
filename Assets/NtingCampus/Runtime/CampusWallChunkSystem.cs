using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    internal static class CampusWallChunkSystem
    {
        internal const int ChunkSize = 16;

        internal static HashSet<Vector2Int> CollectActiveChunks(Tilemap wallLogic)
        {
            HashSet<Vector2Int> chunks = new HashSet<Vector2Int>();
            if (wallLogic == null)
            {
                return chunks;
            }

            wallLogic.CompressBounds();
            BoundsInt bounds = wallLogic.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (wallLogic.HasTile(cell))
                {
                    chunks.Add(new Vector2Int(GetChunkCoord(cell.x), GetChunkCoord(cell.y)));
                }
            }

            return chunks;
        }

        internal static HashSet<Vector2Int> CollectAffectedChunks(IEnumerable<Vector3Int> changedCells)
        {
            HashSet<Vector2Int> chunks = new HashSet<Vector2Int>();
            if (changedCells == null)
            {
                return chunks;
            }

            foreach (Vector3Int cell in changedCells)
            {
                AddAffectedChunksForCell(chunks, cell);
            }

            return chunks;
        }

        internal static HashSet<Vector2Int> ExpandChunks(IEnumerable<Vector2Int> sourceChunks, int radius)
        {
            HashSet<Vector2Int> chunks = new HashSet<Vector2Int>();
            if (sourceChunks == null)
            {
                return chunks;
            }

            int clampedRadius = Mathf.Max(0, radius);
            foreach (Vector2Int chunk in sourceChunks)
            {
                for (int y = chunk.y - clampedRadius; y <= chunk.y + clampedRadius; y++)
                {
                    for (int x = chunk.x - clampedRadius; x <= chunk.x + clampedRadius; x++)
                    {
                        chunks.Add(new Vector2Int(x, y));
                    }
                }
            }

            return chunks;
        }

        internal static void AddAffectedChunksForCell(HashSet<Vector2Int> chunks, Vector3Int cell)
        {
            AddAffectedChunksForCell(chunks, cell.x, cell.y);
        }

        internal static void AddAffectedChunksForCell(HashSet<Vector2Int> chunks, int cellX, int cellY)
        {
            int chunkX = GetChunkCoord(cellX);
            int chunkY = GetChunkCoord(cellY);
            chunks.Add(new Vector2Int(chunkX, chunkY));

            int localX = GetChunkLocalCoord(cellX);
            int localY = GetChunkLocalCoord(cellY);
            bool touchesLeftEdge = localX == 0;
            bool touchesRightEdge = localX == ChunkSize - 1;
            bool touchesBottomEdge = localY == 0;
            bool touchesTopEdge = localY == ChunkSize - 1;
            if (touchesLeftEdge)
            {
                chunks.Add(new Vector2Int(chunkX - 1, chunkY));
            }
            else if (touchesRightEdge)
            {
                chunks.Add(new Vector2Int(chunkX + 1, chunkY));
            }

            if (touchesBottomEdge)
            {
                chunks.Add(new Vector2Int(chunkX, chunkY - 1));
            }
            else if (touchesTopEdge)
            {
                chunks.Add(new Vector2Int(chunkX, chunkY + 1));
            }

            if (touchesLeftEdge && touchesBottomEdge)
            {
                chunks.Add(new Vector2Int(chunkX - 1, chunkY - 1));
            }

            if (touchesLeftEdge && touchesTopEdge)
            {
                chunks.Add(new Vector2Int(chunkX - 1, chunkY + 1));
            }

            if (touchesRightEdge && touchesBottomEdge)
            {
                chunks.Add(new Vector2Int(chunkX + 1, chunkY - 1));
            }

            if (touchesRightEdge && touchesTopEdge)
            {
                chunks.Add(new Vector2Int(chunkX + 1, chunkY + 1));
            }
        }

        internal static int GetChunkCoord(int cellCoord)
        {
            return Mathf.FloorToInt(cellCoord / (float)ChunkSize);
        }

        internal static int GetChunkLocalCoord(int cellCoord)
        {
            int local = cellCoord % ChunkSize;
            return local < 0 ? local + ChunkSize : local;
        }
    }
}
