using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    internal static class CampusProtectedTransferClearanceActionIds
    {
        public const string ClearPending = "campus.protected_transfer.clear";
    }

    internal readonly struct CampusProtectedTransferClearanceSummary
    {
        public CampusProtectedTransferClearanceSummary(int pendingItemCount, int totalPrice)
        {
            PendingItemCount = Mathf.Max(0, pendingItemCount);
            TotalPrice = Mathf.Max(0, totalPrice);
        }

        public int PendingItemCount { get; }
        public int TotalPrice { get; }
        public bool HasPendingItems => PendingItemCount > 0;
    }

    internal static class CampusProtectedTransferClearanceService
    {
        private readonly struct ClearanceSourceScope
        {
            public ClearanceSourceScope(string roomId, CampusRoomType roomType)
            {
                RoomId = NormalizeId(roomId);
                LegacyRoomTypeId = roomType == CampusRoomType.Unknown
                    ? string.Empty
                    : roomType.ToString();
            }

            public string RoomId { get; }
            public string LegacyRoomTypeId { get; }
        }

        public static CampusProtectedTransferClearanceSummary BuildSummary(
            CampusCharacterRuntime actor,
            string sourceRoomId,
            bool chargeItemPrice)
        {
            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            return BuildSummary(inventory, new ClearanceSourceScope(sourceRoomId, CampusRoomType.Unknown), true, chargeItemPrice);
        }

        public static bool TryClearPendingTransfers(
            CampusCharacterRuntime actor,
            CampusServiceStation station,
            out string message)
        {
            message = string.Empty;
            if (actor == null)
            {
                return false;
            }

            CampusServiceStationClearanceDefinition clearance = station.Clearance ?? CampusServiceStationClearanceDefinition.None;
            if (!clearance.ClearsPendingProtectedTransfers)
            {
                message = CampusProtectedTransferClearanceTextCatalog.Get(
                    CampusProtectedTransferClearanceTextId.UnsupportedStation);
                return false;
            }

            ClearanceSourceScope sourceScope = ResolveSourceScope(station);
            bool chargeItemPrice = clearance.PriceMode == CampusServiceStationClearancePriceMode.ItemPrice;
            bool filterBySourceRoom = clearance.Scope == CampusServiceStationClearanceScope.StationRoom;
            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            CampusProtectedTransferClearanceSummary summary =
                BuildSummary(inventory, sourceScope, filterBySourceRoom, chargeItemPrice);
            if (!summary.HasPendingItems)
            {
                message = ResolveText(
                    clearance.NoPendingItemsText,
                    CampusProtectedTransferClearanceTextCatalog.Get(
                        CampusProtectedTransferClearanceTextId.NoPendingItems));
                return false;
            }

            if (chargeItemPrice &&
                summary.TotalPrice > 0 &&
                !TrySpend(actor, summary.TotalPrice))
            {
                message = FormatText(
                    clearance.InsufficientFundsText,
                    CampusProtectedTransferClearanceTextCatalog.Get(
                        CampusProtectedTransferClearanceTextId.InsufficientFunds),
                    summary.TotalPrice,
                    summary.PendingItemCount);
                return false;
            }

            ClearPendingItems(inventory.Hands, actor.CharacterId, sourceScope, filterBySourceRoom);
            ClearPendingItems(inventory.Pockets, actor.CharacterId, sourceScope, filterBySourceRoom);
            ClearPendingItem(inventory.Backpack, actor.CharacterId, sourceScope, filterBySourceRoom);

            if (actor.Data != null)
            {
                actor.Data.AddMemory(CampusCharacterMemoryId.ClearedProtectedTransfer);
                actor.Data.AddMemory(CampusCharacterMemoryId.ReceivedClearedGoods);
            }

            message = FormatText(
                clearance.CompleteText,
                CampusProtectedTransferClearanceTextCatalog.Get(
                    CampusProtectedTransferClearanceTextId.ClearanceComplete),
                summary.TotalPrice,
                summary.PendingItemCount);
            return true;
        }

        public static bool TryResolveClearanceStation(
            string actionId,
            UnityEngine.Object source,
            out CampusServiceStation station)
        {
            station = default;
            return CampusServiceStationRuntimeAvailability.TryResolveActionStation(
                       actionId,
                       source,
                       out station) &&
                   station.Clearance.ClearsPendingProtectedTransfers;
        }

        public static bool TryRequireClearanceStationAvailable(
            string actionId,
            UnityEngine.Object source,
            out CampusServiceStation station,
            out string unavailableMessage)
        {
            unavailableMessage = string.Empty;
            if (!TryResolveClearanceStation(actionId, source, out station))
            {
                unavailableMessage = CampusProtectedTransferClearanceTextCatalog.Get(
                    CampusProtectedTransferClearanceTextId.UnsupportedStation);
                return false;
            }

            if (CampusServiceStationRuntimeAvailability.CanServeNow(station))
            {
                return true;
            }

            unavailableMessage = CampusServiceStationRuntimeAvailability.ResolveUnavailableMessage(station);
            return false;
        }

        private static CampusProtectedTransferClearanceSummary BuildSummary(
            CampusCharacterInventory inventory,
            ClearanceSourceScope sourceScope,
            bool filterBySourceRoom,
            bool chargeItemPrice)
        {
            if (inventory == null)
            {
                return default;
            }

            int pendingItemCount = 0;
            int totalPrice = 0;
            AccumulatePendingSummary(inventory.Hands, sourceScope, filterBySourceRoom, chargeItemPrice, ref pendingItemCount, ref totalPrice);
            AccumulatePendingSummary(inventory.Pockets, sourceScope, filterBySourceRoom, chargeItemPrice, ref pendingItemCount, ref totalPrice);
            AccumulatePendingSummary(inventory.Backpack, sourceScope, filterBySourceRoom, chargeItemPrice, ref pendingItemCount, ref totalPrice);
            return new CampusProtectedTransferClearanceSummary(pendingItemCount, totalPrice);
        }

        private static void AccumulatePendingSummary(
            StorageContainerModel[] containers,
            ClearanceSourceScope sourceScope,
            bool filterBySourceRoom,
            bool chargeItemPrice,
            ref int pendingItemCount,
            ref int totalPrice)
        {
            if (containers == null)
            {
                return;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                AccumulatePendingSummary(containers[i], sourceScope, filterBySourceRoom, chargeItemPrice, ref pendingItemCount, ref totalPrice);
            }
        }

        private static void AccumulatePendingSummary(
            StorageContainerModel container,
            ClearanceSourceScope sourceScope,
            bool filterBySourceRoom,
            bool chargeItemPrice,
            ref int pendingItemCount,
            ref int totalPrice)
        {
            if (container == null || container.Items == null)
            {
                return;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item == null ||
                    !item.IsPendingProtectedTransfer ||
                    !MatchesSourceRoom(item, sourceScope, filterBySourceRoom))
                {
                    continue;
                }

                pendingItemCount++;
                if (chargeItemPrice)
                {
                    totalPrice += Mathf.Max(0, item.Price);
                }
            }
        }

        private static int ClearPendingItems(
            StorageContainerModel[] containers,
            string actorId,
            ClearanceSourceScope sourceScope,
            bool filterBySourceRoom)
        {
            int clearedCount = 0;
            if (containers == null)
            {
                return clearedCount;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                clearedCount += ClearPendingItem(containers[i], actorId, sourceScope, filterBySourceRoom);
            }

            return clearedCount;
        }

        private static int ClearPendingItem(
            StorageContainerModel container,
            string actorId,
            ClearanceSourceScope sourceScope,
            bool filterBySourceRoom)
        {
            if (container == null || container.Items == null)
            {
                return 0;
            }

            int clearedCount = 0;
            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item == null ||
                    !item.IsPendingProtectedTransfer ||
                    !MatchesSourceRoom(item, sourceScope, filterBySourceRoom))
                {
                    continue;
                }

                CampusProtectedTransferState.ClearPendingTransfer(item, actorId);
                clearedCount++;
            }

            return clearedCount;
        }

        private static bool MatchesSourceRoom(StorageItemModel item, ClearanceSourceScope sourceScope, bool filterBySourceRoom)
        {
            if (item == null)
            {
                return false;
            }

            if (!filterBySourceRoom)
            {
                return true;
            }

            if (string.IsNullOrEmpty(sourceScope.RoomId) &&
                string.IsNullOrEmpty(sourceScope.LegacyRoomTypeId))
            {
                return true;
            }

            string itemSourceRoomId = NormalizeId(item.SourceRoomId);
            if (string.Equals(itemSourceRoomId, sourceScope.RoomId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Migration support for older pending items that stored a room type id
            // such as "RetailArea" or "Library" before stable room ids owned clearance.
            return !string.IsNullOrEmpty(sourceScope.LegacyRoomTypeId) &&
                   string.Equals(
                       itemSourceRoomId,
                       sourceScope.LegacyRoomTypeId,
                       System.StringComparison.OrdinalIgnoreCase);
        }

        private static ClearanceSourceScope ResolveSourceScope(CampusServiceStation station)
        {
            CampusGameplayRoom room = station.Room;
            return new ClearanceSourceScope(
                room != null ? room.RoomId : string.Empty,
                room != null ? room.RoomType : CampusRoomType.Unknown);
        }

        private static bool TrySpend(CampusCharacterRuntime actor, int totalPrice)
        {
            if (totalPrice <= 0)
            {
                return true;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusEconomyService economyService = bootstrap != null ? bootstrap.EconomyService : null;
            if (economyService != null)
            {
                return economyService.TrySpendMoney(actor, totalPrice);
            }

            return actor != null &&
                   actor.Data != null &&
                   actor.Data.TrySpendMoney(totalPrice);
        }

        private static string ResolveText(CampusLocalizedText text, string fallback)
        {
            return text.HasAnyText ? text.Current(fallback) : fallback;
        }

        private static string FormatText(CampusLocalizedText text, string fallback, int totalPrice, int itemCount)
        {
            string format = ResolveText(text, fallback);
            return string.Format(format, totalPrice, itemCount);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal sealed class CampusProtectedTransferClearanceCharacterActionProvider : ICampusCharacterActionProvider
    {
        public static readonly CampusProtectedTransferClearanceCharacterActionProvider Instance =
            new CampusProtectedTransferClearanceCharacterActionProvider();

        public string ProviderId => "campus_protected_transfer_clearance_character_actions";

        public bool TryExecute(CampusCharacterActionContext context, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (context.Actor == null)
            {
                return false;
            }

            Component source = ResolveComponent(context.Target);
            if (!CampusProtectedTransferClearanceService.TryResolveClearanceStation(
                    context.ActionId,
                    source,
                    out _))
            {
                return false;
            }

            if (!CampusProtectedTransferClearanceService.TryRequireClearanceStationAvailable(
                context.ActionId,
                source,
                out CampusServiceStation station,
                out string unavailableMessage))
            {
                result = StorageTransferResult.Fail(unavailableMessage);
                return true;
            }

            bool succeeded = CampusProtectedTransferClearanceService.TryClearPendingTransfers(
                context.Actor,
                station,
                out string message);
            result = succeeded
                ? CampusCharacterActionUtility.Success(message)
                : StorageTransferResult.Fail(message);
            return true;
        }

        private static Component ResolveComponent(UnityEngine.Object target)
        {
            if (target is Component component)
            {
                return component;
            }

            return target is GameObject gameObject ? gameObject.GetComponent<Component>() : null;
        }
    }

    internal sealed class CampusProtectedTransferClearanceInteractionProvider :
        ICampusInteractionActionProvider,
        ICampusInteractionPromptOverrideProvider
    {
        public static readonly CampusProtectedTransferClearanceInteractionProvider Instance =
            new CampusProtectedTransferClearanceInteractionProvider();

        public string ProviderId => "campus_protected_transfer_clearance_interactions";

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (!CampusProtectedTransferClearanceService.TryResolveClearanceStation(
                    context.ActionId,
                    context.SourceObject,
                    out _))
            {
                return false;
            }

            CampusCharacterRuntime actor = CampusCharacterActionUtility.ResolveActorRuntime(context.Actor);
            if (actor == null)
            {
                return false;
            }

            if (!CampusProtectedTransferClearanceService.TryRequireClearanceStationAvailable(
                    context.ActionId,
                    context.SourceObject,
                    out CampusServiceStation station,
                    out message))
            {
                return true;
            }

            bool succeeded = CampusProtectedTransferClearanceService.TryClearPendingTransfers(
                actor,
                station,
                out message);

            return true;
        }

        public bool TryResolvePrompt(CampusInteractionActionContext context, out string prompt)
        {
            prompt = string.Empty;
            if (!CampusProtectedTransferClearanceService.TryResolveClearanceStation(
                    context.ActionId,
                    context.SourceObject,
                    out CampusServiceStation station))
            {
                return false;
            }

            if (!CampusServiceStationRuntimeAvailability.CanServeNow(station))
            {
                prompt = CampusServiceStationRuntimeAvailability.ResolveUnavailableMessage(station);
                return true;
            }

            return false;
        }
    }

    internal enum CampusProtectedTransferClearanceTextId
    {
        ClearPrompt = 0,
        ClearanceComplete = 1,
        NoPendingItems = 2,
        InsufficientFunds = 3,
        UnsupportedStation = 4
    }

    internal static class CampusProtectedTransferClearanceTextCatalog
    {
        public static string Get(CampusProtectedTransferClearanceTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusProtectedTransferClearanceTextId id)
        {
            return id switch
            {
                CampusProtectedTransferClearanceTextId.ClearPrompt => Localize(language, "\u767b\u8bb0", "Register"),
                CampusProtectedTransferClearanceTextId.ClearanceComplete => Localize(language, "\u5df2\u5b8c\u6210\u767b\u8bb0\uff0c\u5904\u7406 {1} \u4ef6\uff0c\u82b1\u8d39 {0}\u3002", "Registration complete. Cleared {1} item(s), spent {0}."),
                CampusProtectedTransferClearanceTextId.NoPendingItems => Localize(language, "\u6ca1\u6709\u5f85\u767b\u8bb0\u7269\u54c1\u3002", "No pending items to register."),
                CampusProtectedTransferClearanceTextId.InsufficientFunds => Localize(language, "\u91d1\u94b1\u4e0d\u8db3\uff0c\u9700\u8981 {0}\u3002", "Not enough money. Need {0}."),
                CampusProtectedTransferClearanceTextId.UnsupportedStation => Localize(language, "\u8fd9\u4e2a\u670d\u52a1\u70b9\u4e0d\u80fd\u5904\u7406\u5f85\u767b\u8bb0\u7269\u54c1\u3002", "This service station cannot clear pending items."),
                _ => string.Empty
            };
        }

        private static string Localize(CampusDisplayLanguage language, string chinese, string english)
        {
            return CampusDisplayLanguageCatalog.Resolve(language, chinese, english);
        }
    }
}
