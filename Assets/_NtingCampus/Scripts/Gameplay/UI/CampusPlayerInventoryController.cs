using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class CampusPlayerInventoryController : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private KeyCode backpackKey = KeyCode.B;
        [SerializeField] private bool backpackEquipped = true;

        public KeyCode BackpackKey => backpackKey;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
        }

        private void Awake()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }
        }

        private void Update()
        {
            if (CampusInteractionInput.WasKeyPressed(backpackKey))
            {
                ToggleBackpack();
            }
        }

        public void ToggleBackpack()
        {
            StorageWindowUI existingWindow = FindFirstObjectByType<StorageWindowUI>(FindObjectsInactive.Include);
            if (existingWindow != null && existingWindow.IsOpen)
            {
                existingWindow.Close();
                return;
            }

            OpenBackpack();
        }

        public void OpenBackpack()
        {
            StorageMemory memory = StorageMemory.GetOrCreate();
            StoragePlayerInventoryUtility.EnsureRegistry(memory);
            StorageContainerModel[] hands = StoragePlayerInventoryUtility.GetOrCreateHandContainers(memory);
            StorageContainerModel[] pockets = StoragePlayerInventoryUtility.GetOrCreatePocketContainers(memory);
            StorageContainerModel backpack = StoragePlayerInventoryUtility.GetOrCreateBackpack(memory);
            StoragePlayerInventoryUtility.EnsureStarterItems(memory);

            StorageWindowUI window = EnsureWindow();
            window.SetGroundDropContext(ResolvePlayerObject());
            window.OpenPlayerStorage(hands, pockets, backpack, backpackEquipped, null, true);
        }

        private StorageWindowUI EnsureWindow()
        {
            StorageWindowUI window = FindFirstObjectByType<StorageWindowUI>(FindObjectsInactive.Include);
            if (window != null)
            {
                return window;
            }

            GameObject windowObject = new GameObject("Canvas_Storage", typeof(RectTransform), typeof(StorageWindowUI));
            return windowObject.GetComponent<StorageWindowUI>();
        }

        private GameObject ResolvePlayerObject()
        {
            CampusCharacterRuntime runtime = bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
            if (runtime != null)
            {
                return runtime.gameObject;
            }

            CampusPlayerCharacter playerCharacter = FindFirstObjectByType<CampusPlayerCharacter>(FindObjectsInactive.Include);
            if (playerCharacter != null)
            {
                return playerCharacter.gameObject;
            }

            return gameObject;
        }
    }
}
