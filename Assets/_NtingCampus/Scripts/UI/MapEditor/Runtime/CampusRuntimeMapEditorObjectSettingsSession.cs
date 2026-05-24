using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public sealed class CampusRuntimeMapEditorObjectSettingsSession
    {
        public Vector2 ScrollPosition { get; set; }
        public int PreviewRotation90 { get; set; }
        public int SelectedCustomAnchorIndex { get; set; }
        public GameObject LastSelectedPrefab { get; private set; }
        public GameObject LastFootprintSyncedPrefab { get; private set; }

        private int directionDropRotation90 = -1;

        public void SyncSelection(
            GameObject prefab,
            CampusPlacedObject placed,
            bool force,
            Action syncFootprintFields)
        {
            if (!force && prefab == LastSelectedPrefab)
            {
                return;
            }

            LastSelectedPrefab = prefab;
            LastFootprintSyncedPrefab = null;
            syncFootprintFields?.Invoke();
            if (placed == null)
            {
                return;
            }

            placed.NormalizeCustomInteractionAnchors();
            placed.NormalizeStorageSettings();
            PreviewRotation90 = CampusPlacedObject.NormalizeRotation90(placed.Rotation90);
            directionDropRotation90 = -1;
            SelectedCustomAnchorIndex = ResolveInitialCustomAnchorIndex(placed);
        }

        public bool IsFootprintSynced(GameObject prefab)
        {
            return prefab == LastFootprintSyncedPrefab;
        }

        public void MarkFootprintSynced(GameObject prefab)
        {
            LastFootprintSyncedPrefab = prefab;
        }

        public void ClearSelectionIfMatches(GameObject prefab)
        {
            if (LastSelectedPrefab != prefab)
            {
                return;
            }

            LastSelectedPrefab = null;
            LastFootprintSyncedPrefab = null;
            SelectedCustomAnchorIndex = 0;
            directionDropRotation90 = -1;
            ScrollPosition = Vector2.zero;
        }

        public void SetDirectionDropTarget(int rotation90Index)
        {
            directionDropRotation90 = CampusPlacedObject.NormalizeRotation90(rotation90Index);
        }

        public void ClearDirectionDropTarget()
        {
            directionDropRotation90 = -1;
        }

        public int ResolveDroppedDirectionTargetRotation(bool panelVisible, bool isMouseOverPanel, bool hasSelectedPrefab)
        {
            if (directionDropRotation90 >= 0)
            {
                return CampusPlacedObject.NormalizeRotation90(directionDropRotation90);
            }

            if (!panelVisible || !isMouseOverPanel || !hasSelectedPrefab)
            {
                return -1;
            }

            return CampusPlacedObject.NormalizeRotation90(PreviewRotation90);
        }

        public CampusPlacedObjectInteractionAnchor GetSelectedCustomAnchor(CampusPlacedObject placed)
        {
            EnsureSelectedCustomAnchorIndex(placed);
            if (placed == null || placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count == 0)
            {
                return null;
            }

            return placed.CustomInteractionAnchors[SelectedCustomAnchorIndex];
        }

        public void EnsureSelectedCustomAnchorIndex(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                SelectedCustomAnchorIndex = 0;
                return;
            }

            placed.CustomInteractionAnchors = placed.CustomInteractionAnchors ?? new List<CampusPlacedObjectInteractionAnchor>();
            if (placed.CustomInteractionAnchors.Count == 0)
            {
                SelectedCustomAnchorIndex = 0;
                return;
            }

            SelectedCustomAnchorIndex = Mathf.Clamp(SelectedCustomAnchorIndex, 0, placed.CustomInteractionAnchors.Count - 1);
            if (placed.CustomInteractionAnchors[SelectedCustomAnchorIndex] == null)
            {
                placed.CustomInteractionAnchors[SelectedCustomAnchorIndex] = new CampusPlacedObjectInteractionAnchor();
            }
        }

        public void SyncLegacyAnchorFieldsFromSelectedAnchor(CampusPlacedObject placed)
        {
            CampusPlacedObjectInteractionAnchor anchor = GetSelectedCustomAnchor(placed);
            if (placed == null || anchor == null)
            {
                return;
            }

            placed.CustomInteractionAnchorLocalPosition = anchor.LocalPosition;
            placed.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(anchor.Radius);
            string fallbackPrompt = !string.IsNullOrWhiteSpace(placed.CustomInteractionPromptText)
                ? placed.CustomInteractionPromptText.Trim()
                : new CampusPlacedObjectInteractionAnchor().PromptText;
            placed.CustomInteractionPromptText = string.IsNullOrWhiteSpace(anchor.PromptText) ? fallbackPrompt : anchor.PromptText.Trim();
        }

        public CampusPlacedObjectInteractionAnchor CreateDefaultCustomAnchor(int index, string selectedAnchorLabel)
        {
            int ordinal = Mathf.Max(1, index + 1);
            return new CampusPlacedObjectInteractionAnchor
            {
                AnchorId = "custom_" + ordinal,
                DisplayName = (selectedAnchorLabel ?? string.Empty) + " " + ordinal,
                Enabled = true,
                LocalPosition = Vector3.zero,
                Radius = CampusPlacedObject.DefaultInteractionAnchorRadius,
                Priority = 120,
                LogInteraction = true
            };
        }

        public void AddCustomInteractionAnchor(CampusPlacedObject placed, string selectedAnchorLabel)
        {
            if (placed == null)
            {
                return;
            }

            placed.CustomInteractionAnchors = placed.CustomInteractionAnchors ?? new List<CampusPlacedObjectInteractionAnchor>();
            placed.CustomInteractionAnchors.Add(CreateDefaultCustomAnchor(placed.CustomInteractionAnchors.Count, selectedAnchorLabel));
            SelectedCustomAnchorIndex = placed.CustomInteractionAnchors.Count - 1;
            placed.UseCustomInteractionAnchor = true;
            SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
        }

        public void RemoveSelectedCustomInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null || placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count == 0)
            {
                return;
            }

            EnsureSelectedCustomAnchorIndex(placed);
            placed.CustomInteractionAnchors.RemoveAt(SelectedCustomAnchorIndex);
            if (placed.CustomInteractionAnchors.Count == 0)
            {
                SelectedCustomAnchorIndex = 0;
                placed.UseCustomInteractionAnchor = false;
                placed.CustomInteractionAnchorLocalPosition = Vector3.zero;
                placed.CustomInteractionAnchorRadius = CampusPlacedObject.DefaultInteractionAnchorRadius;
                placed.CustomInteractionPromptText = new CampusPlacedObjectInteractionAnchor().PromptText;
            }
            else
            {
                SelectedCustomAnchorIndex = Mathf.Clamp(SelectedCustomAnchorIndex, 0, placed.CustomInteractionAnchors.Count - 1);
                SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
            }

            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
        }

        private static int ResolveInitialCustomAnchorIndex(CampusPlacedObject placed)
        {
            if (placed == null || placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < placed.CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor anchor = placed.CustomInteractionAnchors[i];
                if (anchor != null && anchor.Enabled)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
