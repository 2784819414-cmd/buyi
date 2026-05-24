using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public sealed class CampusRuntimeMapEditorShellReadModel
    {
        public CampusRuntimeMapEditorShellReadModel(
            CampusRuntimeEditorTab activeTab,
            string title,
            IReadOnlyList<CampusRuntimeMapEditorTabButtonReadModel> tabs)
        {
            ActiveTab = activeTab;
            Title = title ?? string.Empty;
            Tabs = tabs ?? Array.Empty<CampusRuntimeMapEditorTabButtonReadModel>();
        }

        public CampusRuntimeEditorTab ActiveTab { get; }
        public string Title { get; }
        public IReadOnlyList<CampusRuntimeMapEditorTabButtonReadModel> Tabs { get; }
    }

    public sealed class CampusRuntimeMapEditorTabButtonReadModel
    {
        public CampusRuntimeMapEditorTabButtonReadModel(CampusRuntimeEditorTab tab, string label, bool isSelected)
        {
            Tab = tab;
            Label = label ?? string.Empty;
            IsSelected = isSelected;
        }

        public CampusRuntimeEditorTab Tab { get; }
        public string Label { get; }
        public bool IsSelected { get; }
    }

    public sealed class CampusRuntimeMapEditorFloorPanelReadModel
    {
        public CampusRuntimeMapEditorFloorPanelReadModel(
            string title,
            string addLabel,
            string lockLabel,
            string deleteLabel,
            IReadOnlyList<CampusRuntimeMapEditorFloorRowReadModel> rows)
        {
            Title = title ?? string.Empty;
            AddLabel = addLabel ?? string.Empty;
            LockLabel = lockLabel ?? string.Empty;
            DeleteLabel = deleteLabel ?? string.Empty;
            Rows = rows ?? Array.Empty<CampusRuntimeMapEditorFloorRowReadModel>();
        }

        public string Title { get; }
        public string AddLabel { get; }
        public string LockLabel { get; }
        public string DeleteLabel { get; }
        public IReadOnlyList<CampusRuntimeMapEditorFloorRowReadModel> Rows { get; }
    }

    public sealed class CampusRuntimeMapEditorFloorRowReadModel
    {
        public CampusRuntimeMapEditorFloorRowReadModel(int floorIndex, string label, bool isSelected)
        {
            FloorIndex = floorIndex;
            Label = label ?? string.Empty;
            IsSelected = isSelected;
        }

        public int FloorIndex { get; }
        public string Label { get; }
        public bool IsSelected { get; }
    }

    public sealed class CampusRuntimeMapEditorChecklistReadModel
    {
        public CampusRuntimeMapEditorChecklistReadModel(
            string title,
            string emptyMessage,
            IReadOnlyList<CampusRuntimeMapEditorChecklistEntryReadModel> entries)
        {
            Title = title ?? string.Empty;
            EmptyMessage = emptyMessage ?? string.Empty;
            Entries = entries ?? Array.Empty<CampusRuntimeMapEditorChecklistEntryReadModel>();
        }

        public string Title { get; }
        public string EmptyMessage { get; }
        public IReadOnlyList<CampusRuntimeMapEditorChecklistEntryReadModel> Entries { get; }
    }

    public sealed class CampusRuntimeMapEditorChecklistEntryReadModel
    {
        public CampusRuntimeMapEditorChecklistEntryReadModel(string label, string valueText, bool isSatisfied, Color swatchColor)
        {
            Label = label ?? string.Empty;
            ValueText = valueText ?? string.Empty;
            IsSatisfied = isSatisfied;
            SwatchColor = swatchColor;
        }

        public string Label { get; }
        public string ValueText { get; }
        public bool IsSatisfied { get; }
        public Color SwatchColor { get; }
    }

    public sealed class CampusRuntimeMapEditorToolbarReadModel
    {
        public CampusRuntimeMapEditorToolbarReadModel(
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
            CloseLabel = closeLabel ?? string.Empty;
            HelpLabel = helpLabel ?? string.Empty;
            ImportLabel = importLabel ?? string.Empty;
            ExportLabel = exportLabel ?? string.Empty;
            UndoLabel = undoLabel ?? string.Empty;
            RedoLabel = redoLabel ?? string.Empty;
            GridOnLabel = gridOnLabel ?? string.Empty;
            GridOffLabel = gridOffLabel ?? string.Empty;
            SettingsLabel = settingsLabel ?? string.Empty;
            RebuildLabel = rebuildLabel ?? string.Empty;
            IsGridVisible = isGridVisible;
            IsSettingsVisible = isSettingsVisible;
            CanUndo = canUndo;
            CanRedo = canRedo;
        }

        public string CloseLabel { get; }
        public string HelpLabel { get; }
        public string ImportLabel { get; }
        public string ExportLabel { get; }
        public string UndoLabel { get; }
        public string RedoLabel { get; }
        public string GridOnLabel { get; }
        public string GridOffLabel { get; }
        public string SettingsLabel { get; }
        public string RebuildLabel { get; }
        public bool IsGridVisible { get; }
        public bool IsSettingsVisible { get; }
        public bool CanUndo { get; }
        public bool CanRedo { get; }
    }

    public sealed class CampusRuntimeMapEditorHelpReadModel
    {
        public CampusRuntimeMapEditorHelpReadModel(bool isVisible, string title, string body)
        {
            IsVisible = isVisible;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public bool IsVisible { get; }
        public string Title { get; }
        public string Body { get; }
    }

    public sealed class CampusRuntimeMapEditorSettingsReadModel
    {
        public CampusRuntimeMapEditorSettingsReadModel(
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
            IsVisible = isVisible;
            Title = title ?? string.Empty;
            MapSourceText = mapSourceText ?? string.Empty;
            AutoSaveLabel = autoSaveLabel ?? string.Empty;
            AutoLoadLabel = autoLoadLabel ?? string.Empty;
            SavePlayerMapLabel = savePlayerMapLabel ?? string.Empty;
            LoadPlayerMapLabel = loadPlayerMapLabel ?? string.Empty;
            ExportAuthoringLabel = exportAuthoringLabel ?? string.Empty;
            RestoreAuthoringLabel = restoreAuthoringLabel ?? string.Empty;
            AutoSaveEnabled = autoSaveEnabled;
            AutoLoadEnabled = autoLoadEnabled;
        }

        public bool IsVisible { get; }
        public string Title { get; }
        public string MapSourceText { get; }
        public string AutoSaveLabel { get; }
        public string AutoLoadLabel { get; }
        public string SavePlayerMapLabel { get; }
        public string LoadPlayerMapLabel { get; }
        public string ExportAuthoringLabel { get; }
        public string RestoreAuthoringLabel { get; }
        public bool AutoSaveEnabled { get; }
        public bool AutoLoadEnabled { get; }
    }

    public sealed class CampusRuntimeMapEditorObjectSettingsLauncherReadModel
    {
        public CampusRuntimeMapEditorObjectSettingsLauncherReadModel(
            string buttonLabel,
            string selectedObjectName,
            bool hasSelection,
            bool isOpen)
        {
            ButtonLabel = buttonLabel ?? string.Empty;
            SelectedObjectName = selectedObjectName ?? string.Empty;
            HasSelection = hasSelection;
            IsOpen = isOpen;
        }

        public string ButtonLabel { get; }
        public string SelectedObjectName { get; }
        public bool HasSelection { get; }
        public bool IsOpen { get; }
    }

    public sealed class CampusRuntimeMapEditorObjectSettingsPanelReadModel
    {
        public CampusRuntimeMapEditorObjectSettingsPanelReadModel(
            bool isVisible,
            string title,
            string warningMessage,
            string saveAndSyncLabel,
            string applyToAllLabel,
            string syncHintLabel,
            bool canEdit)
        {
            IsVisible = isVisible;
            Title = title ?? string.Empty;
            WarningMessage = warningMessage ?? string.Empty;
            SaveAndSyncLabel = saveAndSyncLabel ?? string.Empty;
            ApplyToAllLabel = applyToAllLabel ?? string.Empty;
            SyncHintLabel = syncHintLabel ?? string.Empty;
            CanEdit = canEdit;
        }

        public bool IsVisible { get; }
        public string Title { get; }
        public string WarningMessage { get; }
        public string SaveAndSyncLabel { get; }
        public string ApplyToAllLabel { get; }
        public string SyncHintLabel { get; }
        public bool CanEdit { get; }
    }

    public sealed class CampusRuntimeMapEditorStatusReadModel
    {
        public CampusRuntimeMapEditorStatusReadModel(bool shouldShow, string message)
        {
            ShouldShow = shouldShow;
            Message = message ?? string.Empty;
        }

        public bool ShouldShow { get; }
        public string Message { get; }
    }
}
