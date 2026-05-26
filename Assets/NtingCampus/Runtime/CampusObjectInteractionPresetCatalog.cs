using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [Serializable]
    internal sealed class CampusObjectInteractionPresetCatalogData
    {
        public List<CampusObjectInteractionPreset> Presets = new List<CampusObjectInteractionPreset>();
    }

    [Serializable]
    internal sealed class CampusObjectInteractionPreset
    {
        public string Eid;
        public List<string> ObjectIds = new List<string>();
        public List<CampusPlacedObjectInteractionAnchor> Anchors = new List<CampusPlacedObjectInteractionAnchor>();
    }

    internal sealed class CampusObjectInteractionPresetCatalog
    {
        private const string PresetFileName = "ObjectInteractionPresets.json";

        public static readonly CampusObjectInteractionPresetCatalog Empty =
            new CampusObjectInteractionPresetCatalog(new CampusObjectInteractionPresetCatalogData());

        private static CampusObjectInteractionPresetCatalog cached;

        private readonly Dictionary<string, CampusObjectInteractionPreset> presetsByEid =
            new Dictionary<string, CampusObjectInteractionPreset>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> objectIdsToPresetEid =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private CampusObjectInteractionPresetCatalog(CampusObjectInteractionPresetCatalogData data)
        {
            if (data == null || data.Presets == null)
            {
                return;
            }

            for (int i = 0; i < data.Presets.Count; i++)
            {
                AddPreset(data.Presets[i]);
            }
        }

        public static CampusObjectInteractionPresetCatalog Current
        {
            get
            {
                if (cached == null)
                {
                    cached = Load();
                }

                return cached;
            }
        }

        public static void ClearCache()
        {
            cached = null;
        }

        public bool TryResolvePreset(CampusPlacedObject placedObject, out CampusObjectInteractionPreset preset)
        {
            preset = null;
            if (placedObject == null)
            {
                return false;
            }

            if (TryGetPreset(placedObject.InteractionPresetEid, out preset))
            {
                return true;
            }

            string objectId = NormalizeId(placedObject.ObjectId);
            if (!string.IsNullOrEmpty(objectId) &&
                objectIdsToPresetEid.TryGetValue(objectId, out string mappedEid) &&
                TryGetPreset(mappedEid, out preset))
            {
                return true;
            }

            string typeId = NormalizeId(placedObject.TypeId);
            return !string.IsNullOrEmpty(typeId) &&
                   objectIdsToPresetEid.TryGetValue(typeId, out mappedEid) &&
                   TryGetPreset(mappedEid, out preset);
        }

        public bool TryGetPreset(string eid, out CampusObjectInteractionPreset preset)
        {
            preset = null;
            string normalized = NormalizeId(eid);
            return !string.IsNullOrEmpty(normalized) &&
                   presetsByEid.TryGetValue(normalized, out preset);
        }

        public static List<CampusPlacedObjectInteractionAnchor> ClonePresetAnchors(CampusObjectInteractionPreset preset)
        {
            return preset != null
                ? CampusPlacedObject.CloneInteractionAnchors(preset.Anchors)
                : new List<CampusPlacedObjectInteractionAnchor>();
        }

        private static CampusObjectInteractionPresetCatalog Load()
        {
            if (!CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                return Empty;
            }

            try
            {
                CampusObjectInteractionPresetCatalogData data =
                    JsonUtility.FromJson<CampusObjectInteractionPresetCatalogData>(json.TrimStart('\uFEFF'));
                return new CampusObjectInteractionPresetCatalog(data);
            }
            catch (Exception exception)
            {
                CampusRuntimePresetLogTextCatalog.Warning(
                    CampusRuntimePresetLogTextId.FailedToReadPresetFile,
                    PresetFileName,
                    exception.Message);
                return Empty;
            }
        }

        private void AddPreset(CampusObjectInteractionPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            preset.Eid = NormalizeId(preset.Eid);
            if (string.IsNullOrEmpty(preset.Eid))
            {
                return;
            }

            preset.Anchors = CampusPlacedObject.CloneInteractionAnchors(preset.Anchors);
            presetsByEid[preset.Eid] = preset;

            if (preset.ObjectIds == null)
            {
                return;
            }

            for (int i = 0; i < preset.ObjectIds.Count; i++)
            {
                string objectId = NormalizeId(preset.ObjectIds[i]);
                if (!string.IsNullOrEmpty(objectId))
                {
                    objectIdsToPresetEid[objectId] = preset.Eid;
                }
            }
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
