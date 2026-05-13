using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public sealed class NtingPointLightShadowProfile : MonoBehaviour
    {
        [Min(0f)] public float shadowLengthWeight = 1f;
        [Min(0f)] public float shadowAlphaWeight = 1f;
        [Min(0f)] public float shadowFillWeight = 1f;

        private void OnValidate()
        {
            shadowLengthWeight = Mathf.Max(0f, shadowLengthWeight);
            shadowAlphaWeight = Mathf.Max(0f, shadowAlphaWeight);
            shadowFillWeight = Mathf.Max(0f, shadowFillWeight);
        }
    }
}
