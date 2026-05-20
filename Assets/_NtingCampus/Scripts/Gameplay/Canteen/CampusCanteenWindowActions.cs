using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenWindowActions
    {
        private readonly CampusCanteenFacts facts;
        private readonly Func<CampusCanteenMenuProfile> resolveMenu;
        private readonly Func<CampusCanteenDishFactory> resolveDishFactory;
        private readonly Action<CampusCharacterRuntime> markMealReceived;
        private readonly Action<string> writeLog;

        public CampusCanteenWindowActions(
            CampusCanteenFacts facts,
            Func<CampusCanteenMenuProfile> resolveMenu,
            Func<CampusCanteenDishFactory> resolveDishFactory,
            Action<CampusCharacterRuntime> markMealReceived,
            Action<string> writeLog)
        {
            this.facts = facts;
            this.resolveMenu = resolveMenu;
            this.resolveDishFactory = resolveDishFactory;
            this.markMealReceived = markMealReceived;
            this.writeLog = writeLog;
        }

        public bool InteractWithServingWindow(
            GameObject actor,
            CampusPlacedObject sourceObject,
            out string message)
        {
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowUnavailable);
            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            if (actorRuntime == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoCharacter);
                return false;
            }

            if (facts == null || !facts.TryResolveStation(sourceObject, out CampusCanteenStation station))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoServingWindow);
                return false;
            }

            return TryUseWindowLikePlayer(actorRuntime, station, out message);
        }

        public bool TryUseWindowLikePlayer(
            CampusCharacterRuntime actor,
            CampusCanteenStation station,
            out string message)
        {
            if (CampusCanteenFacts.IsCanteenClerk(actor))
            {
                return TryPrepareMealAtWindow(actor, station, out message);
            }

            return TryTakeMealAtWindow(actor, station, out message);
        }

        public bool TryPrepareMealAtWindow(
            CampusCharacterRuntime clerk,
            CampusCanteenStation station,
            out string message)
        {
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowUnavailable);
            if (facts == null ||
                !facts.ValidateWindowUse(clerk, station, station != null ? station.ClerkPosition : Vector3.zero, out message))
            {
                return false;
            }

            if (!CampusCanteenFacts.IsCanteenClerk(clerk))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoClerkAtStation);
                return false;
            }

            if (facts.HasFoodAtStation(station))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.MealAlreadyOnCounter);
                return false;
            }

            CampusCanteenDishDefinition dish = SelectDishForStation(station);
            if (dish == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoMatchingFood);
                return false;
            }

            CampusCanteenDishFactory dishFactory = resolveDishFactory != null ? resolveDishFactory() : null;
            StorageItemModel item = dishFactory != null
                ? dishFactory.CreateServedDish(StorageMemory.GetOrCreate(), dish, string.Empty, station.DisplayName)
                : null;
            if (item == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.CouldNotCreateFood);
                return false;
            }

            item.OwnerId = string.Empty;
            item.SourceRoomId = station.RoomId;
            item.SourceContainerId = station.CounterContainerId;
            item.SourceLocation = station.DisplayName;
            item.LegalState = StorageItemLegalState.Public;
            item.AllowTaking = true;

            GameObject context = station.WindowObject != null ? station.WindowObject.gameObject : clerk.gameObject;
            if (!CampusStorageGroundItemUtility.TryPlaceItemAtWorldPosition(
                    context,
                    item,
                    station.MealDropPosition,
                    out string dropError))
            {
                message = string.IsNullOrWhiteSpace(dropError)
                    ? CampusCanteenTextCatalog.Get(CampusCanteenTextId.CouldNotPlaceMeal)
                    : dropError;
                return false;
            }

            message = CampusCanteenTextCatalog.Format(
                CampusCanteenTextId.ClerkPreparedMeal,
                FormatActorName(clerk),
                item.GetDisplayName(CampusLanguageState.CurrentLanguage),
                station.DisplayName);
            writeLog?.Invoke(message);
            return true;
        }

        public bool TryTakeMealAtWindow(
            CampusCharacterRuntime customer,
            CampusCanteenStation station,
            out string message)
        {
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowUnavailable);
            if (facts == null ||
                !facts.ValidateWindowUse(customer, station, station != null ? station.CustomerPosition : Vector3.zero, out message))
            {
                return false;
            }

            if (markMealReceived == null || facts == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.WindowUnavailable);
                return false;
            }

            if (facts.HasReceivedMealToday(customer))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.AlreadyReceivedMeal);
                return false;
            }

            if (!facts.TryFindFoodAtStation(station, out CampusDroppedStorageItem droppedItem))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoMealAtWindow);
                return false;
            }

            string itemName = droppedItem.LocalizedDisplayName.HasAnyText
                ? droppedItem.LocalizedDisplayName.Current(droppedItem.DisplayName, droppedItem.DefinitionId)
                : !string.IsNullOrWhiteSpace(droppedItem.DisplayName)
                    ? droppedItem.DisplayName
                    : droppedItem.DefinitionId;
            if (!CampusCharacterActionExecutor.TryPickUpDroppedItem(customer, droppedItem, out StorageTransferResult result))
            {
                message = result.Message;
                return false;
            }

            markMealReceived(customer);
            message = CampusCanteenTextCatalog.Format(
                CampusCanteenTextId.CustomerTookMeal,
                FormatActorName(customer),
                itemName);
            writeLog?.Invoke(message);
            return true;
        }

        private CampusCanteenDishDefinition SelectDishForStation(CampusCanteenStation station)
        {
            CampusCanteenMenuProfile menu = resolveMenu != null ? resolveMenu() : null;
            if (menu == null || menu.Items == null)
            {
                return null;
            }

            var dishes = menu.Items;
            for (int i = 0; i < dishes.Count; i++)
            {
                CampusCanteenDishDefinition dish = dishes[i];
                if (menu.CanServeDishAtWindow(dish, station != null ? station.WindowTypeId : string.Empty))
                {
                    return dish;
                }
            }

            return null;
        }

        private static CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            return actor != null ? actor.GetComponentInParent<CampusCharacterRuntime>() : null;
        }

        private static string FormatActorName(CampusCharacterRuntime runtime)
        {
            return runtime != null && runtime.Data != null
                ? runtime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)
                : CampusCanteenTextCatalog.Get(CampusCanteenTextId.UnknownActor);
        }
    }
}
