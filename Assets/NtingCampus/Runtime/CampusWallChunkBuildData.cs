using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    internal sealed class CampusWallChunkBuildData
    {
        internal readonly struct CellData
        {
            internal CellData(Vector3Int cell, TileBase tile, int connectionMask, Vector3 worldCenter)
            {
                Cell = cell;
                Tile = tile;
                ConnectionMask = connectionMask;
                WorldCenter = worldCenter;
            }

            internal readonly Vector3Int Cell;
            internal readonly TileBase Tile;
            internal readonly int ConnectionMask;
            internal readonly Vector3 WorldCenter;
        }

        internal sealed class ChunkData
        {
            internal ChunkData(Vector2Int chunk)
            {
                Chunk = chunk;
            }

            internal Vector2Int Chunk { get; }
            internal readonly List<CellData> Cells = new List<CellData>(64);
        }

        private readonly Dictionary<Vector2Int, ChunkData> chunks = new Dictionary<Vector2Int, ChunkData>();

        internal static CampusWallChunkBuildData Capture(Tilemap wallLogic, IReadOnlyCollection<Vector2Int> targetChunks)
        {
            CampusWallChunkBuildData data = new CampusWallChunkBuildData();
            if (wallLogic == null || targetChunks == null || targetChunks.Count == 0)
            {
                return data;
            }

            foreach (Vector2Int chunk in targetChunks)
            {
                ChunkData chunkData = new ChunkData(chunk);
                int minX = chunk.x * CampusWallChunkSystem.ChunkSize;
                int minY = chunk.y * CampusWallChunkSystem.ChunkSize;
                int maxX = minX + CampusWallChunkSystem.ChunkSize;
                int maxY = minY + CampusWallChunkSystem.ChunkSize;
                for (int y = minY; y < maxY; y++)
                {
                    for (int x = minX; x < maxX; x++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (!wallLogic.HasTile(cell))
                        {
                            continue;
                        }

                        chunkData.Cells.Add(new CellData(
                            cell,
                            wallLogic.GetTile(cell),
                            CampusWallTileUtility.GetConnectionMask(wallLogic, cell),
                            wallLogic.GetCellCenterWorld(cell)));
                    }
                }

                data.chunks.Add(chunk, chunkData);
            }

            return data;
        }

        internal bool TryGetChunk(Vector2Int chunk, out ChunkData chunkData)
        {
            return chunks.TryGetValue(chunk, out chunkData);
        }
    }
}
