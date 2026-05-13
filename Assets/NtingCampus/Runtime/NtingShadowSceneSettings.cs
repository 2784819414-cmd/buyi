using UnityEngine;

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NtingShadowSceneSettings : MonoBehaviour
    {
        [Header("System")]
        [Tooltip("总开关。关闭后会隐藏本系统生成的日光、月光和点光源代理阴影。")]
        public bool systemEnabled = NtingShadowSystemSettings.SystemEnabled;
        [Tooltip("关闭 Unity Light2D 自带的非 Global 阴影，避免和自定义点光源阴影重复叠黑。")]
        public bool disableUnityLight2DShadows = NtingShadowSystemSettings.DisableUnityLight2DShadows;
        [Tooltip("启用物体和墙体的普通点光源阴影。")]
        public bool enablePointShadows = NtingShadowSystemSettings.EnablePointShadows;
        [Tooltip("启用物体和墙体的日光/月光方向阴影。")]
        public bool enableSunShadows = NtingShadowSystemSettings.EnableSunShadows;
        [Tooltip("点光源阴影计算时排除日光 Light2D，避免日光被当成普通点光源重复参与计算。")]
        public bool excludeSunLightFromPointShadows = NtingShadowSystemSettings.ExcludeSunLightFromPointShadows;
        [Tooltip("自动给新放置的物体补充 NtingShadowCasterProfile，使新增物体能参与自定义阴影。")]
        public bool autoRegisterPlacedObjects = NtingShadowSystemSettings.AutoRegisterPlacedObjects;

        [Header("Point Shadows")]
        [Tooltip("每帧最多参与阴影计算的点光源数量。数值越大，多灯环境越完整，但成本更高。")]
        [Min(1)] public int maxPointLights = NtingShadowSystemSettings.MaxPointLights;
        [Tooltip("单个物体最多显示多少个点光源阴影。数值越大越容易产生多层叠影。")]
        [Min(1)] public int maxPointLightShadowsPerCaster = NtingShadowSystemSettings.MaxPointLightShadowsPerCaster;
        [Tooltip("物体 alpha 轮廓阴影采样次数。越高边缘越顺滑，性能成本越高。")]
        [Range(8, 256)] public int objectShadowSampleCount = NtingShadowSystemSettings.ObjectShadowSampleCount;
        [Tooltip("普通点光源阴影的最大长度，单位是世界单位。")]
        [Min(0f)] public float pointShadowMaxLengthWorld = NtingShadowSystemSettings.PointShadowMaxLengthWorld;
        [Tooltip("普通点光源阴影基础透明度。越高阴影越深。")]
        [Range(0f, 1f)] public float pointShadowAlpha = NtingShadowSystemSettings.PointShadowAlpha;
        [Tooltip("点光源强度对点光阴影深浅的影响倍率。越高，强灯产生的阴影越明显。")]
        [Range(0.01f, 4f)] public float pointShadowIntensityScale = NtingShadowSystemSettings.PointShadowIntensityScale;

        [Header("Point Light Fill")]
        [Tooltip("点光源照亮已有阴影的总体强度。越高，点光越能把日光/月光阴影和其它阴影提亮。")]
        [Range(0f, 1f)] public float pointLightShadowFillStrength = NtingShadowSystemSettings.PointLightShadowFillStrength;
        [Tooltip("点光源照亮阴影时对 Light2D 强度的响应倍率。越高，强灯越容易洗亮阴影。")]
        [Range(0.01f, 8f)] public float pointLightShadowFillIntensityScale = NtingShadowSystemSettings.PointLightShadowFillIntensityScale;
        [Tooltip("点光照亮阴影的距离衰减曲线。数值越小，灯光边缘也更容易照亮阴影；数值越大，照亮更集中在灯附近。")]
        [Range(0.01f, 2f)] public float pointLightFillRangeExponent = NtingShadowSystemSettings.PointLightFillRangeExponent;
        [Tooltip("点光照亮阴影的额外增强倍率。用于整体加强或减弱点光对阴影的削弱能力。")]
        [Range(0f, 4f)] public float pointLightFillContributionBoost = NtingShadowSystemSettings.PointLightFillContributionBoost;
        [Tooltip("日光/月光阴影被点光照亮时的上限，防止局部灯把主方向阴影完全抹掉。")]
        [Range(0f, 1f)] public float maxSunShadowPointLightFillStrength = NtingShadowSystemSettings.MaxSunShadowPointLightFillStrength;
        [Tooltip("点光源颜色影响点光阴影色相的比例。0 表示只用基础阴影明暗，1 表示完全跟随 Light2D 颜色。")]
        [Range(0f, 1f)] public float pointLightShadowColorInfluence = NtingShadowSystemSettings.PointLightShadowColorInfluence;

        [Header("Sun And Moon Shadows")]
        [Tooltip("日光/月光阴影的基础最大长度，单位是世界单位；最终还会乘以时间曲线的长度系数。")]
        [Min(0f)] public float sunShadowMaxLengthWorld = NtingShadowSystemSettings.SunShadowMaxLengthWorld;
        [Tooltip("日光/月光阴影的基础透明度；最终还会乘以时间曲线的深度系数。")]
        [Range(0f, 1f)] public float sunShadowAlpha = NtingShadowSystemSettings.SunShadowAlpha;
        [Tooltip("开启后，日光/月光阴影长度会按当前时段平滑变化。")]
        public bool scaleSunShadowLengthByDayNight = NtingShadowSystemSettings.ScaleSunShadowLengthByDayNight;
        [Tooltip("开启后，日光/月光阴影深度会按当前时段平滑变化。")]
        public bool scaleSunShadowAlphaByDayNight = NtingShadowSystemSettings.ScaleSunShadowAlphaByDayNight;
        [Tooltip("时间曲线深度归一化上限。应大于等于 NtingSunShadowTimeSettings 里的最大深度系数。")]
        [Min(0.001f)] public float maxDayNightShadowOpacityFactor = NtingShadowSystemSettings.MaxDayNightShadowOpacityFactor;

        [Header("Sun Time Curve")]
        [Tooltip("清晨过渡开始时间，小时制。比如 5.1 表示约 05:06，从这一刻开始从夜间阴影过渡到白天阴影。")]
        [Range(0f, 24f)] public float dawnStartHour = NtingSunShadowTimeSettings.DawnStartHour;
        [Tooltip("白天正式开始时间，小时制。到这一刻后完全使用白天阴影参数。")]
        [Range(0f, 24f)] public float dayStartHour = NtingSunShadowTimeSettings.DayStartHour;
        [Tooltip("黄昏过渡开始时间，小时制。从这一刻开始从白天阴影过渡到夜间阴影。")]
        [Range(0f, 24f)] public float dayEndHour = NtingSunShadowTimeSettings.DayEndHour;
        [Tooltip("夜间正式开始时间，小时制。到这一刻后完全使用夜间/月光阴影参数。")]
        [Range(0f, 24f)] public float duskEndHour = NtingSunShadowTimeSettings.DuskEndHour;
        [Tooltip("夜间/月光阴影长度系数。最终长度 = Sun Shadow Max Length World * 该系数。")]
        [Min(0f)] public float nightShadowLengthFactor = NtingSunShadowTimeSettings.NightShadowLengthFactor;
        [Tooltip("夜间/月光阴影深度系数。最终会按 Max Day Night Shadow Opacity Factor 归一化。")]
        [Min(0f)] public float nightShadowOpacityFactor = NtingSunShadowTimeSettings.NightShadowOpacityFactor;
        [Tooltip("清晨/黄昏太阳接近地平线时的阴影长度系数。")]
        [Min(0f)] public float horizonShadowLengthFactor = NtingSunShadowTimeSettings.HorizonShadowLengthFactor;
        [Tooltip("清晨/黄昏太阳接近地平线时的阴影深度系数。")]
        [Min(0f)] public float horizonShadowOpacityFactor = NtingSunShadowTimeSettings.HorizonShadowOpacityFactor;
        [Tooltip("正午太阳最高时的阴影长度系数。")]
        [Min(0f)] public float noonShadowLengthFactor = NtingSunShadowTimeSettings.NoonShadowLengthFactor;
        [Tooltip("正午太阳最高时的阴影深度系数。")]
        [Min(0f)] public float noonShadowOpacityFactor = NtingSunShadowTimeSettings.NoonShadowOpacityFactor;
        [Tooltip("日光/月光阴影颜色乘子节点。相邻节点之间会平滑渐变；颜色不是最终颜色，而是乘在基础阴影色上的系数。")]
        public NtingSunShadowColorKey[] shadowColorKeys = CloneDefaultShadowColorKeys();

        [Header("Color")]
        [Tooltip("基础阴影颜色。日光/月光会直接使用并乘以时间颜色曲线；点光阴影主要取它的明暗，色相由 Light2D 颜色决定。")]
        public Color baseShadowColor = NtingShadowSystemSettings.BaseShadowColor;

        [Header("Update And Sorting")]
        [Tooltip("扫描场景物体和灯光的间隔，单位秒。越小响应越快，但全量扫描更频繁。")]
        [Min(0.001f)] public float scanInterval = NtingShadowSystemSettings.ScanInterval;
        [Tooltip("扫描间隔的最小保护值，防止误填过低导致频繁全量扫描。")]
        [Min(0.001f)] public float minScanInterval = NtingShadowSystemSettings.MinScanInterval;
        [Tooltip("阴影在 GroundShadow 排序层上的排序偏移，用来控制阴影和地面/物体的前后关系。")]
        public int shadowSortingOrderOffset = NtingShadowSystemSettings.ShadowSortingOrderOffset;

        public void ApplyTo(NtingCustomShadowSystem system)
        {
            if (system == null)
            {
                return;
            }

            ClampValues();
            system.systemEnabled = systemEnabled;
            system.disableUnityLight2DShadows = disableUnityLight2DShadows;
            system.enablePointObjectShadows = enablePointShadows;
            system.enablePointWallShadows = enablePointShadows;
            system.enableSunObjectShadows = enableSunShadows;
            system.excludeSunLightFromPointShadows = excludeSunLightFromPointShadows;
            system.autoRegisterPlacedObjects = autoRegisterPlacedObjects;
            system.maxPointLights = maxPointLights;
            system.maxPointLightShadowsPerCaster = maxPointLightShadowsPerCaster;
            system.objectShadowSampleCount = objectShadowSampleCount;
            system.pointShadowMaxLengthWorld = pointShadowMaxLengthWorld;
            system.pointShadowAlpha = pointShadowAlpha;
            system.pointShadowIntensityScale = pointShadowIntensityScale;
            system.wallPointShadowMaxLengthWorld = pointShadowMaxLengthWorld;
            system.wallPointShadowAlpha = pointShadowAlpha;
            system.wallPointShadowIntensityScale = pointShadowIntensityScale;
            system.pointLightShadowFillStrength = pointLightShadowFillStrength;
            system.pointLightShadowFillIntensityScale = pointLightShadowFillIntensityScale;
            system.pointLightFillRangeExponent = pointLightFillRangeExponent;
            system.pointLightFillContributionBoost = pointLightFillContributionBoost;
            system.maxSunShadowPointLightFillStrength = maxSunShadowPointLightFillStrength;
            system.pointLightShadowColorInfluence = pointLightShadowColorInfluence;
            system.sunObjectShadowMaxLengthWorld = sunShadowMaxLengthWorld;
            system.scaleSunObjectShadowLengthByDayNight = scaleSunShadowLengthByDayNight;
            system.scaleSunShadowAlphaByDayNight = scaleSunShadowAlphaByDayNight;
            system.sunObjectShadowAlpha = sunShadowAlpha;
            system.maxDayNightShadowOpacityFactor = maxDayNightShadowOpacityFactor;
            system.shadowColor = baseShadowColor;
            system.scanInterval = scanInterval;
            system.minScanInterval = minScanInterval;
            system.shadowSortingOrderOffset = shadowSortingOrderOffset;
        }

        [ContextMenu("Reset To Declared Defaults")]
        public void ResetToDeclaredDefaults()
        {
            systemEnabled = NtingShadowSystemSettings.SystemEnabled;
            disableUnityLight2DShadows = NtingShadowSystemSettings.DisableUnityLight2DShadows;
            enablePointShadows = NtingShadowSystemSettings.EnablePointShadows;
            enableSunShadows = NtingShadowSystemSettings.EnableSunShadows;
            excludeSunLightFromPointShadows = NtingShadowSystemSettings.ExcludeSunLightFromPointShadows;
            autoRegisterPlacedObjects = NtingShadowSystemSettings.AutoRegisterPlacedObjects;
            maxPointLights = NtingShadowSystemSettings.MaxPointLights;
            maxPointLightShadowsPerCaster = NtingShadowSystemSettings.MaxPointLightShadowsPerCaster;
            objectShadowSampleCount = NtingShadowSystemSettings.ObjectShadowSampleCount;
            pointShadowMaxLengthWorld = NtingShadowSystemSettings.PointShadowMaxLengthWorld;
            pointShadowAlpha = NtingShadowSystemSettings.PointShadowAlpha;
            pointShadowIntensityScale = NtingShadowSystemSettings.PointShadowIntensityScale;
            pointLightShadowFillStrength = NtingShadowSystemSettings.PointLightShadowFillStrength;
            pointLightShadowFillIntensityScale = NtingShadowSystemSettings.PointLightShadowFillIntensityScale;
            pointLightFillRangeExponent = NtingShadowSystemSettings.PointLightFillRangeExponent;
            pointLightFillContributionBoost = NtingShadowSystemSettings.PointLightFillContributionBoost;
            maxSunShadowPointLightFillStrength = NtingShadowSystemSettings.MaxSunShadowPointLightFillStrength;
            pointLightShadowColorInfluence = NtingShadowSystemSettings.PointLightShadowColorInfluence;
            sunShadowMaxLengthWorld = NtingShadowSystemSettings.SunShadowMaxLengthWorld;
            sunShadowAlpha = NtingShadowSystemSettings.SunShadowAlpha;
            scaleSunShadowLengthByDayNight = NtingShadowSystemSettings.ScaleSunShadowLengthByDayNight;
            scaleSunShadowAlphaByDayNight = NtingShadowSystemSettings.ScaleSunShadowAlphaByDayNight;
            maxDayNightShadowOpacityFactor = NtingShadowSystemSettings.MaxDayNightShadowOpacityFactor;
            dawnStartHour = NtingSunShadowTimeSettings.DawnStartHour;
            dayStartHour = NtingSunShadowTimeSettings.DayStartHour;
            dayEndHour = NtingSunShadowTimeSettings.DayEndHour;
            duskEndHour = NtingSunShadowTimeSettings.DuskEndHour;
            nightShadowLengthFactor = NtingSunShadowTimeSettings.NightShadowLengthFactor;
            nightShadowOpacityFactor = NtingSunShadowTimeSettings.NightShadowOpacityFactor;
            horizonShadowLengthFactor = NtingSunShadowTimeSettings.HorizonShadowLengthFactor;
            horizonShadowOpacityFactor = NtingSunShadowTimeSettings.HorizonShadowOpacityFactor;
            noonShadowLengthFactor = NtingSunShadowTimeSettings.NoonShadowLengthFactor;
            noonShadowOpacityFactor = NtingSunShadowTimeSettings.NoonShadowOpacityFactor;
            shadowColorKeys = CloneDefaultShadowColorKeys();
            baseShadowColor = NtingShadowSystemSettings.BaseShadowColor;
            scanInterval = NtingShadowSystemSettings.ScanInterval;
            minScanInterval = NtingShadowSystemSettings.MinScanInterval;
            shadowSortingOrderOffset = NtingShadowSystemSettings.ShadowSortingOrderOffset;
            ClampValues();
        }

        private void Reset()
        {
            ResetToDeclaredDefaults();
        }

        private void OnValidate()
        {
            ClampValues();
            NtingCustomShadowSystem system = GetComponent<NtingCustomShadowSystem>();
            if (system == null)
            {
                system = FindFirstObjectByType<NtingCustomShadowSystem>(FindObjectsInactive.Include);
            }

            if (system != null)
            {
                system.ApplyRuntimeSettingChanges();
            }
        }

        private void ClampValues()
        {
            maxPointLights = Mathf.Max(1, maxPointLights);
            maxPointLightShadowsPerCaster = Mathf.Max(1, maxPointLightShadowsPerCaster);
            objectShadowSampleCount = Mathf.Clamp(objectShadowSampleCount, 8, 256);
            pointShadowMaxLengthWorld = Mathf.Max(0f, pointShadowMaxLengthWorld);
            pointShadowAlpha = Mathf.Clamp01(pointShadowAlpha);
            pointShadowIntensityScale = Mathf.Max(0.01f, pointShadowIntensityScale);
            pointLightShadowFillStrength = Mathf.Clamp01(pointLightShadowFillStrength);
            pointLightShadowFillIntensityScale = Mathf.Max(0.01f, pointLightShadowFillIntensityScale);
            pointLightFillRangeExponent = Mathf.Max(0.01f, pointLightFillRangeExponent);
            pointLightFillContributionBoost = Mathf.Max(0f, pointLightFillContributionBoost);
            maxSunShadowPointLightFillStrength = Mathf.Clamp01(maxSunShadowPointLightFillStrength);
            pointLightShadowColorInfluence = Mathf.Clamp01(pointLightShadowColorInfluence);
            sunShadowMaxLengthWorld = Mathf.Max(0f, sunShadowMaxLengthWorld);
            sunShadowAlpha = Mathf.Clamp01(sunShadowAlpha);
            maxDayNightShadowOpacityFactor = Mathf.Max(0.001f, maxDayNightShadowOpacityFactor);
            dawnStartHour = Mathf.Repeat(dawnStartHour, 24f);
            dayStartHour = Mathf.Repeat(dayStartHour, 24f);
            dayEndHour = Mathf.Repeat(dayEndHour, 24f);
            duskEndHour = Mathf.Repeat(duskEndHour, 24f);
            nightShadowLengthFactor = Mathf.Max(0f, nightShadowLengthFactor);
            nightShadowOpacityFactor = Mathf.Max(0f, nightShadowOpacityFactor);
            horizonShadowLengthFactor = Mathf.Max(0f, horizonShadowLengthFactor);
            horizonShadowOpacityFactor = Mathf.Max(0f, horizonShadowOpacityFactor);
            noonShadowLengthFactor = Mathf.Max(0f, noonShadowLengthFactor);
            noonShadowOpacityFactor = Mathf.Max(0f, noonShadowOpacityFactor);
            if (shadowColorKeys == null || shadowColorKeys.Length == 0)
            {
                shadowColorKeys = CloneDefaultShadowColorKeys();
            }

            for (int i = 0; i < shadowColorKeys.Length; i++)
            {
                shadowColorKeys[i].Hour = Mathf.Repeat(shadowColorKeys[i].Hour, 24f);
                shadowColorKeys[i].Color.a = 1f;
            }

            baseShadowColor.a = 1f;
            minScanInterval = Mathf.Max(0.001f, minScanInterval);
            scanInterval = Mathf.Max(minScanInterval, scanInterval);
        }

        private static NtingSunShadowColorKey[] CloneDefaultShadowColorKeys()
        {
            NtingSunShadowColorKey[] source = NtingSunShadowTimeSettings.DefaultShadowColorKeys;
            NtingSunShadowColorKey[] keys = new NtingSunShadowColorKey[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                keys[i] = source[i];
            }

            return keys;
        }
    }
}
