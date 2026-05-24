using UnityEngine;

namespace NtingCampusMapEditor
{
    public readonly struct CampusRuntimeMapEditorShellInteraction
    {
        public CampusRuntimeMapEditorShellInteraction(CampusRuntimeEditorTab? selectedTab, Rect contentRect)
        {
            SelectedTab = selectedTab;
            ContentRect = contentRect;
        }

        public CampusRuntimeEditorTab? SelectedTab { get; }
        public Rect ContentRect { get; }
    }

    public readonly struct CampusRuntimeMapEditorFloorPanelInteraction
    {
        public CampusRuntimeMapEditorFloorPanelInteraction(int? selectedFloorIndex, bool addRequested, bool toggleLockRequested, bool deleteRequested)
        {
            SelectedFloorIndex = selectedFloorIndex;
            AddRequested = addRequested;
            ToggleLockRequested = toggleLockRequested;
            DeleteRequested = deleteRequested;
        }

        public int? SelectedFloorIndex { get; }
        public bool AddRequested { get; }
        public bool ToggleLockRequested { get; }
        public bool DeleteRequested { get; }
    }

    public readonly struct CampusRuntimeMapEditorToolbarInteraction
    {
        public CampusRuntimeMapEditorToolbarInteraction(
            bool closeRequested,
            bool toggleHelpRequested,
            bool importRequested,
            bool exportRequested,
            bool undoRequested,
            bool redoRequested,
            bool toggleGridRequested,
            bool toggleSettingsRequested,
            bool rebuildRequested)
        {
            CloseRequested = closeRequested;
            ToggleHelpRequested = toggleHelpRequested;
            ImportRequested = importRequested;
            ExportRequested = exportRequested;
            UndoRequested = undoRequested;
            RedoRequested = redoRequested;
            ToggleGridRequested = toggleGridRequested;
            ToggleSettingsRequested = toggleSettingsRequested;
            RebuildRequested = rebuildRequested;
        }

        public bool CloseRequested { get; }
        public bool ToggleHelpRequested { get; }
        public bool ImportRequested { get; }
        public bool ExportRequested { get; }
        public bool UndoRequested { get; }
        public bool RedoRequested { get; }
        public bool ToggleGridRequested { get; }
        public bool ToggleSettingsRequested { get; }
        public bool RebuildRequested { get; }
    }

    public readonly struct CampusRuntimeMapEditorSettingsInteraction
    {
        public CampusRuntimeMapEditorSettingsInteraction(
            bool autoSaveEnabled,
            bool autoLoadEnabled,
            bool savePlayerMapRequested,
            bool loadPlayerMapRequested,
            bool exportAuthoringRequested,
            bool restoreAuthoringRequested)
        {
            AutoSaveEnabled = autoSaveEnabled;
            AutoLoadEnabled = autoLoadEnabled;
            SavePlayerMapRequested = savePlayerMapRequested;
            LoadPlayerMapRequested = loadPlayerMapRequested;
            ExportAuthoringRequested = exportAuthoringRequested;
            RestoreAuthoringRequested = restoreAuthoringRequested;
        }

        public bool AutoSaveEnabled { get; }
        public bool AutoLoadEnabled { get; }
        public bool SavePlayerMapRequested { get; }
        public bool LoadPlayerMapRequested { get; }
        public bool ExportAuthoringRequested { get; }
        public bool RestoreAuthoringRequested { get; }
    }

    public readonly struct CampusRuntimeMapEditorObjectSettingsLauncherInteraction
    {
        public CampusRuntimeMapEditorObjectSettingsLauncherInteraction(bool toggleRequested)
        {
            ToggleRequested = toggleRequested;
        }

        public bool ToggleRequested { get; }
    }

