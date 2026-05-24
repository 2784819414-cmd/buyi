using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusHandHudController : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusHandHudView view;
        private CampusCharacterRuntime observedPlayerRuntime;
        private StorageWindowUI observedWindow;
        private bool wasDragging;
        private int lastStateHash;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            EnsureView();
            Refresh(force: true);
        }

        private void OnEnable()
        {
            bootstrap = bootstrap != null ? bootstrap : CampusGameBootstrap.Instance;
            EnsureView();
            Refresh(force: true);
        }

        private void Update()
        {
            bootstrap = bootstrap != null ? bootstrap : CampusGameBootstrap.Instance;
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            CampusCharacterRuntime currentPlayerRuntime = ResolvePlayerRuntime();
            StorageWindowUI currentWindow = ResolveOpenWindow();
            bool isDragging = IsDragging(currentWindow);
            int stateHash = BuildStateHash(currentPlayerRuntime, currentWindow);
            bool changed = force ||
                           stateHash != lastStateHash ||
                           observedPlayerRuntime != currentPlayerRuntime ||
                           observedWindow != currentWindow ||
                           wasDragging != isDragging;

            observedPlayerRuntime = currentPlayerRuntime;
            observedWindow = currentWindow;
            wasDragging = isDragging;
            lastStateHash = stateHash;

            EnsureView();
            if (view == null)
            {
                return;
            }

            if (isDragging)
            {
                return;
            }

            if (!changed)
            {
                return;
            }

            view.Apply(CampusHandInventoryUtility.ResolveHands(currentPlayerRuntime), currentWindow);
        }

        private static bool IsDragging(StorageWindowUI currentWindow)
        {
            return currentWindow != null &&
                   currentWindow.DragController != null &&
                   currentWindow.DragController.IsDragging;
        }

        private static int BuildStateHash(CampusCharacterRuntime runtime, StorageWindowUI currentWindow)
        {
            unchecked
            {
                int hash = 17;
                hash = CombineHash(hash, runtime != null ? runtime.CharacterId : string.Empty);
                hash = CombineHash(hash, currentWindow != null ? currentWindow.GetInstanceID() : 0);
                hash = CombineHash(hash, currentWindow != null && currentWindow.IsOpen ? 1 : 0);
                hash = CombineHash(hash, CampusHandInventoryUtility.BuildHandsStateHash(runtime));
                return hash;
            }
        }

        private static int CombineHash(int hash, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return CombineHash(hash, 0);
            }

            for (int i = 0; i < value.Length; i++)
            {
                hash = CombineHash(hash, value[i]);
            }

            return hash;
        }

        private static int CombineHash(int hash, int value)
        {
            unchecked
            {
                return hash * 31 + value;
            }
        }

        private CampusCharacterRuntime ResolvePlayerRuntime()
        {
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
        }

        private static StorageWindowUI ResolveOpenWindow()
        {
            StorageWindowUI window = FindFirstObjectByType<StorageWindowUI>(FindObjectsInactive.Include);
            return window != null && window.IsOpen ? window : null;
        }

        private void EnsureView()
        {
            if (view != null)
            {
                return;
            }

            view = GetComponent<CampusHandHudView>();
            if (view == null)
            {
                view = gameObject.AddComponent<CampusHandHudView>();
            }
        }
    }
}
