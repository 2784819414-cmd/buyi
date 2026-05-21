using Nting.Storage;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CampusCharacterRuntime))]
    public sealed class CampusHeldItemVisual : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.2f;
        private const float CanvasScale = 0.0062f;

        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image leftHandImage;
        [SerializeField] private Image rightHandImage;
        [SerializeField] private Text leftHandLabel;
        [SerializeField] private Text rightHandLabel;

        private float nextRefreshTime;

        private void Awake()
        {
            EnsureSetup();
            RefreshImmediate();
        }

        private void OnEnable()
        {
            EnsureSetup();
            RefreshImmediate();
        }

        private void LateUpdate()
        {
            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + RefreshIntervalSeconds;
            RefreshImmediate();
        }

        public void RefreshImmediate()
        {
            EnsureSetup();
            bool visible = runtime != null && runtime.Data != null;
            if (!visible)
            {
                SetCanvasVisible(false);
                return;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(runtime, false);
            StorageItemModel leftItem = ResolveFirstItem(inventory.Hands, 0);
            StorageItemModel rightItem = ResolveFirstItem(inventory.Hands, 1);
            bool hasAnyItem = leftItem != null || rightItem != null;
            SetCanvasVisible(hasAnyItem);
            if (!hasAnyItem)
            {
                return;
            }

            ApplyItem(leftHandImage, leftHandLabel, leftItem);
            ApplyItem(rightHandImage, rightHandLabel, rightItem);
        }

        private void EnsureSetup()
        {
            runtime = runtime != null ? runtime : GetComponent<CampusCharacterRuntime>();
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("HeldItemVisual");
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            canvasObject.transform.localScale = Vector3.one * CanvasScale;

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 4500;

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(150f, 56f);

            leftHandImage = CreateHandImage(root, "LeftHandItem", new Vector2(-32f, 0f));
            rightHandImage = CreateHandImage(root, "RightHandItem", new Vector2(32f, 0f));
            leftHandLabel = CreateHandLabel(leftHandImage.rectTransform);
            rightHandLabel = CreateHandLabel(rightHandImage.rectTransform);
        }

        private static Image CreateHandImage(RectTransform parent, string objectName, Vector2 anchoredPosition)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(42f, 42f);

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        private static Text CreateHandLabel(RectTransform parent)
        {
            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                        Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static StorageItemModel ResolveFirstItem(StorageContainerModel[] containers, int index)
        {
            if (containers == null || index < 0 || index >= containers.Length || containers[index] == null)
            {
                return null;
            }

            StorageContainerModel container = containers[index];
            return container.Items != null && container.Items.Count > 0 ? container.Items[0] : null;
        }

        private static void ApplyItem(Image image, Text label, StorageItemModel item)
        {
            if (image == null || label == null)
            {
                return;
            }

            if (item == null)
            {
                image.gameObject.SetActive(false);
                label.text = string.Empty;
                return;
            }

            image.gameObject.SetActive(true);
            image.sprite = item.Icon;
            image.color = item.Icon != null ? Color.white : Color.Lerp(item.ThemeColor, Color.white, 0.12f);
            label.text = item.Icon != null ? string.Empty : BuildShortLabel(item);
        }

        private static string BuildShortLabel(StorageItemModel item)
        {
            string source = item != null && !string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.DisplayName.Trim()
                : (item != null ? item.DefinitionId : string.Empty);
            if (string.IsNullOrWhiteSpace(source))
            {
                return "?";
            }

            return source.Length <= 2 ? source.ToUpperInvariant() : source.Substring(0, 2).ToUpperInvariant();
        }

        private void SetCanvasVisible(bool visible)
        {
            if (canvas != null && canvas.gameObject.activeSelf != visible)
            {
                canvas.gameObject.SetActive(visible);
            }
        }
    }
}
