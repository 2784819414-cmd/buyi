using Nting.Storage;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public static class CampusItemRiskUtility
    {
        public static int ResolveProtectedMoveRisk(
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context)
        {
            if (context != null && context.SuspicionRiskOverride >= 0)
            {
                return Mathf.Max(0, context.SuspicionRiskOverride);
            }

            int amount = 8;
            if (source != null)
            {
                amount += Mathf.Max(0, source.SuspicionRisk);
                amount += ResolveAccessPolicyRisk(source.AccessPolicy);
            }

            if (item != null)
            {
                amount += Mathf.Max(0, item.SuspicionRisk);
                amount += item.Weight >= 2f ? 2 : 0;
            }

            return Mathf.Clamp(amount, 1, 45);
        }

        private static int ResolveAccessPolicyRisk(StorageContainerAccessPolicy policy)
        {
            switch (policy)
            {
                case StorageContainerAccessPolicy.StaffOnly:
                    return 12;
                case StorageContainerAccessPolicy.ProtectedTransfer:
                    return 8;
                case StorageContainerAccessPolicy.OwnedPrivate:
                    return 7;
                case StorageContainerAccessPolicy.ProtectedPublic:
                    return 5;
                default:
                    return 0;
            }
        }
    }
}
