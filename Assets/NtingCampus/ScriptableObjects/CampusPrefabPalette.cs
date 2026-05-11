using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [CreateAssetMenu(menuName = "Nting Campus/Prefab Palette", fileName = "CampusPrefabPalette")]
    public sealed class CampusPrefabPalette : ScriptableObject
    {
        public List<GameObject> Prefabs = new List<GameObject>();

        public void RemoveInvalidEntries()
        {
            if (Prefabs == null)
            {
                Prefabs = new List<GameObject>();
                return;
            }

            Prefabs.RemoveAll(prefab => prefab == null);
        }

        public GameObject GetPrefabOrNull(int index)
        {
            RemoveInvalidEntries();
            if (Prefabs == null || index < 0 || index >= Prefabs.Count)
            {
                return null;
            }

            return Prefabs[index];
        }
    }
}
