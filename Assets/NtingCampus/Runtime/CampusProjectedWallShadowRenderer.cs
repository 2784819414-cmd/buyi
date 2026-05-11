using UnityEngine;

namespace NtingCampusMapEditor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CampusProjectedWallShadowRenderer : MonoBehaviour
    {
        private const string RendererName = "Projected Wall Shadows";

        public static CampusProjectedWallShadowRenderer EnsureForFloor(CampusFloorRoot targetFloor)
        {
            ClearForFloor(targetFloor);
            return null;
        }

        public static void ClearForFloor(CampusFloorRoot targetFloor)
        {
            if (targetFloor == null)
            {
                return;
            }

            Transform parent = targetFloor.Grid != null ? targetFloor.Grid.transform : targetFloor.transform;
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == RendererName)
                {
                    DestroyLegacyObject(child.gameObject);
                }
            }

            CampusProjectedWallShadowRenderer[] renderers = parent.GetComponentsInChildren<CampusProjectedWallShadowRenderer>(true);
            for (int i = renderers.Length - 1; i >= 0; i--)
            {
                CampusProjectedWallShadowRenderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.DisableLegacyRenderer();
                    if (renderer.gameObject.name == RendererName)
                    {
                        DestroyLegacyObject(renderer.gameObject);
                    }
                    else
                    {
                        renderer.enabled = false;
                    }
                }
            }
        }

        private void OnEnable()
        {
            DisableLegacyRenderer();
        }

        private void OnValidate()
        {
            DisableLegacyRenderer();
        }

        private void DisableLegacyRenderer()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = null;
            }
        }

        private static void DestroyLegacyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
