using UnityEngine;

namespace NtingCampusMapEditor
{
    public static class NtingShadowSystemSettings
    {
        // PC Clean Cinematic：质量继续拉满，但点光源阴影不再拉成脏风扇。
        // 核心取向：日光负责长方向影；点光源负责局部接触感、轻微遮挡和空间层次。
        public const bool SystemEnabled = true;

        // 本系统统一接管点光源代理阴影，避免和 Unity 2D ShadowCaster 重复叠黑。
        public const bool DisableUnityLight2DShadows = true;

        public const bool EnablePointShadows = true;
        public const bool EnableSunShadows = true;
        public const bool ExcludeSunLightFromPointShadows = true;
        public const bool AutoRegisterPlacedObjects = true;

        // 高质量扫描：允许大量灯参与候选排序。
        // 画质高不等于每个物体吃一堆灯影；后者只会把桌椅变成煤矿出土文物。
        public const int MaxPointLights = 48;

        // 每个物体只保留最有效的少数点光阴影。
        // 点光源在俯视角室内主要提供局部层次，不应该生成一把扇子。
        public const int MaxPointLightShadowsPerCaster = 4;

        // 轮廓采样继续拉高，保证边缘顺滑。质量预设照样往上拉，不拿采样开刀。
        public const int ObjectShadowSampleCount = 128;

        // 点光源阴影必须短、薄、干净。
        // 长影交给太阳；室内点光拖长就是脏，不是电影感。
        public const float PointShadowMaxLengthWorld = 0.62f;
        public const float PointShadowAlpha = 0.4f;
        public const float PointShadowIntensityScale = 0.4f;

        // 点光对已有阴影的填充要更积极，避免桌椅附近堆出黑块。
        public const float PointLightShadowFillStrength = 1.0f;
        public const float PointLightShadowFillIntensityScale = 4f;
        public const float PointLightFillRangeExponent = 0.05f;
        public const float PointLightFillContributionBoost = 1f;

        // 局部灯可以明显削弱日光影，但保留日光方向。
        public const float MaxSunShadowPointLightFillStrength = 1f;

        // 点光源色相只轻微影响阴影，否则暖灯 + 蓝地面会混成脏灰。
        public const float PointLightShadowColorInfluence = 1f;

        // 日光/月光阴影负责主方向感。这里保留较强质量表现，但避免极端长黑块。
        public const float SunShadowMaxLengthWorld = 1.5f;
        public const float SunShadowAlpha = 0.4f;

        public const bool ScaleSunShadowLengthByDayNight = true;
        public const bool ScaleSunShadowAlphaByDayNight = true;

        // 必须高于时间曲线里的最大透明度因子，否则黄昏会被归一化到满黑。
        public const float MaxDayNightShadowOpacityFactor = 0.42f;

        // 更浅一点的冷蓝灰阴影基色。点光阴影太黑时，所有细节都会死在地板上。
        public static readonly Color BaseShadowColor = HexColor("#25324A");

        // PC 端高响应。这里继续保留高刷新，不搞“低配友好”的借口。
        public const float ScanInterval = 0.25f;
        public const float MinScanInterval = 0.1f;

        public const int ShadowSortingOrderOffset = 2;

        public static Color HexColor(string htmlColor)
        {
            return ColorUtility.TryParseHtmlString(htmlColor, out Color color) ? color : Color.white;
        }
    }
}
