using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public sealed class CampusDayNightController : MonoBehaviour
    {
        private const float HoursPerDay = 24f;
        private const float DefaultRealSecondsPerGameDay = 1440f;
        private const float DefaultOrbitRadius = 42f;
        private const float DefaultLightZ = 0.5f;
        private const string ControllerName = "Campus Day Night Controller";

        [SerializeField] private CampusMapRoot mapRoot;
        [SerializeField] private Light2D sunLight;
        [SerializeField] private Light2D globalLight;
        [SerializeField] private float realSecondsPerGameDay = DefaultRealSecondsPerGameDay;
        [SerializeField] private float orbitRadius = DefaultOrbitRadius;
        [SerializeField] private float lightZ = DefaultLightZ;
        [SerializeField, Range(0f, 24f)] private float startGameHour = 12f;
        [SerializeField, Range(0f, 2f)] private float globalLightMinIntensity = 0.3f;
        [SerializeField, Range(0f, 2f)] private float globalLightMaxIntensity = 0.6f;
        [SerializeField] private NtingSunShadowColorKey[] shadowColorKeys = CloneDefaultShadowColorKeys();

        private bool hasInitializedTime;
        private Vector2 cachedCampusCenter;
        private NtingShadowSceneSettings shadowSceneSettings;

        public float SunHeight01 { get; private set; }
        public float ShadowLengthFactor { get; private set; }
        public float ShadowOpacityFactor { get; private set; }
        public Color ShadowColorMultiplier { get; private set; } = Color.white;
        public Vector2 ShadowDirection { get; private set; } = Vector2.down;
        public float GameHour { get; private set; }
        public float Day01 { get; private set; }
        public Light2D SunLight => sunLight;
        public Light2D GlobalLight => globalLight;
        public float RealSecondsPerGameDay
        {
            get => realSecondsPerGameDay;
            set => realSecondsPerGameDay = Mathf.Clamp(value, 1f, 86400f);
        }

        public float DaySpeedMultiplier
        {
            get => DefaultRealSecondsPerGameDay / Mathf.Max(1f, realSecondsPerGameDay);
            set
            {
                float speed = Mathf.Clamp(value, 0.05f, 200f);
                realSecondsPerGameDay = DefaultRealSecondsPerGameDay / speed;
            }
        }

        public float RealMinutesPerGameDay => RealSecondsPerGameDay / 60f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeController()
        {
            EnsureSceneController();
        }

        public static CampusDayNightController EnsureSceneController(CampusMapRoot preferredRoot = null)
        {
            CampusDayNightController controller = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                GameObject controllerObject = new GameObject(ControllerName);
                controller = controllerObject.AddComponent<CampusDayNightController>();
            }

            if (preferredRoot != null)
            {
                controller.mapRoot = preferredRoot;
            }

            controller.ResolveSceneReferences();
            controller.RefreshCampusCenter();
            controller.ApplyCurrentTimeState();
            return controller;
        }

        public void RefreshCampusCenter()
        {
            ResolveSceneReferences();
            cachedCampusCenter = CalculateCampusCenter(mapRoot);
        }

        private void Awake()
        {
            ResolveSceneReferences();
            InitializeTimeIfNeeded();
            RefreshCampusCenter();
            ApplyCurrentTimeState();
        }

        private void OnEnable()
        {
            ResolveSceneReferences();
            InitializeTimeIfNeeded();
            RefreshCampusCenter();
            ApplyCurrentTimeState();
        }

        private void Update()
        {
            InitializeTimeIfNeeded();
            if (Application.isPlaying)
            {
                float secondsPerDay = Mathf.Max(1f, realSecondsPerGameDay);
                Day01 = Mathf.Repeat(Day01 + Time.deltaTime / secondsPerDay, 1f);
            }
            else
            {
                Day01 = Mathf.Repeat(startGameHour / HoursPerDay, 1f);
            }

            ApplyCurrentTimeState();
        }

        private void InitializeTimeIfNeeded()
        {
            if (hasInitializedTime)
            {
                return;
            }

            Day01 = Mathf.Repeat(startGameHour / HoursPerDay, 1f);
            hasInitializedTime = true;
        }

        private void ResolveSceneReferences()
        {
            if (mapRoot == null)
            {
                mapRoot = Object.FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            }

            if (sunLight == null)
            {
                sunLight = FindSunLight2D();
            }

            if (sunLight == null)
            {
                GameObject lightObject = new GameObject(CampusObjectNames.SunLight2D);
                sunLight = lightObject.AddComponent<Light2D>();
                sunLight.lightType = Light2D.LightType.Point;
                sunLight.blendStyleIndex = 0;
            }

            if (globalLight == null || globalLight.lightType != Light2D.LightType.Global)
            {
                globalLight = FindGlobalLight2D();
            }

            if (globalLight == null)
            {
                GameObject lightObject = new GameObject(CampusObjectNames.GlobalLight2D);
                globalLight = lightObject.AddComponent<Light2D>();
                globalLight.blendStyleIndex = 0;
            }

            globalLight.gameObject.name = CampusObjectNames.GlobalLight2D;
            globalLight.lightType = Light2D.LightType.Global;
            CampusDynamicShadowUtility.ConfigureLightShadows(globalLight, false, 0.75f, 0.3f, 0.5f);

            if (shadowSceneSettings == null)
            {
                shadowSceneSettings = Object.FindFirstObjectByType<NtingShadowSceneSettings>(FindObjectsInactive.Include);
            }
        }

        private void ApplyCurrentTimeState()
        {
            ResolveSceneReferences();
            GameHour = Mathf.Repeat(Day01 * HoursPerDay, HoursPerDay);
            UpdateSunPosition();
            UpdateLightIntensityAndColor();
            UpdateShadowParameters();
        }

        private void UpdateSunPosition()
        {
            if (sunLight == null)
            {
                return;
            }

            float theta = Day01 * Mathf.PI * 2f;
            Vector3 offset = new Vector3(
                -Mathf.Sin(theta) * orbitRadius,
                -Mathf.Cos(theta) * orbitRadius,
                0f);
            sunLight.transform.position = new Vector3(cachedCampusCenter.x, cachedCampusCenter.y, lightZ) + offset;
        }

        private void UpdateLightIntensityAndColor()
        {
            EvaluateLightKeyframes(GameHour, out float sunIntensity, out Color lightColor);
            UpdateSunLight(sunIntensity, lightColor);
            UpdateGlobalLight(sunIntensity, lightColor);
        }

        private void UpdateSunLight(float intensity, Color color)
        {
            if (sunLight == null)
            {
                return;
            }

            sunLight.intensity = intensity;
            sunLight.color = color;
            sunLight.lightType = Light2D.LightType.Point;
            sunLight.pointLightInnerAngle = 360f;
            sunLight.pointLightOuterAngle = 360f;
            sunLight.pointLightInnerRadius = Mathf.Max(sunLight.pointLightInnerRadius, orbitRadius * 1.15f);
            sunLight.pointLightOuterRadius = Mathf.Max(sunLight.pointLightOuterRadius, orbitRadius * 1.5f);
        }

        private void UpdateGlobalLight(float sunIntensity, Color color)
        {
            if (globalLight == null)
            {
                return;
            }

            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = EvaluateGlobalLightIntensity(sunIntensity);
            globalLight.color = color;
            globalLight.blendStyleIndex = 0;
            CampusDynamicShadowUtility.ConfigureLightShadows(globalLight, false, 0.75f, 0.3f, 0.5f);
        }

        private void UpdateShadowParameters()
        {
            NtingShadowSceneSettings settings = ResolveShadowSceneSettings();
            float dawnStartHour = settings != null ? settings.dawnStartHour : NtingSunShadowTimeSettings.DawnStartHour;
            float dayStartHour = settings != null ? settings.dayStartHour : NtingSunShadowTimeSettings.DayStartHour;
            float dayEndHour = settings != null ? settings.dayEndHour : NtingSunShadowTimeSettings.DayEndHour;
            float duskEndHour = settings != null ? settings.duskEndHour : NtingSunShadowTimeSettings.DuskEndHour;
            float nightLengthFactor = settings != null ? settings.nightShadowLengthFactor : NtingSunShadowTimeSettings.NightShadowLengthFactor;
            float nightOpacityFactor = settings != null ? settings.nightShadowOpacityFactor : NtingSunShadowTimeSettings.NightShadowOpacityFactor;
            float horizonLengthFactor = settings != null ? settings.horizonShadowLengthFactor : NtingSunShadowTimeSettings.HorizonShadowLengthFactor;
            float horizonOpacityFactor = settings != null ? settings.horizonShadowOpacityFactor : NtingSunShadowTimeSettings.HorizonShadowOpacityFactor;
            float noonLengthFactor = settings != null ? settings.noonShadowLengthFactor : NtingSunShadowTimeSettings.NoonShadowLengthFactor;
            float noonOpacityFactor = settings != null ? settings.noonShadowOpacityFactor : NtingSunShadowTimeSettings.NoonShadowOpacityFactor;
            NtingSunShadowColorKey[] colorKeys = settings != null ? settings.shadowColorKeys : shadowColorKeys;

            float dayBlend = ResolveDayShadowBlend(GameHour, dawnStartHour, dayStartHour, dayEndHour, duskEndHour);
            float dayProgress = Mathf.InverseLerp(
                dayStartHour,
                dayEndHour,
                Mathf.Clamp(GameHour, dayStartHour, dayEndHour));
            float daylightSunHeight = Mathf.Max(0f, Mathf.Sin(dayProgress * Mathf.PI));
            SunHeight01 = Mathf.Clamp01(daylightSunHeight * dayBlend);

            float dayShadowLength = Mathf.Lerp(horizonLengthFactor, noonLengthFactor, Mathf.Pow(SunHeight01, 0.7f));
            float dayShadowOpacity = Mathf.Lerp(horizonOpacityFactor, noonOpacityFactor, SunHeight01);
            ShadowLengthFactor = Mathf.Lerp(nightLengthFactor, dayShadowLength, dayBlend);
            ShadowOpacityFactor = Mathf.Lerp(nightOpacityFactor, dayShadowOpacity, dayBlend);
            ShadowColorMultiplier = EvaluateShadowColorKeys(GameHour, colorKeys);

            Vector2 lightPos = sunLight != null ? (Vector2)sunLight.transform.position : cachedCampusCenter - Vector2.down;
            Vector2 direction = cachedCampusCenter - lightPos;
            ShadowDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        }

        private NtingShadowSceneSettings ResolveShadowSceneSettings()
        {
            if (shadowSceneSettings == null)
            {
                shadowSceneSettings = Object.FindFirstObjectByType<NtingShadowSceneSettings>(FindObjectsInactive.Include);
            }

            return shadowSceneSettings;
        }

        private static Color EvaluateShadowColorKeys(float hour, NtingSunShadowColorKey[] colorKeys)
        {
            if (colorKeys == null || colorKeys.Length == 0)
            {
                return Color.white;
            }

            hour = Mathf.Repeat(hour, HoursPerDay);
            int previousIndex = -1;
            int nextIndex = -1;
            float previousDistance = float.MaxValue;
            float nextDistance = float.MaxValue;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                float keyHour = Mathf.Repeat(colorKeys[i].Hour, HoursPerDay);
                float backwardDistance = Mathf.Repeat(hour - keyHour, HoursPerDay);
                float forwardDistance = Mathf.Repeat(keyHour - hour, HoursPerDay);
                if (backwardDistance < previousDistance)
                {
                    previousDistance = backwardDistance;
                    previousIndex = i;
                }

                if (forwardDistance < nextDistance)
                {
                    nextDistance = forwardDistance;
                    nextIndex = i;
                }
            }

            if (previousIndex < 0 || nextIndex < 0)
            {
                return Color.white;
            }

            Color previous = colorKeys[previousIndex].Color;
            Color next = colorKeys[nextIndex].Color;
            previous.a = 1f;
            next.a = 1f;
            float span = previousDistance + nextDistance;
            if (span <= 0.0001f)
            {
                return previous;
            }

            float t = Mathf.SmoothStep(0f, 1f, previousDistance / span);
            Color color = Color.Lerp(previous, next, t);
            color.a = 1f;
            return color;
        }

        private static float ResolveDayShadowBlend(float hour, float dawnStartHour, float dayStartHour, float dayEndHour, float duskEndHour)
        {
            if (hour >= dayStartHour && hour <= dayEndHour)
            {
                return 1f;
            }

            if (hour >= dawnStartHour && hour < dayStartHour)
            {
                return SmootherStep01(Mathf.InverseLerp(dawnStartHour, dayStartHour, hour));
            }

            if (hour > dayEndHour && hour <= duskEndHour)
            {
                return 1f - SmootherStep01(Mathf.InverseLerp(dayEndHour, duskEndHour, hour));
            }

            return 0f;
        }

        private static float SmootherStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static Light2D FindSunLight2D()
        {
            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light != null && CampusObjectNames.MatchesAny(light.gameObject.name, CampusObjectNames.SunLight2D, CampusObjectNames.LegacySunLight2D))
                {
                    return light;
                }
            }

            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light != null && light.lightType != Light2D.LightType.Global)
                {
                    return light;
                }
            }

            return null;
        }

        private static Light2D FindGlobalLight2D()
        {
            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light != null && CampusObjectNames.MatchesAny(light.gameObject.name, CampusObjectNames.GlobalLight2D, CampusObjectNames.LegacyGlobalLight2D))
                {
                    return light;
                }
            }

            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light != null && light.lightType == Light2D.LightType.Global)
                {
                    return light;
                }
            }

            return null;
        }

        private static Vector2 CalculateCampusCenter(CampusMapRoot root)
        {
            if (root == null)
            {
                return Vector2.zero;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.GetComponent<Light2D>() != null || renderer.GetComponent<CampusProjectedWallShadowRenderer>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? (Vector2)bounds.center : (Vector2)root.transform.position;
        }

        private static void EvaluateLightKeyframes(float hour, out float intensity, out Color color)
        {
            hour = Mathf.Clamp(hour, 0f, 24f);
            for (int i = 0; i < LightKeys.Length - 1; i++)
            {
                LightKey a = LightKeys[i];
                LightKey b = LightKeys[i + 1];
                if (hour <= b.Hour)
                {
                    float t = Mathf.InverseLerp(a.Hour, b.Hour, hour);
                    t = Mathf.SmoothStep(0f, 1f, t);
                    intensity = Mathf.Lerp(a.Intensity, b.Intensity, t);
                    color = Color.Lerp(a.Color, b.Color, t);
                    return;
                }
            }

            LightKey last = LightKeys[LightKeys.Length - 1];
            intensity = last.Intensity;
            color = last.Color;
        }

        private float EvaluateGlobalLightIntensity(float sunIntensity)
        {
            float minIntensity = Mathf.Min(globalLightMinIntensity, globalLightMaxIntensity);
            float maxIntensity = Mathf.Max(globalLightMinIntensity, globalLightMaxIntensity);
            float normalizedSunIntensity = NormalizeLightKeyIntensity(sunIntensity);
            return Mathf.Lerp(minIntensity, maxIntensity, normalizedSunIntensity);
        }

        private static float NormalizeLightKeyIntensity(float intensity)
        {
            float minIntensity = LightKeys[0].Intensity;
            float maxIntensity = LightKeys[0].Intensity;
            for (int i = 1; i < LightKeys.Length; i++)
            {
                minIntensity = Mathf.Min(minIntensity, LightKeys[i].Intensity);
                maxIntensity = Mathf.Max(maxIntensity, LightKeys[i].Intensity);
            }

            return Mathf.InverseLerp(minIntensity, maxIntensity, intensity);
        }

        private readonly struct LightKey
        {
            public readonly float Hour;
            public readonly float Intensity;
            public readonly Color Color;

            public LightKey(float hour, float intensity, string htmlColor)
            {
                Hour = hour;
                Intensity = intensity;
                ColorUtility.TryParseHtmlString(htmlColor, out Color parsedColor);
                Color = parsedColor;
            }
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

        private static readonly LightKey[] LightKeys =
        {
            new LightKey(0f, 0.28f, "#18285F"),
            new LightKey(1.5f, 0.38f, "#223674"),
            new LightKey(3f, 0.65f, "#354C99"),
            new LightKey(4.5f, 1.25f, "#6D78C8"),
            new LightKey(6f, 3.0f, "#FF8A45"),
            new LightKey(7.5f, 5.0f, "#FFB066"),
            new LightKey(9f, 7.5f, "#FFD08A"),
            new LightKey(10.5f, 10.5f, "#FFE8BA"),
            new LightKey(12f, 13.0f, "#FFF4DD"),
            new LightKey(13.5f, 11.5f, "#FFE2AA"),
            new LightKey(15f, 8.5f, "#FFC878"),
            new LightKey(16.5f, 5.2f, "#FF9A55"),
            new LightKey(18f, 2.5f, "#FF6338"),
            new LightKey(19.5f, 1.2f, "#657CFF"),
            new LightKey(21f, 0.65f, "#344FAE"),
            new LightKey(22.5f, 0.42f, "#233880"),
            new LightKey(24f, 0.28f, "#18285F")
        };
    }
}
