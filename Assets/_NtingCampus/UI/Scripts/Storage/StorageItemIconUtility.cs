using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    public static class StorageItemIconUtility
    {
        private const string ResourceRoot = "StorageIcons/";
        private const string GenericIconName = "icon_item";
        private const string GeneratedIconVersion = "generated_v2|";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Resolve(StorageItemModel item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.Icon != null && !IsLegacyGeneratedIcon(item.Icon))
            {
                return item.Icon;
            }

            item.Icon = Resolve(ResolveItemIconKey(item), item.Width, item.Height);
            return item.Icon;
        }

        public static Sprite Resolve(string definitionId, int width, int height, Sprite configuredIcon = null)
        {
            if (configuredIcon != null)
            {
                return configuredIcon;
            }

            string cacheKey = BuildCacheKey(definitionId, width, height);
            if (Cache.TryGetValue(cacheKey, out Sprite cached))
            {
                return cached;
            }

            if (StorageGeneratedIconFactory.TryCreate(definitionId, width, height, out Sprite generated))
            {
                Cache[cacheKey] = generated;
                return generated;
            }

            string iconName = ResolveIconName(definitionId);
            Sprite icon = LoadResourceSprite(iconName);
            if (icon == null && iconName != GenericIconName)
            {
                icon = LoadResourceSprite(GenericIconName);
            }

            Cache[cacheKey] = icon;
            return icon;
        }

        private static Sprite LoadResourceSprite(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return null;
            }

            string cacheKey = "resource:" + iconName;
            if (Cache.TryGetValue(cacheKey, out Sprite cached))
            {
                return cached;
            }

            Sprite icon = Resources.Load<Sprite>(ResourceRoot + iconName);
            Cache[cacheKey] = icon;
            return icon;
        }

        private static string BuildCacheKey(string definitionId, int width, int height)
        {
            string normalizedId = NormalizeDefinitionId(definitionId);
            return GeneratedIconVersion + normalizedId + "|" + Mathf.Max(1, width) + "x" + Mathf.Max(1, height);
        }

        private static string ResolveIconName(string definitionId)
        {
            string normalized = NormalizeDefinitionId(definitionId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return GenericIconName;
            }

            if (normalized.StartsWith("stolen_delivery_"))
            {
                return "icon_delivery";
            }

            if (normalized.StartsWith("stolen_") ||
                normalized.Contains("food") ||
                normalized.Contains("burger") ||
                normalized.Contains("oden") ||
                normalized.Contains("fried"))
            {
                return "icon_food";
            }

            return "icon_" + normalized;
        }

        internal static string NormalizeDefinitionId(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return string.Empty;
            }

            char[] chars = definitionId.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars).Trim('_');
        }

        private static string ResolveItemIconKey(StorageItemModel item)
        {
            if (item == null)
            {
                return "item";
            }

            if (!string.IsNullOrWhiteSpace(item.DefinitionId))
            {
                return item.DefinitionId;
            }

            if (!string.IsNullOrWhiteSpace(item.DisplayName))
            {
                return item.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(item.InstanceId))
            {
                return item.InstanceId;
            }

            return string.IsNullOrWhiteSpace(item.Id) ? "item" : item.Id;
        }

        private static bool IsLegacyGeneratedIcon(Sprite sprite)
        {
            return sprite != null &&
                   !string.IsNullOrWhiteSpace(sprite.name) &&
                   sprite.name.Contains("|") &&
                   !sprite.name.StartsWith(GeneratedIconVersion);
        }
    }
}
