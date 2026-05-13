using System;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [Serializable]
    public struct CampusInteractionPromptData
    {
        public string PromptText;
        public string KeyText;
        public Sprite Icon;
        public Transform Anchor;
        public Vector3 WorldOffset;
        public Color AccentColor;
        public int Priority;
        public bool IsAvailable;
        public string UnavailableText;

        public static CampusInteractionPromptData Create(string promptText)
        {
            return new CampusInteractionPromptData
            {
                PromptText = promptText,
                WorldOffset = new Vector3(0f, 0.85f, 0f),
                AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f),
                IsAvailable = true
            };
        }

        public string DisplayText
        {
            get
            {
                if (!IsAvailable && !string.IsNullOrWhiteSpace(UnavailableText))
                {
                    return UnavailableText;
                }

                return string.IsNullOrWhiteSpace(PromptText) ? "\u4ea4\u4e92" : PromptText;
            }
        }
    }

    public interface ICampusInteractionPromptProvider
    {
        bool TryGetInteractionPrompt(GameObject actor, out CampusInteractionPromptData prompt);
    }

    public interface ICampusInteractionAvailability
    {
        bool CanInteract(GameObject actor, out string unavailableReason);
    }
}
