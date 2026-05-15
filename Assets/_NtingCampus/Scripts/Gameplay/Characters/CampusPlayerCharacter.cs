using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusPlayerCharacter : MonoBehaviour
    {
        [SerializeField] private string characterId = string.Empty;
        [SerializeField] private CampusCharacterRuntime characterRuntime;

        public string CharacterId => characterId;
        public CampusCharacterRuntime CharacterRuntime => characterRuntime;

        public void Bind(CampusCharacterRuntime runtime)
        {
            characterRuntime = runtime;
            characterId = runtime != null ? runtime.CharacterId : string.Empty;
        }
    }
}
