using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor.EditorTools
{
    public static class CampusShadowDiagnostics
    {
        private const string DefaultScenePath = "Assets/Scenes/CampusMap.unity";

        [MenuItem("Tools/Nting Campus/Diagnostics/Unity ShadowCaster Report")]
        public static void RunUnityShadowCasterReport()
        {
            if (!Application.isPlaying && System.IO.File.Exists(DefaultScenePath))
            {
                EditorSceneManager.OpenScene(DefaultScenePath);
            }

            CampusMapRoot root = Object.FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                Debug.Log("[NtingCampusShadowDiagnostic] No CampusMapRoot found.");
                return;
            }

            Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int shadowLights = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light != null && light.lightType != Light2D.LightType.Global && light.shadowsEnabled)
                {
                    shadowLights++;
                }
            }

            root.RebuildFloorReferences();
            for (int i = 0; i < root.Floors.Count; i++)
            {
                CampusFloorRoot floor = root.Floors[i];
                if (floor == null)
                {
                    continue;
                }

                Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
                int wallCells = CountTiles(wallLogic);
                int placedObjects = floor.PropsRoot != null
                    ? floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true).Length
                    : 0;

                ShadowCaster2D[] casters = floor.GetComponentsInChildren<ShadowCaster2D>(true);
                int enabledCasters = 0;
                int wallCasters = 0;
                int objectCasters = 0;
                int stairCasters = 0;
                for (int casterIndex = 0; casterIndex < casters.Length; casterIndex++)
                {
                    ShadowCaster2D caster = casters[casterIndex];
                    if (caster == null)
                    {
                        continue;
                    }

                    if (caster.enabled)
                    {
                        enabledCasters++;
                    }

                    Transform casterTransform = caster.transform;
                    if (floor.PropsRoot != null && IsChildOf(casterTransform, floor.PropsRoot))
                    {
                        objectCasters++;
                    }
                    else if (floor.StairsRoot != null && IsChildOf(casterTransform, floor.StairsRoot))
                    {
                        stairCasters++;
                    }
                    else if (caster.name.StartsWith("WallGroundShadowCaster_", System.StringComparison.Ordinal))
                    {
                        wallCasters++;
                    }
                }

                Debug.Log(
                    "[NtingCampusShadowDiagnostic] floor=" + floor.FloorIndex +
                    " wallCells=" + wallCells +
                    " placedObjects=" + placedObjects +
                    " shadowLights=" + shadowLights +
                    " casters=" + casters.Length +
                    " enabledCasters=" + enabledCasters +
                    " wallCasters=" + wallCasters +
                    " objectCasters=" + objectCasters +
                    " stairCasters=" + stairCasters);
            }
        }

        private static bool IsChildOf(Transform child, Transform parent)
        {
            return child != null && parent != null && (child == parent || child.IsChildOf(parent));
        }

        private static int CountTiles(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                return 0;
            }

            int count = 0;
            tilemap.CompressBounds();
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(cell))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
