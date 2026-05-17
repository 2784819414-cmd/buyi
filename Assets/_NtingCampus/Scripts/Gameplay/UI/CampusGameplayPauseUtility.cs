using System.Reflection;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.UI
{
    internal readonly struct CampusGameplayPauseState
    {
        public CampusGameplayPauseState(bool autoAdvance, CampusTimeSpeedMode speedMode)
        {
            AutoAdvance = autoAdvance;
            SpeedMode = speedMode;
        }

        public bool AutoAdvance { get; }
        public CampusTimeSpeedMode SpeedMode { get; }
    }

    internal static class CampusGameplayPauseUtility
    {
        public static CampusGameplayPauseState Pause(CampusGameBootstrap bootstrap)
        {
            if (bootstrap == null || bootstrap.TimeController == null)
            {
                return new CampusGameplayPauseState(true, CampusTimeSpeedMode.Normal);
            }

            CampusGameplayPauseState state = new CampusGameplayPauseState(
                GetPrivateBool(bootstrap.TimeController, "autoAdvance", true),
                bootstrap.TimeController.SpeedMode);

            SetPrivateBool(bootstrap.TimeController, "autoAdvance", false);
            bootstrap.TimeController.SetSpeedMode(CampusTimeSpeedMode.Paused);
            ApplyPlayerGameplayInput(bootstrap, false);
            return state;
        }

        public static void Resume(CampusGameBootstrap bootstrap, CampusGameplayPauseState state)
        {
            if (bootstrap == null || bootstrap.TimeController == null)
            {
                return;
            }

            SetPrivateBool(bootstrap.TimeController, "autoAdvance", state.AutoAdvance);
            bootstrap.TimeController.SetSpeedMode(state.SpeedMode);
            ApplyPlayerGameplayInput(bootstrap, true);
        }

        private static void ApplyPlayerGameplayInput(CampusGameBootstrap bootstrap, bool enabled)
        {
            CampusCharacterRuntime playerRuntime = bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
            if (playerRuntime == null)
            {
                return;
            }

            MonoBehaviour[] components = playerRuntime.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                {
                    continue;
                }

                MethodInfo method = component.GetType().GetMethod(
                    "SetGameplayInputEnabled",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(bool) },
                    null);
                if (method != null)
                {
                    method.Invoke(component, new object[] { enabled });
                }
            }
        }

        private static bool GetPrivateBool(object target, string fieldName, bool fallback)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? (bool)field.GetValue(target) : fallback;
        }

        private static void SetPrivateBool(object target, string fieldName, bool value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}
