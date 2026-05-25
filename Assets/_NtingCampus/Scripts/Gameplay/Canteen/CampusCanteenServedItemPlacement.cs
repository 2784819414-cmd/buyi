using Nting.Storage;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal static class CampusCanteenServedItemPlacement
    {
        private const int SlotColumns = 3;
        private const float SlotSpacingX = 0.22f;
        private const float SlotSpacingY = 0.08f;
        private const float CustomerSideSurfaceHeight = -0.16f;
        public const string SourceLocationId = "canteen_window";
        public const string SourceContainerPrefix = "canteen_window_order";

        public static bool TryPlace(
            CampusPlacedObject window,
            StorageItemModel item,
            out string errorMessage,
            out CampusDroppedStorageItem droppedItem)
        {
            errorMessage = string.Empty;
            droppedItem = null;
            if (window == null || item == null)
            {
                return false;
            }

            string sourceContainerId = BuildWindowSourceContainerId(window);
            item.SourceContainerId = sourceContainerId;

            int slotIndex = CountExistingServedItems(sourceContainerId);
            Vector3 worldPosition = ResolveServedWorldPosition(window, slotIndex);
            return CampusStorageGroundItemUtility.TryPlaceItemAtWorldPosition(
                window.gameObject,
                item,
                worldPosition,
                out errorMessage,
                out droppedItem);
        }

        public static string BuildWindowSourceContainerId(CampusPlacedObject window)
        {
            if (window == null)
            {
                return SourceContainerPrefix;
            }

            string objectId = SanitizeId(window.ObjectId);
            Vector3Int cell = window.Cell;
            string cellId = "f" + window.FloorIndex + "_c" + cell.x + "_" + cell.y + "_" + cell.z;
            return string.IsNullOrWhiteSpace(objectId)
                ? SourceContainerPrefix + "_" + cellId
                : SourceContainerPrefix + "_" + cellId + "_" + objectId;
        }

        public static bool IsServedCanteenItem(CampusDroppedStorageItem item)
        {
            return item != null &&
                   item.SourceLocation == SourceLocationId &&
                   !string.IsNullOrWhiteSpace(item.SourceContainerId) &&
                   item.SourceContainerId.StartsWith(SourceContainerPrefix, System.StringComparison.OrdinalIgnoreCase);
        }

        private static int CountExistingServedItems(string sourceContainerId)
        {
            if (string.IsNullOrWhiteSpace(sourceContainerId))
            {
                return 0;
            }

            return CampusDroppedStorageItemRegistry.CountBySourceContainer(sourceContainerId);
        }

        private static Vector3 ResolveServedWorldPosition(CampusPlacedObject window, int slotIndex)
        {
            int normalizedIndex = Mathf.Abs(slotIndex);
            int column = normalizedIndex % SlotColumns;
            int row = normalizedIndex / SlotColumns;
            float centeredColumn = column - (SlotColumns - 1) * 0.5f;
            Vector3 localOffset = new Vector3(
                centeredColumn * SlotSpacingX,
                CustomerSideSurfaceHeight + row * SlotSpacingY,
                0f);
            return window.transform.TransformPoint(localOffset);
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!IsAsciiIdChar(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars).Trim('_');
        }

        private static bool IsAsciiIdChar(char value)
        {
            return value >= 'a' && value <= 'z' ||
                   value >= 'A' && value <= 'Z' ||
                   value >= '0' && value <= '9' ||
                   value == '_' ||
                   value == '-';
        }
    }
}
