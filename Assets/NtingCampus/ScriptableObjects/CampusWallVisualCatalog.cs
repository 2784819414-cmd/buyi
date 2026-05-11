using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    [System.Serializable]
    public sealed class CampusWallVisualProfileBinding
    {
        public TileBase LogicTile;
        public CampusWallRenderProfile Profile;
    }

    /// <summary>
    /// Lists wall render profiles shown by the editor.
    /// </summary>
    [CreateAssetMenu(menuName = "Nting Campus/Wall Visual Catalog", fileName = "CampusWallVisualCatalog")]
    public sealed class CampusWallVisualCatalog : ScriptableObject
    {
        public CampusWallRenderProfile DefaultProfile;
        public List<CampusWallRenderProfile> Profiles = new List<CampusWallRenderProfile>();
        public List<CampusWallVisualProfileBinding> ProfileBindings = new List<CampusWallVisualProfileBinding>();

        public CampusWallRenderProfile GetProfileOrDefault(CampusWallRenderProfile selected)
        {
            if (selected != null)
            {
                return selected;
            }

            if (DefaultProfile != null)
            {
                return DefaultProfile;
            }

            if (Profiles != null)
            {
                for (int i = 0; i < Profiles.Count; i++)
                {
                    if (Profiles[i] != null)
                    {
                        return Profiles[i];
                    }
                }
            }

            return null;
        }

        public CampusWallRenderProfile GetProfileForLogicTile(TileBase logicTile, CampusWallRenderProfile fallback)
        {
            if (logicTile != null && ProfileBindings != null)
            {
                for (int i = 0; i < ProfileBindings.Count; i++)
                {
                    CampusWallVisualProfileBinding binding = ProfileBindings[i];
                    if (binding != null && binding.LogicTile == logicTile && binding.Profile != null)
                    {
                        return binding.Profile;
                    }
                }
            }

            if (logicTile != null && Profiles != null)
            {
                for (int i = 0; i < Profiles.Count; i++)
                {
                    CampusWallRenderProfile profile = Profiles[i];
                    if (profile != null && profile.GetLogicTile() == logicTile)
                    {
                        return profile;
                    }
                }
            }

            return GetProfileOrDefault(fallback);
        }
    }
}
