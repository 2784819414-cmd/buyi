using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenStockActions
    {
        private readonly CampusCanteenFacts facts;
        private readonly CampusCanteenStockService stockService;
        private readonly Func<CampusCanteenMenuProfile> resolveMenu;
        private readonly Func<CampusCanteenDishFactory> resolveDishFactory;

        public CampusCanteenStockActions(
            CampusCanteenFacts facts,
            CampusCanteenStockService stockService,
            Func<CampusCanteenMenuProfile> resolveMenu,
            Func<CampusCanteenDishFactory> resolveDishFactory)
        {
            this.facts = facts;
            this.stockService = stockService;
            this.resolveMenu = resolveMenu;
            this.resolveDishFactory = resolveDishFactory;
        }

        public bool OpenStockStorage(GameObject actor, CampusPlacedObject sourceObject, out string message)
        {
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

            StorageContainerModel container = stockService != null
                ? stockService.GetOrCreateFoodBoxContainer(StorageMemory.GetOrCreate(), station, ResolveMenu())
                : null;
            if (container == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoStockContainer);
                return false;
            }

            return CampusInventoryActionExecutor.TryOpenInventoryView(
                actorRuntime,
                container,
                sourceObject != null ? sourceObject.gameObject : actorRuntime.gameObject,
                true,
                out message);
        }

        public bool OpenStockStorage(GameObject actor, string stationId, out string message)
        {
            message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoStockContainer);
            return facts != null &&
                   facts.TryGetStation(stationId, out CampusCanteenStation station) &&
                   OpenStockStorage(actor, station.FoodBoxObject, out message);
        }

        public bool TryStealStockDish(GameObject actor, string dishId, out string message)
        {
            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            if (actorRuntime == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoCharacter);
                return false;
            }

            if (facts == null ||
                !facts.TryFindNearestStation(actorRuntime.transform.position, false, out CampusCanteenStation station))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoServingWindow);
                return false;
            }

            CampusCanteenMenuProfile menu = ResolveMenu();
            if (menu == null ||
                !menu.TryGetDishForWindow(dishId, station.WindowTypeId, out CampusCanteenDishDefinition dish))
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.NoMatchingFood);
                return false;
            }

            CampusCanteenDishFactory dishFactory = resolveDishFactory != null ? resolveDishFactory() : null;
            StorageItemModel item = dishFactory != null
                ? dishFactory.CreateStockDish(StorageMemory.GetOrCreate(), dish, station)
                : null;
            if (item == null)
            {
                message = CampusCanteenTextCatalog.Get(CampusCanteenTextId.CouldNotCreateFood);
                return false;
            }

            item.LegalState = StorageItemLegalState.Stolen;
            item.StolenDuringSession = true;
            StorageTransferContext context = StorageTransferContext.ForActor(
                actorRuntime.gameObject,
                StorageTransferReason.PrankTheft);
            context.ForceIllegal = true;
            context.OwnerId = station.StationId;
            context.SourceLocation = station.DisplayName;
            context.SuspicionRiskOverride = item.SuspicionRisk;

            if (!CampusInventoryTransferService.Resolve().TryPickUpIntoHands(
                    StorageMemory.GetOrCreate(),
                    item,
                    context,
                    out StorageTransferResult result))
            {
                message = result.Message;
                return false;
            }

            message = CampusCanteenTextCatalog.Format(
                CampusCanteenTextId.CustomerTookMeal,
                FormatActorName(actorRuntime),
                item.GetDisplayName(CampusLanguageState.CurrentLanguage));
            return true;
        }

        private CampusCanteenMenuProfile ResolveMenu()
        {
            return resolveMenu != null ? resolveMenu() : null;
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
