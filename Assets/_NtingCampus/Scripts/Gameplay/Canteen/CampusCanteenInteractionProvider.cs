using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenInteractionProvider :
        ICampusInteractionActionProvider,
        ICampusInteractionPromptOverrideProvider
    {
        public static readonly CampusCanteenInteractionProvider Instance =
            new CampusCanteenInteractionProvider();

        public string ProviderId => "campus_canteen_interactions";

        public bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            if (!CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.ServiceWindowUse))
            {
                return false;
            }

            CampusCharacterRuntime actor = CampusCharacterActionUtility.ResolveActorRuntime(context.Actor);
            CampusPlacedObject window = context.SourceObject;
            if (actor == null || window == null)
            {
                return false;
            }

            if (!IsWindowActive(window))
            {
                if (TryOpenUnattendedStock(actor, window))
                {
                    return true;
                }

                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowInactiveLog);
                WriteInteractionLog(message);
                return false;
            }

            if (actor.Data != null && actor.Data.IsPlayerControlled)
            {
                CampusCanteenOrderPanel.Open(actor, window, ParseMenuIds(context.Payload));
                return true;
            }

            CampusCanteenMenuItem menuItem = CampusCanteenMenuCatalog.ResolveDefault(ParseMenuIds(context.Payload));
            if (!CampusCanteenOrderService.TryPlaceOrder(actor, window, menuItem, out message))
            {
                WriteInteractionLog(message);
                return false;
            }

            return true;
        }

        public bool TryResolvePrompt(CampusInteractionActionContext context, out string prompt)
        {
            prompt = string.Empty;
            if (!CampusInteractionActionIds.Equals(context.ActionId, CampusInteractionActionIds.ServiceWindowUse))
            {
                return false;
            }

            if (!IsWindowActive(context.SourceObject))
            {
                prompt = HasProtectedStock(context.SourceObject)
                    ? ResolveUnattendedStockPrompt(context)
                    : CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowInactivePrompt);
                return true;
            }

            prompt = CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderMealPrompt);
            return true;
        }

        private static bool IsWindowActive(CampusPlacedObject window)
        {
            return CampusCanteenServiceWindowAvailability.IsAvailable(window);
        }

        private static bool TryOpenUnattendedStock(
            CampusCharacterRuntime actor,
            CampusPlacedObject window)
        {
            if (actor == null ||
                actor.Data == null ||
                !actor.Data.IsPlayerControlled ||
                window == null ||
                !HasProtectedStock(window))
            {
                return false;
            }

            CampusProtectedStockContainer stock = ResolveProtectedStock(window);
            bool opened = CampusObjectStorageInteraction.TryOpenStorageView(
                window,
                actor.gameObject,
                string.Empty,
                true,
                ResolveStockSourceLocation(window),
                stock != null ? stock.OwnerId : string.Empty,
                stock != null ? stock.SuspicionRisk : -1,
                () => CampusCanteenServiceWindowAvailability.IsAvailable(window));
            if (opened)
            {
                PublishUnattendedStockOpened(actor, window, stock);
            }

            return opened;
        }

        private static bool HasProtectedStock(CampusPlacedObject window)
        {
            return ResolveProtectedStock(window) != null;
        }

        private static CampusProtectedStockContainer ResolveProtectedStock(CampusPlacedObject window)
        {
            return window != null
                ? window.GetComponent<CampusProtectedStockContainer>() ??
                  window.GetComponentInParent<CampusProtectedStockContainer>()
                : null;
        }

        private static string ResolveUnattendedStockPrompt(CampusInteractionActionContext context)
        {
            CampusInteractionAnchor anchor = context.Anchor;
            if (anchor != null)
            {
                if (anchor.LocalizedPromptText.HasAnyText)
                {
                    return anchor.LocalizedPromptText.Current(anchor.PromptText);
                }

                if (!string.IsNullOrWhiteSpace(anchor.PromptText))
                {
                    return anchor.PromptText.Trim();
                }
            }

            return ResolveStockSourceLocation(context.SourceObject);
        }

        private static string ResolveStockSourceLocation(CampusPlacedObject window)
        {
            if (window == null)
            {
                return string.Empty;
            }

            if (window.LocalizedDisplayNameOverride.HasAnyText)
            {
                return window.LocalizedDisplayNameOverride.Current(window.DisplayName);
            }

            return window.DisplayName;
        }

        private static void PublishUnattendedStockOpened(
            CampusCharacterRuntime actor,
            CampusPlacedObject window,
            CampusProtectedStockContainer stock)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap == null || actor == null || window == null)
            {
                return;
            }

            string roomId = ResolveRoomId(window);
            int risk = stock != null ? Mathf.Max(1, stock.SuspicionRisk) : 8;
            if (bootstrap.GameState != null)
            {
                if (actor.Data != null && actor.Data.IsPlayerControlled)
                {
                    bootstrap.GameState.AddPlayerSuspicion(1);
                    bootstrap.GameState.AddPlayerTheftEvidence(1);
                }

                bootstrap.GameState.ApplyAreaDelta(roomId, 2, 1, 1, false, false);
            }

            bootstrap.GameplayEventHub?.PublishProtectedItemMoved(new CampusProtectedItemMovedEvent(
                0,
                actor.CharacterId,
                stock != null ? stock.OwnerId : string.Empty,
                string.Empty,
                "canteen_unattended_stock_access",
                ResolveStockSourceLocation(window),
                stock != null ? stock.ResolveStableContainerId(window) : string.Empty,
                string.Empty,
                roomId,
                actor.transform.position,
                Nting.Storage.StorageTransferReason.ScriptedTake,
                risk + 8));
        }

        private static string ResolveRoomId(CampusPlacedObject window)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            CampusGameplayRoom room = worldService != null && window != null
                ? worldService.FindRoomForPlacedObject(window)
                : null;
            return room != null ? room.RoomId : string.Empty;
        }

        private static string[] ParseMenuIds(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            string[] tokens = payload.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = tokens[i] != null ? tokens[i].Trim() : string.Empty;
            }

            return tokens;
        }

        private static void WriteInteractionLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
            }
        }
    }
}
