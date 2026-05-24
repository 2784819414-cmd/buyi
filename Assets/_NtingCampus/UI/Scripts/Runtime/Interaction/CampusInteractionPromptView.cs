using UnityEngine;
using UnityEngine.UI;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CampusInteractionPromptView : MonoBehaviour
    {
        public Vector2 Size = new Vector2(180f, 110f);
        public float WorldScale = 0.01f;
        public float FadeSpeed = 14f;
        public bool FollowCameraRotation;
        public int SortingOrder = 30000;
        public Color TextColor = Color.white;
        public Color DisabledTextColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        public Color OutlineColor = new Color(0f, 0f, 0f, 0.82f);
        public Color ArrowColor = Color.white;
        public Color DisabledArrowColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        public int FontSize = 28;
        public float TextVerticalOffset = -12f;
        public float ArrowTextGap = 8f;
        public float TextFloatPixels = 1.5f;
        public float TextFloatFrequency = 1.2f;
        public float ArrowFloatPixels = 4f;
        public float ArrowFloatFrequency = 2.4f;
        public float ArrowPulseAmount = 0.08f;
        public Sprite ArrowSprite;
        public Font TextFont;

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform canvasRect;
        private RectTransform root;
        private RectTransform textRect;
        private RectTransform arrowRect;
        private Text promptText;
        private Image arrowImage;
        private Transform targetAnchor;
        private Vector3 targetOffset;
        private bool hasTarget;
        private bool currentAvailable = true;
        private float visibleAmount;
        private float animationSeed;
        private Vector3 lastPromptWorldPosition;
        private Quaternion lastPromptWorldRotation = Quaternion.identity;
        private bool hasLastPromptWorldPosition;

        private static Sprite defaultArrowSprite;

        private void Awake()
        {
            animationSeed = Random.value * 10f;
            EnsureView();
            HideImmediate();
        }

        private void Reset()
        {
            EnsureView();
        }

        private void LateUpdate()
        {
            EnsureView();
            UpdateVisibility();
            UpdateAnimation();
            UpdateFollowPosition();
        }

        public void Show(CampusInteractionTarget target)
        {
            if (!target.IsValid)
            {
                Hide();
                return;
            }

            EnsureView();
            CampusInteractionPromptData prompt = target.Prompt;
            targetAnchor = prompt.Anchor != null ? prompt.Anchor : target.TargetTransform;
            targetOffset = prompt.WorldOffset;
            hasTarget = targetAnchor != null;
            currentAvailable = prompt.IsAvailable;

            promptText.text = prompt.DisplayText;
            promptText.color = currentAvailable ? TextColor : DisabledTextColor;
            arrowImage.color = currentAvailable ? ArrowColor : DisabledArrowColor;
        }

        public void Hide()
        {
            hasTarget = false;
            targetAnchor = null;
        }

        public void HideImmediate()
        {
            Hide();
            visibleAmount = 0f;
            hasLastPromptWorldPosition = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        private void EnsureView()
        {
            canvas = GetOrAdd<Canvas>(gameObject);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.overrideSorting = true;
            canvas.sortingLayerID = SortingLayer.NameToID("Default");
            canvas.sortingOrder = SortingOrder;

            canvasGroup = GetOrAdd<CanvasGroup>(gameObject);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            canvasRect = GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = Size;

            root = FindOrCreateRect("浮动提示根", transform);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Size;
            RemoveObsoletePanelComponents(root.gameObject);
            RemoveObsoleteChild(root, "按键");
            RemoveObsoleteChild(root, "图标");

            textRect = FindOrCreateRect("提示文字", root);
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, TextVerticalOffset);
            textRect.sizeDelta = new Vector2(Size.x, 38f);

            promptText = GetOrAdd<Text>(textRect.gameObject);
            promptText.font = ResolveFont();
            promptText.fontSize = FontSize;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptText.verticalOverflow = VerticalWrapMode.Truncate;
            promptText.color = currentAvailable ? TextColor : DisabledTextColor;
            promptText.raycastTarget = false;

            Outline outline = GetOrAdd<Outline>(textRect.gameObject);
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);

            arrowRect = FindOrCreateRect("提示箭头", root);
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(50f, 20f);
            arrowRect.anchoredPosition = new Vector2(0f, CalculateArrowVerticalOffset(TextVerticalOffset));

            arrowImage = GetOrAdd<Image>(arrowRect.gameObject);
            arrowImage.sprite = ArrowSprite != null ? ArrowSprite : GetDefaultArrowSprite();
            arrowImage.preserveAspect = true;
            arrowImage.color = currentAvailable ? ArrowColor : DisabledArrowColor;
            arrowImage.raycastTarget = false;

            transform.localScale = Vector3.one * WorldScale;
        }

        private void UpdateFollowPosition()
        {
            if (targetAnchor == null)
            {
                if (hasLastPromptWorldPosition && canvas != null && canvas.enabled)
                {
                    transform.position = lastPromptWorldPosition;
                    transform.rotation = lastPromptWorldRotation;
                    transform.localScale = Vector3.one * WorldScale;
                }

                return;
            }

            Camera camera = Camera.main;
            Quaternion rotation = FollowCameraRotation && camera != null ? camera.transform.rotation : Quaternion.identity;
            Vector3 targetPosition = targetAnchor.position + targetOffset;
            Vector3 arrowTipOffset = rotation * (ResolveBaseArrowTipLocalPosition() * WorldScale);
            Vector3 promptPosition = targetPosition - arrowTipOffset;
            promptPosition = MovePromptInFrontOfScene(promptPosition, camera);

            transform.position = promptPosition;
            transform.rotation = rotation;
            transform.localScale = Vector3.one * WorldScale;
            lastPromptWorldPosition = promptPosition;
            lastPromptWorldRotation = rotation;
            hasLastPromptWorldPosition = true;
        }

        private Vector3 ResolveBaseArrowTipLocalPosition()
        {
            if (arrowRect == null || root == null)
            {
                return Vector3.zero;
            }

            float arrowY = CalculateArrowVerticalOffset(TextVerticalOffset);
            float tipY = arrowY - arrowRect.sizeDelta.y * 0.5f;
            return new Vector3(0f, tipY, 0f);
        }

        private static Vector3 MovePromptInFrontOfScene(Vector3 position, Camera camera)
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

            Vector3 nearPosition = camera.transform.position + forward * (camera.nearClipPlane + 0.05f);
            position.z = nearPosition.z;
            return position;
        }

        private void UpdateVisibility()
        {
            float target = hasTarget && targetAnchor != null ? 1f : 0f;
            visibleAmount = Mathf.MoveTowards(visibleAmount, target, FadeSpeed * Time.deltaTime);

            canvas.enabled = visibleAmount > 0.001f;
            canvasGroup.alpha = Smooth01(visibleAmount);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            root.localScale = Vector3.one;
            transform.localScale = Vector3.one * WorldScale;
        }

        private void UpdateAnimation()
        {
            if (textRect == null || arrowRect == null)
            {
                return;
            }

            float textT = Mathf.Sin((Time.time + animationSeed * 0.73f) * TextFloatFrequency * Mathf.PI * 2f);
            float arrowT = Mathf.Sin((Time.time + animationSeed) * ArrowFloatFrequency * Mathf.PI * 2f);
            float textVerticalOffset = TextVerticalOffset + textT * TextFloatPixels;
            textRect.anchoredPosition = new Vector2(0f, textVerticalOffset);
            arrowRect.anchoredPosition = new Vector2(0f, CalculateArrowVerticalOffset(textVerticalOffset) + arrowT * ArrowFloatPixels);
            float pulse = 1f + (1f - Mathf.Abs(arrowT)) * ArrowPulseAmount;
            arrowRect.localScale = new Vector3(pulse, pulse, 1f);
        }

        private float CalculateArrowVerticalOffset(float textVerticalOffset)
        {
            float rootHeight = root != null ? root.sizeDelta.y : Size.y;
            float textHeight = textRect != null ? textRect.sizeDelta.y : 38f;
            float arrowHeight = arrowRect != null ? arrowRect.sizeDelta.y : 20f;
            float textCenterY = rootHeight * 0.5f + textVerticalOffset;
            float textBottomY = textCenterY - textHeight * 0.5f;
            return textBottomY - Mathf.Max(0f, ArrowTextGap) - arrowHeight * 0.5f;
        }

        private Font ResolveFont()
        {
            if (TextFont != null)
            {
                return TextFont;
            }

            TextFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (TextFont == null)
            {
                TextFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return TextFont;
        }

        private static RectTransform FindOrCreateRect(string childName, Transform parent)
        {
            Transform child = parent.Find(childName);
            if (child == null && childName == "浮动提示根")
            {
                child = parent.Find("提示根");
                if (child != null)
                {
                    child.name = childName;
                }
            }

            if (child == null)
            {
                GameObject childObject = new GameObject(childName, typeof(RectTransform));
                childObject.transform.SetParent(parent, false);
                child = childObject.transform;
            }

            return child as RectTransform;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void RemoveObsoletePanelComponents(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image != null)
            {
                DestroyRuntimeOrEditor(image);
            }

            HorizontalLayoutGroup layout = target.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                DestroyRuntimeOrEditor(layout);
            }

            ContentSizeFitter fitter = target.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                DestroyRuntimeOrEditor(fitter);
            }
        }

        private static void RemoveObsoleteChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                DestroyRuntimeOrEditor(child.gameObject);
            }
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

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static Sprite GetDefaultArrowSprite()
        {
            if (defaultArrowSprite == null)
            {
                defaultArrowSprite = CreateDownArrowSprite();
            }

            return defaultArrowSprite;
        }

        private static Sprite CreateDownArrowSprite()
        {
            const int width = 32;
            const int height = 22;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            for (int y = 0; y < height; y++)
            {
                float progress = y / (float)(height - 1);
                int halfWidth = Mathf.RoundToInt(Mathf.Lerp(2f, width * 0.48f, progress));
                int center = width / 2;
                for (int x = center - halfWidth; x <= center + halfWidth; x++)
                {
                    if (x >= 0 && x < width)
                    {
                        pixels[y * width + x] = solid;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
