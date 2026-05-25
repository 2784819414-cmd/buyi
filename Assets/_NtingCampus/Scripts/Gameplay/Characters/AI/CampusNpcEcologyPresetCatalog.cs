namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private const string PresetFileName = "NpcEcologyPresets.json";
        private const float TargetRetryHoldSeconds = 5f;

        private static EcologyPresetData cachedData;

        public static bool EnableSelectionDebug => Data.EnableSelectionDebug;

        private static EcologyPresetData Data
        {
            get
            {
                if (cachedData == null)
                {
                    cachedData = LoadData();
                }

                return cachedData;
            }
        }
    }
}
