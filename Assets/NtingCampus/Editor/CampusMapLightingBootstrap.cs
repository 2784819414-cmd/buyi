using UnityEditor;

namespace NtingCampusMapEditor
{
    public static class CampusMapLightingBootstrap
    {
        [MenuItem("Tools/Nting Campus/Ensure Lighting For Current Map")]
        private static void EnsureLightingFromMenu()
        {
            CampusMapEditorUtility.EnsureMapLighting(CampusMapEditorUtility.FindCampusMapRoot());
        }
    }
}
