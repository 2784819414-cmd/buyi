using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    internal sealed class CampusStartupLogoGlowController : MonoBehaviour
    {
        private const string ShaderResourcePath = "Shaders/CampusStartupLogoGlow";
        private const string ShaderName = "NtingCampus/UI/Startup Logo Glow";
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int BodyBoostId = Shader.PropertyToID("_BodyBoost");

        [SerializeField] private Color glowColor = new Color(0.44f, 0.54f, 0.44f, 0.30f);
        [SerializeField] [Range(0f, 1f)] private float baseGlowStrength = 0.10f;
        [SerializeField] [Range(0f, 1f)] private float pulseGlowStrength = 0.045f;
        [SerializeField] [Range(2f, 12f)] private float pulseSeconds = 6.2f;
        [SerializeField] [Range(0f, 32f)] private float glowRadius = 13f;
        [SerializeField] [Range(0.5f, 2f)] private float bodyBoost = 1.08f;
        [SerializeField] [Range(0f, 4f)] private float startDelaySeconds = 1.0f;

        private Image targetImage;
        private Material runtimeMaterial;
        private float enabledAt;

        private void Awake()
        {
            targetImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            enabledAt = Time.unscaledTime;
            EnsureMaterial();
            ApplyStaticMaterialProperties();
            ApplyGlowStrength(baseGlowStrength);
        }

        private void Update()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            float elapsed = Mathf.Max(0f, Time.unscaledTime - enabledAt - startDelaySeconds);
            float normalized = pulseSeconds > 0.01f
                ? Mathf.Repeat(elapsed / pulseSeconds, 1f)
                : 0f;
            float wave = 0.5f + 0.5f * Mathf.Sin((normalized - 0.25f) * Mathf.PI * 2f);
            float smoothWave = wave * wave * (3f - 2f * wave);
            ApplyGlowStrength(baseGlowStrength + pulseGlowStrength * smoothWave);
        }

        private void OnDestroy()
        {
            if (targetImage != null && targetImage.material == runtimeMaterial)
            {
                targetImage.material = null;
            }

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        private void EnsureMaterial()
        {
            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }

            if (targetImage == null || runtimeMaterial != null)
            {
                return;
            }

            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
            }

            if (shader == null)
            {
                enabled = false;
                return;
            }

            runtimeMaterial = new Material(shader)
            {
                name = "CampusStartupLogoGlow_Runtime",
                hideFlags = HideFlags.DontSave
            };
            targetImage.material = runtimeMaterial;
        }

        private void ApplyStaticMaterialProperties()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            runtimeMaterial.SetColor(GlowColorId, glowColor);
            runtimeMaterial.SetFloat(GlowRadiusId, glowRadius);
            runtimeMaterial.SetFloat(BodyBoostId, bodyBoost);
        }

        private void ApplyGlowStrength(float strength)
        {
            runtimeMaterial.SetFloat(GlowStrengthId, Mathf.Clamp01(strength));
        }
    }
}
