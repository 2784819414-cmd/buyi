using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Retail;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace NtingCampusMapEditor
{
    public sealed partial class CampusRuntimeMapEditor
    {
        private void OnGUI()
        {
            EnsureStyles();
            if (!isOpen)
            {
                textInputFocused = false;
                return;
            }

            ResolveLayoutRects();
            HandleGuiScrollWheel();
            DrawGridOverlay();
            DrawWorldPreviewOverlay();
            DrawLeftPanel();
            DrawFloorPanel();
            DrawChecklistPanel();
            DrawBottomToolbar();
            DrawSettingsPanel();
            DrawObjectSettingsPanel();
            DrawHelpPanel();
            DrawStatusLine();
            RefreshTextInputFocusState();
        }

        private void DrawLeftPanel()
        {
            CampusRuntimeMapEditorShellReadModel model = CreateShellReadModel();
            CampusRuntimeMapEditorShellInteraction interaction = CampusRuntimeMapEditorChromePresenter.DrawShell(
                leftPanelRect,
                panelStyle,
                headerStyle,
                selectedButtonStyle,
                iconButtonStyle,
                model);
            if (interaction.SelectedTab.HasValue)
            {
                HandleTabSelected(interaction.SelectedTab.Value);
            }

            switch (model.ActiveTab)
            {
                case CampusRuntimeEditorTab.Build:
                    DrawBuildTab(interaction.ContentRect);
                    break;
                case CampusRuntimeEditorTab.Rooms:
                    DrawRoomTab(interaction.ContentRect);
                    break;
                case CampusRuntimeEditorTab.Gameplay:
                    DrawGameplayTab(interaction.ContentRect);
                    break;
                case CampusRuntimeEditorTab.Objects:
                    DrawObjectTab(interaction.ContentRect);
                    break;
                case CampusRuntimeEditorTab.Lighting:
                    DrawLightingTab(interaction.ContentRect);
                    break;
            }
        }

        private void HandleTabSelected(CampusRuntimeEditorTab tab)
        {
            activeTab = tab;
            switch (tab)
            {
                case CampusRuntimeEditorTab.Build:
                    brushMode = CampusRuntimeBrushMode.PaintFloor;
                    break;
                case CampusRuntimeEditorTab.Rooms:
                    brushMode = roomPrefabs.Count > 0
                        ? CampusRuntimeBrushMode.PlaceRoomPrefab
                        : CampusRuntimeBrushMode.CreateRoomPrefab;
                    break;
                case CampusRuntimeEditorTab.Gameplay:
                    brushMode = CampusRuntimeBrushMode.PlaceRoom;
                    break;
                case CampusRuntimeEditorTab.Objects:
                    brushMode = CampusRuntimeBrushMode.PlaceObject;
                    break;
                case CampusRuntimeEditorTab.Lighting:
                    brushMode = CampusRuntimeBrushMode.PlaceLight;
                    break;
            }
        }

        private CampusRuntimeMapEditorShellReadModel CreateShellReadModel()
        {
            return CampusRuntimeMapEditorReadModelBuilder.BuildShell(
                activeTab,
                Tr("\u5efa\u9020", "Build"),
                Tr("\u623f\u95f4", "Rooms"),
                Tr("\u533a\u57df", "Areas"),
                Tr("\u7269\u4ef6", "Objects"),
                Tr("\u706f\u5149", "Lighting"));
        }

        private CampusRuntimeMapEditorFloorPanelReadModel CreateFloorPanelReadModel()
        {
            return CampusRuntimeMapEditorReadModelBuilder.BuildFloorPanel(
                Tr("\u697c\u5c42", "Floors"),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Add),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Lock),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Delete),
                selectedFloorIndex,
                mapRoot != null ? mapRoot.Floors : null,
                delegate(CampusFloorRoot floor)
                {
                    return CampusRuntimeEditorTextCatalog.FormatFloorButton(displayLanguage, floor.FloorIndex, floor.IsUnlocked);
                });
        }

        private CampusRuntimeMapEditorChecklistReadModel CreateChecklistReadModel()
        {
            return CampusRuntimeMapEditorReadModelBuilder.BuildChecklist(
                Tr("\u533a\u57df\u68c0\u67e5\u6e05\u5355", "Area Checklist"),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoRoomRequirementsExist),
                roomNames,
                roomRequiredCounts,
                GetAreaPresetLabel,
                GetRoomRegionCount,
                CampusRuntimeGameplayOverlayPalette.ResolveRoomNameColor);
        }

        private CampusRuntimeMapEditorToolbarReadModel CreateToolbarReadModel()
        {
            return CampusRuntimeMapEditorReadModelBuilder.BuildToolbar(
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Close),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Help),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Import),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Export),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Undo),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Redo),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.GridOn),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.GridOff),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Settings),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Rebuild),
                showGridOverlay,
                showSettings,
                undoSnapshots.Count > 0,
                redoSnapshots.Count > 0);
        }

        private CampusRuntimeMapEditorHelpReadModel CreateHelpReadModel()
        {
            return CampusRuntimeMapEditorReadModelBuilder.BuildHelp(
                showHelpOverlay,
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Controls),
                CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ControlsBody));
        }

        private CampusRuntimeMapEditorStatusReadModel CreateStatusReadModel()
        {
            return CampusRuntimeMapEditorReadModelBuilder.BuildStatus(statusText, statusUntil, Time.realtimeSinceStartup);
        }

        private void SelectFloor(int floorIndex)
        {
            selectedFloorIndex = CampusRuntimeMapEditorFloorCommandService.SelectFloor(mapRoot, floorIndex);
        }

        private void HandleAddFloorRequested()
        {
            int nextFloorIndex = CampusRuntimeMapEditorFloorCommandService.AddFloor(
                mapRoot,
                RecordUndo,
                EnsureFloor);
            if (nextFloorIndex <= 0)
            {
                return;
            }

            selectedFloorIndex = nextFloorIndex;
        }

        private void HandleToggleSelectedFloorLockRequested()
        {
            CampusFloorRoot floor = EnsureFloor(selectedFloorIndex);
            CampusRuntimeMapEditorFloorCommandService.ToggleFloorLock(floor, RecordUndo);
        }

        private float GetBuildContentHeight(float width)
        {
            float height = 34f + 40f * 3f + 8f + 38f;
            height += 80f * 2f + 8f;
            height += 250f;
            height += 34f + GetTileGridHeight(floorTiles.Count, width);
            height += 10f + 34f + GetTileGridHeight(wallTiles.Count, width);
            if (wallProfiles.Count > 0)
            {
                height += 10f + 34f + wallProfiles.Count * 36f;
            }

            return height + 36f;
        }

        private float GetRoomContentHeight(float width)
        {
            float height = 34f + 40f * 1f;
            height += 34f + 38f + 42f + (roomPrefabs.Count == 0 ? 66f : roomPrefabs.Count * 42f) + 46f;
            return height;
        }

        private float GetGameplayContentHeight(float width)
        {
            int pointCount = CampusRuntimeGameplayMarkerPresetCatalog.Presets.Length;
            int actorCount = CampusRuntimeGameplayActorPresetCatalog.Presets.Length;
            float height = 34f + 40f + 12f;
            height += 34f + 38f + 80f + 8f;
            height += roomNames.Count == 0 ? 66f : roomNames.Count * 40f;
            height += 12f + (roomNames.Count > 0 ? 42f : 0f) + 70f;
            height += 34f + 40f + Mathf.CeilToInt(pointCount / 2f) * 38f + 18f;
            height += GetGameplayOwnerSelectionHeight();
            height += 34f + 40f + Mathf.CeilToInt(actorCount / 2f) * 38f + 164f;
            height += 96f;
            return height;
        }

        private float GetObjectContentHeight(float width)
        {
            float height = 34f + 40f * 2f + 8f + 44f;
            height += 46f;
            height += 258f;
            height += 80f + 8f;
            height += 34f + GetPrefabGridHeight(objectPrefabs.Count, width);
            if (stairPrefab == null)
            {
                height += 76f;
            }

            return height + 36f;
        }

        private float GetLightingContentHeight(float width)
        {
            int editableLightCount = GetEditableLights().Count;
            float height = 34f + 40f * 2f + 6f + 44f + 30f + 92f + 30f * 5f + 128f + 130f + 34f + editableLightCount * 38f;
            if (selectedLight != null)
            {
                height += 370f;
            }

            return height + 110f;
        }

        private float GetTileGridHeight(int count, float width)
        {
            if (count == 0)
            {
                return 56f;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (PaletteTileSize + 10f)));
            int rows = Mathf.CeilToInt((float)count / columns);
            return rows * (PaletteTileSize + 22f);
        }

        private float GetPrefabGridHeight(int count, float width)
        {
            return GetTileGridHeight(count, width);
        }

        private void DrawBuildTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetBuildContentHeight(viewWidth)));
            leftScroll = GUI.BeginScrollView(contentRect, leftScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.BuildTools), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PaintFloor, CampusRuntimeBrushMode.PaintWall },
                new string[]
                {
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Pan),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PaintFloor),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PaintWall)
                });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.RectangleFloor, CampusRuntimeBrushMode.RectangleWall, CampusRuntimeBrushMode.RectangleErase },
                new string[]
                {
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RectFloor),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RectWall),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RectErase)
                });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Erase, CampusRuntimeBrushMode.Pick },
                new string[]
                {
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Erase),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Pick)
                });

            y += 8f;
            GUI.Label(new Rect(0f, y, 90f, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.BrushSize), bodyStyle);
            brushSize = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(90f, y + 8f, viewRect.width - 150f, 20f), brushSize, 1f, 8f));
            GUI.Label(new Rect(viewRect.width - 50f, y, 50f, 24f), brushSize.ToString(), bodyStyle);
            y += 38f;

            DrawImportFolderRow(ref y, viewRect.width, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.FloorImports), GetFloorImportFolder(), CampusRuntimeImportTarget.Floor);
            DrawImportFolderRow(ref y, viewRect.width, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.WallImports), GetWallImportFolder(), CampusRuntimeImportTarget.Wall);
            y += 8f;

            DrawCustomWallPanel(ref y, viewRect.width);
            y += 8f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.FloorPalette), headerStyle);
            y += 34f;
            y = DrawTilePaletteGrid(floorTiles, selectedFloorTileIndex, y, viewRect.width,
                delegate(int index)
                {
                    selectedFloorTileIndex = index;
                    brushMode = CampusRuntimeBrushMode.PaintFloor;
                },
                delegate(int index)
                {
                    DeleteImportedTileResource(GetFloorImportFolder(), floorTiles, index, "floor");
                });

            y += 10f;
            GUI.Label(new Rect(0f, y, viewRect.width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.WallPalette), headerStyle);
            y += 34f;
            y = DrawTilePaletteGrid(wallTiles, selectedWallTileIndex, y, viewRect.width,
                delegate(int index)
                {
                    selectedWallTileIndex = index;
                    brushMode = CampusRuntimeBrushMode.PaintWall;
                },
                delegate(int index)
                {
                    DeleteImportedTileResource(GetWallImportFolder(), wallTiles, index, "wall");
                });

            if (wallProfiles.Count > 0)
            {
                y += 10f;
                GUI.Label(new Rect(0f, y, viewRect.width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.WallProfiles), headerStyle);
                y += 34f;
                for (int i = 0; i < wallProfiles.Count; i++)
                {
                    CampusWallRenderProfile profile = wallProfiles[i];
                    string label = profile != null ? CampusObjectNames.GetDisplayName(profile.name) : "Missing Profile";
                    if (GUI.Button(new Rect(0f, y, viewRect.width, 32f), label, i == selectedWallProfileIndex ? selectedButtonStyle : buttonStyle))
                    {
                        selectedWallProfileIndex = i;
                        fallbackWallProfile = profile;
                        PrepareRuntimeMapPresentationSafe();
                    }

                    y += 36f;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawRoomTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetRoomContentHeight(viewWidth)));
            leftScroll = GUI.BeginScrollView(contentRect, leftScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u6a21\u5757\u5de5\u5177", "Module Tools"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.CreateRoomPrefab, CampusRuntimeBrushMode.PlaceRoomPrefab },
                new string[] { Tr("\u6846\u9009\u6a21\u5757", "Box Module"), Tr("\u653e\u7f6e\u6a21\u5757", "Place Module") });

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u623f\u95f4\u6a21\u5757", "Room Modules"), headerStyle);
            y += 34f;
            GUI.Label(new Rect(0f, y, 64f, 30f), Tr("\u6a21\u5757", "Module"), bodyStyle);
            newRoomPrefabName = DrawTextInput(new Rect(66f, y, viewRect.width - 178f, 30f), newRoomPrefabName, "room_prefab_name");
            if (GUI.Button(new Rect(viewRect.width - 104f, y, 104f, 30f), Tr("\u6846\u9009\u6a21\u5757", "Box Module"), buttonStyle))
            {
                brushMode = CampusRuntimeBrushMode.CreateRoomPrefab;
                activeTab = CampusRuntimeEditorTab.Rooms;
                SetStatus(Tr("\u62d6\u51fa\u77e9\u5f62\u533a\u57df\uff0c\u677e\u5f00\u540e\u4fdd\u5b58\u4e3a\u623f\u95f4\u6a21\u5757\u3002", "Drag a rectangle area, then release to save it as a room module."));
            }

            y += 38f;
            float moduleButtonWidth = Mathf.Max(72f, (viewRect.width - 16f) / 3f);
            if (GUI.Button(new Rect(0f, y, moduleButtonWidth, 30f), Tr(CampusRuntimeEditorTextId.OpenFolder), buttonStyle))
            {
                OpenImportLocation(GetRoomPrefabFolder());
            }

            if (GUI.Button(new Rect(moduleButtonWidth + 8f, y, moduleButtonWidth, 30f), Tr(CampusRuntimeEditorTextId.Refresh), buttonStyle))
            {
                LoadImportedRoomPrefabs();
                SchedulePlayerMapSave();
                SetStatus(Tr("\u623f\u95f4\u6a21\u5757\u5df2\u5237\u65b0\u3002", "Room modules refreshed."));
            }

            if (roomPrefabs.Count > 0 && GUI.Button(new Rect((moduleButtonWidth + 8f) * 2f, y, moduleButtonWidth, 30f), Tr(CampusRuntimeEditorTextId.Delete), buttonStyle))
            {
                DeleteSelectedRoomPrefab();
            }

            y += 42f;
            if (roomPrefabs.Count == 0)
            {
                GUI.Label(new Rect(0f, y, viewRect.width, 58f), Tr("\u5c1a\u65e0\u623f\u95f4\u6a21\u5757\u3002\u8f93\u5165\u6a21\u5757\u540d\uff0c\u70b9\u51fb\u6846\u9009\u6a21\u5757\uff0c\u518d\u5728\u5730\u56fe\u4e0a\u62d6\u51fa\u533a\u57df\u3002", "No room modules exist. Enter a module name, click Box Module, then drag an area on the map."), mutedStyle);
                y += 66f;
            }
            else
            {
                for (int i = 0; i < roomPrefabs.Count; i++)
                {
                    CampusRuntimeRoomPrefab roomPrefab = roomPrefabs[i];
                    string label = roomPrefab.RoomName + "  " + roomPrefab.Size.x + "x" + roomPrefab.Size.y;
                    if (GUI.Button(new Rect(0f, y, viewRect.width, 34f), label, i == selectedRoomPrefabIndex ? selectedButtonStyle : buttonStyle))
                    {
                        selectedRoomPrefabIndex = i;
                        brushMode = CampusRuntimeBrushMode.PlaceRoomPrefab;
                    }

                    y += 42f;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawGameplayTab(Rect contentRect)
        {
            EnsureCachedGameplayActorsForEditing();
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetGameplayContentHeight(viewWidth)));
            leftScroll = GUI.BeginScrollView(contentRect, leftScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u533a\u57df", "Areas"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[]
                {
                    CampusRuntimeBrushMode.PlaceRoom,
                    CampusRuntimeBrushMode.RectangleRoom
                },
                new string[] { Tr("\u6807\u8bb0\u533a\u57df", "Mark Area"), Tr("\u6846\u9009\u533a\u57df", "Box Area") });

            y += 12f;
            GUI.Label(
                new Rect(0f, y, viewRect.width, 44f),
                Tr("\u533a\u57df\u7c7b\u578b\u5df2\u7531\u9879\u76ee\u9884\u8bbe\u9501\u5b9a\uff1a\u53ea\u80fd\u9009\u62e9\u4e0b\u65b9\u533a\u57df\uff0c\u4e0d\u518d\u652f\u6301\u73a9\u5bb6\u81ea\u5b9a\u4e49\u533a\u57df\u540d\u3002", "Area types are locked to project presets. Choose one below; custom player area names are no longer supported."),
                mutedStyle);
            y += 52f;

            if (roomNames.Count == 0)
            {
                GUI.Label(new Rect(0f, y, viewRect.width, 58f), Tr("\u5c1a\u65e0\u533a\u57df\u9884\u8bbe\u3002", "No area presets exist."), mutedStyle);
                y += 66f;
            }

            for (int i = 0; i < roomNames.Count; i++)
            {
                Rect rowRect = new Rect(0f, y, viewRect.width, 34f);
                int count = GetRoomRegionCount(roomNames[i]);
                int required = roomRequiredCounts[i];
                if (GUI.Button(rowRect, GUIContent.none, i == selectedRoomIndex ? selectedButtonStyle : buttonStyle))
                {
                    selectedRoomIndex = i;
                    brushMode = CampusRuntimeBrushMode.PlaceRoom;
                }

                DrawAreaDefinitionRow(rowRect, roomNames[i], count, required);
                y += 40f;
            }

            y += 12f;
            GUI.Label(new Rect(0f, y, viewRect.width, 48f), Tr("\u533a\u57df\u68c0\u67e5\u6e05\u5355\u4f1a\u5b9e\u65f6\u7edf\u8ba1\u8fde\u901a\u533a\u57df\uff0c\u5e76\u968f JSON \u5bfc\u51fa\u3002", "The area checklist counts connected area regions in real time and is included in exported JSON."), mutedStyle);
            y += 58f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u670d\u52a1\u7ad9\u70b9", "Service Station Points"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.PlaceGameplayMarker, CampusRuntimeBrushMode.EraseGameplayMarker },
                new string[] { Tr("\u653e\u7f6e\u670d\u52a1\u7ad9\u70b9", "Place Service Station Point"), Tr("\u5220\u9664\u670d\u52a1\u7ad9\u70b9", "Erase Service Station Point") });
            DrawGameplayPresetSection(ref y, viewRect.width);
            DrawGameplayOwnerSelectionSection(ref y, viewRect.width);

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u89d2\u8272", "Actors"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.PlaceGameplayActor, CampusRuntimeBrushMode.EraseGameplayActor },
                new string[] { Tr("\u653e\u7f6e NPC", "Place NPC"), Tr("\u5220\u9664 NPC", "Erase NPC") });
            DrawGameplayActorPresetSection(ref y, viewRect.width);
            DrawGameplayActorFields(ref y, viewRect.width);

            y += 8f;
            Rect noteRect = new Rect(0f, y, viewRect.width, 76f);
            GUI.Label(
                noteRect,
                Tr("\u533a\u57df\u4fdd\u5b58\u5728\u5730\u56fe JSON\uff0c\u670d\u52a1\u7ad9\u70b9\u548c NPC \u4fdd\u5b58\u5728\u540c\u76ee\u5f55\u7684 .gameplay.json\u3002\u663e\u5f0f\u670d\u52a1\u7ad9\u70b9\u53ea\u7528\u4e8e\u670d\u52a1\u7ad9\u62d3\u6251\uff1a\u5148\u653e\u670d\u52a1\u7a97\u53e3\uff0c\u518d\u4e3a\u5176\u914d\u64cd\u4f5c\u5458\u4f4d\u3001\u987e\u5ba2\u4f4d\u548c\u6392\u961f\u4f4d\u3002", "Areas are saved in the map JSON. Service station points and NPCs are saved next to the map as .gameplay.json. Explicit service station points are only for service topology: place a service window first, then attach operator, customer, and queue slots to it."),
                mutedStyle);
            y += 84f;

            float halfWidth = (viewRect.width - 8f) * 0.5f;
            if (GUI.Button(new Rect(0f, y, halfWidth, 30f), Tr("\u4fdd\u5b58\u73a9\u6cd5\u5c42", "Save Gameplay Layer"), buttonStyle))
            {
                SaveGameplayOverlayForCurrentSource(true);
            }

            if (GUI.Button(new Rect(halfWidth + 8f, y, halfWidth, 30f), Tr("\u91cd\u8bfb\u73a9\u6cd5\u5c42", "Reload Gameplay Layer"), buttonStyle))
            {
                ReloadGameplayOverlayForCurrentSource(true);
            }

            GUI.EndScrollView();
        }

        private void DrawObjectTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetObjectContentHeight(viewWidth)));
            objectScroll = GUI.BeginScrollView(contentRect, objectScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u7269\u4ef6\u5de5\u5177", "Object Tools"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PlaceObject, CampusRuntimeBrushMode.PlaceStair },
                new string[] { Tr(CampusRuntimeEditorTextId.Pan), Tr("\u653e\u7f6e\u7269\u4ef6", "Place Object"), Tr("\u653e\u7f6e\u697c\u68af", "Place Stair") });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Erase, CampusRuntimeBrushMode.Pick },
                new string[] { Tr(CampusRuntimeEditorTextId.Erase), Tr(CampusRuntimeEditorTextId.Pick) });

            y += 8f;
            GUI.Label(new Rect(0f, y, 70f, 24f), Tr("\u65cb\u8f6c", "Rotation"), bodyStyle);
            if (GUI.Button(new Rect(72f, y, 84f, 30f), TrFormat("{0} \u5ea6", "{0} deg", rotation90 * 90), buttonStyle))
            {
                rotation90 = (rotation90 + 1) % 4;
            }

            GUI.Label(new Rect(170f, y, 76f, 24f), Tr("\u76ee\u6807\u697c\u5c42", "Target Floor"), bodyStyle);
            stairTargetFloorIndex = Mathf.Clamp(ParseIntField(new Rect(246f, y, 58f, 30f), stairTargetFloorIndex), 1, 99);
            y += 44f;

            DrawSelectedObjectSettingsLauncher(ref y, viewRect.width);
            DrawCreateObjectPanel(ref y, viewRect.width);
            y += 8f;

            DrawImportFolderRow(ref y, viewRect.width, Tr(CampusRuntimeEditorTextId.ObjectImports), GetObjectImportFolder(), CampusRuntimeImportTarget.Object);
            y += 8f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u7269\u4ef6\u8c03\u8272\u76d8", "Object Palette"), headerStyle);
            y += 34f;
            y = DrawPrefabPaletteGrid(objectPrefabs, selectedObjectIndex, y, viewRect.width,
                delegate(int index)
                {
                    selectedObjectIndex = index;
                    brushMode = CampusRuntimeBrushMode.PlaceObject;
                },
                delegate(int index)
                {
                    DeleteImportedObjectResource(index);
                });

            if (stairPrefab == null)
            {
                y += 12f;
                GUI.Label(new Rect(0f, y, viewRect.width, 52f), Tr("\u672a\u627e\u5230\u697c\u68af\u9884\u5236\u4f53\u3002\u8bf7\u5c06\u697c\u68af\u9884\u5236\u4f53\u653e\u5165 Resources/NtingCampusRuntime\uff0c\u624d\u80fd\u5728\u6784\u5efa\u7248\u4e2d\u653e\u7f6e\u697c\u68af\u3002", "No stair prefab found. Put a stair prefab in Resources/NtingCampusRuntime to place stairs in builds."), warningStyle);
            }

            GUI.EndScrollView();
        }

        private void DrawCreateObjectPanel(ref float y, float width)
        {
            GUI.Label(new Rect(0f, y, width, 26f), Tr("\u65b0\u589e\u7269\u4f53", "Create Object"), headerStyle);
            y += 34f;

            GUI.Label(new Rect(0f, y, 52f, 30f), Tr(CampusRuntimeEditorTextId.Name), bodyStyle);
            newObjectName = DrawTextInput(new Rect(54f, y, width - 54f, 30f), newObjectName, "create_object_name");
            y += 38f;

            GUI.Label(new Rect(0f, y, 52f, 30f), Tr(CampusRuntimeEditorTextId.Footprint), bodyStyle);
            newObjectFootprintX = Mathf.Clamp(ParseIntField(new Rect(54f, y, 52f, 30f), newObjectFootprintX, "create_object_x"), 1, 32);
            GUI.Label(new Rect(112f, y, 20f, 30f), "x", bodyStyle);
            newObjectFootprintY = Mathf.Clamp(ParseIntField(new Rect(134f, y, 52f, 30f), newObjectFootprintY, "create_object_y"), 1, 32);
            newObjectBlocksMovement = GUI.Toggle(new Rect(198f, y + 3f, 72f, 24f), newObjectBlocksMovement, Tr("\u963b\u6321", "Block"), bodyStyle);
            y += 34f;

            newObjectIsInteractable = GUI.Toggle(new Rect(0f, y, 96f, 24f), newObjectIsInteractable, Tr("\u53ef\u4ea4\u4e92", "Interactable"), bodyStyle);
            newObjectIsStorageContainer = GUI.Toggle(new Rect(104f, y, 96f, 24f), newObjectIsStorageContainer, Tr("\u50a8\u7269", "Storage"), bodyStyle);
            if (newObjectIsStorageContainer)
            {
                newObjectIsInteractable = true;
            }

            y += 30f;
            DrawColorControls(ref y, width, Tr("\u989c\u8272", "Color"), ref newObjectColor);

            if (GUI.Button(new Rect(0f, y, width, 32f), Tr("\u521b\u5efa\u5e76\u9009\u4e2d\u7269\u4f53", "Create And Select Object"), buttonStyle))
            {
                CreateRuntimeObjectFromEditorFields();
            }

            y += 42f;
        }

        private void DrawLightingTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetLightingContentHeight(viewWidth)));
            lightScroll = GUI.BeginScrollView(contentRect, lightScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u706f\u5149\u5de5\u5177", "Lighting Tools"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PlaceLight, CampusRuntimeBrushMode.Erase },
                new string[] { Tr(CampusRuntimeEditorTextId.Pan), Tr("\u653e\u7f6e\u706f\u5149", "Place Light"), Tr(CampusRuntimeEditorTextId.Erase) });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pick },
                new string[] { Tr(CampusRuntimeEditorTextId.Pick) });

            y += 6f;
            if (GUI.Button(new Rect(0f, y, 116f, 32f), Tr(CampusRuntimeEditorTextId.PointLight), lightBrushType == Light2D.LightType.Point ? selectedButtonStyle : buttonStyle))
            {
                lightBrushType = Light2D.LightType.Point;
                brushMode = CampusRuntimeBrushMode.PlaceLight;
            }

            y += 44f;
            y = DrawSlider(y, viewRect.width, Tr("\u5f3a\u5ea6", "Intensity"), ref lightIntensity, 0f, 4f);
            DrawColorControls(ref y, viewRect.width, Tr("\u989c\u8272", "Color"), ref lightColor);
            y = DrawSlider(y, viewRect.width, Tr("\u5185\u534a\u5f84", "Inner Radius"), ref lightInnerRadius, 0f, 12f);
            y = DrawSlider(y, viewRect.width, Tr("\u5916\u534a\u5f84", "Outer Radius"), ref lightOuterRadius, 0.2f, 24f);
            lightShadowsEnabled = GUI.Toggle(new Rect(0f, y, viewRect.width, 24f), lightShadowsEnabled, Tr("\u542f\u7528\u9634\u5f71", "Enable Shadows"), bodyStyle);
            y += 30f;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && lightShadowsEnabled;
            y = DrawSlider(y, viewRect.width, Tr("\u9634\u5f71\u5f3a\u5ea6", "Shadow Intensity"), ref lightShadowIntensity, 0f, 1f);
            y = DrawSlider(y, viewRect.width, Tr("\u9634\u5f71\u67d4\u548c", "Shadow Softness"), ref lightShadowSoftness, 0f, 1f);
            y = DrawSlider(y, viewRect.width, Tr("\u67d4\u548c\u8870\u51cf", "Softness Falloff"), ref lightShadowSoftnessFalloff, 0f, 1f);
            GUI.enabled = previousGuiEnabled;

            DrawLightPreviewCard(ref y, viewRect.width);
            DrawDayNightControls(ref y, viewRect.width);

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u573a\u666f\u70b9\u5149\u6e90", "Point Lights In Scene"), headerStyle);
            y += 34f;
            IReadOnlyList<Light2D> lights = GetEditableLights();
            for (int i = 0; i < lights.Count; i++)
            {
                Light2D light = lights[i];
                if (light == null)
                {
                    continue;
                }

                if (GUI.Button(new Rect(0f, y, viewRect.width - 44f, 32f), light.gameObject.name, selectedLight == light ? selectedButtonStyle : buttonStyle))
                {
                    selectedLight = light;
                    lightIntensity = Mathf.Max(0f, selectedLight.intensity);
                    lightInnerRadius = Mathf.Max(0f, selectedLight.pointLightInnerRadius);
                    lightOuterRadius = Mathf.Max(0.2f, selectedLight.pointLightOuterRadius);
                    SyncShadowFieldsFromSelectedLight();
                }

                if (GUI.Button(new Rect(viewRect.width - 38f, y, 38f, 32f), "X", buttonStyle))
                {
                    RecordUndo();
                    if (selectedLight == light)
                    {
                        selectedLight = null;
                    }

                    DestroyRuntimeObject(light.gameObject);
                    SchedulePlayerMapSave();
                }

                y += 38f;
            }

            if (selectedLight != null && !IsRuntimeEditableLight(selectedLight))
            {
                selectedLight = null;
            }

            if (selectedLight != null)
            {
                bool selectedLightChanged = false;
                y += 8f;
                GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u5f53\u524d\u70b9\u5149\u6e90", "Selected Point Light"), headerStyle);
                y += 34f;

                GUI.Label(new Rect(0f, y, 66f, 24f), Tr("\u5f3a\u5ea6", "Intensity"), bodyStyle);
                float adjustedIntensity = GUI.HorizontalSlider(new Rect(70f, y + 7f, viewRect.width - 178f, 20f), selectedLight.intensity, 0f, 8f);
                if (GUI.Button(new Rect(viewRect.width - 100f, y, 28f, 26f), "-", buttonStyle))
                {
                    adjustedIntensity = Mathf.Max(0f, selectedLight.intensity - 0.1f);
                }

                if (GUI.Button(new Rect(viewRect.width - 68f, y, 28f, 26f), "+", buttonStyle))
                {
                    adjustedIntensity = Mathf.Min(8f, selectedLight.intensity + 0.1f);
                }

                if (!Mathf.Approximately(selectedLight.intensity, adjustedIntensity))
                {
                    selectedLight.intensity = adjustedIntensity;
                    lightIntensity = adjustedIntensity;
                    selectedLightChanged = true;
                }

                GUI.Label(new Rect(viewRect.width - 36f, y, 36f, 24f), selectedLight.intensity.ToString("0.0"), smallBodyStyle);
                y += 30f;

                Color selectedColor = selectedLight.color;
                DrawColorControls(ref y, viewRect.width, Tr("\u989c\u8272", "Color"), ref selectedColor);
                if (selectedLight.color != selectedColor)
                {
                    selectedLight.color = selectedColor;
                    lightColor = selectedColor;
                    selectedLightChanged = true;
                }

                bool previousShadowsEnabled = selectedLight.shadowsEnabled;
                bool selectedShadowsEnabled = GUI.Toggle(new Rect(0f, y, viewRect.width, 24f), selectedLight.shadowsEnabled, Tr("\u542f\u7528\u9634\u5f71", "Enable Shadows"), bodyStyle);
                y += 30f;
                bool selectedPreviousGuiEnabled = GUI.enabled;
                GUI.enabled = selectedPreviousGuiEnabled && selectedShadowsEnabled;
                float selectedShadowIntensity = selectedLight.shadowIntensity;
                float selectedShadowSoftness = selectedLight.shadowSoftness;
                float selectedShadowSoftnessFalloff = selectedLight.shadowSoftnessFalloffIntensity;
                y = DrawSlider(y, viewRect.width, Tr("\u9634\u5f71\u5f3a\u5ea6", "Shadow Intensity"), ref selectedShadowIntensity, 0f, 1f);
                y = DrawSlider(y, viewRect.width, Tr("\u9634\u5f71\u67d4\u548c", "Shadow Softness"), ref selectedShadowSoftness, 0f, 1f);
                y = DrawSlider(y, viewRect.width, Tr("\u67d4\u548c\u8870\u51cf", "Softness Falloff"), ref selectedShadowSoftnessFalloff, 0f, 1f);
                GUI.enabled = selectedPreviousGuiEnabled;
                bool selectedShadowSettingsChanged = previousShadowsEnabled != selectedShadowsEnabled ||
                                                     !Mathf.Approximately(selectedLight.shadowIntensity, selectedShadowIntensity) ||
                                                     !Mathf.Approximately(selectedLight.shadowSoftness, selectedShadowSoftness) ||
                                                     !Mathf.Approximately(selectedLight.shadowSoftnessFalloffIntensity, selectedShadowSoftnessFalloff);
                CampusDynamicShadowUtility.ConfigureLightShadows(selectedLight, selectedShadowsEnabled, selectedShadowIntensity, selectedShadowSoftness, selectedShadowSoftnessFalloff);
                SyncShadowFieldsFromSelectedLight();
                selectedLightChanged |= selectedShadowSettingsChanged;

                float selectedInnerRadius = GUI.HorizontalSlider(new Rect(70f, y + 7f, viewRect.width - 120f, 20f), selectedLight.pointLightInnerRadius, 0f, 12f);
                if (!Mathf.Approximately(selectedLight.pointLightInnerRadius, selectedInnerRadius))
                {
                    selectedLight.pointLightInnerRadius = selectedInnerRadius;
                    selectedLightChanged = true;
                }

                GUI.Label(new Rect(0f, y, 66f, 24f), Tr("\u5185\u5708", "Inner"), bodyStyle);
                GUI.Label(new Rect(viewRect.width - 44f, y, 44f, 24f), selectedLight.pointLightInnerRadius.ToString("0.0"), smallBodyStyle);
                y += 30f;

                float selectedOuterRadius = GUI.HorizontalSlider(new Rect(70f, y + 7f, viewRect.width - 120f, 20f), Mathf.Max(selectedLight.pointLightInnerRadius + 0.1f, selectedLight.pointLightOuterRadius), selectedLight.pointLightInnerRadius + 0.1f, 24f);
                if (!Mathf.Approximately(selectedLight.pointLightOuterRadius, selectedOuterRadius))
                {
                    selectedLight.pointLightOuterRadius = selectedOuterRadius;
                    selectedLightChanged = true;
                }

                GUI.Label(new Rect(0f, y, 66f, 24f), Tr("\u5916\u5708", "Outer"), bodyStyle);
                GUI.Label(new Rect(viewRect.width - 44f, y, 44f, 24f), selectedLight.pointLightOuterRadius.ToString("0.0"), smallBodyStyle);
                y += 30f;

                if (selectedLightChanged)
                {
                    SchedulePlayerMapSave();
                }
            }

            GUI.EndScrollView();
        }

        private void DrawFloorPanel()
        {
            CampusRuntimeMapEditorFloorPanelReadModel model = CreateFloorPanelReadModel();
            CampusRuntimeMapEditorFloorPanelInteraction interaction = CampusRuntimeMapEditorChromePresenter.DrawFloorPanel(
                floorPanelRect,
                panelStyle,
                headerStyle,
                buttonStyle,
                selectedButtonStyle,
                floorScroll,
                out Vector2 nextFloorScroll,
                model);
            floorScroll = nextFloorScroll;

            if (interaction.SelectedFloorIndex.HasValue)
            {
                SelectFloor(interaction.SelectedFloorIndex.Value);
            }

            if (interaction.AddRequested)
            {
                HandleAddFloorRequested();
            }

            if (interaction.ToggleLockRequested)
            {
                HandleToggleSelectedFloorLockRequested();
            }

            if (interaction.DeleteRequested)
            {
                DeleteSelectedFloor();
            }
        }

        private void DrawChecklistPanel()
        {
            CampusRuntimeMapEditorChecklistReadModel model = CreateChecklistReadModel();
            CampusRuntimeMapEditorChromePresenter.DrawChecklistPanel(
                checklistPanelRect,
                panelStyle,
                headerStyle,
                bodyStyle,
                warningStyle,
                checklistScroll,
                out Vector2 nextChecklistScroll,
                model);
            checklistScroll = nextChecklistScroll;
        }

        private void DrawBottomToolbar()
        {
            CampusRuntimeMapEditorToolbarReadModel model = CreateToolbarReadModel();
            CampusRuntimeMapEditorToolbarInteraction interaction = CampusRuntimeMapEditorChromePresenter.DrawToolbar(
                bottomToolbarRect,
                panelStyle,
                buttonStyle,
                selectedButtonStyle,
                model,
                ToolbarButtonWidth);
            CampusRuntimeMapEditorToolbarCommandService.Execute(interaction, CreateToolbarCommandMap());
        }

        private void DrawHelpPanel()
        {
            CampusRuntimeMapEditorChromePresenter.DrawHelpPanel(
                helpPanelRect,
                panelStyle,
                headerStyle,
                bodyStyle,
                CreateHelpReadModel());
        }

        private void DrawStatusLine()
        {
            Rect rect = new Rect(Screen.width * 0.5f - 320f, 18f, 640f, 36f);
            CampusRuntimeMapEditorChromePresenter.DrawStatusLine(rect, panelStyle, CreateStatusReadModel());
        }

        private CampusRuntimeMapEditorToolbarCommandMap CreateToolbarCommandMap()
        {
            return new CampusRuntimeMapEditorToolbarCommandMap
            {
                Close = delegate
                {
                    SetEditorOpen(false);
                    SaveCurrentMapSource(false);
                },
                ToggleHelp = delegate { showHelpOverlay = !showHelpOverlay; },
                Import = ImportLatestJson,
                Export = ExportToJson,
                Undo = UndoSnapshot,
                Redo = RedoSnapshot,
                ToggleGrid = delegate { showGridOverlay = !showGridOverlay; },
                ToggleSettings = delegate { showSettings = !showSettings; },
                Rebuild = delegate
                {
                    PrepareRuntimeMapPresentationSafe();
                    SetStatus(CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.WallVisualsRebuiltStatus));
                }
            };
        }

        private void DrawGridOverlay()
        {
            if (!showGridOverlay || sceneCamera == null || mapRoot == null)
            {
                return;
            }

            CampusFloorRoot floor = mapRoot.GetFloor(selectedFloorIndex);
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            Vector3 minWorld = sceneCamera.ScreenToWorldPoint(new Vector3(0f, 0f, GetCameraPlaneDistance()));
            Vector3 maxWorld = sceneCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, GetCameraPlaneDistance()));
            Vector3Int minCell = floor.Grid.WorldToCell(new Vector3(Mathf.Min(minWorld.x, maxWorld.x), Mathf.Min(minWorld.y, maxWorld.y), 0f));
            Vector3Int maxCell = floor.Grid.WorldToCell(new Vector3(Mathf.Max(minWorld.x, maxWorld.x), Mathf.Max(minWorld.y, maxWorld.y), 0f));
            int minX = Mathf.Max(minCell.x - 1, hoverCell.x - 80);
            int maxX = Mathf.Min(maxCell.x + 1, hoverCell.x + 80);
            int minY = Mathf.Max(minCell.y - 1, hoverCell.y - 80);
            int maxY = Mathf.Min(maxCell.y + 1, hoverCell.y + 80);

            GUI.color = new Color(0.72f, 0.9f, 1f, 0.18f);
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 a = WorldToGuiPoint(floor.Grid.CellToWorld(new Vector3Int(x, minY, 0)));
                Vector2 b = WorldToGuiPoint(floor.Grid.CellToWorld(new Vector3Int(x, maxY + 1, 0)));
                DrawGuiLine(a, b, 1f);
            }

            for (int y = minY; y <= maxY; y++)
            {
                Vector2 a = WorldToGuiPoint(floor.Grid.CellToWorld(new Vector3Int(minX, y, 0)));
                Vector2 b = WorldToGuiPoint(floor.Grid.CellToWorld(new Vector3Int(maxX + 1, y, 0)));
                DrawGuiLine(a, b, 1f);
            }

            GUI.color = Color.white;
        }

        private void DrawWorldPreviewOverlay()
        {
            if (sceneCamera == null || mapRoot == null)
            {
                return;
            }

            CampusFloorRoot floor = mapRoot.GetFloor(selectedFloorIndex);
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            DrawSelectedLightRangeOverlay();
            DrawRoomMarkerOverlay(floor);
            DrawGameplayMarkerOverlay(floor);

            if (rectangleDragActive)
            {
                if (brushMode == CampusRuntimeBrushMode.RectangleRoom)
                {
                    Color roomColor = ResolveSelectedRoomOverlayColor();
                    DrawFilledCellRect(
                        floor.Grid,
                        rectangleStartCell,
                        hoverCell,
                        new Color(roomColor.r, roomColor.g, roomColor.b, 0.22f),
                        new Color(roomColor.r, roomColor.g, roomColor.b, 0.82f),
                        2f);
                }
                else
                {
                    DrawCellRect(floor.Grid, rectangleStartCell, hoverCell, new Color(0.2f, 0.85f, 1f, 0.45f), 2f);
                }
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceLight)
            {
                Vector3 center = floor.Grid.GetCellCenterWorld(hoverCell);
                DrawLightPlacementOverlay(floor.Grid, center);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceObject)
            {
                Vector2Int footprint = GetSelectedObjectFootprint();
                DrawCellGrid(floor.Grid, hoverCell, footprint, new Color(1f, 0.96f, 0.25f, 0.72f), 2f);
                DrawObjectPlacementPreview(floor.Grid, hoverCell, footprint);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceRoomPrefab)
            {
                CampusRuntimeRoomPrefab roomPrefab = GetSelectedRoomPrefab();
                Vector2Int size = roomPrefab != null ? CampusRuntimeRoomPrefabLibrary.NormalizeSize(roomPrefab.Size) : Vector2Int.one;
                DrawCellGrid(floor.Grid, hoverCell, size, new Color(0.2f, 0.85f, 1f, 0.72f), 2f);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceStair)
            {
                Vector3Int secondary = hoverCell + CampusStairLink.DirectionFromRotation(rotation90);
                DrawCellRect(floor.Grid, hoverCell, secondary, new Color(0.2f, 0.85f, 1f, 0.72f), 2f);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceRoom || brushMode == CampusRuntimeBrushMode.RectangleRoom)
            {
                Color roomColor = ResolveSelectedRoomOverlayColor();
                DrawFilledCellRect(
                    floor.Grid,
                    hoverCell,
                    hoverCell,
                    new Color(roomColor.r, roomColor.g, roomColor.b, 0.22f),
                    new Color(roomColor.r, roomColor.g, roomColor.b, 0.82f),
                    2f);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceGameplayMarker ||
                     brushMode == CampusRuntimeBrushMode.EraseGameplayMarker)
            {
                if (brushMode == CampusRuntimeBrushMode.PlaceGameplayMarker)
                {
                    Color color = ResolveSelectedGameplayOverlayColor();
                    DrawFilledCellRect(
                        floor.Grid,
                        hoverCell,
                        hoverCell,
                        new Color(color.r, color.g, color.b, 0.2f),
                        new Color(color.r, color.g, color.b, 0.82f),
                        2f);
                }
                else
                {
                    DrawFilledCellRect(
                        floor.Grid,
                        hoverCell,
                        hoverCell,
                        new Color(1f, 0.18f, 0.18f, 0.16f),
                        new Color(1f, 0.25f, 0.18f, 0.86f),
                        2f);
                }
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceGameplayActor ||
                     brushMode == CampusRuntimeBrushMode.EraseGameplayActor)
            {
                if (brushMode == CampusRuntimeBrushMode.PlaceGameplayActor)
                {
                    Color color = ResolveSelectedGameplayActorOverlayColor();
                    DrawFilledCellRect(
                        floor.Grid,
                        hoverCell,
                        hoverCell,
                        new Color(color.r, color.g, color.b, 0.2f),
                        new Color(color.r, color.g, color.b, 0.92f),
                        2f);
                }
                else
                {
                    DrawFilledCellRect(
                        floor.Grid,
                        hoverCell,
                        hoverCell,
                        new Color(1f, 0.18f, 0.18f, 0.16f),
                        new Color(1f, 0.25f, 0.18f, 0.86f),
                        2f);
                }
            }
            else
            {
                Vector3Int end = new Vector3Int(hoverCell.x + Mathf.Max(1, brushSize) - 1, hoverCell.y + Mathf.Max(1, brushSize) - 1, 0);
                DrawCellRect(floor.Grid, hoverCell, end, new Color(1f, 0.96f, 0.25f, 0.55f), 2f);
            }
        }

        private bool ShouldDrawRoomMarkerOverlay()
        {
            return activeTab == CampusRuntimeEditorTab.Gameplay &&
                   (brushMode == CampusRuntimeBrushMode.PlaceRoom ||
                    brushMode == CampusRuntimeBrushMode.RectangleRoom);
        }

        private void DrawRoomMarkerOverlay(CampusFloorRoot floor)
        {
            HideRoomMarkerSpriteRenderers(floor);
            if (!ShouldDrawRoomMarkerOverlay() || floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
            if (markers == null || markers.Length == 0)
            {
                return;
            }

            Dictionary<string, HashSet<Vector3Int>> cellsByRoomName =
                new Dictionary<string, HashSet<Vector3Int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Vector3Int cell = marker.Cell;
                cell.z = 0;
                string roomName = string.IsNullOrWhiteSpace(marker.RoomName)
                    ? "Unnamed Room"
                    : marker.RoomName.Trim();
                if (!cellsByRoomName.TryGetValue(roomName, out HashSet<Vector3Int> cells))
                {
                    cells = new HashSet<Vector3Int>();
                    cellsByRoomName[roomName] = cells;
                }

                cells.Add(cell);
            }

            if (cellsByRoomName.Count == 0)
            {
                return;
            }

            Color oldColor = GUI.color;
            foreach (KeyValuePair<string, HashSet<Vector3Int>> pair in cellsByRoomName)
            {
                Color roomColor = CampusRuntimeGameplayOverlayPalette.ResolveRoomNameColor(pair.Key);
                GUI.color = new Color(roomColor.r, roomColor.g, roomColor.b, 0.24f);
                foreach (Vector3Int cell in pair.Value)
                {
                    DrawCellFill(floor.Grid, cell);
                }

                GUI.color = new Color(roomColor.r, roomColor.g, roomColor.b, 0.82f);
                foreach (Vector3Int cell in pair.Value)
                {
                    DrawCellOuterEdges(floor.Grid, pair.Value, cell, 2f);
                }
            }

            GUI.color = oldColor;
        }

        private void DrawGameplayMarkerOverlay(CampusFloorRoot floor)
        {
            if (activeTab != CampusRuntimeEditorTab.Gameplay || floor == null || floor.Grid == null)
            {
                return;
            }

            Color oldColor = GUI.color;

            CampusGameplayRoomMarker[] roomMarkers =
                FindObjectsByType<CampusGameplayRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roomMarkers.Length; i++)
            {
                CampusGameplayRoomMarker marker = roomMarkers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Color color = CampusRuntimeGameplayOverlayPalette.ResolveRoomTypeColor(marker.RoomType);
                BoundsInt bounds = marker.BuildBounds();
                DrawFilledCellRect(
                    floor.Grid,
                    new Vector3Int(bounds.xMin, bounds.yMin, 0),
                    new Vector3Int(bounds.xMax - 1, bounds.yMax - 1, 0),
                    new Color(color.r, color.g, color.b, 0.16f),
                    new Color(color.r, color.g, color.b, 0.68f),
                    2f);
                DrawWorldLabel(floor.Grid.GetCellCenterWorld(marker.AnchorCell), marker.RoomDisplayName, color);
            }

            CampusGameplayFacilityMarker[] facilityMarkers =
                FindObjectsByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < facilityMarkers.Length; i++)
            {
                CampusGameplayFacilityMarker marker = facilityMarkers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Color color = CampusRuntimeGameplayOverlayPalette.ResolveFacilityColor(marker.FacilityType);
                Vector3Int cell = NormalizeCell(marker.Cell);
                DrawFilledCellRect(
                    floor.Grid,
                    cell,
                    cell,
                    new Color(color.r, color.g, color.b, 0.2f),
                    new Color(color.r, color.g, color.b, 0.82f),
                    2f);
                DrawWorldLabel(floor.Grid.GetCellCenterWorld(cell), marker.DisplayName, color);
            }

            for (int i = 0; i < cachedGameplayActors.Count; i++)
            {
                CampusRuntimeGameplayActorSnapshot actor = cachedGameplayActors[i];
                if (actor == null || Mathf.Max(1, actor.FloorIndex) != floor.FloorIndex)
                {
                    continue;
                }

                Color color = CampusRuntimeGameplayOverlayPalette.ResolveActorColor(actor);
                Vector3Int cell = NormalizeCell(actor.Cell);
                DrawFilledCellRect(
                    floor.Grid,
                    cell,
                    cell,
                    new Color(color.r, color.g, color.b, 0.22f),
                    new Color(color.r, color.g, color.b, 0.94f),
                    2f);
                string label = actor.LocalizedDisplayName.Get(displayLanguage, actor.DisplayName, actor.Id);
                DrawWorldLabel(floor.Grid.GetCellCenterWorld(cell), label, color);
            }

            GUI.color = oldColor;
        }

        private void DrawWorldLabel(Vector3 worldPosition, string label, Color accent)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            Vector2 guiPoint = WorldToGuiPoint(worldPosition);
            Rect rect = new Rect(guiPoint.x - 58f, guiPoint.y - 30f, 116f, 22f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            if (lineTexture != null)
            {
                GUI.DrawTexture(rect, lineTexture);
            }

            GUI.color = new Color(accent.r, accent.g, accent.b, 1f);
            GUI.Label(rect, Truncate(label, 16), smallBodyStyle);
            GUI.color = oldColor;
        }

        private Color ResolveSelectedGameplayOverlayColor()
        {
            CampusRuntimeGameplayMarkerPreset preset = GetSelectedGameplayPreset();
            return preset != null ? preset.Color : new Color(0.2f, 0.85f, 1f, 1f);
        }

        private Color ResolveSelectedGameplayActorOverlayColor()
        {
            CampusRuntimeGameplayActorPreset preset = GetSelectedGameplayActorPreset();
            return preset != null ? preset.Color : new Color(0.95f, 0.78f, 0.32f, 1f);
        }

        private Color ResolveSelectedRoomOverlayColor()
        {
            return CampusRuntimeGameplayOverlayPalette.ResolveRoomNameColor(GetSelectedRoomName());
        }

        private void SetEditorOpen(bool open)
        {
            if (isOpen == open)
            {
                if (!isOpen)
                {
                    HideAllRoomMarkerSpriteRenderersOnce();
                }

                return;
            }

            isOpen = open;
            if (isOpen)
            {
                roomMarkerVisualsHidden = false;
                return;
            }

            HideAllRoomMarkerSpriteRenderersOnce();
        }

        private void HideAllRoomMarkerSpriteRenderersOnce()
        {
            if (roomMarkerVisualsHidden)
            {
                return;
            }

            HideAllRoomMarkerSpriteRenderers();
            roomMarkerVisualsHidden = true;
        }

        private void HideRoomMarkerSpriteRenderers(CampusFloorRoot floor)
        {
            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void HideAllRoomMarkerSpriteRenderers()
        {
            CampusRuntimeRoomMarker[] markers =
                FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void RebuildGameplayRoomRegistrySafe()
        {
            try
            {
                CampusRoomRegistry registry =
                    FindFirstObjectByType<CampusRoomRegistry>(FindObjectsInactive.Include);
                if (registry != null)
                {
                    registry.RebuildRegistry();
                }
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.FailedToRebuildGameplayRooms,
                    exception.Message);
            }
        }

        private void DrawSelectedLightRangeOverlay()
        {
            if (selectedLight == null || selectedLight.lightType != Light2D.LightType.Point)
            {
                return;
            }

            DrawWorldCircle(selectedLight.transform.position, selectedLight.pointLightOuterRadius, new Color(1f, 0.74f, 0.2f, 0.65f), 2f, 56);
            DrawWorldCircle(selectedLight.transform.position, selectedLight.pointLightInnerRadius, new Color(1f, 0.95f, 0.55f, 0.55f), 1f, 40);
        }

        private void DrawLightPlacementOverlay(Grid grid, Vector3 center)
        {
            DrawCellRect(grid, hoverCell, hoverCell, new Color(1f, 0.96f, 0.25f, 0.75f), 2f);
            if (lightBrushType != Light2D.LightType.Point)
            {
                return;
            }

            DrawWorldCircle(center, Mathf.Max(lightInnerRadius, 0.05f), new Color(1f, 0.96f, 0.55f, 0.7f), 1f, 40);
            DrawWorldCircle(center, Mathf.Max(lightOuterRadius, lightInnerRadius + 0.1f), new Color(1f, 0.76f, 0.2f, 0.78f), 2f, 56);
        }

        private void DrawCellRect(Grid grid, Vector3Int start, Vector3Int end, Color color, float thickness)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x) + 1;
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y) + 1;
            Vector2 a = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(minX, minY, 0)));
            Vector2 b = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(maxX, minY, 0)));
            Vector2 c = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(maxX, maxY, 0)));
            Vector2 d = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(minX, maxY, 0)));
            GUI.color = color;
            DrawGuiLine(a, b, thickness);
            DrawGuiLine(b, c, thickness);
            DrawGuiLine(c, d, thickness);
            DrawGuiLine(d, a, thickness);
            GUI.color = Color.white;
        }

        private void DrawFilledCellRect(Grid grid, Vector3Int start, Vector3Int end, Color fillColor, Color borderColor, float thickness)
        {
            if (grid == null)
            {
                return;
            }

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            Color oldColor = GUI.color;
            GUI.color = fillColor;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    DrawCellFill(grid, new Vector3Int(x, y, 0));
                }
            }

            GUI.color = borderColor;
            DrawCellRect(grid, start, end, borderColor, thickness);
            GUI.color = oldColor;
        }

        private void DrawCellFill(Grid grid, Vector3Int cell)
        {
            if (grid == null || lineTexture == null)
            {
                return;
            }

            Vector2 min = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0)));
            Vector2 max = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, 0)));
            Rect rect = Rect.MinMaxRect(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
            GUI.DrawTexture(rect, lineTexture);
        }

        private void DrawCellOuterEdges(Grid grid, HashSet<Vector3Int> markedCells, Vector3Int cell, float thickness)
        {
            if (grid == null || markedCells == null)
            {
                return;
            }

            Vector3Int right = new Vector3Int(cell.x + 1, cell.y, 0);
            Vector3Int left = new Vector3Int(cell.x - 1, cell.y, 0);
            Vector3Int up = new Vector3Int(cell.x, cell.y + 1, 0);
            Vector3Int down = new Vector3Int(cell.x, cell.y - 1, 0);

            if (!markedCells.Contains(down))
            {
                DrawGuiLine(
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0))),
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y, 0))),
                    thickness);
            }

            if (!markedCells.Contains(right))
            {
                DrawGuiLine(
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y, 0))),
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, 0))),
                    thickness);
            }

            if (!markedCells.Contains(up))
            {
                DrawGuiLine(
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, 0))),
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y + 1, 0))),
                    thickness);
            }

            if (!markedCells.Contains(left))
            {
                DrawGuiLine(
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y + 1, 0))),
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0))),
                    thickness);
            }
        }

        private void DrawCellGrid(Grid grid, Vector3Int anchor, Vector2Int size, Color color, float thickness)
        {
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            Vector3Int end = new Vector3Int(anchor.x + size.x - 1, anchor.y + size.y - 1, anchor.z);
            DrawCellRect(grid, anchor, end, color, thickness);

            GUI.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 0.72f));
            for (int x = 1; x < size.x; x++)
            {
                Vector2 a = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(anchor.x + x, anchor.y, 0)));
                Vector2 b = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(anchor.x + x, anchor.y + size.y, 0)));
                DrawGuiLine(a, b, 1f);
            }

            for (int y = 1; y < size.y; y++)
            {
                Vector2 a = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(anchor.x, anchor.y + y, 0)));
                Vector2 b = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(anchor.x + size.x, anchor.y + y, 0)));
                DrawGuiLine(a, b, 1f);
            }

            GUI.color = Color.white;
        }

        private void DrawObjectPlacementPreview(Grid grid, Vector3Int anchor, Vector2Int footprint)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            if (grid == null || prefab == null)
            {
                return;
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            Sprite sprite = ResolvePrefabPreviewSprite(prefab, placed, out bool usesAuthoredDirectionalSprite, out int effectiveRotation90);
            if (sprite == null || renderer == null)
            {
                return;
            }

            Vector3 worldCenter = CampusPlacedObject.GetPlacementWorldCenter(
                grid,
                anchor,
                footprint,
                placed != null && placed.IsWallMounted,
                effectiveRotation90);
            Vector2 previewScale = placed != null ? placed.NormalizedVisualScale : new Vector2(renderer.transform.localScale.x, renderer.transform.localScale.y);
            Rect rect = BuildWorldPreviewRect(worldCenter, sprite, new Vector3(previewScale.x, previewScale.y, renderer.transform.localScale.z));
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float previewRotation = placed != null && placed.AllowRotation && !usesAuthoredDirectionalSprite && !placed.SuppressFlatSpriteRotation ? -effectiveRotation90 * 90f : 0f;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.58f);
            if (!Mathf.Approximately(previewRotation, 0f))
            {
                GUIUtility.RotateAroundPivot(previewRotation, rect.center);
            }

            DrawSprite(rect, sprite);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private Rect BuildWorldPreviewRect(Vector3 worldCenter, Sprite sprite, Vector3 visualScale)
        {
            if (sceneCamera == null || sprite == null)
            {
                return Rect.zero;
            }

            Vector2 spriteWorldSize = sprite.bounds.size;
            float worldWidth = Mathf.Abs(spriteWorldSize.x * visualScale.x);
            float worldHeight = Mathf.Abs(spriteWorldSize.y * visualScale.y);
            if (worldWidth <= 0f || worldHeight <= 0f)
            {
                return Rect.zero;
            }

            Vector2 center = WorldToGuiPoint(worldCenter);
            float guiWidth = Mathf.Abs(WorldToGuiPoint(worldCenter + Vector3.right * worldWidth).x - center.x);
            float guiHeight = Mathf.Abs(WorldToGuiPoint(worldCenter + Vector3.up * worldHeight).y - center.y);
            return new Rect(center.x - guiWidth * 0.5f, center.y - guiHeight * 0.5f, guiWidth, guiHeight);
        }

        private void DrawGuiLine(Vector2 pointA, Vector2 pointB, float thickness)
        {
            Matrix4x4 matrix = GUI.matrix;
            float angle = Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(pointA, pointB);
            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.DrawTexture(new Rect(pointA.x, pointA.y - thickness * 0.5f, length, thickness), lineTexture);
            GUI.matrix = matrix;
        }

        private void DrawWorldCircle(Vector3 center, float radius, Color color, float thickness, int segments)
        {
            if (radius <= 0f || sceneCamera == null)
            {
                return;
            }

            GUI.color = color;
            Vector2 previous = WorldToGuiPoint(center + new Vector3(radius, 0f, 0f));
            for (int i = 1; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                Vector2 next = WorldToGuiPoint(center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                DrawGuiLine(previous, next, thickness);
                previous = next;
            }

            GUI.color = Color.white;
        }

        private void DrawGuiCircle(Vector2 center, float radius, Color color, float thickness, int segments)
        {
            GUI.color = color;
            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                DrawGuiLine(previous, next, thickness);
                previous = next;
            }

            GUI.color = Color.white;
        }

        private float DrawTilePaletteGrid(List<TileBase> tiles, int selectedIndex, float y, float width, Action<int> onSelect, Action<int> onDelete)
        {
            if (tiles.Count == 0)
            {
                GUI.Label(new Rect(0f, y, width, 46f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoTileAvailable), warningStyle);
                return y + 56f;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (PaletteTileSize + 10f)));
            int pendingDeleteIndex = -1;
            for (int i = 0; i < tiles.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(column * (PaletteTileSize + 10f), y + row * (PaletteTileSize + 22f), PaletteTileSize, PaletteTileSize + 16f);
                if (IsRightClickDeleteRequested(cellRect))
                {
                    pendingDeleteIndex = i;
                }

                if (GUI.Button(cellRect, GUIContent.none, i == selectedIndex ? selectedButtonStyle : buttonStyle))
                {
                    onSelect(i);
                }

                Rect imageRect = new Rect(cellRect.x + 8f, cellRect.y + 8f, PaletteTileSize - 16f, PaletteTileSize - 16f);
                DrawTilePreview(imageRect, tiles[i]);
                GUI.Label(new Rect(cellRect.x + 4f, cellRect.y + PaletteTileSize - 2f, PaletteTileSize - 8f, 18f), Truncate(GetDisplayName(tiles[i]), 5), smallBodyStyle);
            }

            int rows = Mathf.CeilToInt((float)tiles.Count / columns);
            if (pendingDeleteIndex >= 0 && onDelete != null)
            {
                onDelete(pendingDeleteIndex);
            }

            return y + rows * (PaletteTileSize + 22f);
        }

        private float DrawPrefabPaletteGrid(List<GameObject> prefabs, int selectedIndex, float y, float width, Action<int> onSelect, Action<int> onDelete)
        {
            if (prefabs.Count == 0)
            {
                GUI.Label(new Rect(0f, y, width, 46f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoObjectAvailable), warningStyle);
                return y + 56f;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (PaletteTileSize + 10f)));
            int pendingDeleteIndex = -1;
            for (int i = 0; i < prefabs.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(column * (PaletteTileSize + 10f), y + row * (PaletteTileSize + 22f), PaletteTileSize, PaletteTileSize + 16f);
                if (IsRightClickDeleteRequested(cellRect))
                {
                    pendingDeleteIndex = i;
                }

                if (GUI.Button(cellRect, GUIContent.none, i == selectedIndex ? selectedButtonStyle : buttonStyle))
                {
                    onSelect(i);
                }

                Rect imageRect = new Rect(cellRect.x + 8f, cellRect.y + 8f, PaletteTileSize - 16f, PaletteTileSize - 16f);
                DrawPrefabPreview(imageRect, prefabs[i]);
                GUI.Label(new Rect(cellRect.x + 4f, cellRect.y + PaletteTileSize - 2f, PaletteTileSize - 8f, 18f), Truncate(GetObjectDisplayName(prefabs[i]), 5), smallBodyStyle);
            }

            int rows = Mathf.CeilToInt((float)prefabs.Count / columns);
            if (pendingDeleteIndex >= 0 && onDelete != null)
            {
                onDelete(pendingDeleteIndex);
            }

            return y + rows * (PaletteTileSize + 22f);
        }

        private bool IsRightClickDeleteRequested(Rect rect)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 1)
            {
                return false;
            }

            if (!rect.Contains(current.mousePosition))
            {
                return false;
            }

            current.Use();
            return true;
        }

        private void DrawModeButtonRow(ref float y, float width, CampusRuntimeBrushMode[] modes, string[] labels)
        {
            float gap = 8f;
            float buttonWidth = (width - gap * (modes.Length - 1)) / modes.Length;
            for (int i = 0; i < modes.Length; i++)
            {
                Rect rect = new Rect(i * (buttonWidth + gap), y, buttonWidth, 32f);
                if (GUI.Button(rect, labels[i], brushMode == modes[i] ? selectedButtonStyle : buttonStyle))
                {
                    brushMode = modes[i];
                }
            }

            y += 40f;
        }

        private void DrawAreaDefinitionRow(Rect rect, string roomName, int count, int required)
        {
            DrawAreaColorSwatch(new Rect(rect.x + 10f, rect.y + 8f, 18f, 18f), roomName);
            GUI.Label(new Rect(rect.x + 38f, rect.y + 4f, rect.width - 108f, 26f), GetAreaPresetLabel(roomName), bodyStyle);
            GUI.Label(new Rect(rect.xMax - 68f, rect.y + 4f, 60f, 26f), count.ToString(), count >= required ? bodyStyle : warningStyle);
        }

        private void DrawAreaColorSwatch(Rect rect, string roomName)
        {
            if (lineTexture == null)
            {
                return;
            }

            Color color = CampusRuntimeGameplayOverlayPalette.ResolveRoomNameColor(roomName);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, lineTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), lineTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), lineTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), lineTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), lineTexture);
            GUI.color = oldColor;
        }

        private void DrawGameplayPresetSection(ref float y, float width)
        {
            float gap = 8f;
            float buttonWidth = (width - gap) * 0.5f;
            int column = 0;
            for (int i = 0; i < CampusRuntimeGameplayMarkerPresetCatalog.Presets.Length; i++)
            {
                CampusRuntimeGameplayMarkerPreset preset = CampusRuntimeGameplayMarkerPresetCatalog.Presets[i];
                if (preset == null)
                {
                    continue;
                }

                Rect rect = new Rect(column * (buttonWidth + gap), y, buttonWidth, 30f);
                Color oldColor = GUI.color;
                GUI.color = selectedGameplayPresetIndex == i
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.92f);
                if (GUI.Button(rect, GetGameplayPresetLabel(preset), selectedGameplayPresetIndex == i ? selectedButtonStyle : buttonStyle))
                {
                    selectedGameplayPresetIndex = i;
                    brushMode = CampusRuntimeBrushMode.PlaceGameplayMarker;
                }

                GUI.color = oldColor;
                column++;
                if (column >= 2)
                {
                    column = 0;
                    y += 38f;
                }
            }

            if (column != 0)
            {
                y += 38f;
            }

            y += 10f;
        }

        private void DrawGameplayOwnerSelectionSection(ref float y, float width)
        {
            CampusRuntimeGameplayMarkerPreset preset = GetSelectedGameplayPreset();
            if (preset == null || !preset.RequiresOwnerFacility)
            {
                return;
            }

            GUI.Label(new Rect(0f, y, width, 26f), Tr("\u6240\u5c5e\u670d\u52a1\u7a97\u53e3", "Owner Service Window"), headerStyle);
            y += 34f;

            List<CampusGameplayFacilityMarker> candidates =
                CollectGameplayFacilityOwnerCandidates(preset, Mathf.Max(1, selectedFloorIndex));
            if (candidates.Count == 0)
            {
                GUI.Label(
                    new Rect(0f, y, width, 58f),
                    Tr("\u5f53\u524d\u697c\u5c42\u8fd8\u6ca1\u6709\u53ef\u6302\u63a5\u7684\u670d\u52a1\u7a97\u53e3\u3002\u5148\u653e\u4e00\u4e2a\u670d\u52a1\u7a97\u53e3\uff0c\u518d\u653e\u652f\u6491\u70b9\u3002", "No service window is available on the current floor yet. Place a service window first, then place support points."),
                    mutedStyle);
                y += 66f;
                selectedFacilityOwnerId = string.Empty;
                return;
            }

            string normalizedSelectedOwnerId =
                CampusGameplayFacilityMarker.NormalizeFacilityId(selectedFacilityOwnerId);
            bool hasSelectedOwner = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                CampusGameplayFacilityMarker candidate = candidates[i];
                string candidateFacilityId = ResolveGameplayFacilityMarkerId(candidate);
                bool isSelected =
                    !string.IsNullOrEmpty(normalizedSelectedOwnerId) &&
                    string.Equals(
                        candidateFacilityId,
                        normalizedSelectedOwnerId,
                        StringComparison.OrdinalIgnoreCase);
                if (isSelected)
                {
                    hasSelectedOwner = true;
                }

                Rect rowRect = new Rect(0f, y, width, 30f);
                if (GUI.Button(rowRect, GetGameplayFacilityOwnerLabel(candidate), isSelected ? selectedButtonStyle : buttonStyle))
                {
                    selectedFacilityOwnerId = candidateFacilityId;
                    hasSelectedOwner = true;
                }

                y += 36f;
            }

            if (!hasSelectedOwner)
            {
                selectedFacilityOwnerId = string.Empty;
            }

            GUI.Label(
                new Rect(0f, y, width, 40f),
                string.IsNullOrWhiteSpace(selectedFacilityOwnerId)
                    ? Tr("\u5f53\u524d\u70b9\u4f4d\u9700\u8981\u5148\u5728\u670d\u52a1\u7ad9\u5b9e\u4f8b\u4e2d\u58f0\u660e\u6240\u5c5e\u8bbe\u65bd\u3002", "Declare this point through a service station instance before it is used.")
                    : Tr("\u5f53\u524d\u670d\u52a1\u7ad9\u6240\u5c5e\u8bbe\u65bd\u5df2\u9009\u4e2d\u3002", "The service station owner facility is selected."),
                mutedStyle);
            y += 48f;
        }

        private void DrawGameplayActorPresetSection(ref float y, float width)
        {
            float gap = 8f;
            float buttonWidth = (width - gap) * 0.5f;
            int column = 0;
            for (int i = 0; i < CampusRuntimeGameplayActorPresetCatalog.Presets.Length; i++)
            {
                CampusRuntimeGameplayActorPreset preset = CampusRuntimeGameplayActorPresetCatalog.Presets[i];
                if (preset == null)
                {
                    continue;
                }

                Rect rect = new Rect(column * (buttonWidth + gap), y, buttonWidth, 30f);
                Color oldColor = GUI.color;
                GUI.color = selectedGameplayActorPresetIndex == i
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.92f);
                if (GUI.Button(rect, GetGameplayActorPresetLabel(preset), selectedGameplayActorPresetIndex == i ? selectedButtonStyle : buttonStyle))
                {
                    selectedGameplayActorPresetIndex = i;
                    brushMode = CampusRuntimeBrushMode.PlaceGameplayActor;
                    if (string.IsNullOrWhiteSpace(newGameplayActorClassId))
                    {
                        newGameplayActorClassId = preset.ClassId;
                    }
                }

                GUI.color = oldColor;
                column++;
                if (column >= 2)
                {
                    column = 0;
                    y += 38f;
                }
            }

            if (column != 0)
            {
                y += 38f;
            }

            y += 8f;
        }

        private void DrawGameplayActorFields(ref float y, float width)
        {
            GUI.Label(new Rect(0f, y, width, 42f), Tr("\u4e0b\u9762\u90fd\u53ef\u7559\u7a7a\uff1a\u7559\u7a7a\u65f6\u81ea\u52a8\u751f\u6210\u552f\u4e00 ID \u548c\u540d\u5b57\u3002\u628a\u5458\u5de5\u653e\u5728\u5bf9\u5e94\u8bbe\u65bd\u70b9\u9644\u8fd1\u4f1a\u81ea\u52a8\u7ed1\u5b9a\u5de5\u4f4d\u3002", "All fields below are optional. Empty fields generate a unique ID/name. Placing staff near matching facility points auto-binds their workstation."), mutedStyle);
            y += 50f;

            GUI.Label(new Rect(0f, y, 70f, 30f), "ID", bodyStyle);
            newGameplayActorId = DrawTextInput(new Rect(72f, y, width - 72f, 30f), newGameplayActorId, "gameplay_actor_id");
            y += 38f;

            GUI.Label(new Rect(0f, y, 70f, 30f), Tr("\u4e2d\u6587\u540d", "CN Name"), bodyStyle);
            newGameplayActorChineseName = DrawTextInput(new Rect(72f, y, width - 72f, 30f), newGameplayActorChineseName, "gameplay_actor_cn_name");
            y += 38f;

            GUI.Label(new Rect(0f, y, 70f, 30f), Tr("\u82f1\u6587\u540d", "EN Name"), bodyStyle);
            newGameplayActorEnglishName = DrawTextInput(new Rect(72f, y, width - 72f, 30f), newGameplayActorEnglishName, "gameplay_actor_en_name");
            y += 38f;

            GUI.Label(new Rect(0f, y, 70f, 30f), Tr("\u73ed\u7ea7", "Class"), bodyStyle);
            newGameplayActorClassId = DrawTextInput(new Rect(72f, y, width - 72f, 30f), newGameplayActorClassId, "gameplay_actor_class_id");
            y += 42f;
        }

        private float DrawSlider(float y, float width, string label, ref float value, float min, float max)
        {
            GUI.Label(new Rect(0f, y, 64f, 24f), label, bodyStyle);
            value = GUI.HorizontalSlider(new Rect(70f, y + 8f, width - 126f, 18f), value, min, max);
            GUI.Label(new Rect(width - 48f, y, 48f, 24f), value.ToString("0.0"), smallBodyStyle);
            return y + 30f;
        }

        private void DrawLightPreviewCard(ref float y, float width)
        {
            Rect rect = new Rect(0f, y + 4f, width, 112f);
            GUI.Box(rect, GUIContent.none, buttonStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.LightPreview), bodyStyle);
            Rect swatch = new Rect(rect.x + 14f, rect.y + 42f, 32f, 32f);
            Color oldColor = GUI.color;
            GUI.color = lightColor;
            GUI.DrawTexture(swatch, lineTexture);
            GUI.color = oldColor;

            Vector2 center = new Vector2(rect.x + rect.width - 70f, rect.y + 60f);
            float outer = 38f;
            float inner = Mathf.Clamp(lightInnerRadius / Mathf.Max(0.1f, lightOuterRadius), 0f, 1f) * outer;
            DrawGuiCircle(center, outer, new Color(1f, 0.88f, 0.35f, 0.88f), 2f, 40);
            DrawGuiCircle(center, Mathf.Max(4f, inner), new Color(1f, 0.96f, 0.65f, 0.8f), 1f, 32);
            GUI.Label(new Rect(rect.x + 54f, rect.y + 39f, rect.width - 140f, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PointLight), bodyStyle);
            GUI.Label(new Rect(rect.x + 54f, rect.y + 66f, rect.width - 140f, 24f), CampusRuntimeEditorTextCatalog.FormatPointLightStats(displayLanguage, lightIntensity.ToString("0.0"), lightOuterRadius.ToString("0.0"), lightInnerRadius.ToString("0.0")), mutedStyle);
            y += 124f;
        }

        private void DrawDayNightControls(ref float y, float width)
        {
            if (dayNightController == null)
            {
                dayNightController = FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }

            GUI.Label(new Rect(0f, y, width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.DayNight), headerStyle);
            y += 34f;

            if (dayNightController == null)
            {
                GUI.Label(new Rect(0f, y, width, 40f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.MissingDayNightController), warningStyle);
                y += 48f;
                return;
            }

            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.FormatGameTime(displayLanguage, FormatGameTime(dayNightController.GameHour)), bodyStyle);
            y += 30f;

            float speed = dayNightController.DaySpeedMultiplier;
            float editedSpeed = speed;
            y = DrawSlider(y, width, Tr("\u901f\u5ea6", "Speed"), ref editedSpeed, 0.1f, 200f);
            if (!Mathf.Approximately(speed, editedSpeed))
            {
                dayNightController.DaySpeedMultiplier = editedSpeed;
            }

            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.Format(displayLanguage, CampusRuntimeEditorTextId.RealMinutesPerGameDay, dayNightController.RealMinutesPerGameDay.ToString("0.0")), mutedStyle);
            y += 30f;

            float halfWidth = (width - 8f) * 0.5f;
            if (GUI.Button(new Rect(0f, y, halfWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Set1x), buttonStyle))
            {
                dayNightController.DaySpeedMultiplier = 1f;
            }

            if (GUI.Button(new Rect(halfWidth + 8f, y, halfWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Set200x), buttonStyle))
            {
                dayNightController.DaySpeedMultiplier = 200f;
            }

            y += 42f;
        }

        private static string FormatGameTime(float gameHour)
        {
            gameHour = Mathf.Repeat(gameHour, 24f);
            int hour = Mathf.FloorToInt(gameHour);
            int minute = Mathf.FloorToInt((gameHour - hour) * 60f);
            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        private void DrawCustomWallPanel(ref float y, float width)
        {
            Rect rect = new Rect(0f, y, width, 238f);
            GUI.Box(rect, GUIContent.none, buttonStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.CustomWall), headerStyle);

            float rowY = rect.y + 42f;
            DrawWallTexturePicker(rect.x + 12f, rowY, rect.width - 24f, Tr("\u5899\u9762\u8d34\u56fe", "Wall Face Texture"), customWallFaceTexture, CampusRuntimeImportTarget.WallFace);
            rowY += 56f;
            DrawWallTexturePicker(rect.x + 12f, rowY, rect.width - 24f, Tr("\u5899\u9876\u8d34\u56fe", "Wall Cap Texture"), customWallCapTexture, CampusRuntimeImportTarget.WallCap);
            rowY += 58f;

            GUI.Label(new Rect(rect.x + 12f, rowY, 78f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Name), bodyStyle);
            customWallName = GUI.TextField(new Rect(rect.x + 92f, rowY, rect.width - 104f, 30f), customWallName, buttonStyle);
            rowY += 40f;

            float buttonWidth = (rect.width - 36f) / 2f;
            if (GUI.Button(new Rect(rect.x + 12f, rowY, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.CreateProfile), buttonStyle))
            {
                CreateCustomWallProfile();
            }

            if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, rowY, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ApplyToSelected), buttonStyle))
            {
                ApplyCustomTexturesToSelectedWall();
            }

            rowY += 36f;
            if (GUI.Button(new Rect(rect.x + 12f, rowY, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RebuildSelectedFloor), buttonStyle))
            {
                RebuildWallVisuals(EnsureFloor(selectedFloorIndex));
                SchedulePlayerMapSave();
            }

            if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, rowY, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RefreshPresentation), buttonStyle))
            {
                PrepareRuntimeMapPresentationSafe();
            }

            y += rect.height + 12f;
        }

                private void DrawWallTexturePicker(float x, float y, float width, string label, Texture2D texture, CampusRuntimeImportTarget target)
        {
            GUI.Label(new Rect(x, y + 12f, 82f, 28f), label, bodyStyle);
            Rect preview = new Rect(x + width - 48f, y + 4f, 44f, 44f);
            GUI.DrawTexture(preview, texture != null ? texture : tileFallbackTexture, ScaleMode.ScaleToFit);
            float buttonX = x + 86f;
            float buttonWidth = Mathf.Max(62f, (width - 146f) / 2f);
            if (GUI.Button(new Rect(buttonX, y + 10f, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ChooseFile), buttonStyle))
            {
                string path = SelectSingleImageFile(label);
                if (!string.IsNullOrEmpty(path))
                {
                    LoadCustomWallTexture(path, target);
                }
            }

            GUIStyle targetStyle = activeImportTarget == target ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect(buttonX + buttonWidth + 8f, y + 10f, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.UseDragTarget), targetStyle))
            {
                SetActiveImportTarget(target, label);
            }
        }
        private void DrawSelectedObjectSettingsLauncher(ref float y, float width)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            string name = prefab != null ? GetObjectDisplayName(prefab) : Tr("\u672a\u9009\u7269\u54c1", "No Object Selected");
            CampusRuntimeMapEditorObjectSettingsLauncherReadModel model =
                CampusRuntimeMapEditorReadModelBuilder.BuildObjectSettingsLauncher(
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ObjectSettings),
                    Truncate(name, 18),
                    prefab != null,
                    showObjectSettings);
            CampusRuntimeMapEditorObjectSettingsLauncherInteraction interaction =
                CampusRuntimeMapEditorChromePresenter.DrawObjectSettingsLauncher(
                    new Rect(0f, y, width, 34f),
                    objectSettingsHighlightStyle,
                    buttonStyle,
                    selectedButtonStyle,
                    mutedStyle,
                    model);
            if (interaction.ToggleRequested)
            {
                showObjectSettings = !showObjectSettings;
                if (showObjectSettings)
                {
                    CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
                    objectSettingsSession.SyncSelection(prefab, placed, true, SyncSelectedObjectFootprintFields);
                }
            }

            y += 46f;
        }

        private void DrawObjectSettingsPanel()
        {
            if (!showObjectSettings)
            {
                return;
            }

            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            string warningMessage = string.Empty;
            bool canEdit = true;
            if (prefab == null)
            {
                canEdit = false;
                warningMessage = CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SelectObjectFirst);
            }
            else if (placed == null)
            {
                canEdit = false;
                warningMessage = CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.MissingCampusPlacedObject);
            }

            CampusRuntimeMapEditorObjectSettingsPanelReadModel model =
                CampusRuntimeMapEditorReadModelBuilder.BuildObjectSettingsPanel(
                    showObjectSettings,
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ObjectSettings),
                    warningMessage,
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SaveAndSync),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ApplyToAllSameType),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SyncPlacedObjects),
                    canEdit);
            CampusRuntimeMapEditorObjectSettingsPanelInteraction interaction =
                CampusRuntimeMapEditorChromePresenter.DrawObjectSettingsPanelChrome(
                    objectSettingsPanelRect,
                    panelStyle,
                    objectSettingsHighlightStyle,
                    headerStyle,
                    buttonStyle,
                    warningStyle,
                    mutedStyle,
                    model);
            if (interaction.CloseRequested)
            {
                showObjectSettings = false;
                return;
            }

            if (!canEdit)
            {
                return;
            }

            objectSettingsSession.SyncSelection(prefab, placed, false, SyncSelectedObjectFootprintFields);
            if (interaction.SaveAndSyncRequested)
            {
                CampusRuntimeMapEditorObjectSettingsCommandService.CommitDraft(placed);
                SaveSelectedObjectSettings();
            }

            if (interaction.ApplyToAllRequested)
            {
                CampusRuntimeMapEditorObjectSettingsCommandService.CommitDraft(placed);
                ApplySelectedObjectSettingsToPlacedInstances();
            }

            Rect scrollRect = interaction.ScrollRect;
            float viewWidth = interaction.ViewWidth;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(scrollRect.height + 1f, 1700f));
            objectSettingsSession.ScrollPosition = GUI.BeginScrollView(scrollRect, objectSettingsSession.ScrollPosition, viewRect);
            float y = 0f;

            CampusRuntimeMapEditorObjectSettingsInspectorPresenter.DrawContents(
                this,
                ref y,
                viewWidth,
                prefab,
                placed);
            GUI.EndScrollView();
        }

        private void DrawColorControls(ref float y, float width, string label, ref Color color)
        {
            GUI.Label(new Rect(0f, y, width, 24f), label, bodyStyle);
            Rect swatch = new Rect(width - 38f, y + 2f, 32f, 22f);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(swatch, lineTexture);
            GUI.color = oldColor;
            y += 28f;
            color.r = DrawColorChannel(y, width, "R", color.r);
            y += 22f;
            color.g = DrawColorChannel(y, width, "G", color.g);
            y += 22f;
            color.b = DrawColorChannel(y, width, "B", color.b);
            color.a = 1f;
            y += 22f;
        }

        private float DrawColorChannel(float y, float width, string label, float value)
        {
            GUI.Label(new Rect(0f, y, 22f, 20f), label, smallBodyStyle);
            value = GUI.HorizontalSlider(new Rect(28f, y + 6f, width - 78f, 16f), value, 0f, 1f);
            GUI.Label(new Rect(width - 44f, y, 44f, 20f), Mathf.RoundToInt(value * 255f).ToString(), smallBodyStyle);
            return value;
        }

        private void DrawImportFolderRow(ref float y, float width, string label, string folder, CampusRuntimeImportTarget target)
        {
            GUI.Label(new Rect(0f, y, width, 26f), label, headerStyle);
            y += 32f;
            float buttonWidth = Mathf.Max(68f, (width - 24f) / 4f);
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ImportFiles), buttonStyle))
            {
                ImportSelectedFilesIntoFolder(folder, label);
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SelectFolder), buttonStyle))
            {
                ImportSelectedFolderIntoFolder(folder, label);
            }

            GUIStyle targetStyle = activeImportTarget == target ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect((buttonWidth + 8f) * 2f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.DropTarget), targetStyle))
            {
                SetActiveImportTarget(target, label);
            }

            if (GUI.Button(new Rect((buttonWidth + 8f) * 3f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PasteImage), buttonStyle))
            {
                ImportClipboardImagesIntoFolder(folder, label);
            }

            y += 36f;
            if (GUI.Button(new Rect(0f, y, buttonWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.OpenFolder), buttonStyle))
            {
                OpenImportLocation(folder);
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Refresh), buttonStyle))
            {
                ReloadUserImportsFromUi();
                SchedulePlayerMapSave();
            }

            GUI.Label(new Rect((buttonWidth + 8f) * 2f, y, width - (buttonWidth + 8f) * 2f, 28f), activeImportTarget == target ? CampusRuntimeEditorTextCatalog.FormatActiveDropTarget(displayLanguage, label) : Truncate(folder, 22), activeImportTarget == target ? warningStyle : mutedStyle);
            y += 38f;
        }

        private void DrawImportFileRow(ref float y, float width, string label, string filePath)
        {
            GUI.Label(new Rect(0f, y, width, 26f), label, headerStyle);
            y += 32f;
            float buttonWidth = Mathf.Max(68f, (width - 24f) / 4f);
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ImportText), buttonStyle))
            {
                string path = SelectSingleFile(CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SelectRoomDefinitionText), "Text|*.txt|All|*.*");
                if (!string.IsNullOrEmpty(path))
                {
                    RecordUndo();
                    int count = ImportRoomDefinitionsFromText(File.ReadAllText(path));
                    SetStatus(count > 0
                        ? CampusRuntimeEditorTextCatalog.Format(displayLanguage, CampusRuntimeEditorTextId.ImportRoomTypesStatus, count)
                        : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoRoomTypesFoundToImport));
                }
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PasteText), buttonStyle))
            {
                RecordUndo();
                int count = ImportRoomDefinitionsFromText(GUIUtility.systemCopyBuffer ?? string.Empty);
                SetStatus(count > 0
                    ? CampusRuntimeEditorTextCatalog.Format(displayLanguage, CampusRuntimeEditorTextId.ImportRoomTypesClipboardStatus, count)
                    : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoRoomTypesFoundInClipboard));
            }

            GUIStyle targetStyle = activeImportTarget == CampusRuntimeImportTarget.Room ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect((buttonWidth + 8f) * 2f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.DropTarget), targetStyle))
            {
                SetActiveImportTarget(CampusRuntimeImportTarget.Room, label);
            }

            if (GUI.Button(new Rect((buttonWidth + 8f) * 3f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.OpenFile), buttonStyle))
            {
                OpenImportLocation(filePath);
            }

            y += 36f;
            if (GUI.Button(new Rect(0f, y, buttonWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Refresh), buttonStyle))
            {
                LoadImportedRoomDefinitions();
            }

            GUI.Label(new Rect(buttonWidth + 8f, y, width - buttonWidth - 8f, 28f), activeImportTarget == CampusRuntimeImportTarget.Room ? CampusRuntimeEditorTextCatalog.FormatActiveDropTarget(displayLanguage, label) : Truncate(filePath, 24), activeImportTarget == CampusRuntimeImportTarget.Room ? warningStyle : mutedStyle);
            y += 38f;
        }

        private void SetActiveImportTarget(CampusRuntimeImportTarget target, string label)
        {
            activeImportTarget = target;
            activeImportLabel = label;
            SetStatus(TrFormat("\u62d6\u653e\u76ee\u6807\uff1a{0}\u3002\u5c06\u6587\u4ef6\u6216\u6587\u4ef6\u5939\u62d6\u5165\u6e38\u620f\u89c6\u56fe\u3002", "Drag target: {0}. Drag files or folders into the game view.", label));
        }

        private void SetActiveObjectDirectionSpriteDropTarget(int rotation90Index)
        {
            objectSettingsSession.SetDirectionDropTarget(rotation90Index);
            SetStatus(TrFormat("\u62d6\u653e\u76ee\u6807\uff1a\u7269\u4ef6 {0} \u5ea6\u8d34\u56fe\u3002", "Drag target: object {0} deg sprite.", CampusPlacedObject.NormalizeRotation90(rotation90Index) * 90));
        }

        private bool TryImportDroppedObjectDirectionSprite(List<string> paths)
        {
            int targetRotation90 = ResolveDroppedObjectDirectionTargetRotation90();
            if (targetRotation90 < 0)
            {
                return false;
            }

            string sourcePath = ResolveFirstDroppedImagePath(paths);
            if (string.IsNullOrEmpty(sourcePath))
            {
                SetStatus(Tr("\u8bf7\u9009\u62e9 png/jpg/jpeg/bmp \u56fe\u7247\u3002", "Choose a png/jpg/jpeg/bmp image."));
                objectSettingsSession.ClearDirectionDropTarget();
                return true;
            }

            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (placed != null && placed.IsWallMounted)
            {
                TryAssignSelectedWallMountedSprite(sourcePath);
            }
            else
            {
                TryAssignSelectedObjectDirectionSprite(targetRotation90, sourcePath);
            }

            objectSettingsSession.ClearDirectionDropTarget();
            return true;
        }

        private int ResolveDroppedObjectDirectionTargetRotation90()
        {
            return objectSettingsSession.ResolveDroppedDirectionTargetRotation(
                showObjectSettings,
                IsMouseOverObjectSettingsPanel(),
                GetSelectedObjectPrefab() != null);
        }

        private bool IsMouseOverObjectSettingsPanel()
        {
            if (!showObjectSettings)
            {
                return false;
            }

            Vector2 mouseScreenPosition =
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                Mouse.current != null ? Mouse.current.position.ReadValue() : Input.mousePosition;
#else
                Input.mousePosition;
#endif
            Vector2 guiPosition = new Vector2(mouseScreenPosition.x, Screen.height - mouseScreenPosition.y);
            return objectSettingsPanelRect.Contains(guiPosition);
        }

        private string ResolveFirstDroppedImagePath(List<string> paths)
        {
            if (paths == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (File.Exists(path) && CampusRuntimeImportLibrary.IsSupportedImage(path))
                {
                    return path;
                }

                if (Directory.Exists(path))
                {
                    string[] files = CampusRuntimeImportLibrary.GetImageFiles(path);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
            }

            return string.Empty;
        }

        private void LoadCustomWallTexture(string path, CampusRuntimeImportTarget target)
        {
            if (!CampusRuntimeImportLibrary.IsSupportedImage(path))
            {
                SetStatus(Tr("\u8bf7\u9009\u62e9 png/jpg/jpeg/bmp \u56fe\u7247\u3002", "Choose a png/jpg/jpeg/bmp image."));
                return;
            }

            Texture2D texture = LoadImportedTexture(path);
            if (texture == null)
            {
                return;
            }

            if (target == CampusRuntimeImportTarget.WallFace)
            {
                customWallFaceTexture = texture;
            }
            else if (target == CampusRuntimeImportTarget.WallCap)
            {
                customWallCapTexture = texture;
            }
        }

        private void CreateCustomWallProfile()
        {
            Texture2D face = customWallFaceTexture != null ? customWallFaceTexture : customWallCapTexture;
            Texture2D cap = customWallCapTexture != null ? customWallCapTexture : customWallFaceTexture;
            if (face == null && cap == null)
            {
                SetStatus(Tr("\u8bf7\u5148\u9009\u62e9\u5899\u9762\u6216\u5899\u9876\u8d34\u56fe\u3002", "Choose a wall face or wall cap texture first."));
                return;
            }

            string cleanName = string.IsNullOrWhiteSpace(customWallName) ? "CustomWall" : customWallName.Trim();
            Sprite sprite = Sprite.Create(cap != null ? cap : face, new Rect(0f, 0f, (cap != null ? cap.width : face.width), (cap != null ? cap.height : face.height)), new Vector2(0.5f, 0.5f), Mathf.Max(1f, Mathf.Max(cap != null ? cap.width : face.width, cap != null ? cap.height : face.height)));
            sprite.name = cleanName + "_Preview";
            sprite.hideFlags = HideFlags.DontSave;
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = cleanName + "_WallLogic";
            tile.sprite = sprite;

            CampusWallRenderProfile profile = ScriptableObject.CreateInstance<CampusWallRenderProfile>();
            profile.name = cleanName + " Wall Profile";
            profile.ProfileId = cleanName;
            profile.FaceSourceTexture = face;
            profile.CapSourceTexture = cap;
            profile.LogicTile = tile;
            profile.hideFlags = HideFlags.DontSave;

            importedAssets.Add(sprite);
            importedAssets.Add(tile);
            importedAssets.Add(profile);
            AddUnique(runtimeCustomWallTiles, tile);
            AddUnique(runtimeCustomWallProfiles, profile);
            AddUnique(wallTiles, tile);
            AddUnique(wallProfiles, profile);
            EnsureRuntimeWallCatalog(profile);

            selectedWallTileIndex = wallTiles.IndexOf(tile);
            selectedWallProfileIndex = wallProfiles.IndexOf(profile);
            fallbackWallProfile = profile;
            brushMode = CampusRuntimeBrushMode.PaintWall;
            RebuildWallVisuals(EnsureFloor(selectedFloorIndex));
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u521b\u5efa\u5899\u4f53\u914d\u7f6e\uff1a{0}", "Created wall profile: {0}", cleanName));
        }

        private void ApplyCustomTexturesToSelectedWall()
        {
            CampusWallRenderProfile profile = selectedWallProfileIndex >= 0 && selectedWallProfileIndex < wallProfiles.Count ? wallProfiles[selectedWallProfileIndex] : fallbackWallProfile;
            if (profile == null)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u7528\u5899\u4f53\u914d\u7f6e\u3002", "No wall profile is available."));
                return;
            }

            if (customWallFaceTexture != null)
            {
                profile.FaceSourceTexture = customWallFaceTexture;
            }

            if (customWallCapTexture != null)
            {
                profile.CapSourceTexture = customWallCapTexture;
            }

            RebuildWallVisuals(EnsureFloor(selectedFloorIndex));
            SchedulePlayerMapSave();
            SetStatus(Tr("\u5899\u4f53\u8d34\u56fe\u5df2\u5e94\u7528\u3002", "Wall textures applied."));
        }

        private void EnsureRuntimeWallCatalog(CampusWallRenderProfile profile)
        {
            if (wallVisualCatalog == null)
            {
                wallVisualCatalog = ScriptableObject.CreateInstance<CampusWallVisualCatalog>();
                wallVisualCatalog.name = "Runtime Wall Visual Catalog";
                wallVisualCatalog.hideFlags = HideFlags.DontSave;
                wallVisualCatalog.Profiles = new List<CampusWallRenderProfile>();
                importedAssets.Add(wallVisualCatalog);
            }

            if (wallVisualCatalog.DefaultProfile == null)
            {
                wallVisualCatalog.DefaultProfile = fallbackWallProfile != null ? fallbackWallProfile : profile;
            }

            if (wallVisualCatalog.Profiles == null)
            {
                wallVisualCatalog.Profiles = new List<CampusWallRenderProfile>();
            }

            if (!wallVisualCatalog.Profiles.Contains(profile))
            {
                wallVisualCatalog.Profiles.Add(profile);
            }
        }

        private void SyncSelectedObjectFootprintFields()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            if (objectSettingsSession.IsFootprintSynced(prefab))
            {
                return;
            }

            objectSettingsSession.MarkFootprintSynced(prefab);
            CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            if (placed == null)
            {
                selectedObjectFootprintX = Mathf.Max(1, selectedObjectFootprintX);
                selectedObjectFootprintY = Mathf.Max(1, selectedObjectFootprintY);
                return;
            }

            selectedObjectFootprintX = Mathf.Max(1, placed.NormalizedFootprintSize.x);
            selectedObjectFootprintY = Mathf.Max(1, placed.NormalizedFootprintSize.y);
        }

        private void ApplySelectedObjectFootprint()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            if (prefab == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return;
            }

            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (placed == null)
            {
                return;
            }

            placed.FootprintSize = new Vector2Int(Mathf.Clamp(selectedObjectFootprintX, 1, 32), Mathf.Clamp(selectedObjectFootprintY, 1, 32));
            placed.OverrideFootprintSize = true;
            objectSettingsSession.MarkFootprintSynced(prefab);
            SaveSelectedObjectSettings();
            SetStatus(TrFormat("\u5df2\u5e94\u7528\u5360\u5730\u5c3a\u5bf8\uff1a{0}x{1}", "Applied footprint size: {0}x{1}", placed.FootprintSize.x, placed.FootprintSize.y));
        }

        private void ConfigureWallMountedSettings(CampusPlacedObject placed, bool enabled, bool clearDirectionalOverrides)
        {
            if (placed == null)
            {
                return;
            }

            placed.IsWallMounted = enabled;
            if (enabled)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                placed.OverrideFootprintSize = true;
                placed.FootprintSize = Vector2Int.one;
                placed.SortingOrderOffset = Mathf.Max(placed.SortingOrderOffset, 1);
                placed.BlocksMovement = false;
                placed.BlocksSight = false;
                if (clearDirectionalOverrides)
                {
                    AssignRuntimeObjectDirectionSprite(placed, 1, false, string.Empty, placed.ObjectId);
                    AssignRuntimeObjectDirectionSprite(placed, 2, false, string.Empty, placed.ObjectId);
                    AssignRuntimeObjectDirectionSprite(placed, 3, false, string.Empty, placed.ObjectId);
                }
            }

            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
        }

        private void SetSelectedWallMountedSprite()
        {
            string sourcePath = SelectSingleImageFile(Tr("\u9009\u62e9\u58c1\u6302\u4e3b\u8d34\u56fe", "Choose Wall-Mounted Main Sprite"));
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            TryAssignSelectedWallMountedSprite(sourcePath);
        }

        private bool TryAssignSelectedWallMountedSprite(string sourcePath)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return false;
            }

            if (!CampusRuntimeImportLibrary.IsSupportedImage(sourcePath))
            {
                SetStatus(Tr("\u8bf7\u9009\u62e9 png/jpg/jpeg/bmp \u56fe\u7247\u3002", "Choose a png/jpg/jpeg/bmp image."));
                return false;
            }

            try
            {
                ConfigureWallMountedSettings(placed, true, true);
                string storedPath = CopyObjectDirectionSprite(prefab.name, 0, sourcePath);
                AssignRuntimeObjectDirectionSprite(placed, 0, true, storedPath, prefab.name);
                placed.ApplyRotationVisualState();
                SaveSelectedObjectSettings();
                SetStatus(Tr("\u5df2\u8bbe\u7f6e\u58c1\u6302\u4e3b\u8d34\u56fe\u3002", "Wall-mounted main sprite set."));
                return true;
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.FailedToSetWallMountedSprite,
                    exception.Message);
                SetStatus(Tr("\u58c1\u6302\u8d34\u56fe\u8bbe\u7f6e\u5931\u8d25\u3002", "Failed to set wall-mounted sprite."));
                return false;
            }
        }

        private void ClearSelectedWallMountedSprite()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return;
            }

            AssignRuntimeObjectDirectionSprite(placed, 0, false, string.Empty, prefab.name);
            placed.ApplyRotationVisualState();
            SaveSelectedObjectSettings();
            SetStatus(Tr("\u5df2\u6e05\u7a7a\u58c1\u6302\u4e3b\u8d34\u56fe\u3002", "Wall-mounted main sprite cleared."));
        }

        private void SetSelectedObjectDirectionSprite(int rotation90Index)
        {
            string sourcePath = SelectSingleImageFile(TrFormat("\u9009\u62e9 {0} \u5ea6\u8d34\u56fe", "Choose {0} deg Sprite", rotation90Index * 90));
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            TryAssignSelectedObjectDirectionSprite(rotation90Index, sourcePath);
        }

        private bool TryAssignSelectedObjectDirectionSprite(int rotation90Index, string sourcePath)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return false;
            }

            if (placed.IsWallMounted)
            {
                return rotation90Index == 0 && TryAssignSelectedWallMountedSprite(sourcePath);
            }

            if (!CampusRuntimeImportLibrary.IsSupportedImage(sourcePath))
            {
                SetStatus(Tr("\u8bf7\u9009\u62e9 png/jpg/jpeg/bmp \u56fe\u7247\u3002", "Choose a png/jpg/jpeg/bmp image."));
                return false;
            }

            try
            {
                string storedPath = CopyObjectDirectionSprite(prefab.name, rotation90Index, sourcePath);
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                AssignRuntimeObjectDirectionSprite(placed, rotation90Index, true, storedPath, prefab.name);
                objectSettingsSession.PreviewRotation90 = CampusPlacedObject.NormalizeRotation90(rotation90Index);
                placed.ApplyRotationVisualState();
                SaveSelectedObjectSettings();
                SetStatus(TrFormat("\u5df2\u8bbe\u7f6e\u65cb\u8f6c\u8d34\u56fe\uff1a{0}", "Direction sprite set: {0}", rotation90Index * 90));
                return true;
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.FailedToSetObjectDirectionSprite,
                    exception.Message);
                SetStatus(Tr("\u65cb\u8f6c\u8d34\u56fe\u8bbe\u7f6e\u5931\u8d25\u3002", "Failed to set direction sprite."));
                return false;
            }
        }

        private void ClearSelectedObjectDirectionSprite(int rotation90Index)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return;
            }

            if (placed.IsWallMounted)
            {
                if (rotation90Index == 0)
                {
                    ClearSelectedWallMountedSprite();
                }

                return;
            }

            AssignRuntimeObjectDirectionSprite(placed, rotation90Index, false, string.Empty, prefab.name);
            placed.ApplyRotationVisualState();
            SaveSelectedObjectSettings();
            SetStatus(TrFormat("\u5df2\u6e05\u7a7a\u65cb\u8f6c\u8d34\u56fe\uff1a{0}", "Direction sprite cleared: {0}", rotation90Index * 90));
        }

        private void SaveSelectedObjectSettings()
        {
            if (!TryGetSelectedObjectSettingsTarget(out GameObject prefab, out CampusPlacedObject placed))
            {
                return;
            }

            CampusRuntimeObjectSettings settings = CaptureSelectedObjectSettings(prefab, placed);
            SaveRuntimeObjectSettings(settings);
            CampusRuntimeMapEditorObjectSettingsResult result =
                CampusRuntimeMapEditorObjectSettingsCommandService.ApplySettingsToMatchingPlacedObjects(
                    mapRoot,
                    prefab,
                    settings,
                    true,
                    RecordUndo,
                    ApplyRuntimeObjectSettings,
                    ResolveFloorForPlacedObject,
                    RefreshPlacedRetailShelf);
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u7269\u54c1\u8bbe\u7f6e\uff1a{0}\uff0c\u5df2\u540c\u6b65 {1} \u4e2a\u573a\u4e0a\u540c\u7c7b\u7269\u54c1\u3002", "Saved object settings: {0}. Synced {1} placed objects of the same type.", GetObjectDisplayName(prefab), result.AppliedCount));
        }

        private void ApplySelectedObjectSettingsToPlacedInstances()
        {
            if (!TryGetSelectedObjectSettingsTarget(out GameObject prefab, out CampusPlacedObject placed))
            {
                return;
            }

            CampusRuntimeObjectSettings settings = CaptureSelectedObjectSettings(prefab, placed);
            SaveRuntimeObjectSettings(settings);
            CampusRuntimeMapEditorObjectSettingsResult result =
                CampusRuntimeMapEditorObjectSettingsCommandService.ApplySettingsToMatchingPlacedObjects(
                    mapRoot,
                    prefab,
                    settings,
                    true,
                    RecordUndo,
                    ApplyRuntimeObjectSettings,
                    ResolveFloorForPlacedObject,
                    RefreshPlacedRetailShelf);
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u5e94\u7528\u5230\u573a\u4e0a\u540c\u7c7b\u7269\u54c1\uff1a{0} \u4e2a {1}", "Applied to same-type placed objects: {0} {1}", result.AppliedCount, GetObjectDisplayName(prefab)));
        }

        private bool TryGetSelectedObjectSettingsTarget(
            out GameObject prefab,
            out CampusPlacedObject placed)
        {
            prefab = GetSelectedObjectPrefab();
            placed = EnsureRuntimePlacedObject(prefab);
            if (prefab != null && placed != null)
            {
                return true;
            }

            SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
            return false;
        }

        private CampusRuntimeObjectSettings CaptureSelectedObjectSettings(
            GameObject prefab,
            CampusPlacedObject placed)
        {
            if (placed.UseCustomInteractionAnchor)
            {
                placed.IsInteractable = true;
            }

            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
            return CampusRuntimeObjectAuthoring.CaptureSettings(
                prefab,
                placed,
                GetImportRootFolder(),
                Tr("\u4ea4\u4e92", "Interact"));
        }

        private void ApplySavedObjectSettingsToPalette()
        {
            for (int i = 0; i < objectPrefabs.Count; i++)
            {
                GameObject prefab = objectPrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                CampusRuntimeObjectSettings settings = LoadRuntimeObjectSettings(prefab.name);
                if (settings != null)
                {
                    ApplyRuntimeObjectSettings(prefab, settings);
                }
            }
        }

        private void NormalizePlacedObjectTypeIdsFromPalette()
        {
            CampusMapRoot targetRoot = mapRoot != null
                ? mapRoot
                : FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            if (targetRoot == null)
            {
                return;
            }

            CampusPlacedObject[] placedObjects = targetRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int i = 0; i < placedObjects.Length; i++)
            {
                CampusPlacedObject placed = placedObjects[i];
                if (placed == null || !string.IsNullOrWhiteSpace(placed.TypeId))
                {
                    continue;
                }

                string resolvedTypeId = ResolveObjectTypeIdForPlacedObjectFromPalette(placed);
                if (!string.IsNullOrWhiteSpace(resolvedTypeId))
                {
                    placed.TypeId = resolvedTypeId;
                }
            }
        }

        private string ResolveObjectTypeIdForPlacedObjectFromPalette(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(placed.TypeId))
            {
                return placed.TypeId.Trim();
            }

            string objectId = !string.IsNullOrWhiteSpace(placed.ObjectId)
                ? placed.ObjectId.Trim()
                : placed.gameObject != null ? placed.gameObject.name : string.Empty;
            CampusRuntimeObjectSettings settings = LoadRuntimeObjectSettings(objectId);
            if (settings != null && !string.IsNullOrWhiteSpace(settings.TypeId))
            {
                return settings.TypeId.Trim();
            }

            int prefabIndex = FindPrefabIndexByName(objectId);
            if (prefabIndex >= 0 && prefabIndex < objectPrefabs.Count)
            {
                GameObject prefab = objectPrefabs[prefabIndex];
                CampusPlacedObject prefabPlaced = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
                if (prefabPlaced != null && !string.IsNullOrWhiteSpace(prefabPlaced.TypeId))
                {
                    return prefabPlaced.TypeId.Trim();
                }

                settings = prefab != null ? LoadRuntimeObjectSettings(prefab.name) : null;
                if (settings != null && !string.IsNullOrWhiteSpace(settings.TypeId))
                {
                    return settings.TypeId.Trim();
                }
            }

            return CampusRuntimeObjectAuthoring.ResolveTypeIdForPlacedObject(placed);
        }

        private CampusPlacedObject ApplyRuntimeObjectSettings(GameObject target, CampusRuntimeObjectSettings settings)
        {
            CampusPlacedObject placed = EnsureRuntimePlacedObject(target);
            if (settings != null)
            {
                settings.ObjectId = objectDefinitionCatalog.NormalizeObjectId(settings.ObjectId);
                settings.TypeId = objectDefinitionCatalog.ResolveTypeId(settings.ObjectId, settings.TypeId);
                string displayName = objectDefinitionCatalog.ResolveDisplayNameText(
                    settings.ObjectId,
                    settings.DisplayNameOverride);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    settings.DisplayNameOverride = displayName;
                }
            }

            return CampusRuntimeObjectAuthoring.ApplySettings(
                target,
                placed,
                settings,
                GetImportRootFolder(),
                Tr("\u4ea4\u4e92", "Interact"),
                LoadRuntimeObjectSprite,
                EnsureStorageInteractionAnchor);
        }

        private void SaveRuntimeObjectSettings(CampusRuntimeObjectSettings settings)
        {
            if (settings != null)
            {
                settings.ObjectId = objectDefinitionCatalog.NormalizeObjectId(settings.ObjectId);
                settings.TypeId = objectDefinitionCatalog.ResolveTypeId(settings.ObjectId, settings.TypeId);
            }

            CampusRuntimeObjectSettingsStore.Save(GetImportRootFolder(), settings);
            RefreshImportAssetDatabaseIfProjectBacked();
        }

        private CampusRuntimeObjectSettings LoadRuntimeObjectSettings(string objectId)
        {
            List<string> lookupIds = objectDefinitionCatalog.GetSettingsLookupIds(objectId);
            for (int i = 0; i < lookupIds.Count; i++)
            {
                CampusRuntimeObjectSettings settings = CampusRuntimeObjectSettingsStore.Load(
                    GetImportRootFolder(),
                    lookupIds[i],
                    message => CampusRuntimeMapEditorLogTextCatalog.Warning(
                        CampusRuntimeMapEditorLogTextId.WarningMessage,
                        message));
                if (settings == null)
                {
                    continue;
                }

                settings.ObjectId = objectDefinitionCatalog.NormalizeObjectId(settings.ObjectId);
                return settings;
            }

            return null;
        }

        private void AssignRuntimeObjectDirectionSprite(CampusPlacedObject placed, int rotation90Index, bool hasOverride, string spritePath, string objectName)
        {
            CampusRuntimeObjectAuthoring.AssignDirectionSprite(
                placed,
                rotation90Index,
                hasOverride,
                spritePath,
                objectName,
                GetImportRootFolder(),
                LoadRuntimeObjectSprite);
        }

        private Sprite LoadRuntimeObjectSprite(string path, string spriteName, Vector2Int footprint)
        {
            string normalizedPath = NormalizeSerializedImportPath(path);
            string resolvedPath = ResolveImportContentPath(normalizedPath);
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                return null;
            }

            Vector2Int normalizedFootprint = CampusPlacedObject.NormalizeFootprintSize(footprint);
            string cacheKey = resolvedPath.Replace('\\', '/') + "|" + normalizedFootprint.x + "x" + normalizedFootprint.y;
            if (runtimeObjectSpriteCache.TryGetValue(cacheKey, out Sprite cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            Texture2D texture = LoadImportedTexture(resolvedPath);
            if (texture == null)
            {
                return null;
            }

            Sprite sprite = CreateObjectSprite(texture, spriteName, normalizedFootprint);
            if (sprite != null)
            {
                runtimeObjectSpriteCache[cacheKey] = sprite;
            }

            return sprite;
        }

        private string CopyObjectDirectionSprite(string objectId, int rotation90Index, string sourcePath)
        {
            string serializedPath = CampusRuntimeObjectSettingsStore.CopyDirectionSprite(
                GetImportRootFolder(),
                objectId,
                rotation90Index,
                sourcePath);
            RefreshImportAssetDatabaseIfProjectBacked();
            return serializedPath;
        }

        private Sprite GetObjectDirectionSprite(CampusPlacedObject placed, int rotation90Index)
        {
            if (placed == null)
            {
                return null;
            }

            switch (CampusPlacedObject.NormalizeRotation90(rotation90Index))
            {
                case 0:
                    return placed.Rotation0Sprite;
                case 1:
                    return placed.Rotation90Sprite;
                case 2:
                    return placed.Rotation180Sprite;
                case 3:
                    return placed.Rotation270Sprite;
                default:
                    return null;
            }
        }

        private CampusPlacedObject EnsureRuntimePlacedObject(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            CampusPlacedObject placed = target.GetComponent<CampusPlacedObject>();
            if (placed == null)
            {
                if (!target.scene.IsValid())
                {
                    SetStatus(Tr("\u8be5\u7269\u54c1\u9884\u5236\u4f53\u7f3a\u5c11 CampusPlacedObject\uff0c\u8bf7\u5148\u5728\u9879\u76ee\u4e2d\u914d\u7f6e\u3002", "This object prefab is missing CampusPlacedObject. Configure it in the project first."));
                    return null;
                }

                placed = target.AddComponent<CampusPlacedObject>();
            }

            if (string.IsNullOrWhiteSpace(placed.ObjectId))
            {
                placed.ObjectId = target.name;
            }

            return placed;
        }

        private bool DoesPlacedObjectMatchTypeKey(CampusPlacedObject placed, string targetObjectTypeKey)
        {
            if (placed == null || string.IsNullOrEmpty(targetObjectTypeKey))
            {
                return false;
            }

            string objectTypeKey = CampusRuntimeObjectAuthoring.ResolvePlacedObjectTypeKey(placed);
            return string.Equals(objectTypeKey, targetObjectTypeKey, StringComparison.Ordinal);
        }

        private CampusFloorRoot ResolveFloorForPlacedObject(CampusPlacedObject placed)
        {
            if (placed == null || mapRoot == null || mapRoot.Floors == null)
            {
                return null;
            }

            Transform placedTransform = placed.transform;
            for (int floorIndex = 0; floorIndex < mapRoot.Floors.Count; floorIndex++)
            {
                CampusFloorRoot floor = mapRoot.Floors[floorIndex];
                if (floor == null || floor.PropsRoot == null)
                {
                    continue;
                }

                if (placedTransform.IsChildOf(floor.PropsRoot))
                {
                    return floor;
                }
            }

            return mapRoot.GetFloor(placed.FloorIndex);
        }

        private void OpenImportLocation(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
            GUIUtility.systemCopyBuffer = path;
            try
            {
                Application.OpenURL(new Uri(path).AbsoluteUri);
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.FailedToOpenFolder,
                    path,
                    exception.Message);
            }

            SetStatus(TrFormat("\u5df2\u6253\u5f00\u5bfc\u5165\u76ee\u5f55\uff1a{0}", "Opened import folder: {0}", path));
        }

        private void DrawToolbarButton(ref float x, float y, string label, Action action)
        {
            DrawToolbarButton(ref x, y, label, action, true);
        }

        private void DrawToolbarButton(ref float x, float y, string label, Action action, bool enabled)
        {
            GUI.enabled = enabled;
            if (GUI.Button(new Rect(x, y, ToolbarButtonWidth, 46f), label, buttonStyle))
            {
                action();
            }

            GUI.enabled = true;
            x += ToolbarButtonWidth + 10f;
        }

        private void DrawTilePreview(Rect rect, TileBase tile)
        {
            Sprite sprite = GetTileSprite(tile);
            if (sprite == null)
            {
                GUI.DrawTexture(rect, tileFallbackTexture, ScaleMode.ScaleToFit);
                return;
            }

            DrawSprite(rect, sprite);
        }

        private void DrawPrefabPreview(Rect rect, GameObject prefab)
        {
            Sprite sprite = GetPrefabSprite(prefab);
            if (sprite == null)
            {
                GUI.DrawTexture(rect, tileFallbackTexture, ScaleMode.ScaleToFit);
                return;
            }

            DrawSprite(rect, sprite);
        }

        private void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            Rect textureRect = sprite.textureRect;
            Rect texCoords = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, texCoords, true);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelTexture = MakeTexture(CampusUiVisualTheme.Panel);
            headerTexture = MakeTexture(CampusUiVisualTheme.PanelRaised);
            buttonTexture = MakeTexture(CampusUiVisualTheme.PanelSoft);
            selectedTexture = MakeTexture(CampusUiVisualTheme.Accent);
            hoverTexture = MakeTexture(CampusUiVisualTheme.AccentSoftFill);
            inputTexture = MakeTexture(CampusUiVisualTheme.PanelRaised);
            inputFocusedTexture = MakeTexture(new Color(0.15f, 0.2f, 0.28f, 0.98f));
            objectSettingsHighlightTexture = MakeTexture(new Color(0.98f, 0.68f, 0.22f, 0.22f));
            lineTexture = MakeTexture(CampusUiVisualTheme.Border);
            tileFallbackTexture = MakeCheckerTexture(new Color(0.14f, 0.18f, 0.24f, 1f), new Color(0.08f, 0.1f, 0.14f, 1f));

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            panelStyle.normal.textColor = CampusUiVisualTheme.TextPrimary;
            panelStyle.border = new RectOffset(4, 4, 4, 4);
            panelStyle.padding = new RectOffset(10, 10, 8, 8);
            panelStyle.alignment = TextAnchor.MiddleCenter;
            panelStyle.fontSize = 22;

            headerStyle = new GUIStyle(GUI.skin.box);
            headerStyle.normal.background = headerTexture;
            headerStyle.normal.textColor = CampusUiVisualTheme.TextPrimary;
            headerStyle.fontSize = 28;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.padding = new RectOffset(14, 10, 0, 0);

            bodyStyle = new GUIStyle(GUI.skin.label);
            bodyStyle.normal.textColor = CampusUiVisualTheme.TextPrimary;
            bodyStyle.fontSize = 24;
            bodyStyle.wordWrap = true;

            smallBodyStyle = new GUIStyle(GUI.skin.label);
            smallBodyStyle.normal.textColor = CampusUiVisualTheme.TextSecondary;
            smallBodyStyle.fontSize = 17;
            smallBodyStyle.alignment = TextAnchor.MiddleCenter;
            smallBodyStyle.clipping = TextClipping.Clip;

            mutedStyle = new GUIStyle(bodyStyle);
            mutedStyle.normal.textColor = CampusUiVisualTheme.TextMuted;
            mutedStyle.fontSize = 19;

            warningStyle = new GUIStyle(bodyStyle);
            warningStyle.normal.textColor = CampusUiVisualTheme.TextGold;
            warningStyle.fontSize = 22;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = hoverTexture;
            buttonStyle.active.background = selectedTexture;
            buttonStyle.normal.textColor = CampusUiVisualTheme.TextPrimary;
            buttonStyle.hover.textColor = CampusUiVisualTheme.TextPrimary;
            buttonStyle.active.textColor = CampusUiVisualTheme.TextPrimary;
            buttonStyle.fontSize = 21;
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.padding = new RectOffset(8, 8, 6, 6);

            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.background = selectedTexture;

            iconButtonStyle = new GUIStyle(buttonStyle);
            iconButtonStyle.fontSize = 20;

            inputStyle = new GUIStyle(GUI.skin.textField);
            inputStyle.normal.background = inputTexture;
            inputStyle.focused.background = inputFocusedTexture;
            inputStyle.hover.background = inputFocusedTexture;
            inputStyle.active.background = inputFocusedTexture;
            inputStyle.normal.textColor = CampusUiVisualTheme.TextPrimary;
            inputStyle.focused.textColor = CampusUiVisualTheme.TextPrimary;
            inputStyle.hover.textColor = CampusUiVisualTheme.TextPrimary;
            inputStyle.active.textColor = CampusUiVisualTheme.TextPrimary;
            inputStyle.fontSize = 21;
            inputStyle.alignment = TextAnchor.MiddleLeft;
            inputStyle.padding = new RectOffset(10, 10, 6, 6);

            objectSettingsHighlightStyle = new GUIStyle(GUI.skin.box);
            objectSettingsHighlightStyle.normal.background = objectSettingsHighlightTexture;
            objectSettingsHighlightStyle.border = new RectOffset(4, 4, 4, 4);
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private Texture2D MakeCheckerTexture(Color a, Color b)
        {
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    texture.SetPixel(x, y, ((x / 4 + y / 4) % 2) == 0 ? a : b);
                }
            }

            texture.Apply();
            return texture;
        }

        private CampusMapRoot CreateMapRoot()
        {
            GameObject rootObject = new GameObject(CampusObjectNames.MapRoot);
            CampusMapRoot root = rootObject.AddComponent<CampusMapRoot>();
            root.SortingOrderStepPerFloor = 1000;
            root.CurrentPreviewFloor = 1;
            rootObject.AddComponent<CampusFloorVisibilityController>().MapRoot = root;
            EnsureChild(rootObject.transform, CampusObjectNames.FloorsRoot);
            EnsureChild(rootObject.transform, CampusObjectNames.EditorDataRoot);
            mapRoot = root;
            EnsureFloor(1);
            return root;
        }

        private CampusFloorRoot EnsureFloor(int floorIndex)
        {
            if (mapRoot == null)
            {
                return null;
            }

            floorIndex = Mathf.Max(1, floorIndex);
            mapRoot.RebuildFloorReferences();
            CampusFloorRoot floor = mapRoot.GetFloor(floorIndex);
            if (floor != null)
            {
                EnsureFloorStructure(floor);
                return floor;
            }

            Transform floorsRoot = EnsureChild(mapRoot.transform, CampusObjectNames.FloorsRoot);
            GameObject floorObject = new GameObject(CampusObjectNames.GetFloorName(floorIndex));
            floorObject.transform.SetParent(floorsRoot, false);
            floor = floorObject.AddComponent<CampusFloorRoot>();
            floor.FloorIndex = floorIndex;
            floor.IsUnlocked = true;

            Transform gridTransform = EnsureChild(floorObject.transform, CampusObjectNames.Grid);
            Grid grid = gridTransform.GetComponent<Grid>();
            if (grid == null)
            {
                grid = gridTransform.gameObject.AddComponent<Grid>();
            }

            grid.cellSize = Vector3.one;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            floor.Grid = grid;
            int sortingBase = floorIndex * mapRoot.SortingOrderStepPerFloor;
            floor.FloorTilemap = CreateTilemap(gridTransform, CampusObjectNames.FloorTilemap, sortingBase + CampusRenderSortingUtility.FloorOffset, true);
            floor.WallLogicTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallLogicTilemap, sortingBase + CampusRenderSortingUtility.WallLogicOffset, false);
            floor.WallTilemap = floor.WallLogicTilemap;
            floor.WallFaceTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallFaceTilemap, sortingBase + CampusRenderSortingUtility.WallFaceOffset, false);
            floor.WallSideTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallSideTilemap, sortingBase + CampusRenderSortingUtility.WallSideOffset, false);
            floor.WallCapTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallCapTilemap, sortingBase + CampusRenderSortingUtility.WallCapOffset, false);
            floor.WallOverlayTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallOverlayTilemap, sortingBase + CampusRenderSortingUtility.WallVisualOverlayOffset, false);
            floor.OverlayTilemap = CreateTilemap(gridTransform, CampusObjectNames.OverlayTilemap, sortingBase + CampusRenderSortingUtility.OverlayOffset, true);
            floor.CollisionDebugTilemap = CreateTilemap(gridTransform, CampusObjectNames.CollisionDebugTilemap, sortingBase + CampusRenderSortingUtility.CollisionDebugOffset, false);
            floor.WallMeshRoot = EnsureChild(gridTransform, CampusObjectNames.WallMeshRoot);
            floor.PropsRoot = EnsureChild(floorObject.transform, CampusObjectNames.PropsRoot);
            floor.StairsRoot = EnsureChild(floorObject.transform, CampusObjectNames.StairsRoot);
            EnsureWallCollision(floor);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, sortingBase);
            mapRoot.RebuildFloorReferences();
            MarkSceneReferencesDirty();
            return floor;
        }

        private void EnsureFloorStructure(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            if (floor.Grid == null)
            {
                floor.Grid = floor.GetComponentInChildren<Grid>(true);
            }

            if (floor.Grid == null)
            {
                Transform gridTransform = EnsureChild(floor.transform, CampusObjectNames.Grid);
                floor.Grid = gridTransform.gameObject.AddComponent<Grid>();
            }

            floor.Grid.cellSize = Vector3.one;
            floor.Grid.cellLayout = GridLayout.CellLayout.Rectangle;
            int sortingBase = floor.FloorIndex * (mapRoot != null ? mapRoot.SortingOrderStepPerFloor : 1000);

            floor.FloorTilemap = floor.FloorTilemap != null ? floor.FloorTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.FloorTilemap, sortingBase + CampusRenderSortingUtility.FloorOffset, true, CampusObjectNames.LegacyFloorTilemap);
            floor.WallLogicTilemap = floor.WallLogicTilemap != null ? floor.WallLogicTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallLogicTilemap, sortingBase + CampusRenderSortingUtility.WallLogicOffset, false, CampusObjectNames.LegacyWallLogicTilemap, CampusObjectNames.LegacyWallsTilemap);
            floor.WallTilemap = floor.WallLogicTilemap;
            CampusDynamicShadowUtility.RemoveFixedWallShadowTilemaps(floor);
            floor.WallFaceTilemap = floor.WallFaceTilemap != null ? floor.WallFaceTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallFaceTilemap, sortingBase + CampusRenderSortingUtility.WallFaceOffset, false, CampusObjectNames.LegacyWallFaceTilemap);
            floor.WallSideTilemap = floor.WallSideTilemap != null ? floor.WallSideTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallSideTilemap, sortingBase + CampusRenderSortingUtility.WallSideOffset, false, CampusObjectNames.LegacyWallSideTilemap);
            floor.WallCapTilemap = floor.WallCapTilemap != null ? floor.WallCapTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallCapTilemap, sortingBase + CampusRenderSortingUtility.WallCapOffset, false, CampusObjectNames.LegacyWallCapTilemap);
            floor.WallOverlayTilemap = floor.WallOverlayTilemap != null ? floor.WallOverlayTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallOverlayTilemap, sortingBase + CampusRenderSortingUtility.WallVisualOverlayOffset, false, CampusObjectNames.LegacyWallOverlayTilemap);
            floor.OverlayTilemap = floor.OverlayTilemap != null ? floor.OverlayTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.OverlayTilemap, sortingBase + CampusRenderSortingUtility.OverlayOffset, true, CampusObjectNames.LegacyOverlayTilemap);
            floor.CollisionDebugTilemap = floor.CollisionDebugTilemap != null ? floor.CollisionDebugTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.CollisionDebugTilemap, sortingBase + CampusRenderSortingUtility.CollisionDebugOffset, false, CampusObjectNames.LegacyCollisionDebugTilemap);
            floor.WallMeshRoot = floor.WallMeshRoot != null ? floor.WallMeshRoot : EnsureChild(floor.Grid.transform, CampusObjectNames.WallMeshRoot);
            floor.PropsRoot = floor.PropsRoot != null ? floor.PropsRoot : EnsureChild(floor.transform, CampusObjectNames.PropsRoot);
            floor.StairsRoot = floor.StairsRoot != null ? floor.StairsRoot : EnsureChild(floor.transform, CampusObjectNames.StairsRoot);
            EnsureWallCollision(floor);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, sortingBase);
            CampusWallTileUtility.SetTilemapVisible(floor.CollisionDebugTilemap, false);
            floor.CaptureOriginalRenderState();
        }

        private Tilemap FindOrCreateTilemap(Transform parent, string name, int sortingOrder, bool visible, params string[] legacyNames)
        {
            Tilemap existing = FindTilemapByName(parent, name, legacyNames);
            if (existing != null)
            {
                existing.name = name;
                TilemapRenderer renderer = existing.GetComponent<TilemapRenderer>();
                if (renderer == null)
                {
                    renderer = existing.gameObject.AddComponent<TilemapRenderer>();
                }

                renderer.sortingOrder = sortingOrder;
                renderer.enabled = visible;
                return existing;
            }

            return CreateTilemap(parent, name, sortingOrder, visible);
        }

        private Tilemap CreateTilemap(Transform parent, string name, int sortingOrder, bool visible)
        {
            GameObject tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = visible;
            return tilemap;
        }

        private Tilemap FindTilemapByName(Transform parent, string name, params string[] legacyNames)
        {
            if (parent == null)
            {
                return null;
            }

            Tilemap[] tilemaps = parent.GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap == null)
                {
                    continue;
                }

                if (CampusObjectNames.MatchesAny(tilemap.name, name))
                {
                    return tilemap;
                }

                for (int legacyIndex = 0; legacyIndex < legacyNames.Length; legacyIndex++)
                {
                    if (CampusObjectNames.MatchesAny(tilemap.name, legacyNames[legacyIndex]))
                    {
                        return tilemap;
                    }
                }
            }

            return null;
        }

        private Transform EnsureChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private void EnsureWallCollision(CampusFloorRoot floor)
        {
            using (CampusWallBuildProfiler.EnsureWallCollision.Auto())
            {
                CampusWallCollisionRenderer.EnsureForFloor(floor);
            }
        }

        private void RebuildWallVisuals(CampusFloorRoot floor)
        {
            using (CampusWallBuildProfiler.RebuildWallVisuals.Auto())
            using (CampusWallBuildProfiler.RebuildWallVisualsFull.Auto())
            {
            if (floor == null)
            {
                return;
            }

            if (wallProfiles.Count > 0)
            {
                selectedWallProfileIndex = Mathf.Clamp(selectedWallProfileIndex, 0, wallProfiles.Count - 1);
                fallbackWallProfile = wallProfiles[selectedWallProfileIndex];
            }

            CampusWallAutoRenderer.RebuildFloor(floor, wallVisualCatalog, fallbackWallProfile);
            CampusWallAutoRenderer.ApplyFinalWallVisualState(floor);
            }
        }

        private void RebuildWallVisuals(CampusFloorRoot floor, IReadOnlyList<Vector3Int> affectedCells)
        {
            using (CampusWallBuildProfiler.RebuildWallVisuals.Auto())
            using (CampusWallBuildProfiler.RebuildWallVisualsChanged.Auto())
            {
            if (floor == null)
            {
                return;
            }

            if (affectedCells == null || affectedCells.Count == 0)
            {
                RebuildWallVisuals(floor);
                return;
            }

            if (wallProfiles.Count > 0)
            {
                selectedWallProfileIndex = Mathf.Clamp(selectedWallProfileIndex, 0, wallProfiles.Count - 1);
                fallbackWallProfile = wallProfiles[selectedWallProfileIndex];
            }

            CampusWallAutoRenderer.RebuildChangedCells(floor, affectedCells, wallVisualCatalog, fallbackWallProfile);
            CampusWallAutoRenderer.ApplyFinalWallVisualState(floor);
            }
        }

        private void QueueWallVisualRebuild(CampusFloorRoot floor, IReadOnlyList<Vector3Int> affectedCells)
        {
            if (floor == null || affectedCells == null || affectedCells.Count == 0)
            {
                return;
            }

            if (pendingWallVisualRebuildFloor != null && pendingWallVisualRebuildFloor != floor)
            {
                FlushPendingWallVisualRebuild();
            }

            pendingWallVisualRebuildFloor = floor;
            for (int i = 0; i < affectedCells.Count; i++)
            {
                Vector3Int cell = affectedCells[i];
                if (pendingWallVisualRebuildCellSet.Add(cell))
                {
                    pendingWallVisualRebuildCells.Add(cell);
                    CampusWallChunkSystem.AddAffectedChunksForCell(pendingWallVisualRebuildChunks, cell);
                }
            }

            bool shouldFlush = !wallStrokeVisualPreviewInitialized ||
                               pendingWallVisualRebuildCells.Count >= WallStrokeVisualBatchCellThreshold ||
                               pendingWallVisualRebuildChunks.Count >= WallStrokeVisualBatchChunkThreshold;
            if (shouldFlush)
            {
                FlushPendingWallVisualRebuild();
                wallStrokeVisualPreviewInitialized = true;
            }
        }

        private void FlushPendingWallVisualRebuild()
        {
            if (pendingWallVisualRebuildFloor == null || pendingWallVisualRebuildCells.Count == 0)
            {
                ClearPendingWallVisualRebuild();
                return;
            }

            RebuildWallVisuals(pendingWallVisualRebuildFloor, pendingWallVisualRebuildCells);
            ClearPendingWallVisualRebuild();
        }

        private void ClearPendingWallVisualRebuild()
        {
            pendingWallVisualRebuildFloor = null;
            pendingWallVisualRebuildCells.Clear();
            pendingWallVisualRebuildCellSet.Clear();
            pendingWallVisualRebuildChunks.Clear();
        }


        private void DeleteSelectedFloor()
        {
            CampusRuntimeMapEditorFloorDeleteResult result = CampusRuntimeMapEditorFloorCommandService.DeleteSelectedFloor(
                mapRoot,
                selectedFloorIndex,
                RecordUndo,
                DestroyRuntimeObject);
            switch (result.Outcome)
            {
                case CampusRuntimeMapEditorFloorDeleteOutcome.KeepAtLeastOneFloor:
                    SetStatus(Tr("\u81f3\u5c11\u4fdd\u7559\u4e00\u4e2a\u697c\u5c42\u3002", "Keep at least one floor."));
                    return;
                case CampusRuntimeMapEditorFloorDeleteOutcome.Deleted:
                    selectedFloorIndex = result.NextSelectedFloorIndex;
                    MarkSceneReferencesDirty();
                    RefreshSceneReferencesIfNeeded(true);
                    SetStatus(Tr("\u5df2\u5220\u9664\u697c\u5c42\u3002", "Floor deleted."));
                    return;
                default:
                    return;
            }
        }

        private int[] GetAllSortingLayerIds()
        {
            SortingLayer[] layers = SortingLayer.layers;
            int[] ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                ids[i] = layers[i].id;
            }

            return ids;
        }

        private void AddRoomRequirement(string roomName, int required)
        {
            AddOrUpdateRoomDefinition(roomName, required);
        }

        private void AddOrUpdateRoomDefinition(string roomName, int required)
        {
            if (!CampusRuntimeAreaPresetCatalog.TryResolveRoomName(roomName, out string presetRoomName))
            {
                return;
            }

            CampusRuntimeAreaPreset preset = GetAreaPreset(presetRoomName);
            int resolvedRequired = preset != null ? preset.RequiredCount : Mathf.Max(0, required);
            int index = FindRoomDefinitionIndex(presetRoomName);
            if (index >= 0)
            {
                roomRequiredCounts[index] = resolvedRequired;
                return;
            }

            roomNames.Add(presetRoomName);
            roomRequiredCounts.Add(resolvedRequired);
            selectedRoomIndex = roomNames.Count - 1;
        }

        private void DeleteSelectedRoomDefinition()
        {
            if (roomNames.Count == 0)
            {
                return;
            }

            int index = Mathf.Clamp(selectedRoomIndex, 0, roomNames.Count - 1);
            string roomName = roomNames[index];
            RecordUndo();
            roomNames.RemoveAt(index);
            roomRequiredCounts.RemoveAt(index);
            selectedRoomIndex = roomNames.Count > 0 ? Mathf.Clamp(index, 0, roomNames.Count - 1) : 0;

            CampusRuntimeRoomMarker[] markers = FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker != null && marker.RoomName == roomName)
                {
                    DestroyRuntimeObject(marker.gameObject);
                }
            }

            InvalidateRoomRegionCountCache();
        }

        private void ClearRoomDefinitions()
        {
            RecordUndo();
            roomNames.Clear();
            roomRequiredCounts.Clear();
            selectedRoomIndex = 0;
            CampusRuntimeRoomMarker[] markers = FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                if (markers[i] != null)
                {
                    DestroyRuntimeObject(markers[i].gameObject);
                }
            }

            InvalidateRoomRegionCountCache();
        }

        private int GetRoomRegionCount(string roomName)
        {
            if (!CampusRuntimeAreaPresetCatalog.TryResolveRoomName(roomName, out string targetRoomName))
            {
                return 0;
            }

            EnsureRoomRegionCountCache();
            int count;
            if (roomRegionCountsByName.TryGetValue(targetRoomName, out count))
            {
                return count;
            }

            return 0;
        }

        private void RefreshOpenPanelCaches(bool force)
        {
            if (!isOpen)
            {
                return;
            }

            if (force || roomRegionCountCacheDirty)
            {
                RebuildRoomRegionCountCache();
            }

            if (force || editableLightCacheDirty)
            {
                RebuildEditableLightCache();
            }
        }

        private void InvalidateOpenPanelCaches()
        {
            InvalidateRoomRegionCountCache();
            InvalidateEditableLightCache();
        }

        private void InvalidateRoomRegionCountCache()
        {
            roomRegionCountCacheDirty = true;
        }

        private void EnsureRoomRegionCountCache()
        {
            if (roomRegionCountCacheDirty)
            {
                RebuildRoomRegionCountCache();
            }
        }

        private void RebuildRoomRegionCountCache()
        {
            CampusRuntimeRoomMarker[] markers = FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            CampusRuntimeAreaRegionCounter.CountRegionsByRoomName(markers, roomRegionCountsByName);
            roomRegionCountCacheDirty = false;
        }

        private void InvalidateEditableLightCache()
        {
            editableLightCacheDirty = true;
        }

        private IReadOnlyList<Light2D> GetEditableLights()
        {
            if (editableLightCacheDirty)
            {
                RebuildEditableLightCache();
            }

            return editableLights;
        }

        private void RebuildEditableLightCache()
        {
            editableLights.Clear();
            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (IsRuntimeEditableLight(light))
                {
                    editableLights.Add(light);
                }
            }

            if (selectedLight != null && !editableLights.Contains(selectedLight))
            {
                selectedLight = null;
            }

            editableLightCacheDirty = false;
        }

        private string GetSelectedRoomName()
        {
            EnsureRoomRequirements();
            if (roomNames.Count == 0)
            {
                return string.Empty;
            }

            selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, roomNames.Count - 1);
            return roomNames[selectedRoomIndex];
        }

        private CampusRuntimeRoomPrefab GetSelectedRoomPrefab()
        {
            if (roomPrefabs.Count == 0)
            {
                return null;
            }

            selectedRoomPrefabIndex = Mathf.Clamp(selectedRoomPrefabIndex, 0, roomPrefabs.Count - 1);
            return roomPrefabs[selectedRoomPrefabIndex];
        }

        private CampusRuntimeGameplayMarkerPreset GetSelectedGameplayPreset()
        {
            if (CampusRuntimeGameplayMarkerPresetCatalog.Presets.Length == 0)
            {
                return null;
            }

            selectedGameplayPresetIndex = Mathf.Clamp(
                selectedGameplayPresetIndex,
                0,
                CampusRuntimeGameplayMarkerPresetCatalog.Presets.Length - 1);
            return CampusRuntimeGameplayMarkerPresetCatalog.Presets[selectedGameplayPresetIndex];
        }

        private CampusRuntimeGameplayActorPreset GetSelectedGameplayActorPreset()
        {
            if (CampusRuntimeGameplayActorPresetCatalog.Presets.Length == 0)
            {
                return null;
            }

            selectedGameplayActorPresetIndex = Mathf.Clamp(
                selectedGameplayActorPresetIndex,
                0,
                CampusRuntimeGameplayActorPresetCatalog.Presets.Length - 1);
            return CampusRuntimeGameplayActorPresetCatalog.Presets[selectedGameplayActorPresetIndex];
        }

        private string GetGameplayPresetLabel(CampusRuntimeGameplayMarkerPreset preset)
        {
            if (preset == null)
            {
                return Tr("\u670d\u52a1\u7ad9\u70b9", "Service Station Point");
            }

            return Tr(preset.Label);
        }

        private string GetGameplayPresetDisplayName(CampusRuntimeGameplayMarkerPreset preset)
        {
            if (preset == null)
            {
                return Tr("\u670d\u52a1\u7ad9\u70b9", "Service Station Point");
            }

            return Tr(preset.DisplayName);
        }

        private float GetGameplayOwnerSelectionHeight()
        {
            CampusRuntimeGameplayMarkerPreset preset = GetSelectedGameplayPreset();
            if (preset == null || !preset.RequiresOwnerFacility)
            {
                return 0f;
            }

            int ownerCount = CollectGameplayFacilityOwnerCandidates(
                preset,
                Mathf.Max(1, selectedFloorIndex)).Count;
            return 34f + (ownerCount == 0 ? 66f : ownerCount * 36f + 48f);
        }

        private bool TryResolveGameplayMarkerOwnerFacilityId(
            CampusRuntimeGameplayMarkerPreset preset,
            int floorIndex,
            out string ownerFacilityId)
        {
            ownerFacilityId = string.Empty;
            if (preset == null || !preset.RequiresOwnerFacility)
            {
                return true;
            }

            List<CampusGameplayFacilityMarker> candidates =
                CollectGameplayFacilityOwnerCandidates(preset, Mathf.Max(1, floorIndex));
            if (candidates.Count == 0)
            {
                selectedFacilityOwnerId = string.Empty;
                return false;
            }

            string normalizedSelectedOwnerId =
                CampusGameplayFacilityMarker.NormalizeFacilityId(selectedFacilityOwnerId);
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidateFacilityId = ResolveGameplayFacilityMarkerId(candidates[i]);
                if (string.Equals(
                        candidateFacilityId,
                        normalizedSelectedOwnerId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ownerFacilityId = candidateFacilityId;
                    return true;
                }
            }

            selectedFacilityOwnerId = string.Empty;
            return false;
        }

        private List<CampusGameplayFacilityMarker> CollectGameplayFacilityOwnerCandidates(
            CampusRuntimeGameplayMarkerPreset preset,
            int floorIndex)
        {
            List<CampusGameplayFacilityMarker> results = new List<CampusGameplayFacilityMarker>();
            if (preset == null || !preset.RequiresOwnerFacility)
            {
                return results;
            }

            CampusGameplayFacilityMarker[] markers =
                FindObjectsByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<string> capturedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayFacilityMarker marker = markers[i];
                if (marker == null ||
                    marker.FloorIndex != floorIndex ||
                    !preset.AcceptsOwnerFacilityType(marker.FacilityType))
                {
                    continue;
                }

                string facilityId = ResolveGameplayFacilityMarkerId(marker);
                if (string.IsNullOrEmpty(facilityId) || !capturedIds.Add(facilityId))
                {
                    continue;
                }

                results.Add(marker);
            }

            results.Sort(CompareGameplayFacilityOwnerCandidates);
            return results;
        }

        private static int CompareGameplayFacilityOwnerCandidates(
            CampusGameplayFacilityMarker left,
            CampusGameplayFacilityMarker right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int xCompare = left.Cell.x.CompareTo(right.Cell.x);
            if (xCompare != 0)
            {
                return xCompare;
            }

            int yCompare = left.Cell.y.CompareTo(right.Cell.y);
            if (yCompare != 0)
            {
                return yCompare;
            }

            return string.Compare(
                ResolveGameplayFacilityMarkerId(left),
                ResolveGameplayFacilityMarkerId(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private string GetGameplayFacilityOwnerLabel(CampusGameplayFacilityMarker marker)
        {
            if (marker == null)
            {
                return Tr("\u670d\u52a1\u7a97\u53e3", "Service Window");
            }

            string displayName = !string.IsNullOrWhiteSpace(marker.DisplayName)
                ? marker.DisplayName.Trim()
                : ResolveGameplayFacilityTypeLabel(marker.FacilityType);
            return displayName + " (" + marker.Cell.x + ", " + marker.Cell.y + ")";
        }

        private string ResolveGameplayFacilityTypeLabel(CampusFacilityType facilityType)
        {
            return CampusRuntimeGameplayMarkerPresetCatalog.TryGetPreset(
                       facilityType,
                       out CampusRuntimeGameplayMarkerPreset preset) &&
                   preset != null
                ? GetGameplayPresetLabel(preset)
                : facilityType.ToString();
        }

        private static string ResolveGameplayFacilityMarkerId(CampusGameplayFacilityMarker marker)
        {
            if (marker == null)
            {
                return string.Empty;
            }

            string facilityId = CampusGameplayFacilityMarker.NormalizeFacilityId(marker.FacilityId);
            return !string.IsNullOrEmpty(facilityId)
                ? facilityId
                : CampusGameplayFacilityMarker.BuildStableFacilityId(
                    marker.FloorIndex,
                    marker.FacilityType,
                    marker.Cell);
        }

        private string GetGameplayActorPresetLabel(CampusRuntimeGameplayActorPreset preset)
        {
            if (preset == null)
            {
                return Tr("NPC", "NPC");
            }

            return Tr(preset.Label);
        }

        private bool TryResolveGameplayMarkerCell(Component component, out int floorIndex, out Vector3Int cell)
        {
            RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
            return CampusRuntimeGameplayOverlayAuthoring.TryResolveMarkerCell(
                component,
                mapRoot,
                out floorIndex,
                out cell);
        }

        private string ResolveNewRoomPrefabName()
        {
            if (!string.IsNullOrWhiteSpace(newRoomPrefabName))
            {
                return newRoomPrefabName.Trim();
            }

            return GetSelectedRoomName();
        }

        private static bool BoundsOverlap2D(BoundsInt a, BoundsInt b)
        {
            return a.xMin < b.xMax &&
                   a.xMax > b.xMin &&
                   a.yMin < b.yMax &&
                   a.yMax > b.yMin;
        }

        private static bool BoundsContains2D(BoundsInt container, BoundsInt contained)
        {
            return contained.xMin >= container.xMin &&
                   contained.xMax <= container.xMax &&
                   contained.yMin >= container.yMin &&
                   contained.yMax <= container.yMax;
        }

        private static Vector3Int NormalizeCell(Vector3Int cell)
        {
            return new Vector3Int(cell.x, cell.y, 0);
        }

        private static bool IsPlacedObjectFullyInsideBounds(CampusPlacedObject placed, BoundsInt bounds)
        {
            if (placed == null)
            {
                return false;
            }

            Vector2Int footprint = placed.RotatedFootprintSize;
            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    Vector3Int cell = new Vector3Int(placed.Cell.x + x, placed.Cell.y + y, 0);
                    if (!CampusRuntimeRoomPrefabAuthoring.CellInBounds(bounds, cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private TileBase GetSelectedFloorTile()
        {
            return floorTiles.Count == 0 ? null : floorTiles[Mathf.Clamp(selectedFloorTileIndex, 0, floorTiles.Count - 1)];
        }

        private TileBase GetSelectedWallTile()
        {
            if (wallTiles.Count > 0)
            {
                return wallTiles[Mathf.Clamp(selectedWallTileIndex, 0, wallTiles.Count - 1)];
            }

            return fallbackWallProfile != null ? fallbackWallProfile.GetLogicTile() : null;
        }

        private GameObject GetSelectedObjectPrefab()
        {
            return objectPrefabs.Count == 0 ? null : objectPrefabs[Mathf.Clamp(selectedObjectIndex, 0, objectPrefabs.Count - 1)];
        }

        private string GetObjectDisplayName(GameObject prefab)
        {
            if (prefab == null)
            {
                return Tr("\u672a\u9009\u7269\u54c1", "No Object Selected");
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            if (placed != null && !string.IsNullOrWhiteSpace(placed.DisplayNameOverride))
            {
                return placed.DisplayNameOverride.Trim();
            }

            return GetObjectFallbackDisplayName(prefab);
        }

        private string GetObjectDisplayName(string objectId)
        {
            int index = FindPrefabIndexByName(objectId);
            return index >= 0 ? GetObjectDisplayName(objectPrefabs[index]) : CampusObjectNames.GetDisplayName(objectId);
        }

        private string GetObjectFallbackDisplayName(GameObject prefab)
        {
            if (prefab == null)
            {
                return Tr("\u672a\u9009\u7269\u54c1", "No Object Selected");
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            string objectId = placed != null && !string.IsNullOrWhiteSpace(placed.ObjectId) ? placed.ObjectId : prefab.name;
            return CampusObjectNames.GetDisplayName(objectId);
        }

        private Vector2Int GetSelectedObjectFootprint()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            Vector2Int footprint = placed != null ? placed.NormalizedFootprintSize : Vector2Int.one;
            int effectiveRotation90 = placed != null ? placed.ResolveAllowedRotation90(rotation90) : 0;
            return CampusPlacedObject.RotateFootprintSize(footprint, effectiveRotation90);
        }

        private Matrix4x4 BuildTileTransform()
        {
            return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, rotation90 * 90f), Vector3.one);
        }

        private TileBase ResolveTile(CampusRuntimeTileSnapshot tileSnapshot, List<TileBase> palette)
        {
            if (tileSnapshot.PaletteIndex >= 0 && tileSnapshot.PaletteIndex < palette.Count && palette[tileSnapshot.PaletteIndex] != null)
            {
                return palette[tileSnapshot.PaletteIndex];
            }

            for (int i = 0; i < palette.Count; i++)
            {
                if (palette[i] != null && palette[i].name == tileSnapshot.AssetName)
                {
                    return palette[i];
                }
            }

            return null;
        }

        private GameObject ResolvePrefab(CampusRuntimeObjectSnapshot objectSnapshot)
        {
            int objectIdIndex = FindPrefabIndexByName(objectSnapshot.ObjectId);
            if (objectIdIndex >= 0)
            {
                return objectPrefabs[objectIdIndex];
            }

            if (objectSnapshot.PaletteIndex >= 0 &&
                objectSnapshot.PaletteIndex < objectPrefabs.Count &&
                objectPrefabs[objectSnapshot.PaletteIndex] != null)
            {
                return objectPrefabs[objectSnapshot.PaletteIndex];
            }

            return null;
        }

        private int FindPrefabIndexByName(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return -1;
            }

            string normalizedObjectId = objectDefinitionCatalog.NormalizeObjectId(objectId);
            for (int i = 0; i < objectPrefabs.Count; i++)
            {
                GameObject prefab = objectPrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
                string prefabObjectId = placed != null && !string.IsNullOrWhiteSpace(placed.ObjectId)
                    ? placed.ObjectId.Trim()
                    : prefab.name;
                if (objectDefinitionCatalog.ObjectIdsMatch(prefabObjectId, normalizedObjectId) ||
                    objectDefinitionCatalog.ObjectIdsMatch(prefab.name, normalizedObjectId) ||
                    prefab.name == objectId ||
                    CampusObjectNames.GetDisplayName(prefab.name) == CampusObjectNames.GetDisplayName(objectId))
                {
                    return i;
                }
            }

            return -1;
        }

        private void ResolveLightCell(Light2D light, out int floorIndex, out Vector3Int cell)
        {
            floorIndex = 0;
            cell = Vector3Int.zero;
            if (mapRoot == null || light == null)
            {
                return;
            }

            mapRoot.RebuildFloorReferences();
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < mapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = mapRoot.Floors[i];
                if (floor == null || floor.Grid == null)
                {
                    continue;
                }

                Vector3Int candidateCell = floor.Grid.WorldToCell(light.transform.position);
                Vector3 center = floor.Grid.GetCellCenterWorld(candidateCell);
                float distance = Vector2.Distance(center, light.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    floorIndex = floor.FloorIndex;
                    cell = candidateCell;
                    cell.z = 0;
                }
            }
        }

        private bool HasUsableMatrix(Matrix4x4 matrix)
        {
            return !Mathf.Approximately(matrix.m33, 0f);
        }

        private Vector3 GetStairWorldCenter(Grid grid, Vector3Int primaryCell, Vector3Int secondaryCell)
        {
            if (grid == null)
            {
                return Vector3.zero;
            }

            return (grid.GetCellCenterWorld(primaryCell) + grid.GetCellCenterWorld(secondaryCell)) * 0.5f;
        }

        private void EnsureTriggerCollider(GameObject target, Vector2 size)
        {
            Collider2D collider = target.GetComponent<Collider2D>();
            if (collider == null)
            {
                collider = target.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            BoxCollider2D box = collider as BoxCollider2D;
            if (box != null)
            {
                box.size = size;
            }
        }

        private void AddRoomMarkerVisual(GameObject markerObject, CampusFloorRoot floor)
        {
            if (markerObject == null)
            {
                return;
            }

            SpriteRenderer renderer = markerObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private void DestroyChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyRuntimeObject(root.GetChild(i).gameObject);
            }
        }

        private void DestroyFloorAuthoredProps(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                CampusRuntimeGameplayOverlayEntity gameplayEntity =
                    child.GetComponent<CampusRuntimeGameplayOverlayEntity>();
                if (gameplayEntity != null && !gameplayEntity.IsActorEntity)
                {
                    continue;
                }

                DestroyRuntimeObject(child);
            }
        }

        private void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private bool IsPointerOverEditorUi(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return IsGuiPositionOverEditorUi(guiPosition);
        }

        private bool IsGuiPositionOverEditorUi(Vector2 guiPosition)
        {
            return leftPanelRect.Contains(guiPosition) ||
                   floorPanelRect.Contains(guiPosition) ||
                   checklistPanelRect.Contains(guiPosition) ||
                   bottomToolbarRect.Contains(guiPosition) ||
                   (showSettings && settingsPanelRect.Contains(guiPosition)) ||
                   (showObjectSettings && objectSettingsPanelRect.Contains(guiPosition)) ||
                   (showHelpOverlay && helpPanelRect.Contains(guiPosition));
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            if (sceneCamera == null)
            {
                return Vector3.zero;
            }

            Vector3 world = sceneCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, GetCameraPlaneDistance()));
            world.z = 0f;
            return world;
        }

        private float GetCameraPlaneDistance()
        {
            if (sceneCamera == null)
            {
                return 0f;
            }

            return sceneCamera.orthographic ? Mathf.Abs(sceneCamera.transform.position.z) : Mathf.Max(sceneCamera.nearClipPlane, Mathf.Abs(sceneCamera.transform.position.z));
        }

        private Vector2 WorldToGuiPoint(Vector3 worldPosition)
        {
            Vector3 screen = sceneCamera.WorldToScreenPoint(worldPosition);
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        private int ParseIntField(Rect rect, int value)
        {
            return ParseIntField(rect, value, null);
        }

        private int ParseIntField(Rect rect, int value, string key)
        {
            string text = DrawTextInput(rect, value.ToString(CultureInfo.InvariantCulture), key ?? BuildRectTextInputKey("int", rect));
            int parsed;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : value;
        }

        private float ParseFloatField(Rect rect, float value)
        {
            return ParseFloatField(rect, value, null);
        }

        private float ParseFloatField(Rect rect, float value, string key)
        {
            string text = DrawTextInput(rect, FormatFloat(value), key ?? BuildRectTextInputKey("float", rect));
            float parsed;
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : value;
        }

        private string DrawTextInput(Rect rect, string value, string key)
        {
            string safeKey = string.IsNullOrEmpty(key) ? BuildRectTextInputKey("text", rect) : key;
            string controlName = TextInputControlPrefix + safeKey;
            bool focusedBefore = GUI.GetNameOfFocusedControl() == controlName;
            string draft;
            if (!focusedBefore || !textInputDrafts.TryGetValue(safeKey, out draft))
            {
                draft = value ?? string.Empty;
            }

            GUI.SetNextControlName(controlName);
            string next = GUI.TextField(rect, draft, inputStyle);
            bool focusedAfter = GUI.GetNameOfFocusedControl() == controlName;
            if (focusedAfter)
            {
                textInputDrafts[safeKey] = next;
            }
            else
            {
                textInputDrafts.Remove(safeKey);
            }

            return next;
        }

        private void RefreshTextInputFocusState()
        {
            string focusedControl = GUI.GetNameOfFocusedControl();
            textInputFocused = !string.IsNullOrEmpty(focusedControl) &&
                               focusedControl.StartsWith(TextInputControlPrefix, StringComparison.Ordinal);
        }

        private bool IsEditingTextInput()
        {
            return textInputFocused;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string BuildRectTextInputKey(string prefix, Rect rect)
        {
            return prefix + "_" +
                   Mathf.RoundToInt(rect.x) + "_" +
                   Mathf.RoundToInt(rect.y) + "_" +
                   Mathf.RoundToInt(rect.width) + "_" +
                   Mathf.RoundToInt(rect.height);
        }

        private Sprite GetTileSprite(TileBase tile)
        {
            Tile spriteTile = tile as Tile;
            return spriteTile != null ? spriteTile.sprite : null;
        }

        private Sprite GetPrefabSprite(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            if (placed != null)
            {
                Sprite configuredSprite = placed.ResolveSpriteForRotation(0, out _, out _);
                if (configuredSprite != null)
                {
                    return configuredSprite;
                }
            }

            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private Sprite ResolvePrefabPreviewSprite(GameObject prefab, CampusPlacedObject placed, out bool usesAuthoredDirectionalSprite, out int effectiveRotation90)
        {
            usesAuthoredDirectionalSprite = false;
            effectiveRotation90 = 0;
            if (prefab == null)
            {
                return null;
            }

            if (placed != null)
            {
                return placed.ResolveSpriteForRotation(rotation90, out usesAuthoredDirectionalSprite, out effectiveRotation90);
            }

            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private string GetDisplayName(TileBase tile)
        {
            return tile == null ? Tr("\u7a7a", "Empty") : CampusObjectNames.GetDisplayName(tile.name);
        }

        private string Truncate(string value, int maxCharacters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
            {
                return value;
            }

            return value.Substring(0, maxCharacters);
        }

        private void DrawSettingsPanel()
        {
            CampusRuntimeMapEditorSettingsReadModel model =
                CampusRuntimeMapEditorReadModelBuilder.BuildSettings(
                    showSettings,
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Settings),
                    CampusRuntimeEditorTextCatalog.FormatMapSource(displayLanguage, DescribeMapLoadSource()),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.AutosavePlayerMap),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.AutoloadPlayerMapOnStart),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SavePlayerMap),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.LoadPlayerMap),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ExportAuthoring),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RestoreAuthoring),
                    autoSavePlayerMap,
                    autoLoadPlayerMapOnStart);
            CampusRuntimeMapEditorSettingsInteraction interaction =
                CampusRuntimeMapEditorChromePresenter.DrawSettingsPanel(
                    settingsPanelRect,
                    panelStyle,
                    headerStyle,
                    bodyStyle,
                    buttonStyle,
                    model);
            autoSavePlayerMap = interaction.AutoSaveEnabled;
            autoLoadPlayerMapOnStart = interaction.AutoLoadEnabled;

            if (interaction.SavePlayerMapRequested)
            {
                SavePlayerMap();
            }

            if (interaction.LoadPlayerMapRequested)
            {
                LoadPlayerMap();
            }

            if (interaction.ExportAuthoringRequested)
            {
                ExportRuntimeAuthoringPackage();
            }

            if (interaction.RestoreAuthoringRequested)
            {
                RestoreRuntimeAuthoringPackage();
            }
        }

        private void ResolveLayoutRects()
        {
            float toolbarY = Mathf.Max(Screen.height - BottomToolbarHeight - PanelMargin, TopMargin + 340f);
            bottomToolbarRect = new Rect(PanelMargin, toolbarY, Mathf.Max(360f, Screen.width - PanelMargin * 2f), BottomToolbarHeight);

            float rightWidth = Mathf.Clamp(Screen.width * 0.2f, 320f, 420f);
            float availableLeftWidth = Screen.width - rightWidth - PanelMargin * 3f;
            float leftWidth = Mathf.Clamp(Screen.width * 0.28f, 360f, 520f);
            leftWidth = Mathf.Min(leftWidth, Mathf.Max(320f, availableLeftWidth));
            float panelHeight = Mathf.Max(340f, toolbarY - TopMargin - PanelMargin);
            leftPanelRect = new Rect(PanelMargin, TopMargin, leftWidth, panelHeight);

            float rightX = Mathf.Max(leftPanelRect.xMax + PanelMargin, Screen.width - rightWidth - PanelMargin);
            rightWidth = Mathf.Min(rightWidth, Screen.width - rightX - PanelMargin);
            if (rightWidth < 280f)
            {
                rightWidth = 280f;
                rightX = Screen.width - rightWidth - PanelMargin;
            }

            float floorHeight = Mathf.Clamp(Screen.height * 0.24f, 220f, 320f);
            floorPanelRect = new Rect(rightX, TopMargin, rightWidth, floorHeight);
            float checklistY = floorPanelRect.yMax + 18f;
            float checklistHeight = Mathf.Max(220f, toolbarY - checklistY - PanelMargin);
            checklistPanelRect = new Rect(rightX, checklistY, rightWidth, checklistHeight);
            settingsPanelRect = new Rect(
                Mathf.Clamp(Screen.width - 460f - PanelMargin, PanelMargin, Screen.width - 460f),
                Mathf.Max(TopMargin, toolbarY - 390f),
                430f,
                Mathf.Min(360f, toolbarY - TopMargin - PanelMargin));

            float objectSettingsWidth = Mathf.Clamp(Screen.width * 0.36f, 520f, 760f);
            float objectSettingsHeight = Mathf.Clamp(toolbarY - TopMargin - PanelMargin, 500f, 800f);
            float objectSettingsX = leftPanelRect.xMax + PanelMargin;
            float objectSettingsRightLimit = floorPanelRect.x - PanelMargin;
            if (objectSettingsRightLimit - objectSettingsX < objectSettingsWidth)
            {
                objectSettingsX = Mathf.Clamp(
                    (Screen.width - objectSettingsWidth) * 0.5f,
                    PanelMargin,
                    Mathf.Max(PanelMargin, Screen.width - objectSettingsWidth - PanelMargin));
            }

            objectSettingsPanelRect = new Rect(
                objectSettingsX,
                TopMargin,
                objectSettingsWidth,
                Mathf.Min(objectSettingsHeight, toolbarY - TopMargin - PanelMargin));
            helpPanelRect = new Rect(
                Mathf.Max(PanelMargin, Screen.width * 0.5f - 360f),
                TopMargin + 30f,
                Mathf.Min(720f, Screen.width - PanelMargin * 2f),
                Mathf.Min(480f, toolbarY - TopMargin - 56f));
        }

        private bool HandleObjectDirectionSpriteDrop(Rect rect, int rotation90Index)
        {
#if UNITY_EDITOR
            Event current = Event.current;
            if (current == null || !rect.Contains(current.mousePosition))
            {
                return false;
            }

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
            {
                return false;
            }

            string sourcePath = ResolveEditorDraggedImagePath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                return false;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                TryAssignSelectedObjectDirectionSprite(rotation90Index, sourcePath);
            }

            current.Use();
            return true;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        private string ResolveEditorDraggedImagePath()
        {
            string[] paths = DragAndDrop.paths;
            if (paths != null)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) && CampusRuntimeImportLibrary.IsSupportedImage(path))
                    {
                        return path;
                    }
                }
            }

            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            if (references != null)
            {
                for (int i = 0; i < references.Length; i++)
                {
                    string assetPath = AssetDatabase.GetAssetPath(references[i]);
                    if (string.IsNullOrWhiteSpace(assetPath))
                    {
                        continue;
                    }

                    string fullPath = Path.GetFullPath(assetPath);
                    if (File.Exists(fullPath) && CampusRuntimeImportLibrary.IsSupportedImage(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return string.Empty;
        }
#endif

        private string BuildObjectSettingsInputKey(CampusPlacedObject placed, string fieldName)
        {
            string objectId = placed != null && !string.IsNullOrWhiteSpace(placed.ObjectId) ? placed.ObjectId : "object";
            return "object_settings_" + objectId + "_" + fieldName;
        }

        CampusRuntimeMapEditorObjectSettingsSession ICampusRuntimeMapEditorObjectSettingsInspectorHost.ObjectSettingsSession => objectSettingsSession;
        GUIStyle ICampusRuntimeMapEditorObjectSettingsInspectorHost.HeaderStyle => headerStyle;
        GUIStyle ICampusRuntimeMapEditorObjectSettingsInspectorHost.BodyStyle => bodyStyle;
        GUIStyle ICampusRuntimeMapEditorObjectSettingsInspectorHost.MutedStyle => mutedStyle;
        GUIStyle ICampusRuntimeMapEditorObjectSettingsInspectorHost.ButtonStyle => buttonStyle;
        GUIStyle ICampusRuntimeMapEditorObjectSettingsInspectorHost.SelectedButtonStyle => selectedButtonStyle;
        Texture2D ICampusRuntimeMapEditorObjectSettingsInspectorHost.LineTexture => lineTexture;
        Texture2D ICampusRuntimeMapEditorObjectSettingsInspectorHost.TileFallbackTexture => tileFallbackTexture;

        int ICampusRuntimeMapEditorObjectSettingsInspectorHost.SelectedObjectFootprintX
        {
            get => selectedObjectFootprintX;
            set => selectedObjectFootprintX = value;
        }

        int ICampusRuntimeMapEditorObjectSettingsInspectorHost.SelectedObjectFootprintY
        {
            get => selectedObjectFootprintY;
            set => selectedObjectFootprintY = value;
        }

        float ICampusRuntimeMapEditorObjectSettingsInspectorHost.ObjectSettingsMinScale => ObjectSettingsMinScale;
        float ICampusRuntimeMapEditorObjectSettingsInspectorHost.ObjectSettingsMaxScale => ObjectSettingsMaxScale;
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.SyncSelectedObjectFootprintFields() => SyncSelectedObjectFootprintFields();
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.ApplySelectedObjectFootprint() => ApplySelectedObjectFootprint();
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.ConfigureWallMountedSettings(CampusPlacedObject placed, bool enabled, bool clearDirectionalOverrides) => ConfigureWallMountedSettings(placed, enabled, clearDirectionalOverrides);
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.SetSelectedWallMountedSprite() => SetSelectedWallMountedSprite();
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.ClearSelectedWallMountedSprite() => ClearSelectedWallMountedSprite();
        CampusRetailShelf ICampusRuntimeMapEditorObjectSettingsInspectorHost.EnsureRetailShelfForAuthoring(CampusPlacedObject placed) => EnsureRetailShelfForAuthoring(placed);
        string ICampusRuntimeMapEditorObjectSettingsInspectorHost.ResolveRetailShelfModeLabel(CampusRetailShelfMode shelfMode) => ResolveRetailShelfModeLabel(shelfMode);
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.SaveSelectedObjectSettings() => SaveSelectedObjectSettings();
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.SetSelectedObjectDirectionSprite(int rotation90Index) => SetSelectedObjectDirectionSprite(rotation90Index);
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.ClearSelectedObjectDirectionSprite(int rotation90Index) => ClearSelectedObjectDirectionSprite(rotation90Index);
        bool ICampusRuntimeMapEditorObjectSettingsInspectorHost.HandleObjectDirectionSpriteDrop(Rect rect, int rotation90Index) => HandleObjectDirectionSpriteDrop(rect, rotation90Index);
        Sprite ICampusRuntimeMapEditorObjectSettingsInspectorHost.GetObjectDirectionSprite(CampusPlacedObject placed, int rotation90Index) => GetObjectDirectionSprite(placed, rotation90Index);
        string ICampusRuntimeMapEditorObjectSettingsInspectorHost.GetObjectDisplayName(GameObject prefab) => GetObjectDisplayName(prefab);
        string ICampusRuntimeMapEditorObjectSettingsInspectorHost.GetText(CampusRuntimeEditorTextId id) => CampusRuntimeEditorTextCatalog.Get(displayLanguage, id);
        string ICampusRuntimeMapEditorObjectSettingsInspectorHost.TranslateText(string chinese, string english) => Tr(chinese, english);
        string ICampusRuntimeMapEditorObjectSettingsInspectorHost.TranslateText(CampusRuntimeEditorTextId id) => Tr(id);
        string ICampusRuntimeMapEditorObjectSettingsInspectorHost.Truncate(string value, int maxCharacters) => Truncate(value, maxCharacters);
        int ICampusRuntimeMapEditorObjectSettingsInspectorHost.ParseIntField(Rect rect, int value, string key) => ParseIntField(rect, value, key);
        float ICampusRuntimeMapEditorObjectSettingsInspectorHost.ParseFloatField(Rect rect, float value, string key) => ParseFloatField(rect, value, key);
        string ICampusRuntimeMapEditorObjectSettingsInspectorHost.DrawTextInput(Rect rect, string value, string key) => DrawTextInput(rect, value, key);
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.SetTextInputDraft(string key, string value) => textInputDrafts[key] = value ?? string.Empty;
        string ICampusRuntimeMapEditorObjectSettingsInspectorHost.BuildObjectSettingsInputKey(CampusPlacedObject placed, string fieldName) => BuildObjectSettingsInputKey(placed, fieldName);
        void ICampusRuntimeMapEditorObjectSettingsInspectorHost.DrawSprite(Rect rect, Sprite sprite) => DrawSprite(rect, sprite);

        private void ImportSelectedFilesIntoFolder(string folder, string label)
        {
#if UNITY_EDITOR
            string path = SelectSingleFile(TrFormat("\u5bfc\u5165 {0}", "Import {0}", label), Tr("\u5168\u90e8", "All") + "|*.*");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (CampusRuntimeImportLibrary.ImportFiles(new[] { path }, folder, false) > 0)
            {
                LoadRuntimeResources();
                RefreshImportAssetDatabaseIfProjectBacked();
            }
#endif
        }

        private void ImportSelectedFolderIntoFolder(string folder, string label)
        {
#if UNITY_EDITOR
            string source = EditorUtility.OpenFolderPanel(TrFormat("\u5bfc\u5165 {0}", "Import {0}", label), Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            CampusRuntimeImportLibrary.MirrorDirectory(source, folder, false);
            LoadRuntimeResources();
            RefreshImportAssetDatabaseIfProjectBacked();
#endif
        }

        private int ImportClipboardImagesIntoFolder(string folder, string label)
        {
            string buffer = GUIUtility.systemCopyBuffer ?? string.Empty;
            if (string.IsNullOrWhiteSpace(buffer))
            {
                return 0;
            }

            int count = CampusRuntimeImportLibrary.ImportClipboardImages(buffer, folder);
            if (count > 0)
            {
                LoadRuntimeResources();
                RefreshImportAssetDatabaseIfProjectBacked();
            }

            return count;
        }

        private string SelectSingleImageFile(string title)
        {
            return SelectSingleFile(title, Tr("\u56fe\u7247", "Image") + "|*.png;*.jpg;*.jpeg;*.bmp|" + Tr("\u5168\u90e8", "All") + "|*.*");
        }

        private string SelectSingleFile(string title, string filter)
        {
#if UNITY_EDITOR
            string[] parts = (filter ?? "All|*.*").Split('|');
            if (parts.Length >= 2)
            {
                string extension = ExtractEditorFileExtension(parts[1]);
                return EditorUtility.OpenFilePanel(title, Application.dataPath, extension);
            }
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return OpenSingleFilePanelWindows(title, filter);
#else
            return string.Empty;
#endif
        }

        private static string ExtractEditorFileExtension(string rawFilterPattern)
        {
            if (string.IsNullOrWhiteSpace(rawFilterPattern))
            {
                return string.Empty;
            }

            string[] patterns = rawFilterPattern.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < patterns.Length; i++)
            {
                string pattern = patterns[i].Trim();
                if (pattern.StartsWith("*.", StringComparison.Ordinal))
                {
                    return pattern.Substring(2);
                }
            }

            return string.Empty;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static string OpenSingleFilePanelWindows(string title, string filter)
        {
            try
            {
                return CampusRuntimeNativeFileDialog.OpenSingleFile(title, filter);
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.NativeFileDialogFailed,
                    exception.Message);
                return string.Empty;
            }
        }
#endif

        private string ResolveImportTargetLabel(CampusRuntimeImportTarget target)
        {
            switch (target)
            {
                case CampusRuntimeImportTarget.Floor:
                    return Tr(CampusRuntimeEditorTextId.FloorImports);
                case CampusRuntimeImportTarget.Wall:
                    return Tr(CampusRuntimeEditorTextId.WallImports);
                case CampusRuntimeImportTarget.Object:
                    return Tr(CampusRuntimeEditorTextId.ObjectImports);
                case CampusRuntimeImportTarget.Room:
                    return Tr(CampusRuntimeEditorTextId.RoomList);
                case CampusRuntimeImportTarget.WallFace:
                    return Tr("\u5899\u9762\u8d34\u56fe", "Wall Face Texture");
                case CampusRuntimeImportTarget.WallCap:
                    return Tr("\u5899\u9876\u8d34\u56fe", "Wall Cap Texture");
                default:
                    return Tr("\u5bfc\u5165", "Import");
            }
        }

        private string Tr(CampusRuntimeEditorTextId id)
        {
            return CampusRuntimeEditorTextCatalog.Get(displayLanguage, id);
        }

        private string Tr(
            string chinese,
            string english,
            string traditionalChinese = null,
            string russian = null,
            string japanese = null)
        {
            return CampusRuntimeEditorTextCatalog.Get(
                displayLanguage,
                chinese,
                english,
                traditionalChinese,
                russian,
                japanese);
        }

        private string Tr(CampusLocalizedTextEntry text)
        {
            return text.Get(displayLanguage);
        }

        private string TrFormat(string chinese, string english, params object[] args)
        {
            return CampusRuntimeEditorTextCatalog.Format(displayLanguage, chinese, english, args);
        }

        private void SetStatus(string message)
        {
            statusText = message;
            statusUntil = Time.realtimeSinceStartup + 4f;
        }

        private static bool WasKeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            KeyControl control = GetKeyControl(key);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        private static bool IsKeyHeld(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            KeyControl control = GetKeyControl(key);
            return control != null && control.isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        private static bool WasMouseButtonPressed(int button)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current == null)
            {
                return false;
            }

            if (button == 0)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }

            if (button == 1)
            {
                return Mouse.current.rightButton.wasPressedThisFrame;
            }

            return Mouse.current.middleButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
#endif
        }

        private static bool WasMouseButtonReleased(int button)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current == null)
            {
                return false;
            }

            if (button == 0)
            {
                return Mouse.current.leftButton.wasReleasedThisFrame;
            }

            if (button == 1)
            {
                return Mouse.current.rightButton.wasReleasedThisFrame;
            }

            return Mouse.current.middleButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(button);
