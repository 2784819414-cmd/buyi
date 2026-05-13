using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.InventoryGrid
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InventoryRoundedBoxGraphic : MaskableGraphic
    {
        public Color FillColor = Color.white;
        public Color BorderColor = Color.clear;
        public float BorderWidth = 0f;
        public float CornerRadius = 0f;
        public int CornerSegments = 5;

        private readonly List<Vector2> outerPoints = new List<Vector2>(32);
        private readonly List<Vector2> innerPoints = new List<Vector2>(32);

        protected override void OnEnable()
        {
            if (GetComponent<CanvasRenderer>() == null)
            {
                gameObject.AddComponent<CanvasRenderer>();
            }

            base.OnEnable();
        }

        public void SetStyle(Color fillColor, Color borderColor, float borderWidth, float cornerRadius)
        {
            FillColor = fillColor;
            BorderColor = borderColor;
            BorderWidth = Mathf.Max(0f, borderWidth);
            CornerRadius = Mathf.Max(0f, cornerRadius);
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            int segments = Mathf.Clamp(CornerSegments, 1, 12);
            float radius = Mathf.Clamp(CornerRadius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            BuildRoundedRect(rect, radius, segments, outerPoints);

            Color fill = Multiply(FillColor, color);
            if (fill.a > 0.001f)
            {
                AddFilledPolygon(vertexHelper, outerPoints, fill);
            }

            float borderWidth = Mathf.Clamp(BorderWidth, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            Color border = Multiply(BorderColor, color);
            if (borderWidth <= 0.001f || border.a <= 0.001f)
            {
                return;
            }

            Rect innerRect = new Rect(
                rect.xMin + borderWidth,
                rect.yMin + borderWidth,
                Mathf.Max(0f, rect.width - borderWidth * 2f),
                Mathf.Max(0f, rect.height - borderWidth * 2f));

            if (innerRect.width <= 0f || innerRect.height <= 0f)
            {
                AddFilledPolygon(vertexHelper, outerPoints, border);
                return;
            }

            BuildRoundedRect(innerRect, Mathf.Max(0f, radius - borderWidth), segments, innerPoints);
            AddBorderRing(vertexHelper, outerPoints, innerPoints, border);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            BorderWidth = Mathf.Max(0f, BorderWidth);
            CornerRadius = Mathf.Max(0f, CornerRadius);
            CornerSegments = Mathf.Clamp(CornerSegments, 1, 12);
            SetAllDirty();
        }

        private static void BuildRoundedRect(Rect rect, float radius, int segments, List<Vector2> points)
        {
            points.Clear();
            if (radius <= 0.001f)
            {
                points.Add(new Vector2(rect.xMin, rect.yMax));
                points.Add(new Vector2(rect.xMax, rect.yMax));
                points.Add(new Vector2(rect.xMax, rect.yMin));
                points.Add(new Vector2(rect.xMin, rect.yMin));
                return;
            }

            AddArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius), 180f, 90f, radius, segments);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius), 90f, 0f, radius, segments);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius), 0f, -90f, radius, segments);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius), -90f, -180f, radius, segments);
        }

        private static void AddArc(List<Vector2> points, Vector2 center, float startAngle, float endAngle, float radius, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float t = segments == 0 ? 1f : i / (float)segments;
                float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
                points.Add(new Vector2(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius));
            }
        }

        private static void AddFilledPolygon(VertexHelper vertexHelper, List<Vector2> points, Color fillColor)
        {
            if (points.Count < 3)
            {
                return;
            }

            Vector2 center = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
            {
                center += points[i];
            }

            center /= points.Count;

            int centerIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(center, fillColor, Vector2.zero);
            for (int i = 0; i < points.Count; i++)
            {
                vertexHelper.AddVert(points[i], fillColor, Vector2.zero);
            }

            for (int i = 0; i < points.Count; i++)
            {
                int current = centerIndex + 1 + i;
                int next = centerIndex + 1 + ((i + 1) % points.Count);
                vertexHelper.AddTriangle(centerIndex, next, current);
            }
        }

        private static void AddBorderRing(VertexHelper vertexHelper, List<Vector2> outer, List<Vector2> inner, Color borderColor)
        {
            int count = Mathf.Min(outer.Count, inner.Count);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int startIndex = vertexHelper.currentVertCount;
                vertexHelper.AddVert(outer[i], borderColor, Vector2.zero);
                vertexHelper.AddVert(outer[next], borderColor, Vector2.zero);
                vertexHelper.AddVert(inner[next], borderColor, Vector2.zero);
                vertexHelper.AddVert(inner[i], borderColor, Vector2.zero);
                vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
            }
        }

        private static Color Multiply(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        }
    }
}
