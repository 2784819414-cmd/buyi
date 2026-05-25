using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [Serializable]
    internal sealed class CampusRuntimeObjectDefinitionCatalogData
    {
        public List<CampusRuntimeObjectDefinition> Objects = new List<CampusRuntimeObjectDefinition>();
    }

    [Serializable]
    internal sealed class CampusRuntimeObjectDefinition
    {
        public string ObjectId;
        public string SourceObjectId;
        public string TypeId;
        public CampusLocalizedText DisplayName;
        public List<string> LegacyObjectIds = new List<string>();
    }

    internal sealed class CampusRuntimeObjectDefinitionCatalog
    {
        public static readonly CampusRuntimeObjectDefinitionCatalog Empty =
            new CampusRuntimeObjectDefinitionCatalog(new CampusRuntimeObjectDefinitionCatalogData());

        private readonly Dictionary<string, CampusRuntimeObjectDefinition> definitionsById =
            new Dictionary<string, CampusRuntimeObjectDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> aliasesToObjectId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> sourceToObjectId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private CampusRuntimeObjectDefinitionCatalog(CampusRuntimeObjectDefinitionCatalogData data)
        {
            if (data == null || data.Objects == null)
            {
                return;
            }

            for (int i = 0; i < data.Objects.Count; i++)
            {
                AddDefinition(data.Objects[i]);
            }
        }

        public static CampusRuntimeObjectDefinitionCatalog Load(string importRoot, Action<string> logWarning)
        {
            string path = CampusRuntimeImportLibrary.GetObjectDefinitionsPath(importRoot);
            if (!File.Exists(path))
            {
                return Empty;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
                CampusRuntimeObjectDefinitionCatalogData data =
                    JsonUtility.FromJson<CampusRuntimeObjectDefinitionCatalogData>(json);
                return new CampusRuntimeObjectDefinitionCatalog(data);
            }
            catch (Exception exception)
            {
                if (logWarning != null)
                {
                    logWarning("Failed to load object definitions '" + path + "': " + exception.Message);
                }

                return Empty;
            }
        }

        public bool TryGetDefinition(string objectId, out CampusRuntimeObjectDefinition definition)
        {
            definition = null;
            string canonicalId = NormalizeObjectId(objectId);
            return !string.IsNullOrWhiteSpace(canonicalId) &&
                   definitionsById.TryGetValue(canonicalId, out definition);
        }

        public string ResolveObjectIdForSource(string sourceObjectId)
        {
            string source = NormalizeRawId(sourceObjectId);
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            return sourceToObjectId.TryGetValue(source, out string objectId)
                ? objectId
                : NormalizeObjectId(source);
        }

        public string NormalizeObjectId(string objectId)
        {
            string normalized = NormalizeRawId(objectId);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            return aliasesToObjectId.TryGetValue(normalized, out string canonicalId)
                ? canonicalId
                : normalized;
        }

        public string ResolveTypeId(string objectId, string fallbackTypeId)
        {
            return TryGetDefinition(objectId, out CampusRuntimeObjectDefinition definition) &&
                   !string.IsNullOrWhiteSpace(definition.TypeId)
                ? definition.TypeId.Trim()
                : NormalizeRawId(fallbackTypeId);
        }

        public CampusLocalizedText ResolveDisplayName(string objectId, CampusLocalizedText fallback)
        {
            return TryGetDefinition(objectId, out CampusRuntimeObjectDefinition definition) &&
                   definition.DisplayName.HasAnyText
                ? definition.DisplayName
                : fallback;
        }

        public string ResolveDisplayNameText(string objectId, string fallback)
        {
            return TryGetDefinition(objectId, out CampusRuntimeObjectDefinition definition) &&
                   definition.DisplayName.HasAnyText
                ? definition.DisplayName.ResolvePrimary(fallback)
                : NormalizeRawId(fallback);
        }

        public bool ObjectIdsMatch(string left, string right)
        {
            string normalizedLeft = NormalizeObjectId(left);
            string normalizedRight = NormalizeObjectId(right);
            return !string.IsNullOrWhiteSpace(normalizedLeft) &&
                   !string.IsNullOrWhiteSpace(normalizedRight) &&
                   string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        public List<string> GetSettingsLookupIds(string objectId)
        {
            List<string> ids = new List<string>();
            string canonicalId = NormalizeObjectId(objectId);
            AddUnique(ids, canonicalId);
            AddUnique(ids, objectId);

            if (definitionsById.TryGetValue(canonicalId, out CampusRuntimeObjectDefinition definition))
            {
                AddUnique(ids, definition.SourceObjectId);
                if (definition.LegacyObjectIds != null)
                {
                    for (int i = 0; i < definition.LegacyObjectIds.Count; i++)
                    {
                        AddUnique(ids, definition.LegacyObjectIds[i]);
                    }
                }
            }

            return ids;
        }

        public static string BuildStableObjectId(string displayName, Vector2Int footprint, Func<string, bool> exists)
        {
            string stem = BuildAsciiStem(displayName);
            if (string.IsNullOrWhiteSpace(stem))
            {
                stem = "custom_object";
            }

            string baseId = stem + "_" + Mathf.Max(1, footprint.x) + "x" + Mathf.Max(1, footprint.y);
            string candidate = baseId;
            int suffix = 1;
            while (exists != null && exists(candidate))
            {
                candidate = baseId + "_" + suffix;
                suffix++;
            }

            return candidate;
        }

        public static bool IsSafeObjectId(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            string trimmed = objectId.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool safe =
                    c >= 'A' && c <= 'Z' ||
                    c >= 'a' && c <= 'z' ||
                    c >= '0' && c <= '9' ||
                    c == '_' ||
                    c == '-' ||
                    c == '.';
                if (!safe)
                {
                    return false;
                }
            }

            return true;
        }

        private void AddDefinition(CampusRuntimeObjectDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            definition.ObjectId = NormalizeRawId(definition.ObjectId);
            if (string.IsNullOrEmpty(definition.ObjectId))
            {
                return;
            }

            definition.SourceObjectId = NormalizeRawId(definition.SourceObjectId);
            definition.TypeId = NormalizeRawId(definition.TypeId);
            definitionsById[definition.ObjectId] = definition;
            aliasesToObjectId[definition.ObjectId] = definition.ObjectId;

            AddAlias(definition.SourceObjectId, definition.ObjectId);
            if (!string.IsNullOrEmpty(definition.SourceObjectId))
            {
                sourceToObjectId[definition.SourceObjectId] = definition.ObjectId;
            }

            if (definition.LegacyObjectIds == null)
            {
                return;
            }

            for (int i = 0; i < definition.LegacyObjectIds.Count; i++)
            {
                AddAlias(definition.LegacyObjectIds[i], definition.ObjectId);
            }
        }

        private void AddAlias(string alias, string objectId)
        {
            alias = NormalizeRawId(alias);
            if (!string.IsNullOrEmpty(alias))
            {
                aliasesToObjectId[alias] = objectId;
            }
        }

        private static string NormalizeRawId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static void AddUnique(List<string> ids, string id)
        {
            id = NormalizeRawId(id);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            ids.Add(id);
        }

        private static string BuildAsciiStem(string displayName)
        {
            string value = NormalizeRawId(displayName).ToLowerInvariant();
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            bool lastWasSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isAsciiLetterOrDigit = c >= 'a' && c <= 'z' || c >= '0' && c <= '9';
                if (isAsciiLetterOrDigit)
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                    continue;
                }

                if ((c == '_' || c == '-' || c == ' ') && builder.Length > 0 && !lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            return builder.ToString().Trim('_');
        }
    }
}
