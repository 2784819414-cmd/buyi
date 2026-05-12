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
        [SerializeField] private float realSecondsPerGameDay = DefaultRealSecondsPerGameDay;
        [SerializeField] private float orbitRadius = DefaultOrbitRadius;
        [SerializeField] private float lightZ = DefaultLightZ;
        [SerializeField, Range(0f, 24f)] private float startGameHour = 12f;

        private bool hasInitializedTime;
        private Vector2 cachedCampusCenter;

        public float SunHeight01 { get; private set; }
        public float ShadowLengthFactor { get; private set; }
        public float ShadowOpacityFactor { get; private set; }
        public Vector2 ShadowDirection { get; private set; } = Vector2.down;
        public float GameHour { get; private set; }
        public float Day01 { get; private set; }
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

            NormalizePointLightRotation(sunLight);
        }

        private void ApplyCurrentTimeState()
        {
            ResolveSceneReferences();
            GameHour = Mathf.Repeat(Day01 * HoursPerDay, HoursPerDay);
            UpdateSunPosition();
            UpdateSunIntensityAndColor();
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

        private void UpdateSunIntensityAndColor()
        {
            if (sunLight == null)
            {
                return;
            }

            EvaluateLightKeyframes(GameHour, out float intensity, out Color color);
            sunLight.intensity = intensity;
            sunLight.color = color;
            sunLight.lightType = Light2D.LightType.Point;
            sunLight.pointLightInnerAngle = 360f;
            sunLight.pointLightOuterAngle = 360f;
            sunLight.pointLightInnerRadius = Mathf.Max(sunLight.pointLightInnerRadius, orbitRadius * 1.15f);
            sunLight.pointLightOuterRadius = Mathf.Max(sunLight.pointLightOuterRadius, orbitRadius * 1.5f);
            NormalizePointLightRotation(sunLight);
        }

        private void UpdateShadowParameters()
        {
            bool isDay = GameHour >= 6f && GameHour <= 18f;
            if (isDay)
            {
                float dayProgress = Mathf.InverseLerp(6f, 18f, GameHour);
                SunHeight01 = Mathf.Sin(dayProgress * Mathf.PI);
                ShadowLengthFactor = Mathf.Lerp(1.0f, 0.12f, Mathf.Pow(SunHeight01, 0.7f));
                ShadowOpacityFactor = Mathf.Lerp(0.30f, 0.12f, SunHeight01);
            }
            else
            {
                SunHeight01 = 0f;
                ShadowLengthFactor = 0.45f;
                ShadowOpacityFactor = 0.08f;
            }

            Vector2 lightPos = sunLight != null ? (Vector2)sunLight.transform.position : cachedCampusCenter - Vector2.down;
            Vector2 direction = cachedCampusCenter - lightPos;
            ShadowDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
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

        private static void NormalizePointLightRotation(Light2D light)
        {
            if (light != null && light.lightType == Light2D.LightType.Point)
            {
                light.transform.localRotation = Quaternion.identity;
            }
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
