using UnityEngine;
using UnityEngine.UI;
using NtingCampus.Gameplay.UI;

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageDemoBootstrap : MonoBehaviour
    {
        private const string DemoSeedFlag = "storage_demo_seeded";

        public bool BackpackEquipped = true;
        public StorageItemRegistry ItemRegistry;

        private StorageWindowUI window;
        private StorageMemory memory;
        private StorageContainerModel[] hands;
        private StorageContainerModel[] pockets;
        private StorageContainerModel backpack;
        private StorageContainerModel testBox;

        public static void OpenDefaultTestBoxWindow(bool backpackEquipped = true)
        {
            StorageDemoBootstrap bootstrap = FindFirstObjectByType<StorageDemoBootstrap>();
            if (bootstrap == null)
            {
                GameObject bootstrapObject = new GameObject("StorageDemoBootstrap");
                bootstrap = bootstrapObject.AddComponent<StorageDemoBootstrap>();
            }

            bootstrap.BackpackEquipped = backpackEquipped;
            bootstrap.EnsureDemoData();
            bootstrap.EnsureWindow();
            bootstrap.window.OpenPlayerStorage(bootstrap.hands, bootstrap.pockets, bootstrap.backpack, bootstrap.BackpackEquipped, bootstrap.testBox, false);
        }

        private void Start()
        {
            EnsureDemoData();
        }

        [ContextMenu("Open Storage Demo")]
        public void OpenDemo()
        {
            EnsureDemoData();
            EnsureWindow();
            window.OpenPlayerStorage(hands, pockets, backpack, BackpackEquipped, testBox, false);
        }

        [ContextMenu("Save Storage Memory To PlayerPrefs")]
        public void SaveMemory()
        {
            EnsureDemoData();
            memory.SaveToPlayerPrefs();
        }

        [ContextMenu("Load Storage Memory From PlayerPrefs")]
        public void LoadMemory()
        {
            EnsureMemory();
            memory.LoadFromPlayerPrefs();
            EnsureDemoData();
            if (window != null && window.IsOpen)
            {
                window.OpenPlayerStorage(hands, pockets, backpack, BackpackEquipped, testBox, false);
            }
        }

        private void EnsureWindow()
        {
            window = FindFirstObjectByType<StorageWindowUI>();
            if (window != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Canvas_Storage", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(StorageWindowUI));
            window = canvasObject.GetComponent<StorageWindowUI>();
        }

        private void EnsureDemoData()
        {
            EnsureMemory();

            hands = StoragePlayerInventoryUtility.GetOrCreateHandContainers(memory);
            pockets = StoragePlayerInventoryUtility.GetOrCreatePocketContainers(memory);
            backpack = StoragePlayerInventoryUtility.GetOrCreateBackpack(memory);
            CampusLocalizedText testBoxName = new CampusLocalizedText(
                StorageTextCatalog.Get(CampusDisplayLanguage.Chinese, StorageTextId.TestBox),
                StorageTextCatalog.Get(CampusDisplayLanguage.English, StorageTextId.TestBox));
            testBox = memory.GetOrCreateContainer(
                "test_box",
                testBoxName.ResolvePrimary("test_box"),
                testBoxName,
                4,
                4,
                12f);

            if (!memory.IsSessionFlagSet(DemoSeedFlag))
            {
                SeedDemoItems();
                memory.SetSessionFlag(DemoSeedFlag);
            }
        }

        private void EnsureMemory()
        {
            memory = StorageMemory.GetOrCreate();
            ItemRegistry = ItemRegistry != null ? ItemRegistry : StoragePlayerInventoryUtility.EnsureRegistry(memory);
            memory.SetRegistry(ItemRegistry);
        }

        private void SeedDemoItems()
        {
            memory.TryPlaceNewItem(StoragePlayerInventoryUtility.LeftChestPocketContainerId, "phone", "phone_player_001", 0, 0);
            memory.TryPlaceNewItem(StoragePlayerInventoryUtility.LeftChestPocketContainerId, "note", "note_player_001", 1, 0);
            memory.TryPlaceNewItem(StoragePlayerInventoryUtility.RightChestPocketContainerId, "key", "key_player_001", 0, 0);
            memory.TryPlaceNewItem(StoragePlayerInventoryUtility.LeftPantsPocketContainerId, "snack", "snack_player_001", 0, 1);

            memory.TryPlaceNewItem(StoragePlayerInventoryUtility.BackpackContainerId, "textbook", "textbook_player_001", 0, 0);
            memory.TryPlaceNewItem(StoragePlayerInventoryUtility.BackpackContainerId, "workbook", "workbook_player_001", 2, 0);
            memory.TryPlaceNewItem(StoragePlayerInventoryUtility.BackpackContainerId, "pencil_case", "pencil_case_player_001", 2, 2);

            memory.TryPlaceNewItem("test_box", "lunch_box", "lunch_box_test_box_001", 0, 0);
        }
    }
}