#endif
        }

        private static bool IsMouseButtonHeld(int button)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current == null)
            {
                return false;
            }

            if (button == 0)
            {
                return Mouse.current.leftButton.isPressed;
            }

            if (button == 1)
            {
                return Mouse.current.rightButton.isPressed;
            }

            return Mouse.current.middleButton.isPressed;
#else
            return Input.GetMouseButton(button);
#endif
        }

        private static Vector2 GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private static float GetMouseScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private static KeyControl GetKeyControl(KeyCode key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return null;
            }

            switch (key)
            {
                case KeyCode.F10:
                    return keyboard.f10Key;
                case KeyCode.Escape:
                    return keyboard.escapeKey;
                case KeyCode.G:
                    return keyboard.gKey;
                case KeyCode.R:
                    return keyboard.rKey;
                case KeyCode.Z:
                    return keyboard.zKey;
                case KeyCode.Y:
                    return keyboard.yKey;
                case KeyCode.LeftBracket:
                    return keyboard.leftBracketKey;
                case KeyCode.RightBracket:
                    return keyboard.rightBracketKey;
                case KeyCode.LeftShift:
                    return keyboard.leftShiftKey;
                case KeyCode.RightShift:
                    return keyboard.rightShiftKey;
                case KeyCode.LeftControl:
                    return keyboard.leftCtrlKey;
                case KeyCode.RightControl:
                    return keyboard.rightCtrlKey;
                case KeyCode.LeftAlt:
                    return keyboard.leftAltKey;
                case KeyCode.RightAlt:
                    return keyboard.rightAltKey;
                case KeyCode.Space:
                    return keyboard.spaceKey;
                default:
                    return null;
            }
        }
#endif
    }
}

