using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Scene root for all editor-authored campus floors.
    /// </summary>
    public sealed class CampusMapRoot : MonoBehaviour
    {
        public List<CampusFloorRoot> Floors = new List<CampusFloorRoot>();
        public int CurrentPreviewFloor = 1;
        public float LowerFloorAlphaStep = 0.72f;
        public float LowerFloorDarkenStep = 0.82f;
        public int SortingOrderStepPerFloor = 1000;

        public Transform FloorsRoot => CampusObjectNames.FindDirectChild(transform, CampusObjectNames.FloorsRoot, CampusObjectNames.LegacyFloorsRoot);
        public Transform EditorDataRoot => CampusObjectNames.FindDirectChild(transform, CampusObjectNames.EditorDataRoot, CampusObjectNames.LegacyEditorDataRoot);

        public void RebuildFloorReferences()
        {
            Floors = GetComponentsInChildren<CampusFloorRoot>(true)
                .Where(floor => floor != null)
                .OrderBy(floor => floor.FloorIndex)
                .ToList();

            for (int i = 0; i < Floors.Count; i++)
            {
                Floors[i].RefreshUsedBounds();
                Floors[i].CaptureOriginalRenderState();
            }
        }

        public CampusFloorRoot GetFloor(int floorIndex)
        {
            if (Floors == null)
            {
                return null;
            }

            for (int i = 0; i < Floors.Count; i++)
            {
                if (Floors[i] != null && Floors[i].FloorIndex == floorIndex)
                {
                    return Floors[i];
                }
            }

            return null;
        }

        public int GetHighestFloorIndex()
        {
            int highest = 0;
            if (Floors == null)
            {
                return highest;
            }

            for (int i = 0; i < Floors.Count; i++)
            {
                if (Floors[i] != null)
                {
                    highest = Mathf.Max(highest, Floors[i].FloorIndex);
                }
            }

            return highest;
        }

        public void CaptureFloorOriginalStates(bool force)
        {
            RebuildFloorReferences();
            for (int i = 0; i < Floors.Count; i++)
            {
                if (Floors[i] == null)
                {
                    continue;
                }

                Floors[i].CaptureOriginalRenderState(force);
            }
        }
    }
}
