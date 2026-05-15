using UnityEngine;

namespace NtingCampusMapEditor
{
    public static class NtingSunShadowTimeSettings
    {
        // 24 分钟一昼夜时，过渡不能拖太长，否则玩家会长时间泡在半死不活的灰蓝里。
        public const float DawnStartHour = 5f;
        public const float DayStartHour = 6f;
        public const float DayEndHour = 17f;
        public const float DuskEndHour = 19f;

        // 夜晚靠环境光和局部灯塑形，不靠月影满地乱拖。
        public const float NightShadowLengthFactor = 0.2f;
        public const float NightShadowOpacityFactor = 0.2f;

        // 清晨/黄昏保留长影方向感，但不允许变成大面积黑块。
        public const float HorizonShadowLengthFactor = 0.4f;
        public const float HorizonShadowOpacityFactor = 0.3f;

        // 正午影子短、浅、干净，主要负责贴地感。
        public const float NoonShadowLengthFactor = 0.2f;
        public const float NoonShadowOpacityFactor = 1f;

        // 这是阴影颜色乘子，不是日光颜色。
        // 目标：早晚略紫冷，白天干净，夜晚偏蓝，但全程不脏。
        public static readonly NtingSunShadowColorKey[] DefaultShadowColorKeys =
        {
            new NtingSunShadowColorKey(0f, HexColor("#95A5EA")),
            new NtingSunShadowColorKey(5f, HexColor("#95A5EA")),
            new NtingSunShadowColorKey(6f, HexColor("#BAAECF")),
            new NtingSunShadowColorKey(9f, HexColor("#D8DADB")),
            new NtingSunShadowColorKey(12f, HexColor("#D8D8D8")),
            new NtingSunShadowColorKey(15f, HexColor("#DAD8E5")),
            new NtingSunShadowColorKey(17f, HexColor("#BCA4BA")),
            new NtingSunShadowColorKey(19f, HexColor("#95A5EA")),
            new NtingSunShadowColorKey(0f, HexColor("#95A5EA"))
        };

        public static Color HexColor(string htmlColor)
        {
            return ColorUtility.TryParseHtmlString(htmlColor, out Color color) ? color : Color.white;
        }
    }

    [System.Serializable]
    public struct NtingSunShadowColorKey
    {
        [Range(0f, 24f)] public float Hour;
        public Color Color;

        public NtingSunShadowColorKey(float hour, Color color)
        {
            Hour = hour;
            Color = color;
        }
    }
}
