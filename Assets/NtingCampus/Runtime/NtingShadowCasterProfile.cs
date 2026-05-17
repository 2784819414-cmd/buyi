using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NtingShadowCasterProfile : MonoBehaviour
    {
        public enum ShadowSourceMode
        {
            FullSprite = 0,
            BottomBand = 1
        }

        private const string ProxyRootName = "Nting Custom Shadow Proxies";
        private const string SunProxyName = "NtingSunShadowProxy";
        private const string PointProxyPrefix = "NtingPointShadowProxy_";
        private const string ShadowShaderName = "Nting Campus/2D/Sprite Alpha Extruded Shadow";
        private const string ShadowShaderResourcePath = "Shaders/NtingSpriteShadowExtrude";
        private const float MinShadowLength = 0.025f;
        private const float MinShadowAlpha = 0.002f;
        private const float MinSpriteWorldSize = 0.01f;
        private const float MinSunPointLightFillMultiplier = 0.18f;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SpriteUvMinSizeId = Shader.PropertyToID("_SpriteUVMinSize");
        private static readonly int SourceCenterSizeId = Shader.PropertyToID("_SourceCenterSize");
        private static readonly int SourceRightId = Shader.PropertyToID("_SourceRight");
        private static readonly int SourceUpId = Shader.PropertyToID("_SourceUp");
        private static readonly int ShadowDirId = Shader.PropertyToID("_ShadowDir");
        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int ShadowLengthId = Shader.PropertyToID("_ShadowLength");
        private static readonly int ShadowAlphaId = Shader.PropertyToID("_ShadowAlpha");
        private static readonly int SampleCountId = Shader.PropertyToID("_SampleCount");
        private static readonly int FlipId = Shader.PropertyToID("_Flip");
        private static readonly Vector2[] ProxyUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        private static readonly int[] ProxyTriangles = { 0, 1, 2, 0, 2, 3 };

        public bool castCustomShadows = true;
        public bool castPointLightShadows = true;
        public bool castSunShadow = true;
        public SpriteRenderer sourceSpriteRenderer;
        public ShadowSourceMode shadowSourceMode = ShadowSourceMode.FullSprite;
        public Color shadowColor = new Color(0.04f, 0.06f, 0.09f, 1f);
        [Range(0f, 2f)] public float pointShadowLengthMultiplier = 1f;
        [Range(0f, 2f)] public float pointShadowAlphaMultiplier = 1f;
        [Range(0f, 2f)] public float sunShadowLengthMultiplier = 1f;
        [Range(0f, 2f)] public float sunShadowAlphaMultiplier = 1f;
        [Range(8, 256)] public int shadowSampleCount = 32;
        public Vector2 localOffset = Vector2.zero;
        [Range(0.1f, 1f)] public float bottomBandHeightRatio = 1f;
        [Range(0.1f, 1f)] public float bottomBandWidthRatio = 1f;

        private readonly List<ShadowCandidate> candidates = new List<ShadowCandidate>(8);
        private readonly List<ShadowProxy> pointProxies = new List<ShadowProxy>(8);
        private Transform proxyRoot;
        private ShadowProxy sunProxy;
        private int lastRefreshSignature = int.MinValue;
        private static Material sharedShadowMaterial;
        private static bool legacySpriteProxyPurgeDone;

        public static NtingShadowCasterProfile EnsureForPlacedObject(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return null;
            }

            NtingShadowCasterProfile profile = EnsureForObject(placed.gameObject);
            if (profile != null)
            {
                profile.sourceSpriteRenderer = profile.FindSourceSpriteRenderer();
                profile.ApplyPlacedObjectDefaults(placed);
            }

            return profile;
        }

        public static NtingShadowCasterProfile EnsureForObject(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            NtingShadowCasterProfile profile = target.GetComponent<NtingShadowCasterProfile>();
            if (profile == null)
            {
                profile = target.AddComponent<NtingShadowCasterProfile>();
            }

            if (profile.sourceSpriteRenderer == null)
            {
                profile.sourceSpriteRenderer = profile.FindSourceSpriteRenderer();
            }

            return profile;
        }

        public static void PurgeLegacySpriteProxyObjectsInSceneOnce()
        {
            if (legacySpriteProxyPurgeDone)
            {
                return;
            }

            legacySpriteProxyPurgeDone = true;
            SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null && IsProxyObjectName(renderer.gameObject.name))
                {
                    DestroyGeneratedObject(renderer.gameObject);
                }
            }
        }

        public void RefreshCustomShadows(
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int pointLightCount,
            bool enablePointShadows,
            bool enableSunShadow,
            Vector2 sunDirection,
            float sunShadowLengthWorld,
            float sunShadowAlpha,
            Color globalShadowColor,
            Color sunGlobalShadowColor,
            float pointShadowMaxLengthWorld,
            float pointShadowAlpha,
            float pointShadowIntensityScale,
            float pointLightFillStrength,
            float pointLightFillIntensityScale,
            int maxPointLightShadows,
            int globalSampleCount,
            int sortingLayerId,
            int sortingOrder)
        {
            if (sourceSpriteRenderer == null || IsProxyRenderer(sourceSpriteRenderer) || !IsRendererOwnedByProfile(sourceSpriteRenderer))
            {
                sourceSpriteRenderer = FindSourceSpriteRenderer();
            }

            bool canCast = castCustomShadows &&
                sourceSpriteRenderer != null &&
                sourceSpriteRenderer.sprite != null &&
                sourceSpriteRenderer.enabled &&
                sourceSpriteRenderer.gameObject.activeInHierarchy;

            if (!canCast)
            {
                lastRefreshSignature = int.MinValue;
                HideAllProxies();
                return;
            }

            int refreshSignature = ComputeRefreshSignature(
                pointLights,
                pointLightCount,
                enablePointShadows,
                enableSunShadow,
                sunDirection,
                sunShadowLengthWorld,
                sunShadowAlpha,
                globalShadowColor,
                sunGlobalShadowColor,
                pointShadowMaxLengthWorld,
                pointShadowAlpha,
                pointShadowIntensityScale,
                pointLightFillStrength,
                pointLightFillIntensityScale,
                maxPointLightShadows,
                globalSampleCount,
                sortingLayerId,
                sortingOrder);
            if (refreshSignature == lastRefreshSignature)
            {
                return;
            }

            lastRefreshSignature = refreshSignature;

            Color effectiveColor = shadowColor;
            effectiveColor.r *= globalShadowColor.r;
            effectiveColor.g *= globalShadowColor.g;
            effectiveColor.b *= globalShadowColor.b;
            effectiveColor.a = 1f;

            Color effectiveSunColor = shadowColor;
            effectiveSunColor.r *= sunGlobalShadowColor.r;
            effectiveSunColor.g *= sunGlobalShadowColor.g;
            effectiveSunColor.b *= sunGlobalShadowColor.b;
            effectiveSunColor.a = 1f;

            int effectiveSampleCount = globalSampleCount > 0 ? globalSampleCount : shadowSampleCount;
            RefreshSunProxy(pointLights, pointLightCount, enableSunShadow, sunDirection, sunShadowLengthWorld, sunShadowAlpha, pointLightFillStrength, pointLightFillIntensityScale, effectiveSampleCount, effectiveSunColor, sortingLayerId, sortingOrder);
            RefreshPointProxies(pointLights, pointLightCount, enablePointShadows, pointShadowMaxLengthWorld, pointShadowAlpha, pointShadowIntensityScale, pointLightFillStrength, pointLightFillIntensityScale, maxPointLightShadows, effectiveSampleCount, effectiveColor, sortingLayerId, sortingOrder);
        }

        public int ComputeSceneRefreshSignature()
        {
            unchecked
            {
                int signature = 17;
                Sprite sprite = sourceSpriteRenderer != null ? sourceSpriteRenderer.sprite : null;
                Bounds bounds = sourceSpriteRenderer != null ? sourceSpriteRenderer.bounds : default;
                signature = signature * 31 + GetInstanceID();
                signature = signature * 31 + (sprite != null ? sprite.GetInstanceID() : 0);
                signature = signature * 31 + (sourceSpriteRenderer != null && sourceSpriteRenderer.enabled ? 1 : 0);
                signature = signature * 31 + (sourceSpriteRenderer != null && sourceSpriteRenderer.flipX ? 1 : 0);
                signature = signature * 31 + (sourceSpriteRenderer != null && sourceSpriteRenderer.flipY ? 1 : 0);
                signature = signature * 31 + Quantize(bounds.center.x);
                signature = signature * 31 + Quantize(bounds.center.y);
                signature = signature * 31 + Quantize(bounds.size.x);
                signature = signature * 31 + Quantize(bounds.size.y);
                signature = signature * 31 + Quantize(localOffset.x);
                signature = signature * 31 + Quantize(localOffset.y);
                signature = signature * 31 + (int)shadowSourceMode;
                signature = signature * 31 + Quantize(bottomBandHeightRatio);
                signature = signature * 31 + Quantize(bottomBandWidthRatio);
                return signature;
            }
        }

        private int ComputeRefreshSignature(
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int pointLightCount,
            bool enablePointShadows,
            bool enableSunShadow,
            Vector2 sunDirection,
            float sunShadowLengthWorld,
            float sunShadowAlpha,
            Color globalShadowColor,
            Color sunGlobalShadowColor,
            float pointShadowMaxLengthWorld,
            float pointShadowAlpha,
            float pointShadowIntensityScale,
            float pointLightFillStrength,
            float pointLightFillIntensityScale,
            int maxPointLightShadows,
            int globalSampleCount,
            int sortingLayerId,
            int sortingOrder)
        {
            unchecked
            {
                int signature = 17;
                Sprite sprite = sourceSpriteRenderer != null ? sourceSpriteRenderer.sprite : null;
                Bounds bounds = sourceSpriteRenderer != null ? sourceSpriteRenderer.bounds : default;
                signature = signature * 31 + (sprite != null ? sprite.GetInstanceID() : 0);
                signature = signature * 31 + (sourceSpriteRenderer != null && sourceSpriteRenderer.enabled ? 1 : 0);
                signature = signature * 31 + (sourceSpriteRenderer != null && sourceSpriteRenderer.flipX ? 1 : 0);
                signature = signature * 31 + (sourceSpriteRenderer != null && sourceSpriteRenderer.flipY ? 1 : 0);
                signature = signature * 31 + Quantize(bounds.center.x);
                signature = signature * 31 + Quantize(bounds.center.y);
                signature = signature * 31 + Quantize(bounds.size.x);
                signature = signature * 31 + Quantize(bounds.size.y);
                signature = signature * 31 + Quantize(localOffset.x);
                signature = signature * 31 + Quantize(localOffset.y);
                signature = signature * 31 + (int)shadowSourceMode;
                signature = signature * 31 + Quantize(bottomBandHeightRatio);
                signature = signature * 31 + Quantize(bottomBandWidthRatio);
                signature = signature * 31 + (castCustomShadows ? 1 : 0);
                signature = signature * 31 + (castPointLightShadows ? 1 : 0);
                signature = signature * 31 + (castSunShadow ? 1 : 0);
                signature = signature * 31 + (enablePointShadows ? 1 : 0);
                signature = signature * 31 + (enableSunShadow ? 1 : 0);
                signature = signature * 31 + Quantize(sunDirection.x);
                signature = signature * 31 + Quantize(sunDirection.y);
                signature = signature * 31 + Quantize(sunShadowLengthWorld);
                signature = signature * 31 + Quantize(sunShadowAlpha);
                signature = signature * 31 + Quantize(globalShadowColor.r);
                signature = signature * 31 + Quantize(globalShadowColor.g);
                signature = signature * 31 + Quantize(globalShadowColor.b);
                signature = signature * 31 + Quantize(sunGlobalShadowColor.r);
                signature = signature * 31 + Quantize(sunGlobalShadowColor.g);
                signature = signature * 31 + Quantize(sunGlobalShadowColor.b);
                signature = signature * 31 + Quantize(pointShadowMaxLengthWorld);
                signature = signature * 31 + Quantize(pointShadowAlpha);
                signature = signature * 31 + Quantize(pointShadowIntensityScale);
                signature = signature * 31 + Quantize(pointLightFillStrength);
                signature = signature * 31 + Quantize(pointLightFillIntensityScale);
                signature = signature * 31 + maxPointLightShadows;
                signature = signature * 31 + globalSampleCount;
                signature = signature * 31 + sortingLayerId;
                signature = signature * 31 + sortingOrder;
                signature = signature * 31 + Quantize(pointShadowLengthMultiplier);
                signature = signature * 31 + Quantize(pointShadowAlphaMultiplier);
                signature = signature * 31 + Quantize(sunShadowLengthMultiplier);
                signature = signature * 31 + Quantize(sunShadowAlphaMultiplier);
                signature = signature * 31 + Quantize(shadowColor.r);
                signature = signature * 31 + Quantize(shadowColor.g);
                signature = signature * 31 + Quantize(shadowColor.b);

                int count = pointLights != null ? Mathf.Min(pointLightCount, pointLights.Length) : 0;
                signature = signature * 31 + count;
                for (int i = 0; i < count; i++)
                {
                    NtingCustomShadowSystem.ShadowLightInfo light = pointLights[i];
                    signature = signature * 31 + light.InstanceId;
                    signature = signature * 31 + Quantize(light.Position.x);
                    signature = signature * 31 + Quantize(light.Position.y);
                    signature = signature * 31 + Quantize(light.Radius);
                    signature = signature * 31 + Quantize(light.Intensity);
                    signature = signature * 31 + Quantize(light.ShadowLengthWeight);
                    signature = signature * 31 + Quantize(light.ShadowAlphaWeight);
                    signature = signature * 31 + Quantize(light.ShadowFillWeight);
                    signature = signature * 31 + Quantize(light.ShadowColorTint.r);
                    signature = signature * 31 + Quantize(light.ShadowColorTint.g);
                    signature = signature * 31 + Quantize(light.ShadowColorTint.b);
                }

                return signature;
            }
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        private void OnDisable()
        {
            lastRefreshSignature = int.MinValue;
            HideAllProxies();
        }

        private void OnDestroy()
        {
            DestroyProxyRoot();
        }

        private void OnValidate()
        {
            lastRefreshSignature = int.MinValue;
            pointShadowLengthMultiplier = Mathf.Clamp(pointShadowLengthMultiplier, 0f, 2f);
            pointShadowAlphaMultiplier = Mathf.Clamp(pointShadowAlphaMultiplier, 0f, 2f);
            sunShadowLengthMultiplier = Mathf.Clamp(sunShadowLengthMultiplier, 0f, 2f);
            sunShadowAlphaMultiplier = Mathf.Clamp(sunShadowAlphaMultiplier, 0f, 2f);
            shadowSampleCount = Mathf.Clamp(shadowSampleCount, 8, 256);
            bottomBandHeightRatio = Mathf.Clamp(bottomBandHeightRatio, 0.1f, 1f);
            bottomBandWidthRatio = Mathf.Clamp(bottomBandWidthRatio, 0.1f, 1f);
        }

        private void ApplyPlacedObjectDefaults(CampusPlacedObject placed)
        {
            shadowSourceMode = ShadowSourceMode.FullSprite;
            bottomBandHeightRatio = 1f;
            bottomBandWidthRatio = 1f;

            Vector2Int footprint = placed != null ? placed.RotatedFootprintSize : Vector2Int.one;
            int maxFootprint = Mathf.Max(footprint.x, footprint.y);
            if (maxFootprint <= 1)
            {
                pointShadowLengthMultiplier = 0.65f;
                sunShadowLengthMultiplier = 0.65f;
            }
            else if (maxFootprint >= 3)
            {
                pointShadowLengthMultiplier = 1.25f;
                sunShadowLengthMultiplier = 1.2f;
            }
            else
            {
                pointShadowLengthMultiplier = 1f;
                sunShadowLengthMultiplier = 1f;
            }
        }

        public void ApplyCharacterDefaults()
        {
            shadowSourceMode = ShadowSourceMode.BottomBand;
            bottomBandHeightRatio = 0.28f;
            bottomBandWidthRatio = 0.62f;
            pointShadowLengthMultiplier = 0.52f;
            pointShadowAlphaMultiplier = 0.92f;
            sunShadowLengthMultiplier = 0.55f;
            sunShadowAlphaMultiplier = 0.88f;
        }

        private void RefreshSunProxy(
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int pointLightCount,
            bool enableSunShadow,
            Vector2 direction,
            float lengthWorld,
            float alpha,
            float pointLightFillStrength,
            float pointLightFillIntensityScale,
            int sampleCount,
            Color color,
            int sortingLayerId,
            int sortingOrder)
        {
            if (!castSunShadow || !enableSunShadow || lengthWorld <= 0f || alpha <= 0f || sunShadowLengthMultiplier <= 0f || sunShadowAlphaMultiplier <= 0f)
            {
                HideSunProxy();
                return;
            }

            float length = Mathf.Max(MinShadowLength, lengthWorld * sunShadowLengthMultiplier);
            float effectiveAlpha = Mathf.Max(MinShadowAlpha, Mathf.Clamp01(alpha * sunShadowAlphaMultiplier));
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            if (!IsFinite(length) || !IsFinite(effectiveAlpha) || !IsFinite(normalizedDirection.x) || !IsFinite(normalizedDirection.y))
            {
                HideSunProxy();
                ResetProxyMesh(sunProxy);
                return;
            }

            Vector2 samplePosition = (Vector2)sourceSpriteRenderer.bounds.center + normalizedDirection * (length * 0.55f);
            float fillMultiplier = NtingCustomShadowSystem.ResolvePointLightFillMultiplier(
                pointLights,
                pointLightCount,
                0,
                samplePosition,
                pointLightFillStrength,
                pointLightFillIntensityScale);
            effectiveAlpha = Mathf.Max(MinShadowAlpha, effectiveAlpha * Mathf.Max(MinSunPointLightFillMultiplier, fillMultiplier));

            ConfigureSunProxy(EnsureSunProxy(), normalizedDirection, length, effectiveAlpha, sampleCount, color, sortingLayerId, sortingOrder);
        }

        private void ConfigureSunProxy(
            ShadowProxy proxy,
            Vector2 direction,
            float length,
            float alpha,
            int sampleCount,
            Color baseColor,
            int sortingLayerId,
            int sortingOrder)
        {
            if (proxy == null || !TryResolveSpriteFrame(out SpriteShadowFrame frame))
            {
                HideSunProxy();
                ResetProxyMesh(proxy);
                return;
            }

            Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            Vector2 localShadowDelta = new Vector2(
                Vector2.Dot(normalizedDirection, frame.SourceRight2),
                Vector2.Dot(normalizedDirection, frame.SourceUp2)) * length;

            float minX = -frame.SourceWidth * 0.5f + Mathf.Min(0f, localShadowDelta.x);
            float maxX = frame.SourceWidth * 0.5f + Mathf.Max(0f, localShadowDelta.x);
            float minY = -frame.SourceHeight * 0.5f + Mathf.Min(0f, localShadowDelta.y);
            float maxY = frame.SourceHeight * 0.5f + Mathf.Max(0f, localShadowDelta.y);
            if (!IsFinite(minX) || !IsFinite(maxX) || !IsFinite(minY) || !IsFinite(maxY))
            {
                HideSunProxy();
                ResetProxyMesh(proxy);
                return;
            }

            PrepareProxyForRendering(proxy, frame, minX, maxX, minY, maxY, sortingLayerId, sortingOrder);
            ApplyShadowProperties(proxy, frame, normalizedDirection, length, alpha, sampleCount, baseColor);
            proxy.SetActive(true);
        }

        private bool TryResolveSpriteFrame(out SpriteShadowFrame frame)
        {
            frame = default;
            if (sourceSpriteRenderer == null || sourceSpriteRenderer.sprite == null)
            {
                return false;
            }

            Sprite sprite = sourceSpriteRenderer.sprite;
            Texture2D texture = sprite.texture;
            if (texture == null)
            {
                return false;
            }

            Transform sourceTransform = sourceSpriteRenderer.transform;
            Vector3 sourceScale = sourceTransform.lossyScale;
            Vector2 localSpriteSize = ResolveLocalSpriteSize(sourceSpriteRenderer, sprite);
            Vector3 sourceRight3 = SafeNormalized(sourceTransform.TransformDirection(Vector3.right), Vector3.right);
            Vector3 sourceUp3 = SafeNormalized(sourceTransform.TransformDirection(Vector3.up), Vector3.up);
            Vector2 sourceRight2 = new Vector2(sourceRight3.x, sourceRight3.y);
            Vector2 sourceUp2 = new Vector2(sourceUp3.x, sourceUp3.y);
            Vector3 fullSourceCenter = sourceTransform.TransformPoint(sprite.bounds.center) + sourceTransform.TransformVector(new Vector3(localOffset.x, localOffset.y, 0f));
            float fullSourceWidth = Mathf.Max(MinSpriteWorldSize, Mathf.Abs(localSpriteSize.x * sourceScale.x));
            float fullSourceHeight = Mathf.Max(MinSpriteWorldSize, Mathf.Abs(localSpriteSize.y * sourceScale.y));
            Vector4 sourceUvMinSize = ResolveSpriteUvMinSize(sprite);
            Vector3 sourceCenter = fullSourceCenter;
            float sourceWidth = fullSourceWidth;
            float sourceHeight = fullSourceHeight;

            if (shadowSourceMode == ShadowSourceMode.BottomBand)
            {
                float widthRatio = Mathf.Clamp(bottomBandWidthRatio, 0.1f, 1f);
                float heightRatio = Mathf.Clamp(bottomBandHeightRatio, 0.1f, 1f);
                sourceWidth = Mathf.Max(MinSpriteWorldSize, fullSourceWidth * widthRatio);
                sourceHeight = Mathf.Max(MinSpriteWorldSize, fullSourceHeight * heightRatio);

                float verticalOffset = (-fullSourceHeight * 0.5f) + (sourceHeight * 0.5f);
                sourceCenter = fullSourceCenter + sourceUp3 * verticalOffset;

                float uvMinX = sourceUvMinSize.x + sourceUvMinSize.z * ((1f - widthRatio) * 0.5f);
                float uvMinY = sourceUvMinSize.y;
                float uvWidth = sourceUvMinSize.z * widthRatio;
                float uvHeight = sourceUvMinSize.w * heightRatio;
                sourceUvMinSize = new Vector4(uvMinX, uvMinY, uvWidth, uvHeight);
            }

            frame = new SpriteShadowFrame(
                sprite,
                texture,
                sourceCenter,
                sourceRight3,
                sourceUp3,
                sourceRight2,
                sourceUp2,
                sourceWidth,
                sourceHeight,
                sourceUvMinSize);
            return true;
        }

        private void PrepareProxyForRendering(
            ShadowProxy proxy,
            SpriteShadowFrame frame,
            float minX,
            float maxX,
            float minY,
            float maxY,
            int sortingLayerId,
            int sortingOrder)
        {
            if (proxyRoot != null && !proxyRoot.gameObject.activeSelf)
            {
                proxyRoot.gameObject.SetActive(true);
            }

            if (proxy.GameObject != null && !proxy.GameObject.activeSelf)
            {
                proxy.GameObject.SetActive(true);
            }

            proxy.Transform.position = frame.SourceCenter;
            proxy.Transform.rotation = Quaternion.identity;
            proxy.Transform.localScale = Vector3.one;
            UpdateProxyMesh(proxy, frame.SourceRight3, frame.SourceUp3, minX, maxX, minY, maxY);

            proxy.Renderer.sharedMaterial = ResolveShadowMaterial();
            proxy.Renderer.sortingLayerID = sortingLayerId;
            proxy.Renderer.sortingOrder = sortingOrder;
            proxy.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            proxy.Renderer.receiveShadows = false;
            proxy.Renderer.forceRenderingOff = false;
            proxy.Renderer.enabled = true;
            proxy.Renderer.lightProbeUsage = LightProbeUsage.Off;
            proxy.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void ApplyShadowProperties(
            ShadowProxy proxy,
            SpriteShadowFrame frame,
            Vector2 normalizedDirection,
            float length,
            float alpha,
            int sampleCount,
            Color baseColor)
        {
            Color finalColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            MaterialPropertyBlock block = proxy.PropertyBlock;
            block.Clear();
            block.SetTexture(MainTexId, frame.Texture);
            block.SetVector(SpriteUvMinSizeId, frame.SpriteUvMinSize);
            block.SetVector(SourceCenterSizeId, new Vector4(frame.SourceCenter.x, frame.SourceCenter.y, frame.SourceWidth, frame.SourceHeight));
            block.SetVector(SourceRightId, new Vector4(frame.SourceRight2.x, frame.SourceRight2.y, 0f, 0f));
            block.SetVector(SourceUpId, new Vector4(frame.SourceUp2.x, frame.SourceUp2.y, 0f, 0f));
            block.SetVector(ShadowDirId, new Vector4(normalizedDirection.x, normalizedDirection.y, 0f, 0f));
            block.SetColor(ShadowColorId, finalColor);
            block.SetFloat(ShadowLengthId, Mathf.Max(0f, length));
            block.SetFloat(ShadowAlphaId, Mathf.Clamp01(alpha));
            block.SetFloat(SampleCountId, Mathf.Clamp(sampleCount, 8, 256));
            block.SetVector(FlipId, new Vector4(sourceSpriteRenderer.flipX ? 1f : 0f, sourceSpriteRenderer.flipY ? 1f : 0f, 0f, 0f));
            proxy.Renderer.SetPropertyBlock(block);
        }

        private void RefreshPointProxies(
            NtingCustomShadowSystem.ShadowLightInfo[] pointLights,
            int pointLightCount,
            bool enablePointShadows,
            float maxLengthWorld,
            float alpha,
            float intensityScale,
            float pointLightFillStrength,
            float pointLightFillIntensityScale,
            int maxPointLightShadows,
            int sampleCount,
            Color color,
            int sortingLayerId,
            int sortingOrder)
        {
            candidates.Clear();
            if (!castPointLightShadows || !enablePointShadows || pointLights == null || pointLightCount <= 0 || maxPointLightShadows <= 0)
            {
                HidePointProxies(0);
                return;
            }

            Bounds bounds = sourceSpriteRenderer.bounds;
            Vector3 center = bounds.center;
            int count = Mathf.Min(pointLightCount, pointLights.Length);
            for (int i = 0; i < count; i++)
            {
                NtingCustomShadowSystem.ShadowLightInfo light = pointLights[i];
                Vector2 awayFromLight = (Vector2)(center - light.Position);
                float distance = awayFromLight.magnitude;
                if (distance <= 0.001f || distance >= light.Radius)
                {
                    continue;
                }

                float distance01 = Mathf.Clamp01(distance / light.Radius);
                float rangeFactor = 1f - distance01;
                float visibleRangeFactor = Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(rangeFactor));
                float intensityFactor = Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(light.Intensity * Mathf.Max(0.001f, intensityScale)));
                Vector2 normalizedDirection = awayFromLight.normalized;
                float lengthDistanceFactor = Mathf.Lerp(0.15f, 1f, distance01);
                float length = Mathf.Max(MinShadowLength, maxLengthWorld * pointShadowLengthMultiplier * light.ShadowLengthWeight * lengthDistanceFactor * Mathf.Lerp(0.85f, 1.15f, intensityFactor));
                Vector2 samplePosition = (Vector2)center + normalizedDirection * (length * 0.55f);
                float fillMultiplier = NtingCustomShadowSystem.ResolvePointLightFillMultiplier(
                    pointLights,
                    count,
                    light.InstanceId,
                    samplePosition,
                    pointLightFillStrength,
                    pointLightFillIntensityScale);
                float effectiveAlpha = Mathf.Clamp01(alpha * pointShadowAlphaMultiplier * light.ShadowAlphaWeight * visibleRangeFactor * intensityFactor * fillMultiplier);
                if (effectiveAlpha <= MinShadowAlpha)
                {
                    continue;
                }

                Color lightShadowColor = NtingCustomShadowSystem.ResolvePointLightShadowColor(color, light);
                candidates.Add(new ShadowCandidate(normalizedDirection, length, effectiveAlpha, lightShadowColor));
            }

            if (candidates.Count == 0)
            {
                HidePointProxies(0);
                return;
            }

            candidates.Sort(CompareCandidates);
            int visibleCount = Mathf.Min(maxPointLightShadows, candidates.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                ShadowCandidate candidate = candidates[i];
                ConfigureProxy(EnsurePointProxy(i), candidate.Direction, candidate.Length, candidate.Alpha, sampleCount, candidate.Color, sortingLayerId, sortingOrder + i);
            }

            HidePointProxies(visibleCount);
        }

        private void ConfigureProxy(
            ShadowProxy proxy,
            Vector2 direction,
            float length,
            float alpha,
            int sampleCount,
            Color baseColor,
            int sortingLayerId,
            int sortingOrder)
        {
            if (proxy == null || !TryResolveSpriteFrame(out SpriteShadowFrame frame))
            {
                return;
            }
            Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            Vector2 localShadowDelta = new Vector2(
                Vector2.Dot(normalizedDirection, frame.SourceRight2),
                Vector2.Dot(normalizedDirection, frame.SourceUp2)) * length;

            float minX = -frame.SourceWidth * 0.5f + Mathf.Min(0f, localShadowDelta.x);
            float maxX = frame.SourceWidth * 0.5f + Mathf.Max(0f, localShadowDelta.x);
            float minY = -frame.SourceHeight * 0.5f + Mathf.Min(0f, localShadowDelta.y);
            float maxY = frame.SourceHeight * 0.5f + Mathf.Max(0f, localShadowDelta.y);
            if (!IsFinite(minX) || !IsFinite(maxX) || !IsFinite(minY) || !IsFinite(maxY))
            {
                proxy.SetActive(false);
                ResetProxyMesh(proxy);
                return;
            }

            PrepareProxyForRendering(proxy, frame, minX, maxX, minY, maxY, sortingLayerId, sortingOrder);
            ApplyShadowProperties(proxy, frame, normalizedDirection, length, alpha, sampleCount, baseColor);
            proxy.SetActive(true);
        }

        private static void UpdateProxyMesh(ShadowProxy proxy, Vector3 right, Vector3 up, float minX, float maxX, float minY, float maxY)
        {
            Mesh mesh = proxy.Mesh;
            Vector3[] vertices = proxy.Vertices;
            vertices[0] = right * minX + up * minY;
            vertices[1] = right * maxX + up * minY;
            vertices[2] = right * maxX + up * maxY;
            vertices[3] = right * minX + up * maxY;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static void ResetProxyMesh(ShadowProxy proxy)
        {
            if (proxy == null || proxy.Mesh == null)
            {
                return;
            }

            Vector3[] vertices = proxy.Vertices;
            vertices[0] = Vector3.zero;
            vertices[1] = Vector3.zero;
            vertices[2] = Vector3.zero;
            vertices[3] = Vector3.zero;
            Mesh mesh = proxy.Mesh;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = ProxyUvs;
            mesh.triangles = ProxyTriangles;
            mesh.RecalculateBounds();
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

        private ShadowProxy EnsureSunProxy()
        {
            if (sunProxy == null || sunProxy.Renderer == null)
            {
                sunProxy = CreateProxy(SunProxyName);
            }

            return sunProxy;
        }

        private ShadowProxy EnsurePointProxy(int index)
        {
            while (pointProxies.Count <= index)
            {
                pointProxies.Add(null);
            }

            ShadowProxy proxy = pointProxies[index];
            if (proxy == null || proxy.Renderer == null)
            {
                proxy = CreateProxy(PointProxyPrefix + index);
                pointProxies[index] = proxy;
            }

            return proxy;
        }

        private ShadowProxy CreateProxy(string objectName)
        {
            Transform root = EnsureProxyRoot();
            Transform existing = root.Find(objectName);
            GameObject proxyObject = existing != null ? existing.gameObject : new GameObject(objectName);
            if (!proxyObject.activeSelf)
            {
                proxyObject.SetActive(true);
            }

            proxyObject.transform.SetParent(root, true);
            RemoveLegacyComponents(proxyObject);

            MeshFilter filter = proxyObject.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = proxyObject.AddComponent<MeshFilter>();
            }

            MeshRenderer renderer = proxyObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = proxyObject.AddComponent<MeshRenderer>();
            }

            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = objectName + "_Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
            }

            mesh.MarkDynamic();
            ShadowProxy proxy = new ShadowProxy(proxyObject, filter, renderer, mesh);
            InitializeProxyMesh(proxy);
            return proxy;
        }

        private static void InitializeProxyMesh(ShadowProxy proxy)
        {
            Mesh mesh = proxy.Mesh;
            mesh.Clear();
            mesh.vertices = proxy.Vertices;
            mesh.uv = ProxyUvs;
            mesh.triangles = ProxyTriangles;
            mesh.RecalculateBounds();
        }

        private Transform EnsureProxyRoot()
        {
            if (proxyRoot != null)
            {
                return proxyRoot;
            }

            Transform parent = transform.parent;
            Transform existing = parent != null ? parent.Find(ProxyRootName + "_" + GetInstanceID()) : null;
            if (existing != null)
            {
                proxyRoot = existing;
                PurgeLegacySpriteProxyObjects();
                return proxyRoot;
            }

            GameObject rootObject = new GameObject(ProxyRootName + "_" + GetInstanceID());
            proxyRoot = rootObject.transform;
            if (parent != null)
            {
                proxyRoot.SetParent(parent, true);
            }

            proxyRoot.position = Vector3.zero;
            proxyRoot.rotation = Quaternion.identity;
            proxyRoot.localScale = Vector3.one;
            return proxyRoot;
        }

        private void HideSunProxy()
        {
            if (sunProxy != null)
            {
                sunProxy.SetActive(false);
            }
        }

        private void HidePointProxies(int firstHiddenIndex)
        {
            for (int i = firstHiddenIndex; i < pointProxies.Count; i++)
            {
                if (pointProxies[i] != null)
                {
                    pointProxies[i].SetActive(false);
                }
            }
        }

        private void HideAllProxies()
        {
            HideSunProxy();
            HidePointProxies(0);
        }

        private void DestroyProxyRoot()
        {
            if (sunProxy != null)
            {
                DestroyGeneratedObject(sunProxy.Mesh);
                sunProxy = null;
            }

            for (int i = 0; i < pointProxies.Count; i++)
            {
                if (pointProxies[i] != null)
                {
                    DestroyGeneratedObject(pointProxies[i].Mesh);
                }
            }

            pointProxies.Clear();

            if (proxyRoot != null)
            {
                DestroyGeneratedObject(proxyRoot.gameObject);
                proxyRoot = null;
            }
        }

        private void PurgeLegacySpriteProxyObjects()
        {
            if (proxyRoot == null)
            {
                return;
            }

            for (int i = proxyRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = proxyRoot.GetChild(i);
                if (child != null && IsProxyObjectName(child.gameObject.name) && child.GetComponent<SpriteRenderer>() != null)
                {
                    DestroyGeneratedObject(child.gameObject);
                }
            }
        }

        private static void RemoveLegacyComponents(GameObject proxyObject)
        {
            if (proxyObject == null)
            {
                return;
            }

            SpriteRenderer legacySpriteRenderer = proxyObject.GetComponent<SpriteRenderer>();
            if (legacySpriteRenderer != null)
            {
                DestroyGeneratedObject(legacySpriteRenderer);
            }

            ShadowCaster2D accidentalCaster = proxyObject.GetComponent<ShadowCaster2D>();
            if (accidentalCaster != null)
            {
                DestroyGeneratedObject(accidentalCaster);
            }
        }

        private static bool IsProxyRenderer(SpriteRenderer renderer)
        {
            return renderer != null && IsProxyObjectName(renderer.gameObject.name);
        }

        private bool IsRendererOwnedByProfile(SpriteRenderer renderer)
        {
            return renderer != null &&
                renderer.transform != null &&
                renderer.transform.IsChildOf(transform);
        }

        private static bool IsProxyObjectName(string objectName)
        {
            return !string.IsNullOrEmpty(objectName) &&
                (objectName.StartsWith(SunProxyName, System.StringComparison.Ordinal) ||
                 objectName.StartsWith(PointProxyPrefix, System.StringComparison.Ordinal) ||
                 objectName.StartsWith(ProxyRootName, System.StringComparison.Ordinal));
        }

        private static Vector2 ResolveLocalSpriteSize(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer != null && renderer.drawMode != SpriteDrawMode.Simple)
            {
                return renderer.size;
            }

            return sprite != null ? sprite.bounds.size : Vector2.one;
        }

        private static Vector3 SafeNormalized(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.000001f ? value.normalized : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Vector4 ResolveSpriteUvMinSize(Sprite sprite)
        {
            Texture2D texture = sprite != null ? sprite.texture : null;
            if (sprite == null || texture == null)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            try
            {
                Rect textureRect = sprite.textureRect;
                return new Vector4(
                    textureRect.xMin / texture.width,
                    textureRect.yMin / texture.height,
                    textureRect.width / texture.width,
                    textureRect.height / texture.height);
            }
            catch (UnityException)
            {
                Vector2[] uvs = sprite.uv;
                if (uvs == null || uvs.Length == 0)
                {
                    return new Vector4(0f, 0f, 1f, 1f);
                }

                Vector2 min = uvs[0];
                Vector2 max = uvs[0];
                for (int i = 1; i < uvs.Length; i++)
                {
                    min = Vector2.Min(min, uvs[i]);
                    max = Vector2.Max(max, uvs[i]);
                }

                Vector2 size = Vector2.Max(max - min, new Vector2(0.0001f, 0.0001f));
                return new Vector4(min.x, min.y, size.x, size.y);
            }
        }

        private static int CompareCandidates(ShadowCandidate left, ShadowCandidate right)
        {
            return right.Alpha.CompareTo(left.Alpha);
        }

        private static Material ResolveShadowMaterial()
        {
            if (sharedShadowMaterial != null)
            {
                return sharedShadowMaterial;
            }

            Shader shader = Shader.Find(ShadowShaderName);
            if (shader == null)
            {
                shader = Resources.Load<Shader>(ShadowShaderResourcePath);
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            sharedShadowMaterial = new Material(shader)
            {
                name = "NtingSpriteAlphaExtrudedShadow_Runtime",
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

        private readonly struct ShadowCandidate
        {
            public readonly Vector2 Direction;
            public readonly float Length;
            public readonly float Alpha;
            public readonly Color Color;

            public ShadowCandidate(Vector2 direction, float length, float alpha, Color color)
            {
                Direction = direction;
                Length = length;
                Alpha = alpha;
                Color = color;
            }
        }

        private readonly struct SpriteShadowFrame
        {
            public readonly Sprite Sprite;
            public readonly Texture2D Texture;
            public readonly Vector3 SourceCenter;
            public readonly Vector3 SourceRight3;
            public readonly Vector3 SourceUp3;
            public readonly Vector2 SourceRight2;
            public readonly Vector2 SourceUp2;
            public readonly float SourceWidth;
            public readonly float SourceHeight;
            public readonly Vector4 SpriteUvMinSize;

            public SpriteShadowFrame(
                Sprite sprite,
                Texture2D texture,
                Vector3 sourceCenter,
                Vector3 sourceRight3,
                Vector3 sourceUp3,
                Vector2 sourceRight2,
                Vector2 sourceUp2,
                float sourceWidth,
                float sourceHeight,
                Vector4 spriteUvMinSize)
            {
                Sprite = sprite;
                Texture = texture;
                SourceCenter = sourceCenter;
                SourceRight3 = sourceRight3;
                SourceUp3 = sourceUp3;
                SourceRight2 = sourceRight2;
                SourceUp2 = sourceUp2;
                SourceWidth = sourceWidth;
                SourceHeight = sourceHeight;
                SpriteUvMinSize = spriteUvMinSize;
            }
        }

        private sealed class ShadowProxy
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly MeshFilter Filter;
            public readonly MeshRenderer Renderer;
            public readonly Mesh Mesh;
            public readonly MaterialPropertyBlock PropertyBlock;
            public readonly Vector3[] Vertices;

            public ShadowProxy(GameObject gameObject, MeshFilter filter, MeshRenderer renderer, Mesh mesh)
            {
                GameObject = gameObject;
                Transform = gameObject != null ? gameObject.transform : null;
                Filter = filter;
                Renderer = renderer;
                Mesh = mesh;
                PropertyBlock = new MaterialPropertyBlock();
                Vertices = new Vector3[4];
            }

            public void SetActive(bool active)
            {
                if (GameObject != null && active && !GameObject.activeSelf)
                {
                    GameObject.SetActive(true);
                }

                if (Renderer != null && Renderer.enabled != active)
                {
                    Renderer.enabled = active;
                }
            }
        }
    }
}
