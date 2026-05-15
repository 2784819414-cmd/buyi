using System.Collections.Generic;
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
            if (Floors == null)
            {
                Floors = new List<CampusFloorRoot>();
            }

            Floors.Clear();
            GetComponentsInChildren(true, Floors);
            Floors.Sort(CompareFloorIndex);

            for (int i = 0; i < Floors.Count; i++)
            {
                CampusFloorRoot floor = Floors[i];
                if (floor == null)
                {
                    continue;
                }

                floor.RefreshUsedBoundsIfDirty();
                floor.CaptureOriginalRenderState();
            }
        }

        private static int CompareFloorIndex(CampusFloorRoot a, CampusFloorRoot b)
        {
            int left = a != null ? a.FloorIndex : int.MinValue;
            int right = b != null ? b.FloorIndex : int.MinValue;
            return left.CompareTo(right);
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
