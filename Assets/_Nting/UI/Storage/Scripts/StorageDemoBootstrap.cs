using UnityEngine;
using UnityEngine.UI;

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
            bootstrap.window.Open(bootstrap.pockets, bootstrap.backpack, bootstrap.BackpackEquipped, bootstrap.testBox);
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
            window.Open(pockets, backpack, BackpackEquipped, testBox);
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
                window.Open(pockets, backpack, BackpackEquipped, testBox);
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

            pockets = new[]
            {
                memory.GetOrCreateContainer("pocket_left_chest", "左胸袋", 2, 3, 1.5f),
                memory.GetOrCreateContainer("pocket_right_chest", "右胸袋", 2, 3, 1.5f),
                memory.GetOrCreateContainer("pocket_left_pants", "左裤袋", 2, 3, 2f),
                memory.GetOrCreateContainer("pocket_right_pants", "右裤袋", 2, 3, 2f)
            };

            backpack = memory.GetOrCreateContainer("school_backpack", "学生书包", 5, 6, 20f);
            testBox = memory.GetOrCreateContainer("test_box", "测试箱", 4, 4, 12f);

            if (!memory.IsSessionFlagSet(DemoSeedFlag))
            {
                SeedDemoItems();
                memory.SetSessionFlag(DemoSeedFlag);
            }
        }

        private void EnsureMemory()
        {
            memory = StorageMemory.GetOrCreate();
            if (ItemRegistry == null)
            {
                ItemRegistry = Resources.Load<StorageItemRegistry>("StorageItemRegistry");
            }

            if (ItemRegistry == null)
            {
                ItemRegistry = StorageItemRegistry.CreateDemoRegistry();
            }

            memory.SetRegistry(ItemRegistry);
        }

        private void SeedDemoItems()
        {
            memory.TryPlaceNewItem("pocket_left_chest", "phone", "phone_player_001", 0, 0);
            memory.TryPlaceNewItem("pocket_left_chest", "note", "note_player_001", 1, 0);
            memory.TryPlaceNewItem("pocket_right_chest", "key", "key_player_001", 0, 0);
            memory.TryPlaceNewItem("pocket_left_pants", "snack", "snack_player_001", 0, 1);

            memory.TryPlaceNewItem("school_backpack", "textbook", "textbook_player_001", 0, 0);
            memory.TryPlaceNewItem("school_backpack", "workbook", "workbook_player_001", 2, 0);
            memory.TryPlaceNewItem("school_backpack", "pencil_case", "pencil_case_player_001", 2, 2);

            memory.TryPlaceNewItem("test_box", "lunch_box", "lunch_box_test_box_001", 0, 0);
        }
    }
}
