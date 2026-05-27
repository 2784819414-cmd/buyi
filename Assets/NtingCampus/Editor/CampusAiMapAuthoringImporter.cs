using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    internal static class CampusAiMapAuthoringImporter
    {
        public static void ImportFromMenu()
        {
            string path = EditorUtility.OpenFilePanel(
                CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.SelectJsonTitle),
                string.Empty,
                CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.JsonExtension));

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ImportFromPath(path);
        }

        public static void GenerateCatalogFromMenu()
        {
            CampusAiMapAuthoringAssetCatalog catalog = CampusAiMapAuthoringCatalog.GenerateDefaultAssetCatalog();
            string summary = CampusAiMapAuthoringTextCatalog.Format(
                CampusAiMapAuthoringTextId.GenerateCatalogSummary,
                catalog.FloorTiles.Count,
                catalog.WallTiles.Count,
                catalog.Objects.Count);
            Debug.Log(summary);
            EditorUtility.DisplayDialog(
                CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.ImportMenuTitle),
                summary,
                CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.DialogOk));
        }

        public static bool ImportFromPath(string path)
        {
            CampusAiMapAuthoringDocument document = JsonUtility.FromJson<CampusAiMapAuthoringDocument>(File.ReadAllText(path));
            CampusAiMapAuthoringCatalog catalog = CampusAiMapAuthoringCatalog.BuildDefault();
            List<string> errors = Validate(document, catalog);
            if (errors.Count > 0)
            {
                string message = string.Join(Environment.NewLine, errors);
                Debug.LogError(CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.ValidationErrorPrefix) + message);
                EditorUtility.DisplayDialog(
                    CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.ImportFailedTitle),
                    message,
                    CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.DialogOk));
                return false;
            }

            CampusMapData mapData = ScriptableObject.CreateInstance<CampusMapData>();
            BuildMapData(document, catalog, mapData);

            CampusMapRoot root = CampusMapEditorUtility.FindOrCreateCampusMapRoot();
            ClearExistingFloors(root);
            CampusMapEditorUtility.LoadMapData(mapData, root);
            CampusMapEditorUtility.RebuildAllWallVisuals(root, CampusMapEditorUtility.LoadDefaultWallRenderProfile());
            CampusMapEditorUtility.RunValidation(root, CampusMapEditorUtility.LoadDefaultFloorPalette(), CampusMapEditorUtility.LoadDefaultWallPalette(), CampusMapEditorUtility.LoadDefaultPrefabPalette());
            CampusMapEditorUtility.MarkSceneDirty();

            string summary = CampusAiMapAuthoringTextCatalog.Format(
                CampusAiMapAuthoringTextId.AppliedSummary,
                mapData.MapId,
                mapData.Floors.Count);
            Debug.Log(summary);
            EditorUtility.DisplayDialog(
                CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.ImportSuccessTitle),
                summary,
                CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.DialogOk));
            return true;
        }

        private static void ClearExistingFloors(CampusMapRoot root)
        {
            if (root == null)
            {
                return;
            }

            root.RebuildFloorReferences();
            if (root.Floors == null)
            {
                return;
            }

            for (int i = root.Floors.Count - 1; i >= 0; i--)
            {
                CampusFloorRoot floor = root.Floors[i];
                if (floor != null)
                {
                    Undo.DestroyObjectImmediate(floor.gameObject);
                }
            }

            root.RebuildFloorReferences();
        }

        private static List<string> Validate(CampusAiMapAuthoringDocument document, CampusAiMapAuthoringCatalog catalog)
        {
            List<string> errors = new List<string>();
            if (document == null)
            {
                errors.Add(CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.ValidationNoMap));
                return errors;
            }

            if (string.IsNullOrWhiteSpace(document.MapId))
            {
                errors.Add(CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.ValidationMapIdMissing));
            }

            if (document.Floors == null || document.Floors.Count == 0)
            {
                errors.Add(CampusAiMapAuthoringTextCatalog.Get(CampusAiMapAuthoringTextId.ValidationNoFloors));
                return errors;
            }

            for (int i = 0; i < document.Floors.Count; i++)
            {
                ValidateFloor(document.Floors[i], catalog, errors);
            }

            return errors;
        }

        private static void ValidateFloor(CampusAiMapAuthoringFloor floor, CampusAiMapAuthoringCatalog catalog, List<string> errors)
        {
            if (floor == null || floor.FloorIndex <= 0)
            {
                errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationFloorIndexInvalid, floor != null ? floor.FloorIndex : 0));
                return;
            }

            ValidateRooms(floor, catalog, errors);
            ValidateTileStamps(floor.FloorIndex, floor.FloorTiles, true, catalog, errors);
            ValidateTileStamps(floor.FloorIndex, floor.WallTiles, false, catalog, errors);
            ValidateObjects(floor, catalog, errors);
            ValidateStairs(floor, errors);
        }

        private static void ValidateRooms(CampusAiMapAuthoringFloor floor, CampusAiMapAuthoringCatalog catalog, List<string> errors)
        {
            if (floor.Rooms == null)
            {
                return;
            }

            for (int i = 0; i < floor.Rooms.Count; i++)
            {
                CampusAiMapAuthoringRoom room = floor.Rooms[i];
                if (room == null || string.IsNullOrWhiteSpace(room.Id))
                {
                    errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationRoomIdMissing, floor.FloorIndex));
                    continue;
                }

                if (room.Rect.Width <= 0 || room.Rect.Height <= 0)
                {
                    errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationRoomSizeInvalid, floor.FloorIndex, room.Id));
                }

                ValidateTileId(floor.FloorIndex, room.Id, room.FloorTileId, true, catalog, errors);
                ValidateTileId(floor.FloorIndex, room.Id, room.WallTileId, false, catalog, errors);
            }
        }

        private static void ValidateTileStamps(int floorIndex, List<CampusAiMapAuthoringTileStamp> stamps, bool floorTile, CampusAiMapAuthoringCatalog catalog, List<string> errors)
        {
            if (stamps == null)
            {
                return;
            }

            for (int i = 0; i < stamps.Count; i++)
            {
                CampusAiMapAuthoringTileStamp stamp = stamps[i];
                ValidateTileId(floorIndex, i.ToString(), stamp != null ? stamp.TileId : string.Empty, floorTile, catalog, errors);
            }
        }

        private static void ValidateTileId(int floorIndex, string owner, string tileId, bool floorTile, CampusAiMapAuthoringCatalog catalog, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(tileId))
            {
                errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationTileIdMissing, floorIndex, owner));
                return;
            }

            bool found = floorTile ? catalog.TryGetFloorTile(tileId, out _) : catalog.TryGetWallTile(tileId, out _);
            if (!found)
            {
                errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationTileIdUnknown, floorIndex, owner, tileId));
            }
        }

        private static void ValidateObjects(CampusAiMapAuthoringFloor floor, CampusAiMapAuthoringCatalog catalog, List<string> errors)
        {
            if (floor.Objects == null)
            {
                return;
            }

            for (int i = 0; i < floor.Objects.Count; i++)
            {
                CampusAiMapAuthoringObject authoredObject = floor.Objects[i];
                string objectId = authoredObject != null ? authoredObject.ObjectId : string.Empty;
                if (string.IsNullOrWhiteSpace(objectId))
                {
                    errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationObjectIdMissing, floor.FloorIndex));
                    continue;
                }

                if (!catalog.TryGetObject(objectId, out _))
                {
                    errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationObjectIdUnknown, floor.FloorIndex, objectId));
                }
            }
        }

        private static void ValidateStairs(CampusAiMapAuthoringFloor floor, List<string> errors)
        {
            if (floor.Stairs == null)
            {
                return;
            }

            for (int i = 0; i < floor.Stairs.Count; i++)
            {
                CampusAiMapAuthoringStair stair = floor.Stairs[i];
                if (stair == null)
                {
                    errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationStairCellMissing, floor.FloorIndex));
                    continue;
                }

                if (stair.ToFloor <= 0 || stair.ToFloor == floor.FloorIndex)
                {
                    errors.Add(CampusAiMapAuthoringTextCatalog.Format(CampusAiMapAuthoringTextId.ValidationStairTargetInvalid, floor.FloorIndex));
                }
            }
        }

        private static void BuildMapData(CampusAiMapAuthoringDocument document, CampusAiMapAuthoringCatalog catalog, CampusMapData output)
        {
            output.MapId = document.MapId.Trim();
            output.Floors.Clear();

            for (int i = 0; i < document.Floors.Count; i++)
            {
                CampusAiMapAuthoringFloor sourceFloor = document.Floors[i];
                CampusFloorData floorData = new CampusFloorData
                {
                    FloorIndex = sourceFloor.FloorIndex,
                    IsUnlocked = sourceFloor.IsUnlocked
                };

                AddRooms(sourceFloor, catalog, floorData);
                AddTileStamps(sourceFloor.FloorTiles, true, catalog, floorData);
                AddTileStamps(sourceFloor.WallTiles, false, catalog, floorData);
                AddObjects(sourceFloor, catalog, floorData);
                AddStairs(sourceFloor, floorData);
                output.Floors.Add(floorData);
            }
        }

        private static void AddRooms(CampusAiMapAuthoringFloor sourceFloor, CampusAiMapAuthoringCatalog catalog, CampusFloorData floorData)
        {
            if (sourceFloor.Rooms == null)
            {
                return;
            }

            for (int i = 0; i < sourceFloor.Rooms.Count; i++)
            {
                CampusAiMapAuthoringRoom room = sourceFloor.Rooms[i];
                catalog.TryGetFloorTile(room.FloorTileId, out TileBase floorTile);
                catalog.TryGetWallTile(room.WallTileId, out TileBase wallTile);

                for (int x = room.Rect.X; x < room.Rect.X + room.Rect.Width; x++)
                {
                    for (int y = room.Rect.Y; y < room.Rect.Y + room.Rect.Height; y++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        floorData.FloorTiles.Add(CreateTileData(cell, floorTile, 1, 0, false, false));
                        if (IsRoomPerimeter(room.Rect, x, y))
                        {
                            floorData.WallTiles.Add(CreateTileData(cell, wallTile, 1, 0, false, false));
                        }
                    }
                }
            }
        }

        private static void AddTileStamps(List<CampusAiMapAuthoringTileStamp> stamps, bool floorTile, CampusAiMapAuthoringCatalog catalog, CampusFloorData floorData)
        {
            if (stamps == null)
            {
                return;
            }

            for (int i = 0; i < stamps.Count; i++)
            {
                CampusAiMapAuthoringTileStamp stamp = stamps[i];
                TileBase tile;
                if (floorTile)
                {
                    catalog.TryGetFloorTile(stamp.TileId, out tile);
                    floorData.FloorTiles.Add(CreateTileData(stamp.Cell.ToVector3Int(), tile, stamp.Size, stamp.Rotation90, stamp.FlipX, stamp.FlipY));
                }
                else
                {
                    catalog.TryGetWallTile(stamp.TileId, out tile);
                    floorData.WallTiles.Add(CreateTileData(stamp.Cell.ToVector3Int(), tile, stamp.Size, stamp.Rotation90, stamp.FlipX, stamp.FlipY));
                }
            }
        }

        private static void AddObjects(CampusAiMapAuthoringFloor sourceFloor, CampusAiMapAuthoringCatalog catalog, CampusFloorData floorData)
        {
            if (sourceFloor.Objects == null)
            {
                return;
            }

            for (int i = 0; i < sourceFloor.Objects.Count; i++)
            {
                CampusAiMapAuthoringObject authoredObject = sourceFloor.Objects[i];
                catalog.TryGetObject(authoredObject.ObjectId, out GameObject prefab);
                CampusPlacedObject prefabPlaced = prefab.GetComponent<CampusPlacedObject>();

                floorData.Objects.Add(new CampusPlacedObjectData
                {
                    ObjectId = prefabPlaced != null && !string.IsNullOrWhiteSpace(prefabPlaced.ObjectId) ? prefabPlaced.ObjectId.Trim() : authoredObject.ObjectId.Trim(),
                    ObjectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)),
                    TypeId = !string.IsNullOrWhiteSpace(authoredObject.TypeId)
                        ? authoredObject.TypeId.Trim()
                        : prefabPlaced != null ? prefabPlaced.TypeId : string.Empty,
                    Cell = authoredObject.Cell.ToVector3Int(),
                    FloorIndex = sourceFloor.FloorIndex,
                    FootprintSize = CampusPlacedObject.NormalizeFootprintSize(authoredObject.FootprintSize),
                    OverrideFootprintSize = authoredObject.OverrideFootprintSize,
                    VisualScale = CampusPlacedObject.NormalizeVisualScale(authoredObject.VisualScale),
                    LockVisualScaleAspect = authoredObject.LockVisualScaleAspect,
                    Rotation90 = CampusPlacedObject.NormalizeRotation90(authoredObject.Rotation90),
                    Position = Vector3.zero
                });
            }
        }

        private static void AddStairs(CampusAiMapAuthoringFloor sourceFloor, CampusFloorData floorData)
        {
            if (sourceFloor.Stairs == null)
            {
                return;
            }

            for (int i = 0; i < sourceFloor.Stairs.Count; i++)
            {
                CampusAiMapAuthoringStair authoredStair = sourceFloor.Stairs[i];
                int rotation90 = CampusPlacedObject.NormalizeRotation90(authoredStair.Rotation90);
                Vector3Int fromCell = authoredStair.FromCell.ToVector3Int();
                Vector3Int secondaryCell = fromCell + CampusStairLink.DirectionFromRotation(rotation90);

                floorData.Stairs.Add(new CampusStairData
                {
                    FromFloor = sourceFloor.FloorIndex,
                    ToFloor = authoredStair.ToFloor,
                    FromCell = fromCell,
                    ToCell = secondaryCell,
                    SecondaryCell = secondaryCell,
                    Rotation90 = rotation90,
                    LinkId = string.IsNullOrWhiteSpace(authoredStair.LinkId) ? Guid.NewGuid().ToString("N") : authoredStair.LinkId.Trim(),
                    IsAutoReturnStair = false
                });
            }
        }

        private static CampusTileCellData CreateTileData(Vector3Int cell, TileBase tile, int size, int rotation90, bool flipX, bool flipY)
        {
            return new CampusTileCellData
            {
                Cell = cell,
                TileId = tile.name,
                TileGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile)),
                Size = Mathf.Clamp(size <= 0 ? 1 : size, 1, 3),
                Rotation90 = CampusPlacedObject.NormalizeRotation90(rotation90),
                FlipX = flipX,
                FlipY = flipY,
                Transform = BuildTileTransform(size, rotation90, flipX, flipY)
            };
        }

        private static Matrix4x4 BuildTileTransform(int tileSize, int rotation90, bool flipX, bool flipY)
        {
            int size = Mathf.Clamp(tileSize <= 0 ? 1 : tileSize, 1, 3);
            Vector3 offset = new Vector3((size - 1) * 0.5f, (size - 1) * 0.5f, 0f);
            Vector3 scale = new Vector3((flipX ? -1f : 1f) * size, (flipY ? -1f : 1f) * size, 1f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, CampusPlacedObject.NormalizeRotation90(rotation90) * 90f);
            return Matrix4x4.TRS(offset, rotation, scale);
        }

        private static bool IsRoomPerimeter(CampusAiMapAuthoringRect rect, int x, int y)
        {
            return x == rect.X ||
                   y == rect.Y ||
                   x == rect.X + rect.Width - 1 ||
                   y == rect.Y + rect.Height - 1;
        }
    }
}
