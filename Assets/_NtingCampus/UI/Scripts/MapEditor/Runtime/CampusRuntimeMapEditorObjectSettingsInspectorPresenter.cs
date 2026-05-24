using NtingCampus.Gameplay.Retail;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public interface ICampusRuntimeMapEditorObjectSettingsInspectorHost
    {
        CampusRuntimeMapEditorObjectSettingsSession ObjectSettingsSession { get; }
        GUIStyle HeaderStyle { get; }
        GUIStyle BodyStyle { get; }
        GUIStyle MutedStyle { get; }
        GUIStyle ButtonStyle { get; }
        GUIStyle SelectedButtonStyle { get; }
        Texture2D LineTexture { get; }
        Texture2D TileFallbackTexture { get; }
        int SelectedObjectFootprintX { get; set; }
        int SelectedObjectFootprintY { get; set; }
        float ObjectSettingsMinScale { get; }
        float ObjectSettingsMaxScale { get; }

        void SyncSelectedObjectFootprintFields();
        void ApplySelectedObjectFootprint();
        void ConfigureWallMountedSettings(CampusPlacedObject placed, bool enabled, bool clearDirectionalOverrides);
        void SetSelectedWallMountedSprite();
        void ClearSelectedWallMountedSprite();
        CampusRetailShelf EnsureRetailShelfForAuthoring(CampusPlacedObject placed);
        string ResolveRetailShelfModeLabel(CampusRetailShelfMode shelfMode);
        void SaveSelectedObjectSettings();
        void SetSelectedObjectDirectionSprite(int rotation90Index);
        void ClearSelectedObjectDirectionSprite(int rotation90Index);
        bool HandleObjectDirectionSpriteDrop(Rect rect, int rotation90Index);
        Sprite GetObjectDirectionSprite(CampusPlacedObject placed, int rotation90Index);
        string GetObjectDisplayName(GameObject prefab);
        string GetText(CampusRuntimeEditorTextId id);
        string TranslateText(string chinese, string english);
        string TranslateText(CampusRuntimeEditorTextId id);
        string Truncate(string value, int maxCharacters);
        int ParseIntField(Rect rect, int value, string key = null);
        float ParseFloatField(Rect rect, float value, string key = null);
        string DrawTextInput(Rect rect, string value, string key);
        void SetTextInputDraft(string key, string value);
        string BuildObjectSettingsInputKey(CampusPlacedObject placed, string fieldName);
        void DrawSprite(Rect rect, Sprite sprite);
    }

    public static class CampusRuntimeMapEditorObjectSettingsInspectorPresenter
    {
        public static void DrawContents(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            GameObject prefab,
            CampusPlacedObject placed)
        {
            DrawRenameControls(host, ref y, width, prefab, placed);
            DrawTypeIdControls(host, ref y, width, placed);
            DrawWallMountControls(host, ref y, width, placed);
            DrawPreviewControls(host, ref y, width, prefab, placed);
            DrawFootprintControls(host, ref y, width, placed);
            DrawStorageControls(host, ref y, width, placed);
            DrawRetailControls(host, ref y, width, placed);
            DrawScaleControls(host, ref y, width, placed);
            DrawRotationControls(host, ref y, width, placed);
            DrawAnchorControls(host, ref y, width, placed);
        }

        private static void DrawRenameControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            GameObject prefab,
            CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, 96f, 28f), host.GetText(CampusRuntimeEditorTextId.DisplayName), host.BodyStyle);
            string key = host.BuildObjectSettingsInputKey(placed, "display_name");
            string current = string.IsNullOrEmpty(placed.DisplayNameOverride) ? string.Empty : placed.DisplayNameOverride;
            string next = host.DrawTextInput(new Rect(102f, y, width - 102f, 30f), current, key);
            placed.DisplayNameOverride = string.IsNullOrWhiteSpace(next) ? string.Empty : next.Trim();
            y += 40f;
        }

        private static void DrawTypeIdControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            GUI.Label(new Rect(0f, y, 96f, 28f), host.TranslateText("类型 ID", "Type ID"), host.BodyStyle);
            string key = host.BuildObjectSettingsInputKey(placed, "type_id");
            string current = string.IsNullOrEmpty(placed.TypeId) ? string.Empty : placed.TypeId;
            string next = host.DrawTextInput(new Rect(102f, y, Mathf.Max(40f, width - 168f), 30f), current, key);
            placed.TypeId = string.IsNullOrWhiteSpace(next) ? string.Empty : next.Trim();
            if (GUI.Button(new Rect(width - 58f, y, 58f, 28f), host.GetText(CampusRuntimeEditorTextId.Clear), host.ButtonStyle))
            {
                placed.TypeId = string.Empty;
                host.SetTextInputDraft(key, string.Empty);
            }

            y += 34f;
            GUI.Label(
                new Rect(0f, y, width, 54f),
                host.TranslateText(
                    "用于物品或设施判定的稳定 ID。例：StudentDesk、OfficeDesk、Blackboard、Podium、Storage。",
                    "Stable ID for object/facility checks. Examples: StudentDesk, OfficeDesk, Blackboard, Podium, Storage."),
                host.MutedStyle);
            y += 62f;
        }

        private static void DrawWallMountControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            bool nextWallMounted = GUI.Toggle(new Rect(0f, y, width, 24f), placed.IsWallMounted, host.TranslateText("墙挂物体", "Wall Mounted Object"));
            if (nextWallMounted != placed.IsWallMounted)
            {
                host.ConfigureWallMountedSettings(placed, nextWallMounted, true);
            }

            y += 30f;
            if (!placed.IsWallMounted)
            {
                return;
            }

            string spriteName = placed.Rotation0Sprite != null
                ? host.Truncate(placed.Rotation0Sprite.name, 22)
                : host.GetText(CampusRuntimeEditorTextId.NotSet);
            GUI.Label(new Rect(0f, y, 84f, 28f), host.TranslateText("主贴图", "Main Sprite"), host.BodyStyle);
            GUI.Box(new Rect(88f, y, Mathf.Max(10f, width - 216f), 30f), spriteName, host.ButtonStyle);
            if (GUI.Button(new Rect(width - 120f, y, 56f, 28f), host.GetText(CampusRuntimeEditorTextId.PickSprite), host.ButtonStyle))
            {
                host.SetSelectedWallMountedSprite();
            }

            if (GUI.Button(new Rect(width - 58f, y, 56f, 28f), host.GetText(CampusRuntimeEditorTextId.Clear), host.ButtonStyle))
            {
                host.ClearSelectedWallMountedSprite();
            }

            y += 34f;
            GUI.Label(
                new Rect(0f, y, width, 42f),
                host.TranslateText(
                    "墙挂模式只使用这一张贴图，场景中会自动生成薄 3D 板并吸附到墙体。",
                    "Wall-mounted mode only uses this sprite. The scene generates a thin 3D plate and snaps it to the wall."),
                host.MutedStyle);
            y += 48f;
        }

        private static void DrawPreviewControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            GameObject prefab,
            CampusPlacedObject placed)
        {
            bool usesDirectionalSprite;
            int effectiveRotation;
            Sprite sprite = ResolvePreviewSprite(host, prefab, placed, out usesDirectionalSprite, out effectiveRotation);
            string spriteName = sprite != null ? sprite.name : host.GetText(CampusRuntimeEditorTextId.NoSprite);
            GUI.Label(new Rect(0f, y, width, 24f), host.GetText(CampusRuntimeEditorTextId.PreviewSprite), host.HeaderStyle);
            y += 28f;
            Vector2Int footprint = CampusPlacedObject.RotateFootprintSize(placed.NormalizedFootprintSize, host.ObjectSettingsSession.PreviewRotation90);
            float previewSize = Mathf.Clamp(width * 0.52f, 156f, 232f);
            Rect previewRect = new Rect(0f, y, previewSize, previewSize);
            GUI.Box(previewRect, GUIContent.none, host.ButtonStyle);
            Rect gridRect = DrawPreviewGrid(host, previewRect, footprint, sprite, placed, usesDirectionalSprite, effectiveRotation);
            HandlePreviewAnchorInput(host, previewRect, gridRect, footprint, placed);

            float textX = previewRect.xMax + 12f;
            float textWidth = Mathf.Max(10f, width - textX);
            GUI.Label(new Rect(textX, y + 4f, textWidth, 24f), host.Truncate(spriteName, 32), host.MutedStyle);
            GUI.Label(new Rect(textX, y + 30f, textWidth, 24f), host.TranslateText(CampusRuntimeEditorTextId.PreviewGrid) + ": " + footprint.x + "x" + footprint.y, host.MutedStyle);
            string previewMode = placed != null && placed.IsWallMounted
                ? host.TranslateText("墙挂吸附", "Wall Snap")
                : usesDirectionalSprite
                    ? host.TranslateText(CampusRuntimeEditorTextId.DirectionalSprite)
                    : host.TranslateText(CampusRuntimeEditorTextId.DefaultSprite);
            GUI.Label(new Rect(textX, y + 56f, textWidth, 24f), previewMode + " / " + (effectiveRotation * 90) + " deg", host.MutedStyle);
            GUI.Label(new Rect(textX, y + 82f, textWidth, 44f), host.GetText(CampusRuntimeEditorTextId.ClickPreviewToPlaceAnchor), host.MutedStyle);
            y += previewSize + 14f;
        }

        private static void DrawFootprintControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            CampusPlacedObject placed)
        {
            if (placed != null && placed.IsWallMounted)
            {
                placed.OverrideFootprintSize = true;
                placed.FootprintSize = Vector2Int.one;
                GUI.Label(new Rect(0f, y, width, 24f), host.TranslateText("墙挂物体固定使用 1x1 墙体锚点。", "Wall-mounted objects always use a 1x1 wall anchor."), host.MutedStyle);
                y += 34f;
                return;
            }

            DrawSelectedObjectFootprintControls(host, ref y, width);
        }

        private static void DrawSelectedObjectFootprintControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width)
        {
            host.SyncSelectedObjectFootprintFields();
            GUI.Label(new Rect(0f, y, 70f, 28f), host.GetText(CampusRuntimeEditorTextId.Footprint), host.BodyStyle);
            host.SelectedObjectFootprintX = Mathf.Clamp(host.ParseIntField(new Rect(72f, y, 48f, 30f), host.SelectedObjectFootprintX), 1, 32);
            GUI.Label(new Rect(126f, y, 20f, 28f), "x", host.BodyStyle);
            host.SelectedObjectFootprintY = Mathf.Clamp(host.ParseIntField(new Rect(148f, y, 48f, 30f), host.SelectedObjectFootprintY), 1, 32);
            if (GUI.Button(new Rect(208f, y, 92f, 30f), host.GetText(CampusRuntimeEditorTextId.ApplySize), host.ButtonStyle))
            {
                host.ApplySelectedObjectFootprint();
            }

            string name = host.GetObjectDisplayName(null);
            GUI.Label(new Rect(306f, y, Mathf.Max(10f, width - 306f), 28f), host.Truncate(name, 12), host.MutedStyle);
            y += 46f;
        }

        private static void DrawStorageControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            CampusPlacedObject placed)
        {
            if (placed != null && placed.IsWallMounted)
            {
                placed.BlocksMovement = false;
                placed.BlocksSight = false;
            }

            CampusRetailShelf retailShelf = placed != null ? placed.GetComponent<CampusRetailShelf>() : null;
            bool retailOwnsStorage = retailShelf != null;
            if (retailOwnsStorage)
            {
                placed.IsStorageContainer = retailShelf.ShelfMode == CampusRetailShelfMode.Container;
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !retailOwnsStorage;
            placed.IsStorageContainer = GUI.Toggle(new Rect(0f, y, width, 24f), placed.IsStorageContainer, host.GetText(CampusRuntimeEditorTextId.StorageContainer));
            y += 30f;
            GUI.enabled = previousEnabled && placed.IsStorageContainer;
            GUI.Label(new Rect(0f, y, 56f, 28f), host.GetText(CampusRuntimeEditorTextId.Size), host.BodyStyle);
            placed.StorageSize = new Vector2Int(
                Mathf.Clamp(host.ParseIntField(new Rect(60f, y, 48f, 30f), placed.NormalizedStorageSize.x), 1, 64),
                Mathf.Clamp(host.ParseIntField(new Rect(136f, y, 48f, 30f), placed.NormalizedStorageSize.y), 1, 64));
            GUI.Label(new Rect(114f, y, 18f, 28f), "x", host.BodyStyle);
            GUI.enabled = previousEnabled;
            y += 40f;
        }

        private static void DrawRetailControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            CampusPlacedObject placed)
        {
            CampusRetailShelf shelf = host.EnsureRetailShelfForAuthoring(placed);
            if (shelf == null)
            {
                return;
            }

            GUI.Label(new Rect(0f, y, width, 24f), host.GetText(CampusRuntimeEditorTextId.RetailShelfSection), host.HeaderStyle);
            y += 30f;

            GUI.Label(new Rect(0f, y, 96f, 28f), host.GetText(CampusRuntimeEditorTextId.RetailShelfMode), host.BodyStyle);
            GUI.Label(new Rect(102f, y, width - 102f, 28f), host.ResolveRetailShelfModeLabel(shelf.ShelfMode), host.MutedStyle);
            y += 36f;

            string itemKey = host.BuildObjectSettingsInputKey(placed, "retail_item_id");
            GUI.Label(new Rect(0f, y, 96f, 28f), host.GetText(CampusRuntimeEditorTextId.RetailItemDefinitionId), host.BodyStyle);
            string currentItemId = string.IsNullOrEmpty(shelf.ItemDefinitionId) ? string.Empty : shelf.ItemDefinitionId;
            string nextItemId = host.DrawTextInput(new Rect(102f, y, Mathf.Max(40f, width - 168f), 30f), currentItemId, itemKey);
            shelf.ItemDefinitionId = string.IsNullOrWhiteSpace(nextItemId) ? string.Empty : nextItemId.Trim();
            if (GUI.Button(new Rect(width - 58f, y, 58f, 28f), host.GetText(CampusRuntimeEditorTextId.Clear), host.ButtonStyle))
            {
                shelf.ItemDefinitionId = string.Empty;
                host.SetTextInputDraft(itemKey, string.Empty);
            }

            y += 38f;
            shelf.AutoRestock = GUI.Toggle(new Rect(0f, y, width, 24f), shelf.AutoRestock, host.GetText(CampusRuntimeEditorTextId.RetailAutoRestock));
            y += 32f;

            GUI.Label(new Rect(0f, y, 96f, 28f), host.GetText(CampusRuntimeEditorTextId.RetailStockCount), host.BodyStyle);
            shelf.StockCount = Mathf.Clamp(host.ParseIntField(new Rect(102f, y, 64f, 30f), shelf.StockCount, host.BuildObjectSettingsInputKey(placed, "retail_stock_count")), 1, 999);
            y += 36f;

            GUI.Label(new Rect(0f, y, 96f, 28f), host.GetText(CampusRuntimeEditorTextId.RetailDisplayCount), host.BodyStyle);
            shelf.DisplaySlotCount = Mathf.Clamp(host.ParseIntField(new Rect(102f, y, 64f, 30f), shelf.DisplaySlotCount, host.BuildObjectSettingsInputKey(placed, "retail_display_count")), 1, 999);
            y += 42f;
        }

        private static void DrawScaleControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            CampusPlacedObject placed)
        {
            Vector2 scale = placed.NormalizedVisualScale;
            GUI.Label(new Rect(0f, y, 80f, 28f), host.GetText(CampusRuntimeEditorTextId.Scale), host.BodyStyle);
            placed.LockVisualScaleAspect = GUI.Toggle(new Rect(84f, y + 4f, width - 84f, 24f), placed.LockVisualScaleAspect, host.GetText(CampusRuntimeEditorTextId.LockAspect));
            y += 34f;
            if (placed.LockVisualScaleAspect)
            {
                float uniform = Mathf.Clamp(scale.x, host.ObjectSettingsMinScale, host.ObjectSettingsMaxScale);
                GUI.Label(new Rect(0f, y, 90f, 24f), host.GetText(CampusRuntimeEditorTextId.UniformScale), host.BodyStyle);
                uniform = GUI.HorizontalSlider(new Rect(96f, y + 8f, Mathf.Max(40f, width - 170f), 16f), uniform, host.ObjectSettingsMinScale, host.ObjectSettingsMaxScale);
                uniform = host.ParseFloatField(new Rect(width - 66f, y, 66f, 30f), uniform, host.BuildObjectSettingsInputKey(placed, "uniform_scale"));
                uniform = Mathf.Clamp(uniform, host.ObjectSettingsMinScale, host.ObjectSettingsMaxScale);
                scale = new Vector2(uniform, uniform);
                y += 34f;
            }
            else
            {
                scale.x = Mathf.Clamp(scale.x, host.ObjectSettingsMinScale, host.ObjectSettingsMaxScale);
                scale.y = Mathf.Clamp(scale.y, host.ObjectSettingsMinScale, host.ObjectSettingsMaxScale);
            }

            scale.x = host.ParseFloatField(new Rect(0f, y, 70f, 30f), scale.x, host.BuildObjectSettingsInputKey(placed, "scale_x"));
            GUI.Label(new Rect(76f, y, 18f, 28f), "x", host.BodyStyle);
            scale.y = host.ParseFloatField(new Rect(98f, y, 70f, 30f), scale.y, host.BuildObjectSettingsInputKey(placed, "scale_y"));
            GUI.Label(new Rect(176f, y, Mathf.Max(10f, width - 176f), 24f), host.TranslateText(CampusRuntimeEditorTextId.PreviewGrid) + " " + placed.NormalizedFootprintSize.x + "x" + placed.NormalizedFootprintSize.y, host.MutedStyle);

            placed.VisualScale = CampusPlacedObject.NormalizeVisualScale(scale);
            placed.ApplyVisualScaleState();
            y += 42f;
        }

        private static void DrawRotationControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            CampusPlacedObject placed)
        {
            if (placed != null && placed.IsWallMounted)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                GUI.Label(new Rect(0f, y, width, 24f), host.TranslateText("旋转决定吸附墙面", "Rotation chooses the snapped wall face"), host.BodyStyle);
                y += 28f;
                float wallFaceButtonWidth = Mathf.Max(54f, (width - 24f) * 0.25f);
                string[] faceLabels =
                {
                    host.TranslateText("0 度 北", "0 deg North"),
                    host.TranslateText("90 度 东", "90 deg East"),
                    host.TranslateText("180 度 南", "180 deg South"),
                    host.TranslateText("270 度 西", "270 deg West")
                };
                for (int i = 0; i < 4; i++)
                {
                    Rect buttonRect = new Rect((wallFaceButtonWidth + 8f) * i, y, wallFaceButtonWidth, 28f);
                    GUIStyle style = host.ObjectSettingsSession.PreviewRotation90 == i ? host.SelectedButtonStyle : host.ButtonStyle;
                    if (GUI.Button(buttonRect, faceLabels[i], style))
                    {
                        host.ObjectSettingsSession.PreviewRotation90 = i;
                    }
                }

                y += 40f;
                GUI.Label(new Rect(0f, y, width, 42f), host.TranslateText("墙挂物体只使用一张主贴图，预览旋转表示吸附的墙面。", "Wall-mounted objects use one main sprite. Preview rotation shows the snapped wall face."), host.MutedStyle);
                y += 48f;
                return;
            }

            bool allowRotation = GUI.Toggle(new Rect(0f, y, width, 24f), placed.AllowRotation, host.GetText(CampusRuntimeEditorTextId.AllowFourDirections));
            if (allowRotation != placed.AllowRotation)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = allowRotation;
                placed.ApplyRotationVisualState();
            }

            y += 30f;
            GUI.Label(new Rect(0f, y, width, 24f), host.GetText(CampusRuntimeEditorTextId.RotationPreview), host.BodyStyle);
            y += 28f;
            float previewButtonWidth = Mathf.Max(54f, (width - 24f) * 0.25f);
            for (int i = 0; i < 4; i++)
            {
                Rect buttonRect = new Rect((previewButtonWidth + 8f) * i, y, previewButtonWidth, 28f);
                GUIStyle style = host.ObjectSettingsSession.PreviewRotation90 == i ? host.SelectedButtonStyle : host.ButtonStyle;
                if (GUI.Button(buttonRect, (i * 90) + " deg", style))
                {
                    host.ObjectSettingsSession.PreviewRotation90 = i;
                }
            }

            y += 36f;
            for (int i = 0; i < 4; i++)
            {
                int degrees = i * 90;
                GUI.Label(new Rect(0f, y, 42f, 24f), degrees + " deg", host.BodyStyle);
                Sprite directionSprite = host.GetObjectDirectionSprite(placed, i);
                string spriteName = directionSprite != null
                    ? host.Truncate(directionSprite.name, 18)
                    : host.GetText(CampusRuntimeEditorTextId.NotSet);
                Rect directionRect = new Rect(48f, y, Mathf.Max(90f, width - 176f), 28f);
                bool dragHover = host.HandleObjectDirectionSpriteDrop(directionRect, i);
                GUIStyle rowStyle = host.ObjectSettingsSession.PreviewRotation90 == i ? host.SelectedButtonStyle : host.ButtonStyle;
                if (dragHover)
                {
                    GUI.Box(directionRect, GUIContent.none, host.SelectedButtonStyle);
                }

                if (GUI.Button(directionRect, spriteName, rowStyle))
                {
                    host.ObjectSettingsSession.PreviewRotation90 = i;
                }

                if (GUI.Button(new Rect(width - 120f, y, 56f, 28f), host.GetText(CampusRuntimeEditorTextId.PickSprite), host.ButtonStyle))
                {
                    host.SetSelectedObjectDirectionSprite(i);
                }

                if (GUI.Button(new Rect(width - 58f, y, 56f, 28f), host.GetText(CampusRuntimeEditorTextId.Clear), host.ButtonStyle))
                {
                    host.ClearSelectedObjectDirectionSprite(i);
                }

                y += 32f;
            }

            y += 4f;
        }

        private static void DrawAnchorControls(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            ref float y,
            float width,
            CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 24f), host.GetText(CampusRuntimeEditorTextId.MultiInteractionAnchors), host.HeaderStyle);
            y += 28f;
            placed.UseCustomInteractionAnchor = GUI.Toggle(new Rect(0f, y, width, 24f), placed.UseCustomInteractionAnchor, host.GetText(CampusRuntimeEditorTextId.UseCustomInteractionAnchor));
            y += 30f;
            if (!placed.UseCustomInteractionAnchor)
            {
                return;
            }

            placed.CustomInteractionAnchors = placed.CustomInteractionAnchors ?? new System.Collections.Generic.List<CampusPlacedObjectInteractionAnchor>();
            if (placed.CustomInteractionAnchors.Count == 0)
            {
                host.ObjectSettingsSession.AddCustomInteractionAnchor(placed, host.GetText(CampusRuntimeEditorTextId.SelectedAnchor));
            }

            host.ObjectSettingsSession.EnsureSelectedCustomAnchorIndex(placed);
            float buttonWidth = Mathf.Max(92f, (width - 8f) * 0.5f);
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), host.GetText(CampusRuntimeEditorTextId.AddAnchor), host.ButtonStyle))
            {
                host.ObjectSettingsSession.AddCustomInteractionAnchor(placed, host.GetText(CampusRuntimeEditorTextId.SelectedAnchor));
            }

            GUI.enabled = placed.CustomInteractionAnchors.Count > 0;
            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), host.GetText(CampusRuntimeEditorTextId.RemoveAnchor), host.ButtonStyle))
            {
                host.ObjectSettingsSession.RemoveSelectedCustomInteractionAnchor(placed);
            }

            GUI.enabled = true;
            y += 38f;
            GUI.Label(new Rect(0f, y, width, 24f), host.GetText(CampusRuntimeEditorTextId.AnchorList), host.BodyStyle);
            y += 28f;
            for (int i = 0; i < placed.CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor listAnchor = placed.CustomInteractionAnchors[i];
                if (listAnchor == null)
                {
                    listAnchor = host.ObjectSettingsSession.CreateDefaultCustomAnchor(i, host.GetText(CampusRuntimeEditorTextId.SelectedAnchor));
                    placed.CustomInteractionAnchors[i] = listAnchor;
                }

                Rect selectRect = new Rect(0f, y, 148f, 28f);
                GUIStyle selectStyle = host.ObjectSettingsSession.SelectedCustomAnchorIndex == i ? host.SelectedButtonStyle : host.ButtonStyle;
                string anchorLabel = string.IsNullOrWhiteSpace(listAnchor.DisplayName) ? listAnchor.AnchorId : listAnchor.DisplayName;
                if (GUI.Button(selectRect, host.Truncate(anchorLabel, 16), selectStyle))
                {
                    host.ObjectSettingsSession.SelectedCustomAnchorIndex = i;
                    host.ObjectSettingsSession.SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
                }

                listAnchor.Enabled = GUI.Toggle(new Rect(156f, y + 4f, width - 156f, 24f), listAnchor.Enabled, host.GetText(CampusRuntimeEditorTextId.Enabled));
                y += 32f;
            }

            CampusPlacedObjectInteractionAnchor anchor = host.ObjectSettingsSession.GetSelectedCustomAnchor(placed);
            if (anchor == null)
            {
                return;
            }

            GUI.Label(new Rect(0f, y, width, 24f), host.GetText(CampusRuntimeEditorTextId.SelectedAnchor), host.BodyStyle);
            y += 28f;
            GUI.Label(new Rect(0f, y, 84f, 28f), host.GetText(CampusRuntimeEditorTextId.AnchorId), host.BodyStyle);
            anchor.AnchorId = host.DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.AnchorId) ? string.Empty : anchor.AnchorId, host.BuildObjectSettingsInputKey(placed, "anchor_id_" + host.ObjectSettingsSession.SelectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 84f, 28f), host.GetText(CampusRuntimeEditorTextId.DisplayName), host.BodyStyle);
            anchor.DisplayName = host.DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.DisplayName) ? string.Empty : anchor.DisplayName, host.BuildObjectSettingsInputKey(placed, "anchor_name_" + host.ObjectSettingsSession.SelectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 84f, 28f), host.GetText(CampusRuntimeEditorTextId.ActionId), host.BodyStyle);
            anchor.ActionId = host.DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.ActionId) ? string.Empty : anchor.ActionId, host.BuildObjectSettingsInputKey(placed, "anchor_action_" + host.ObjectSettingsSession.SelectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 84f, 28f), host.GetText(CampusRuntimeEditorTextId.Payload), host.BodyStyle);
            anchor.Payload = host.DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.Payload) ? string.Empty : anchor.Payload, host.BuildObjectSettingsInputKey(placed, "anchor_payload_" + host.ObjectSettingsSession.SelectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 84f, 28f), host.GetText(CampusRuntimeEditorTextId.Prompt), host.BodyStyle);
            anchor.PromptText = host.DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.PromptText) ? string.Empty : anchor.PromptText, host.BuildObjectSettingsInputKey(placed, "anchor_prompt_" + host.ObjectSettingsSession.SelectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 18f, 28f), host.GetText(CampusRuntimeEditorTextId.X), host.BodyStyle);
            Vector3 localPosition = anchor.LocalPosition;
            localPosition.x = host.ParseFloatField(new Rect(22f, y, 56f, 30f), localPosition.x, host.BuildObjectSettingsInputKey(placed, "anchor_x_" + host.ObjectSettingsSession.SelectedCustomAnchorIndex));
            GUI.Label(new Rect(84f, y, 18f, 28f), host.GetText(CampusRuntimeEditorTextId.Y), host.BodyStyle);
            localPosition.y = host.ParseFloatField(new Rect(106f, y, 56f, 30f), localPosition.y, host.BuildObjectSettingsInputKey(placed, "anchor_y_" + host.ObjectSettingsSession.SelectedCustomAnchorIndex));
            GUI.Label(new Rect(168f, y, 18f, 28f), host.GetText(CampusRuntimeEditorTextId.R), host.BodyStyle);
            anchor.Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(host.ParseFloatField(new Rect(190f, y, 56f, 30f), anchor.Radius, host.BuildObjectSettingsInputKey(placed, "anchor_r_" + host.ObjectSettingsSession.SelectedCustomAnchorIndex)));
            anchor.LocalPosition = localPosition;
            y += 38f;
            host.ObjectSettingsSession.SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
        }

        private static Sprite ResolvePreviewSprite(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            GameObject prefab,
            CampusPlacedObject placed,
            out bool usesDirectionalSprite,
            out int effectiveRotation90)
        {
            usesDirectionalSprite = false;
            effectiveRotation90 = 0;
            if (prefab == null)
            {
                return null;
            }

            if (placed != null)
            {
                return placed.ResolveSpriteForRotation(host.ObjectSettingsSession.PreviewRotation90, out usesDirectionalSprite, out effectiveRotation90);
            }

            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private static Rect DrawPreviewGrid(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            Rect previewRect,
            Vector2Int footprint,
            Sprite sprite,
            CampusPlacedObject placed,
            bool usesDirectionalSprite,
            int effectiveRotation90)
        {
            float cellSize = Mathf.Min((previewRect.width - 12f) / footprint.x, (previewRect.height - 12f) / footprint.y);
            cellSize = Mathf.Max(8f, cellSize);
            float gridWidth = cellSize * footprint.x;
            float gridHeight = cellSize * footprint.y;
            Rect gridRect = new Rect(
                previewRect.x + (previewRect.width - gridWidth) * 0.5f,
                previewRect.y + (previewRect.height - gridHeight) * 0.5f,
                gridWidth,
                gridHeight);
            DrawPreviewGridCells(host, gridRect, footprint, cellSize);

            if (sprite != null)
            {
                DrawPreviewSprite(host, gridRect, cellSize, sprite, placed, usesDirectionalSprite, effectiveRotation90);
            }
            else
            {
                GUI.DrawTexture(new Rect(gridRect.x + 4f, gridRect.y + 4f, gridRect.width - 8f, gridRect.height - 8f), host.TileFallbackTexture, ScaleMode.ScaleToFit);
            }

            if (placed != null && placed.UseCustomInteractionAnchor)
            {
                DrawPreviewAnchorMarker(host, gridRect, footprint, host.ObjectSettingsSession.GetSelectedCustomAnchor(placed));
            }

            return gridRect;
        }

        private static void DrawPreviewGridCells(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            Rect gridRect,
            Vector2Int footprint,
            float cellSize)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.09f);
            GUI.DrawTexture(gridRect, host.LineTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            for (int x = 0; x <= footprint.x; x++)
            {
                GUI.DrawTexture(new Rect(gridRect.x + x * cellSize, gridRect.y, 1f, gridRect.height), host.LineTexture);
            }

            for (int row = 0; row <= footprint.y; row++)
            {
                GUI.DrawTexture(new Rect(gridRect.x, gridRect.y + row * cellSize, gridRect.width, 1f), host.LineTexture);
            }

            GUI.color = oldColor;
        }

        private static void DrawPreviewSprite(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            Rect gridRect,
            float cellSize,
            Sprite sprite,
            CampusPlacedObject placed,
            bool usesDirectionalSprite,
            int effectiveRotation90)
        {
            if (sprite == null)
            {
                return;
            }

            Vector2 spriteWorldSize = sprite.bounds.size;
            Vector2 visualScale = placed != null ? placed.NormalizedVisualScale : Vector2.one;
            float pixelWidth = Mathf.Max(8f, spriteWorldSize.x * visualScale.x * cellSize);
            float pixelHeight = Mathf.Max(8f, spriteWorldSize.y * visualScale.y * cellSize);
            Rect spriteRect = new Rect(
                gridRect.center.x - pixelWidth * 0.5f,
                gridRect.center.y - pixelHeight * 0.5f,
                pixelWidth,
                pixelHeight);

            Matrix4x4 oldMatrix = GUI.matrix;
            if (!usesDirectionalSprite &&
                (placed == null || !placed.SuppressFlatSpriteRotation) &&
                !Mathf.Approximately(effectiveRotation90 * 90f, 0f))
            {
                GUIUtility.RotateAroundPivot(-effectiveRotation90 * 90f, spriteRect.center);
            }

            host.DrawSprite(spriteRect, sprite);
            GUI.matrix = oldMatrix;
        }

        private static void DrawPreviewAnchorMarker(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            Rect gridRect,
            Vector2Int footprint,
            CampusPlacedObjectInteractionAnchor anchor)
        {
            if (anchor == null)
            {
                return;
            }

            Vector2 previewPoint = ConvertAnchorLocalPositionToPreviewPoint(gridRect, footprint, anchor.LocalPosition);
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.78f, 0.22f, 1f);
            GUI.DrawTexture(new Rect(previewPoint.x - 2f, previewPoint.y - 6f, 4f, 12f), host.LineTexture);
            GUI.DrawTexture(new Rect(previewPoint.x - 6f, previewPoint.y - 2f, 12f, 4f), host.LineTexture);
            GUI.color = oldColor;
        }

        private static void HandlePreviewAnchorInput(
            ICampusRuntimeMapEditorObjectSettingsInspectorHost host,
            Rect previewRect,
            Rect gridRect,
            Vector2Int footprint,
            CampusPlacedObject placed)
        {
            if (placed == null || !placed.UseCustomInteractionAnchor)
            {
                return;
            }

            CampusPlacedObjectInteractionAnchor anchor = host.ObjectSettingsSession.GetSelectedCustomAnchor(placed);
            if (anchor == null)
            {
                return;
            }

            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (!previewRect.Contains(current.mousePosition) || !gridRect.Contains(current.mousePosition))
            {
                return;
            }

            anchor.LocalPosition = ConvertPreviewPointToAnchorLocalPosition(gridRect, footprint, current.mousePosition);
            host.ObjectSettingsSession.SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
            GUI.changed = true;
            current.Use();
        }

        private static Vector2 ConvertAnchorLocalPositionToPreviewPoint(Rect gridRect, Vector2Int footprint, Vector3 localPosition)
        {
            float normalizedX = Mathf.InverseLerp(-footprint.x * 0.5f, footprint.x * 0.5f, localPosition.x);
            float normalizedY = Mathf.InverseLerp(-footprint.y * 0.5f, footprint.y * 0.5f, localPosition.y);
            return new Vector2(
                Mathf.Lerp(gridRect.xMin, gridRect.xMax, normalizedX),
                Mathf.Lerp(gridRect.yMax, gridRect.yMin, normalizedY));
        }

        private static Vector3 ConvertPreviewPointToAnchorLocalPosition(Rect gridRect, Vector2Int footprint, Vector2 previewPoint)
        {
            float normalizedX = Mathf.InverseLerp(gridRect.xMin, gridRect.xMax, previewPoint.x);
            float normalizedY = Mathf.InverseLerp(gridRect.yMax, gridRect.yMin, previewPoint.y);
            return new Vector3(
                Mathf.Lerp(-footprint.x * 0.5f, footprint.x * 0.5f, normalizedX),
                Mathf.Lerp(-footprint.y * 0.5f, footprint.y * 0.5f, normalizedY),
                0f);
        }
    }
}

