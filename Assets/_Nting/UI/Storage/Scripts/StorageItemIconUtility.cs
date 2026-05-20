using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    public static class StorageItemIconUtility
    {
        private const string ResourceRoot = "StorageIcons/";
        private const string GenericIconName = "icon_item";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Resolve(string definitionId, Sprite configuredIcon = null)
        {
            if (configuredIcon != null)
            {
                return configuredIcon;
            }

            string iconName = ResolveIconName(definitionId);
            Sprite icon = Load(iconName);
            return icon != null || iconName == GenericIconName ? icon : Load(GenericIconName);
        }

        private static Sprite Load(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return null;
            }

            if (Cache.TryGetValue(iconName, out Sprite cached))
            {
                return cached;
            }

            Sprite icon = Resources.Load<Sprite>(ResourceRoot + iconName);
            Cache[iconName] = icon;
            return icon;
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

        private static string NormalizeDefinitionId(string definitionId)
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
    }
}
