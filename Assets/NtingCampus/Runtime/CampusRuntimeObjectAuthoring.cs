using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Retail;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeObjectAuthoring
    {
        internal static CampusRuntimeObjectSettings CaptureSettings(
            GameObject prefab,
            CampusPlacedObject placed,
            string importRoot,
            string defaultInteractionPrompt)
        {
            CampusRuntimeObjectSettings settings = new CampusRuntimeObjectSettings();
            settings.ObjectId = prefab != null ? prefab.name : (placed != null ? placed.ObjectId : string.Empty);
            if (placed == null)
            {
                return settings;
            }

            placed.NormalizeCustomInteractionAnchors();
            placed.NormalizeStorageSettings();
            settings.TypeId = string.IsNullOrWhiteSpace(placed.TypeId) ? string.Empty : placed.TypeId.Trim();
            settings.DisplayNameOverride = string.IsNullOrWhiteSpace(placed.DisplayNameOverride)
                ? string.Empty
                : placed.DisplayNameOverride.Trim();
            settings.OverrideFootprintSize = placed.OverrideFootprintSize;
            settings.FootprintSize = placed.NormalizedFootprintSize;
            settings.VisualScale = placed.NormalizedVisualScale;
            settings.LockVisualScaleAspect = placed.LockVisualScaleAspect;
            settings.IsWallMounted = placed.IsWallMounted;
            settings.OverrideAllowRotation = placed.OverrideAllowRotation;
            settings.AllowRotation = placed.AllowRotation;
            settings.OverrideRotation0Sprite = placed.OverrideRotation0Sprite;
            settings.Rotation0SpritePath =
                CampusRuntimeImportLibrary.NormalizeSerializedPath(placed.Rotation0SpritePath, importRoot);
            settings.OverrideRotation90Sprite = placed.OverrideRotation90Sprite;
            settings.Rotation90SpritePath =
                CampusRuntimeImportLibrary.NormalizeSerializedPath(placed.Rotation90SpritePath, importRoot);
            settings.OverrideRotation180Sprite = placed.OverrideRotation180Sprite;
            settings.Rotation180SpritePath =
                CampusRuntimeImportLibrary.NormalizeSerializedPath(placed.Rotation180SpritePath, importRoot);
            settings.OverrideRotation270Sprite = placed.OverrideRotation270Sprite;
            settings.Rotation270SpritePath =
                CampusRuntimeImportLibrary.NormalizeSerializedPath(placed.Rotation270SpritePath, importRoot);
            settings.UseCustomInteractionAnchor = placed.UseCustomInteractionAnchor;
            settings.CustomInteractionAnchorLocalPosition = placed.CustomInteractionAnchorLocalPosition;
            settings.CustomInteractionAnchorRadius =
                CampusPlacedObject.NormalizeInteractionAnchorRadius(placed.CustomInteractionAnchorRadius);
            settings.CustomInteractionPromptText = string.IsNullOrWhiteSpace(placed.CustomInteractionPromptText)
                ? defaultInteractionPrompt
                : placed.CustomInteractionPromptText.Trim();
            settings.CustomInteractionAnchors =
                CampusPlacedObject.CloneInteractionAnchors(placed.CustomInteractionAnchors);
            settings.IsStorageContainer = placed.IsStorageContainer;
            settings.InteractionPresetEid = NormalizeInteractionPresetEid(placed.InteractionPresetEid);
            settings.StorageSize = placed.NormalizedStorageSize;
            settings.StorageMaxWeight = placed.NormalizedStorageMaxWeight;
            settings.RetailShelf = CaptureRetailShelfData(placed.gameObject);
            settings.ProtectedStockContainer = CaptureProtectedStockContainerData(placed.gameObject);
            return settings;
        }

        internal static CampusPlacedObject ApplySettings(
            GameObject target,
            CampusPlacedObject placed,
            CampusRuntimeObjectSettings settings,
            string importRoot,
            string defaultInteractionPrompt,
            Func<string, string, Vector2Int, Sprite> spriteResolver,
            Action<CampusPlacedObject> ensureStorageInteractionAnchor)
        {
            if (target == null || placed == null || settings == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(placed.ObjectId))
            {
                placed.ObjectId = !string.IsNullOrWhiteSpace(settings.ObjectId) ? settings.ObjectId : target.name;
            }

            placed.TypeId = ResolveTypeIdForSettings(target, settings);
            placed.DisplayNameOverride = string.IsNullOrWhiteSpace(settings.DisplayNameOverride)
                ? string.Empty
                : settings.DisplayNameOverride.Trim();
            placed.VisualScale = CampusPlacedObject.NormalizeVisualScale(settings.VisualScale);
            placed.LockVisualScaleAspect = settings.LockVisualScaleAspect;
            if (placed.LockVisualScaleAspect)
            {
                float uniform = Mathf.Max(placed.VisualScale.x, placed.VisualScale.y);
                placed.VisualScale = new Vector2(uniform, uniform);
            }

            placed.OverrideFootprintSize = settings.OverrideFootprintSize;
            placed.FootprintSize = CampusPlacedObject.NormalizeFootprintSize(settings.FootprintSize);
            placed.IsWallMounted = settings.IsWallMounted;
            if (placed.IsWallMounted)
            {
                placed.OverrideFootprintSize = true;
                placed.FootprintSize = Vector2Int.one;
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                placed.BlocksMovement = false;
                placed.BlocksSight = false;
            }

            placed.OverrideAllowRotation = settings.OverrideAllowRotation;
            placed.AllowRotation = settings.AllowRotation;
            if (placed.IsWallMounted)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
            }

            AssignDirectionSprite(
                placed,
                0,
                settings.OverrideRotation0Sprite,
                settings.Rotation0SpritePath,
                target.name,
                importRoot,
                spriteResolver);
            AssignDirectionSprite(
                placed,
                1,
                settings.OverrideRotation90Sprite,
                settings.Rotation90SpritePath,
                target.name,
                importRoot,
                spriteResolver);
            AssignDirectionSprite(
                placed,
                2,
                settings.OverrideRotation180Sprite,
                settings.Rotation180SpritePath,
                target.name,
                importRoot,
                spriteResolver);
            AssignDirectionSprite(
                placed,
                3,
                settings.OverrideRotation270Sprite,
                settings.Rotation270SpritePath,
                target.name,
                importRoot,
                spriteResolver);

            placed.UseCustomInteractionAnchor = settings.UseCustomInteractionAnchor;
            placed.CustomInteractionAnchorLocalPosition = settings.CustomInteractionAnchorLocalPosition;
            placed.CustomInteractionAnchorRadius =
                CampusPlacedObject.NormalizeInteractionAnchorRadius(settings.CustomInteractionAnchorRadius);
            placed.CustomInteractionPromptText = string.IsNullOrWhiteSpace(settings.CustomInteractionPromptText)
                ? defaultInteractionPrompt
                : settings.CustomInteractionPromptText;
            placed.CustomInteractionAnchors =
                CampusPlacedObject.CloneInteractionAnchors(settings.CustomInteractionAnchors);
            placed.IsStorageContainer = settings.IsStorageContainer;
            placed.InteractionPresetEid = NormalizeInteractionPresetEid(settings.InteractionPresetEid);
            placed.StorageSize = CampusPlacedObject.NormalizeStorageSize(settings.StorageSize);
            placed.StorageMaxWeight = CampusPlacedObject.NormalizeStorageMaxWeight(settings.StorageMaxWeight);

            ApplyRetailShelfData(target, placed, settings.RetailShelf);
            ApplyProtectedStockContainerData(target, placed, settings.ProtectedStockContainer);

            if (placed.IsStorageContainer &&
                string.IsNullOrWhiteSpace(placed.InteractionPresetEid) &&
                ensureStorageInteractionAnchor != null)
            {
                ensureStorageInteractionAnchor(placed);
            }

            placed.IsInteractable = placed.UseCustomInteractionAnchor ||
                                    placed.IsStorageContainer ||
                                    !string.IsNullOrWhiteSpace(placed.InteractionPresetEid);
            NormalizeStackableFacilityObject(placed, ResolveFacilityType(placed));

            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
            return placed;
        }

        internal static void AssignDirectionSprite(
            CampusPlacedObject placed,
            int rotation90Index,
            bool hasOverride,
            string spritePath,
            string objectName,
            string importRoot,
            Func<string, string, Vector2Int, Sprite> spriteResolver)
        {
            if (placed == null)
            {
                return;
            }

            string normalizedSpritePath = CampusRuntimeImportLibrary.NormalizeSerializedPath(spritePath, importRoot);
            bool enableOverride = hasOverride && !string.IsNullOrWhiteSpace(normalizedSpritePath);
            Vector2Int spriteFootprint =
                CampusPlacedObject.RotateFootprintSize(placed.NormalizedFootprintSize, rotation90Index);
            Sprite sprite = enableOverride && spriteResolver != null
                ? spriteResolver(normalizedSpritePath, objectName + "_" + (rotation90Index * 90), spriteFootprint)
                : null;
            switch (CampusPlacedObject.NormalizeRotation90(rotation90Index))
            {
                case 0:
                    placed.OverrideRotation0Sprite = enableOverride;
                    placed.Rotation0SpritePath = enableOverride ? normalizedSpritePath : string.Empty;
                    placed.Rotation0Sprite = sprite;
                    break;
                case 1:
                    placed.OverrideRotation90Sprite = enableOverride;
                    placed.Rotation90SpritePath = enableOverride ? normalizedSpritePath : string.Empty;
                    placed.Rotation90Sprite = sprite;
                    break;
                case 2:
                    placed.OverrideRotation180Sprite = enableOverride;
                    placed.Rotation180SpritePath = enableOverride ? normalizedSpritePath : string.Empty;
                    placed.Rotation180Sprite = sprite;
                    break;
                case 3:
                    placed.OverrideRotation270Sprite = enableOverride;
                    placed.Rotation270SpritePath = enableOverride ? normalizedSpritePath : string.Empty;
                    placed.Rotation270Sprite = sprite;
                    break;
            }
        }

        internal static string ResolveTypeIdForPlacedObject(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(placed.TypeId))
            {
                return placed.TypeId.Trim();
            }

            return InferObjectTypeId(
                placed.ObjectId,
                placed.DisplayName,
                placed.IsStorageContainer);
        }

        internal static string NormalizeInteractionPresetEid(string eid)
        {
            return string.IsNullOrWhiteSpace(eid) ? string.Empty : eid.Trim();
        }

        internal static string ResolveTypeIdForSnapshot(
            CampusRuntimeObjectSnapshot objectSnapshot,
            GameObject prefab,
            string displayName)
        {
            if (objectSnapshot == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(objectSnapshot.TypeId))
            {
                return objectSnapshot.TypeId.Trim();
            }

            CampusPlacedObject prefabPlaced = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            if (prefabPlaced != null && !string.IsNullOrWhiteSpace(prefabPlaced.TypeId))
            {
                return prefabPlaced.TypeId.Trim();
            }

            return InferObjectTypeId(
                !string.IsNullOrWhiteSpace(objectSnapshot.ObjectId)
                    ? objectSnapshot.ObjectId
                    : prefab != null ? prefab.name : string.Empty,
                displayName,
                objectSnapshot.IsStorageContainer);
        }

        internal static string ResolveTypeIdForSettings(GameObject target, CampusRuntimeObjectSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(settings.TypeId))
            {
                return settings.TypeId.Trim();
            }

            return InferObjectTypeId(
                !string.IsNullOrWhiteSpace(settings.ObjectId)
                    ? settings.ObjectId
                    : target != null ? target.name : string.Empty,
                settings.DisplayNameOverride,
                settings.IsStorageContainer);
        }

        internal static string ResolveSyncId(CampusRuntimeObjectSettings settings, GameObject prefab)
        {
            string objectId = settings != null && !string.IsNullOrWhiteSpace(settings.ObjectId)
                ? settings.ObjectId
                : prefab != null ? prefab.name : string.Empty;
            return string.IsNullOrWhiteSpace(objectId) ? string.Empty : objectId.Trim();
        }

        internal static bool DoesPlacedObjectMatchIdentity(
            CampusPlacedObject placed,
            string targetObjectId,
            string fallbackPrefabName)
        {
            if (placed == null || string.IsNullOrWhiteSpace(targetObjectId))
            {
                return false;
            }

            string placedObjectId = !string.IsNullOrWhiteSpace(placed.ObjectId)
                ? placed.ObjectId
                : placed.gameObject != null ? placed.gameObject.name : string.Empty;
            return ObjectIdentityEquals(placedObjectId, targetObjectId) ||
                   ObjectIdentityEquals(placedObjectId, fallbackPrefabName);
        }

        internal static string ResolvePlacedObjectTypeKey(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return string.Empty;
            }

            return ResolveObjectTypeKey(
                placed.TypeId,
                placed.ObjectId,
                placed.gameObject != null ? placed.gameObject.name : string.Empty);
        }

        internal static CampusFacilityType ResolveFacilityType(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return CampusFacilityType.Unknown;
            }

            if (CampusPlacedObjectConceptResolver.TryResolveFacility(placed, out CampusFacilityTypeResolution facilityResolution))
            {
                return facilityResolution.FacilityType;
            }

            return CampusFacilityType.Unknown;
        }

        internal static CampusRuntimeRetailShelfData CaptureRetailShelfData(GameObject target)
        {
            CampusRuntimeRetailShelfData data = new CampusRuntimeRetailShelfData();
            CampusRetailShelf shelf = target != null ? target.GetComponent<CampusRetailShelf>() : null;
            if (shelf == null)
            {
                return data;
            }

            data.Enabled = true;
            data.ShelfMode = shelf.ShelfMode;
            data.ItemDefinitionId = string.IsNullOrWhiteSpace(shelf.ItemDefinitionId)
                ? string.Empty
                : shelf.ItemDefinitionId.Trim();
            data.StockCount = Mathf.Max(1, shelf.StockCount);
            data.DisplaySlotCount = Mathf.Max(1, shelf.DisplaySlotCount);
            data.AutoRestock = shelf.AutoRestock;
            return data;
        }

        internal static CampusRuntimeRetailShelfData CloneRetailShelfData(CampusRuntimeRetailShelfData source)
        {
            CampusRuntimeRetailShelfData clone = new CampusRuntimeRetailShelfData();
            if (source == null)
            {
                return clone;
            }

            clone.Enabled = source.Enabled;
            clone.ShelfMode = source.ShelfMode;
            clone.ItemDefinitionId = source.ItemDefinitionId;
            clone.StockCount = source.StockCount;
            clone.DisplaySlotCount = source.DisplaySlotCount;
            clone.AutoRestock = source.AutoRestock;
            NormalizeRetailShelfData(clone);
            return clone;
        }

        internal static void NormalizeRetailShelfData(CampusRuntimeRetailShelfData data)
        {
            if (data == null)
            {
                return;
            }

            data.ItemDefinitionId = string.IsNullOrWhiteSpace(data.ItemDefinitionId)
                ? string.Empty
                : data.ItemDefinitionId.Trim();
            data.StockCount = Mathf.Max(1, data.StockCount);
            data.DisplaySlotCount = Mathf.Max(1, data.DisplaySlotCount);
        }

        internal static void ApplyRetailShelfData(
            GameObject target,
            CampusPlacedObject placed,
            CampusRuntimeRetailShelfData data)
        {
            NormalizeRetailShelfData(data);
            if (target == null || !target.scene.IsValid() && placed == null)
            {
                return;
            }

            if (data == null || !data.Enabled)
            {
                return;
            }

            CampusRetailShelf shelf = target.GetComponent<CampusRetailShelf>();
            if (shelf == null)
            {
                shelf = target.AddComponent<CampusRetailShelf>();
            }

            if (string.IsNullOrWhiteSpace(shelf.ShelfId) && placed != null && !string.IsNullOrWhiteSpace(placed.ObjectId))
            {
                shelf.ShelfId = placed.ObjectId.Trim();
            }

            shelf.ItemDefinitionId = data.ItemDefinitionId;
            shelf.ShelfMode = data.ShelfMode;
            shelf.StockCount = data.StockCount;
            shelf.DisplaySlotCount = data.DisplaySlotCount;
            shelf.AutoRestock = data.AutoRestock;
            if (placed != null)
            {
                placed.IsStorageContainer = data.ShelfMode == CampusRetailShelfMode.Container;
            }
        }

        internal static CampusRuntimeProtectedStockContainerData CaptureProtectedStockContainerData(GameObject target)
        {
            CampusRuntimeProtectedStockContainerData data = new CampusRuntimeProtectedStockContainerData();
            CampusProtectedStockContainer stockContainer = target != null
                ? target.GetComponent<CampusProtectedStockContainer>()
                : null;
            if (stockContainer == null)
            {
                return data;
            }

            stockContainer.Normalize();
            data.Enabled = true;
            data.ContainerId = stockContainer.ContainerId;
            data.OwnerId = stockContainer.OwnerId;
            data.OwnerRole = stockContainer.OwnerRole;
            data.AllowTakingContents = stockContainer.AllowTakingContents;
            data.SuspicionRisk = stockContainer.SuspicionRisk;
            data.AutoRestock = stockContainer.AutoRestock;
            data.StockItems = CloneStockEntries(stockContainer.StockItems);
            return data;
        }

        internal static CampusRuntimeProtectedStockContainerData CloneProtectedStockContainerData(
            CampusRuntimeProtectedStockContainerData source)
        {
            CampusRuntimeProtectedStockContainerData clone = new CampusRuntimeProtectedStockContainerData();
            if (source == null)
            {
                return clone;
            }

            clone.Enabled = source.Enabled;
            clone.ContainerId = source.ContainerId;
            clone.OwnerId = source.OwnerId;
            clone.OwnerRole = source.OwnerRole;
            clone.AllowTakingContents = source.AllowTakingContents;
            clone.SuspicionRisk = source.SuspicionRisk;
            clone.AutoRestock = source.AutoRestock;
            clone.StockItems = CloneStockEntries(source.StockItems);
            NormalizeProtectedStockContainerData(clone);
            return clone;
        }

        internal static void NormalizeProtectedStockContainerData(CampusRuntimeProtectedStockContainerData data)
        {
            if (data == null)
            {
                return;
            }

            data.ContainerId = string.IsNullOrWhiteSpace(data.ContainerId) ? string.Empty : data.ContainerId.Trim();
            data.OwnerId = string.IsNullOrWhiteSpace(data.OwnerId) ? string.Empty : data.OwnerId.Trim();
            data.OwnerRole = string.IsNullOrWhiteSpace(data.OwnerRole) ? "Campus" : data.OwnerRole.Trim();
            data.SuspicionRisk = Mathf.Max(0, data.SuspicionRisk);
            data.StockItems = NormalizeStockEntries(data.StockItems);
        }

        internal static void ApplyProtectedStockContainerData(
            GameObject target,
            CampusPlacedObject placed,
            CampusRuntimeProtectedStockContainerData data)
        {
            NormalizeProtectedStockContainerData(data);
            if (target == null || data == null || !data.Enabled)
            {
                return;
            }

            CampusProtectedStockContainer stockContainer = target.GetComponent<CampusProtectedStockContainer>();
            if (stockContainer == null)
            {
                stockContainer = target.AddComponent<CampusProtectedStockContainer>();
            }

            stockContainer.ContainerId = data.ContainerId;
            stockContainer.OwnerId = data.OwnerId;
            stockContainer.OwnerRole = data.OwnerRole;
            stockContainer.AllowTakingContents = data.AllowTakingContents;
            stockContainer.SuspicionRisk = data.SuspicionRisk;
            stockContainer.AutoRestock = data.AutoRestock;
            stockContainer.StockItems = CloneStockEntries(data.StockItems);
            stockContainer.Normalize();

            if (placed != null)
            {
                placed.IsStorageContainer = true;
            }
        }

        private static List<CampusProtectedStockEntry> CloneStockEntries(List<CampusProtectedStockEntry> source)
        {
            List<CampusProtectedStockEntry> clone = new List<CampusProtectedStockEntry>();
            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Count; i++)
            {
                CampusProtectedStockEntry entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemDefinitionId))
                {
                    continue;
                }

                clone.Add(new CampusProtectedStockEntry
                {
                    ItemDefinitionId = entry.ItemDefinitionId.Trim(),
                    StockCount = Mathf.Max(1, entry.StockCount)
                });
            }

            return clone;
        }

        private static List<CampusProtectedStockEntry> NormalizeStockEntries(List<CampusProtectedStockEntry> entries)
        {
            List<CampusProtectedStockEntry> normalized = new List<CampusProtectedStockEntry>();
            if (entries == null)
            {
                return normalized;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                CampusProtectedStockEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemDefinitionId))
                {
                    continue;
                }

                normalized.Add(new CampusProtectedStockEntry
                {
                    ItemDefinitionId = entry.ItemDefinitionId.Trim(),
                    StockCount = Mathf.Max(1, entry.StockCount)
                });
            }

            return normalized;
        }

        internal static CampusFacilityType ResolveFacilityType(
            GameObject prefab,
            CampusPlacedObject placed,
            string displayName)
        {
            if (placed != null)
            {
                CampusFacilityType resolved = ResolveFacilityType(placed);
                if (resolved != CampusFacilityType.Unknown)
                {
                    return resolved;
                }
            }

            string objectId = placed != null && !string.IsNullOrWhiteSpace(placed.ObjectId)
                ? placed.ObjectId
                : prefab != null ? prefab.name : string.Empty;
            string inferredTypeId = InferObjectTypeId(
                objectId,
                displayName,
                placed != null && placed.IsStorageContainer);
            return TryParseFacilityType(inferredTypeId, out CampusFacilityType inferred)
                ? inferred
                : CampusFacilityType.Unknown;
        }

        internal static bool IsStackableFacilityObject(CampusFacilityType facilityType)
        {
            return false;
        }

        internal static void NormalizeStackableFacilityObject(CampusPlacedObject placed, CampusFacilityType facilityType)
        {
            if (placed == null || !IsStackableFacilityObject(facilityType))
            {
                return;
            }

            placed.OverrideFootprintSize = true;
            placed.FootprintSize = Vector2Int.one;
            placed.BlocksMovement = false;
            placed.BlocksSight = false;
            placed.SortingOrderOffset = Mathf.Max(
                placed.SortingOrderOffset,
                CampusPlacedObject.StackableFacilitySortingOrderOffset);
        }

        internal static string InferObjectTypeId(string objectId, string displayName, bool isStorageContainer)
        {
            if (isStorageContainer)
            {
                return nameof(CampusFacilityType.Storage);
            }

            string key = NormalizeObjectTypeIdSeed(objectId) + "|" + NormalizeObjectTypeIdSeed(displayName);
            if (ContainsObjectTypeToken(key, "studentdesk", "student_desk", "desk_1x1", "\u8bfe\u684c", "\u4e66\u684c"))
            {
                return nameof(CampusFacilityType.StudentDesk);
            }

            if (ContainsObjectTypeToken(key, "officedesk", "office_desk", "teacherdesk", "teacher_desk", "\u529e\u516c\u684c", "\u6559\u5e08\u684c"))
            {
                return nameof(CampusFacilityType.OfficeDesk);
            }

            if (ContainsObjectTypeToken(key, "blackboard", "whiteboard", "chalkboard", "\u9ed1\u677f", "\u767d\u677f"))
            {
                return nameof(CampusFacilityType.Blackboard);
            }

            if (ContainsObjectTypeToken(key, "podium", "teacherpodium", "teacher_podium", "\u8bb2\u53f0"))
            {
                return nameof(CampusFacilityType.Podium);
            }

            if (ContainsObjectTypeToken(key, "diningtable", "dining_table", "\u9910\u684c", "\u5403\u996d"))
            {
                return nameof(CampusFacilityType.DiningTable);
            }

            if (ContainsObjectTypeToken(key, "bulletin", "\u516c\u544a\u680f"))
            {
                return nameof(CampusFacilityType.BulletinBoard);
            }

            if (ContainsObjectTypeToken(key, "recruitment", "recruit", "\u62db\u52df"))
            {
                return nameof(CampusFacilityType.Recruitment);
            }

            if (ContainsObjectTypeToken(key, "restroomstall", "restroom_stall", "stall", "\u9694\u95f4"))
            {
                return nameof(CampusFacilityType.RestroomStall);
            }

            if (ContainsObjectTypeToken(key, "urinal", "\u5c0f\u4fbf\u6c60"))
            {
                return nameof(CampusFacilityType.Urinal);
            }

            if (ContainsObjectTypeToken(key, "sink", "\u6d17\u624b\u6c60", "\u6c34\u6c60"))
            {
                return nameof(CampusFacilityType.Sink);
            }

            if (ContainsObjectTypeToken(key, "bed", "\u5e8a"))
            {
                return nameof(CampusFacilityType.Bed);
            }

            if (ContainsObjectTypeToken(key, "chair", "\u6905\u5b50"))
            {
                return nameof(CampusFacilityType.Chair);
            }

            if (ContainsObjectTypeToken(key, "door", "\u95e8"))
            {
                return nameof(CampusFacilityType.Door);
            }

            if (ContainsObjectTypeToken(key, "stair", "\u697c\u68af"))
            {
                return nameof(CampusFacilityType.Stair);
            }

            return string.Empty;
        }

        private static bool TryParseFacilityType(string typeId, out CampusFacilityType type)
        {
            type = CampusFacilityType.Unknown;
            return !string.IsNullOrWhiteSpace(typeId) &&
                   Enum.TryParse(typeId.Trim(), true, out type) &&
                   type != CampusFacilityType.Unknown;
        }

        private static string ResolveObjectTypeKey(string typeId, string objectId, string fallbackName)
        {
            string resolved = !string.IsNullOrWhiteSpace(typeId)
                ? typeId
                : !string.IsNullOrWhiteSpace(objectId)
                    ? objectId
                    : fallbackName;
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return string.Empty;
            }

            return resolved.Trim();
        }

        private static bool ObjectIdentityEquals(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            string normalizedLeft = left.Trim();
            string normalizedRight = right.Trim();
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       CampusObjectNames.GetDisplayName(normalizedLeft),
                       CampusObjectNames.GetDisplayName(normalizedRight),
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeObjectTypeIdSeed(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", string.Empty).Replace("-", "_").ToLowerInvariant();
        }

        private static bool ContainsObjectTypeToken(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = NormalizeObjectTypeIdSeed(tokens[i]);
                if (!string.IsNullOrEmpty(token) && value.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
