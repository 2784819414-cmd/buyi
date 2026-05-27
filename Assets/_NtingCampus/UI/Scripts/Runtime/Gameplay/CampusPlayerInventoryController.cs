using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusPlayerInventoryController : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;

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
            if (CampusGameplayInputBindings.WasPressed(CampusGameplayInputActionId.Backpack))
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
            CampusCharacterRuntime runtime = ResolvePlayerRuntime();
            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(runtime, true);
            CampusPlayerInventoryViewService.TryOpen(
                runtime,
                null,
                ResolvePlayerObject(runtime),
                inventory != null && inventory.HasBackpack,
                out _);
        }

        private CampusCharacterRuntime ResolvePlayerRuntime()
        {
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
        }

        private GameObject ResolvePlayerObject(CampusCharacterRuntime runtime)
        {
            if (runtime != null)
            {
                return runtime.gameObject;
            }

            CampusPlayerCharacter playerCharacter = CampusPlayerCharacter.FindCurrent();
            if (playerCharacter != null)
            {
                return playerCharacter.gameObject;
            }

            return gameObject;
        }
    }
}

