using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [Serializable]
    internal sealed class CampusRuntimeObjectCatalogData
    {
        public List<CampusRuntimeObjectCatalogEntry> Objects = new List<CampusRuntimeObjectCatalogEntry>();
    }

    [Serializable]
    internal sealed class CampusRuntimeObjectCatalogEntry
    {
        public string ObjectId;
        public string SourceObjectId;
        public string ImagePath;
        public string GeneratedVisualKind;
        public CampusLocalizedText DisplayName;
        public CampusRuntimeObjectSettings Settings = new CampusRuntimeObjectSettings();
        public List<string> LegacyObjectIds = new List<string>();
    }

    internal sealed class CampusRuntimeObjectCatalog
    {
        public static readonly CampusRuntimeObjectCatalog Empty =
            new CampusRuntimeObjectCatalog(string.Empty, new CampusRuntimeObjectCatalogData());

        private readonly Dictionary<string, CampusRuntimeObjectCatalogEntry> entriesById =
            new Dictionary<string, CampusRuntimeObjectCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> aliasesToObjectId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string importRoot;

        private CampusRuntimeObjectCatalog(string importRoot, CampusRuntimeObjectCatalogData data)
        {
            this.importRoot = importRoot ?? string.Empty;
            Data = data ?? new CampusRuntimeObjectCatalogData();
            Data.Objects = Data.Objects ?? new List<CampusRuntimeObjectCatalogEntry>();
            for (int i = 0; i < Data.Objects.Count; i++)
            {
                NormalizeEntry(Data.Objects[i]);
                RegisterEntry(Data.Objects[i]);
            }
        }

        public CampusRuntimeObjectCatalogData Data { get; }

        public IReadOnlyList<CampusRuntimeObjectCatalogEntry> Objects => Data.Objects;

        public static CampusRuntimeObjectCatalog Load(string importRoot, Action<string> logWarning)
        {
            string path = CampusRuntimeImportLibrary.GetObjectCatalogPath(importRoot);
            if (!File.Exists(path))
            {
                return new CampusRuntimeObjectCatalog(importRoot, new CampusRuntimeObjectCatalogData());
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
                CampusRuntimeObjectCatalogData data =
                    JsonUtility.FromJson<CampusRuntimeObjectCatalogData>(json);
                return new CampusRuntimeObjectCatalog(importRoot, data);
            }
            catch (Exception exception)
            {
                logWarning?.Invoke("Failed to load runtime object catalog '" + path + "': " + exception.Message);
                return new CampusRuntimeObjectCatalog(importRoot, new CampusRuntimeObjectCatalogData());
            }
        }

        public bool ContainsObjectId(string objectId)
        {
            return !string.IsNullOrWhiteSpace(NormalizeObjectId(objectId));
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
                : entriesById.ContainsKey(normalized)
                    ? normalized
                    : string.Empty;
        }

        public string ResolveTypeId(string objectId, string fallbackTypeId)
        {
            return TryGetEntry(objectId, out CampusRuntimeObjectCatalogEntry entry) &&
                   entry.Settings != null &&
                   !string.IsNullOrWhiteSpace(entry.Settings.TypeId)
                ? entry.Settings.TypeId.Trim()
                : NormalizeRawId(fallbackTypeId);
        }

        public CampusLocalizedText ResolveDisplayName(string objectId, CampusLocalizedText fallback)
        {
            return TryGetEntry(objectId, out CampusRuntimeObjectCatalogEntry entry) &&
                   entry.DisplayName.HasAnyText
                ? entry.DisplayName
                : fallback;
        }

        public string ResolveDisplayNameText(string objectId, string fallback)
        {
            return TryGetEntry(objectId, out CampusRuntimeObjectCatalogEntry entry) &&
                   entry.DisplayName.HasAnyText
                ? entry.DisplayName.ResolvePrimary(fallback)
                : NormalizeRawId(fallback);
        }

        public bool TryGetSettings(string objectId, out CampusRuntimeObjectSettings settings)
        {
            settings = null;
            if (!TryGetEntry(objectId, out CampusRuntimeObjectCatalogEntry entry) || entry.Settings == null)
            {
                return false;
            }

            settings = CloneSettings(entry.Settings);
            CampusRuntimeObjectSettingsStore.Normalize(settings, importRoot, entry.ObjectId);
            return true;
        }

        public void SaveSettings(CampusRuntimeObjectSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.ObjectId))
            {
                return;
            }

            string objectId = NormalizeObjectId(settings.ObjectId);
            if (string.IsNullOrWhiteSpace(objectId))
            {
                objectId = settings.ObjectId.Trim();
            }

            CampusRuntimeObjectCatalogEntry entry = GetOrCreateEntry(objectId);
            settings.ObjectId = objectId;
            CampusRuntimeObjectSettingsStore.Normalize(settings, importRoot, objectId);
            entry.Settings = CloneSettings(settings);
            entry.ObjectId = objectId;
            entry.SourceObjectId = string.IsNullOrWhiteSpace(entry.SourceObjectId) ? objectId : entry.SourceObjectId.Trim();
            if (!entry.DisplayName.HasAnyText && settings.LocalizedDisplayNameOverride.HasAnyText)
            {
                entry.DisplayName = settings.LocalizedDisplayNameOverride;
            }

            Save();
        }

        public bool TryRemove(string objectId)
        {
            string canonicalId = NormalizeObjectId(objectId);
            if (string.IsNullOrWhiteSpace(canonicalId))
            {
                return false;
            }

            for (int i = Data.Objects.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Data.Objects[i].ObjectId, canonicalId, StringComparison.OrdinalIgnoreCase))
                {
                    Data.Objects.RemoveAt(i);
                    RebuildIndexes();
                    Save();
                    return true;
                }
            }

            return false;
        }

        public void AddOrUpdateImageObject(
            string objectId,
            string imagePath,
            CampusLocalizedText displayName,
            CampusRuntimeObjectSettings settings)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return;
            }

            objectId = objectId.Trim();
            CampusRuntimeObjectCatalogEntry entry = GetOrCreateEntry(objectId);
            entry.SourceObjectId = string.IsNullOrWhiteSpace(entry.SourceObjectId) ? objectId : entry.SourceObjectId.Trim();
            entry.ImagePath = CampusRuntimeImportLibrary.NormalizeSerializedPath(imagePath, importRoot);
            entry.GeneratedVisualKind = string.Empty;
            entry.DisplayName = displayName.HasAnyText ? displayName : new CampusLocalizedText(objectId, objectId);
            entry.Settings = settings ?? new CampusRuntimeObjectSettings();
            entry.Settings.ObjectId = objectId;
            CampusRuntimeObjectSettingsStore.Normalize(entry.Settings, importRoot, objectId);
            RebuildIndexes();
            Save();
        }

        public bool EnsureSourceObject(
            string objectId,
            string sourceObjectId,
            CampusLocalizedText displayName,
            CampusRuntimeObjectSettings settings,
            IEnumerable<string> legacyObjectIds)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            objectId = objectId.Trim();
            sourceObjectId = string.IsNullOrWhiteSpace(sourceObjectId) ? objectId : sourceObjectId.Trim();
            string canonicalId = NormalizeObjectId(objectId);
            CampusRuntimeObjectCatalogEntry entry = string.IsNullOrWhiteSpace(canonicalId)
                ? GetOrCreateEntry(objectId)
                : GetOrCreateEntry(canonicalId);

            bool changed = false;
            if (string.IsNullOrWhiteSpace(entry.SourceObjectId))
            {
                entry.SourceObjectId = sourceObjectId;
                changed = true;
            }

            if (!entry.DisplayName.HasAnyText && displayName.HasAnyText)
            {
                entry.DisplayName = displayName;
                changed = true;
            }

            if (entry.Settings == null || string.IsNullOrWhiteSpace(entry.Settings.ObjectId))
            {
                entry.Settings = CloneSettings(settings) ?? new CampusRuntimeObjectSettings();
                entry.Settings.ObjectId = entry.ObjectId;
                CampusRuntimeObjectSettingsStore.Normalize(entry.Settings, importRoot, entry.ObjectId);
                changed = true;
            }

            if (legacyObjectIds != null)
            {
                entry.LegacyObjectIds = entry.LegacyObjectIds ?? new List<string>();
                foreach (string legacyObjectId in legacyObjectIds)
                {
                    string alias = NormalizeRawId(legacyObjectId);
                    if (string.IsNullOrEmpty(alias) || ContainsAlias(entry, alias))
                    {
                        continue;
                    }

                    entry.LegacyObjectIds.Add(alias);
                    changed = true;
                }
            }

            if (changed)
            {
                RebuildIndexes();
                Save();
            }

            return changed;
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(importRoot))
            {
                return;
            }

            string path = CampusRuntimeImportLibrary.GetObjectCatalogPath(importRoot);
            Directory.CreateDirectory(importRoot);
            File.WriteAllText(path, JsonUtility.ToJson(Data, true), Encoding.UTF8);
        }

        public bool TryGetEntry(string objectId, out CampusRuntimeObjectCatalogEntry entry)
        {
            entry = null;
            string canonicalId = NormalizeRawId(objectId);
            if (string.IsNullOrEmpty(canonicalId))
            {
                return false;
            }

            if (aliasesToObjectId.TryGetValue(canonicalId, out string resolvedId))
            {
                canonicalId = resolvedId;
            }

            return entriesById.TryGetValue(canonicalId, out entry);
        }

        public bool ObjectIdsMatch(string left, string right)
        {
            string normalizedLeft = NormalizeObjectId(left);
            string normalizedRight = NormalizeObjectId(right);
            return !string.IsNullOrWhiteSpace(normalizedLeft) &&
                   !string.IsNullOrWhiteSpace(normalizedRight) &&
                   string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
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

        private CampusRuntimeObjectCatalogEntry GetOrCreateEntry(string objectId)
        {
            if (TryGetEntry(objectId, out CampusRuntimeObjectCatalogEntry entry))
            {
                return entry;
            }

            entry = new CampusRuntimeObjectCatalogEntry
            {
                ObjectId = objectId,
                SourceObjectId = objectId,
                DisplayName = new CampusLocalizedText(objectId, objectId),
                Settings = new CampusRuntimeObjectSettings { ObjectId = objectId }
            };
            Data.Objects.Add(entry);
            RegisterEntry(entry);
            return entry;
        }

        private void RebuildIndexes()
        {
            entriesById.Clear();
            aliasesToObjectId.Clear();
            for (int i = 0; i < Data.Objects.Count; i++)
            {
                NormalizeEntry(Data.Objects[i]);
                RegisterEntry(Data.Objects[i]);
            }
        }

        private void RegisterEntry(CampusRuntimeObjectCatalogEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ObjectId))
            {
                return;
            }

            entriesById[entry.ObjectId] = entry;
            AddAlias(entry.ObjectId, entry.ObjectId);
            AddAlias(entry.SourceObjectId, entry.ObjectId);
            if (entry.LegacyObjectIds == null)
            {
                return;
            }

            for (int i = 0; i < entry.LegacyObjectIds.Count; i++)
            {
                AddAlias(entry.LegacyObjectIds[i], entry.ObjectId);
            }
        }

        private void NormalizeEntry(CampusRuntimeObjectCatalogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.ObjectId = NormalizeRawId(entry.ObjectId);
            entry.SourceObjectId = NormalizeRawId(entry.SourceObjectId);
            entry.ImagePath = CampusRuntimeImportLibrary.NormalizeSerializedPath(entry.ImagePath, importRoot);
            entry.GeneratedVisualKind = NormalizeRawId(entry.GeneratedVisualKind);
            entry.Settings = entry.Settings ?? new CampusRuntimeObjectSettings();
            if (string.IsNullOrWhiteSpace(entry.Settings.ObjectId))
            {
                entry.Settings.ObjectId = entry.ObjectId;
            }

            CampusRuntimeObjectSettingsStore.Normalize(entry.Settings, importRoot, entry.ObjectId);
            entry.LegacyObjectIds = entry.LegacyObjectIds ?? new List<string>();
        }

        private void AddAlias(string alias, string objectId)
        {
            alias = NormalizeRawId(alias);
            if (!string.IsNullOrEmpty(alias))
            {
                aliasesToObjectId[alias] = objectId;
            }
        }

        private static CampusRuntimeObjectSettings CloneSettings(CampusRuntimeObjectSettings source)
        {
            return source == null
                ? null
                : JsonUtility.FromJson<CampusRuntimeObjectSettings>(JsonUtility.ToJson(source));
        }

        private static bool ContainsAlias(CampusRuntimeObjectCatalogEntry entry, string alias)
        {
            if (entry == null || string.IsNullOrWhiteSpace(alias))
            {
                return true;
            }

            if (string.Equals(entry.ObjectId, alias, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.SourceObjectId, alias, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (entry.LegacyObjectIds == null)
            {
                return false;
            }

            for (int i = 0; i < entry.LegacyObjectIds.Count; i++)
            {
                if (string.Equals(entry.LegacyObjectIds[i], alias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeRawId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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
