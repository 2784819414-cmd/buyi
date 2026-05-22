using System;
using System.Collections.Generic;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    public static class CampusInteractionActionRegistry
    {
        private static readonly List<ICampusInteractionActionProvider> Providers =
            new List<ICampusInteractionActionProvider>();

        private static bool builtInsRegistered;

        public static void Register(ICampusInteractionActionProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            string providerId = provider.ProviderId ?? string.Empty;
            bool hasProviderId = !string.IsNullOrWhiteSpace(providerId);
            for (int i = 0; i < Providers.Count; i++)
            {
                ICampusInteractionActionProvider existing = Providers[i];
                if (ReferenceEquals(existing, provider) ||
                    hasProviderId &&
                    string.Equals(existing.ProviderId ?? string.Empty, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    Providers[i] = provider;
                    return;
                }
            }

            Providers.Add(provider);
        }

        public static void Unregister(ICampusInteractionActionProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            Providers.Remove(provider);
        }

        public static bool TryHandle(
            CampusSimpleInteractable sourceInteractable,
            CampusInteractionAnchor anchor,
            string actionId,
            string payload,
            GameObject actor,
            out string message)
        {
            message = string.Empty;
            if (sourceInteractable == null || string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            EnsureBuiltInsRegistered();
            CampusInteractionActionContext context = new CampusInteractionActionContext(
                sourceInteractable,
                anchor,
                actionId,
                payload,
                actor);
            return TryHandle(context, out message);
        }

        public static bool TryHandle(CampusGameplayActionRequest request, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(request.ActionId))
            {
                return false;
            }

            CampusInteractionActionContext context = new CampusInteractionActionContext(
                null,
                request.Anchor,
                request.ActionId,
                request.Payload,
                request.Actor,
                request.DirectTarget,
                ResolveSourceObject(request));
            return TryHandle(context, out message);
        }

        public static bool TryResolvePrompt(
            CampusSimpleInteractable sourceInteractable,
            CampusInteractionAnchor anchor,
            string actionId,
            string payload,
            GameObject actor,
            out string prompt)
        {
            prompt = string.Empty;
            if (sourceInteractable == null)
            {
                return false;
            }

            EnsureBuiltInsRegistered();
            CampusInteractionActionContext context = new CampusInteractionActionContext(
                sourceInteractable,
                anchor,
                actionId,
                payload,
                actor);

            for (int i = 0; i < Providers.Count; i++)
            {
                if (Providers[i] is ICampusInteractionPromptOverrideProvider promptProvider &&
                    promptProvider.TryResolvePrompt(context, out prompt) &&
                    !string.IsNullOrWhiteSpace(prompt))
                {
                    return true;
                }
            }

            prompt = string.Empty;
            return false;
        }

        private static bool TryHandle(CampusInteractionActionContext context, out string message)
        {
            message = string.Empty;
            EnsureBuiltInsRegistered();

            for (int i = 0; i < Providers.Count; i++)
            {
                ICampusInteractionActionProvider provider = Providers[i];
                if (provider != null && provider.TryHandle(context, out message))
                {
                    return true;
                }
            }

            message = string.Empty;
            return false;
        }

        private static CampusPlacedObject ResolveSourceObject(CampusGameplayActionRequest request)
        {
            if (request.DirectTarget != null &&
                request.DirectTarget.TryGetComponent(out CampusPlacedObject directPlacedObject))
            {
                return directPlacedObject;
            }

            if (request.DirectTarget != null)
            {
                CampusPlacedObject parentPlacedObject = request.DirectTarget.GetComponentInParent<CampusPlacedObject>();
                if (parentPlacedObject != null)
                {
                    return parentPlacedObject;
                }
            }

            return request.Anchor != null ? request.Anchor.GetComponentInParent<CampusPlacedObject>() : null;
        }

        private static CampusPlacedObject ResolveSourceObject(UnityEngine.Object target)
        {
            if (target is CampusPlacedObject placedObject)
            {
                return placedObject;
            }

            if (target is Component component)
            {
                return component.GetComponent<CampusPlacedObject>() ?? component.GetComponentInParent<CampusPlacedObject>();
            }

            if (target is GameObject gameObject)
            {
                return gameObject.GetComponent<CampusPlacedObject>() ?? gameObject.GetComponentInParent<CampusPlacedObject>();
            }

            return null;
        }

        private static void EnsureBuiltInsRegistered()
        {
            if (builtInsRegistered)
            {
                return;
            }

            builtInsRegistered = true;
            CampusBuiltInInteractionActionProviders.Install();
        }
    }
}