    public readonly struct CampusRuntimeMapEditorObjectSettingsPanelInteraction
    {
        public CampusRuntimeMapEditorObjectSettingsPanelInteraction(
            bool closeRequested,
            bool saveAndSyncRequested,
            bool applyToAllRequested,
            Rect scrollRect,
            float viewWidth)
        {
            CloseRequested = closeRequested;
            SaveAndSyncRequested = saveAndSyncRequested;
            ApplyToAllRequested = applyToAllRequested;
            ScrollRect = scrollRect;
            ViewWidth = viewWidth;
        }

        public bool CloseRequested { get; }
        public bool SaveAndSyncRequested { get; }
        public bool ApplyToAllRequested { get; }
        public Rect ScrollRect { get; }
        public float ViewWidth { get; }
    }

    public static class CampusRuntimeMapEditorChromePresenter
    {
        public static CampusRuntimeMapEditorShellInteraction DrawShell(
            Rect panelRect,
            GUIStyle panelStyle,
            GUIStyle headerStyle,
            GUIStyle selectedButtonStyle,
            GUIStyle iconButtonStyle,
            CampusRuntimeMapEditorShellReadModel model)
        {
            GUI.Box(panelRect, GUIContent.none, panelStyle);
            Rect tabRect = new Rect(panelRect.x + 14f, panelRect.y + 12f, panelRect.width - 28f, 54f);
            float tabGap = 8f;
            float tabWidth = (tabRect.width - tabGap * 4f) / 5f;
            CampusRuntimeEditorTab? selectedTab = null;

            for (int i = 0; i < model.Tabs.Count; i++)
            {
                CampusRuntimeMapEditorTabButtonReadModel tab = model.Tabs[i];
                Rect buttonRect = new Rect(tabRect.x + (tabWidth + tabGap) * i, tabRect.y, tabWidth, tabRect.height);
                GUIStyle style = tab.IsSelected ? selectedButtonStyle : iconButtonStyle;
                if (GUI.Button(buttonRect, tab.Label, style))
                {
                    selectedTab = tab.Tab;
                }
            }

            Rect titleRect = new Rect(panelRect.x + 18f, panelRect.y + 76f, panelRect.width - 36f, 48f);
            GUI.Box(titleRect, model.Title, headerStyle);
            Rect contentRect = new Rect(panelRect.x + 16f, panelRect.y + 132f, panelRect.width - 32f, panelRect.height - 150f);
            return new CampusRuntimeMapEditorShellInteraction(selectedTab, contentRect);
        }

        public static CampusRuntimeMapEditorFloorPanelInteraction DrawFloorPanel(
            Rect panelRect,
            GUIStyle panelStyle,
            GUIStyle headerStyle,
            GUIStyle buttonStyle,
            GUIStyle selectedButtonStyle,
            Vector2 scrollPosition,
            out Vector2 nextScrollPosition,
            CampusRuntimeMapEditorFloorPanelReadModel model)
        {
            GUI.Box(panelRect, GUIContent.none, panelStyle);
            GUI.Box(new Rect(panelRect.x + 12f, panelRect.y + 12f, panelRect.width - 24f, 40f), model.Title, headerStyle);

            Rect listRect = new Rect(panelRect.x + 12f, panelRect.y + 60f, panelRect.width - 24f, panelRect.height - 110f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(180f, model.Rows.Count * 40f));
            int? selectedFloorIndex = null;

            nextScrollPosition = GUI.BeginScrollView(listRect, scrollPosition, viewRect);
            for (int i = 0; i < model.Rows.Count; i++)
            {
                CampusRuntimeMapEditorFloorRowReadModel row = model.Rows[i];
                float rowY = i * 40f;
                GUIStyle style = row.IsSelected ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(0f, rowY, viewRect.width, 34f), row.Label, style))
                {
                    selectedFloorIndex = row.FloorIndex;
                }
            }

            GUI.EndScrollView();

