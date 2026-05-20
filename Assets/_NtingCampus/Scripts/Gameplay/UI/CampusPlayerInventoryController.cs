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
            CampusCharacterRuntime runtime = ResolvePlayerRuntime();
            CampusCharacterActionExecutor.TryExecute(
                runtime,
                CampusCharacterAction.OpenInventoryView(null, ResolvePlayerObject(runtime), backpackEquipped),
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
