using System;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcInteractionPresenter : MonoBehaviour
    {
        private const string InteractionAnchorName = "NpcTalkAnchor";
        private const string LegacyInteractionTargetName = "NpcInteractionTarget";
        private const float AmbientSpeechMinDelaySeconds = 8f;
        private const float AmbientSpeechMaxDelaySeconds = 18f;

        [SerializeField] private CampusNpcInteractable interactable;
        [SerializeField] private CampusNpcSpeechBubble speechBubble;
        [SerializeField] private Transform speechAnchor;
        [SerializeField] private float nextAmbientSpeechTime = -1f;
        [SerializeField] private int ambientSpeechSerial;

        public void Ensure(ICampusNpcTalkSource talkSource)
        {
            RemoveLegacyInteractionTarget();
            EnsureSpeechAnchor();
            EnsureSpeechBubble();
            EnsureInteractable(talkSource);
            EnsureInteractionAnchor();
        }

        public void Speak(string line, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (speechBubble != null)
            {
                speechBubble.Speak(line.Trim(), durationSeconds);
            }
        }

        public void ResetAmbientSpeechSchedule()
        {
            nextAmbientSpeechTime = -1f;
            ambientSpeechSerial = 0;
        }

        public void EnsureAmbientSpeechScheduled(int personalSeed, bool initial)
        {
            if (nextAmbientSpeechTime > Time.time)
            {
                return;
            }

            ScheduleNextAmbientSpeech(personalSeed, initial);
        }

        public void TickAmbientSpeech(int personalSeed, Func<string> resolveLine)
        {
            if (Time.time < nextAmbientSpeechTime || speechBubble == null || resolveLine == null)
            {
                return;
            }

            ambientSpeechSerial++;
            bool shouldSpeak = ShouldSpeakAmbientLine(personalSeed, ambientSpeechSerial);
            ScheduleNextAmbientSpeech(personalSeed, false);
            if (!shouldSpeak)
            {
                return;
            }

            Speak(resolveLine(), 1.7f);
        }

        private void EnsureSpeechAnchor()
        {
            if (speechAnchor != null)
            {
                return;
            }

            Transform existingAnchor = transform.Find(InteractionAnchorName);
            if (existingAnchor == null)
            {
                GameObject anchorObject = new GameObject(InteractionAnchorName);
                anchorObject.transform.SetParent(transform, false);
                anchorObject.transform.localPosition = new Vector3(0f, 0.82f, 0f);
                existingAnchor = anchorObject.transform;
            }

            speechAnchor = existingAnchor;
        }

        private void EnsureSpeechBubble()
        {
            if (speechBubble == null)
            {
                speechBubble = GetComponent<CampusNpcSpeechBubble>();
            }

            if (speechBubble == null)
            {
                speechBubble = gameObject.AddComponent<CampusNpcSpeechBubble>();
            }

            speechBubble.Bind(speechAnchor);
        }

        private void EnsureInteractable(ICampusNpcTalkSource talkSource)
        {
            if (interactable == null)
            {
                interactable = GetComponent<CampusNpcInteractable>();
            }

            if (interactable == null)
            {
                interactable = gameObject.AddComponent<CampusNpcInteractable>();
            }

            interactable.Bind(talkSource);
        }

        private void EnsureInteractionAnchor()
        {
            if (speechAnchor == null)
            {
                return;
            }

            CircleCollider2D collider = speechAnchor.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = speechAnchor.gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.radius = 0.82f;
            collider.offset = new Vector2(0f, -0.48f);

            CampusInteractionAnchor anchor = speechAnchor.GetComponent<CampusInteractionAnchor>();
            if (anchor == null)
            {
                anchor = speechAnchor.gameObject.AddComponent<CampusInteractionAnchor>();
            }

            anchor.InteractionTarget = interactable;
            anchor.ActionId = CampusInteractionActionIds.NpcTalk;
            anchor.Payload = string.Empty;
            anchor.PromptAnchor = speechAnchor;
            anchor.PromptText = "Talk";
            anchor.KeyOverride = string.Empty;
            anchor.Priority = 55;
            anchor.IsAvailable = true;
            anchor.HideWhenUnavailable = false;
            anchor.LogInteraction = false;
        }

        private void RemoveLegacyInteractionTarget()
        {
            Transform legacyTarget = transform.Find(LegacyInteractionTargetName);
            if (legacyTarget == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(legacyTarget.gameObject);
            }
            else
            {
                DestroyImmediate(legacyTarget.gameObject);
            }
        }

        private void ScheduleNextAmbientSpeech(int personalSeed, bool initial)
        {
            int salt = initial ? 197 : 311 + ambientSpeechSerial * 37;
            float random01 = PseudoRandom01(personalSeed, salt);
            float delay = initial
                ? Mathf.Lerp(3.5f, 13.5f, random01)
                : Mathf.Lerp(AmbientSpeechMinDelaySeconds, AmbientSpeechMaxDelaySeconds, random01);
            nextAmbientSpeechTime = Time.time + delay;
        }

        private static bool ShouldSpeakAmbientLine(int personalSeed, int serial)
        {
            int roll = Mathf.FloorToInt(PseudoRandom01(personalSeed, 911 + serial * 53) * 100f);
            return roll < 18;
        }

        private static float PseudoRandom01(int seed, int salt)
        {
            unchecked
            {
                int value = seed;
                value = (value * 397) ^ salt;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return CampusNpcStableIds.PositiveModulo(value, 10000) / 9999f;
            }
        }
    }
}
