using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    public sealed class StorageItemUseContext
    {
        public GameObject Actor;
        public StorageTransferReason Reason = StorageTransferReason.UseItem;

        public static StorageItemUseContext ForActor(GameObject actor, StorageTransferReason reason)
        {
            return new StorageItemUseContext
            {
                Actor = actor,
                Reason = reason
            };
        }
    }

    public interface IStorageItemUseAction
    {
        string ActionId { get; }

        bool CanUse(StorageItemModel item, StorageGridUI sourceGrid, out string reason);

        bool TryUse(
            StorageItemModel item,
            StorageGridUI sourceGrid,
            StorageItemUseContext context,
            out string statusMessage);
    }

    public static class StorageItemUseActionRegistry
    {
        private static readonly Dictionary<string, IStorageItemUseAction> Actions =
            new Dictionary<string, IStorageItemUseAction>(System.StringComparer.OrdinalIgnoreCase);

        public static void Register(IStorageItemUseAction action)
        {
            if (action != null && !string.IsNullOrWhiteSpace(action.ActionId))
            {
                Actions[action.ActionId.Trim()] = action;
            }
        }

        public static bool TryGet(string actionId, out IStorageItemUseAction action)
        {
            action = null;
            return !string.IsNullOrWhiteSpace(actionId) &&
                   Actions.TryGetValue(actionId.Trim(), out action) &&
                   action != null;
        }
    }
}
