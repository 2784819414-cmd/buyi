using UnityEngine;
using NtingCampus.Gameplay.Core;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusPlayerCharacter : MonoBehaviour
    {
        [SerializeField] private string characterId = string.Empty;
        [SerializeField] private CampusCharacterRuntime characterRuntime;

        public string CharacterId => characterId;
        public CampusCharacterRuntime CharacterRuntime => characterRuntime;
        public bool IsCurrentPlayer =>
            characterRuntime != null &&
            characterRuntime.Data != null &&
            characterRuntime.Data.IsPlayerControlled &&
            characterRuntime.gameObject == gameObject;

        public void Bind(CampusCharacterRuntime runtime)
        {
            characterRuntime = runtime;
            characterId = runtime != null ? runtime.CharacterId : string.Empty;
        }

        public void Clear()
        {
            characterRuntime = null;
            characterId = string.Empty;
        }

        public static CampusPlayerCharacter FindCurrent()
        {
            CampusRosterService rosterService =
                CampusGameBootstrap.Instance != null ? CampusGameBootstrap.Instance.RosterService : null;
            CampusCharacterRuntime runtime = rosterService != null ? rosterService.PlayerRuntime : null;
            CampusPlayerCharacter player = runtime != null ? runtime.GetComponent<CampusPlayerCharacter>() : null;
            if (player != null && player.IsCurrentPlayer)
            {
                return player;
            }

            return null;
        }
    }
}
