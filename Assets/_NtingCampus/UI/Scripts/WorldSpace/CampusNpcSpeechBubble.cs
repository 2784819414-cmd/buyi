using DG.Tweening;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcSpeechBubble : MonoBehaviour
    {
        private const string BubbleRootName = "SpeechBubble";
        private const string BubbleVisualRootName = "SpeechBubbleVisual";
        private const string TextNodeName = "SpeechText";
        private const int BubbleSortingOrder = 31000;

        [SerializeField] private Transform anchor;
        [SerializeField] private Canvas bubbleCanvas;
        [SerializeField] private CanvasGroup bubbleGroup;
        [SerializeField] private RectTransform bubbleRect;
        [SerializeField] private RectTransform bubbleVisualRect;
        [SerializeField] private RectTransform textRect;
        [SerializeField] private Text bubbleText;
        [SerializeField] private float hideAtTime = -1f;
        [SerializeField] private Vector2 bubbleSize = new Vector2(360f, 84f);
        [SerializeField] private float worldScale = 0.01f;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.75f);
        [SerializeField] private int fontSize = 28;
        [SerializeField] private float floatAmplitude = 1.8f;
        [SerializeField] private float floatFrequency = 1.3f;
        [SerializeField] private bool followCameraRotation = false;
        [SerializeField] private Font textFont;

        private Sequence visibilityTween;
        private float floatSeed;

        public void Bind(Transform targetAnchor)
        {
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
            bubbleText.color = textColor;
            bubbleCanvas.enabled = true;
            bubbleGroup.alpha = 1f;
            hideAtTime = Time.time + Mathf.Max(0.8f, durationSeconds);
            AnimateVisible(true);
        }

        private void Awake()
        {
            floatSeed = Random.value * 10f;
            EnsureVisual();
            HideImmediate();
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
                return;
            }

            if (bubbleCanvas.enabled)
            {
                UpdateBubbleTransform();
                UpdateFloatMotion();
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
                GameObject bubbleObject = new GameObject(BubbleRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                bubbleObject.transform.SetParent(anchor, false);
                root = bubbleObject.transform;
            }

            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one * worldScale;

            bubbleRect = root as RectTransform;
            if (bubbleRect == null)
            {
                bubbleRect = root.GetComponent<RectTransform>();
            }
            bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleRect.sizeDelta = bubbleSize;

            bubbleVisualRect = FindOrCreateRect(BubbleVisualRootName, root);
            bubbleVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleVisualRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleVisualRect.sizeDelta = bubbleSize;
            bubbleVisualRect.anchoredPosition = Vector2.zero;
            bubbleVisualRect.localScale = Vector3.one;

            bubbleCanvas = GetOrAdd<Canvas>(root.gameObject);
            bubbleCanvas.renderMode = RenderMode.WorldSpace;
            bubbleCanvas.worldCamera = Camera.main;
            bubbleCanvas.overrideSorting = true;
            bubbleCanvas.sortingLayerID = SortingLayer.NameToID("Default");
            bubbleCanvas.sortingOrder = BubbleSortingOrder;

            bubbleGroup = GetOrAdd<CanvasGroup>(root.gameObject);
            bubbleGroup.interactable = false;
            bubbleGroup.blocksRaycasts = false;

            textRect = FindOrCreateRect(TextNodeName, bubbleVisualRect, root);
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(0f, 0f);
            textRect.offsetMax = new Vector2(0f, 0f);

            bubbleText = GetOrAdd<Text>(textRect.gameObject);
            bubbleText.font = ResolveFont();
            bubbleText.fontSize = fontSize;
            bubbleText.alignment = TextAnchor.MiddleCenter;
            bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
            bubbleText.supportRichText = false;
            bubbleText.raycastTarget = false;
            bubbleText.color = textColor;

            Outline outline = GetOrAdd<Outline>(textRect.gameObject);
            outline.effectColor = shadowColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = GetOrAdd<Shadow>(textRect.gameObject);
            shadow.effectColor = shadowColor;
            shadow.effectDistance = new Vector2(0f, -1f);
            shadow.useGraphicAlpha = true;
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
            Quaternion rotation = followCameraRotation && camera != null ? camera.transform.rotation : Quaternion.identity;
            if (camera != null)
            {
                position = MoveInFrontOfScene(position, camera);
            }

            bubbleRect.position = position;
            bubbleRect.rotation = rotation;
            bubbleRect.localScale = Vector3.one * worldScale;
        }

        private void UpdateFloatMotion()
        {
            if (textRect == null)
            {
                return;
            }

            float t = (Time.unscaledTime + floatSeed) * floatFrequency * Mathf.PI * 2f;
            float y = Mathf.Sin(t) * floatAmplitude;
            textRect.anchoredPosition = new Vector2(0f, y);
        }

        private void HideImmediate()
        {
            hideAtTime = -1f;
            KillTween();

            if (bubbleText != null)
            {
                bubbleText.text = string.Empty;
            }

            if (bubbleGroup != null)
            {
                bubbleGroup.alpha = 0f;
                bubbleGroup.interactable = false;
                bubbleGroup.blocksRaycasts = false;
            }

            if (bubbleVisualRect != null)
            {
                bubbleVisualRect.localScale = Vector3.one;
                bubbleVisualRect.anchoredPosition = Vector2.zero;
            }

            if (bubbleCanvas != null)
            {
                bubbleCanvas.enabled = false;
            }
        }

        private void AnimateVisible(bool visible)
        {
            EnsureVisual();
            KillTween();

            if (bubbleGroup == null || bubbleRect == null)
            {
                return;
            }

            bubbleCanvas.enabled = true;
            bubbleGroup.interactable = false;
            bubbleGroup.blocksRaycasts = false;

            if (visible)
            {
                bubbleGroup.alpha = 0f;
                if (bubbleVisualRect != null)
                {
                    bubbleVisualRect.localScale = Vector3.one * 0.92f;
                    bubbleVisualRect.anchoredPosition = new Vector2(0f, -10f);
                }

                visibilityTween = CampusUiTweenUtility.OpenPanel(bubbleGroup, bubbleVisualRect, 0.16f, 0.92f);
                return;
            }

            visibilityTween = CampusUiTweenUtility.ClosePanel(bubbleGroup, bubbleVisualRect, 0.1f, 0.96f);
            visibilityTween.OnComplete(() =>
            {
                if (bubbleCanvas != null)
                {
                    bubbleCanvas.enabled = false;
                }
            });
        }

        private static RectTransform FindOrCreateRect(string childName, Transform root)
        {
            return FindOrCreateRect(childName, root, null);
        }

        private static RectTransform FindOrCreateRect(string childName, Transform root, Transform legacyParent)
        {
            Transform child = root.Find(childName);
            if (child == null && legacyParent != null)
            {
                child = legacyParent.Find(childName);
                if (child != null)
                {
                    child.SetParent(root, false);
                }
            }

            if (child == null)
            {
                GameObject textObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(root, false);
                child = textObject.transform;
            }

            return child as RectTransform;
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

        private Font ResolveFont()
        {
            if (textFont != null)
            {
                return textFont;
            }

            textFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (textFont == null)
            {
                textFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return textFont;
        }

        private void KillTween()
        {
            if (visibilityTween != null && visibilityTween.IsActive())
            {
                visibilityTween.Kill();
            }

            visibilityTween = null;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
