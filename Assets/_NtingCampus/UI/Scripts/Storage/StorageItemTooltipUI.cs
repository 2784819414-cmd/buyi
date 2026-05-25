using NtingCampus.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Nting.Storage
{
    public sealed class StorageItemTooltipUI
    {
        private const float Width = 340f;
        private const float HudWidth = 276f;
        private const float CompactHeight = 112f;
        private const float HudCompactHeight = 96f;
        private const float HudDescriptionHeight = 128f;
        private const float HudStateHeight = 150f;
        private const float DescriptionHeight = 150f;
        private const float StateHeight = 174f;
        private const float CursorOffset = 22f;
        private const float ScreenPadding = 12f;
        private const float AnchorGap = 14f;

        private readonly RectTransform root;
        private readonly RectTransform shadow;
        private readonly RectTransform panel;
        private readonly Text nameText;
        private readonly Text metaText;
        private readonly Text descriptionText;
        private readonly Text stateText;

        private StorageItemTooltipUI(
            RectTransform root,
            RectTransform shadow,
            RectTransform panel,
            Text nameText,
            Text metaText,
            Text descriptionText,
            Text stateText)
        {
            this.root = root;
            this.shadow = shadow;
            this.panel = panel;
            this.nameText = nameText;
            this.metaText = metaText;
            this.descriptionText = descriptionText;
            this.stateText = stateText;
        }

        public static StorageItemTooltipUI Create(RectTransform parent)
        {
            GameObject rootObject = StorageUIUtility.CreateRectObject("StorageItemTooltip", parent);
            RectTransform root = rootObject.GetComponent<RectTransform>();
            StorageUIUtility.SetAnchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            root.sizeDelta = new Vector2(Width, CompactHeight);
            root.gameObject.SetActive(false);

            CanvasGroup group = rootObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform shadow = StorageUIUtility.CreateBox(
                "Shadow",
                root,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(8f, -8f),
                new Vector2(Width, CompactHeight),
                StoragePalette.WindowShadow,
                Color.clear,
                0f,
                14f);

            RectTransform panel = StorageUIUtility.CreateBox(
                "Panel",
                root,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(Width, CompactHeight),
                StoragePalette.PanelRaised,
                StoragePalette.PanelBorder,
                1f,
                14f);

            StorageUIUtility.CreateBox(
                "Accent",
                panel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(16f, -16f),
                new Vector2(4f, 44f),
                StoragePalette.Accent,
                Color.clear,
                0f,
                2f);

            Text nameText = StorageUIUtility.CreateText("Name", panel, string.Empty, 18, TextAnchor.MiddleLeft, StoragePalette.TextPrimary);
            nameText.fontStyle = FontStyle.Bold;
            StorageUIUtility.SetTopLeft(nameText.rectTransform, 32f, 14f, 284f, 26f);

            Text metaText = StorageUIUtility.CreateText("Meta", panel, string.Empty, 13, TextAnchor.MiddleLeft, StoragePalette.TextSecondary);
            StorageUIUtility.SetTopLeft(metaText.rectTransform, 32f, 42f, 284f, 20f);

            Text descriptionText = StorageUIUtility.CreateText("Description", panel, string.Empty, 13, TextAnchor.UpperLeft, StoragePalette.TextSecondary);
            descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            StorageUIUtility.SetTopLeft(descriptionText.rectTransform, 18f, 76f, 304f, 44f);

            Text stateText = StorageUIUtility.CreateText("State", panel, string.Empty, 12, TextAnchor.MiddleLeft, StoragePalette.PaperDim);
            StorageUIUtility.SetTopLeft(stateText.rectTransform, 18f, 134f, 304f, 24f);

            return new StorageItemTooltipUI(root, shadow, panel, nameText, metaText, descriptionText, stateText);
        }

        public void Show(
            StorageItemModel item,
            StorageContainerModel container,
            RectTransform parent,
            Vector2 screenPosition,
            Camera eventCamera)
        {
            if (item == null || parent == null)
            {
                Hide();
                return;
            }

            string description = item.GetDescription();
            string state = BuildStateText(item, container);
            bool hasDescription = !string.IsNullOrWhiteSpace(description);
            bool hasState = !string.IsNullOrWhiteSpace(state);
            float height = hasState && hasDescription
                ? StateHeight
                : hasDescription
                    ? DescriptionHeight
                    : CompactHeight;

            ApplyLayout(
                Width,
                height,
                18,
                13,
                13,
                32f,
                14f,
                284f,
                26f,
                32f,
                42f,
                284f,
                20f,
                18f,
                76f,
                304f,
                44f,
                18f,
                hasDescription ? 134f : 76f,
                304f,
                24f);

            nameText.text = item.GetDisplayName();
            metaText.text = item.CurrentWidth + "x" + item.CurrentHeight + "  " + item.Weight.ToString("0.#") + "kg";
            descriptionText.text = description;
            descriptionText.gameObject.SetActive(hasDescription);
            stateText.text = state;
            stateText.gameObject.SetActive(hasState);

            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            Move(parent, screenPosition, eventCamera);
        }

        public void ShowAnchoredCompact(
            StorageItemModel item,
            StorageContainerModel container,
            RectTransform parent,
            RectTransform anchor,
            Camera eventCamera)
        {
            if (item == null || parent == null || anchor == null)
            {
                Hide();
                return;
            }

            string description = item.GetDescription();
            string state = BuildStateText(item, container);
            bool hasDescription = !string.IsNullOrWhiteSpace(description);
            bool hasState = !string.IsNullOrWhiteSpace(state);
            float height = hasState && hasDescription
                ? HudStateHeight
                : hasDescription
                    ? HudDescriptionHeight
                    : HudCompactHeight;

            ApplyLayout(
                HudWidth,
                height,
                16,
                13,
                12,
                28f,
                12f,
                252f,
                22f,
                28f,
                38f,
                252f,
                18f,
                16f,
                68f,
                244f,
                38f,
                16f,
                hasDescription ? 118f : 68f,
                244f,
                22f);

            nameText.text = item.GetDisplayName();
            metaText.text = item.CurrentWidth + "x" + item.CurrentHeight + "  " + item.Weight.ToString("0.#") + "kg";
            descriptionText.text = description;
            descriptionText.gameObject.SetActive(hasDescription);
            stateText.text = state;
            stateText.gameObject.SetActive(hasState);

            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            MoveAnchored(parent, anchor, eventCamera);
        }

        public void Move(RectTransform parent, Vector2 screenPosition, Camera eventCamera)
        {
            if (!root.gameObject.activeSelf || parent == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, eventCamera, out Vector2 localPoint))
            {
                return;
            }

            Rect parentRect = parent.rect;
            float x = localPoint.x - parentRect.xMin + CursorOffset;
            float y = parentRect.yMax - localPoint.y + CursorOffset;
            x = Mathf.Clamp(x, ScreenPadding, Mathf.Max(ScreenPadding, parentRect.width - root.sizeDelta.x - ScreenPadding));
            y = Mathf.Clamp(y, ScreenPadding, Mathf.Max(ScreenPadding, parentRect.height - root.sizeDelta.y - ScreenPadding));
            root.anchoredPosition = new Vector2(x, -y);
        }

        public void MoveAnchored(RectTransform parent, RectTransform anchor, Camera eventCamera)
        {
            if (!root.gameObject.activeSelf || parent == null || anchor == null)
            {
                return;
            }

            Rect parentRect = parent.rect;
            Rect anchorRect = BuildLocalRect(parent, anchor, eventCamera);
            float x = anchorRect.xMin - root.sizeDelta.x - AnchorGap;
            float y = anchorRect.yMax;

            if (x < ScreenPadding)
            {
                x = anchorRect.xMax + AnchorGap;
            }

            x = Mathf.Clamp(x, ScreenPadding, Mathf.Max(ScreenPadding, parentRect.width - root.sizeDelta.x - ScreenPadding));
            y = Mathf.Clamp(y, ScreenPadding, Mathf.Max(ScreenPadding, parentRect.height - root.sizeDelta.y - ScreenPadding));
            root.anchoredPosition = new Vector2(x, -y);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private static string BuildStateText(StorageItemModel item, StorageContainerModel container)
        {
            string state = item.IsUsable
                ? StorageTextCatalog.Get(StorageTextId.UsableFromHand)
                : string.Empty;

            if (CampusProtectedTransferState.ShouldDisplayPendingCheckout(item, container))
            {
                return AppendState(state, BuildSourceState(StorageTextId.PendingCheckout, item));
            }

            return item.IsStolenEvidence
                ? AppendState(state, BuildStolenState(item))
                : state;
        }

        private void ApplyLayout(
            float width,
            float height,
            int nameSize,
            int metaSize,
            int detailSize,
            float nameX,
            float nameY,
            float nameWidth,
            float nameHeight,
            float metaX,
            float metaY,
            float metaWidth,
            float metaHeight,
            float descriptionX,
            float descriptionY,
            float descriptionWidth,
            float descriptionHeight,
            float stateX,
            float stateY,
            float stateWidth,
            float stateHeight)
        {
            root.sizeDelta = new Vector2(width, height);
            shadow.sizeDelta = root.sizeDelta;
            panel.sizeDelta = root.sizeDelta;

            nameText.fontSize = nameSize;
            metaText.fontSize = metaSize;
            descriptionText.fontSize = detailSize;
            stateText.fontSize = detailSize;

            StorageUIUtility.SetTopLeft(nameText.rectTransform, nameX, nameY, nameWidth, nameHeight);
            StorageUIUtility.SetTopLeft(metaText.rectTransform, metaX, metaY, metaWidth, metaHeight);
            StorageUIUtility.SetTopLeft(descriptionText.rectTransform, descriptionX, descriptionY, descriptionWidth, descriptionHeight);
            StorageUIUtility.SetTopLeft(stateText.rectTransform, stateX, stateY, stateWidth, stateHeight);
        }

        private static Rect BuildLocalRect(RectTransform parent, RectTransform target, Camera eventCamera)
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Rect parentRect = parent.rect;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, eventCamera, out Vector2 localPoint))
                {
                    continue;
                }

                float x = localPoint.x - parentRect.xMin;
                float y = parentRect.yMax - localPoint.y;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            if (float.IsInfinity(minX))
            {
                return new Rect();
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static string BuildStolenState(StorageItemModel item)
        {
            string state = BuildSourceState(StorageTextId.Stolen, item);
            if (item.SuspicionRisk > 0)
            {
                state += ", " + StorageTextCatalog.Get(StorageTextId.Risk) + " " + item.SuspicionRisk;
            }

            return state;
        }

        private static string BuildSourceState(StorageTextId stateId, StorageItemModel item)
        {
            string state = StorageTextCatalog.Get(stateId);
            if (!string.IsNullOrWhiteSpace(item.SourceLocation))
            {
                state += " " + StorageTextCatalog.Get(StorageTextId.From) + " " + item.SourceLocation;
            }

            return state;
        }

        private static string AppendState(string current, string next)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return next;
            }

            return string.IsNullOrWhiteSpace(next)
                ? current
                : current + "  |  " + next;
        }
    }
}
