using Nting.Storage;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public sealed class CampusSimpleInteractable : CampusInteractionPromptSource, ICampusInteractable, ICampusInteractionActionHandler
    {
        public bool LogInteraction = true;
        public string InteractionLogMessage;

        [Header("Unified Action")]
        public string DefaultActionId;

        public void Interact(GameObject actor)
        {
            string actionId = ResolveDefaultActionId(null);
            if (!TryHandleInteractionAction(null, actionId, null, actor))
            {
                LogDefaultInteraction(actor);
            }
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            string resolvedActionId = CampusInteractionActionIds.Normalize(actionId);
            if (string.IsNullOrEmpty(resolvedActionId))
            {
                resolvedActionId = ResolveDefaultActionId(anchor);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.OpenStorage))
            {
                return TryOpenStorageWindow(payload);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.InteractTarget))
            {
                return TryInteractTarget(anchor, actor);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.Log))
            {
                LogDefaultInteraction(actor);
                return true;
            }

            return false;
        }

        private string ResolveDefaultActionId(CampusInteractionAnchor anchor)
        {
            if (!string.IsNullOrWhiteSpace(DefaultActionId))
            {
                return DefaultActionId;
            }

            if (anchor != null && anchor.InteractionTarget is ICampusInteractable target && !ReferenceEquals(target, this))
            {
                return CampusInteractionActionIds.InteractTarget;
            }

            return CampusInteractionActionIds.Log;
        }

        private bool TryOpenStorageWindow(string payload)
        {
            StorageMemory memory = StorageMemory.GetOrCreate();
            StoragePlayerInventoryUtility.EnsureRegistry(memory);
            StorageContainerModel[] hands = StoragePlayerInventoryUtility.GetOrCreateHandContainers(memory);
            StorageContainerModel[] pockets = StoragePlayerInventoryUtility.GetOrCreatePocketContainers(memory);
            StorageContainerModel backpack = StoragePlayerInventoryUtility.GetOrCreateBackpack(memory);
            StoragePlayerInventoryUtility.EnsureStarterItems(memory);
            string containerId = ResolveStorageContainerId(payload);
            Vector2Int storageSize = ResolveObjectStorageSize();
            StorageContainerModel container = memory.GetOrCreateContainer(
                containerId,
                ResolveObjectDisplayName(),
                storageSize.x,
                storageSize.y,
                ResolveObjectStorageMaxWeight());
            ConfigureObjectStorageContainer(container);

            StorageWindowUI window = FindFirstObjectByType<StorageWindowUI>();
            if (window == null)
            {
                GameObject windowObject = new GameObject("Canvas_Storage", typeof(RectTransform), typeof(StorageWindowUI));
                window = windowObject.GetComponent<StorageWindowUI>();
            }

            window.SetGroundDropContext(gameObject);
            window.OpenPlayerStorage(hands, pockets, backpack, true, container, false);
            return true;
        }

        private void ConfigureObjectStorageContainer(StorageContainerModel container)
        {
            if (container == null)
            {
                return;
            }

            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            CampusGameplayRoom room = ResolveGameplayRoom(placedObject);
            container.AccessPolicy = StorageContainerAccessPolicy.ProtectedPublic;
            container.OwnerId = placedObject != null && !string.IsNullOrWhiteSpace(placedObject.ObjectId)
                ? placedObject.ObjectId
                : gameObject.name;
            container.OwnerRole = "Campus";
            container.RoomId = room != null ? room.RoomId : string.Empty;
            container.AllowTakingContents = false;
            container.IsPlayerCarried = false;
            container.SuspicionRisk = ResolveStorageSuspicionRisk(room);
        }

        private CampusGameplayRoom ResolveGameplayRoom(CampusPlacedObject placedObject)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            if (worldService == null)
            {
                return null;
            }

            int floorIndex = placedObject != null ? placedObject.FloorIndex : 1;
            return worldService.FindRoomForPosition(floorIndex, transform.position);
        }

        private static int ResolveStorageSuspicionRisk(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return 2;
            }

            switch (room.RoomType)
            {
                case CampusRoomType.Office:
                    return 12;
                case CampusRoomType.Canteen:
                    return 8;
                case CampusRoomType.Store:
                    return 8;
                case CampusRoomType.Classroom:
                    return 5;
                case CampusRoomType.Dormitory:
                    return 4;
                default:
                    return 3;
            }
        }

        private Vector2Int ResolveObjectStorageSize()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (placedObject == null)
            {
                return CampusPlacedObject.DefaultStorageSize;
            }

            placedObject.NormalizeStorageSettings();
            return placedObject.NormalizedStorageSize;
        }

        private float ResolveObjectStorageMaxWeight()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (placedObject == null)
            {
                return CampusPlacedObject.DefaultStorageMaxWeight;
            }

            placedObject.NormalizeStorageSettings();
            return placedObject.NormalizedStorageMaxWeight;
        }

        private bool TryInteractTarget(CampusInteractionAnchor anchor, GameObject actor)
        {
            if (anchor == null || !(anchor.InteractionTarget is ICampusInteractable target) || ReferenceEquals(target, this))
            {
                return false;
            }

            target.Interact(actor);
            return true;
        }

        private void LogDefaultInteraction(GameObject actor)
        {
            if (!LogInteraction)
            {
                return;
            }

            string actorName = actor != null ? actor.name : "UnknownActor";
            string message = string.IsNullOrWhiteSpace(InteractionLogMessage)
                ? actorName + " interacted with " + ResolveObjectDisplayName()
                : InteractionLogMessage;

            Debug.Log(message, this);
        }

        private string ResolveStorageContainerId(string payload)
        {
            if (!string.IsNullOrWhiteSpace(payload))
            {
                return "object_storage_" + SanitizeStorageId(payload);
            }

            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            Vector3 position = transform.position;
            int px = Mathf.RoundToInt(position.x * 100f);
            int py = Mathf.RoundToInt(position.y * 100f);
            int pz = Mathf.RoundToInt(position.z * 100f);

            if (placedObject != null)
            {
                Vector3Int cell = placedObject.Cell;
                string objectId = string.IsNullOrWhiteSpace(placedObject.ObjectId) ? gameObject.name : placedObject.ObjectId;
                return "object_storage_" + SanitizeStorageId(objectId) +
                       "_f" + placedObject.FloorIndex +
                       "_c" + cell.x + "_" + cell.y + "_" + cell.z +
                       "_p" + px + "_" + py + "_" + pz;
            }

            return "object_storage_" + SanitizeStorageId(gameObject.name) + "_p" + px + "_" + py + "_" + pz + "_" + gameObject.GetInstanceID();
        }

        private string ResolveObjectDisplayName()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (placedObject != null)
            {
                return placedObject.DisplayName;
            }

            return CampusObjectNames.GetDisplayName(gameObject.name);
        }

        private static string SanitizeStorageId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
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
            return string.IsNullOrEmpty(result) ? "unnamed" : result;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CampusDroppedStorageItem : MonoBehaviour, ICampusInteractable, ICampusInteractionActionHandler, ICampusInteractionPromptProvider
    {
        public string DefinitionId;
        public string InstanceId;
        public string DisplayName;
        public int Width = 1;
        public int Height = 1;
        public float Weight;
        [TextArea]
        public string Description;
        public Color ThemeColor = new Color(0.38f, 0.49f, 0.56f, 1f);
        public bool IsUsable;
        public string UseActionId;
        public bool ConsumeOnUse = true;
        public string UseText;
        public StorageItemLegalState LegalState;
        public string OwnerId;
        public string SourceContainerId;
        public string SourceRoomId;
        public string SourceLocation;
        public bool StolenDuringSession;
        public int SuspicionRisk;

        public void Interact(GameObject actor)
        {
            TryPickup(actor, out _);
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            string normalized = CampusInteractionActionIds.Normalize(actionId);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !CampusInteractionActionIds.Equals(normalized, CampusInteractionActionIds.PickupStorageItem) &&
                !CampusInteractionActionIds.Equals(normalized, CampusInteractionActionIds.InteractTarget))
            {
                return false;
            }

            return TryPickup(actor, out _);
        }

        public bool TryGetInteractionPrompt(GameObject actor, out CampusInteractionPromptData prompt)
        {
            string itemName = string.IsNullOrWhiteSpace(DisplayName) ? DefinitionId : DisplayName;
            prompt = CampusInteractionPromptData.Create("拾取 " + itemName);
            prompt.Anchor = transform;
            prompt.WorldOffset = new Vector3(0f, 0.38f, 0f);
            prompt.Priority = 140;
            prompt.IsAvailable = true;
            return true;
        }

        private bool TryPickup(GameObject actor, out string errorMessage)
        {
            errorMessage = string.Empty;
            StorageMemory memory = StorageMemory.GetOrCreate();
            StoragePlayerInventoryUtility.EnsureRegistry(memory);
            StorageItemModel item = BuildStorageItem(memory);
            if (item == null)
            {
                errorMessage = "Could not rebuild dropped item.";
                return false;
            }

            StorageTransferContext context = StorageTransferContext.ForActor(actor, StorageTransferReason.Pickup);
            context.AllowProtectedTake = true;
            context.SourceLocation = SourceLocation;
            context.OwnerId = OwnerId;
            CampusInventoryTransferService service = CampusInventoryTransferService.Resolve();
            if (!service.TryPlaceInCarriedStorage(memory, item, context, out StorageTransferResult result))
            {
                errorMessage = result.Message;
                WritePickupLog(result.Message, true);
                return false;
            }

            WritePickupLog(result.Message, false);
            Destroy(gameObject);
            return true;
        }

        private StorageItemModel BuildStorageItem(StorageMemory memory)
        {
            StorageItemModel item = null;
            if (memory != null &&
                memory.ItemRegistry != null &&
                !string.IsNullOrWhiteSpace(DefinitionId))
            {
                item = memory.ItemRegistry.CreateItem(DefinitionId, InstanceId);
            }

            if (item == null)
            {
                item = new StorageItemModel
                {
                    Id = string.IsNullOrWhiteSpace(InstanceId) ? DefinitionId : InstanceId,
                    InstanceId = string.IsNullOrWhiteSpace(InstanceId) ? DefinitionId : InstanceId,
                    DefinitionId = DefinitionId
                };
            }

            item.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? DefinitionId : DisplayName;
            item.Width = Mathf.Max(1, Width);
            item.Height = Mathf.Max(1, Height);
            item.Weight = Weight;
            item.Description = Description;
            item.ThemeColor = ThemeColor;
            item.IsUsable = IsUsable;
            item.UseActionId = UseActionId;
            item.ConsumeOnUse = ConsumeOnUse;
            item.UseText = UseText;
            item.LegalState = LegalState == StorageItemLegalState.Unknown ? StorageItemLegalState.Personal : LegalState;
            item.OwnerId = OwnerId;
            item.SourceContainerId = SourceContainerId;
            item.SourceRoomId = SourceRoomId;
            item.SourceLocation = SourceLocation;
            item.StolenDuringSession = StolenDuringSession;
            item.SuspicionRisk = SuspicionRisk;
            item.AllowTaking = !item.IsStolenEvidence;
            return item;
        }

        private static void WritePickupLog(string message, bool warning)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog((warning ? "[Pickup] " : "[Pickup] ") + message);
            }
        }
    }

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
                errorMessage = "Missing item.";
                return false;
            }

            CampusFloorRoot floor = ResolveFloor(sourceContext);
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                errorMessage = "No valid floor props root was found.";
                return false;
            }

            Sprite sprite = ResolveWorldSprite(item);
            if (sprite == null)
            {
                errorMessage = "Item has no ground sprite.";
                return false;
            }

            Vector3Int originCell = ResolveOriginCell(floor, sourceContext);
            if (!TryFindDropCell(floor, originCell, out Vector3Int dropCell))
            {
                errorMessage = "No free floor cell nearby.";
                return false;
            }

            return CreateWorldItemObject(floor, item, sprite, dropCell) != null;
        }

        private static GameObject CreateWorldItemObject(CampusFloorRoot floor, StorageItemModel item, Sprite sprite, Vector3Int cell)
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
            droppedItem.Width = item.CurrentWidth;
            droppedItem.Height = item.CurrentHeight;
            droppedItem.Weight = item.Weight;
            droppedItem.Description = item.Description;
            droppedItem.ThemeColor = item.ThemeColor;
            droppedItem.IsUsable = item.IsUsable;
            droppedItem.UseActionId = item.UseActionId;
            droppedItem.ConsumeOnUse = item.ConsumeOnUse;
            droppedItem.UseText = item.UseText;
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
            placed.TypeId = item != null && !string.IsNullOrWhiteSpace(item.DefinitionId)
                ? item.DefinitionId.Trim()
                : string.Empty;
            placed.DisplayNameOverride = item.DisplayName;
            placed.Cell = cell;
            placed.FootprintSize = Vector2Int.one;
            placed.OverrideFootprintSize = true;
            placed.VisualScale = ResolveWorldVisualScale(sprite);
            placed.LockVisualScaleAspect = true;
            placed.OverrideAllowRotation = true;
            placed.AllowRotation = false;
            placed.Rotation90 = 0;
            placed.SortingOrderOffset = 0;
            placed.BlocksMovement = false;
            placed.BlocksSight = false;
            placed.IsInteractable = true;
            placed.IsStorageContainer = false;
            placed.UseCustomInteractionAnchor = false;
            placed.ApplyPlacementRotation(0);
            placed.ApplyCellToTransform(floor.Grid);
            placed.ApplyVisualScaleState();

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
            if (item != null && item.Icon != null)
            {
                return item.Icon;
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
            string displayName = item != null && !string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.DisplayName.Trim()
                : "DroppedItem";
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
