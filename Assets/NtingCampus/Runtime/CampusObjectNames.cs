using UnityEngine;

namespace NtingCampusMapEditor
{
    public static class CampusObjectNames
    {
        public const string MapRoot = "\u6821\u56ed\u5730\u56fe\u6839\u8282\u70b9";
        public const string LegacyMapRoot = "CampusMapRoot";

        public const string FloorsRoot = "\u697c\u5c42";
        public const string LegacyFloorsRoot = "Floors";

        public const string EditorDataRoot = "\u7f16\u8f91\u5668\u6570\u636e";
        public const string LegacyEditorDataRoot = "EditorData";

        public const string FloorPrefix = "\u697c\u5c42_";
        public const string LegacyFloorPrefix = "Floor_";

        public const string Grid = "\u7f51\u683c";
        public const string LegacyGrid = "Grid";

        public const string FloorTilemap = "\u5730\u677f\u74e6\u7247\u56fe";
        public const string LegacyFloorTilemap = "Tilemap_Floor";

        public const string WallLogicTilemap = "\u5899\u4f53\u903b\u8f91\u74e6\u7247\u56fe";
        public const string LegacyWallLogicTilemap = "Tilemap_WallLogic";
        public const string LegacyWallsTilemap = "Tilemap_Walls";

        public const string WallCapTilemap = "\u5899\u9876\u74e6\u7247\u56fe";
        public const string LegacyWallCapTilemap = "Tilemap_WallCap";

        public const string WallFaceTilemap = "\u5899\u9762\u74e6\u7247\u56fe";
        public const string LegacyWallFaceTilemap = "Tilemap_WallFace";

        public const string WallSideTilemap = "\u5899\u4fa7\u74e6\u7247\u56fe";
        public const string LegacyWallSideTilemap = "Tilemap_WallSide";

        public const string WallShadowTilemap = "\u5899\u4f53\u9634\u5f71\u74e6\u7247\u56fe";
        public const string LegacyWallShadowTilemap = "Tilemap_WallShadow";

        public const string WallOverlayTilemap = "\u5899\u4f53\u8986\u76d6\u74e6\u7247\u56fe";
        public const string LegacyWallOverlayTilemap = "Tilemap_WallOverlay";

        public const string WallMeshRoot = "\u5899\u4f533D\u89c6\u89c9";
        public const string LegacyWallMeshRoot = "WallMesh_VisualRoot";

        public const string OverlayTilemap = "\u8986\u76d6\u74e6\u7247\u56fe";
        public const string LegacyOverlayTilemap = "Tilemap_Overlay";

        public const string CollisionDebugTilemap = "\u78b0\u649e\u8c03\u8bd5\u74e6\u7247\u56fe";
        public const string LegacyCollisionDebugTilemap = "Tilemap_Collision_Debug";

        public const string PropsRoot = "\u9053\u5177\u6839\u8282\u70b9";
        public const string LegacyPropsRoot = "PropsRoot";

        public const string StairsRoot = "\u697c\u68af\u6839\u8282\u70b9";
        public const string LegacyStairsRoot = "StairsRoot";

        public const string TestPlayer = "\u6d4b\u8bd5\u4eba\u7269";
        public const string LegacyTestPlayer = "Campus_TestPlayer";

        public const string TestPropBox = "\u6d4b\u8bd5\u7bb1";
        public const string LegacyDebugPropBox = "Debug_Prop_Box";

        public const string DiningTable = "\u9910\u684c_3x4";
        public const string LegacyDiningTable = "Dining_Table_2x3";
        public const string DiningTableVisual = "\u9910\u684c\u89c6\u89c9";
        public const string LegacyDiningTableVisual = "Dining_Table_2x3_Visual";

        public const string Door = "\u95e8";
        public const string LegacyDoor = "Door_South";
        public const string DoorVisual = "\u95e8\u89c6\u89c9";
        public const string LegacyDoorVisual = "Door_Visual";
        public const string LegacyStandaloneDoorVisual = "Door_South_Visual";

