using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcSpeechBubble : MonoBehaviour
    {
        private const string BubbleRootName = "SpeechBubble";
        private const string TextNodeName = "SpeechText";
        private const int BubbleSortingOrder = 31000;

        [SerializeField] private Transform anchor;
        [SerializeField] private Canvas bubbleCanvas;
        [SerializeField] private RectTransform bubbleRect;
        [SerializeField] private Text bubbleText;
        [SerializeField] private Outline bubbleOutline;
        [SerializeField] private float hideAtTime = -1f;
        [SerializeField] private Vector2 bubbleSize = new Vector2(320f, 96f);
        [SerializeField] private float worldScale = 0.01f;

        public void Bind(Transform targetAnchor)
        {
            if (anchor == targetAnchor && bubbleCanvas != null && bubbleText != null)
            {
                return;
            }

            anchor = targetAnchor != null ? targetAnchor : transform;
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
            if (bubbleText == null || bubbleCanvas == null)
            {
                return;
            }

            bubbleText.text = line.Trim();
            bubbleCanvas.enabled = true;
            bubbleText.gameObject.SetActive(true);
            hideAtTime = Time.time + Mathf.Max(0.8f, durationSeconds);
        }

        private void LateUpdate()
        {
            if (bubbleCanvas == null)
            {
                return;
            }

            if (hideAtTime >= 0f && Time.time >= hideAtTime)
            {
                HideImmediate();
            }

            if (bubbleCanvas.enabled)
            {
                UpdateBubbleTransform();
            }
        }

        private void EnsureVisual()
        {
            if (anchor == null)
            {
                anchor = transform;
            }

            Transform root = anchor.Find(BubbleRootName);
            if (root == null)
            {
                GameObject bubbleObject = new GameObject(BubbleRootName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                bubbleObject.transform.SetParent(anchor, false);
                root = bubbleObject.transform;
            }

            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one * worldScale;

            bubbleRect = root as RectTransform;
            if (bubbleRect == null)
            {
                bubbleRect = root.gameObject.GetComponent<RectTransform>();
            }

            DestroyLegacyTextMesh(root.gameObject);

            bubbleCanvas = GetOrAdd<Canvas>(root.gameObject);
            bubbleCanvas.renderMode = RenderMode.WorldSpace;
            bubbleCanvas.worldCamera = Camera.main;
            bubbleCanvas.overrideSorting = true;
            bubbleCanvas.sortingLayerID = SortingLayer.NameToID("Default");
            bubbleCanvas.sortingOrder = BubbleSortingOrder;

            GraphicRaycaster raycaster = root.gameObject.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }

            bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(0.5f, 0f);
            bubbleRect.anchoredPosition = Vector2.zero;
            bubbleRect.sizeDelta = bubbleSize;

            RectTransform textRect = FindOrCreateTextRect(root);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.pivot = new Vector2(0.5f, 0.5f);

            bubbleText = GetOrAdd<Text>(textRect.gameObject);
            bubbleText.font = ResolveSpeechFont();
            bubbleText.fontSize = 30;
            bubbleText.alignment = TextAnchor.LowerCenter;
            bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
            bubbleText.supportRichText = false;
            bubbleText.raycastTarget = false;
            bubbleText.color = new Color(0.98f, 0.98f, 0.93f, 1f);

            bubbleOutline = GetOrAdd<Outline>(textRect.gameObject);
            bubbleOutline.effectColor = Color.black;
            bubbleOutline.effectDistance = new Vector2(1.2f, -1.2f);
            bubbleOutline.useGraphicAlpha = true;

            UpdateBubbleTransform();
        }

        private void UpdateBubbleTransform()
        {
            if (bubbleRect == null || anchor == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (bubbleCanvas != null)
            {
                bubbleCanvas.worldCamera = camera;
            }

            Vector3 position = anchor.position;
            Quaternion rotation = Quaternion.identity;

            if (camera != null)
            {
                rotation = camera.transform.rotation;
                position = MoveInFrontOfScene(position, camera);
            }

            bubbleRect.position = position;
            bubbleRect.rotation = rotation;
            bubbleRect.localScale = Vector3.one * worldScale;
        }

        private void HideImmediate()
        {
            hideAtTime = -1f;
            if (bubbleText != null)
            {
                bubbleText.text = string.Empty;
                bubbleText.gameObject.SetActive(false);
            }

            if (bubbleCanvas != null)
            {
                bubbleCanvas.enabled = false;
            }
        }

        private static RectTransform FindOrCreateTextRect(Transform root)
        {
            Transform child = root.Find(TextNodeName);
            if (child == null)
            {
                GameObject textObject = new GameObject(TextNodeName, typeof(RectTransform), typeof(Text), typeof(Outline));
                textObject.transform.SetParent(root, false);
                child = textObject.transform;
            }

            return child as RectTransform;
        }

        private static void DestroyLegacyTextMesh(GameObject root)
        {
            TextMesh legacyText = root.GetComponent<TextMesh>();
            if (legacyText != null)
            {
                DestroyRuntimeOrEditor(legacyText);
            }

            MeshRenderer legacyRenderer = root.GetComponent<MeshRenderer>();
            if (legacyRenderer != null)
            {
                DestroyRuntimeOrEditor(legacyRenderer);
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void DestroyRuntimeOrEditor(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Vector3 MoveInFrontOfScene(Vector3 position, Camera camera)
        {
            if (camera == null || !camera.orthographic)
            {
                return position;
            }

            Vector3 forward = camera.transform.forward;
            if (Mathf.Abs(forward.z) <= 0.0001f)
            {
                return position;
            }

            Vector3 nearPosition = camera.transform.position + forward * (camera.nearClipPlane + 0.06f);
            position.z = nearPosition.z;
            return position;
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
