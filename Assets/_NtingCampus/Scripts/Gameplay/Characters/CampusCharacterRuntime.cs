using UnityEngine;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusCharacterRuntime : MonoBehaviour
    {
        [SerializeField] private CampusCharacterData data;

        public CampusCharacterData Data => data;
        public string CharacterId => data != null ? data.Id : string.Empty;

        private void Awake()
        {
            EnsureShadowCasterProfile();
        }

        private void Reset()
        {
            EnsureShadowCasterProfile();
        }

        public void Bind(CampusCharacterData characterData, bool renameGameObject)
        {
            data = characterData;
            if (renameGameObject && data != null)
            {
                string objectName = data.GetPreferredObjectName();
                if (!string.IsNullOrWhiteSpace(objectName))
                {
                    gameObject.name = objectName;
                }
            }
        }

        private void EnsureShadowCasterProfile()
        {
            NtingShadowCasterProfile profile = NtingShadowCasterProfile.EnsureForObject(gameObject);
            if (profile == null)
            {
                return;
            }

            profile.ApplyCharacterDefaults();
            profile.castCustomShadows = true;
            profile.castPointLightShadows = true;
            profile.castSunShadow = true;
        }
    }
}
