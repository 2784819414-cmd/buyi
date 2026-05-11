using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NtingFiniteSunShadow : MonoBehaviour
    {
        private const float DefaultTileSize = 1f;
        public const string ProxyName = "SunShadowProxy";

        public enum ShadowPreset
        {
            Small,
            Furniture,
            Cabinet,
            Tall
        }

        public bool castSunShadow = true;
        public SpriteRenderer sourceSpriteRenderer;
        public bool useDayNightController = true;
        public Vector2 sunDirection2D = new Vector2(1f, -0.4f);
        public ShadowPreset preset = ShadowPreset.Furniture;
        public bool usePresetLength = true;
        public float maxShadowLength = 1f;
        [Range(0f, 1f)] public float shadowAlpha = 0.22f;
        public bool castNightShadow = true;
        [Range(0f, 1f)] public float nightShadowAlphaFactor = 0.18f;
        public Color shadowColor = new Color(0.05f, 0.08f, 0.12f, 1f);
        public int sortingOrderOffset = -100;
        public bool copySourceSprite = true;
        public Vector2 localOffset = Vector2.zero;

        private SpriteRenderer proxyRenderer;
        private CampusDayNightController dayNightController;
        private static Material sharedShadowMaterial;

        public static NtingFiniteSunShadow EnsureForPlacedObject(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return null;
            }

            NtingFiniteSunShadow shadow = placed.GetComponent<NtingFiniteSunShadow>();
            if (shadow == null)
            {
                shadow = placed.gameObject.AddComponent<NtingFiniteSunShadow>();
                shadow.ApplyPreset(InferPreset(placed));
            }

            shadow.RefreshShadow();
            return shadow;
        }

        private static ShadowPreset InferPreset(CampusPlacedObject placed)
        {
            string objectName = placed != null ? placed.gameObject.name : string.Empty;
            if (objectName.Contains("树") || objectName.Contains("路灯") || objectName.Contains("灯杆") ||
                objectName.IndexOf("tree", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ShadowPreset.Tall;
            }

            if (objectName.Contains("柜") || objectName.Contains("架") ||
                objectName.IndexOf("cabinet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("shelf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ShadowPreset.Cabinet;
            }

            Vector2Int footprint = placed != null ? placed.NormalizedFootprintSize : Vector2Int.one;
            int maxFootprint = Mathf.Max(footprint.x, footprint.y);
            if (maxFootprint <= 1)
            {
                return ShadowPreset.Small;
            }

            return maxFootprint >= 3 ? ShadowPreset.Cabinet : ShadowPreset.Furniture;
        }

        private void Awake()
        {
            RefreshShadow();
        }

        private void OnEnable()
        {
            RefreshShadow();
        }

        private void OnDisable()
        {
            SetProxyActive(false);
        }

        private void OnValidate()
        {
            maxShadowLength = Mathf.Max(0f, maxShadowLength);
            shadowAlpha = Mathf.Clamp01(shadowAlpha);
            nightShadowAlphaFactor = Mathf.Clamp01(nightShadowAlphaFactor);
            RefreshShadow();
        }

        private void LateUpdate()
        {
            RefreshShadow();
        }

        public void ApplyPreset(ShadowPreset nextPreset)
        {
            preset = nextPreset;
            if (usePresetLength)
            {
                maxShadowLength = ResolvePresetLength(preset);
            }
        }

        public void RefreshShadow()
        {
            if (sourceSpriteRenderer == null || IsProxyRenderer(sourceSpriteRenderer))
            {
                sourceSpriteRenderer = FindSourceSpriteRenderer();
            }

            if (usePresetLength)
            {
                maxShadowLength = ResolvePresetLength(preset);
            }

            bool canShow = castSunShadow &&
                sourceSpriteRenderer != null &&
                sourceSpriteRenderer.sprite != null &&
                sourceSpriteRenderer.enabled &&
                sourceSpriteRenderer.gameObject.activeInHierarchy &&
                maxShadowLength > 0.001f &&
                shadowAlpha > 0.001f;

            if (!canShow)
            {
                SetProxyActive(false);
                return;
            }

            SpriteRenderer proxy = ResolveProxyRenderer();
            if (proxy == null)
            {
                return;
            }

            Vector2 direction = ResolveSunDirection();
            float dayAlpha = ResolveDayAlpha();
            if (dayAlpha <= 0.001f)
            {
                SetProxyActive(false);
                return;
            }

            proxy.gameObject.SetActive(true);
            ShadowCaster2D accidentalCaster = proxy.GetComponent<ShadowCaster2D>();
            if (accidentalCaster != null)
            {
                DestroyGeneratedObject(accidentalCaster);
            }

            proxy.sprite = copySourceSprite ? sourceSpriteRenderer.sprite : proxy.sprite;
            proxy.flipX = sourceSpriteRenderer.flipX;
            proxy.flipY = sourceSpriteRenderer.flipY;
            proxy.drawMode = sourceSpriteRenderer.drawMode;
            proxy.size = sourceSpriteRenderer.size;
            proxy.tileMode = sourceSpriteRenderer.tileMode;
            proxy.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, Mathf.Clamp01(shadowAlpha * dayAlpha));
            proxy.sharedMaterial = ResolveShadowMaterial();
            proxy.sortingLayerID = ResolveSortingLayerId(sourceSpriteRenderer.sortingLayerID);
            proxy.sortingOrder = sourceSpriteRenderer.sortingOrder + sortingOrderOffset;

            Transform proxyTransform = proxy.transform;
            if (proxyTransform.parent != sourceSpriteRenderer.transform)
            {
                proxyTransform.SetParent(sourceSpriteRenderer.transform, false);
            }

            proxyTransform.localRotation = Quaternion.identity;
            proxyTransform.localPosition = ResolveLocalOffset(direction);
            proxyTransform.localScale = ResolveLocalScale(direction);
        }

        private SpriteRenderer FindSourceSpriteRenderer()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null && !IsProxyRenderer(renderer) && renderer.sprite != null)
                {
                    return renderer;
                }
            }

            return null;
        }

        private SpriteRenderer ResolveProxyRenderer()
        {
            SpriteRenderer keep = proxyRenderer != null && proxyRenderer.transform.IsChildOf(transform)
                ? proxyRenderer
                : null;

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || !IsProxyRenderer(renderer))
                {
                    continue;
                }

                if (keep == null || IsBetterProxy(renderer, keep))
                {
                    keep = renderer;
                }
            }

            if (keep == null)
            {
                GameObject proxyObject = new GameObject(ProxyName);
                keep = proxyObject.AddComponent<SpriteRenderer>();
            }

            keep.gameObject.name = ProxyName;
            proxyRenderer = keep;
            DestroyDuplicateProxies(keep);
            return keep;
        }

        private bool IsBetterProxy(SpriteRenderer candidate, SpriteRenderer current)
        {
            if (candidate == null)
            {
                return false;
            }

            if (current == null)
            {
                return true;
            }

            bool candidateIsDirectChild = sourceSpriteRenderer != null && candidate.transform.parent == sourceSpriteRenderer.transform;
            bool currentIsDirectChild = sourceSpriteRenderer != null && current.transform.parent == sourceSpriteRenderer.transform;
            if (candidateIsDirectChild != currentIsDirectChild)
            {
                return candidateIsDirectChild;
            }

            return candidate.GetInstanceID() < current.GetInstanceID();
        }

        private void DestroyDuplicateProxies(SpriteRenderer keep)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = renderers.Length - 1; i >= 0; i--)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer == keep || !IsProxyRenderer(renderer))
                {
                    continue;
                }

                DestroyGeneratedObject(renderer.gameObject);
            }
        }

        private Vector2 ResolveSunDirection()
        {
            if (useDayNightController)
            {
                if (dayNightController == null)
                {
                    dayNightController = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
                }

                if (dayNightController != null && dayNightController.SunDirection2D.sqrMagnitude > 0.0001f)
                {
                    sunDirection2D = dayNightController.SunDirection2D;
                }
            }

            if (sunDirection2D.sqrMagnitude <= 0.0001f)
            {
                sunDirection2D = Vector2.down;
            }

            return sunDirection2D.normalized;
        }

        private float ResolveDayAlpha()
        {
            if (!useDayNightController)
            {
                return 1f;
            }

            if (dayNightController == null)
            {
                dayNightController = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }

            if (dayNightController == null)
            {
                return 1f;
            }

            if (dayNightController.GameHour < 6f || dayNightController.GameHour > 18f)
            {
                return castNightShadow ? nightShadowAlphaFactor : 0f;
            }

            return Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(dayNightController.SunLowFactor));
        }

        private Vector3 ResolveLocalOffset(Vector2 direction)
        {
            Vector3 worldOffset = new Vector3(direction.x, direction.y, 0f) * (ResolveShadowLength() * 0.5f);
            Vector3 local = sourceSpriteRenderer.transform.InverseTransformVector(worldOffset);
            return local + new Vector3(localOffset.x, localOffset.y, 0f);
        }

        private Vector3 ResolveLocalScale(Vector2 direction)
        {
            Sprite sprite = sourceSpriteRenderer.sprite;
            if (sprite == null)
            {
                return Vector3.one;
            }

            Transform sourceTransform = sourceSpriteRenderer.transform;
            Vector2 localDirection = new Vector2(
                Vector2.Dot(direction, sourceTransform.right),
                Vector2.Dot(direction, sourceTransform.up));
            Vector3 lossyScale = sourceTransform.lossyScale;
            Vector2 spriteSize = sprite.bounds.size;
            float worldWidth = Mathf.Max(0.01f, Mathf.Abs(spriteSize.x * lossyScale.x));
            float worldHeight = Mathf.Max(0.01f, Mathf.Abs(spriteSize.y * lossyScale.y));
            float length = ResolveShadowLength();
            float scaleX = 1f + Mathf.Abs(localDirection.x) * length / worldWidth;
            float scaleY = 1f + Mathf.Abs(localDirection.y) * length / worldHeight;
            return new Vector3(scaleX, scaleY, 1f);
        }

        private float ResolveShadowLength()
        {
            return Mathf.Max(0f, maxShadowLength * DefaultTileSize);
        }

        private void SetProxyActive(bool active)
        {
            if (proxyRenderer == null)
            {
                SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer != null && IsProxyRenderer(renderer))
                    {
                        proxyRenderer = renderer;
                        break;
                    }
                }
            }

            if (proxyRenderer != null)
            {
                proxyRenderer.gameObject.SetActive(active);
            }
        }

        private static float ResolvePresetLength(ShadowPreset preset)
        {
            switch (preset)
            {
                case ShadowPreset.Small:
                    return 0.5f;
                case ShadowPreset.Cabinet:
                    return 1.2f;
                case ShadowPreset.Tall:
                    return 2.5f;
                default:
                    return 1f;
            }
        }

        private static int ResolveSortingLayerId(int fallbackSortingLayerId)
        {
            return CampusRenderSortingUtility.ResolveSunShadowSortingLayerId(fallbackSortingLayerId);
        }

        private static bool IsProxyRenderer(SpriteRenderer renderer)
        {
            return renderer != null && renderer.gameObject.name == ProxyName;
        }

        private static Material ResolveShadowMaterial()
        {
            if (sharedShadowMaterial != null)
            {
                return sharedShadowMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            sharedShadowMaterial = new Material(shader)
            {
                name = "NtingFiniteSunShadow_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedShadowMaterial;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
