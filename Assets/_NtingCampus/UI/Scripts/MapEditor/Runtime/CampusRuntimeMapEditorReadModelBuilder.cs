using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public static class CampusRuntimeMapEditorReadModelBuilder
    {
        public static CampusRuntimeMapEditorShellReadModel BuildShell(
            CampusRuntimeEditorTab activeTab,
            string buildLabel,
            string roomsLabel,
            string gameplayLabel,
            string objectsLabel,
            string lightingLabel)
        {
            CampusRuntimeMapEditorTabButtonReadModel[] tabs =
            {
                new CampusRuntimeMapEditorTabButtonReadModel(CampusRuntimeEditorTab.Build, buildLabel, activeTab == CampusRuntimeEditorTab.Build),
                new CampusRuntimeMapEditorTabButtonReadModel(CampusRuntimeEditorTab.Rooms, roomsLabel, activeTab == CampusRuntimeEditorTab.Rooms),
                new CampusRuntimeMapEditorTabButtonReadModel(CampusRuntimeEditorTab.Gameplay, gameplayLabel, activeTab == CampusRuntimeEditorTab.Gameplay),
                new CampusRuntimeMapEditorTabButtonReadModel(CampusRuntimeEditorTab.Objects, objectsLabel, activeTab == CampusRuntimeEditorTab.Objects),
                new CampusRuntimeMapEditorTabButtonReadModel(CampusRuntimeEditorTab.Lighting, lightingLabel, activeTab == CampusRuntimeEditorTab.Lighting)
            };

            return new CampusRuntimeMapEditorShellReadModel(activeTab, ResolveShellTitle(activeTab, tabs), tabs);
        }

        public static CampusRuntimeMapEditorFloorPanelReadModel BuildFloorPanel(
            string title,
            string addLabel,
            string lockLabel,
            string deleteLabel,
            int selectedFloorIndex,
            IReadOnlyList<CampusFloorRoot> floors,
            Func<CampusFloorRoot, string> labelSelector)
        {
            List<CampusRuntimeMapEditorFloorRowReadModel> rows = new List<CampusRuntimeMapEditorFloorRowReadModel>();
            if (floors != null)
            {
                for (int i = floors.Count - 1; i >= 0; i--)
                {
                    CampusFloorRoot floor = floors[i];
                    if (floor == null)
                    {
                        continue;
                    }

                    string label = labelSelector != null ? labelSelector(floor) : floor.FloorIndex.ToString();
                    rows.Add(new CampusRuntimeMapEditorFloorRowReadModel(
                        floor.FloorIndex,
                        label,
                        selectedFloorIndex == floor.FloorIndex));
                }
            }

            return new CampusRuntimeMapEditorFloorPanelReadModel(title, addLabel, lockLabel, deleteLabel, rows);
        }

        public static CampusRuntimeMapEditorChecklistReadModel BuildChecklist(
            string title,
            string emptyMessage,
            IReadOnlyList<string> roomNames,
            IReadOnlyList<int> roomRequiredCounts,
            Func<string, string> labelResolver,
            Func<string, int> countResolver,
            Func<string, Color> swatchResolver)
        {
            List<CampusRuntimeMapEditorChecklistEntryReadModel> entries = new List<CampusRuntimeMapEditorChecklistEntryReadModel>();
            if (roomNames != null && roomRequiredCounts != null)
            {
                int count = Mathf.Min(roomNames.Count, roomRequiredCounts.Count);
                for (int i = 0; i < count; i++)
                {
                    string roomName = roomNames[i] ?? string.Empty;
                    int required = roomRequiredCounts[i];
                    int actual = countResolver != null ? countResolver(roomName) : 0;
                    string label = labelResolver != null ? labelResolver(roomName) : roomName;
                    Color swatchColor = swatchResolver != null ? swatchResolver(roomName) : Color.white;
                    entries.Add(new CampusRuntimeMapEditorChecklistEntryReadModel(
                        label,
                        actual.ToString(),
                        actual >= required,
                        swatchColor));
                }
            }

            return new CampusRuntimeMapEditorChecklistReadModel(title, emptyMessage, entries);
        }

        public static CampusRuntimeMapEditorToolbarReadModel BuildToolbar(
            string closeLabel,
            string helpLabel,
            string importLabel,
            string exportLabel,
            string undoLabel,
            string redoLabel,
            string gridOnLabel,
            string gridOffLabel,
            string settingsLabel,
            string rebuildLabel,
            bool isGridVisible,
            bool isSettingsVisible,
            bool canUndo,
            bool canRedo)
        {
            return new CampusRuntimeMapEditorToolbarReadModel(
                closeLabel,
                helpLabel,
                importLabel,
                exportLabel,
                undoLabel,
                redoLabel,
                gridOnLabel,
                gridOffLabel,
                settingsLabel,
                rebuildLabel,
                isGridVisible,
                isSettingsVisible,
                canUndo,
                canRedo);
        }

        public static CampusRuntimeMapEditorHelpReadModel BuildHelp(bool isVisible, string title, string body)
        {
            return new CampusRuntimeMapEditorHelpReadModel(isVisible, title, body);
        }

        public static CampusRuntimeMapEditorSettingsReadModel BuildSettings(
            bool isVisible,
            string title,
            string mapSourceText,
            string autoSaveLabel,
            string autoLoadLabel,
            string savePlayerMapLabel,
            string loadPlayerMapLabel,
            string exportAuthoringLabel,
            string restoreAuthoringLabel,
            bool autoSaveEnabled,
            bool autoLoadEnabled)
        {
            return new CampusRuntimeMapEditorSettingsReadModel(
                isVisible,
                title,
                mapSourceText,
                autoSaveLabel,
                autoLoadLabel,
                savePlayerMapLabel,
                loadPlayerMapLabel,
                exportAuthoringLabel,
                restoreAuthoringLabel,
                autoSaveEnabled,
                autoLoadEnabled);
        }

        public static CampusRuntimeMapEditorObjectSettingsLauncherReadModel BuildObjectSettingsLauncher(
            string buttonLabel,
            string selectedObjectName,
            bool hasSelection,
            bool isOpen)
        {
            return new CampusRuntimeMapEditorObjectSettingsLauncherReadModel(
                buttonLabel,
                selectedObjectName,
                hasSelection,
                isOpen);
        }

        public static CampusRuntimeMapEditorObjectSettingsPanelReadModel BuildObjectSettingsPanel(
            bool isVisible,
            string title,
            string warningMessage,
            string saveAndSyncLabel,
            string applyToAllLabel,
            string syncHintLabel,
            bool canEdit)
        {
            return new CampusRuntimeMapEditorObjectSettingsPanelReadModel(
                isVisible,
                title,
                warningMessage,
                saveAndSyncLabel,
                applyToAllLabel,
                syncHintLabel,
                canEdit);
        }

        public static CampusRuntimeMapEditorStatusReadModel BuildStatus(string message, float expiresAt, float currentTime)
        {
            bool shouldShow = !string.IsNullOrEmpty(message) && currentTime <= expiresAt;
            return new CampusRuntimeMapEditorStatusReadModel(shouldShow, shouldShow ? message : string.Empty);
        }

        private static string ResolveShellTitle(
            CampusRuntimeEditorTab activeTab,
            IReadOnlyList<CampusRuntimeMapEditorTabButtonReadModel> tabs)
        {
            if (tabs == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                CampusRuntimeMapEditorTabButtonReadModel tab = tabs[i];
                if (tab != null && tab.Tab == activeTab)
                {
                    return tab.Label;
                }
            }

            return string.Empty;
        }
    }
}