        public const string SquatStall = "\u8e72\u5751\u5305\u95f4";
        public const string LegacyRestroomSquatStall = "Restroom_SquatStall";

        public const string Urinal = "\u5c0f\u4fbf\u6c60";
        public const string LegacyRestroomUrinal = "Restroom_Urinal";

        public const string BaseVisual = "\u5e95\u5ea7\u89c6\u89c9";
        public const string LegacyBaseVisual = "Base_Visual";

        public const string DoorPivot = "\u95e8\u8f74";
        public const string LegacyDoorPivot = "Door_Pivot";

        public const string FrameColliders = "\u95e8\u6846\u78b0\u649e\u4f53";
        public const string LegacyFrameColliders = "Frame_Colliders";

        public const string DoorCollider = "\u95e8\u78b0\u649e\u4f53";
        public const string LegacyDoorCollider = "Door_Collider";

        public const string TestStair = "\u6d4b\u8bd5\u697c\u68af";
        public const string LegacyDebugStair = "Debug_Stair";

        public const string MainCamera = "\u4e3b\u76f8\u673a";
        public const string LegacyMainCamera = "Main Camera";

        public const string GlobalLight2D = "\u4e8c\u7ef4\u5168\u5c40\u5149";
        public const string LegacyGlobalLight2D = "Global Light 2D";
        public const string SunLight2D = "\u4e8c\u7ef4\u65e5\u5149";
        public const string LegacySunLight2D = "Campus Sun Light 2D";

        public const string MapKeyLight = "\u5730\u56fe\u4e3b\u5149\u6e90";
        public const string LegacyMapKeyLight = "Campus Map Key Light";

        public static string GetFloorName(int floorIndex)
        {
            return FloorPrefix + floorIndex;
        }

        public static Transform FindDirectChild(Transform parent, params string[] names)
        {
            if (parent == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                {
                    continue;
                }

                Transform child = parent.Find(names[i]);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        public static bool MatchesAny(string candidate, params string[] names)
        {
            if (string.IsNullOrEmpty(candidate) || names == null)
            {
                return false;
            }

            string normalizedCandidate = LocalizeLegacyHierarchyName(candidate);
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (candidate == name || normalizedCandidate == LocalizeLegacyHierarchyName(name))
                {
                    return true;
                }
            }

            return false;
        }

        public static string LocalizeLegacyHierarchyName(string name)
        {
            switch (name)
            {
                case LegacyMapRoot:
                    return MapRoot;
                case LegacyFloorsRoot:
                    return FloorsRoot;
                case LegacyEditorDataRoot:
                    return EditorDataRoot;
                case LegacyGrid:
                    return Grid;
                case LegacyFloorTilemap:
                    return FloorTilemap;
                case LegacyWallLogicTilemap:
                case LegacyWallsTilemap:
                    return WallLogicTilemap;
                case LegacyWallCapTilemap:
                    return WallCapTilemap;
                case LegacyWallFaceTilemap:
                    return WallFaceTilemap;
                case LegacyWallSideTilemap:
                    return WallSideTilemap;
                case LegacyWallShadowTilemap:
                    return WallShadowTilemap;
                case LegacyWallOverlayTilemap:
                    return WallOverlayTilemap;
                case LegacyWallMeshRoot:
                    return WallMeshRoot;
                case LegacyOverlayTilemap:
                    return OverlayTilemap;
                case LegacyCollisionDebugTilemap:
                    return CollisionDebugTilemap;
                case LegacyPropsRoot:
                    return PropsRoot;
                case LegacyStairsRoot:
                    return StairsRoot;
                case LegacyTestPlayer:
                    return TestPlayer;
                case LegacyDebugPropBox:
                    return TestPropBox;
                case LegacyDiningTable:
                    return DiningTable;
                case LegacyDiningTableVisual:
                    return DiningTableVisual;
                case LegacyDoor:
                    return Door;
                case LegacyDoorVisual:
                case LegacyStandaloneDoorVisual:
                    return DoorVisual;
                case LegacyRestroomSquatStall:
                    return SquatStall;
                case LegacyRestroomUrinal:
                    return Urinal;
                case LegacyBaseVisual:
                    return BaseVisual;
                case LegacyDoorPivot:
                    return DoorPivot;
                case LegacyFrameColliders:
                    return FrameColliders;
                case LegacyDoorCollider:
                    return DoorCollider;
                case LegacyDebugStair:
                    return TestStair;
                case LegacyMainCamera:
                    return MainCamera;
                case LegacyGlobalLight2D:
                    return GlobalLight2D;
                case LegacySunLight2D:
                    return SunLight2D;
                case LegacyMapKeyLight:
                    return MapKeyLight;
                default:
                    return name;
            }
        }

        public static string GetDisplayName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return rawName;
            }

