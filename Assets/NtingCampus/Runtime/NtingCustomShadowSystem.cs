using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NtingCustomShadowSystem : MonoBehaviour
    {
        private const string SystemName = "Nting Custom Shadow System";

        [Tooltip("场景里的阴影调试参数组件。通常指向 ShadowManager 上的 NtingShadowSceneSettings。")]
        public NtingShadowSceneSettings sceneSettings;
        [HideInInspector]
        public bool systemEnabled = NtingShadowSystemSettings.SystemEnabled;
        [HideInInspector]
        public bool disableUnityLight2DShadows = NtingShadowSystemSettings.DisableUnityLight2DShadows;
        [HideInInspector]
        public bool enablePointObjectShadows = NtingShadowSystemSettings.EnablePointShadows;
        [HideInInspector]
        public bool enablePointWallShadows = NtingShadowSystemSettings.EnablePointShadows;
        [HideInInspector]
        public bool enableSunObjectShadows = NtingShadowSystemSettings.EnableSunShadows;
        [HideInInspector]
        public bool excludeSunLightFromPointShadows = NtingShadowSystemSettings.ExcludeSunLightFromPointShadows;
        [HideInInspector]
        public bool autoRegisterPlacedObjects = NtingShadowSystemSettings.AutoRegisterPlacedObjects;
        [HideInInspector]
        [Min(1)] public int maxPointLights = NtingShadowSystemSettings.MaxPointLights;
        [HideInInspector]
        [Min(1)] public int maxPointLightShadowsPerCaster = NtingShadowSystemSettings.MaxPointLightShadowsPerCaster;
        [HideInInspector]
        [Range(8, 256)] public int objectShadowSampleCount = NtingShadowSystemSettings.ObjectShadowSampleCount;
        [HideInInspector]
        [Min(0f)] public float pointShadowMaxLengthWorld = NtingShadowSystemSettings.PointShadowMaxLengthWorld;
        [HideInInspector]
        [Range(0f, 1f)] public float pointShadowAlpha = NtingShadowSystemSettings.PointShadowAlpha;
        [HideInInspector]
        [Range(0.01f, 2f)] public float pointShadowIntensityScale = NtingShadowSystemSettings.PointShadowIntensityScale;
        [HideInInspector]
        [Min(0f)] public float wallPointShadowMaxLengthWorld = NtingShadowSystemSettings.PointShadowMaxLengthWorld;
        [HideInInspector]
        [Range(0f, 1f)] public float wallPointShadowAlpha = NtingShadowSystemSettings.PointShadowAlpha;
        [HideInInspector]
        [Range(0.01f, 2f)] public float wallPointShadowIntensityScale = NtingShadowSystemSettings.PointShadowIntensityScale;
        [HideInInspector] public float pointLightShadowFillStrength = NtingShadowSystemSettings.PointLightShadowFillStrength;
        [HideInInspector] public float pointLightShadowFillIntensityScale = NtingShadowSystemSettings.PointLightShadowFillIntensityScale;
        [HideInInspector] public float pointLightFillRangeExponent = NtingShadowSystemSettings.PointLightFillRangeExponent;
        [HideInInspector] public float pointLightFillContributionBoost = NtingShadowSystemSettings.PointLightFillContributionBoost;
        [HideInInspector] public float maxSunShadowPointLightFillStrength = NtingShadowSystemSettings.MaxSunShadowPointLightFillStrength;
        [HideInInspector] public float pointLightShadowColorInfluence = NtingShadowSystemSettings.PointLightShadowColorInfluence;
        [HideInInspector]
        [Min(0f)] public float sunObjectShadowMaxLengthWorld = NtingShadowSystemSettings.SunShadowMaxLengthWorld;
        [HideInInspector]
        public bool scaleSunObjectShadowLengthByDayNight = NtingShadowSystemSettings.ScaleSunShadowLengthByDayNight;
        [HideInInspector]
        public bool scaleSunShadowAlphaByDayNight = NtingShadowSystemSettings.ScaleSunShadowAlphaByDayNight;
        [HideInInspector]
        [Range(0f, 1f)] public float sunObjectShadowAlpha = NtingShadowSystemSettings.SunShadowAlpha;
        [HideInInspector] public float maxDayNightShadowOpacityFactor = NtingShadowSystemSettings.MaxDayNightShadowOpacityFactor;
        [HideInInspector] public Color shadowColor = NtingShadowSystemSettings.BaseShadowColor;
        [HideInInspector]
        [Min(0.05f)] public float scanInterval = NtingShadowSystemSettings.ScanInterval;
        [HideInInspector] public float minScanInterval = NtingShadowSystemSettings.MinScanInterval;
        [HideInInspector]
        public int shadowSortingOrderOffset = NtingShadowSystemSettings.ShadowSortingOrderOffset;

        private readonly List<NtingShadowCasterProfile> casterProfiles = new List<NtingShadowCasterProfile>(256);
        private readonly List<CampusProjectedWallShadowRenderer> sunWallRenderers = new List<CampusProjectedWallShadowRenderer>(16);
        private readonly List<NtingWallPointShadowRenderer> wallRenderers = new List<NtingWallPointShadowRenderer>(16);
        private readonly List<ShadowLightInfo> pointLights = new List<ShadowLightInfo>(16);
        private readonly List<Light2D> allLights = new List<Light2D>(32);
        private readonly List<CampusFloorRoot> floors = new List<CampusFloorRoot>(8);

        private float nextScanTime;
        private bool forceScan = true;
        private static NtingCustomShadowSystem activeSystem;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeSystem()
        {
            EnsureSceneSystem();
        }

        public static NtingCustomShadowSystem EnsureSceneSystem()
        {
            NtingCustomShadowSystem system = Object.FindFirstObjectByType<NtingCustomShadowSystem>(FindObjectsInactive.Include);
            if (system != null)
            {
                system.EnsureSceneSettings();
                return system;
            }

            GameObject systemObject = new GameObject(SystemName);
            systemObject.AddComponent<NtingShadowSceneSettings>();
            system = systemObject.AddComponent<NtingCustomShadowSystem>();
            return system;
        }

        public void MarkSceneDirty()
        {
            forceScan = true;
        }

        public void ApplyRuntimeSettingChanges()
        {
            ApplySceneShadowSettings(false);
            MarkSceneDirty();
        }

        public void ApplyDeclaredShadowSettings()
        {
            ApplySceneShadowSettings(true);
        }

        private void ApplySceneShadowSettings(bool markDirty)
        {
            NtingShadowSceneSettings settings = ResolveSceneSettings(markDirty);
            if (settings != null)
            {
                settings.ApplyTo(this);
            }

            ClampShadowSettings();
            activeSystem = this;
            if (markDirty)
            {
                MarkSceneDirty();
            }
        }

        private void OnEnable()
        {
            EnsureSceneSettings();
            ApplySceneShadowSettings(true);
            activeSystem = this;
            forceScan = true;
            RefreshNow();
        }

        private void OnDisable()
        {
            if (activeSystem == this)
            {
                activeSystem = null;
            }

            HideCustomShadows();
        }

        private void OnValidate()
        {
            ApplySceneShadowSettings(false);
            forceScan = true;
        }

        private void EnsureSceneSettings()
        {
            ResolveSceneSettings(true);
        }

        private NtingShadowSceneSettings ResolveSceneSettings(bool createIfMissing)
        {
            if (sceneSettings != null)
            {
                return sceneSettings;
            }

            sceneSettings = GetComponent<NtingShadowSceneSettings>();
            if (sceneSettings == null)
            {
                sceneSettings = Object.FindFirstObjectByType<NtingShadowSceneSettings>(FindObjectsInactive.Include);
            }

            if (sceneSettings == null && createIfMissing)
            {
                sceneSettings = gameObject.AddComponent<NtingShadowSceneSettings>();
            }

            return sceneSettings;
        }

        private void ClampShadowSettings()
        {
            enablePointWallShadows = enablePointObjectShadows;
            maxPointLights = Mathf.Max(1, maxPointLights);
            maxPointLightShadowsPerCaster = Mathf.Max(1, maxPointLightShadowsPerCaster);
            objectShadowSampleCount = Mathf.Clamp(objectShadowSampleCount, 8, 256);
            pointShadowMaxLengthWorld = Mathf.Max(0f, pointShadowMaxLengthWorld);
            sunObjectShadowMaxLengthWorld = Mathf.Max(0f, sunObjectShadowMaxLengthWorld);
            pointShadowAlpha = Mathf.Clamp01(pointShadowAlpha);
            pointShadowIntensityScale = Mathf.Max(0.01f, pointShadowIntensityScale);
            wallPointShadowMaxLengthWorld = pointShadowMaxLengthWorld;
            wallPointShadowAlpha = pointShadowAlpha;
            wallPointShadowIntensityScale = pointShadowIntensityScale;
            pointLightShadowFillStrength = Mathf.Clamp01(pointLightShadowFillStrength);
            pointLightShadowFillIntensityScale = Mathf.Max(0.01f, pointLightShadowFillIntensityScale);
            pointLightFillRangeExponent = Mathf.Max(0.01f, pointLightFillRangeExponent);
            pointLightFillContributionBoost = Mathf.Max(0f, pointLightFillContributionBoost);
            maxSunShadowPointLightFillStrength = Mathf.Clamp01(maxSunShadowPointLightFillStrength);
            pointLightShadowColorInfluence = Mathf.Clamp01(pointLightShadowColorInfluence);
            sunObjectShadowAlpha = Mathf.Clamp01(sunObjectShadowAlpha);
            maxDayNightShadowOpacityFactor = Mathf.Max(0.001f, maxDayNightShadowOpacityFactor);
            shadowColor.a = 1f;
            minScanInterval = Mathf.Max(0.001f, minScanInterval);
            scanInterval = Mathf.Max(minScanInterval, scanInterval);
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        public void RefreshNow()
        {
            ApplySceneShadowSettings(false);
            if (!systemEnabled)
            {
                HideCustomShadows();
                return;
            }

            if (forceScan || Time.realtimeSinceStartup >= nextScanTime)
            {
                ScanScene();
                nextScanTime = Time.realtimeSinceStartup + Mathf.Max(minScanInterval, scanInterval);
                forceScan = false;
            }

            CollectPointLights();
            if (disableUnityLight2DShadows)
            {
                DisableUnityLightShadows();
            }

            int sortingLayerId = ResolveGroundSortingLayerId();
            RefreshCasterProfiles(sortingLayerId);
            RefreshWallSunShadows();
            RefreshWallPointShadows(sortingLayerId);
        }

        private void ScanScene()
        {
            NtingShadowCasterProfile.PurgeLegacySpriteProxyObjectsInSceneOnce();

            if (autoRegisterPlacedObjects)
            {
                CampusPlacedObject[] placedObjects = Object.FindObjectsByType<CampusPlacedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < placedObjects.Length; i++)
                {
                    CampusPlacedObject placed = placedObjects[i];
                    if (placed != null && placed.GetComponentInChildren<SpriteRenderer>(true) != null)
                    {
                        NtingShadowCasterProfile.EnsureForPlacedObject(placed);
                    }
                }
            }

            casterProfiles.Clear();
            NtingShadowCasterProfile[] profiles = Object.FindObjectsByType<NtingShadowCasterProfile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < profiles.Length; i++)
            {
                if (profiles[i] != null)
                {
                    casterProfiles.Add(profiles[i]);
                }
            }

            floors.Clear();
            CampusFloorRoot[] sceneFloors = Object.FindObjectsByType<CampusFloorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneFloors.Length; i++)
            {
                if (sceneFloors[i] != null)
                {
                    floors.Add(sceneFloors[i]);
                }
            }

            sunWallRenderers.Clear();
            for (int i = 0; i < floors.Count; i++)
            {
                CampusProjectedWallShadowRenderer renderer = CampusProjectedWallShadowRenderer.EnsureForFloor(floors[i]);
                if (renderer != null)
                {
                    sunWallRenderers.Add(renderer);
                }
            }

            wallRenderers.Clear();
            if (enablePointWallShadows)
            {
                for (int i = 0; i < floors.Count; i++)
                {
                    NtingWallPointShadowRenderer renderer = NtingWallPointShadowRenderer.EnsureForFloor(floors[i]);
                    if (renderer != null)
                    {
                        wallRenderers.Add(renderer);
                    }
                }
            }
        }

        private void CollectPointLights()
        {
            allLights.Clear();
            pointLights.Clear();
            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light == null)
                {
                    continue;
                }

                allLights.Add(light);
                if (!IsPointShadowLight(light))
                {
                    continue;
                }

                float radius = Mathf.Max(0.001f, light.pointLightOuterRadius);
                pointLights.Add(CreateShadowLightInfo(light, radius));
            }

            pointLights.Sort(CompareLights);
            if (pointLights.Count > maxPointLights)
            {
                pointLights.RemoveRange(maxPointLights, pointLights.Count - maxPointLights);
            }
        }

        private bool IsPointShadowLight(Light2D light)
        {
            return IsPointLightUsableForCustomShadow(light, excludeSunLightFromPointShadows);
        }

        private void DisableUnityLightShadows()
        {
            for (int i = 0; i < allLights.Count; i++)
            {
                Light2D light = allLights[i];
                if (light == null || light.lightType == Light2D.LightType.Global)
                {
                    continue;
                }

                light.shadowsEnabled = false;
                light.shadowIntensity = 0f;
            }
        }

        private void RefreshCasterProfiles(int sortingLayerId)
        {
            ShadowLightInfo[] lights = pointLights.ToArray();
            int lightCount = lights.Length;
            CampusDayNightController dayNight = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            Vector2 sunDirection = ResolveSunDirection(dayNight);
            float sunShadowLengthWorld = ResolveSunObjectShadowLengthWorld(dayNight);
            float sunShadowAlpha = ResolveSunObjectShadowAlpha(dayNight);
            Color sunShadowColor = ResolveSunShadowColor(dayNight);
            float sunShadowFillStrength = ResolveSunShadowPointLightFillStrength();
            for (int i = 0; i < casterProfiles.Count; i++)
            {
                NtingShadowCasterProfile profile = casterProfiles[i];
                if (profile == null)
                {
                    continue;
                }

                int sortingOrder = ResolveGroundSortingOrder(profile);
                profile.RefreshCustomShadows(
                    lights,
                    lightCount,
                    enablePointObjectShadows,
                    enableSunObjectShadows,
                    sunDirection,
                    sunShadowLengthWorld,
                    sunShadowAlpha,
                    shadowColor,
                    sunShadowColor,
                    pointShadowMaxLengthWorld,
                    pointShadowAlpha,
                    pointShadowIntensityScale,
                    sunShadowFillStrength,
                    pointLightShadowFillIntensityScale,
                    maxPointLightShadowsPerCaster,
                    objectShadowSampleCount,
                    sortingLayerId,
                    sortingOrder);
            }
        }

        private void RefreshWallPointShadows(int sortingLayerId)
        {
            ShadowLightInfo[] lights = pointLights.ToArray();
            int lightCount = lights.Length;
            for (int i = 0; i < wallRenderers.Count; i++)
            {
                NtingWallPointShadowRenderer renderer = wallRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                int sortingOrder = ResolveGroundSortingOrder(renderer);
                renderer.RefreshShadows(
                    lights,
                    lightCount,
                    enablePointWallShadows,
                    pointShadowMaxLengthWorld,
                    pointShadowAlpha,
                    pointShadowIntensityScale,
                    pointLightShadowFillStrength,
                    pointLightShadowFillIntensityScale,
                    ResolveWallShadowColor(),
                    maxPointLights,
                    sortingLayerId,
                    sortingOrder);
            }
        }

        private void RefreshWallSunShadows()
        {
            bool enabled = systemEnabled && enableSunObjectShadows;
            CampusDayNightController dayNight = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            float sunShadowAlpha = ResolveSunObjectShadowAlpha(dayNight);
            Color wallSunShadowColor = ResolveWallSunShadowColor(dayNight);
            float sunShadowFillStrength = ResolveSunShadowPointLightFillStrength();
            for (int i = 0; i < sunWallRenderers.Count; i++)
            {
                CampusProjectedWallShadowRenderer renderer = sunWallRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                bool changed = renderer.ApplyRuntimeSunShadowSettings(
                    enabled,
                    sunObjectShadowMaxLengthWorld,
                    sunShadowAlpha,
                    scaleSunObjectShadowLengthByDayNight,
                    wallSunShadowColor,
                    sunShadowFillStrength,
                    pointLightShadowFillIntensityScale,
                    maxPointLights);
                if (changed)
                {
                    renderer.RefreshRuntimeSunShadowNow();
                }
            }
        }

        private void HideCustomShadows()
        {
            for (int i = 0; i < casterProfiles.Count; i++)
            {
                if (casterProfiles[i] != null)
                {
                    casterProfiles[i].RefreshCustomShadows(null, 0, false, false, Vector2.down, 0f, 0f, shadowColor, shadowColor, 0f, 0f, pointShadowIntensityScale, pointLightShadowFillStrength, pointLightShadowFillIntensityScale, 0, objectShadowSampleCount, ResolveGroundSortingLayerId(), 0);
                }
            }

            for (int i = 0; i < wallRenderers.Count; i++)
            {
                if (wallRenderers[i] != null)
                {
                    wallRenderers[i].RefreshShadows(null, 0, false, 0f, 0f, pointShadowIntensityScale, pointLightShadowFillStrength, pointLightShadowFillIntensityScale, ResolveWallShadowColor(), 0, ResolveGroundSortingLayerId(), 0);
                }
            }

            CampusProjectedWallShadowRenderer[] projectedWallRenderers = Object.FindObjectsByType<CampusProjectedWallShadowRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < projectedWallRenderers.Length; i++)
            {
                CampusProjectedWallShadowRenderer renderer = projectedWallRenderers[i];
                if (renderer != null)
                {
                    if (renderer.ApplyRuntimeSunShadowSettings(false, 0f, 0f, scaleSunObjectShadowLengthByDayNight, ResolveWallShadowColor(), pointLightShadowFillStrength, pointLightShadowFillIntensityScale, maxPointLights))
                    {
                        renderer.RefreshRuntimeSunShadowNow();
                    }
                }
            }
        }

        private Color ResolveWallShadowColor()
        {
            return ResolveWallShadowColor(shadowColor);
        }

        private Color ResolveWallSunShadowColor(CampusDayNightController dayNight)
        {
            return ResolveWallShadowColor(ResolveSunShadowColor(dayNight));
        }

        private Color ResolveWallShadowColor(Color baseColor)
        {
            Color color = baseColor;
            color.r = Mathf.Clamp01(color.r * baseColor.r);
            color.g = Mathf.Clamp01(color.g * baseColor.g);
            color.b = Mathf.Clamp01(color.b * baseColor.b);
            color.a = 1f;
            return color;
        }

        private Color ResolveSunShadowColor(CampusDayNightController dayNight)
        {
            Color color = shadowColor;
            if (dayNight != null)
            {
                Color multiplier = dayNight.ShadowColorMultiplier;
                color.r = Mathf.Clamp01(color.r * multiplier.r);
                color.g = Mathf.Clamp01(color.g * multiplier.g);
                color.b = Mathf.Clamp01(color.b * multiplier.b);
            }

            color.a = 1f;
            return color;
        }

        private float ResolveSunObjectShadowLengthWorld(CampusDayNightController dayNight)
        {
            float lengthFactor = 1f;
            if (scaleSunObjectShadowLengthByDayNight && dayNight != null)
            {
                lengthFactor = SanitizeUnitValue(dayNight.ShadowLengthFactor, 1f);
            }

            return Mathf.Max(0f, sunObjectShadowMaxLengthWorld * lengthFactor);
        }

        private float ResolveSunObjectShadowAlpha(CampusDayNightController dayNight)
        {
            float alphaFactor = 1f;
            if (scaleSunShadowAlphaByDayNight && dayNight != null)
            {
                alphaFactor = SanitizeUnitValue(dayNight.ShadowOpacityFactor / maxDayNightShadowOpacityFactor, 1f);
            }

            return Mathf.Clamp01(sunObjectShadowAlpha * alphaFactor);
        }

        private static float SanitizeUnitValue(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? Mathf.Clamp01(fallback) : Mathf.Clamp01(value);
        }

        public static Color ResolvePointLightShadowColor(Color baseShadowColor, ShadowLightInfo light)
        {
            float brightness = Mathf.Clamp01(Mathf.Max(baseShadowColor.r, baseShadowColor.g, baseShadowColor.b));
            Color tint = light.ShadowColorTint;
            return new Color(
                Mathf.Clamp01(brightness * tint.r),
                Mathf.Clamp01(brightness * tint.g),
                Mathf.Clamp01(brightness * tint.b),
                1f);
        }

        private static Color ResolvePointLightShadowTint(Color lightColor)
        {
            float maxChannel = Mathf.Max(lightColor.r, lightColor.g, lightColor.b);
            if (maxChannel <= 0.0001f || float.IsNaN(maxChannel) || float.IsInfinity(maxChannel))
            {
                return Color.white;
            }

            Color normalizedLightColor = new Color(
                Mathf.Clamp01(lightColor.r / maxChannel),
                Mathf.Clamp01(lightColor.g / maxChannel),
                Mathf.Clamp01(lightColor.b / maxChannel),
                1f);

            Color tint = Color.Lerp(Color.white, normalizedLightColor, ResolveActivePointLightShadowColorInfluence());
            tint.a = 1f;
            return tint;
        }

        private float ResolveSunShadowPointLightFillStrength()
        {
            return Mathf.Min(pointLightShadowFillStrength, maxSunShadowPointLightFillStrength);
        }

        private static float ResolveActivePointLightShadowColorInfluence()
        {
            return activeSystem != null ? activeSystem.pointLightShadowColorInfluence : NtingShadowSystemSettings.PointLightShadowColorInfluence;
        }

        private static float ResolveActivePointLightFillRangeExponent()
        {
            return activeSystem != null ? activeSystem.pointLightFillRangeExponent : NtingShadowSystemSettings.PointLightFillRangeExponent;
        }

        private static float ResolveActivePointLightFillContributionBoost()
        {
            return activeSystem != null ? activeSystem.pointLightFillContributionBoost : NtingShadowSystemSettings.PointLightFillContributionBoost;
        }

        private static Vector2 ResolveSunDirection(CampusDayNightController dayNight)
        {
            if (dayNight != null && dayNight.ShadowDirection.sqrMagnitude > 0.0001f)
            {
                return dayNight.ShadowDirection.normalized;
            }

            return new Vector2(1f, -0.4f).normalized;
        }

        private int ResolveGroundSortingLayerId()
        {
            return CampusRenderSortingUtility.ResolveGroundShadowSortingLayerId(SortingLayer.NameToID("Default"));
        }

        private int ResolveGroundSortingOrder(Component component)
        {
            CampusFloorRoot floor = component != null ? component.GetComponentInParent<CampusFloorRoot>() : null;
            if (floor != null && floor.FloorTilemap != null)
            {
                Renderer renderer = floor.FloorTilemap.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return renderer.sortingOrder + shadowSortingOrderOffset;
                }
            }

            return shadowSortingOrderOffset;
        }

        private static int CompareLights(ShadowLightInfo left, ShadowLightInfo right)
        {
            int intensity = right.SortWeight.CompareTo(left.SortWeight);
            return intensity != 0 ? intensity : left.InstanceId.CompareTo(right.InstanceId);
        }

        public static bool IsPointLightUsableForCustomShadow(Light2D light, bool excludeSunLight)
        {
            if (light == null || !light.isActiveAndEnabled || light.lightType != Light2D.LightType.Point || light.intensity <= 0f)
            {
                return false;
            }

            if (excludeSunLight && IsDayNightSunLight(light))
            {
                return false;
            }

            return true;
        }

        private static bool IsDayNightSunLight(Light2D light)
        {
            if (light == null)
            {
                return false;
            }

            if (CampusObjectNames.MatchesAny(light.gameObject.name, CampusObjectNames.SunLight2D, CampusObjectNames.LegacySunLight2D))
            {
                return true;
            }

            CampusDayNightController dayNight = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            return dayNight != null && dayNight.SunLight == light;
        }

        public static ShadowLightInfo CreateShadowLightInfo(Light2D light)
        {
            float radius = light != null ? Mathf.Max(0.001f, light.pointLightOuterRadius) : 0.001f;
            return CreateShadowLightInfo(light, radius);
        }

        private static ShadowLightInfo CreateShadowLightInfo(Light2D light, float radius)
        {
            if (light == null)
            {
                return new ShadowLightInfo(Vector3.zero, radius, 0f, 0);
            }

            NtingPointLightShadowProfile profile = light.GetComponent<NtingPointLightShadowProfile>();
            float lengthWeight = profile != null ? profile.shadowLengthWeight : 1f;
            float alphaWeight = profile != null ? profile.shadowAlphaWeight : 1f;
            float fillWeight = profile != null ? profile.shadowFillWeight : 1f;
            return new ShadowLightInfo(
                light.transform.position,
                radius,
                Mathf.Max(0f, light.intensity),
                light.GetInstanceID(),
                lengthWeight,
                alphaWeight,
                fillWeight,
                light.color);
        }

        public static float ResolvePointLightFillMultiplier(
            ShadowLightInfo[] pointLights,
            int pointLightCount,
            int ignoredLightInstanceId,
            Vector2 sampleWorldPosition,
            float fillStrength,
            float fillIntensityScale)
        {
            if (pointLights == null || pointLightCount <= 0 || fillStrength <= 0f)
            {
                return 1f;
            }

            float combinedFill = 0f;
            int count = Mathf.Min(pointLightCount, pointLights.Length);
            for (int i = 0; i < count; i++)
            {
                ShadowLightInfo light = pointLights[i];
                if (light.InstanceId == ignoredLightInstanceId)
                {
                    continue;
                }

                float distance = Vector2.Distance(sampleWorldPosition, light.Position);
                if (distance >= light.Radius)
                {
                    continue;
                }

                float contribution = ResolvePointLightFillContribution(light, distance, fillIntensityScale);
                combinedFill = CombinePointLightFill(combinedFill, contribution);
                if (combinedFill >= 0.999f)
                {
                    break;
                }
            }

            return Mathf.Clamp01(1f - combinedFill * Mathf.Clamp01(fillStrength));
        }

        public static float ResolvePointLightFillMultiplier(
            IReadOnlyList<ShadowLightInfo> pointLights,
            int pointLightCount,
            int ignoredLightInstanceId,
            Vector2 sampleWorldPosition,
            float fillStrength,
            float fillIntensityScale)
        {
            if (pointLights == null || pointLightCount <= 0 || fillStrength <= 0f)
            {
                return 1f;
            }

            float combinedFill = 0f;
            int count = Mathf.Min(pointLightCount, pointLights.Count);
            for (int i = 0; i < count; i++)
            {
                ShadowLightInfo light = pointLights[i];
                if (light.InstanceId == ignoredLightInstanceId)
                {
                    continue;
                }

                float distance = Vector2.Distance(sampleWorldPosition, light.Position);
                if (distance >= light.Radius)
                {
                    continue;
                }

                float contribution = ResolvePointLightFillContribution(light, distance, fillIntensityScale);
                combinedFill = CombinePointLightFill(combinedFill, contribution);
                if (combinedFill >= 0.999f)
                {
                    break;
                }
            }

            return Mathf.Clamp01(1f - combinedFill * Mathf.Clamp01(fillStrength));
        }

        private static float ResolvePointLightFillContribution(ShadowLightInfo light, float distance, float fillIntensityScale)
        {
            float rangeFactor = 1f - Mathf.Clamp01(distance / light.Radius);
            float softenedRange = Mathf.Pow(rangeFactor, ResolveActivePointLightFillRangeExponent());
            float intensityFactor = Mathf.Clamp01(light.Intensity * light.ShadowFillWeight * Mathf.Max(0.001f, fillIntensityScale) * ResolveActivePointLightFillContributionBoost());
            return Mathf.Clamp01(softenedRange * intensityFactor);
        }

        private static float CombinePointLightFill(float currentFill, float contribution)
        {
            return Mathf.Clamp01(1f - ((1f - currentFill) * (1f - contribution)));
        }

        public readonly struct ShadowLightInfo
        {
            public readonly Vector3 Position;
            public readonly float Radius;
            public readonly float Intensity;
            public readonly int InstanceId;
            public readonly float ShadowLengthWeight;
            public readonly float ShadowAlphaWeight;
            public readonly float ShadowFillWeight;
            public readonly Color ShadowColorTint;

            public ShadowLightInfo(
                Vector3 position,
                float radius,
                float intensity,
                int instanceId,
                float shadowLengthWeight = 1f,
                float shadowAlphaWeight = 1f,
                float shadowFillWeight = 1f,
                Color lightColor = default)
            {
                Position = position;
                Radius = Mathf.Max(0.001f, radius);
                Intensity = Mathf.Max(0f, intensity);
                InstanceId = instanceId;
                ShadowLengthWeight = Mathf.Max(0f, shadowLengthWeight);
                ShadowAlphaWeight = Mathf.Max(0f, shadowAlphaWeight);
                ShadowFillWeight = Mathf.Max(0f, shadowFillWeight);
                ShadowColorTint = ResolvePointLightShadowTint(lightColor);
            }

            public float SortWeight => Intensity * Mathf.Max(ShadowAlphaWeight, ShadowFillWeight);
        }
    }
}
