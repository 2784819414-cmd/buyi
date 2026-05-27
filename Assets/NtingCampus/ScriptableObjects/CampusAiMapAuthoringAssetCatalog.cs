using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    [Serializable]
    public sealed class CampusAiMapAuthoringTileEntry
    {
        public string Id;
        public TileBase Tile;
    }

    [Serializable]
    public sealed class CampusAiMapAuthoringObjectEntry
    {
        public string Id;
        public GameObject Prefab;
    }

    [CreateAssetMenu(menuName = "Nting Campus/AI Map Authoring Asset Catalog", fileName = "CampusAiMapAuthoringAssetCatalog")]
    public sealed class CampusAiMapAuthoringAssetCatalog : ScriptableObject
    {
        public List<CampusAiMapAuthoringTileEntry> FloorTiles = new List<CampusAiMapAuthoringTileEntry>();
        public List<CampusAiMapAuthoringTileEntry> WallTiles = new List<CampusAiMapAuthoringTileEntry>();
        public List<CampusAiMapAuthoringObjectEntry> Objects = new List<CampusAiMapAuthoringObjectEntry>();
    }
}
