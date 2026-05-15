using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcSpeechBubble : MonoBehaviour
    {
        private const string BubbleRootName = "SpeechBubble";

        [SerializeField] private Transform anchor;
        [SerializeField] private TextMesh textMesh;
        [SerializeField] private float hideAtTime = -1f;

        public void Bind(Transform targetAnchor)
        {
            anchor = targetAnchor;
            EnsureVisual();
            HideImmediate();
        }

        public void Speak(string line, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                HideImmediate();
                return;
            }

            EnsureVisual();
            if (textMesh == null)
            {
                return;
            }

            textMesh.text = line.Trim();
            textMesh.gameObject.SetActive(true);
            hideAtTime = Time.time + Mathf.Max(0.8f, durationSeconds);
        }

        private void Update()
        {
            if (textMesh == null || hideAtTime < 0f)
            {
                return;
            }

            if (Time.time >= hideAtTime)
            {
                HideImmediate();
            }
        }

        private void EnsureVisual()
        {
            if (anchor == null)
            {
                anchor = transform;
            }

            if (textMesh != null)
            {
                return;
            }

            Transform bubbleRoot = anchor.Find(BubbleRootName);
            if (bubbleRoot == null)
            {
                GameObject bubbleObject = new GameObject(BubbleRootName);
                bubbleObject.transform.SetParent(anchor, false);
                bubbleObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                bubbleRoot = bubbleObject.transform;
            }

            textMesh = bubbleRoot.GetComponent<TextMesh>();
            if (textMesh == null)
            {
                textMesh = bubbleRoot.gameObject.AddComponent<TextMesh>();
            }

            textMesh.anchor = TextAnchor.LowerCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.055f;
            textMesh.fontSize = 48;
            textMesh.color = new Color(0.98f, 0.98f, 0.93f, 1f);
            textMesh.font = ResolveSpeechFont();

            MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerID = SortingLayer.NameToID("Default");
                renderer.sortingOrder = 1800;
            }
        }

        private void HideImmediate()
        {
            hideAtTime = -1f;
            if (textMesh != null)
            {
                textMesh.text = string.Empty;
                textMesh.gameObject.SetActive(false);
            }
        }

        private static Font ResolveSpeechFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
