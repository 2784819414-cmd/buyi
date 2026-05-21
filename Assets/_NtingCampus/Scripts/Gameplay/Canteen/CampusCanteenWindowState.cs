using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    [DisallowMultipleComponent]
    internal sealed class CampusCanteenWindowState : MonoBehaviour
    {
        [SerializeField] private string pendingCustomerId = string.Empty;
        [SerializeField] private string readyCustomerId = string.Empty;
        [SerializeField] private CampusDroppedStorageItem readyMealItem;

        public string PendingCustomerId => NormalizeId(pendingCustomerId);
        public string ReadyCustomerId => NormalizeId(readyCustomerId);
        public CampusDroppedStorageItem ReadyMealItem => RefreshReadyMealItem();

        public bool HasPendingOrder => !string.IsNullOrEmpty(PendingCustomerId);
        public bool HasReadyMeal => RefreshReadyMealItem() != null && !string.IsNullOrEmpty(ReadyCustomerId);

        public bool IsReservedFor(string actorId)
        {
            string normalizedActorId = NormalizeId(actorId);
            return (!string.IsNullOrEmpty(PendingCustomerId) &&
                    string.Equals(PendingCustomerId, normalizedActorId, System.StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrEmpty(ReadyCustomerId) &&
                    string.Equals(ReadyCustomerId, normalizedActorId, System.StringComparison.OrdinalIgnoreCase));
        }

        public bool CanAcceptOrder(string actorId)
        {
            RefreshReadyMealItem();
            string normalizedActorId = NormalizeId(actorId);
            if (string.IsNullOrEmpty(normalizedActorId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(readyCustomerId))
            {
                return string.Equals(readyCustomerId, normalizedActorId, System.StringComparison.OrdinalIgnoreCase);
            }

            return string.IsNullOrEmpty(pendingCustomerId) ||
                   string.Equals(pendingCustomerId, normalizedActorId, System.StringComparison.OrdinalIgnoreCase);
        }

        public void PlaceOrder(string customerId)
        {
            pendingCustomerId = NormalizeId(customerId);
            readyCustomerId = string.Empty;
            RefreshReadyMealItem();
        }

        public void MarkMealReady(string customerId, CampusDroppedStorageItem droppedItem)
        {
            pendingCustomerId = string.Empty;
            readyCustomerId = NormalizeId(customerId);
            readyMealItem = droppedItem;
        }

        public void ClearReadyMeal()
        {
            readyCustomerId = string.Empty;
            readyMealItem = null;
        }

        public void ClearPendingOrder()
        {
            pendingCustomerId = string.Empty;
        }

        private CampusDroppedStorageItem RefreshReadyMealItem()
        {
            if (readyMealItem == null)
            {
                readyCustomerId = string.Empty;
                return null;
            }

            return readyMealItem;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
