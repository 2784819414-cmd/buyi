using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public enum CampusGameplayInputActionId
    {
        MoveUpPrimary = 0,
        MoveDownPrimary = 1,
        MoveLeftPrimary = 2,
        MoveRightPrimary = 3,
        MoveUpSecondary = 4,
        MoveDownSecondary = 5,
        MoveLeftSecondary = 6,
        MoveRightSecondary = 7,
        Interact = 8,
        Sprint = 9,
        Backpack = 10,
        Settings = 11,
        ToggleMode = 12,
        TimePause = 13,
        TimeNormalSpeed = 14,
        TimeFastSpeed = 15,
        TimeMaxSpeed = 16
    }

    public static class CampusGameplayInputBindings
    {
        private const string PlayerPrefsPrefix = "NtingCampus.Gameplay.Input.";

        private static readonly CampusGameplayInputActionId[] OrderedActions =
        {
            CampusGameplayInputActionId.MoveUpPrimary,
            CampusGameplayInputActionId.MoveDownPrimary,
            CampusGameplayInputActionId.MoveLeftPrimary,
            CampusGameplayInputActionId.MoveRightPrimary,
            CampusGameplayInputActionId.MoveUpSecondary,
            CampusGameplayInputActionId.MoveDownSecondary,
            CampusGameplayInputActionId.MoveLeftSecondary,
            CampusGameplayInputActionId.MoveRightSecondary,
            CampusGameplayInputActionId.Interact,
            CampusGameplayInputActionId.Sprint,
            CampusGameplayInputActionId.Backpack,
            CampusGameplayInputActionId.Settings,
            CampusGameplayInputActionId.ToggleMode,
            CampusGameplayInputActionId.TimePause,
            CampusGameplayInputActionId.TimeNormalSpeed,
            CampusGameplayInputActionId.TimeFastSpeed,
            CampusGameplayInputActionId.TimeMaxSpeed
        };

        public static event Action BindingsChanged;

        public static IReadOnlyList<CampusGameplayInputActionId> RebindableActions => OrderedActions;

        public static Vector2 ReadMoveInput()
        {
            float x = 0f;
            float y = 0f;

            if (IsHeld(CampusGameplayInputActionId.MoveLeftPrimary) || IsHeld(CampusGameplayInputActionId.MoveLeftSecondary))
            {
                x -= 1f;
            }

            if (IsHeld(CampusGameplayInputActionId.MoveRightPrimary) || IsHeld(CampusGameplayInputActionId.MoveRightSecondary))
            {
                x += 1f;
            }

            if (IsHeld(CampusGameplayInputActionId.MoveDownPrimary) || IsHeld(CampusGameplayInputActionId.MoveDownSecondary))
            {
                y -= 1f;
            }

            if (IsHeld(CampusGameplayInputActionId.MoveUpPrimary) || IsHeld(CampusGameplayInputActionId.MoveUpSecondary))
            {
                y += 1f;
            }

            return new Vector2(x, y);
        }

        public static bool WasPressed(CampusGameplayInputActionId actionId)
        {
            return CampusInteractionInput.WasKeyPressed(GetBinding(actionId));
        }

        public static bool IsHeld(CampusGameplayInputActionId actionId)
        {
            return CampusInteractionInput.IsKeyHeld(GetBinding(actionId));
        }

        public static KeyCode GetBinding(CampusGameplayInputActionId actionId)
        {
            KeyCode defaultKey = GetDefaultBinding(actionId);
            string key = BuildPlayerPrefsKey(actionId);
            if (!PlayerPrefs.HasKey(key))
            {
                return defaultKey;
            }

            int storedValue = PlayerPrefs.GetInt(key, (int)defaultKey);
            if (!Enum.IsDefined(typeof(KeyCode), storedValue))
            {
                return defaultKey;
            }

            KeyCode storedKey = (KeyCode)storedValue;
            return storedKey == KeyCode.None ? defaultKey : storedKey;
        }

        public static KeyCode GetDefaultBinding(CampusGameplayInputActionId actionId)
        {
            switch (actionId)
            {
                case CampusGameplayInputActionId.MoveUpPrimary: return KeyCode.W;
                case CampusGameplayInputActionId.MoveDownPrimary: return KeyCode.S;
                case CampusGameplayInputActionId.MoveLeftPrimary: return KeyCode.A;
                case CampusGameplayInputActionId.MoveRightPrimary: return KeyCode.D;
                case CampusGameplayInputActionId.MoveUpSecondary: return KeyCode.UpArrow;
                case CampusGameplayInputActionId.MoveDownSecondary: return KeyCode.DownArrow;
                case CampusGameplayInputActionId.MoveLeftSecondary: return KeyCode.LeftArrow;
                case CampusGameplayInputActionId.MoveRightSecondary: return KeyCode.RightArrow;
                case CampusGameplayInputActionId.Interact: return KeyCode.E;
                case CampusGameplayInputActionId.Sprint: return KeyCode.LeftShift;
                case CampusGameplayInputActionId.Backpack: return KeyCode.B;
                case CampusGameplayInputActionId.Settings: return KeyCode.Escape;
                case CampusGameplayInputActionId.ToggleMode: return KeyCode.Tab;
                case CampusGameplayInputActionId.TimePause: return KeyCode.Alpha1;
                case CampusGameplayInputActionId.TimeNormalSpeed: return KeyCode.Alpha2;
                case CampusGameplayInputActionId.TimeFastSpeed: return KeyCode.Alpha3;
                case CampusGameplayInputActionId.TimeMaxSpeed: return KeyCode.Alpha4;
                default: return KeyCode.None;
            }
        }

        public static bool TrySetBinding(
            CampusGameplayInputActionId actionId,
            KeyCode keyCode,
            out CampusGameplayInputActionId conflictingAction)
        {
            conflictingAction = default;
            if (keyCode == KeyCode.None)
            {
                return false;
            }

            if (TryFindConflict(actionId, keyCode, out conflictingAction))
            {
                return false;
            }

            PlayerPrefs.SetInt(BuildPlayerPrefsKey(actionId), (int)keyCode);
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
            return true;
        }

        public static void ResetBinding(CampusGameplayInputActionId actionId)
        {
            PlayerPrefs.DeleteKey(BuildPlayerPrefsKey(actionId));
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        public static void ResetAll()
        {
            for (int i = 0; i < OrderedActions.Length; i++)
            {
                PlayerPrefs.DeleteKey(BuildPlayerPrefsKey(OrderedActions[i]));
            }

            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        public static string GetBindingLabel(CampusGameplayInputActionId actionId)
        {
            return CampusInteractionInput.GetKeyLabel(GetBinding(actionId));
        }

        public static bool TryFindConflict(
            CampusGameplayInputActionId sourceAction,
            KeyCode keyCode,
            out CampusGameplayInputActionId conflictingAction)
        {
            for (int i = 0; i < OrderedActions.Length; i++)
            {
                CampusGameplayInputActionId candidate = OrderedActions[i];
                if (candidate == sourceAction)
                {
                    continue;
                }

                if (GetBinding(candidate) == keyCode)
                {
                    conflictingAction = candidate;
                    return true;
                }
            }

            conflictingAction = default;
            return false;
        }

        private static string BuildPlayerPrefsKey(CampusGameplayInputActionId actionId)
        {
            return PlayerPrefsPrefix + actionId;
        }
    }
}
