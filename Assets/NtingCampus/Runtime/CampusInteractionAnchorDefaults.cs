using System;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public static class CampusInteractionAnchorDefaults
    {
        private const string AnchorRootName = "\u4ea4\u4e92\u951a\u70b9";
        private const string GenericAnchorName = "\u9ed8\u8ba4\u4ea4\u4e92";
        public static bool EnsureDefaultAnchors(CampusPlacedObject placedObject)
        {
            if (placedObject == null)
            {
                return false;
            }

            if (placedObject.UseCustomInteractionAnchor)
            {
                return false;
            }

            string displayName = ResolveDisplayName(placedObject);
            if (MatchesObject(placedObject, displayName, CampusObjectNames.DiningTable, CampusObjectNames.LegacyDiningTable))
            {
                placedObject.IsInteractable = true;
                EnsureDiningTableAnchors(placedObject);
                return true;
            }

            ResolveDefaultAnchorAction(
                placedObject,
                displayName,
                out MonoBehaviour target,
                out string actionId,
                out CampusLocalizedText promptText);
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            if (TryResolveConfiguredPrompt(placedObject, ref promptText, out string configuredPrompt))
            {
                promptText = placedObject.LocalizedCustomInteractionPromptText.HasAnyText
                    ? placedObject.LocalizedCustomInteractionPromptText
                    : promptText;
            }
            else
            {
                configuredPrompt = promptText.ResolvePrimary();
            }

            Vector3 fallbackPosition = ResolveColliderTopLocal(placedObject.transform, FindPrimaryCollider(placedObject), Vector3.zero);
            EnsureAnchor(
                placedObject,
                GenericAnchorName,
                fallbackPosition,
                0.65f,
                configuredPrompt,
                promptText,
                target,
                actionId,
                string.Empty,
                110,
                false,
                null,
                default);
            return true;
        }

        public static void RemoveDefaultAnchors(CampusPlacedObject placedObject)
        {
            if (placedObject == null)
            {
                return;
            }

            Transform root = placedObject.transform.Find(AnchorRootName);
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null ||
                    !IsDefaultAnchorName(child.name) ||
                    child.GetComponent<CampusInteractionAnchor>() == null)
                {
                    continue;
                }

                DestroyUnityObject(child.gameObject);
            }
        }

        private static void ResolveDefaultAnchorAction(
            CampusPlacedObject placedObject,
            string displayName,
            out MonoBehaviour target,
            out string actionId,
            out CampusLocalizedText promptText)
        {
            target = null;
            actionId = string.Empty;
            promptText = BuildLocalizedText(CampusInteractionTextId.InteractWith, displayName);
            if (placedObject == null)
            {
                return;
            }

            if (placedObject.IsStorageContainer)
            {
                actionId = CampusInteractionActionIds.OpenStorage;
                promptText = BuildLocalizedText(CampusInteractionTextId.OpenObject, displayName);
                return;
            }

            if (CampusPlacedObjectInteractionState.TryResolveFacilityDefaultAction(placedObject, out actionId))
            {
                return;
            }

            target = FindInteractionTarget(placedObject, null);
            actionId = target != null ? CampusInteractionActionIds.InteractTarget : string.Empty;
        }

        private static bool TryResolveConfiguredPrompt(
            CampusPlacedObject placedObject,
            ref CampusLocalizedText promptText,
            out string prompt)
        {
            prompt = string.Empty;
            if (placedObject == null)
            {
                return false;
            }

            if (placedObject.LocalizedCustomInteractionPromptText.HasAnyText)
            {
                prompt = placedObject.LocalizedCustomInteractionPromptText.ResolvePrimary(
                    placedObject.CustomInteractionPromptText,
                    promptText.ResolvePrimary());
                return true;
            }

            if (string.IsNullOrWhiteSpace(placedObject.CustomInteractionPromptText) ||
                string.Equals(
                    placedObject.CustomInteractionPromptText,
                    CampusPlacedObject.CustomInteractionPromptFallback,
                    StringComparison.Ordinal))
            {
                return false;
            }

            prompt = placedObject.CustomInteractionPromptText.Trim();
            promptText = new CampusLocalizedText(prompt, string.Empty);
            return true;
        }

        private static void EnsureDiningTableAnchors(CampusPlacedObject placedObject)
        {
            CampusLocalizedText seatPrompt = BuildLocalizedText(CampusInteractionTextId.SitDown);
            CampusLocalizedText logMessage = BuildLocalizedText(CampusInteractionTextId.SitDownObjectLog, CampusObjectNames.DiningTable);
            EnsureAnchor(placedObject, "\u5ea7\u4f4d_\u5de6_1", new Vector3(-1.35f, -1.2f, 0f), 0.42f, seatPrompt.ResolvePrimary(), seatPrompt, null, CampusInteractionActionIds.Log, string.Empty, 130, false, logMessage.ResolvePrimary(), logMessage);
            EnsureAnchor(placedObject, "\u5ea7\u4f4d_\u5de6_2", new Vector3(-1.35f, 0f, 0f), 0.42f, seatPrompt.ResolvePrimary(), seatPrompt, null, CampusInteractionActionIds.Log, string.Empty, 130, false, logMessage.ResolvePrimary(), logMessage);
            EnsureAnchor(placedObject, "\u5ea7\u4f4d_\u5de6_3", new Vector3(-1.35f, 1.2f, 0f), 0.42f, seatPrompt.ResolvePrimary(), seatPrompt, null, CampusInteractionActionIds.Log, string.Empty, 130, false, logMessage.ResolvePrimary(), logMessage);
            EnsureAnchor(placedObject, "\u5ea7\u4f4d_\u53f3_1", new Vector3(1.35f, -1.2f, 0f), 0.42f, seatPrompt.ResolvePrimary(), seatPrompt, null, CampusInteractionActionIds.Log, string.Empty, 130, false, logMessage.ResolvePrimary(), logMessage);
            EnsureAnchor(placedObject, "\u5ea7\u4f4d_\u53f3_2", new Vector3(1.35f, 0f, 0f), 0.42f, seatPrompt.ResolvePrimary(), seatPrompt, null, CampusInteractionActionIds.Log, string.Empty, 130, false, logMessage.ResolvePrimary(), logMessage);
            EnsureAnchor(placedObject, "\u5ea7\u4f4d_\u53f3_3", new Vector3(1.35f, 1.2f, 0f), 0.42f, seatPrompt.ResolvePrimary(), seatPrompt, null, CampusInteractionActionIds.Log, string.Empty, 130, false, logMessage.ResolvePrimary(), logMessage);
        }

        private static bool IsDefaultAnchorName(string name)
        {
            if (string.Equals(name, GenericAnchorName, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(name, "\u5ea7\u4f4d_\u5de6_1", StringComparison.Ordinal) ||
                   string.Equals(name, "\u5ea7\u4f4d_\u5de6_2", StringComparison.Ordinal) ||
                   string.Equals(name, "\u5ea7\u4f4d_\u5de6_3", StringComparison.Ordinal) ||
                   string.Equals(name, "\u5ea7\u4f4d_\u53f3_1", StringComparison.Ordinal) ||
                   string.Equals(name, "\u5ea7\u4f4d_\u53f3_2", StringComparison.Ordinal) ||
                   string.Equals(name, "\u5ea7\u4f4d_\u53f3_3", StringComparison.Ordinal);
        }

        private static CampusInteractionAnchor EnsureAnchor(
            CampusPlacedObject placedObject,
            string anchorName,
            Vector3 localPosition,
            float radius,
            string promptText,
            CampusLocalizedText localizedPromptText,
            MonoBehaviour target,
            string actionId,
            string payload,
            int priority,
            bool useDoorStatePrompt,
            string logMessage,
            CampusLocalizedText localizedLogMessage)
        {
            Transform root = EnsureAnchorRoot(placedObject);
            Transform anchorTransform = root.Find(anchorName);
            if (anchorTransform == null)
            {
                GameObject anchorObject = new GameObject(anchorName);
                anchorObject.layer = placedObject.gameObject.layer;
                anchorObject.transform.SetParent(root, false);
                anchorTransform = anchorObject.transform;
            }

            anchorTransform.gameObject.layer = placedObject.gameObject.layer;
            anchorTransform.localPosition = localPosition;
            anchorTransform.localRotation = Quaternion.identity;
            anchorTransform.localScale = Vector3.one;

            CircleCollider2D collider = anchorTransform.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = anchorTransform.gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.offset = Vector2.zero;
            collider.radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(radius);

            CampusInteractionAnchor anchor = anchorTransform.GetComponent<CampusInteractionAnchor>();
            if (anchor == null)
            {
                anchor = anchorTransform.gameObject.AddComponent<CampusInteractionAnchor>();
            }

            anchor.InteractionTarget = target;
            anchor.ActionId = CampusInteractionActionIds.Normalize(actionId);
            anchor.Payload = payload;
            anchor.PromptAnchor = anchorTransform;
            anchor.PromptText = promptText;
            anchor.LocalizedPromptText = localizedPromptText;
            anchor.KeyOverride = string.Empty;
            anchor.Icon = null;
            anchor.AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
            anchor.Priority = priority;
            anchor.IsAvailable = true;
            anchor.UnavailableText = string.Empty;
            anchor.HideWhenUnavailable = false;
            anchor.UseTargetDoorStatePrompt = useDoorStatePrompt;
            anchor.LogInteraction = CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.Log);
            anchor.InteractionLogMessage = logMessage;
            anchor.LocalizedInteractionLogMessage = localizedLogMessage;
            return anchor;
        }

        private static Transform EnsureAnchorRoot(CampusPlacedObject placedObject)
        {
            Transform root = placedObject.transform.Find(AnchorRootName);
            if (root != null)
            {
                return root;
            }

            GameObject rootObject = new GameObject(AnchorRootName);
            rootObject.layer = placedObject.gameObject.layer;
            rootObject.transform.SetParent(placedObject.transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private static string ResolveDisplayName(CampusPlacedObject placedObject)
        {
            return placedObject.GetDisplayName(CampusLanguageState.CurrentLanguage);
        }

        private static CampusLocalizedText BuildLocalizedText(CampusInteractionTextId id, params object[] args)
        {
            string chinese = args != null && args.Length > 0
                ? string.Format(CampusInteractionTextCatalog.Get(CampusDisplayLanguage.Chinese, id), args)
                : CampusInteractionTextCatalog.Get(CampusDisplayLanguage.Chinese, id);
            string english = args != null && args.Length > 0
                ? string.Format(CampusInteractionTextCatalog.Get(CampusDisplayLanguage.English, id), args)
                : CampusInteractionTextCatalog.Get(CampusDisplayLanguage.English, id);
            return new CampusLocalizedText(chinese, english);
        }

        private static bool MatchesObject(CampusPlacedObject placedObject, string displayName, params string[] names)
        {
            return CampusObjectNames.MatchesAny(displayName, names) ||
                   CampusObjectNames.MatchesAny(placedObject.ObjectId, names) ||
                   CampusObjectNames.MatchesAny(placedObject.gameObject.name, names);
        }

        private static MonoBehaviour FindInteractionTarget(CampusPlacedObject placedObject, Type preferredType)
        {
            MonoBehaviour fallback = null;
            MonoBehaviour[] behaviours = placedObject.GetComponentsInChildren<MonoBehaviour>(true);
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

                if (preferredType == null || preferredType.IsInstanceOfType(behaviour))
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

        private static Collider2D FindPrimaryCollider(CampusPlacedObject placedObject)
        {
            Collider2D fallback = null;
            Collider2D[] colliders = placedObject.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || collider.GetComponent<CampusInteractionAnchor>() != null)
                {
                    continue;
                }

                if (!collider.isTrigger)
                {
                    return collider;
                }

                if (fallback == null)
                {
                    fallback = collider;
                }
            }

            return fallback;
        }

        private static void RemoveAnchor(CampusPlacedObject placedObject, string anchorName)
        {
            Transform root = placedObject != null ? placedObject.transform.Find(AnchorRootName) : null;
            Transform anchor = root != null ? root.Find(anchorName) : null;
            if (anchor == null)
            {
                return;
            }

            DestroyUnityObject(anchor.gameObject);
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

        private static Vector3 ResolveColliderTopLocal(Transform owner, Collider2D collider, Vector3 fallback)
        {
            if (owner == null || collider == null)
            {
                return fallback;
            }

            Bounds bounds = collider.bounds;
            return owner.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        }
    }
}
