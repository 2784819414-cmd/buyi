using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace NtingCampusMapEditor
{
    public enum CampusBrushMode
    {
        PaintFloorTile,
        PaintWallTile,
        PlacePrefab,
        PlaceStair,
        PlaceLight,
        Erase,
        Pick,
        RectangleFillFloor,
        RectangleErase,
        Pan,
        RectangleWall
    }

    public enum CampusMapEditorLanguage
    {
        English,
        ChineseSimplified
    }

    public enum CampusFloorTileSize
    {
        Small = 1,
        Medium = 2,
        Large = 3
    }

    /// <summary>
    /// Main editor window for authoring 2D campus floors inside the Unity Editor.
    /// </summary>
    public sealed class CampusMapEditorWindow : EditorWindow
    {
        private const string LanguageEditorPrefsKey = "NtingCampusMapEditor.Language";
        private static readonly int[] RotationValues = { 0, 1, 2, 3 };
        private static readonly string[] RotationLabels = { "0", "90", "180", "270" };
        private static readonly string[] LanguageLabels = { "English", "简体中文" };

        [SerializeField] private CampusMapRoot mapRoot;
        [SerializeField] private CampusMapData mapData;
        [SerializeField] private CampusTilePalette floorTilePalette;
        [SerializeField] private CampusWallPalette wallPalette;
        [SerializeField] private CampusWallRenderProfile wallRenderProfile;
        [SerializeField] private Texture2D selectedWallFaceTexture;
        [SerializeField] private Texture2D selectedWallCapTexture;
        [SerializeField] private string newWallTypeName = "Custom Wall";
        [SerializeField] private CampusPrefabPalette prefabPalette;
        [SerializeField] private TileBase currentFloorTile;
        [SerializeField] private TileBase currentWallTile;
        [SerializeField] private GameObject currentPrefab;
        [SerializeField] private GameObject stairPrefab;
        [SerializeField] private CampusBrushMode brushMode = CampusBrushMode.PaintFloorTile;
        [SerializeField] private int currentFloorIndex = 1;
        [SerializeField] private int brushSize = 1;
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private int rotation90;
        [SerializeField] private bool flipX;
        [SerializeField] private bool flipY;
        [SerializeField] private bool enableEditing = true;
        [SerializeField] private CampusWallDebugView wallDebugView = CampusWallDebugView.ShowFinalWallVisuals;
        [SerializeField] private CampusMapEditorLanguage language = CampusMapEditorLanguage.ChineseSimplified;
        [SerializeField] private Light2D selectedLight;
        [SerializeField] private string newLightName = "新光源";
        [SerializeField] private Light2D.LightType newLightType = Light2D.LightType.Point;

        private CampusMapEditorUtility.DebugAssetSet debugAssets;
        private Vector2 scroll;
        private bool temporaryPanOverride;

        public CampusMapRoot MapRoot => mapRoot;
        public CampusBrushMode BrushMode => brushMode;
        public CampusBrushMode ActiveBrushMode => temporaryPanOverride ? CampusBrushMode.Pan : brushMode;
        public int CurrentFloorIndex => currentFloorIndex;
        public int BrushSize => Mathf.Max(1, brushSize);
        public int FloorTileSizeCells => 1;
        public bool SnapToGrid => snapToGrid;
        public int Rotation90 => Mathf.Clamp(rotation90, 0, 3);
        public bool FlipX => flipX;
        public bool FlipY => flipY;
        public bool EnableEditing => enableEditing;
        public CampusWallRenderProfile WallRenderProfileOrFallback => GetWallRenderProfileOrFallback();
        public CampusWallDebugView WallDebugView => wallDebugView;
        public CampusMapEditorLanguage Language => language;
        public bool IsTemporaryPanActive => temporaryPanOverride;
        public string LightBrushName => string.IsNullOrWhiteSpace(newLightName) ? Text("New Light", "新光源") : newLightName.Trim();
        public Light2D.LightType LightBrushType => newLightType;

        public bool EnsureMapRootForEditing()
        {
            if (mapRoot == null)
            {
                mapRoot = CampusMapEditorUtility.FindCampusMapRoot();
            }

            if (mapRoot == null)
            {
                mapRoot = CampusMapEditorUtility.FindOrCreateCampusMapRoot();
            }

            if (mapRoot == null)
            {
                return false;
            }

            debugAssets = CampusMapEditorUtility.LoadDebugAssets();
            LoadGeneratedPalettesIfAvailable();
            ApplyExistingFallbacksOnly(false);
            CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
            ClampCurrentFloorIndex();
            Repaint();
            SceneView.RepaintAll();
            return true;
        }

        public CampusFloorRoot EnsureCurrentFloorForEditing()
        {
            if (!EnsureMapRootForEditing())
            {
                return null;
            }

            CampusFloorRoot floor = CampusMapEditorUtility.GetOrCreateFloor(mapRoot, currentFloorIndex, true);
            if (floor != null)
            {
                CampusMapEditorUtility.EnsureFloorStructure(mapRoot, floor, false);
                mapRoot.RebuildFloorReferences();
            }

            return floor;
        }

        private void OnEnable()
        {
            language = (CampusMapEditorLanguage)EditorPrefs.GetInt(LanguageEditorPrefsKey, (int)language);
            debugAssets = CampusMapEditorUtility.LoadDebugAssets();
            if (mapRoot == null)
            {
                mapRoot = CampusMapEditorUtility.FindCampusMapRoot();
            }

            LoadGeneratedPalettesIfAvailable();
            ApplyExistingFallbacksOnly(false);
            CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawLanguageSection();
            DrawMapRootSection();
            DrawFloorSection();
            DrawLightingSection();
            DrawWallVisualSection();
            DrawBrushModeSection();
            DrawPaletteSection();
            DrawBrushSettingsSection();
            DrawCurrentPreviewSection();
            DrawValidationSection();
            EditorGUILayout.EndScrollView();
        }

        public CampusFloorRoot GetCurrentFloor()
        {
            if (mapRoot == null)
            {
                return null;
            }

            mapRoot.RebuildFloorReferences();
            CampusFloorRoot floor = mapRoot.GetFloor(currentFloorIndex);
            CampusMapEditorUtility.EnsureFloorGridAlignedToSceneGrid(floor);
            return floor;
        }

        public TileBase GetCurrentFloorTileOrFallback()
        {
            return CampusTilePalette.IsUsableTile(currentFloorTile) ? currentFloorTile : debugAssets.FloorTile;
        }

        public TileBase GetCurrentWallTileOrFallback()
        {
            if (CampusTilePalette.IsUsableTile(currentWallTile))
            {
                return currentWallTile;
            }

            CampusWallRenderProfile profile = GetWallRenderProfileOrFallback();
            if (profile != null && CampusTilePalette.IsUsableTile(profile.GetLogicTile()))
            {
                return profile.GetLogicTile();
            }

            TileBase paletteWall = GetDefaultWallTile();
            if (paletteWall != null)
            {
                return paletteWall;
            }

            return debugAssets.HighWallTile != null ? debugAssets.HighWallTile : debugAssets.WallTile;
        }

        public TileBase EnsureWallTileForPainting()
        {
            TileBase tile = GetCurrentWallTileOrFallback();
            if (CampusTilePalette.IsUsableTile(tile))
            {
                currentWallTile = tile;
                return tile;
            }

            debugAssets = CampusMapEditorUtility.EnsureDebugAssets();
            LoadGeneratedPalettesIfAvailable();

            CampusWallRenderProfile profile = GetWallRenderProfileOrFallback();
            if (profile == null || !CampusTilePalette.IsUsableTile(profile.GetLogicTile()))
            {
                profile = CampusMapEditorUtility.EnsureWallRenderProfiles(debugAssets);
                wallRenderProfile = profile;
                SyncWallTextureSelectionsFromProfile(false);
            }

            tile = profile != null ? profile.GetLogicTile() : null;
            if (!CampusTilePalette.IsUsableTile(tile))
            {
                tile = GetDefaultWallTile();
            }

            if (!CampusTilePalette.IsUsableTile(tile))
            {
                tile = debugAssets.HighWallTile != null ? debugAssets.HighWallTile : debugAssets.WallTile;
            }

            if (CampusTilePalette.IsUsableTile(tile))
            {
                currentWallTile = tile;
                wallRenderProfile = CampusMapEditorUtility.GetWallRenderProfileForLogicTile(currentWallTile, GetWallRenderProfileOrFallback());
                SyncWallTextureSelectionsFromProfile(false);
                Repaint();
                SceneView.RepaintAll();
                return tile;
            }

            return null;
        }

        public GameObject GetCurrentPrefabOrFallback()
        {
            return currentPrefab != null ? currentPrefab : debugAssets.PropBoxPrefab;
        }

        public GameObject GetStairPrefabOrFallback()
        {
            return stairPrefab != null ? stairPrefab : debugAssets.StairPrefab;
        }

        public void SetCurrentFloorTile(TileBase tile)
        {
            currentFloorTile = tile;
            brushMode = CampusBrushMode.PaintFloorTile;
            Repaint();
        }

        public void SetFloorTileSizeCells(int size)
        {
            Repaint();
        }

        public void SetCurrentWallTile(TileBase tile)
        {
            currentWallTile = tile;
            wallRenderProfile = CampusMapEditorUtility.GetWallRenderProfileForLogicTile(currentWallTile, GetWallRenderProfileOrFallback());
            SyncWallTextureSelectionsFromProfile(true);
            brushMode = CampusBrushMode.PaintWallTile;
            Repaint();
        }

        public void SetCurrentPrefab(GameObject prefab)
        {
            currentPrefab = prefab;
            brushMode = CampusBrushMode.PlacePrefab;
            Repaint();
        }

        public void SelectLightBrush(string requestedName, Light2D.LightType lightType)
        {
            if (!string.IsNullOrWhiteSpace(requestedName))
            {
                newLightName = requestedName.Trim();
            }

            newLightType = lightType;
            brushMode = CampusBrushMode.PlaceLight;
            Repaint();
            SceneView.RepaintAll();
        }

        public void SetSelectedLight(Light2D light)
        {
            selectedLight = light;
            if (light != null)
            {
                Selection.activeGameObject = light.gameObject;
            }

            Repaint();
        }

        public void ClearBrushSelection()
        {
            currentFloorTile = null;
            currentWallTile = null;
            currentPrefab = null;
            Repaint();
            SceneView.RepaintAll();
        }

        public void RefreshAfterSceneEdit()
        {
            if (mapRoot != null)
            {
                mapRoot.RebuildFloorReferences();
                CampusFloorRoot floor = mapRoot.GetFloor(currentFloorIndex);
                if (floor != null)
                {
                    floor.RefreshUsedBounds();
                }

                EditorUtility.SetDirty(mapRoot);
            }

            Repaint();
            SceneView.RepaintAll();
        }

        public void EnsureNotPreviewingBeforeEdit()
        {
            CampusMapEditorUtility.EnsureNotPreviewingBeforeEdit(mapRoot);
        }

        public void SetTemporaryPanOverride(bool active)
        {
            if (temporaryPanOverride == active)
            {
                return;
            }

            temporaryPanOverride = active;
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            CampusMapEditorSceneTools.HandleSceneGUI(this, sceneView);
        }

        private void DrawLanguageSection()
        {
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);
            EditorGUI.BeginChangeCheck();
            language = (CampusMapEditorLanguage)EditorGUILayout.Popup(Text("Language", "语言"), (int)language, LanguageLabels);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt(LanguageEditorPrefsKey, (int)language);
                Repaint();
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndVertical();
        }

        public string Text(string english, string chinese)
        {
            return language == CampusMapEditorLanguage.ChineseSimplified ? chinese : english;
        }

        public string BrushModeLabel(CampusBrushMode mode)
        {
            string[] labels = BrushModeLabels();
            int index = Mathf.Clamp((int)mode, 0, labels.Length - 1);
            return labels[index];
        }

        private string[] BrushModeLabels()
        {
            return new[]
            {
                Text("Paint Floor Tile", "绘制地面"),
                Text("Paint Wall Tile", "绘制墙体"),
                Text("Place Object", "放置物体"),
                Text("Place Stair", "放置楼梯"),
                Text("Place Light", "放置光源"),
                Text("Erase", "擦除"),
                Text("Pick", "拾取"),
                Text("Rectangle Floor", "矩形铺地"),
                Text("Rectangle Erase", "矩形删除"),
                Text("Pan View", "拖拽平移"),
                Text("Rectangle Wall", "矩形墙体")
            };
        }

        private string[] WallDebugViewLabels()
        {
            return new[]
            {
                Text("Show Final Wall Visuals", "显示最终墙体"),
                Text("Show Wall Logic Only", "只显示墙逻辑"),
                Text("Show Both", "两者都显示")
            };
        }

        public string FloorTileSizeLabel()
        {
            return "1x1";
        }

        private void DrawMapRootSection()
        {
            EditorGUILayout.LabelField(Text("Map Root", "地图根节点"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);
            mapRoot = (CampusMapRoot)EditorGUILayout.ObjectField(Text("Campus Map Root", "Campus Map 根节点"), mapRoot, typeof(CampusMapRoot), true);
            enableEditing = EditorGUILayout.Toggle(Text("Enable Editing", "启用编辑"), enableEditing);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Create / Find CampusMapRoot", "创建 / 查找 CampusMapRoot")))
            {
                EnsureNotPreviewingBeforeEdit();
                mapRoot = CampusMapEditorUtility.FindOrCreateCampusMapRoot();
                currentFloorIndex = 1;
                ApplyDebugFallbacks(false);
                Selection.activeGameObject = mapRoot != null ? mapRoot.gameObject : null;
            }

            if (GUILayout.Button(Text("Rebuild Floor References", "重建楼层引用")))
            {
                EnsureNotPreviewingBeforeEdit();
                CampusMapEditorUtility.RebuildFloorReferences(mapRoot);
                if (mapRoot != null)
                {
                    mapRoot.CaptureFloorOriginalStates(true);
                    CampusMapEditorUtility.RebuildAllWallVisuals(mapRoot, GetWallRenderProfileOrFallback());
                    CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
                }

                ClampCurrentFloorIndex();
            }

            EditorGUILayout.EndHorizontal();

            mapData = (CampusMapData)EditorGUILayout.ObjectField(Text("Map Data", "地图数据"), mapData, typeof(CampusMapData), false);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Save Map Data", "保存地图数据")))
            {
                mapData = CampusMapEditorUtility.SaveMapData(mapRoot, mapData);
            }

            if (GUILayout.Button(Text("Load Map Data", "读取地图数据")))
            {
                EnsureNotPreviewingBeforeEdit();
                if (mapData == null)
                {
                    mapData = AssetDatabase.LoadAssetAtPath<CampusMapData>(CampusMapEditorUtility.DefaultMapDataPath);
                }

                CampusMapEditorUtility.LoadMapData(mapData, mapRoot);
                CampusMapEditorUtility.RebuildAllWallVisuals(mapRoot, GetWallRenderProfileOrFallback());
                CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
                ClampCurrentFloorIndex();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawFloorSection()
        {
            EditorGUILayout.LabelField(Text("Floor", "楼层"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);
            if (mapRoot == null)
            {
                EditorGUILayout.HelpBox(Text("Create or assign a CampusMapRoot first.", "请先创建或指定 CampusMapRoot。"), MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            mapRoot.RebuildFloorReferences();
            ClampCurrentFloorIndex();

            string[] floorLabels = BuildFloorLabelsForCurrentFloor(mapRoot.Floors, out int selectedIndex);
            if (floorLabels.Length > 0)
            {
                int nextSelected = EditorGUILayout.Popup(Text("Current Edit Floor", "当前编辑楼层"), selectedIndex, floorLabels);
                if (nextSelected >= 0 && nextSelected < mapRoot.Floors.Count && mapRoot.Floors[nextSelected] != null)
                {
                    int nextFloorIndex = mapRoot.Floors[nextSelected].FloorIndex;
                    if (nextFloorIndex != currentFloorIndex)
                    {
                        currentFloorIndex = nextFloorIndex;
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField(Text("Current Edit Floor", "当前编辑楼层"), Text("Floor 1", "楼层 1"));
                if (currentFloorIndex != 1)
                {
                    currentFloorIndex = 1;
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Add Floor", "新增楼层")))
            {
                EnsureNotPreviewingBeforeEdit();
                int nextFloor = Mathf.Max(1, mapRoot.GetHighestFloorIndex() + 1);
                CampusMapEditorUtility.GetOrCreateFloor(mapRoot, nextFloor, true);
                currentFloorIndex = nextFloor;
            }

            if (GUILayout.Button(Text("Unlock Next Floor", "解锁下一层")))
            {
                EnsureNotPreviewingBeforeEdit();
                int nextFloor = currentFloorIndex + 1;
                CampusFloorRoot floor = CampusMapEditorUtility.GetOrCreateFloor(mapRoot, nextFloor, true);
                if (floor != null)
                {
                    Undo.RecordObject(floor, "Unlock Campus Floor");
                    floor.IsUnlocked = true;
                    EditorUtility.SetDirty(floor);
                    currentFloorIndex = nextFloor;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Preview As Player On This Floor", "以玩家视角预览本层")))
            {
                CampusFloorVisibilityController controller = EnsureVisibilityController();
                if (controller != null)
                {
                    controller.PreviewFloor(currentFloorIndex);
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                }
            }

            if (GUILayout.Button(Text("Reset Floor Visibility", "重置楼层可见性")))
            {
                CampusFloorVisibilityController controller = EnsureVisibilityController();
                if (controller != null)
                {
                    controller.ResetVisibility();
                    EditorUtility.SetDirty(controller);
                }
            }

            EditorGUILayout.EndHorizontal();

            CampusFloorRoot currentFloor = GetCurrentFloor();
            if (currentFloor != null)
            {
                EditorGUILayout.LabelField(Text("Unlocked", "已解锁"), currentFloor.IsUnlocked ? Text("Yes", "是") : Text("No", "否"));
                EditorGUILayout.LabelField(Text("Used Bounds", "已用边界"), currentFloor.UsedBounds.ToString());
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLightingSection()
        {
            EditorGUILayout.LabelField(Text("Lighting", "光源"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Fit Selected To Map", "适配地图范围")))
            {
                CampusMapEditorUtility.FitLight2DToMap(mapRoot, selectedLight);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            Light2D[] sceneLights = CampusMapEditorUtility.FindSceneLights2D();
            if (sceneLights.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < sceneLights.Length; i++)
                {
                    Light2D light = sceneLights[i];
                    if (light == null)
                    {
                        continue;
                    }

                    if (GUILayout.Button(light.gameObject.name, CampusMapEditorStyles.MiniPreview))
                    {
                        selectedLight = light;
                        Selection.activeGameObject = light.gameObject;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            selectedLight = (Light2D)EditorGUILayout.ObjectField(Text("Selected Light", "当前光源"), selectedLight, typeof(Light2D), true);
            EditorGUILayout.Space(3f);

            newLightName = EditorGUILayout.TextField(Text("New Light Name", "新光源名称"), newLightName);
            newLightType = (Light2D.LightType)EditorGUILayout.EnumPopup(Text("New Light Type", "新光源类型"), newLightType);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Use Light Brush", "使用光源刷子")))
            {
                SelectLightBrush(newLightName, newLightType);
            }

            if (GUILayout.Button(Text("Global Brush", "全局光刷子")))
            {
                SelectLightBrush(CampusObjectNames.GlobalLight2D, Light2D.LightType.Global);
            }

            if (GUILayout.Button(Text("Point Brush", "点光刷子")))
            {
                SelectLightBrush(CampusObjectNames.SunLight2D, Light2D.LightType.Point);
            }

            EditorGUILayout.EndHorizontal();

            if (selectedLight == null)
            {
                EditorGUILayout.HelpBox(Text("Select an existing Light2D or choose a light brush, then click in Scene view to place it.", "请选择现有 Light2D，或选择光源刷子后在 Scene 视图点击放置。"), MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawSelectedLightEditor(selectedLight);
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedLightEditor(Light2D light)
        {
            if (light == null)
            {
                return;
            }

            Transform lightTransform = light.transform;
            EditorGUI.BeginChangeCheck();
            string objectName = EditorGUILayout.TextField(Text("Name", "名称"), light.gameObject.name);
            Light2D.LightType lightType = (Light2D.LightType)EditorGUILayout.EnumPopup(Text("Type", "类型"), light.lightType);
            Color color = EditorGUILayout.ColorField(Text("Color", "颜色"), light.color);
            float intensity = EditorGUILayout.Slider(Text("Intensity", "强度"), light.intensity, 0f, 8f);
            int blendStyle = Mathf.Max(0, EditorGUILayout.IntField(Text("Blend Style", "混合样式"), light.blendStyleIndex));
            float falloff = EditorGUILayout.Slider(Text("Falloff", "衰减"), light.falloffIntensity, 0f, 1f);
            bool shadowsEnabled = EditorGUILayout.Toggle(Text("Enable Shadows", "启用阴影"), light.shadowsEnabled);
            float shadowIntensity = light.shadowIntensity;
            float shadowSoftness = light.shadowSoftness;
            float shadowSoftnessFalloff = light.shadowSoftnessFalloffIntensity;
            using (new EditorGUI.DisabledScope(!shadowsEnabled || lightType == Light2D.LightType.Global))
            {
                shadowIntensity = EditorGUILayout.Slider(Text("Shadow Intensity", "阴影强度"), shadowIntensity, 0f, 1f);
                shadowSoftness = EditorGUILayout.Slider(Text("Shadow Softness", "阴影柔和度"), shadowSoftness, 0f, 1f);
                shadowSoftnessFalloff = EditorGUILayout.Slider(Text("Shadow Falloff", "阴影柔和衰减"), shadowSoftnessFalloff, 0f, 1f);
            }
            Vector3 position = EditorGUILayout.Vector3Field(Text("Position", "位置"), lightTransform.position);
            float rotationZ = EditorGUILayout.FloatField(Text("Rotation Z", "Z 旋转"), lightTransform.eulerAngles.z);

            float innerRadius = light.pointLightInnerRadius;
            float outerRadius = light.pointLightOuterRadius;
            float innerAngle = light.pointLightInnerAngle;
            float outerAngle = light.pointLightOuterAngle;
            if (lightType == Light2D.LightType.Point)
            {
                innerRadius = Mathf.Max(0f, EditorGUILayout.FloatField(Text("Inner Radius", "内半径"), innerRadius));
                outerRadius = Mathf.Max(innerRadius, EditorGUILayout.FloatField(Text("Outer Radius", "外半径"), outerRadius));
                innerAngle = Mathf.Clamp(EditorGUILayout.FloatField(Text("Inner Angle", "内角度"), innerAngle), 0f, 360f);
                outerAngle = Mathf.Clamp(EditorGUILayout.FloatField(Text("Outer Angle", "外角度"), outerAngle), innerAngle, 360f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new Object[] { light, light.gameObject, lightTransform }, "Edit Campus Light 2D");
                light.gameObject.name = string.IsNullOrWhiteSpace(objectName) ? light.gameObject.name : objectName.Trim();
                light.lightType = lightType;
                light.color = color;
                light.intensity = intensity;
                light.blendStyleIndex = blendStyle;
                light.falloffIntensity = falloff;
                CampusDynamicShadowUtility.ConfigureLightShadows(light, light.lightType != Light2D.LightType.Global && shadowsEnabled, shadowIntensity, shadowSoftness, shadowSoftnessFalloff);
                light.targetSortingLayers = light.targetSortingLayers == null || light.targetSortingLayers.Length == 0
                    ? new[] { SortingLayer.NameToID("Default") }
                    : light.targetSortingLayers;
                lightTransform.position = position;
                lightTransform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
                if (light.lightType == Light2D.LightType.Point)
                {
                    light.pointLightInnerRadius = innerRadius;
                    light.pointLightOuterRadius = outerRadius;
                    light.pointLightInnerAngle = innerAngle;
                    light.pointLightOuterAngle = outerAngle;
                }

                EditorUtility.SetDirty(light);
                EditorUtility.SetDirty(light.gameObject);
                EditorUtility.SetDirty(lightTransform);
                CampusMapEditorUtility.MarkSceneDirty();
                SceneView.RepaintAll();
            }

            SerializedObject serializedLight = new SerializedObject(light);
            SerializedProperty normalMapQuality = serializedLight.FindProperty("m_NormalMapQuality");
            SerializedProperty normalMapDistance = serializedLight.FindProperty("m_NormalMapDistance");
            if (normalMapQuality != null || normalMapDistance != null)
            {
                EditorGUILayout.Space(3f);
                EditorGUI.BeginChangeCheck();
                if (normalMapQuality != null)
                {
                    EditorGUILayout.PropertyField(normalMapQuality, new GUIContent(Text("Normal Map Quality", "法线贴图质量")));
                }

                if (normalMapDistance != null)
                {
                    EditorGUILayout.PropertyField(normalMapDistance, new GUIContent(Text("Normal Map Distance", "法线贴图距离")));
                }

                if (EditorGUI.EndChangeCheck())
                {
                    serializedLight.ApplyModifiedProperties();
                    CampusMapEditorUtility.MarkSceneDirty();
                    SceneView.RepaintAll();
                }
                else
                {
                    serializedLight.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Apply All Sorting Layers", "应用全部排序层")))
            {
                Undo.RecordObject(light, "Apply Campus Light Sorting Layers");
                light.targetSortingLayers = SortingLayer.layers != null && SortingLayer.layers.Length > 0
                    ? System.Array.ConvertAll(SortingLayer.layers, layer => layer.id)
                    : new[] { SortingLayer.NameToID("Default") };
                EditorUtility.SetDirty(light);
                CampusMapEditorUtility.MarkSceneDirty();
            }

            if (GUILayout.Button(Text("Select In Hierarchy", "在层级中选中")))
            {
                Selection.activeGameObject = light.gameObject;
            }

            if (GUILayout.Button(Text("Delete Light", "删除光源")))
            {
                GameObject lightObject = light.gameObject;
                selectedLight = null;
                Undo.DestroyObjectImmediate(lightObject);
                CampusMapEditorUtility.MarkSceneDirty();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWallVisualSection()
        {
            EditorGUILayout.LabelField(Text("Wall Visuals", "墙体视觉"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);
            EditorGUI.BeginChangeCheck();
            wallRenderProfile = (CampusWallRenderProfile)EditorGUILayout.ObjectField(Text("Wall Render Profile", "墙体渲染配置"), wallRenderProfile, typeof(CampusWallRenderProfile), false);
            if (EditorGUI.EndChangeCheck())
            {
                SyncWallTextureSelectionsFromProfile(true);
                if (wallRenderProfile != null && CampusTilePalette.IsUsableTile(wallRenderProfile.GetLogicTile()))
                {
                    currentWallTile = wallRenderProfile.GetLogicTile();
                    brushMode = CampusBrushMode.PaintWallTile;
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Default Prototype", "默认原型")))
            {
                wallRenderProfile = CampusMapEditorUtility.LoadDefaultWallRenderProfile();
                SyncWallTextureSelectionsFromProfile(true);
                currentWallTile = wallRenderProfile != null ? wallRenderProfile.GetLogicTile() : GetDefaultWallTile();
                brushMode = CampusBrushMode.PaintWallTile;
            }

            if (GUILayout.Button(Text("Brick Prototype", "砖墙原型")))
            {
                wallRenderProfile = CampusMapEditorUtility.LoadBrickWallRenderProfile();
                SyncWallTextureSelectionsFromProfile(true);
                currentWallTile = wallRenderProfile != null ? wallRenderProfile.GetLogicTile() : GetDefaultWallTile();
                brushMode = CampusBrushMode.PaintWallTile;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            selectedWallFaceTexture = (Texture2D)EditorGUILayout.ObjectField(Text("Wall Face Texture", "墙面贴图"), selectedWallFaceTexture, typeof(Texture2D), false);
            selectedWallCapTexture = (Texture2D)EditorGUILayout.ObjectField(Text("Wall Top Texture", "墙顶贴图"), selectedWallCapTexture, typeof(Texture2D), false);
            newWallTypeName = EditorGUILayout.TextField(Text("New Wall Name", "新墙体名称"), newWallTypeName);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Apply Wall Textures", "应用墙体贴图")))
            {
                EnsureNotPreviewingBeforeEdit();
                TileBase logicTile = GetCurrentWallTileOrFallback();
                CampusWallRenderProfile profile = CampusMapEditorUtility.ApplyWallTextureSelection(logicTile, GetWallRenderProfileOrFallback(), selectedWallFaceTexture, selectedWallCapTexture);
                if (profile != null)
                {
                    wallRenderProfile = profile;
                    currentWallTile = logicTile;
                    brushMode = CampusBrushMode.PaintWallTile;
                    CampusMapEditorUtility.RebuildAllWallVisuals(mapRoot, profile);
                    CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
                }
            }

            if (GUILayout.Button(Text("Create Wall Type", "新建墙体")))
            {
                EnsureNotPreviewingBeforeEdit();
                CampusWallRenderProfile profile = CampusMapEditorUtility.CreateWallTextureProfile(newWallTypeName, selectedWallFaceTexture, selectedWallCapTexture);
                if (profile != null)
                {
                    wallRenderProfile = profile;
                    currentWallTile = profile.GetLogicTile();
                    brushMode = CampusBrushMode.PaintWallTile;
                    LoadGeneratedPalettesIfAvailable();
                    SyncWallTextureSelectionsFromProfile(true);
                    CampusMapEditorUtility.RebuildAllWallVisuals(mapRoot, profile);
                    CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
                }
            }

            if (GUILayout.Button(Text("Refresh Assets/瓦片", "刷新 Assets/瓦片")))
            {
                debugAssets = CampusMapEditorUtility.EnsureDebugAssets();
                LoadGeneratedPalettesIfAvailable();
                SyncWallTextureSelectionsFromProfile(true);
                ApplyExistingFallbacksOnly(false);
            }

            EditorGUILayout.EndHorizontal();

            CampusWallDebugView nextDebugView = (CampusWallDebugView)EditorGUILayout.Popup(
                Text("Wall Debug View", "墙体调试显示"),
                (int)wallDebugView,
                WallDebugViewLabels());
            if (nextDebugView != wallDebugView)
            {
                wallDebugView = nextDebugView;
                CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Rebuild Wall Visuals", "重建当前楼层墙视觉")))
            {
                EnsureNotPreviewingBeforeEdit();
                CampusFloorRoot floor = GetCurrentFloor();
                CampusMapEditorUtility.RebuildWallVisuals(floor, GetWallRenderProfileOrFallback());
                if (floor != null)
                {
                    CampusWallAutoRenderer.ApplyDebugView(floor, wallDebugView);
                }
            }

            if (GUILayout.Button(Text("Rebuild All Wall Visuals", "重建全部墙视觉")))
            {
                EnsureNotPreviewingBeforeEdit();
                CampusMapEditorUtility.RebuildAllWallVisuals(mapRoot, GetWallRenderProfileOrFallback());
                CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
            }

            EditorGUILayout.EndHorizontal();
            if (GetWallRenderProfileOrFallback() == null)
            {
                EditorGUILayout.HelpBox(Text("No wall render profile is available. Generate Debug Assets to create prototype cutaway wall resources.", "没有可用墙体渲染配置。请生成调试资源。"), MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBrushModeSection()
        {
            EditorGUILayout.LabelField(Text("Brush Mode", "工具模式"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);
            brushMode = (CampusBrushMode)GUILayout.SelectionGrid((int)brushMode, BrushModeLabels(), 2);
            EditorGUILayout.EndVertical();
        }

        private void DrawPaletteSection()
        {
            EditorGUILayout.LabelField(Text("Palette", "资源面板"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);

            floorTilePalette = (CampusTilePalette)EditorGUILayout.ObjectField(Text("Floor Tile Palette", "地面 Tile 面板"), floorTilePalette, typeof(CampusTilePalette), false);
            floorTilePalette?.RemoveInvalidEntries();
            DrawTileButtons(floorTilePalette != null ? floorTilePalette.FloorTiles : null, SetCurrentFloorTile);
            EditorGUI.BeginChangeCheck();
            TileBase selectedFloorTile = (TileBase)EditorGUILayout.ObjectField(Text("Current Tile", "当前地面 Tile"), currentFloorTile, typeof(TileBase), false);
            if (EditorGUI.EndChangeCheck())
            {
                currentFloorTile = selectedFloorTile;
                if (currentFloorTile != null)
                {
                    brushMode = CampusBrushMode.PaintFloorTile;
                    SceneView.RepaintAll();
                }
            }

            if (GetCurrentFloorTileOrFallback() == null)
            {
                EditorGUILayout.HelpBox(Text("No usable floor tile is selected and Debug_FloorTile is missing. Generate Debug Assets before painting.", "未选择可用地面 Tile，且缺少 Debug_FloorTile。请先生成调试资源。"), MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
            wallPalette = (CampusWallPalette)EditorGUILayout.ObjectField(Text("Wall Palette", "墙体面板"), wallPalette, typeof(CampusWallPalette), false);
            wallPalette?.RemoveInvalidEntries();
            DrawWallButtons();
            EditorGUI.BeginChangeCheck();
            TileBase selectedWallTile = (TileBase)EditorGUILayout.ObjectField(Text("Current Wall Logic Tile", "当前墙逻辑 Tile"), currentWallTile, typeof(TileBase), false);
            if (EditorGUI.EndChangeCheck())
            {
                currentWallTile = selectedWallTile;
                if (currentWallTile != null)
                {
                    wallRenderProfile = CampusMapEditorUtility.GetWallRenderProfileForLogicTile(currentWallTile, GetWallRenderProfileOrFallback());
                    SyncWallTextureSelectionsFromProfile(true);
                    brushMode = CampusBrushMode.PaintWallTile;
                    SceneView.RepaintAll();
                }
            }

            if (GetCurrentWallTileOrFallback() == null)
            {
                EditorGUILayout.HelpBox(Text("No usable wall tile is selected and Debug_WallTile is missing. Generate Debug Assets before painting.", "未选择可用墙 Tile，且缺少 Debug_WallTile。请先生成调试资源。"), MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
            prefabPalette = (CampusPrefabPalette)EditorGUILayout.ObjectField(Text("Object Palette", "物体面板"), prefabPalette, typeof(CampusPrefabPalette), false);
            prefabPalette?.RemoveInvalidEntries();
            DrawPrefabButtons(prefabPalette != null ? prefabPalette.Prefabs : null);
            EditorGUI.BeginChangeCheck();
            GameObject selectedPrefab = (GameObject)EditorGUILayout.ObjectField(Text("Current Object", "当前物体"), currentPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                currentPrefab = selectedPrefab;
                if (currentPrefab != null)
                {
                    brushMode = CampusBrushMode.PlacePrefab;
                    SceneView.RepaintAll();
                }
            }

            EditorGUI.BeginChangeCheck();
            GameObject selectedStairPrefab = (GameObject)EditorGUILayout.ObjectField(Text("Stair Object", "楼梯物体"), stairPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                stairPrefab = selectedStairPrefab;
                if (stairPrefab != null)
                {
                    brushMode = CampusBrushMode.PlaceStair;
                    SceneView.RepaintAll();
                }
            }

            if (GetCurrentPrefabOrFallback() == null)
            {
                EditorGUILayout.HelpBox(Text("No usable object is selected and the test box is missing. Generate Debug Assets before placing objects.", "未选择可用物体，且缺少测试箱。请先生成调试资源。"), MessageType.Warning);
            }

            if (GetStairPrefabOrFallback() == null)
            {
                EditorGUILayout.HelpBox(Text("No stair object is selected and the test stair is missing. Generate Debug Assets before placing stairs.", "未选择楼梯物体，且缺少测试楼梯。请先生成调试资源。"), MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Generate Debug Assets", "生成调试资源")))
            {
                debugAssets = CampusMapEditorUtility.EnsureDebugAssets();
                wallRenderProfile = CampusMapEditorUtility.LoadDefaultWallRenderProfile();
                ApplyDebugFallbacks(false);
            }

            if (GUILayout.Button(Text("Use Debug Set", "使用调试资源")))
            {
                ApplyDebugFallbacks(true);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField(Text("Validation", "校验"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Run Validation", "运行校验")))
            {
                CampusMapEditorUtility.RebuildAllWallVisuals(mapRoot, GetWallRenderProfileOrFallback());
                CampusMapEditorUtility.ApplyWallDebugView(mapRoot, wallDebugView);
                CampusMapEditorUtility.RunValidation(mapRoot, floorTilePalette, wallPalette, prefabPalette);
            }

            if (GUILayout.Button(Text("Fix Validation Issues", "修复校验问题")))
            {
                CampusMapEditorUtility.FixValidationIssues(mapRoot, floorTilePalette, wallPalette, prefabPalette);
                mapRoot = CampusMapEditorUtility.FindCampusMapRoot();
                debugAssets = CampusMapEditorUtility.LoadDebugAssets();
                LoadGeneratedPalettesIfAvailable();
                ApplyExistingFallbacksOnly(false);
                ClampCurrentFloorIndex();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawBrushSettingsSection()
        {
            EditorGUILayout.LabelField(Text("Brush Settings", "工具设置"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);
            EditorGUILayout.LabelField(Text("Floor / Wall Unit", "地板/墙体单位"), FloorTileSizeLabel());
            brushSize = Mathf.Max(1, EditorGUILayout.IntField(Text("Wall / Erase Brush Size", "墙体/擦除笔刷尺寸"), brushSize));
            snapToGrid = EditorGUILayout.Toggle(Text("Snap To Grid", "吸附网格"), snapToGrid);
            rotation90 = EditorGUILayout.IntPopup(Text("Rotate", "旋转"), Rotation90, RotationLabels, RotationValues);
            flipX = EditorGUILayout.Toggle(Text("Flip X", "水平翻转"), flipX);
            flipY = EditorGUILayout.Toggle(Text("Flip Y", "垂直翻转"), flipY);
            EditorGUILayout.EndVertical();
        }

        private void DrawCurrentPreviewSection()
        {
            EditorGUILayout.LabelField(Text("Current Preview", "当前预览"), CampusMapEditorStyles.Header);
            EditorGUILayout.BeginVertical(CampusMapEditorStyles.HelpBox);
            EditorGUILayout.LabelField(Text("Mode", "模式"), BrushModeLabel(ActiveBrushMode), CampusMapEditorStyles.MiniPreview);
            EditorGUILayout.LabelField(Text("Tile", "地面"), ObjectName(currentFloorTile, debugAssets.FloorTile), CampusMapEditorStyles.MiniPreview);
            EditorGUILayout.LabelField(Text("Floor Tile Size", "地砖尺寸"), FloorTileSizeLabel(), CampusMapEditorStyles.MiniPreview);
            EditorGUILayout.LabelField(Text("Wall", "墙体"), ObjectName(currentWallTile, debugAssets.WallTile), CampusMapEditorStyles.MiniPreview);
            EditorGUILayout.LabelField(Text("Wall Profile", "墙体配置"), ObjectName(wallRenderProfile, CampusMapEditorUtility.LoadDefaultWallRenderProfile()), CampusMapEditorStyles.MiniPreview);
            EditorGUILayout.LabelField(Text("Object", "物体"), ObjectName(currentPrefab, debugAssets.PropBoxPrefab), CampusMapEditorStyles.MiniPreview);
            EditorGUILayout.LabelField(Text("Stair", "楼梯"), ObjectName(stairPrefab, debugAssets.StairPrefab), CampusMapEditorStyles.MiniPreview);
            EditorGUILayout.EndVertical();
        }

        private void DrawTileButtons(List<TileBase> tiles, System.Action<TileBase> onSelect)
        {
            if (tiles == null || tiles.Count == 0)
            {
                return;
            }

            int column = 0;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < tiles.Count; i++)
            {
                TileBase tile = tiles[i];
                if (!CampusTilePalette.IsUsableTile(tile))
                {
                    continue;
                }

                if (GUILayout.Button(CampusObjectNames.GetDisplayName(tile.name), GUILayout.MaxWidth(120f)))
                {
                    onSelect(tile);
                }

                column++;
                if (column % 3 == 0 && i < tiles.Count - 1)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWallButtons()
        {
            List<TileBase> wallTiles = new List<TileBase>();
            if (wallPalette != null)
            {
                AddWallButtonTile(wallTiles, wallPalette.HighWall);
                AddWallButtonTile(wallTiles, wallPalette.HorizontalWall);
                AddWallButtonTile(wallTiles, wallPalette.VerticalWall);
                AddWallButtonTile(wallTiles, wallPalette.CornerWall);

                if (wallPalette.WallTiles != null)
                {
                    for (int i = 0; i < wallPalette.WallTiles.Count; i++)
                    {
                        AddWallButtonTile(wallTiles, wallPalette.WallTiles[i]);
                    }
                }
            }

            DrawTileButtons(wallTiles, SetCurrentWallTile);
        }

        private static void AddWallButtonTile(List<TileBase> wallTiles, TileBase tile)
        {
            if (wallTiles != null && CampusTilePalette.IsUsableTile(tile) && !wallTiles.Contains(tile))
            {
                wallTiles.Add(tile);
            }
        }

        private void DrawPrefabButtons(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                return;
            }

            int column = 0;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                if (GUILayout.Button(CampusObjectNames.GetDisplayName(prefab.name), GUILayout.MaxWidth(120f)))
                {
                    SetCurrentPrefab(prefab);
                }

                column++;
                if (column % 3 == 0 && i < prefabs.Count - 1)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyDebugFallbacks(bool force)
        {
            if (debugAssets.FloorTile == null)
            {
                debugAssets = CampusMapEditorUtility.EnsureDebugAssets();
            }

            LoadGeneratedPalettesIfAvailable();

            if (force || currentFloorTile == null)
            {
                currentFloorTile = floorTilePalette != null && floorTilePalette.GetTileOrNull(0) != null
                    ? floorTilePalette.GetTileOrNull(0)
                    : debugAssets.FloorTile;
            }

            if (force || currentWallTile == null)
            {
                currentWallTile = GetDefaultWallTile();
                if (currentWallTile == null)
                {
                    currentWallTile = debugAssets.HighWallTile != null ? debugAssets.HighWallTile : debugAssets.WallTile;
                }
            }
            else
            {
                ReplaceDebugWallWithPaletteWall();
            }

            if (force || currentPrefab == null)
            {
                currentPrefab = debugAssets.PropBoxPrefab;
            }

            if (force || stairPrefab == null)
            {
                stairPrefab = debugAssets.StairPrefab;
            }
        }

        private void ApplyExistingFallbacksOnly(bool force)
        {
            LoadGeneratedPalettesIfAvailable();

            if (force || currentFloorTile == null)
            {
                currentFloorTile = floorTilePalette != null && floorTilePalette.GetTileOrNull(0) != null
                    ? floorTilePalette.GetTileOrNull(0)
                    : debugAssets.FloorTile;
            }

            if (force || currentWallTile == null)
            {
                currentWallTile = GetDefaultWallTile();
                if (currentWallTile == null)
                {
                    currentWallTile = debugAssets.HighWallTile != null ? debugAssets.HighWallTile : debugAssets.WallTile;
                }
            }
            else
            {
                ReplaceDebugWallWithPaletteWall();
            }

            if (force || currentPrefab == null)
            {
                currentPrefab = debugAssets.PropBoxPrefab;
            }

            if (force || stairPrefab == null)
            {
                stairPrefab = debugAssets.StairPrefab;
            }
        }

        private void LoadGeneratedPalettesIfAvailable()
        {
            if (floorTilePalette == null)
            {
                floorTilePalette = CampusMapEditorUtility.LoadDefaultFloorPalette();
            }

            if (wallPalette == null)
            {
                wallPalette = CampusMapEditorUtility.LoadDefaultWallPalette();
            }

            if (prefabPalette == null)
            {
                prefabPalette = CampusMapEditorUtility.LoadDefaultPrefabPalette();
            }

            if (wallRenderProfile == null)
            {
                wallRenderProfile = CampusMapEditorUtility.LoadDefaultWallRenderProfile();
            }

            SyncWallTextureSelectionsFromProfile(false);
        }

        private void SyncWallTextureSelectionsFromProfile(bool force)
        {
            CampusWallRenderProfile profile = GetWallRenderProfileOrFallback();
            if (profile == null)
            {
                return;
            }

            if (force || selectedWallFaceTexture == null)
            {
                selectedWallFaceTexture = profile.FaceSourceTexture;
            }

            if (force || selectedWallCapTexture == null)
            {
                selectedWallCapTexture = profile.CapSourceTexture;
            }
        }

        private TileBase GetDefaultWallTile()
        {
            CampusWallRenderProfile profile = GetWallRenderProfileOrFallback();
            if (profile != null && CampusTilePalette.IsUsableTile(profile.GetLogicTile()))
            {
                return profile.GetLogicTile();
            }

            if (wallPalette == null)
            {
                return null;
            }

            if (CampusTilePalette.IsUsableTile(wallPalette.HighWall))
            {
                return wallPalette.HighWall;
            }

            if (wallPalette.WallTiles != null)
            {
                for (int i = 0; i < wallPalette.WallTiles.Count; i++)
                {
                    if (CampusTilePalette.IsUsableTile(wallPalette.WallTiles[i]))
                    {
                        return wallPalette.WallTiles[i];
                    }
                }
            }

            if (CampusTilePalette.IsUsableTile(wallPalette.HorizontalWall))
            {
                return wallPalette.HorizontalWall;
            }

            if (CampusTilePalette.IsUsableTile(wallPalette.VerticalWall))
            {
                return wallPalette.VerticalWall;
            }

            if (CampusTilePalette.IsUsableTile(wallPalette.CornerWall))
            {
                return wallPalette.CornerWall;
            }

            return null;
        }

        private CampusWallRenderProfile GetWallRenderProfileOrFallback()
        {
            CampusWallVisualCatalog catalog = CampusMapEditorUtility.LoadWallVisualCatalog();
            CampusWallRenderProfile fallback = wallRenderProfile != null
                ? wallRenderProfile
                : (catalog != null ? catalog.GetProfileOrDefault(null) : CampusMapEditorUtility.LoadDefaultWallRenderProfile());
            if (catalog != null && CampusTilePalette.IsUsableTile(currentWallTile))
            {
                return catalog.GetProfileForLogicTile(currentWallTile, fallback);
            }

            return fallback;
        }

        private void ReplaceDebugWallWithPaletteWall()
        {
            TileBase paletteWall = GetDefaultWallTile();
            if (paletteWall == null || paletteWall == currentWallTile)
            {
                return;
            }

            if (currentWallTile == debugAssets.HighWallTile ||
                currentWallTile == debugAssets.WallTile ||
                currentWallTile == debugAssets.HorizontalWallTile ||
                currentWallTile == debugAssets.VerticalWallTile ||
                currentWallTile == debugAssets.CornerWallTile)
            {
                currentWallTile = paletteWall;
            }
        }

        private CampusFloorVisibilityController EnsureVisibilityController()
        {
            if (mapRoot == null)
            {
                return null;
            }

            CampusFloorVisibilityController controller = mapRoot.GetComponent<CampusFloorVisibilityController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<CampusFloorVisibilityController>(mapRoot.gameObject);
            }

            controller.MapRoot = mapRoot;
            return controller;
        }

        private void ClampCurrentFloorIndex()
        {
            if (mapRoot == null)
            {
                currentFloorIndex = 1;
                return;
            }

            mapRoot.RebuildFloorReferences();
            if (mapRoot.Floors.Count == 0)
            {
                currentFloorIndex = 1;
                return;
            }

            if (mapRoot.GetFloor(currentFloorIndex) != null)
            {
                return;
            }

            currentFloorIndex = mapRoot.Floors[0].FloorIndex;
        }

        private string[] BuildFloorLabelsForCurrentFloor(List<CampusFloorRoot> floors, out int selectedIndex)
        {
            string[] labels = BuildFloorLabelsStatic(floors, currentFloorIndex, out selectedIndex);
            return labels;
        }

        private static string[] BuildFloorLabelsStatic(List<CampusFloorRoot> floors, int currentFloor, out int selectedIndex)
        {
            selectedIndex = 0;
            if (floors == null || floors.Count == 0)
            {
                return new string[0];
            }

            string[] labels = new string[floors.Count];
            for (int i = 0; i < floors.Count; i++)
            {
                CampusFloorRoot floor = floors[i];
                if (floor == null)
                {
                    labels[i] = "Missing Floor";
                    continue;
                }

                if (floor.FloorIndex == currentFloor)
                {
                    selectedIndex = i;
                }

                labels[i] = "Floor " + floor.FloorIndex + (floor.IsUnlocked ? "" : " (Locked)");
            }

            return labels;
        }

        private static string ObjectName(Object selected, Object fallback)
        {
            Object value = selected != null ? selected : fallback;
            return value != null ? CampusObjectNames.GetDisplayName(value.name) : "None";
        }
    }
}
