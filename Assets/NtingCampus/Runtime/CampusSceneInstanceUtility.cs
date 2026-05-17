using UnityEngine;

namespace NtingCampusMapEditor
{
    public static class CampusSceneInstanceUtility
    {
        private const HideFlags RuntimeSourceFlags =
            HideFlags.DontSaveInEditor |
            HideFlags.DontSaveInBuild |
            HideFlags.DontUnloadUnusedAsset;

        public static void NormalizeSceneInstance(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null)
                {
                    continue;
                }

                GameObject gameObject = transform.gameObject;
                gameObject.hideFlags &= ~RuntimeSourceFlags;

                Component[] components = gameObject.GetComponents<Component>();
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component != null)
                    {
                        component.hideFlags &= ~RuntimeSourceFlags;
                    }
                }
            }
        }
    }
}
