using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusPlacedObjectInteractionState
    {
        private const string AnchorRootName = "\u4ea4\u4e92\u951a\u70b9";
        private const string LegacyCustomAnchorName = "\u73a9\u5bb6\u4ea4\u4e92";
        private const string CustomAnchorNamePrefix = "\u81ea\u5b9a\u4e49\u4ea4\u4e92_";

        public static void Apply(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            placed.NormalizeStorageSettings();
            NormalizeCustomAnchors(placed);
            if (UsesFacilityDefaultInteraction(placed))
            {
                placed.IsInteractable = true;
            }

            if (!CanEditSceneHierarchy(placed))
            {
                return;
            }

            bool hasAuthoredPreset = HasInteractionPreset(placed);
            if (hasAuthoredPreset)
            {
                CampusInteractionAnchorDefaults.RemoveDefaultAnchors(placed);
            }
            else if (!CampusInteractionAnchorDefaults.EnsureDefaultAnchors(placed))
            {
                CampusInteractionAnchorDefaults.RemoveDefaultAnchors(placed);
            }

            ApplyUnifiedInteractionHandlerState(placed);
            ApplyCustomInteractionAnchors(placed);
        }

        public static void ApplyCustomInteractionAnchors(CampusPlacedObject placed)
        {
            if (placed == null || !CanEditSceneHierarchy(placed))
            {
                return;
            }

            NormalizeCustomAnchors(placed);
            List<CampusPlacedObjectInteractionAnchor> anchors = ResolveAuthoredInteractionAnchors(
                placed,
                out bool hasAuthoredPreset);
            Transform root = placed.transform.Find(AnchorRootName);
            if (!hasAuthoredPreset && !placed.UseCustomInteractionAnchor)
            {
                RemoveStaleCustomInteractionAnchors(root, null);
                return;
            }

            if (anchors.Count == 0)
            {
                placed.IsInteractable = false;
                if (hasAuthoredPreset)
                {
                    RemoveStaleAuthoredInteractionAnchors(root, new HashSet<string>());
                }
                else
                {
                    RemoveStaleCustomInteractionAnchors(root, null);
                }

                return;
            }

            placed.IsInteractable = true;
            root = root != null ? root : EnsureAnchorRoot(placed);
            HashSet<string> expectedNames = new HashSet<string>();
            for (int i = 0; i < anchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor data = anchors[i];
                if (data == null || !data.Enabled)
                {
                    continue;
                }

                string anchorName = BuildAnchorName(data, i);
                expectedNames.Add(anchorName);
                ConfigureAnchor(placed, root, anchorName, data);
            }

            if (hasAuthoredPreset)
            {
                RemoveStaleAuthoredInteractionAnchors(root, expectedNames);
            }
            else
            {
                RemoveStaleCustomInteractionAnchors(root, expectedNames);
            }
        }

        public static void NormalizeCustomAnchors(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            if (placed.CustomInteractionAnchors == null)
            {
                placed.CustomInteractionAnchors = new List<CampusPlacedObjectInteractionAnchor>();
            }

            if (placed.UseCustomInteractionAnchor && placed.CustomInteractionAnchors.Count == 0)
            {
                placed.CustomInteractionAnchors.Add(new CampusPlacedObjectInteractionAnchor
                {
                    AnchorId = "custom_1",
                    DisplayName = LegacyCustomAnchorName,
                    Enabled = true,
                    LocalPosition = placed.CustomInteractionAnchorLocalPosition,
                    Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(placed.CustomInteractionAnchorRadius),
                    PromptText = ResolveCustomPromptText(placed),
                    LocalizedPromptText = placed.LocalizedCustomInteractionPromptText,
                    Priority = 120,
                    LogInteraction = true
                });
            }

            if (placed.UseCustomInteractionAnchor && ShouldSyncLegacyPrimaryAnchorFields(placed))
            {
                CampusPlacedObjectInteractionAnchor editablePrimary = GetFirstEnabledCustomAnchor(placed);
                if (editablePrimary == null && placed.CustomInteractionAnchors.Count > 0)
                {
                    editablePrimary = placed.CustomInteractionAnchors[0];
                }

                if (editablePrimary != null)
                {
                    editablePrimary.LocalPosition = placed.CustomInteractionAnchorLocalPosition;
                    editablePrimary.Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(
                        placed.CustomInteractionAnchorRadius);
                    editablePrimary.PromptText = ResolveCustomPromptText(placed);
                    editablePrimary.LocalizedPromptText = placed.LocalizedCustomInteractionPromptText;
                }
            }

            for (int i = 0; i < placed.CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor data = placed.CustomInteractionAnchors[i];
                if (data == null)
                {
                    data = new CampusPlacedObjectInteractionAnchor();
                    placed.CustomInteractionAnchors[i] = data;
                }

                if (string.IsNullOrWhiteSpace(data.AnchorId))
                {
                    data.AnchorId = "custom_" + (i + 1);
                }

                if (string.IsNullOrWhiteSpace(data.DisplayName))
                {
                    data.DisplayName = LegacyCustomAnchorName + " " + (i + 1);
                }

                if (string.IsNullOrWhiteSpace(data.PromptText))
                {
                    data.PromptText = CampusInteractionTextCatalog.Get(CampusInteractionTextId.Interact);
                }

                data.Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(data.Radius);
                data.Priority = Mathf.Max(0, data.Priority);
            }

            CampusPlacedObjectInteractionAnchor primary = GetFirstEnabledCustomAnchor(placed);
            if (primary != null)
            {
                placed.CustomInteractionAnchorLocalPosition = primary.LocalPosition;
                placed.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(primary.Radius);
                placed.CustomInteractionPromptText = string.IsNullOrWhiteSpace(primary.PromptText)
                    ? CampusInteractionTextCatalog.Get(CampusInteractionTextId.Interact)
                    : primary.PromptText;
                placed.LocalizedCustomInteractionPromptText = primary.LocalizedPromptText;
            }
        }

        public static CampusPlacedObjectInteractionAnchor GetFirstEnabledCustomAnchor(CampusPlacedObject placed)
        {
            if (placed == null || placed.CustomInteractionAnchors == null)
            {
                return null;
            }

            for (int i = 0; i < placed.CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor data = placed.CustomInteractionAnchors[i];
                if (data != null && data.Enabled)
                {
                    return data;
                }
            }

            return placed.CustomInteractionAnchors.Count > 0 ? placed.CustomInteractionAnchors[0] : null;
        }

        public static bool HasInteractionPresetAnchors(CampusPlacedObject placed)
        {
            return CampusObjectInteractionPresetCatalog.Current.TryResolvePreset(
                       placed,
                       out CampusObjectInteractionPreset preset) &&
                   preset != null &&
                   preset.Anchors != null &&
                   preset.Anchors.Count > 0;
        }

        public static bool UsesFacilityDefaultInteraction(CampusPlacedObject placed)
        {
            if (TryResolveServiceStationDefaultAction(placed, out _))
            {
                return true;
            }

            switch (CampusFacilityTypeResolver.Resolve(placed))
            {
                case CampusFacilityType.ServiceWindow:
                    return true;
                default:
                    return false;
            }
        }

        private static void ApplyUnifiedInteractionHandlerState(CampusPlacedObject placed)
        {
            bool shouldUseHandler = placed.IsInteractable ||
                                    placed.UseCustomInteractionAnchor ||
                                    HasInteractionPresetAnchors(placed) ||
                                    placed.IsStorageContainer ||
                                    UsesFacilityDefaultInteraction(placed);
            CampusSimpleInteractable handler = placed.GetComponent<CampusSimpleInteractable>();
            if (!shouldUseHandler)
            {
                if (handler != null)
                {
                    DestroyUnityObject(handler);
                }

                return;
            }

            if (handler == null)
            {
                handler = placed.gameObject.AddComponent<CampusSimpleInteractable>();
            }

            handler.IsAvailable = true;
            handler.HideWhenUnavailable = false;
            handler.DefaultActionId = ResolveDefaultActionId(placed);
            if (placed.IsStorageContainer)
            {
                handler.DefaultActionId = CampusInteractionActionIds.OpenStorage;

                if (string.IsNullOrWhiteSpace(handler.PromptText) ||
                    handler.PromptText == CampusPlacedObject.CustomInteractionPromptFallback)
                {
                    handler.PromptText = CampusInteractionTextCatalog.Format(
                        CampusInteractionTextId.OpenObject,
                        placed.DisplayName);
                }
            }
        }

        public static bool TryResolveFacilityDefaultAction(CampusPlacedObject placed, out string actionId)
        {
            if (TryResolveServiceStationDefaultAction(placed, out actionId))
            {
                return true;
            }

            switch (CampusFacilityTypeResolver.Resolve(placed))
            {
                case CampusFacilityType.ServiceWindow:
                    actionId = CampusInteractionActionIds.ServiceWindowUse;
                    return true;
                default:
                    actionId = string.Empty;
                    return false;
            }
        }

        private static string ResolveDefaultActionId(CampusPlacedObject placed)
        {
            if (TryResolveAuthoredPresetDefaultAction(placed, out string presetActionId))
            {
                return presetActionId;
            }

            return TryResolveFacilityDefaultAction(placed, out string actionId)
                ? actionId
                : string.Empty;
        }

        private static bool TryResolveAuthoredPresetDefaultAction(CampusPlacedObject placed, out string actionId)
        {
            actionId = string.Empty;
            if (!CampusObjectInteractionPresetCatalog.Current.TryResolvePreset(
                    placed,
                    out CampusObjectInteractionPreset preset) ||
                preset == null ||
                preset.Anchors == null)
            {
                return false;
            }

            for (int i = 0; i < preset.Anchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor anchor = preset.Anchors[i];
                if (anchor == null || !anchor.Enabled || string.IsNullOrWhiteSpace(anchor.ActionId))
                {
                    continue;
                }

                actionId = CampusInteractionActionIds.Normalize(anchor.ActionId);
                return !string.IsNullOrEmpty(actionId);
            }

            return false;
        }

        private static bool TryResolveServiceStationDefaultAction(CampusPlacedObject placed, out string actionId)
        {
            actionId = string.Empty;
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            if (worldService == null ||
                !worldService.ServiceStations.TryResolveByPlacedObject(
                    worldService,
                    placed,
                    out CampusServiceStation station) ||
                string.IsNullOrWhiteSpace(station.InteractionActionId))
            {
                return false;
            }

            actionId = CampusInteractionActionIds.Normalize(station.InteractionActionId);
            return !string.IsNullOrEmpty(actionId);
        }

        private static List<CampusPlacedObjectInteractionAnchor> ResolveAuthoredInteractionAnchors(
            CampusPlacedObject placed,
            out bool hasAuthoredPreset)
        {
            hasAuthoredPreset = CampusObjectInteractionPresetCatalog.Current.TryResolvePreset(
                placed,
                out CampusObjectInteractionPreset preset);
            return hasAuthoredPreset
                ? CampusObjectInteractionPresetCatalog.ClonePresetAnchors(preset)
                : CampusPlacedObject.CloneInteractionAnchors(placed.CustomInteractionAnchors);
        }

        private static void ConfigureAnchor(
            CampusPlacedObject placed,
            Transform root,
            string anchorName,
            CampusPlacedObjectInteractionAnchor data)
        {
            Transform anchorTransform = root.Find(anchorName);
            if (anchorTransform == null)
            {
                GameObject anchorObject = new GameObject(anchorName);
                anchorObject.layer = placed.gameObject.layer;
                anchorObject.transform.SetParent(root, false);
                anchorTransform = anchorObject.transform;
            }

            anchorTransform.gameObject.layer = placed.gameObject.layer;
            anchorTransform.localPosition = placed.ResolveInteractionAnchorLocalPosition(data.LocalPosition);
            anchorTransform.localRotation = Quaternion.identity;
            anchorTransform.localScale = Vector3.one;

            CircleCollider2D collider = anchorTransform.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = anchorTransform.gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.offset = Vector2.zero;
            collider.radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(data.Radius);

            CampusInteractionAnchor anchor = anchorTransform.GetComponent<CampusInteractionAnchor>();
            if (anchor == null)
            {
                anchor = anchorTransform.gameObject.AddComponent<CampusInteractionAnchor>();
            }

            string prompt = data.LocalizedPromptText.HasAnyText
                ? data.LocalizedPromptText.Current(data.PromptText)
                : ResolvePromptText(data.PromptText);
            MonoBehaviour target = ResolveInteractionTarget(placed, data.TargetComponentType);
            string actionId = ResolveActionId(data, target);
            anchor.InteractionTarget = target;
            anchor.ActionId = actionId;
            anchor.Payload = data.Payload;
            anchor.PromptAnchor = anchorTransform;
            anchor.PromptText = prompt;
            anchor.LocalizedPromptText = data.LocalizedPromptText;
            anchor.KeyOverride = string.Empty;
            anchor.Icon = null;
            anchor.AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
            anchor.Priority = Mathf.Max(0, data.Priority);
            anchor.IsAvailable = true;
            anchor.UnavailableText = string.Empty;
            anchor.HideWhenUnavailable = false;
            anchor.UseTargetDoorStatePrompt = data.UseTargetDoorStatePrompt;
            anchor.LogInteraction = data.LogInteraction &&
                                    CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.Log);
            anchor.InteractionLogMessage = prompt + " " + placed.DisplayName;
            anchor.LocalizedInteractionLogMessage = data.LocalizedInteractionLogMessage;
            if (!string.IsNullOrWhiteSpace(data.InteractionLogMessage))
            {
                anchor.InteractionLogMessage = data.InteractionLogMessage.Trim();
            }
        }

        private static MonoBehaviour ResolveInteractionTarget(CampusPlacedObject placed, string targetComponentType)
        {
            MonoBehaviour fallback = null;
            MonoBehaviour[] behaviours = placed.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null ||
                    behaviour is CampusInteractionAnchor ||
                    behaviour is CampusSimpleInteractable ||
                    !(behaviour is ICampusInteractable))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(targetComponentType))
                {
                    return behaviour;
                }

                Type type = behaviour.GetType();
                if (type.Name == targetComponentType || type.FullName == targetComponentType)
                {
                    return behaviour;
                }

                if (fallback == null)
                {
                    fallback = behaviour;
                }
            }

            return fallback;
        }

        private static string ResolveActionId(CampusPlacedObjectInteractionAnchor data, MonoBehaviour target)
        {
            if (data != null && !string.IsNullOrWhiteSpace(data.ActionId))
            {
                return CampusInteractionActionIds.Normalize(data.ActionId);
            }

            return target != null ? CampusInteractionActionIds.InteractTarget : string.Empty;
        }

        private static Transform EnsureAnchorRoot(CampusPlacedObject placed)
        {
            GameObject rootObject = new GameObject(AnchorRootName);
            rootObject.layer = placed.gameObject.layer;
            rootObject.transform.SetParent(placed.transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private static void RemoveStaleCustomInteractionAnchors(Transform root, HashSet<string> expectedNames)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                bool isCustomAnchor = child.name.StartsWith(CustomAnchorNamePrefix, StringComparison.Ordinal) ||
                                      child.name == LegacyCustomAnchorName;
                if (!isCustomAnchor)
                {
                    continue;
                }

                if (expectedNames != null && expectedNames.Contains(child.name))
                {
                    continue;
                }

                if (child.GetComponent<CampusInteractionAnchor>() != null)
                {
                    DestroyUnityObject(child.gameObject);
                }
            }
        }

        private static void RemoveStaleAuthoredInteractionAnchors(Transform root, HashSet<string> expectedNames)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null ||
                    child.GetComponent<CampusInteractionAnchor>() == null ||
                    expectedNames != null && expectedNames.Contains(child.name))
                {
                    continue;
                }

                DestroyUnityObject(child.gameObject);
            }
        }

        private static bool HasInteractionPreset(CampusPlacedObject placed)
        {
            return CampusObjectInteractionPresetCatalog.Current.TryResolvePreset(placed, out _);
        }

        private static bool ShouldSyncLegacyPrimaryAnchorFields(CampusPlacedObject placed)
        {
            return placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count <= 1;
        }

        private static string ResolveCustomPromptText(CampusPlacedObject placed)
        {
            if (placed.LocalizedCustomInteractionPromptText.HasAnyText)
            {
                return placed.LocalizedCustomInteractionPromptText.Current(placed.CustomInteractionPromptText);
            }

            return ResolvePromptText(placed.CustomInteractionPromptText);
        }

        private static string ResolvePromptText(string promptText)
        {
            return string.IsNullOrWhiteSpace(promptText) ||
                   promptText.Trim() == CampusPlacedObject.CustomInteractionPromptFallback
                ? CampusInteractionTextCatalog.Get(CampusInteractionTextId.Interact)
                : promptText.Trim();
        }

        private static string BuildAnchorName(CampusPlacedObjectInteractionAnchor data, int index)
        {
            string id = data != null && !string.IsNullOrWhiteSpace(data.AnchorId)
                ? data.AnchorId.Trim()
                : "custom_" + (index + 1);
            return CustomAnchorNamePrefix + SanitizeAnchorName(id);
        }

        private static string SanitizeAnchorName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "custom";
            }

            string sanitized = value.Trim().Replace('/', '_').Replace('\\', '_');
            return string.IsNullOrWhiteSpace(sanitized) ? "custom" : sanitized;
        }

        private static bool CanEditSceneHierarchy(CampusPlacedObject placed)
        {
            return placed != null && placed.gameObject.scene.IsValid();
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
