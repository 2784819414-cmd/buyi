using System.Collections.Generic;
using Nting.Storage;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    public sealed class CampusCanteenStockService
    {
        private static readonly StorageItemModel[] EmptyStockItems = System.Array.Empty<StorageItemModel>();
        private static readonly CampusCanteenDishDefinition[] EmptyMenuItems =
            System.Array.Empty<CampusCanteenDishDefinition>();

        private readonly CampusCanteenDishFactory dishFactory;

        public CampusCanteenStockService(CampusCanteenDishFactory dishFactory)
        {
            this.dishFactory = dishFactory;
        }

        public StorageContainerModel GetOrCreateStockContainer(
            StorageMemory memory,
            CampusCanteenStation station,
            CampusCanteenMenuProfile menu)
        {
            return GetOrCreateFoodBoxContainer(memory, station, menu);
        }

        public StorageContainerModel GetOrCreateFoodBoxContainer(
            StorageMemory memory,
            CampusCanteenStation station,
            CampusCanteenMenuProfile menu)
        {
            if (memory == null || station == null)
            {
                return null;
            }

            if (!station.HasFoodBox)
            {
                return null;
            }

            StorageContainerModel container = memory.GetOrCreateContainer(
                station.FoodBoxContainerId,
                CampusCanteenTextCatalog.Format(CampusCanteenTextId.FoodBoxName, station.DisplayName),
                4,
                4,
                36f);
            ConfigureProtectedContainer(container, station, StorageContainerAccessPolicy.Commerce);
            SeedStockIfEmpty(memory, container, station, menu);
            return container;
        }

        public bool TryFindStockItem(
            StorageContainerModel stock,
            string itemInstanceId,
            out StorageItemModel item)
        {
            item = null;
            if (stock == null || stock.Items == null || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return false;
            }

            for (int i = 0; i < stock.Items.Count; i++)
            {
                StorageItemModel candidate = stock.Items[i];
                if (candidate != null &&
                    string.Equals(candidate.InstanceId, itemInstanceId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                {
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<StorageItemModel> GetStockItems(
            StorageMemory memory,
            CampusCanteenStation station,
            CampusCanteenMenuProfile menu)
        {
            StorageContainerModel stock = GetOrCreateFoodBoxContainer(memory, station, menu);
            return stock != null ? stock.Items : EmptyStockItems;
        }

        public IReadOnlyList<CampusCanteenDishDefinition> GetMenuItems(
            CampusCanteenMenuProfile menu,
            CampusCanteenStation station)
        {
            if (menu == null || menu.Items == null)
            {
                return EmptyMenuItems;
            }

            if (station == null)
            {
                return menu.Items;
            }

            List<CampusCanteenDishDefinition> result = new List<CampusCanteenDishDefinition>();
            IReadOnlyList<CampusCanteenDishDefinition> dishes = menu.Items;
            for (int i = 0; i < dishes.Count; i++)
            {
                CampusCanteenDishDefinition dish = dishes[i];
                if (menu.CanServeDishAtWindow(dish, station.WindowTypeId))
                {
                    result.Add(dish);
                }
            }

            return result;
        }

        private static void ConfigureProtectedContainer(
            StorageContainerModel container,
            CampusCanteenStation station,
            StorageContainerAccessPolicy policy)
        {
            if (container == null || station == null)
            {
                return;
            }

            container.AccessPolicy = policy;
            container.OwnerId = station.StationId;
            container.OwnerRole = "Canteen";
            container.RoomId = station.RoomId;
            container.AllowTakingContents = false;
            container.IsPlayerCarried = false;
            container.SuspicionRisk = 18;
        }

        private void SeedStockIfEmpty(
            StorageMemory memory,
            StorageContainerModel stock,
            CampusCanteenStation station,
            CampusCanteenMenuProfile menu)
        {
            if (stock == null || stock.Items.Count > 0 || menu == null || menu.Items == null)
            {
                return;
            }

            List<CampusCanteenDishDefinition> stocked = new List<CampusCanteenDishDefinition>();
            IReadOnlyList<CampusCanteenDishDefinition> dishes = menu.Items;
            for (int i = 0; i < dishes.Count; i++)
            {
                CampusCanteenDishDefinition dish = dishes[i];
                if (dish != null &&
                    dish.StockedInTray &&
                    menu.CanServeDishAtWindow(dish, station != null ? station.WindowTypeId : "generic"))
                {
                    stocked.Add(dish);
                }
            }

            for (int i = 0; i < stocked.Count; i++)
            {
                StorageItemModel item = dishFactory.CreateStockDish(memory, stocked[i], station);
                if (item == null)
                {
                    continue;
                }

                if (stock.FindFirstFit(item, out Vector2Int position))
                {
                    stock.PlaceItem(item, position.x, position.y);
                }
            }
        }
    }
}
