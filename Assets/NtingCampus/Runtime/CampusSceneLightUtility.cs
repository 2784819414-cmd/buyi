using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NtingCampusMapEditor
{
    internal static class CampusSceneLightUtility
    {
        private static readonly List<Light2D> scratchLights = new List<Light2D>(64);
        private static CampusMapRoot cachedMapRoot;
        private static CampusDayNightController cachedDayNight;

        internal static void CollectLights(List<Light2D> results, bool includeInactive)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            ResolveSceneRoots();

            if (cachedMapRoot != null)
            {
                cachedMapRoot.GetComponentsInChildren(includeInactive, results);
            }
            else
            {
                Light2D[] sceneLights = Object.FindObjectsByType<Light2D>(
                    includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (int i = 0; i < sceneLights.Length; i++)
                {
                    if (sceneLights[i] != null)
                    {
                        results.Add(sceneLights[i]);
                    }
                }
            }

            if (cachedDayNight != null && (cachedMapRoot == null || !cachedDayNight.transform.IsChildOf(cachedMapRoot.transform)))
            {
                scratchLights.Clear();
                cachedDayNight.GetComponentsInChildren(includeInactive, scratchLights);
                for (int i = 0; i < scratchLights.Count; i++)
                {
                    Light2D light = scratchLights[i];
                    if (light != null && !results.Contains(light))
                    {
                        results.Add(light);
                    }
                }
            }
        }

        internal static void InvalidateSceneRoots()
        {
            cachedMapRoot = null;
            cachedDayNight = null;
        }

        private static void ResolveSceneRoots()
        {
            if (cachedMapRoot == null)
            {
                cachedMapRoot = Object.FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            }

            if (cachedDayNight == null)
            {
                cachedDayNight = Object.FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }
        }
    }
}
