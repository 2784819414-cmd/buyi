using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor.EditorTools
{
    public static class CampusShadowDiagnostics
    {
        private const string DefaultScenePath = "Assets/Scenes/CampusMap.unity";

        [MenuItem("Tools/Nting Campus/Diagnostics/Projected Shadow Report")]
        public static void RunProjectedShadowReport()
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

            root.RebuildFloorReferences();
            for (int i = 0; i < root.Floors.Count; i++)
            {
                CampusFloorRoot floor = root.Floors[i];
                if (floor == null)
                {
                    continue;
                }

                CampusProjectedWallShadowRenderer projected = CampusProjectedWallShadowRenderer.EnsureForFloor(floor);
                Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
                int wallCells = CountTiles(wallLogic);
                int placedObjects = floor.PropsRoot != null
                    ? floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true).Length
                    : 0;
                MeshFilter meshFilter = projected != null ? projected.GetComponent<MeshFilter>() : null;
                MeshRenderer meshRenderer = projected != null ? projected.GetComponent<MeshRenderer>() : null;
                Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                int edgeCount = ReadPrivateListCount(projected, "cachedEdges");

                Debug.Log(
                    "[NtingCampusShadowDiagnostic] floor=" + floor.FloorIndex +
                    " wallCells=" + wallCells +
                    " placedObjects=" + placedObjects +
                    " projected=" + (projected != null ? projected.name : "null") +
                    " active=" + (projected != null && projected.gameObject.activeInHierarchy) +
                    " enabled=" + (meshRenderer != null && meshRenderer.enabled) +
                    " edges=" + edgeCount +
                    " vertices=" + (mesh != null ? mesh.vertexCount : 0) +
                    " triangles=" + (mesh != null ? mesh.triangles.Length / 3 : 0) +
                    " material=" + (meshRenderer != null && meshRenderer.sharedMaterial != null ? meshRenderer.sharedMaterial.name : "null") +
                    " shader=" + (meshRenderer != null && meshRenderer.sharedMaterial != null && meshRenderer.sharedMaterial.shader != null ? meshRenderer.sharedMaterial.shader.name : "null") +
                    " sortingLayer=" + (meshRenderer != null ? SortingLayer.IDToName(meshRenderer.sortingLayerID) : "null") +
                    " sortingOrder=" + (meshRenderer != null ? meshRenderer.sortingOrder : 0));
            }
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

        private static int ReadPrivateListCount(object target, string fieldName)
        {
            if (target == null)
            {
                return 0;
            }

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            object value = field != null ? field.GetValue(target) : null;
            PropertyInfo count = value != null ? value.GetType().GetProperty("Count") : null;
            return count != null ? (int)count.GetValue(value) : 0;
        }
    }
}