            string localized = LocalizeLegacyHierarchyName(rawName);
            if (localized != rawName)
            {
                return localized;
            }

            switch (rawName)
            {
                case "CampusTilePalette":
                    return "\u5730\u9762\u74e6\u7247\u9762\u677f";
                case "CampusWallPalette":
                    return "\u5899\u4f53\u74e6\u7247\u9762\u677f";
                case "CampusPrefabPalette":
                    return "\u7269\u4f53\u8d44\u6e90\u9762\u677f";
                case "CampusWallVisualCatalog":
                    return "\u5899\u4f53\u89c6\u89c9\u76ee\u5f55";
                case "CampusPrankSpot_PassNote":
                    return "\u4f20\u7eb8\u6761";
                case "CampusPrankSpot_ConfuseBooks":
                    return "\u4e71\u6574\u7406\u4e66";
                case "CampusPrankSpot_StealDelivery":
                    return "\u5077\u5916\u5356";
                case "CampusPrankSpot_StealFriedChicken":
                    return "\u5077\u70b8\u9e21";
                case "CampusPrankSpot_TwistBottleCaps":
                    return "\u62e7\u74f6\u76d6";
                case "Debug_FloorTile":
                    return "\u8c03\u8bd5\u5730\u9762";
                case "Debug_WallTile":
                    return "\u8c03\u8bd5\u5899\u4f53";
                case "Wall_Corner_Debug":
                    return "\u8c03\u8bd5\u8f6c\u89d2\u5899";
                case "Wall_High_Debug":
                    return "\u8c03\u8bd5\u9ad8\u5899";
                case "Wall_Horizontal_Debug":
                    return "\u8c03\u8bd5\u6a2a\u5899";
                case "Wall_Vertical_Debug":
                    return "\u8c03\u8bd5\u7ad6\u5899";
                case "Default_WallLogic":
                    return "\u9ed8\u8ba4\u5899\u903b\u8f91";
                case "DefaultPrototypeWall_WallLogic":
                    return "\u9ed8\u8ba4\u539f\u578b\u5899\u903b\u8f91";
                case "Default Prototype Wall Profile":
                    return "\u9ed8\u8ba4\u539f\u578b\u5899\u4f53\u914d\u7f6e";
                case "Brick Prototype Wall Profile":
                    return "\u7816\u5899\u539f\u578b\u5899\u4f53\u914d\u7f6e";
            }

            if (rawName.StartsWith("FloorTile_"))
            {
                return "\u5730\u9762_" + rawName.Substring("FloorTile_".Length);
            }

            if (rawName.StartsWith("WallTile_"))
            {
                return "\u5899\u4f53_" + rawName.Substring("WallTile_".Length);
            }

            if (rawName.EndsWith(" Wall Profile"))
            {
                string baseName = rawName.Substring(0, rawName.Length - " Wall Profile".Length);
                return GetDisplayName(baseName) + " \u5899\u4f53\u914d\u7f6e";
            }

            if (rawName.EndsWith("_WallLogic"))
            {
                string baseName = rawName.Substring(0, rawName.Length - "_WallLogic".Length);
                return GetDisplayName(baseName) + "_\u5899\u903b\u8f91";
            }

            if (rawName.EndsWith("_Visual"))
            {
                string baseName = rawName.Substring(0, rawName.Length - "_Visual".Length);
                return GetDisplayName(baseName) + "\u89c6\u89c9";
            }

            return rawName;
        }
    }
}