            float buttonY = panelRect.yMax - 42f;
            bool addRequested = GUI.Button(new Rect(panelRect.x + 12f, buttonY, 78f, 30f), model.AddLabel, buttonStyle);
            bool toggleLockRequested = GUI.Button(new Rect(panelRect.x + 96f, buttonY, 78f, 30f), model.LockLabel, buttonStyle);
            bool deleteRequested = GUI.Button(new Rect(panelRect.x + 180f, buttonY, 78f, 30f), model.DeleteLabel, buttonStyle);
            return new CampusRuntimeMapEditorFloorPanelInteraction(selectedFloorIndex, addRequested, toggleLockRequested, deleteRequested);
        }

        public static void DrawChecklistPanel(
            Rect panelRect,
            GUIStyle panelStyle,
            GUIStyle headerStyle,
            GUIStyle bodyStyle,
            GUIStyle warningStyle,
            Vector2 scrollPosition,
            out Vector2 nextScrollPosition,
            CampusRuntimeMapEditorChecklistReadModel model)
        {
            GUI.Box(panelRect, GUIContent.none, panelStyle);
            GUI.Box(new Rect(panelRect.x + 12f, panelRect.y + 12f, panelRect.width - 24f, 40f), model.Title, headerStyle);

            Rect listRect = new Rect(panelRect.x + 12f, panelRect.y + 62f, panelRect.width - 24f, panelRect.height - 78f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(listRect.height, model.Entries.Count * 30f));
            nextScrollPosition = GUI.BeginScrollView(listRect, scrollPosition, viewRect);

            if (model.Entries.Count == 0)
            {
                GUI.Label(new Rect(0f, 0f, viewRect.width, 58f), model.EmptyMessage, bodyStyle);
            }

            Color previousColor = GUI.color;
            for (int i = 0; i < model.Entries.Count; i++)
            {
                CampusRuntimeMapEditorChecklistEntryReadModel entry = model.Entries[i];
                float rowY = i * 30f;
                Rect swatchRect = new Rect(0f, rowY + 6f, 18f, 16f);
                GUI.color = entry.SwatchColor;
                GUI.DrawTexture(swatchRect, Texture2D.whiteTexture);
                GUI.color = previousColor;
                GUI.Label(new Rect(26f, rowY, viewRect.width - 92f, 28f), entry.Label, bodyStyle);
                GUI.Label(new Rect(viewRect.width - 66f, rowY, 62f, 28f), entry.ValueText, entry.IsSatisfied ? bodyStyle : warningStyle);
            }

            GUI.color = previousColor;
            GUI.EndScrollView();
        }

        public static CampusRuntimeMapEditorToolbarInteraction DrawToolbar(
            Rect panelRect,
            GUIStyle panelStyle,
            GUIStyle buttonStyle,
            GUIStyle selectedButtonStyle,
            CampusRuntimeMapEditorToolbarReadModel model,
            float toolbarButtonWidth)
        {
            GUI.Box(panelRect, GUIContent.none, panelStyle);
            float x = panelRect.x + 14f;
            float y = panelRect.y + 14f;

            bool closeRequested = DrawToolbarButton(ref x, y, model.CloseLabel, buttonStyle, true, toolbarButtonWidth);
            bool helpRequested = DrawToolbarButton(ref x, y, model.HelpLabel, buttonStyle, true, toolbarButtonWidth);
            bool importRequested = DrawToolbarButton(ref x, y, model.ImportLabel, buttonStyle, true, toolbarButtonWidth);
            bool exportRequested = DrawToolbarButton(ref x, y, model.ExportLabel, buttonStyle, true, toolbarButtonWidth);
            bool undoRequested = DrawToolbarButton(ref x, y, model.UndoLabel, buttonStyle, model.CanUndo, toolbarButtonWidth);
            bool redoRequested = DrawToolbarButton(ref x, y, model.RedoLabel, buttonStyle, model.CanRedo, toolbarButtonWidth);

            float rightX = panelRect.xMax - 354f;
            bool toggleGridRequested = GUI.Button(
                new Rect(rightX, y, 106f, 46f),
                model.IsGridVisible ? model.GridOnLabel : model.GridOffLabel,
                model.IsGridVisible ? selectedButtonStyle : buttonStyle);
            bool toggleSettingsRequested = GUI.Button(
                new Rect(rightX + 116f, y, 106f, 46f),
                model.SettingsLabel,
                model.IsSettingsVisible ? selectedButtonStyle : buttonStyle);
            bool rebuildRequested = GUI.Button(new Rect(rightX + 232f, y, 106f, 46f), model.RebuildLabel, buttonStyle);

            return new CampusRuntimeMapEditorToolbarInteraction(
                closeRequested,
                helpRequested,
                importRequested,
                exportRequested,
                undoRequested,
                redoRequested,
                toggleGridRequested,
                toggleSettingsRequested,
                rebuildRequested);
        }

        public static void DrawHelpPanel(Rect panelRect, GUIStyle panelStyle, GUIStyle headerStyle, GUIStyle bodyStyle, CampusRuntimeMapEditorHelpReadModel model)
        {
            if (!model.IsVisible)
            {
                return;
            }

            GUI.Box(panelRect, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + 16f, panelRect.width - 36f, 32f), model.Title, headerStyle);
            GUI.Label(new Rect(panelRect.x + 22f, panelRect.y + 66f, panelRect.width - 44f, panelRect.height - 88f), model.Body, bodyStyle);
        }

        public static CampusRuntimeMapEditorSettingsInteraction DrawSettingsPanel(
            Rect panelRect,
            GUIStyle panelStyle,
            GUIStyle headerStyle,
            GUIStyle bodyStyle,
            GUIStyle buttonStyle,
            CampusRuntimeMapEditorSettingsReadModel model)
        {
            if (!model.IsVisible)
            {
                return new CampusRuntimeMapEditorSettingsInteraction(
                    model.AutoSaveEnabled,
                    model.AutoLoadEnabled,
                    false,
                    false,
                    false,
                    false);
            }

            GUI.Box(panelRect, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 12f, panelRect.width - 32f, 28f), model.Title, headerStyle);
            GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 44f, panelRect.width - 32f, 44f), model.MapSourceText, bodyStyle);

            bool autoSaveEnabled = GUI.Toggle(
                new Rect(panelRect.x + 16f, panelRect.y + 92f, panelRect.width - 32f, 24f),
                model.AutoSaveEnabled,
                model.AutoSaveLabel);
            bool autoLoadEnabled = GUI.Toggle(
                new Rect(panelRect.x + 16f, panelRect.y + 120f, panelRect.width - 32f, 24f),
                model.AutoLoadEnabled,
                model.AutoLoadLabel);

            float y = panelRect.y + 156f;
            float width = panelRect.width - 32f;
            float buttonWidth = (width - 8f) * 0.5f;
            bool savePlayerMapRequested = GUI.Button(
                new Rect(panelRect.x + 16f, y, buttonWidth, 30f),
                model.SavePlayerMapLabel,
                buttonStyle);
            bool loadPlayerMapRequested = GUI.Button(
                new Rect(panelRect.x + 24f + buttonWidth, y, buttonWidth, 30f),
                model.LoadPlayerMapLabel,
                buttonStyle);

            y += 38f;
            bool exportAuthoringRequested = GUI.Button(
                new Rect(panelRect.x + 16f, y, buttonWidth, 30f),
                model.ExportAuthoringLabel,
                buttonStyle);
            bool restoreAuthoringRequested = GUI.Button(
                new Rect(panelRect.x + 24f + buttonWidth, y, buttonWidth, 30f),
                model.RestoreAuthoringLabel,
                buttonStyle);

            return new CampusRuntimeMapEditorSettingsInteraction(
                autoSaveEnabled,
                autoLoadEnabled,
                savePlayerMapRequested,
                loadPlayerMapRequested,
                exportAuthoringRequested,
                restoreAuthoringRequested);
        }

        public static CampusRuntimeMapEditorObjectSettingsLauncherInteraction DrawObjectSettingsLauncher(
            Rect rowRect,
            GUIStyle highlightStyle,
            GUIStyle buttonStyle,
            GUIStyle selectedButtonStyle,
            GUIStyle mutedStyle,
            CampusRuntimeMapEditorObjectSettingsLauncherReadModel model)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && model.HasSelection;
            GUI.Box(new Rect(rowRect.x, rowRect.y - 4f, rowRect.width, 42f), GUIContent.none, highlightStyle);
            bool toggleRequested = GUI.Button(
                new Rect(rowRect.x + 8f, rowRect.y, 156f, 34f),
                model.ButtonLabel,
                model.IsOpen ? selectedButtonStyle : buttonStyle);
            GUI.enabled = previousEnabled;
            GUI.Label(
                new Rect(rowRect.x + 174f, rowRect.y + 3f, Mathf.Max(10f, rowRect.width - 174f), 28f),
                model.SelectedObjectName,
                mutedStyle);
            return new CampusRuntimeMapEditorObjectSettingsLauncherInteraction(toggleRequested);
        }

        public static CampusRuntimeMapEditorObjectSettingsPanelInteraction DrawObjectSettingsPanelChrome(
            Rect panelRect,
            GUIStyle panelStyle,
            GUIStyle highlightStyle,
            GUIStyle headerStyle,
            GUIStyle buttonStyle,
            GUIStyle warningStyle,
            GUIStyle mutedStyle,
            CampusRuntimeMapEditorObjectSettingsPanelReadModel model)
        {
            if (!model.IsVisible)
            {
                return default;
            }

            GUI.Box(panelRect, GUIContent.none, panelStyle);
            GUI.Box(new Rect(panelRect.x + 8f, panelRect.y + 8f, panelRect.width - 16f, 84f), GUIContent.none, highlightStyle);
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 62f, 38f), model.Title, headerStyle);
            bool closeRequested = GUI.Button(new Rect(panelRect.xMax - 46f, panelRect.y + 12f, 32f, 32f), "X", buttonStyle);

            if (!model.CanEdit)
            {
                GUI.Label(
                    new Rect(panelRect.x + 18f, panelRect.y + 62f, panelRect.width - 36f, 70f),
                    model.WarningMessage,
                    warningStyle);
                return new CampusRuntimeMapEditorObjectSettingsPanelInteraction(
                    closeRequested,
                    false,
                    false,
                    default,
                    0f);
            }

            float actionY = panelRect.y + 56f;
            bool saveAndSyncRequested = GUI.Button(
                new Rect(panelRect.x + 14f, actionY, 132f, 32f),
                model.SaveAndSyncLabel,
                buttonStyle);
            bool applyToAllRequested = GUI.Button(
                new Rect(panelRect.x + 154f, actionY, 184f, 32f),
                model.ApplyToAllLabel,
                buttonStyle);
            GUI.Label(
                new Rect(panelRect.x + 348f, actionY + 2f, Mathf.Max(10f, panelRect.width - 408f), 30f),
                model.SyncHintLabel,
                mutedStyle);

            Rect scrollRect = new Rect(panelRect.x + 14f, panelRect.y + 98f, panelRect.width - 28f, panelRect.height - 112f);
            float viewWidth = scrollRect.width - 22f;
            return new CampusRuntimeMapEditorObjectSettingsPanelInteraction(
                closeRequested,
                saveAndSyncRequested,
                applyToAllRequested,
                scrollRect,
                viewWidth);
        }

        public static void DrawStatusLine(Rect rect, GUIStyle panelStyle, CampusRuntimeMapEditorStatusReadModel model)
        {
            if (!model.ShouldShow)
            {
                return;
            }

            GUI.Box(rect, model.Message, panelStyle);
        }

        private static bool DrawToolbarButton(ref float x, float y, string label, GUIStyle buttonStyle, bool enabled, float buttonWidth)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && enabled;
            bool clicked = GUI.Button(new Rect(x, y, buttonWidth, 46f), label, buttonStyle);
            GUI.enabled = previousEnabled;
            x += buttonWidth + 8f;
            return clicked;
        }
    }
}
