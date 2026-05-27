using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    public static class StorageItemStackingService
    {
        public const int MaxSupportedStackSize = 9;

        public static bool CanItemStack(StorageItemModel item)
        {
            return item != null &&
                   !IsMarked(item) &&
                   ResolveMaxStackSize(item) > 1 &&
                   !string.IsNullOrWhiteSpace(item.StackGroupId);
        }

        public static int ResolveMaxStackSize(StorageItemModel item)
        {
            return Mathf.Clamp(item != null ? item.MaxStackSize : 1, 1, MaxSupportedStackSize);
        }

        public static bool IsMarked(StorageItemModel item)
        {
            StorageItemEvidenceState evidence = item != null ? item.Evidence : null;
            if (evidence == null)
            {
                return false;
            }

            return evidence.LegalState != StorageItemLegalState.Unknown &&
                   evidence.LegalState != StorageItemLegalState.Personal ||
                   evidence.StolenDuringSession ||
                   evidence.SuspicionRisk > 0 ||
                   !string.IsNullOrWhiteSpace(evidence.OwnerId) ||
                   !string.IsNullOrWhiteSpace(evidence.SourceContainerId) ||
                   !string.IsNullOrWhiteSpace(evidence.SourceRoomId) ||
                   !string.IsNullOrWhiteSpace(evidence.SourceLocation);
        }

        public static bool CanStackTogether(StorageItemModel left, StorageItemModel right)
        {
            return CanItemStack(left) &&
                   CanItemStack(right) &&
                   string.Equals(left.StackGroupId.Trim(), right.StackGroupId.Trim(), StringComparison.OrdinalIgnoreCase) &&
                   left.CurrentWidth == right.CurrentWidth &&
                   left.CurrentHeight == right.CurrentHeight;
        }

        public static bool TryFindCompatibleStackAt(
            StorageContainerModel container,
            StorageItemModel item,
            int x,
            int y,
            StorageItemModel ignoreItem,
            out StorageItemModel representative)
        {
            representative = null;
            if (container == null || container.Items == null || !CanItemStack(item))
            {
                return false;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel candidate = container.Items[i];
                if (candidate == null || candidate == ignoreItem)
                {
                    continue;
                }

                if (candidate.X != x ||
                    candidate.Y != y ||
                    !CanStackTogether(item, candidate) ||
                    GetStackCount(container, candidate) >= ResolveStackLimit(item, candidate))
                {
                    continue;
                }

                representative = ResolveRepresentative(container, candidate);
                return representative != null;
            }

            return false;
        }

        public static void PrepareItemForPlacement(
            StorageContainerModel container,
            StorageItemModel item,
            int x,
            int y,
            StorageItemModel ignoreItem)
        {
            if (item == null)
            {
                return;
            }

            if (!CanItemStack(item))
            {
                item.StackId = string.Empty;
                return;
            }

            if (TryFindCompatibleStackAt(container, item, x, y, ignoreItem, out StorageItemModel stack))
            {
                item.StackId = stack.StackId;
                item.X = stack.X;
                item.Y = stack.Y;
                return;
            }

            if (string.IsNullOrWhiteSpace(item.StackId))
            {
                item.StackId = Guid.NewGuid().ToString("N");
            }
        }

        public static bool IsSameStack(StorageItemModel left, StorageItemModel right)
        {
            return left != null &&
                   right != null &&
                   !string.IsNullOrWhiteSpace(left.StackId) &&
                   string.Equals(left.StackId, right.StackId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRepresentative(StorageContainerModel container, StorageItemModel item)
        {
            return item != null && ResolveRepresentative(container, item) == item;
        }

        public static StorageItemModel ResolveRepresentative(StorageContainerModel container, StorageItemModel item)
        {
            if (container == null || container.Items == null || item == null || string.IsNullOrWhiteSpace(item.StackId))
            {
                return item;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel candidate = container.Items[i];
                if (IsSameStack(candidate, item))
                {
                    return candidate;
                }
            }

            return item;
        }

        public static int GetStackCount(StorageContainerModel container, StorageItemModel item)
        {
            if (container == null || container.Items == null || item == null || string.IsNullOrWhiteSpace(item.StackId))
            {
                return item != null ? 1 : 0;
            }

            int count = 0;
            for (int i = 0; i < container.Items.Count; i++)
            {
                if (IsSameStack(container.Items[i], item))
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }

        public static List<StorageItemModel> GetStackMembers(StorageContainerModel container, StorageItemModel item)
        {
            List<StorageItemModel> members = new List<StorageItemModel>();
            if (container == null || container.Items == null || item == null)
            {
                return members;
            }

            if (string.IsNullOrWhiteSpace(item.StackId))
            {
                members.Add(item);
                return members;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel candidate = container.Items[i];
                if (IsSameStack(candidate, item))
                {
                    members.Add(candidate);
                }
            }

            return members;
        }

        public static void NormalizeContainer(StorageContainerModel container)
        {
            if (container == null || container.Items == null)
            {
                return;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null && !CanItemStack(item))
                {
                    item.StackId = string.Empty;
                }
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.StackId))
                {
                    continue;
                }

                List<StorageItemModel> members = GetStackMembers(container, item);
                if (members.Count <= 1)
                {
                    item.StackId = string.Empty;
                    continue;
                }

                int limit = ResolveMaxStackSize(item);
                for (int memberIndex = limit; memberIndex < members.Count; memberIndex++)
                {
                    members[memberIndex].StackId = string.Empty;
                }
            }
        }

        public static float GetStackWeight(IReadOnlyList<StorageItemModel> items)
        {
            float weight = 0f;
            if (items == null)
            {
                return weight;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    weight += items[i].Weight;
                }
            }

            return weight;
        }

        public static void MoveStackMembers(StorageContainerModel container, StorageItemModel item, int x, int y)
        {
            if (container == null || container.Items == null || item == null || string.IsNullOrWhiteSpace(item.StackId))
            {
                return;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel member = container.Items[i];
                if (IsSameStack(member, item))
                {
                    member.X = x;
                    member.Y = y;
                }
            }
        }

        public static void NormalizeItemAfterRemoval(StorageContainerModel previousContainer, StorageItemModel item)
        {
            if (item == null || previousContainer == null || string.IsNullOrWhiteSpace(item.StackId))
            {
                return;
            }

            string removedStackId = item.StackId;
            StorageItemModel remaining = null;
            int remainingCount = 0;
            for (int i = 0; i < previousContainer.Items.Count; i++)
            {
                StorageItemModel candidate = previousContainer.Items[i];
                if (candidate != null &&
                    !string.IsNullOrWhiteSpace(candidate.StackId) &&
                    string.Equals(candidate.StackId, removedStackId, StringComparison.OrdinalIgnoreCase))
                {
                    remaining = candidate;
                    remainingCount++;
                }
            }

            if (remainingCount <= 1)
            {
                item.StackId = string.Empty;
                if (remaining != null)
                {
                    remaining.StackId = string.Empty;
                }
            }
        }

        private static int ResolveStackLimit(StorageItemModel left, StorageItemModel right)
        {
            return Mathf.Min(ResolveMaxStackSize(left), ResolveMaxStackSize(right));
        }
    }
}
