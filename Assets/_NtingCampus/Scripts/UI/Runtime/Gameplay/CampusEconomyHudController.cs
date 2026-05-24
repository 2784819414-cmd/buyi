using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Retail;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusEconomyHudController : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusEconomyHudView view;
        [SerializeField, Min(0.05f)] private float pendingRefreshInterval = 0.15f;

        private CampusCharacterRuntime observedPlayerRuntime;
        private CampusResourceState observedResourceState;
        private CampusEconomyHudSnapshot lastSnapshot;
        private bool hasSnapshot;
        private bool languageSubscribed;
        private float nextPendingRefreshAt;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            EnsureView();
            BindGlobalState();
            BindPlayerRuntime();
            RefreshSnapshot(true);
        }

        private void OnEnable()
        {
            bootstrap = bootstrap != null ? bootstrap : CampusGameBootstrap.Instance;
            EnsureView();
            BindGlobalState();
            BindPlayerRuntime();
            RefreshSnapshot(true);
        }

        private void OnDisable()
        {
            UnbindPlayerRuntime();
            UnbindGlobalState();
        }

        private void OnDestroy()
        {
            UnbindPlayerRuntime();
            UnbindGlobalState();
        }

        private void Update()
        {
            bootstrap = bootstrap != null ? bootstrap : CampusGameBootstrap.Instance;
            if (bootstrap == null)
            {
                return;
            }

            BindGlobalState();
            BindPlayerRuntime();
            if (Time.unscaledTime < nextPendingRefreshAt)
            {
                return;
            }

            RefreshSnapshot(false);
        }

        private void OnValidate()
        {
            pendingRefreshInterval = Mathf.Max(0.05f, pendingRefreshInterval);
        }

        private void BindGlobalState()
        {
            CampusResourceState currentResourceState = bootstrap != null ? bootstrap.ResourceState : null;
            if (observedResourceState != currentResourceState)
            {
                if (observedResourceState != null)
                {
                    observedResourceState.DivinePowerChanged -= HandleDivinePowerChanged;
                }

                observedResourceState = currentResourceState;
                if (observedResourceState != null)
                {
                    observedResourceState.DivinePowerChanged += HandleDivinePowerChanged;
                }
            }

            if (languageSubscribed)
            {
                return;
            }

            CampusLanguageState.LanguageChanged += HandleLanguageChanged;
            languageSubscribed = true;
        }

        private void UnbindGlobalState()
        {
            if (observedResourceState != null)
            {
                observedResourceState.DivinePowerChanged -= HandleDivinePowerChanged;
                observedResourceState = null;
            }

            if (!languageSubscribed)
            {
                return;
            }

            CampusLanguageState.LanguageChanged -= HandleLanguageChanged;
            languageSubscribed = false;
        }

        private void BindPlayerRuntime()
        {
            CampusCharacterRuntime currentPlayerRuntime = bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
            if (observedPlayerRuntime == currentPlayerRuntime)
            {
                return;
            }

            if (observedPlayerRuntime != null && observedPlayerRuntime.Data != null)
            {
                observedPlayerRuntime.Data.Economy.MoneyChanged -= HandleMoneyChanged;
            }

            observedPlayerRuntime = currentPlayerRuntime;
            if (observedPlayerRuntime != null && observedPlayerRuntime.Data != null)
            {
                observedPlayerRuntime.Data.Economy.MoneyChanged += HandleMoneyChanged;
            }

            RefreshSnapshot(true);
        }

        private void UnbindPlayerRuntime()
        {
            if (observedPlayerRuntime != null && observedPlayerRuntime.Data != null)
            {
                observedPlayerRuntime.Data.Economy.MoneyChanged -= HandleMoneyChanged;
            }

            observedPlayerRuntime = null;
        }

        private void HandleMoneyChanged(int _)
        {
            RefreshSnapshot(false);
        }

        private void HandleDivinePowerChanged(int _)
        {
            RefreshSnapshot(false);
        }

        private void HandleLanguageChanged(CampusDisplayLanguage _)
        {
            RefreshSnapshot(true);
        }

        private void RefreshSnapshot(bool immediate)
        {
            nextPendingRefreshAt = Time.unscaledTime + pendingRefreshInterval;

            CampusRetailCheckoutSummary pendingSummary = observedPlayerRuntime != null
                ? CampusRetailService.BuildPendingSummary(observedPlayerRuntime)
                : default;
            int playerMoney = observedPlayerRuntime != null && observedPlayerRuntime.Data != null
                ? observedPlayerRuntime.Data.Money
                : 0;
            int divinePower = observedResourceState != null
                ? observedResourceState.DivinePower
                : 0;
            bool hasPendingCheckout = pendingSummary.PendingItemCount > 0;
            CampusEconomyHudSnapshot snapshot = new CampusEconomyHudSnapshot(
                playerMoney,
                divinePower,
                pendingSummary.PendingItemCount,
                pendingSummary.TotalPrice,
                !hasPendingCheckout || playerMoney >= pendingSummary.TotalPrice,
                hasPendingCheckout);

            if (!immediate && hasSnapshot && snapshot.Equals(lastSnapshot))
            {
                return;
            }

            lastSnapshot = snapshot;
            hasSnapshot = true;
            EnsureView();
            if (view != null)
            {
                view.Apply(snapshot, immediate);
            }
        }

        private void EnsureView()
        {
            if (view != null)
            {
                return;
            }

            view = GetComponent<CampusEconomyHudView>();
            if (view == null)
            {
                view = gameObject.AddComponent<CampusEconomyHudView>();
            }
        }
    }
}

