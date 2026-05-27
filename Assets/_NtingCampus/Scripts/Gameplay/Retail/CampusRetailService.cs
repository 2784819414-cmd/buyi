using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Retail
{
    internal readonly struct CampusRetailCheckoutSummary
    {
        public CampusRetailCheckoutSummary(int pendingItemCount, int totalPrice)
        {
            PendingItemCount = Mathf.Max(0, pendingItemCount);
            TotalPrice = Mathf.Max(0, totalPrice);
        }

        public int PendingItemCount { get; }
        public int TotalPrice { get; }
        public bool HasPendingItems => PendingItemCount > 0;
    }

    internal static class CampusRetailService
    {
        public static CampusRetailCheckoutSummary BuildPendingSummary(CampusCharacterRuntime actor)
        {
            return BuildSummary(actor, string.Empty, false);
        }

        public static CampusRetailCheckoutSummary BuildCheckoutSummary(
            CampusCharacterRuntime actor,
            Component checkoutSource)
        {
            return BuildSummary(actor, ResolveStoreRoomId(actor, checkoutSource), true);
        }

        public static bool TryCheckoutActor(
            CampusCharacterRuntime actor,
            Component checkoutSource,
            out string message)
        {
            message = string.Empty;
            if (actor == null)
            {
                return false;
            }

            string storeRoomId = ResolveStoreRoomId(actor, checkoutSource);
            CampusProtectedTransferClearanceSummary summary =
                CampusProtectedTransferClearanceService.BuildSummary(actor, storeRoomId, true);
            if (!summary.HasPendingItems)
            {
                message = CampusRetailTextCatalog.Get(CampusRetailTextId.NoPendingItems);
                return false;
            }

            CampusServiceStationClearanceDefinition clearance = ResolveCheckoutClearance(checkoutSource);

            if (!CampusProtectedTransferClearanceService.TryClearPendingTransfers(
                    actor,
                    checkoutSource,
                    clearance,
                    out message))
            {
                return false;
            }
            return true;
        }

        private static CampusServiceStationClearanceDefinition ResolveCheckoutClearance(Component checkoutSource)
        {
            if (CampusServiceStationRuntimeAvailability.TryResolveActionStation(
                    CampusRetailActionIds.Checkout,
                    checkoutSource,
                    out CampusServiceStation station) &&
                station.Clearance.ClearsPendingProtectedTransfers)
            {
                return station.Clearance;
            }

            return new CampusServiceStationClearanceDefinition(
                CampusServiceStationClearanceMode.ClearPendingProtectedTransfers,
                CampusServiceStationClearancePriceMode.ItemPrice,
                new NtingCampus.UI.Runtime.Gameplay.CampusLocalizedText(
                    CampusRetailTextCatalog.Get(NtingCampus.UI.Runtime.Gameplay.CampusDisplayLanguage.Chinese, CampusRetailTextId.CheckoutComplete),
                    CampusRetailTextCatalog.Get(NtingCampus.UI.Runtime.Gameplay.CampusDisplayLanguage.English, CampusRetailTextId.CheckoutComplete)),
                new NtingCampus.UI.Runtime.Gameplay.CampusLocalizedText(
                    CampusRetailTextCatalog.Get(NtingCampus.UI.Runtime.Gameplay.CampusDisplayLanguage.Chinese, CampusRetailTextId.NoPendingItems),
                    CampusRetailTextCatalog.Get(NtingCampus.UI.Runtime.Gameplay.CampusDisplayLanguage.English, CampusRetailTextId.NoPendingItems)),
                new NtingCampus.UI.Runtime.Gameplay.CampusLocalizedText(
                    CampusRetailTextCatalog.Get(NtingCampus.UI.Runtime.Gameplay.CampusDisplayLanguage.Chinese, CampusRetailTextId.InsufficientFunds),
                    CampusRetailTextCatalog.Get(NtingCampus.UI.Runtime.Gameplay.CampusDisplayLanguage.English, CampusRetailTextId.InsufficientFunds)));
        }

        public static string ResolveStoreRoomId(
            CampusCharacterRuntime actor,
            Component source)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            CampusPlacedObject placedObject = source != null ? source.GetComponentInParent<CampusPlacedObject>() : null;
            if (worldService != null && placedObject != null)
            {
                CampusGameplayRoom sourceRoom = worldService.FindRoomForPosition(
                    placedObject.FloorIndex,
                    placedObject.transform.position);
                if (sourceRoom != null)
                {
                    return sourceRoom.RoomId;
                }
            }

            if (worldService != null && actor != null)
            {
                CampusGameplayRoom actorRoom = worldService.FindRoomForRuntime(actor);
                if (actorRoom != null)
                {
                    return actorRoom.RoomId;
                }
            }

            return actor != null && actor.Data != null ? actor.Data.CurrentRoomId : string.Empty;
        }

        private static CampusRetailCheckoutSummary BuildSummary(
            CampusCharacterRuntime actor,
            string storeRoomId,
            bool filterByStore)
        {
            CampusProtectedTransferClearanceSummary summary =
                CampusProtectedTransferClearanceService.BuildSummary(actor, storeRoomId, true);
            return new CampusRetailCheckoutSummary(summary.PendingItemCount, summary.TotalPrice);
        }
    }
}
