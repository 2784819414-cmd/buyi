using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    internal sealed class CampusAiMapAuthoringCatalog
    {
        public const string DefaultCatalogPath = CampusMapEditorUtility.ScriptableObjectsPath + "/CampusAiMapAuthoringAssetCatalog.asset";

        private readonly Dictionary<string, TileBase> floorTiles = new Dictionary<string, TileBase>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TileBase> wallTiles = new Dictionary<string, TileBase>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        public static CampusAiMapAuthoringCatalog BuildDefault()
        {
            CampusMapEditorUtility.EnsureDirectories();

            CampusAiMapAuthoringCatalog catalog = new CampusAiMapAuthoringCatalog();
            catalog.RegisterExplicitCatalog(AssetDatabase.LoadAssetAtPath<CampusAiMapAuthoringAssetCatalog>(DefaultCatalogPath));
            catalog.RegisterPrefabPalette(AssetDatabase.LoadAssetAtPath<CampusPrefabPalette>(CampusMapEditorUtility.DefaultPrefabPalettePath));
            catalog.RegisterPrefabsFromFolder(CampusMapEditorUtility.PropPrefabsPath);
            return catalog;
        }

        public static CampusAiMapAuthoringAssetCatalog GenerateDefaultAssetCatalog()
        {
            CampusMapEditorUtility.EnsureDirectories();

            CampusAiMapAuthoringAssetCatalog assetCatalog = AssetDatabase.LoadAssetAtPath<CampusAiMapAuthoringAssetCatalog>(DefaultCatalogPath);
            if (assetCatalog == null)
            {
                assetCatalog = ScriptableObject.CreateInstance<CampusAiMapAuthoringAssetCatalog>();
                AssetDatabase.CreateAsset(assetCatalog, DefaultCatalogPath);
            }

            assetCatalog.FloorTiles.Clear();
            assetCatalog.WallTiles.Clear();
            assetCatalog.Objects.Clear();

            AddFloorTiles(assetCatalog, AssetDatabase.LoadAssetAtPath<CampusTilePalette>(CampusMapEditorUtility.DefaultFloorPalettePath));
            AddWallTiles(assetCatalog, AssetDatabase.LoadAssetAtPath<CampusWallPalette>(CampusMapEditorUtility.DefaultWallPalettePath));
            AddPrefabs(assetCatalog, AssetDatabase.LoadAssetAtPath<CampusPrefabPalette>(CampusMapEditorUtility.DefaultPrefabPalettePath));

            EditorUtility.SetDirty(assetCatalog);
            AssetDatabase.SaveAssets();
            return assetCatalog;
        }

        public bool TryGetFloorTile(string tileId, out TileBase tile)
        {
            return floorTiles.TryGetValue(NormalizeId(tileId), out tile);
        }

        public bool TryGetWallTile(string tileId, out TileBase tile)
        {
            return wallTiles.TryGetValue(NormalizeId(tileId), out tile);
        }

        public bool TryGetObject(string objectId, out GameObject prefab)
        {
            return objects.TryGetValue(NormalizeId(objectId), out prefab);
        }

        private void RegisterExplicitCatalog(CampusAiMapAuthoringAssetCatalog assetCatalog)
        {
            if (assetCatalog == null)
            {
                return;
            }

            if (assetCatalog.FloorTiles != null)
            {
                for (int i = 0; i < assetCatalog.FloorTiles.Count; i++)
                {
                    CampusAiMapAuthoringTileEntry entry = assetCatalog.FloorTiles[i];
                    RegisterTile(entry != null ? entry.Id : string.Empty, entry != null ? entry.Tile : null, floorTiles);
                }
            }

            if (assetCatalog.WallTiles != null)
            {
                for (int i = 0; i < assetCatalog.WallTiles.Count; i++)
                {
                    CampusAiMapAuthoringTileEntry entry = assetCatalog.WallTiles[i];
                    RegisterTile(entry != null ? entry.Id : string.Empty, entry != null ? entry.Tile : null, wallTiles);
                }
            }

            if (assetCatalog.Objects != null)
            {
                for (int i = 0; i < assetCatalog.Objects.Count; i++)
                {
                    CampusAiMapAuthoringObjectEntry entry = assetCatalog.Objects[i];
                    RegisterPrefab(entry != null ? entry.Id : string.Empty, entry != null ? entry.Prefab : null);
                }
            }
        }

        private void RegisterPrefabPalette(CampusPrefabPalette palette)
        {
            if (palette == null || palette.Prefabs == null)
            {
                return;
            }

            for (int i = 0; i < palette.Prefabs.Count; i++)
            {
                RegisterPrefab(palette.Prefabs[i]);
            }
        }

        private static void AddFloorTiles(CampusAiMapAuthoringAssetCatalog target, CampusTilePalette palette)
        {
            if (palette == null || palette.FloorTiles == null)
            {
                return;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < palette.FloorTiles.Count; i++)
            {
                AddTileEntry(target.FloorTiles, "floor", palette.FloorTiles[i], ids);
            }
        }

        private static void AddWallTiles(CampusAiMapAuthoringAssetCatalog target, CampusWallPalette palette)
        {
            if (palette == null)
            {
                return;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddTileEntry(target.WallTiles, "wall", palette.HorizontalWall, ids);
            AddTileEntry(target.WallTiles, "wall", palette.VerticalWall, ids);
            AddTileEntry(target.WallTiles, "wall", palette.CornerWall, ids);
            AddTileEntry(target.WallTiles, "wall", palette.HighWall, ids);

            if (palette.WallTiles == null)
            {
                return;
            }

            for (int i = 0; i < palette.WallTiles.Count; i++)
            {
                AddTileEntry(target.WallTiles, "wall", palette.WallTiles[i], ids);
            }
        }

        private static void AddPrefabs(CampusAiMapAuthoringAssetCatalog target, CampusPrefabPalette palette)
        {
            if (palette == null || palette.Prefabs == null)
            {
                return;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < palette.Prefabs.Count; i++)
            {
                GameObject prefab = palette.Prefabs[i];
                CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
                string id = placed != null && !string.IsNullOrWhiteSpace(placed.ObjectId)
                    ? placed.ObjectId.Trim()
                    : placed != null && !string.IsNullOrWhiteSpace(placed.TypeId)
                        ? placed.TypeId.Trim()
                        : string.Empty;

                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                {
                    continue;
                }

                target.Objects.Add(new CampusAiMapAuthoringObjectEntry
                {
                    Id = id,
                    Prefab = prefab
                });
            }
        }

        private static void AddTileEntry(List<CampusAiMapAuthoringTileEntry> target, string prefix, TileBase tile, HashSet<string> ids)
        {
            if (!CampusTilePalette.IsUsableTile(tile))
            {
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile));
            string id = BuildGeneratedId(prefix, tile.name, guid);
            if (!ids.Add(id))
            {
                return;
            }

            target.Add(new CampusAiMapAuthoringTileEntry
            {
                Id = id,
                Tile = tile
            });
        }

        private void RegisterTile(string id, TileBase tile, Dictionary<string, TileBase> target)
        {
            if (!CampusTilePalette.IsUsableTile(tile))
            {
                return;
            }

            Register(target, id, tile);
            Register(target, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile)), tile);
        }

        private void RegisterPrefabsFromFolder(string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                RegisterPrefab(string.Empty, AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
        }

        private void RegisterPrefab(GameObject prefab)
        {
            RegisterPrefab(string.Empty, prefab);
        }

        private void RegisterPrefab(string explicitId, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            Register(objects, explicitId, prefab);
            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            if (placed != null)
            {
                Register(objects, placed.ObjectId, prefab);
                Register(objects, placed.TypeId, prefab);
            }

            Register(objects, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), prefab);
        }

        private static string BuildGeneratedId(string prefix, string source, string guid)
        {
            string normalized = NormalizeId(source).ToLowerInvariant();
            StringBuilder builder = new StringBuilder(prefix);
            builder.Append('.');

            bool previousSeparator = false;
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    builder.Append(c);
                    previousSeparator = false;
                    continue;
                }

                if (!previousSeparator)
                {
                    builder.Append('.');
                    previousSeparator = true;
                }
            }

            string id = builder.ToString().Trim('.');
            if (id == prefix && !string.IsNullOrWhiteSpace(guid))
            {
                return prefix + "." + guid.Substring(0, Mathf.Min(8, guid.Length));
            }

            return id;
        }

        private static void Register<T>(Dictionary<string, T> target, string id, T value)
        {
            string normalized = NormalizeId(id);
            if (string.IsNullOrEmpty(normalized) || target.ContainsKey(normalized))
            {
                return;
            }

            target.Add(normalized, value);
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }
    }
}
