using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    [DisallowMultipleComponent]
    public sealed class CampusEconomyService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
        }

        public int GetBalance(CampusCharacterRuntime actor)
        {
            return actor != null && actor.Data != null ? actor.Data.Money : 0;
        }

        public void AddMoney(CampusCharacterRuntime actor, int amount)
        {
            if (actor == null || actor.Data == null)
            {
                return;
            }

            actor.Data.AddMoney(amount);
        }

        public bool TrySpendMoney(CampusCharacterRuntime actor, int amount)
        {
            return actor != null &&
                   actor.Data != null &&
                   actor.Data.TrySpendMoney(amount);
        }

        public bool TryTransferMoney(
            CampusCharacterRuntime source,
            CampusCharacterRuntime target,
            int amount)
        {
            if (source == null ||
                source.Data == null ||
                target == null ||
                target.Data == null ||
                amount <= 0)
            {
                return false;
            }

            if (!source.Data.TrySpendMoney(amount))
            {
                return false;
            }

            target.Data.AddMoney(amount);
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CampusDailyBusinessSettlementService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField, Min(0)] private int areaPressureDailyDecay = 8;

        private CampusTimeController subscribedTimeController;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            Subscribe(bootstrap != null ? bootstrap.TimeController : null);
        }

        private void OnDisable()
        {
            Subscribe(null);
        }

        private void Subscribe(CampusTimeController timeController)
        {
            if (subscribedTimeController == timeController)
            {
                return;
            }

            if (subscribedTimeController != null)
            {
                subscribedTimeController.DailySettlementStarted -= HandleDailySettlementStarted;
            }

            subscribedTimeController = timeController;
            if (subscribedTimeController != null)
            {
                subscribedTimeController.DailySettlementStarted += HandleDailySettlementStarted;
            }
        }

        private void HandleDailySettlementStarted(CampusGameDate date)
        {
            CampusGameState state = bootstrap != null ? bootstrap.GameState : null;
            if (state == null)
            {
                return;
            }

            int decay = Mathf.Max(0, areaPressureDailyDecay);
            state.DecayAreaStates(decay);

            CampusEventLog eventLog = bootstrap != null ? bootstrap.EventLog : null;
            eventLog?.AddLog(CampusCoreTextCatalog.Format(
                CampusCoreTextId.DailyBusinessSettlementSummary,
                state.Day,
                decay));
        }
    }
}
