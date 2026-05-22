using System.Reflection;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.UI
{
    internal readonly struct CampusGameplayPauseState
    {
        public CampusGameplayPauseState(bool autoAdvance)
        {
            AutoAdvance = autoAdvance;
        }

        public bool AutoAdvance { get; }
    }

    internal static class CampusGameplayPauseUtility
    {
        public static CampusGameplayPauseState Pause(CampusGameBootstrap bootstrap)
        {
            if (bootstrap == null || bootstrap.TimeController == null)
            {
                return new CampusGameplayPauseState(true);
            }

            CampusGameplayPauseState state = new CampusGameplayPauseState(
                bootstrap.TimeController.AutoAdvanceEnabled);

            bootstrap.TimeController.SetAutoAdvanceEnabled(false);
            ApplyPlayerGameplayInput(bootstrap, false);
            return state;
        }

        public static void Resume(CampusGameBootstrap bootstrap, CampusGameplayPauseState state)
        {
            if (bootstrap == null || bootstrap.TimeController == null)
            {
                return;
            }

            bootstrap.TimeController.SetAutoAdvanceEnabled(state.AutoAdvance);
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
    }
}
