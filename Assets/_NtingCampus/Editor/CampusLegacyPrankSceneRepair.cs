using NtingCampus.Gameplay.Pranks;
using NtingCampusMapEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NtingCampus.EditorTools
{
    public static class CampusLegacyPrankSceneRepair
    {
        private const string ScenePath = "Assets/Scenes/CampusMap.unity";
        private const string LegacyRootName = "NtingCampus_V01_MischiefSkeletonRoot";

        [MenuItem("NtingCampus/Gameplay/Cleanup Scene Prank Spots")]
        public static void CleanupScenePrankSpotsMenu()
        {
            CleanupScenePrankSpots();
        }

        public static void CleanupScenePrankSpots()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveLegacySkeletonRoot();
            RemoveStandalonePrankSpots();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CampusLegacyPrankSceneRepair] Removed standalone prank scene spots. Formal prank points should come from CampusRuntimeImports objects.");
        }

        private static void RemoveLegacySkeletonRoot()
        {
            GameObject legacyRoot = GameObject.Find(LegacyRootName);
            if (legacyRoot != null)
            {
                Object.DestroyImmediate(legacyRoot);
            }
        }

        private static void RemoveStandalonePrankSpots()
        {
            CampusPrankInteractionSpot[] spots = Object.FindObjectsByType<CampusPrankInteractionSpot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < spots.Length; index++)
            {
                CampusPrankInteractionSpot spot = spots[index];
                if (spot != null && spot.GetComponentInParent<CampusPlacedObject>() == null)
                {
                    Object.DestroyImmediate(spot.gameObject);
                }
            }
        }
    }
}
