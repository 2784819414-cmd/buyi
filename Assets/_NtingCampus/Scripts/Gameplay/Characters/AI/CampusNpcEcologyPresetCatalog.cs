using System;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private const string PresetFileName = "NpcEcologyPresets.json";
        private const float TargetRetryHoldSeconds = 5f;
        private const float PresetChangeCheckSeconds = 0.75f;

        private static EcologyPresetData cachedData;
        private static DateTime cachedPresetWriteUtc = DateTime.MinValue;
        private static float nextPresetChangeCheckTime;

        public static bool EnableSelectionDebug => Data.EnableSelectionDebug;

        private static EcologyPresetData Data
        {
            get
            {
                if (ShouldReloadData())
                {
                    cachedData = LoadData();
                    cachedPresetWriteUtc = ResolvePresetLastWriteUtc();
                }

                return cachedData;
            }
        }

        private static bool ShouldReloadData()
        {
            if (cachedData == null)
            {
                return true;
            }

            if (Application.isPlaying && Time.realtimeSinceStartup < nextPresetChangeCheckTime)
            {
                return false;
            }

            nextPresetChangeCheckTime = Application.isPlaying
                ? Time.realtimeSinceStartup + PresetChangeCheckSeconds
                : 0f;
            return ResolvePresetLastWriteUtc() != cachedPresetWriteUtc;
        }

        private static DateTime ResolvePresetLastWriteUtc()
        {
            return CampusRuntimeModPresetStore.TryGetPresetLastWriteUtc(PresetFileName, out DateTime lastWriteUtc)
                ? lastWriteUtc
                : DateTime.MinValue;
        }
    }
}
