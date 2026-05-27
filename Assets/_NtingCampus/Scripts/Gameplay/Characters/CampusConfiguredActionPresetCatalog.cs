using System;
using System.Collections.Generic;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusConfiguredActionPresetCatalog
    {
        private const string PresetFileName = "ConfiguredActionPresets.json";

        private static Dictionary<string, CampusConfiguredActionPayload> payloadsByActionId;

        public static bool TryResolvePayload(
            string actionId,
            out CampusConfiguredActionPayload payload)
        {
            EnsureLoaded();
            return payloadsByActionId.TryGetValue(NormalizeId(actionId), out payload) &&
                   payload != null &&
                   !string.IsNullOrWhiteSpace(payload.Mode);
        }

        private static void EnsureLoaded()
        {
            if (payloadsByActionId != null)
            {
                return;
            }

            payloadsByActionId = LoadPayloads();
        }

        private static Dictionary<string, CampusConfiguredActionPayload> LoadPayloads()
        {
            Dictionary<string, CampusConfiguredActionPayload> payloads =
                new Dictionary<string, CampusConfiguredActionPayload>(StringComparer.OrdinalIgnoreCase);
            if (!CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                return payloads;
            }

            try
            {
                ConfiguredActionPresetFile file = JsonUtility.FromJson<ConfiguredActionPresetFile>(json);
                if (file == null || file.Actions == null)
                {
                    return payloads;
                }

                for (int i = 0; i < file.Actions.Count; i++)
                {
                    ConfiguredActionPresetRecord action = file.Actions[i];
                    string actionId = NormalizeId(action != null ? action.ActionId : string.Empty);
                    CampusConfiguredActionPayload payload = action != null ? action.Payload : null;
                    if (!string.IsNullOrEmpty(actionId) &&
                        payload != null &&
                        !string.IsNullOrWhiteSpace(payload.Mode))
                    {
                        payload.Normalize();
                        payloads[actionId] = payload;
                    }
                }
            }
            catch (Exception exception)
            {
                CampusRuntimePresetLogTextCatalog.Warning(
                    CampusRuntimePresetLogTextId.FailedToReadPresetFile,
                    PresetFileName,
                    exception.Message);
            }

            return payloads;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        [Serializable]
        private sealed class ConfiguredActionPresetFile
        {
            public List<ConfiguredActionPresetRecord> Actions =
                new List<ConfiguredActionPresetRecord>();
        }

        [Serializable]
        private sealed class ConfiguredActionPresetRecord
        {
            public string ActionId = string.Empty;
            public CampusConfiguredActionPayload Payload = new CampusConfiguredActionPayload();
        }
    }
}
