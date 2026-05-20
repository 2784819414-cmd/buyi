using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcPresentation : MonoBehaviour
    {
        private const string VisualRootName = "NpcVisual";

        private static readonly Dictionary<int, Sprite> GeneratedBodySprites = new Dictionary<int, Sprite>();

        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SortingGroup sortingGroup;

        public void Ensure(CampusCharacterData data)
        {
            EnsureSortingGroup();
            EnsureBodyRenderer();

            if (bodyRenderer != null && bodyRenderer.sprite == null)
            {
                bodyRenderer.sprite = GetGeneratedBodySprite(ResolveShirtColor(data));
            }
        }

        private void EnsureSortingGroup()
        {
            if (sortingGroup == null)
            {
                sortingGroup = GetComponent<SortingGroup>();
            }

            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
            }
        }

        private void EnsureBodyRenderer()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (bodyRenderer != null)
            {
                return;
            }

            Transform visualRoot = transform.Find(VisualRootName);
            if (visualRoot == null)
            {
                GameObject visualObject = new GameObject(VisualRootName);
                visualObject.transform.SetParent(transform, false);
                visualRoot = visualObject.transform;
            }

            bodyRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
        }

        private static Color ResolveShirtColor(CampusCharacterData data)
        {
            if (data == null)
            {
                return new Color(0.48f, 0.62f, 0.84f, 1f);
            }

            switch (data.Role)
            {
                case CampusCharacterRole.Teacher:
                    return new Color(0.48f, 0.48f, 0.52f, 1f);
                case CampusCharacterRole.Staff:
                    return new Color(0.65f, 0.54f, 0.32f, 1f);
                default:
                    int seed = CampusNpcStableIds.PositiveModulo(CampusNpcStableIds.Hash(data.Id), 3);
                    if (seed == 0)
                    {
                        return new Color(0.38f, 0.56f, 0.83f, 1f);
                    }

                    return seed == 1
                        ? new Color(0.62f, 0.42f, 0.66f, 1f)
                        : new Color(0.42f, 0.62f, 0.48f, 1f);
            }
        }

        private static Sprite GetGeneratedBodySprite(Color shirtColor)
        {
            int key = ColorToKey(shirtColor);
            if (GeneratedBodySprites.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            const int width = 24;
            const int height = 32;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            FillRect(pixels, width, 9, 24, 6, 5, new Color(0.98f, 0.82f, 0.62f, 1f));
            FillRect(pixels, width, 7, 12, 10, 12, shirtColor);
            FillRect(pixels, width, 8, 4, 3, 8, new Color(0.22f, 0.22f, 0.24f, 1f));
            FillRect(pixels, width, 13, 4, 3, 8, new Color(0.22f, 0.22f, 0.24f, 1f));
            FillRect(pixels, width, 5, 12, 2, 8, shirtColor * 0.9f);
            FillRect(pixels, width, 17, 12, 2, 8, shirtColor * 0.9f);

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.08f), 32f);
            sprite.name = "GeneratedNpcBody_" + key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            GeneratedBodySprites[key] = sprite;
            return sprite;
        }

        private static void FillRect(Color[] pixels, int textureWidth, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    int index = py * textureWidth + px;
                    if (index >= 0 && index < pixels.Length)
                    {
                        pixels[index] = color;
                    }
                }
            }
        }

        private static int ColorToKey(Color color)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
            int a = Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f);
            return (r << 24) ^ (g << 16) ^ (b << 8) ^ a;
        }
    }
}
