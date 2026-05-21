using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public readonly struct CampusCharacterActionContext
    {
        public CampusCharacterActionContext(
            CampusCharacterRuntime actor,
            string actionId,
            string payload,
            UnityEngine.Object target)
        {
            Actor = actor;
            ActionId = string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
            Payload = payload ?? string.Empty;
            Target = target;
        }

        public CampusCharacterRuntime Actor { get; }
        public string ActionId { get; }
        public string Payload { get; }
        public UnityEngine.Object Target { get; }
    }

    public static class CampusCharacterActionUtility
    {
        public static bool IdEquals(string actionId, string expected)
        {
            return string.Equals(
                NormalizeId(actionId),
                NormalizeId(expected),
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IdIsAny(string actionId, params string[] expectedIds)
        {
            if (expectedIds == null)
            {
                return false;
            }

            for (int i = 0; i < expectedIds.Length; i++)
            {
                if (IdEquals(actionId, expectedIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static StorageTransferResult Result(bool succeeded, string message)
        {
            return succeeded
                ? Success(message)
                : StorageTransferResult.Fail(message ?? string.Empty);
        }

        public static StorageTransferResult Success(string message = null)
        {
            return new StorageTransferResult(true, false, false, message ?? string.Empty, string.Empty);
        }

        public static bool TryResolveComponentTarget<T>(UnityEngine.Object target, out T component)
            where T : UnityEngine.Component
        {
            component = null;
            if (target is T direct)
            {
                component = direct;
                return true;
            }

            if (target is GameObject gameObject)
            {
                component = gameObject.GetComponent<T>();
                return component != null;
            }

            if (target is UnityEngine.Component sourceComponent)
            {
                component = sourceComponent.GetComponent<T>();
                return component != null;
            }

            return false;
        }

        public static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public interface ICampusCharacterActionProvider
    {
        string ProviderId { get; }
        bool TryExecute(CampusCharacterActionContext context, out StorageTransferResult result);
    }

    public static class CampusCharacterActionRegistry
    {
        private static readonly List<ICampusCharacterActionProvider> Providers =
            new List<ICampusCharacterActionProvider>();

        private static bool builtInsRegistered;

        public static void Register(ICampusCharacterActionProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            string providerId = provider.ProviderId ?? string.Empty;
            bool hasProviderId = !string.IsNullOrWhiteSpace(providerId);
            for (int i = 0; i < Providers.Count; i++)
            {
                ICampusCharacterActionProvider existing = Providers[i];
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

        public static void Unregister(ICampusCharacterActionProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            Providers.Remove(provider);
        }

        public static bool TryExecute(CampusCharacterActionContext context, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (context.Actor == null || string.IsNullOrWhiteSpace(context.ActionId))
            {
                return false;
            }

            EnsureBuiltInsRegistered();
            for (int i = 0; i < Providers.Count; i++)
            {
                ICampusCharacterActionProvider provider = Providers[i];
                if (provider != null && provider.TryExecute(context, out result))
                {
                    return result.Succeeded;
                }
            }

            result = StorageTransferResult.Fail(string.Empty);
            return false;
        }

        private static void EnsureBuiltInsRegistered()
        {
            if (builtInsRegistered)
            {
                return;
            }

            builtInsRegistered = true;
            CampusBuiltInCharacterActionProviders.Install();
        }
    }

    internal static class CampusBuiltInCharacterActionProviders
    {
        public static void Install()
        {
        }
    }
}
