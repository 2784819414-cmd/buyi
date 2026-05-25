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
            EnsureRoomTracker();
        }

        private void Reset()
        {
            EnsureShadowCasterProfile();
            EnsureRoomTracker();
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

            EnsureRoomTracker();
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

        private void EnsureRoomTracker()
        {
            CampusCharacterCurrentRoomTracker.EnsureFor(this);
        }
    }

    public static class CampusCharacterSpeechUtility
    {
        private const string SpeechAnchorName = "CharacterSpeechAnchor";
        private const float SpeechAnchorHeight = 0.82f;

        public static void Speak(CampusCharacterRuntime runtime, string line, float durationSeconds)
        {
            if (runtime == null || string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            CampusNpcSpeechBubble speechBubble = runtime.GetComponent<CampusNpcSpeechBubble>();
            if (speechBubble == null)
            {
                speechBubble = runtime.gameObject.AddComponent<CampusNpcSpeechBubble>();
                speechBubble.Bind(ResolveSpeechAnchor(runtime.transform));
            }

            speechBubble.Speak(line.Trim(), durationSeconds);
        }

        private static Transform ResolveSpeechAnchor(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform anchor = root.Find(SpeechAnchorName);
            if (anchor != null)
            {
                return anchor;
            }

            GameObject anchorObject = new GameObject(SpeechAnchorName);
            anchorObject.transform.SetParent(root, false);
            anchorObject.transform.localPosition = new Vector3(0f, SpeechAnchorHeight, 0f);
            return anchorObject.transform;
        }
    }

    public static class CampusCharacterMovementTuning
    {
        public const float PlayerMoveSpeed = 7f;
        public const float PlayerSprintSpeedMultiplier = 1.55f;
        public const float NpcWalkSpeed = 2.7f;
        public const float NpcPersonalSpeedMultiplierMin = 0.9f;
        public const float NpcPersonalSpeedMultiplierMax = 1.12f;

        public static float ResolvePlayerMoveSpeed()
        {
            return PlayerMoveSpeed;
        }

        public static float ResolvePlayerSprintSpeed()
        {
            return PlayerMoveSpeed * PlayerSprintSpeedMultiplier;
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
