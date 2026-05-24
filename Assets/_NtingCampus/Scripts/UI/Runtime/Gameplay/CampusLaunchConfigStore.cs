using NtingCampusMapEditor;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public static class CampusLaunchConfigStore
    {
        public static bool HasPendingSelection { get; private set; }
        public static bool StartWithBlankMap { get; private set; }
        public static string PendingBlankMapName { get; private set; } = string.Empty;

        public static string SelectedMapPath { get; private set; } = string.Empty;
        public static CampusRuntimeMapLoadSource SelectedMapSource { get; private set; } = CampusRuntimeMapLoadSource.Scene;
        public static string SelectedSavePath { get; private set; } = string.Empty;
        public static CampusRuntimeMapLoadSource SelectedSaveSource { get; private set; } = CampusRuntimeMapLoadSource.Scene;

        public static void SetPendingSelection(
            string mapPath,
            CampusRuntimeMapLoadSource mapSource,
            string savePath,
            CampusRuntimeMapLoadSource saveSource,
            bool startWithBlankMap = false,
            string pendingBlankMapName = "")
        {
            SelectedMapPath = mapPath ?? string.Empty;
            SelectedMapSource = mapSource;
            SelectedSavePath = savePath ?? string.Empty;
            SelectedSaveSource = saveSource;
            StartWithBlankMap = startWithBlankMap;
            PendingBlankMapName = pendingBlankMapName ?? string.Empty;
            HasPendingSelection = true;
        }

        public static void Clear()
        {
            SelectedMapPath = string.Empty;
            SelectedMapSource = CampusRuntimeMapLoadSource.Scene;
            SelectedSavePath = string.Empty;
            SelectedSaveSource = CampusRuntimeMapLoadSource.Scene;
            StartWithBlankMap = false;
            PendingBlankMapName = string.Empty;
            HasPendingSelection = false;
        }
    }
}

