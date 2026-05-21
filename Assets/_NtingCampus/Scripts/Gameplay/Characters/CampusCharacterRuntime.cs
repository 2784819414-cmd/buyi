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

    public static class CampusCharacterMovementTuning
    {
        public const float PlayerMoveSpeed = 7f;
        public const float NpcWalkSpeed = 2.7f;
        public const float NpcPersonalSpeedMultiplierMin = 0.9f;
        public const float NpcPersonalSpeedMultiplierMax = 1.12f;

        public static float ResolvePlayerMoveSpeed()
        {
            return PlayerMoveSpeed;
        }

        public static float ResolveNpcMoveSpeed(int personalSeed)
        {
            return NpcWalkSpeed * ResolveNpcPersonalSpeedMultiplier(personalSeed);
        }

        public static float ResolveNpcPersonalSpeedMultiplier(int personalSeed)
        {
            int normalizedSeed = Mathf.Max(1, Mathf.Abs(personalSeed));
            float seed01 = CampusNpcStableIds.PositiveModulo(normalizedSeed, 100) / 99f;
            return Mathf.Lerp(
                NpcPersonalSpeedMultiplierMin,
                NpcPersonalSpeedMultiplierMax,
                seed01);
        }
    }
}
