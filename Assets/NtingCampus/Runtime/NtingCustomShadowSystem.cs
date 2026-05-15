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
        private const float MinimumSceneScanInterval = 0.25f;
        private const float ObjectShadowValueQuantizeStep = 0.03f;
        private const float ObjectShadowDirectionQuantizeDegrees = 2f;
        private static readonly Color WallCasterShadowBaseColor = new Color(0.04f, 0.06f, 0.09f, 1f);

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
        private readonly List<CampusPlacedObject> placedObjects = new List<CampusPlacedObject>(128);
        private readonly List<NtingShadowCasterProfile> scratchProfiles = new List<NtingShadowCasterProfile>(256);

        private ShadowLightInfo[] pointLightSnapshot = System.Array.Empty<ShadowLightInfo>();
        private int pointLightSnapshotCount;
        private int casterProfileSetSignature;
        private CampusDayNightController cachedDayNight;
        private CampusMapRoot cachedMapRoot;
        private float nextScanTime;
        private int lastObjectShadowBatchSignature = int.MinValue;
        private bool forceScan = true;
        private static NtingCustomShadowSystem activeSystem;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeSystem()
        {
            NtingCustomShadowSystem system = Object.FindFirstObjectByType<NtingCustomShadowSystem>(FindObjectsInactive.Include);
            if (system != null)
            {
                system.EnsureSceneSettings();
            }
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

        internal static bool HasActiveSystemInstance()
        {
            return activeSystem != null && activeSystem.isActiveAndEnabled;
        }

        public void MarkSceneDirty()
        {
            forceScan = true;
            lastObjectShadowBatchSignature = int.MinValue;
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
            maxPointLights = Mathf.Clamp(maxPointLights, 1, NtingShadowSystemSettings.MaxPointLights);
            maxPointLightShadowsPerCaster = Mathf.Max(1, maxPointLightShadowsPerCaster);
            objectShadowSampleCount = Mathf.Clamp(objectShadowSampleCount, 8, NtingShadowSystemSettings.ObjectShadowSampleCount);
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
            minScanInterval = Mathf.Max(MinimumSceneScanInterval, minScanInterval);
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

            bool forceCasterRefresh = false;
            if (forceScan || Time.realtimeSinceStartup >= nextScanTime)
            {
                bool autoRegisterMissingProfiles = forceScan;
                forceCasterRefresh = forceScan;
                ScanScene(autoRegisterMissingProfiles);
                nextScanTime = Time.realtimeSinceStartup + Mathf.Max(minScanInterval, scanInterval);
                forceScan = false;
            }

            CollectPointLights();
            UpdatePointLightSnapshot();
            if (disableUnityLight2DShadows)
            {
                DisableUnityLightShadows();
            }

            int sortingLayerId = ResolveGroundSortingLayerId();
            RefreshCasterProfiles(sortingLayerId, forceCasterRefresh);
            RefreshWallSunShadows();
            RefreshWallPointShadows(sortingLayerId);
        }

        private void ScanScene(bool autoRegisterMissingProfiles)
        {
            NtingShadowCasterProfile.PurgeLegacySpriteProxyObjectsInSceneOnce();
            ResolveSceneRoots();
            CampusSceneLightUtility.CollectLights(allLights, true);
            RefreshFloorReferences();

            if (autoRegisterPlacedObjects && autoRegisterMissingProfiles)
            {
                CollectPlacedObjects();
                for (int i = 0; i < placedObjects.Count; i++)
                {
                    CampusPlacedObject placed = placedObjects[i];
                    if (placed != null && placed.GetComponentInChildren<SpriteRenderer>(true) != null)
                    {
                        NtingShadowCasterProfile.EnsureForPlacedObject(placed);
                    }
                }
            }

            casterProfiles.Clear();
            CollectCasterProfiles();
            casterProfileSetSignature = ComputeCasterProfileSetSignature();

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

        private void ResolveSceneRoots()
        {
            if (cachedDayNight == null)
            {
                cachedDayNight = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }

            if (cachedMapRoot == null)
            {
                cachedMapRoot = Object.FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            }
        }

        private void RefreshFloorReferences()
        {
            floors.Clear();
            if (cachedMapRoot != null)
            {
                cachedMapRoot.RebuildFloorReferences();
                for (int i = 0; i < cachedMapRoot.Floors.Count; i++)
                {
                    CampusFloorRoot floor = cachedMapRoot.Floors[i];
                    if (floor != null)
                    {
                        floors.Add(floor);
                    }
                }

                return;
            }

            CampusFloorRoot[] sceneFloors = Object.FindObjectsByType<CampusFloorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneFloors.Length; i++)
            {
                if (sceneFloors[i] != null)
                {
                    floors.Add(sceneFloors[i]);
                }
            }
        }

        private void CollectPlacedObjects()
        {
            placedObjects.Clear();
            for (int i = 0; i < floors.Count; i++)
            {
                CampusFloorRoot floor = floors[i];
                if (floor == null || floor.PropsRoot == null)
                {
                    continue;
                }

                floor.PropsRoot.GetComponentsInChildren(true, placedObjects);
            }
        }

        private void CollectCasterProfiles()
        {
            scratchProfiles.Clear();
            NtingShadowCasterProfile[] sceneProfiles = Object.FindObjectsByType<NtingShadowCasterProfile>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (sceneProfiles == null || sceneProfiles.Length == 0)
            {
                return;
            }

            for (int i = 0; i < sceneProfiles.Length; i++)
            {
                NtingShadowCasterProfile profile = sceneProfiles[i];
                if (profile == null)
                {
                    continue;
                }

                scratchProfiles.Add(profile);
            }

            for (int i = 0; i < scratchProfiles.Count; i++)
            {
                NtingShadowCasterProfile profile = scratchProfiles[i];
                if (profile != null)
                {
                    casterProfiles.Add(profile);
                }
            }
        }

        private void CollectPointLights()
        {
            pointLights.Clear();
            for (int i = allLights.Count - 1; i >= 0; i--)
            {
                Light2D light = allLights[i];
                if (light == null)
                {
                    allLights.RemoveAt(i);
                    continue;
                }

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

        private int ComputeCasterProfileSetSignature()
        {
            unchecked
            {
                int signature = 17;
                signature = signature * 31 + casterProfiles.Count;
                for (int i = 0; i < casterProfiles.Count; i++)
                {
                    NtingShadowCasterProfile profile = casterProfiles[i];
                    signature = signature * 31 + (profile != null ? profile.GetInstanceID() : 0);
                }

                return signature;
            }
        }

        private void UpdatePointLightSnapshot()
        {
            pointLightSnapshotCount = pointLights.Count;
            if (pointLightSnapshot.Length < pointLightSnapshotCount)
            {
                pointLightSnapshot = new ShadowLightInfo[Mathf.NextPowerOfTwo(Mathf.Max(1, pointLightSnapshotCount))];
            }

            for (int i = 0; i < pointLightSnapshotCount; i++)
            {
                pointLightSnapshot[i] = pointLights[i];
            }
        }

        private bool IsPointShadowLight(Light2D light)
        {
            if (!IsPointLightUsableForCustomShadow(light, false))
            {
                return false;
            }

            return !excludeSunLightFromPointShadows || !IsDayNightSunLight(light, cachedDayNight);
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

                if (light.shadowsEnabled)
                {
                    light.shadowsEnabled = false;
                }

                if (!Mathf.Approximately(light.shadowIntensity, 0f))
                {
                    light.shadowIntensity = 0f;
                }
            }
        }

        private void RefreshCasterProfiles(int sortingLayerId, bool force)
        {
            ShadowLightInfo[] lights = pointLightSnapshot;
            int lightCount = pointLightSnapshotCount;
            CampusDayNightController dayNight = cachedDayNight;
            Vector2 sunDirection = ResolveSunDirection(dayNight);
            float sunShadowLengthWorld = ResolveSunObjectShadowLengthWorld(dayNight);
            float sunShadowAlpha = ResolveSunObjectShadowAlpha(dayNight);
            Color sunShadowColor = ResolveSunShadowColor(dayNight);
            float sunShadowFillStrength = ResolveSunShadowPointLightFillStrength();
            int batchSignature = ComputeObjectShadowBatchSignature(
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
                sortingLayerId);
            if (!force && Application.isPlaying)
            {
                if (batchSignature == lastObjectShadowBatchSignature)
                {
                    return;
                }
            }

            lastObjectShadowBatchSignature = batchSignature;

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

        private int ComputeObjectShadowBatchSignature(
            ShadowLightInfo[] lights,
            int lightCount,
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
            int sortingLayerId)
        {
            unchecked
            {
                int signature = 17;
                signature = signature * 31 + casterProfiles.Count;
                signature = signature * 31 + casterProfileSetSignature;
                signature = signature * 31 + (enablePointShadows ? 1 : 0);
                signature = signature * 31 + (enableSunShadow ? 1 : 0);
                signature = signature * 31 + QuantizeObjectShadowDirection(sunDirection);
                signature = signature * 31 + QuantizeObjectShadowValue(sunShadowLengthWorld);
                signature = signature * 31 + QuantizeObjectShadowValue(sunShadowAlpha);
                signature = signature * 31 + QuantizeObjectShadowColor(globalShadowColor.r);
                signature = signature * 31 + QuantizeObjectShadowColor(globalShadowColor.g);
                signature = signature * 31 + QuantizeObjectShadowColor(globalShadowColor.b);
                signature = signature * 31 + QuantizeObjectShadowColor(sunGlobalShadowColor.r);
                signature = signature * 31 + QuantizeObjectShadowColor(sunGlobalShadowColor.g);
                signature = signature * 31 + QuantizeObjectShadowColor(sunGlobalShadowColor.b);
                signature = signature * 31 + QuantizeObjectShadowValue(pointShadowMaxLengthWorld);
                signature = signature * 31 + QuantizeObjectShadowValue(pointShadowAlpha);
                signature = signature * 31 + QuantizeObjectShadowValue(pointShadowIntensityScale);
                signature = signature * 31 + QuantizeObjectShadowValue(pointLightFillStrength);
                signature = signature * 31 + QuantizeObjectShadowValue(pointLightFillIntensityScale);
                signature = signature * 31 + maxPointLightShadows;
                signature = signature * 31 + globalSampleCount;
                signature = signature * 31 + sortingLayerId;
                for (int i = 0; i < casterProfiles.Count; i++)
                {
                    NtingShadowCasterProfile profile = casterProfiles[i];
                    signature = signature * 31 + (profile != null ? profile.ComputeSceneRefreshSignature() : 0);
                }

                int count = lights != null ? Mathf.Min(lightCount, lights.Length, maxPointLights) : 0;
                signature = signature * 31 + count;
                for (int i = 0; i < count; i++)
                {
                    ShadowLightInfo light = lights[i];
                    signature = signature * 31 + light.InstanceId;
                    signature = signature * 31 + QuantizeObjectShadowValue(light.Position.x);
                    signature = signature * 31 + QuantizeObjectShadowValue(light.Position.y);
                    signature = signature * 31 + QuantizeObjectShadowValue(light.Radius);
                    signature = signature * 31 + QuantizeObjectShadowValue(light.Intensity);
                    signature = signature * 31 + QuantizeObjectShadowValue(light.ShadowLengthWeight);
                    signature = signature * 31 + QuantizeObjectShadowValue(light.ShadowAlphaWeight);
                    signature = signature * 31 + QuantizeObjectShadowValue(light.ShadowFillWeight);
                    signature = signature * 31 + QuantizeObjectShadowColor(light.ShadowColorTint.r);
                    signature = signature * 31 + QuantizeObjectShadowColor(light.ShadowColorTint.g);
                    signature = signature * 31 + QuantizeObjectShadowColor(light.ShadowColorTint.b);
                }

                return signature;
            }
        }

        private static int QuantizeObjectShadowValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0;
            }

            return Mathf.RoundToInt(value / ObjectShadowValueQuantizeStep);
        }

        private static int QuantizeObjectShadowColor(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0;
            }

            return Mathf.RoundToInt(Mathf.Clamp01(value) / ObjectShadowValueQuantizeStep);
        }

        private static int QuantizeObjectShadowDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f || float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsInfinity(direction.x) || float.IsInfinity(direction.y))
            {
                return 0;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Mathf.RoundToInt(angle / ObjectShadowDirectionQuantizeDegrees);
        }

        private void RefreshWallPointShadows(int sortingLayerId)
        {
            ShadowLightInfo[] lights = pointLightSnapshot;
            int lightCount = pointLightSnapshotCount;
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
            CampusDayNightController dayNight = cachedDayNight;
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

                renderer.ApplyRuntimeSunShadowSettings(
                    enabled,
                    sunObjectShadowMaxLengthWorld,
                    sunShadowAlpha,
                    scaleSunObjectShadowLengthByDayNight,
                    wallSunShadowColor,
                    sunShadowFillStrength,
                    pointLightShadowFillIntensityScale,
                    maxPointLights);
                renderer.ApplyProjectedFillLights(pointLights, pointLights.Count);
                renderer.RefreshRuntimeSunShadowIfNeeded();
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
            return new Color(
                Mathf.Clamp01(WallCasterShadowBaseColor.r * baseColor.r),
                Mathf.Clamp01(WallCasterShadowBaseColor.g * baseColor.g),
                Mathf.Clamp01(WallCasterShadowBaseColor.b * baseColor.b),
                1f);
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
            if (TryResolveExplicitFloorSortingOrder(component, out int explicitSortingOrder))
            {
                return explicitSortingOrder;
            }

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

        private bool TryResolveExplicitFloorSortingOrder(Component component, out int sortingOrder)
        {
            sortingOrder = 0;
            if (!TryResolveFloorIndex(component, out int floorIndex))
            {
                return false;
            }

            CampusMapRoot mapRoot = cachedMapRoot;
            if (mapRoot == null)
            {
                mapRoot = Object.FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
                cachedMapRoot = mapRoot;
            }

            int sortingStep = mapRoot != null ? mapRoot.SortingOrderStepPerFloor : 1000;
            sortingOrder = Mathf.Max(1, floorIndex) * sortingStep + CampusRenderSortingUtility.FloorOffset + shadowSortingOrderOffset;
            return true;
        }

        private static bool TryResolveFloorIndex(Component component, out int floorIndex)
        {
            floorIndex = 0;
            if (component == null)
            {
                return false;
            }

            CampusPlacedObject placedObject = component.GetComponent<CampusPlacedObject>();
            if (placedObject == null)
            {
                placedObject = component.GetComponentInParent<CampusPlacedObject>();
            }

            if (placedObject != null)
            {
                floorIndex = Mathf.Max(1, placedObject.FloorIndex);
                return true;
            }

            CampusTestPlayerController playerController = component.GetComponent<CampusTestPlayerController>();
            if (playerController == null)
            {
                playerController = component.GetComponentInParent<CampusTestPlayerController>();
            }

            if (playerController != null)
            {
                floorIndex = Mathf.Max(1, playerController.FloorIndex);
                return true;
            }

            CampusFloorRoot floor = component.GetComponentInParent<CampusFloorRoot>();
            if (floor != null)
            {
                floorIndex = Mathf.Max(1, floor.FloorIndex);
                return true;
            }

            return false;
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

            if (excludeSunLight && IsDayNightSunLight(light, null))
            {
                return false;
            }

            return true;
        }

        private static bool IsDayNightSunLight(Light2D light, CampusDayNightController dayNight)
        {
            if (light == null)
            {
                return false;
            }

            if (CampusObjectNames.MatchesAny(light.gameObject.name, CampusObjectNames.SunLight2D, CampusObjectNames.LegacySunLight2D))
            {
                return true;
            }

            if (dayNight == null)
            {
                dayNight = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }

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
