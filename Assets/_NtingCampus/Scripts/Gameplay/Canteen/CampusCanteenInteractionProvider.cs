using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
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

            CampusCharacterRuntime actor = ResolveActorRuntime(context.Actor);
            CampusPlacedObject window = context.SourceObject;
            if (actor == null || window == null)
            {
                return false;
            }

            if (!IsWindowActive(window))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowInactiveLog);
                WriteInteractionLog(message);
                return false;
            }

            if (!CampusCanteenMealRules.CanOrderMeal(actor))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.HandsFullLog);
                WriteInteractionLog(message);
                return false;
            }

            if (!TryGiveMealToActor(actor, window, out message))
            {
                WriteInteractionLog(message);
                return false;
            }

            message = string.IsNullOrWhiteSpace(message)
                ? CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderedMealLog)
                : message;
            if (context.SourceInteractable == null)
            {
                WriteInteractionLog(message);
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

            prompt = IsWindowActive(context.SourceObject)
                ? CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderMealPrompt)
                : CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowInactivePrompt);
            return true;
        }

        private static bool TryGiveMealToActor(
            CampusCharacterRuntime actor,
            CampusPlacedObject window,
            out string message)
        {
            message = string.Empty;
            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                return false;
            }

            StorageItemRegistry registry = CampusCharacterInventoryService.EnsureRegistry(memory);
            StorageItemModel meal = registry.CreateItem(
                "lunch_box",
                actor.CharacterId + ".canteen_meal." + Guid.NewGuid().ToString("N"));
            if (meal == null)
            {
                return false;
            }

            meal.OwnerId = actor.CharacterId;
            meal.LegalState = StorageItemLegalState.Personal;
            meal.SourceLocation = "canteen_window";
            meal.SourceRoomId = ResolveRoomId(window);

            StorageTransferContext context = StorageTransferContext.ForActor(actor.gameObject, StorageTransferReason.ScriptedTake);
            context.SuppressNpcDetection = true;
            context.SuppressSuspicion = true;
            context.OwnerId = actor.CharacterId;
            context.SourceLocation = meal.SourceLocation;

            bool pickedUp = CampusInventoryTransferService.Resolve().TryPickUpIntoHands(memory, meal, context, out StorageTransferResult result);
            message = result.Message;
            return pickedUp;
        }

        private static bool IsWindowActive(CampusPlacedObject window)
        {
            if (window == null || CampusFacilityTypeResolver.Resolve(window) != CampusFacilityType.ServiceWindow)
            {
                return false;
            }

            return CampusCanteenServiceWindowAvailability.IsAvailable(window);
        }

        private static CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            if (actor != null)
            {
                CampusCharacterRuntime runtime = actor.GetComponentInParent<CampusCharacterRuntime>();
                if (runtime != null)
                {
                    return runtime;
                }
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
        }

        private static string ResolveRoomId(CampusPlacedObject placedObject)
        {
            CampusWorldService worldService = CampusGameBootstrap.Instance != null
                ? CampusGameBootstrap.Instance.WorldService
                : null;
            CampusGameplayRoom room = worldService != null ? worldService.FindRoomForPosition(placedObject.FloorIndex, placedObject.transform.position) : null;
            return room != null ? room.RoomId : string.Empty;
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
