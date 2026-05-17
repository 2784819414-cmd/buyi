using System;
using UnityEngine;

namespace NtingCampus.Gameplay.UI
{
    public static class CampusGuiScaleUtility
    {
        public readonly struct Scope : IDisposable
        {
            private readonly Matrix4x4 previousMatrix;

            public Scope(Matrix4x4 matrix)
            {
                previousMatrix = GUI.matrix;
                GUI.matrix = matrix;
            }

            public void Dispose()
            {
                GUI.matrix = previousMatrix;
            }
        }

        public static Scope BeginScaledGui(
            Vector2 referenceResolution,
            float matchWidthOrHeight,
            float sensitivity,
            float minScale,
            float maxScale)
        {
            float scale = CalculateScale(referenceResolution, matchWidthOrHeight, sensitivity, minScale, maxScale);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            return new Scope(matrix);
        }

        public static Rect BuildCenteredRect(Vector2 referenceResolution, Vector2 contentSize, Vector2 offset)
        {
            float x = (referenceResolution.x - contentSize.x) * 0.5f + offset.x;
            float y = (referenceResolution.y - contentSize.y) * 0.5f + offset.y;
            return new Rect(x, y, contentSize.x, contentSize.y);
        }

        public static float CalculateScale(
            Vector2 referenceResolution,
            float matchWidthOrHeight,
            float sensitivity,
            float minScale,
            float maxScale)
        {
            float safeReferenceWidth = Mathf.Max(1f, referenceResolution.x);
            float safeReferenceHeight = Mathf.Max(1f, referenceResolution.y);
            float widthScale = Screen.width / safeReferenceWidth;
            float heightScale = Screen.height / safeReferenceHeight;
            float fittedScale = Mathf.Lerp(widthScale, heightScale, Mathf.Clamp01(matchWidthOrHeight));
            float scaled = Mathf.Lerp(1f, fittedScale, Mathf.Max(0f, sensitivity));
            return Mathf.Clamp(scaled, Mathf.Max(0.1f, minScale), Mathf.Max(minScale, maxScale));
        }
    }
}
