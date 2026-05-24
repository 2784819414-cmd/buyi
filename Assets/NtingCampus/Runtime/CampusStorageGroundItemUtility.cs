using Nting.Storage;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    public static class CampusStorageGroundItemUtility
    {
        private const float TargetWorldIconSize = 0.28f;
        private const float MinimumWorldIconSize = 0.12f;
        private const float MaximumWorldIconSize = 0.4f;
        private const int SearchRadius = 6;

        public static bool TryDropItemToGround(GameObject sourceContext, StorageItemModel item, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (item == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.MissingItem);
                return false;
            }

            CampusFloorRoot floor = ResolveFloor(sourceContext);
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.NoValidFloorPropsRoot);
                return false;
            }

            Sprite sprite = ResolveWorldSprite(item);
            if (sprite == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.ItemHasNoGroundSprite);
                return false;
            }

            Vector3Int originCell = ResolveOriginCell(floor, sourceContext);
            if (!TryFindDropCell(floor, originCell, out Vector3Int dropCell))
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.NoFreeFloorCellNearby);
                return false;
            }

            return CreateWorldItemObject(floor, item, sprite, dropCell) != null;
        }

        public static bool TryPlaceItemAtWorldPosition(
            GameObject sourceContext,
            StorageItemModel item,
            Vector3 worldPosition,
            out string errorMessage)
        {
            return TryPlaceItemAtWorldPosition(
                sourceContext,
                item,
                worldPosition,
                out errorMessage,
                out _);
        }

        public static bool TryPlaceItemAtWorldPosition(
            GameObject sourceContext,
            StorageItemModel item,
            Vector3 worldPosition,
            out string errorMessage,
            out CampusDroppedStorageItem droppedItem)
        {
            errorMessage = string.Empty;
            droppedItem = null;
            if (item == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.MissingItem);
                return false;
            }

            CampusFloorRoot floor = ResolveFloor(sourceContext);
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.NoValidFloorPropsRoot);
                return false;
            }

            Sprite sprite = ResolveWorldSprite(item);
            if (sprite == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.ItemHasNoGroundSprite);
                return false;
            }

            Vector3Int cell = floor.Grid.WorldToCell(worldPosition);
            GameObject worldItem = CreateWorldItemObject(floor, item, sprite, cell, worldPosition, true);
            droppedItem = worldItem != null ? worldItem.GetComponent<CampusDroppedStorageItem>() : null;
            return worldItem != null;
        }

        private static GameObject CreateWorldItemObject(CampusFloorRoot floor, StorageItemModel item, Sprite sprite, Vector3Int cell)
        {
            return CreateWorldItemObject(floor, item, sprite, cell, Vector3.zero, false);
        }

        private static GameObject CreateWorldItemObject(
            CampusFloorRoot floor,
            StorageItemModel item,
            Sprite sprite,
            Vector3Int cell,
            Vector3 exactWorldPosition,
            bool useExactWorldPosition)
        {
            GameObject worldItem = new GameObject(BuildObjectName(item));
            worldItem.transform.SetParent(floor.PropsRoot, false);
            CampusSceneInstanceUtility.NormalizeSceneInstance(worldItem);

            SpriteRenderer renderer = worldItem.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderer.maskInteraction = SpriteMaskInteraction.None;

            CampusDroppedStorageItem droppedItem = worldItem.AddComponent<CampusDroppedStorageItem>();
            droppedItem.DefinitionId = item.DefinitionId;
            droppedItem.InstanceId = item.InstanceId;
            droppedItem.DisplayName = item.DisplayName;
            droppedItem.LocalizedDisplayName = item.LocalizedDisplayName;
            droppedItem.Width = item.CurrentWidth;
            droppedItem.Height = item.CurrentHeight;
            droppedItem.Weight = item.Weight;
            droppedItem.Price = item.Price;
            droppedItem.Description = item.Description;
            droppedItem.LocalizedDescription = item.LocalizedDescription;
            droppedItem.ThemeColor = item.ThemeColor;
            droppedItem.IsUsable = item.IsUsable;
            droppedItem.UseActionId = item.UseActionId;
            droppedItem.ConsumeOnUse = item.ConsumeOnUse;
            droppedItem.UseText = item.UseText;
            droppedItem.LocalizedUseText = item.LocalizedUseText;
            droppedItem.LegalState = item.LegalState;
            droppedItem.OwnerId = item.OwnerId;
            droppedItem.SourceContainerId = item.SourceContainerId;
            droppedItem.SourceRoomId = item.SourceRoomId;
            droppedItem.SourceLocation = item.SourceLocation;
            droppedItem.StolenDuringSession = item.StolenDuringSession;
            droppedItem.SuspicionRisk = item.SuspicionRisk;

            CircleCollider2D interactionCollider = worldItem.AddComponent<CircleCollider2D>();
            interactionCollider.isTrigger = true;
            interactionCollider.radius = 0.24f;

            CampusPlacedObject placed = worldItem.AddComponent<CampusPlacedObject>();
            placed.FloorIndex = floor.FloorIndex;
            placed.ObjectId = BuildObjectId(item);
            // Inventory items own their identity on CampusDroppedStorageItem, not on facility TypeId.
            placed.TypeId = string.Empty;
            placed.DisplayNameOverride = item.DisplayName;
            placed.LocalizedDisplayNameOverride = item.LocalizedDisplayName;
            placed.Cell = cell;
            placed.FootprintSize = Vector2Int.one;
            placed.OverrideFootprintSize = true;
            placed.VisualScale = ResolveWorldVisualScale(sprite);
            placed.LockVisualScaleAspect = true;
            placed.OverrideAllowRotation = true;
            placed.AllowRotation = false;
            placed.Rotation90 = 0;
            placed.SortingOrderOffset = CampusPlacedObject.DroppedStorageItemSortingOrderOffset;
            placed.BlocksMovement = false;
            placed.BlocksSight = false;
            placed.IsInteractable = true;
            placed.IsStorageContainer = false;
            placed.UseCustomInteractionAnchor = false;
            placed.ApplyPlacementRotation(0);
            if (useExactWorldPosition)
            {
                worldItem.transform.position = exactWorldPosition;
            }
            else
            {
                placed.ApplyCellToTransform(floor.Grid);
            }

            placed.ApplyVisualScaleState();
            if (useExactWorldPosition)
            {
                worldItem.transform.position = exactWorldPosition;
            }

            CampusRenderSortingUtility.ApplyObjectSortingGroups(
                floor.PropsRoot,
                ResolveSortingLayerId(floor),
                CampusRenderSortingUtility.SharedWallObjectOffset);

            placed.EnsureShadowRegistration();
            return worldItem;
        }

        private static CampusFloorRoot ResolveFloor(GameObject sourceContext)
        {
            if (sourceContext != null)
            {
                CampusFloorRoot sourceFloor = sourceContext.GetComponentInParent<CampusFloorRoot>(true);
                if (sourceFloor != null)
                {
                    return sourceFloor;
                }
            }

            return Object.FindFirstObjectByType<CampusFloorRoot>(FindObjectsInactive.Include);
        }

        private static Vector3Int ResolveOriginCell(CampusFloorRoot floor, GameObject sourceContext)
        {
            if (floor == null || floor.Grid == null)
            {
                return Vector3Int.zero;
            }

            if (sourceContext != null)
            {
                CampusPlacedObject placedObject = sourceContext.GetComponent<CampusPlacedObject>();
                if (placedObject != null)
                {
                    return placedObject.Cell;
                }

                return floor.Grid.WorldToCell(sourceContext.transform.position);
            }

            return floor.Grid.WorldToCell(floor.transform.position);
        }

        private static bool TryFindDropCell(CampusFloorRoot floor, Vector3Int originCell, out Vector3Int cell)
        {
            cell = originCell;
            if (IsDropCellAvailable(floor, originCell))
            {
                return true;
            }

            for (int radius = 1; radius <= SearchRadius; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        {
                            continue;
                        }

                        Vector3Int candidate = new Vector3Int(originCell.x + dx, originCell.y + dy, originCell.z);
                        if (!IsDropCellAvailable(floor, candidate))
                        {
                            continue;
                        }

                        cell = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsDropCellAvailable(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.Grid == null || floor.FloorTilemap == null)
            {
                return false;
            }

            if (!floor.FloorTilemap.HasTile(cell))
            {
                return false;
            }

            Tilemap wallLogic = floor.WallLogicTilemap != null ? floor.WallLogicTilemap : floor.WallTilemap;
            if (wallLogic != null && wallLogic.HasTile(cell))
            {
                return false;
            }

            if (floor.PropsRoot != null)
            {
                CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
                for (int i = 0; i < objects.Length; i++)
                {
                    if (objects[i] != null && objects[i].ContainsCell(cell))
                    {
                        return false;
                    }
                }
            }

            if (floor.StairsRoot != null)
            {
                CampusStairLink[] stairs = floor.StairsRoot.GetComponentsInChildren<CampusStairLink>(true);
                for (int i = 0; i < stairs.Length; i++)
                {
                    if (stairs[i] != null && stairs[i].ContainsCell(cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static Sprite ResolveWorldSprite(StorageItemModel item)
        {
            Sprite itemIcon = StorageItemIconUtility.Resolve(item);
            if (itemIcon != null)
            {
                return itemIcon;
            }

            StorageMemory memory = StorageMemory.Instance;
            if (memory != null &&
                memory.ItemRegistry != null &&
                memory.ItemRegistry.TryGetDefinition(item != null ? item.DefinitionId : string.Empty, out StorageItemDefinition definition) &&
                definition != null)
            {
                return definition.Icon;
            }

            return null;
        }

        private static Vector2 ResolveWorldVisualScale(Sprite sprite)
        {
            if (sprite == null)
            {
                return new Vector2(0.25f, 0.25f);
            }

            Vector2 spriteSize = sprite.bounds.size;
            float maxDimension = Mathf.Max(spriteSize.x, spriteSize.y);
            if (maxDimension <= 0.0001f)
            {
                return new Vector2(0.25f, 0.25f);
            }

            float scale = Mathf.Clamp(TargetWorldIconSize / maxDimension, 0.05f, 16f);
            float worldSize = maxDimension * scale;
            if (worldSize < MinimumWorldIconSize)
            {
                scale *= MinimumWorldIconSize / worldSize;
            }
            else if (worldSize > MaximumWorldIconSize)
            {
                scale *= MaximumWorldIconSize / worldSize;
            }

            return new Vector2(scale, scale);
        }

        private static int ResolveSortingLayerId(CampusFloorRoot floor)
        {
            if (floor != null)
            {
                int layerId = ResolveSortingLayerId(floor.WallFaceTilemap);
                if (layerId != 0)
                {
                    return layerId;
                }

                layerId = ResolveSortingLayerId(floor.WallCapTilemap);
                if (layerId != 0)
                {
                    return layerId;
                }
            }

            return SortingLayer.NameToID("Default");
        }

        private static int ResolveSortingLayerId(Component component)
        {
            Renderer renderer = component != null ? component.GetComponent<Renderer>() : null;
            return renderer != null ? renderer.sortingLayerID : 0;
        }

        private static string BuildObjectName(StorageItemModel item)
        {
            string displayName = item != null
                ? item.GetDisplayName()
                : CampusInteractionTextCatalog.Get(CampusInteractionTextId.DroppedItem);
            return "GroundItem_" + displayName;
        }

        private static string BuildObjectId(StorageItemModel item)
        {
            string seed = item != null && !string.IsNullOrWhiteSpace(item.DefinitionId)
                ? item.DefinitionId
                : (item != null ? item.DisplayName : "ground_item");
            return "ground_item_" + SanitizeId(seed);
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "item";
            }

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            string result = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(result) ? "item" : result;
        }
    }
}
