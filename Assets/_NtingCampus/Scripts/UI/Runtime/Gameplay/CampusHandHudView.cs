using Nting.Storage;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusHandHudView : MonoBehaviour
    {
        private const string CanvasRootName = "CampusHandHudCanvas";
        private const int SortingOrder = 32500;

        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private RectTransform hudRoot;
        [SerializeField] private StorageGridUI leftHandGrid;
        [SerializeField] private StorageGridUI rightHandGrid;
        [SerializeField] private Text leftHandLabelText;
        [SerializeField] private Text rightHandLabelText;

        private HandSlotVisual leftHandVisual;
        private HandSlotVisual rightHandVisual;

        public void Apply(StorageContainerModel[] hands, StorageWindowUI window)
        {
            EnsureVisual();
            bool interactive = window != null && window.IsOpen;
            StorageContainerModel leftHand = CampusHandInventoryUtility.ResolveHandContainer(hands, 0);
            StorageContainerModel rightHand = CampusHandInventoryUtility.ResolveHandContainer(hands, 1);

            RebindGrids(leftHand, rightHand, interactive ? window : null);
            ApplyHeldItemVisuals(leftHand, rightHand);

            UpdateLabels();
            SetInteractive(interactive);
        }

        private void EnsureVisual()
        {
            if (canvas != null &&
                canvasGroup != null &&
                hudRoot != null &&
                leftHandGrid != null &&
                rightHandGrid != null &&
                leftHandVisual != null &&
                rightHandVisual != null)
            {
                return;
            }

            Transform existingRoot = transform.Find(CanvasRootName);
            GameObject canvasObject;
            if (existingRoot == null)
            {
                canvasObject = new GameObject(
                    CanvasRootName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(CanvasGroup));
                canvasObject.transform.SetParent(transform, false);
            }
            else
            {
                canvasObject = existingRoot.gameObject;
            }

            canvasRoot = canvasObject.GetComponent<RectTransform>();
            canvasRoot.anchorMin = Vector2.zero;
            canvasRoot.anchorMax = Vector2.one;
            canvasRoot.offsetMin = Vector2.zero;
            canvasRoot.offsetMax = Vector2.zero;

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.GetComponent<CanvasGroup>();

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.enabled = true;

            BuildHud();
            UpdateLabels();
            SetInteractive(false);
        }

        private void BuildHud()
        {
            ClearExistingHud();

            hudRoot = StorageUIUtility.CreateBox(
                "HandHudRoot",
                canvasRoot,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(20f, 20f),
                new Vector2(256f, 132f),
                StoragePalette.Panel,
                StoragePalette.PanelBorder,
                1.2f,
                18f);

            RectTransform shadow = StorageUIUtility.CreateStretchBox(
                "HandHudShadow",
                hudRoot,
                new Vector2(-5f, -5f),
                new Vector2(5f, 5f),
                StoragePalette.WindowShadow,
                Color.clear,
                0f,
                20f);
            shadow.SetAsFirstSibling();

            RectTransform leftCard = CreateHandCard(hudRoot, "LeftHandCard", new Vector2(14f, -14f), out leftHandLabelText);
            RectTransform rightCard = CreateHandCard(hudRoot, "RightHandCard", new Vector2(136f, -14f), out rightHandLabelText);
            leftHandGrid = CreateHandGrid(leftCard, "LeftHandGrid");
            rightHandGrid = CreateHandGrid(rightCard, "RightHandGrid");
            leftHandVisual = HandSlotVisual.Create(leftCard, "LeftHandHeldVisual", new Vector2(17f, -27f), 72f);
            rightHandVisual = HandSlotVisual.Create(rightCard, "RightHandHeldVisual", new Vector2(17f, -27f), 72f);
        }

        private static RectTransform CreateHandCard(RectTransform parent, string name, Vector2 position, out Text label)
        {
            RectTransform card = StorageUIUtility.CreateBox(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                position,
                new Vector2(106f, 104f),
                StoragePalette.PanelRaised,
                StoragePalette.PanelBorder,
                0.9f,
                14f);

            RectTransform strip = StorageUIUtility.CreateBox(
                "TitleStrip",
                card,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 22f),
                StoragePalette.PanelHeader,
                Color.clear,
                0f,
                12f);
            strip.offsetMin = new Vector2(8f, -22f);
            strip.offsetMax = new Vector2(-8f, 0f);

            label = StorageUIUtility.CreateText("Label", strip, string.Empty, 12, TextAnchor.MiddleCenter, StoragePalette.TextPrimary);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return card;
        }

        private static StorageGridUI CreateHandGrid(RectTransform parent, string name)
        {
            GameObject gridObject = new GameObject(name, typeof(RectTransform), typeof(StorageGridUI));
            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.SetParent(parent, false);
            StorageUIUtility.SetAnchor(gridRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            gridRect.anchoredPosition = new Vector2(17f, -27f);

            StorageGridUI grid = gridObject.GetComponent<StorageGridUI>();
            grid.DropArea = parent;
            grid.CellSize = 72f;
            grid.CellSpacing = 0f;
            grid.RenderItemViews = false;
            return grid;
        }

        private void RebindGrids(
            StorageContainerModel leftHand,
            StorageContainerModel rightHand,
            StorageWindowUI ownerWindow)
        {
            leftHandGrid.Bind(leftHand, ownerWindow);
            rightHandGrid.Bind(rightHand, ownerWindow);
        }

        private void ApplyHeldItemVisuals(StorageContainerModel leftHand, StorageContainerModel rightHand)
        {
            leftHandVisual?.Apply(leftHand);
            rightHandVisual?.Apply(rightHand);
        }

        private void UpdateLabels()
        {
            if (leftHandLabelText != null)
            {
                leftHandLabelText.text = StorageTextCatalog.Get(StorageTextId.LeftHand);
            }

            if (rightHandLabelText != null)
            {
                rightHandLabelText.text = StorageTextCatalog.Get(StorageTextId.RightHand);
            }
        }

        private void SetInteractive(bool interactive)
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = interactive;
                canvasGroup.interactable = interactive;
                canvasGroup.alpha = 1f;
            }

            if (leftHandGrid != null)
            {
                leftHandGrid.SetItemsRaycast(interactive);
            }

            if (rightHandGrid != null)
            {
                rightHandGrid.SetItemsRaycast(interactive);
            }
        }

        private void ClearExistingHud()
        {
            if (canvasRoot == null)
            {
                return;
            }

            Transform existing = canvasRoot.Find("HandHudRoot");
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        private sealed class HandSlotVisual
        {
            private readonly RectTransform root;
            private readonly StorageBoxGraphic plate;
            private readonly Image iconImage;
            private readonly Text fallbackText;
            private readonly StorageBoxGraphic badge;
            private readonly Text badgeText;

            private HandSlotVisual(
                RectTransform root,
                StorageBoxGraphic plate,
                Image iconImage,
                Text fallbackText,
                StorageBoxGraphic badge,
                Text badgeText)
            {
                this.root = root;
                this.plate = plate;
                this.iconImage = iconImage;
                this.fallbackText = fallbackText;
                this.badge = badge;
                this.badgeText = badgeText;
            }

            public static HandSlotVisual Create(RectTransform parent, string name, Vector2 position, float size)
            {
                GameObject rootObject = StorageUIUtility.CreateRectObject(name, parent);
                RectTransform root = rootObject.GetComponent<RectTransform>();
                StorageUIUtility.SetAnchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                root.anchoredPosition = position;
                root.sizeDelta = new Vector2(size, size);
                root.SetAsLastSibling();

                StorageBoxGraphic plate = StorageUIUtility.CreateStretchBox(
                    "HeldItemPlate",
                    root,
                    new Vector2(3f, 3f),
                    new Vector2(-3f, -3f),
                    Color.clear,
                    Color.clear,
                    0f,
                    9f).GetComponent<StorageBoxGraphic>();
                plate.raycastTarget = false;

                Image icon = CreateIconImage(root);
                Text fallback = StorageUIUtility.CreateText("FallbackLabel", root, string.Empty, 18, TextAnchor.MiddleCenter, StoragePalette.TextPrimary);
                fallback.raycastTarget = false;
                fallback.rectTransform.offsetMin = new Vector2(6f, 6f);
                fallback.rectTransform.offsetMax = new Vector2(-6f, -6f);

                StorageBoxGraphic badge = StorageUIUtility.CreateBox(
                    "StatusBadge",
                    root,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-7f, -7f),
                    new Vector2(20f, 20f),
                    Color.clear,
                    Color.clear,
                    0f,
                    4f).GetComponent<StorageBoxGraphic>();
                badge.raycastTarget = false;

                Text badgeText = StorageUIUtility.CreateText("Text", badge.transform, string.Empty, 13, TextAnchor.MiddleCenter, Color.white);
                badgeText.raycastTarget = false;
                badgeText.rectTransform.offsetMin = Vector2.zero;
                badgeText.rectTransform.offsetMax = Vector2.zero;

                root.gameObject.SetActive(false);
                return new HandSlotVisual(root, plate, icon, fallback, badge, badgeText);
            }

            public void Apply(StorageContainerModel hand)
            {
                StorageItemModel item = ResolveHeldItem(hand);
                if (item == null)
                {
                    root.gameObject.SetActive(false);
                    return;
                }

                root.gameObject.SetActive(true);
                ApplyItem(item);
                ApplyBadge(item, hand);
            }

            private void ApplyItem(StorageItemModel item)
            {
                Sprite icon = StorageItemIconUtility.Resolve(item);
                Color theme = item != null ? item.ThemeColor : StoragePalette.Accent;
                plate.SetStyle(
                    Color.Lerp(StoragePalette.ItemBase, theme, 0.1f),
                    new Color(1f, 0.74f, 0.24f, 0.52f),
                    0.8f,
                    9f);

                bool hasIcon = icon != null;
                iconImage.gameObject.SetActive(hasIcon);
                fallbackText.gameObject.SetActive(!hasIcon);
                if (hasIcon)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                    fallbackText.text = string.Empty;
                    return;
                }

                iconImage.sprite = null;
                fallbackText.color = StoragePalette.TextPrimary;
                fallbackText.text = BuildShortLabel(item);
            }

            private void ApplyBadge(StorageItemModel item, StorageContainerModel hand)
            {
                HandItemBadgeState state = ResolveBadgeState(item, hand);
                if (state == HandItemBadgeState.None)
                {
                    badge.gameObject.SetActive(false);
                    badgeText.text = string.Empty;
                    return;
                }

                badge.gameObject.SetActive(true);
                badgeText.text = "!";
                if (state == HandItemBadgeState.PendingProtectedTransfer)
                {
                    badge.SetStyle(
                        new Color(0.42f, 0.31f, 0.08f, 0.94f),
                        new Color(1f, 0.88f, 0.42f, 0.78f),
                        0.8f,
                        4f);
                    badgeText.color = StoragePalette.Warning;
                    return;
                }

                badge.SetStyle(
                    new Color(0.58f, 0.12f, 0.1f, 0.94f),
                    new Color(1f, 0.78f, 0.65f, 0.7f),
                    0.8f,
                    4f);
                badgeText.color = Color.white;
            }

            private static Image CreateIconImage(RectTransform parent)
            {
                GameObject imageObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = imageObject.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(5f, 5f);
                rect.offsetMax = new Vector2(-5f, -5f);

                Image image = imageObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.gameObject.SetActive(false);
                return image;
            }

            private static StorageItemModel ResolveHeldItem(StorageContainerModel hand)
            {
                return hand != null && hand.Items != null && hand.Items.Count > 0
                    ? hand.Items[0]
                    : null;
            }

            private static HandItemBadgeState ResolveBadgeState(StorageItemModel item, StorageContainerModel hand)
            {
                if (item == null)
                {
                    return HandItemBadgeState.None;
                }

                if (CampusProtectedTransferState.ShouldDisplayPendingCheckout(item, hand))
                {
                    return HandItemBadgeState.PendingProtectedTransfer;
                }

                return item.IsStolenEvidence ? HandItemBadgeState.StolenEvidence : HandItemBadgeState.None;
            }

            private static string BuildShortLabel(StorageItemModel item)
            {
                string source = item != null && !string.IsNullOrWhiteSpace(item.DisplayName)
                    ? item.DisplayName.Trim()
                    : item != null
                        ? item.DefinitionId
                        : string.Empty;
                if (string.IsNullOrWhiteSpace(source))
                {
                    return "?";
                }

                return source.Length <= 2 ? source.ToUpperInvariant() : source.Substring(0, 2).ToUpperInvariant();
            }

            private enum HandItemBadgeState
            {
                None = 0,
                PendingProtectedTransfer = 1,
                StolenEvidence = 2
            }
        }
    }
}
