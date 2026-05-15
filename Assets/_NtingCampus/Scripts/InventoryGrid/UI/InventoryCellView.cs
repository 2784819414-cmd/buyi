using UnityEngine;

namespace NtingCampus.InventoryGrid
{
    [DisallowMultipleComponent]
    public sealed class InventoryCellView : MonoBehaviour
    {
        public int x;
        public int y;
        public InventoryRoundedBoxGraphic background;

        private static readonly Color NormalColor = new Color(0.12f, 0.14f, 0.16f, 0.92f);
        private static readonly Color NormalBorderColor = new Color(0.27f, 0.31f, 0.35f, 0.95f);
        private static readonly Color ValidPreviewColor = new Color(0.14f, 0.72f, 0.42f, 0.78f);
        private static readonly Color ValidBorderColor = new Color(0.46f, 1f, 0.68f, 1f);
        private static readonly Color InvalidPreviewColor = new Color(0.84f, 0.16f, 0.16f, 0.78f);
        private static readonly Color InvalidBorderColor = new Color(1f, 0.42f, 0.36f, 1f);
        private static readonly Color OccupiedColor = new Color(0.08f, 0.09f, 0.1f, 0.97f);
        private static readonly Color OccupiedBorderColor = new Color(0.36f, 0.4f, 0.44f, 1f);

        private void Awake()
        {
            EnsureBackground();
        }

        public void Bind(int x, int y)
        {
            this.x = x;
            this.y = y;
            EnsureBackground();
            SetNormal();
        }

        public void SetNormal()
        {
            EnsureBackground();
            SetBackground(NormalColor, NormalBorderColor, 1f);
        }

        public void SetValidPreview()
        {
            EnsureBackground();
            SetBackground(ValidPreviewColor, ValidBorderColor, 2f);
        }

        public void SetInvalidPreview()
        {
            EnsureBackground();
            SetBackground(InvalidPreviewColor, InvalidBorderColor, 2f);
        }

        public void SetOccupied()
        {
            EnsureBackground();
            SetBackground(OccupiedColor, OccupiedBorderColor, 1.5f);
        }

        private void EnsureBackground()
        {
            if (background == null)
            {
                background = GetComponent<InventoryRoundedBoxGraphic>();
            }

            if (background == null)
            {
                background = gameObject.AddComponent<InventoryRoundedBoxGraphic>();
            }

            background.raycastTarget = false;
            background.CornerRadius = 5f;
            background.CornerSegments = 4;
        }

        private void SetBackground(Color fillColor, Color borderColor, float borderWidth)
        {
            background.FillColor = fillColor;
            background.BorderColor = borderColor;
            background.BorderWidth = borderWidth;
            background.SetAllDirty();
        }
    }

}
