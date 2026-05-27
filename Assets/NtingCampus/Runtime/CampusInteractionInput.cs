using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace NtingCampusMapEditor
{
    public static class CampusInteractionInput
    {
        private static readonly KeyCode[] SupportedKeyboardKeys =
        {
            KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F, KeyCode.G, KeyCode.H,
            KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.P,
            KeyCode.Q, KeyCode.R, KeyCode.S, KeyCode.T, KeyCode.U, KeyCode.V, KeyCode.W, KeyCode.X,
            KeyCode.Y, KeyCode.Z,
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
            KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Escape, KeyCode.Tab,
            KeyCode.Backspace, KeyCode.Delete,
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt, KeyCode.RightAlt,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12
        };

        public static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryReadPressedFromInputSystem(keyCode, out bool inputSystemPressed))
            {
                return inputSystemPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

        public static bool IsKeyHeld(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryReadHeldFromInputSystem(keyCode, out bool inputSystemHeld))
            {
                return inputSystemHeld;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(keyCode);
#else
            return false;
#endif
        }

        public static bool WasMouseButtonPressed(int button)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryReadMouseButtonPressedFromInputSystem(button, out bool inputSystemPressed))
            {
                return inputSystemPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        public static bool TryReadPressedKeyboardKey(out KeyCode keyCode)
        {
            for (int i = 0; i < SupportedKeyboardKeys.Length; i++)
            {
                KeyCode candidate = SupportedKeyboardKeys[i];
                if (WasKeyPressed(candidate))
                {
                    keyCode = candidate;
                    return true;
                }
            }

            keyCode = KeyCode.None;
            return false;
        }

        public static string GetKeyLabel(KeyCode keyCode)
        {
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
            {
                return keyCode.ToString().ToUpperInvariant();
            }

            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
            {
                return ((int)keyCode - (int)KeyCode.Alpha0).ToString();
            }

            if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
            {
                return "Num " + ((int)keyCode - (int)KeyCode.Keypad0);
            }

            switch (keyCode)
            {
                case KeyCode.Space:
                    return "Space";
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    return "Enter";
                case KeyCode.Escape:
                    return "Esc";
                case KeyCode.Tab:
                    return "Tab";
                case KeyCode.Backspace:
                    return "Backspace";
                case KeyCode.Delete:
                    return "Delete";
                case KeyCode.F1:
                    return "F1";
                case KeyCode.F2:
                    return "F2";
                case KeyCode.F3:
                    return "F3";
                case KeyCode.F4:
                    return "F4";
                case KeyCode.F5:
                    return "F5";
                case KeyCode.F6:
                    return "F6";
                case KeyCode.F7:
                    return "F7";
                case KeyCode.F8:
                    return "F8";
                case KeyCode.F9:
                    return "F9";
                case KeyCode.F10:
                    return "F10";
                case KeyCode.F11:
                    return "F11";
                case KeyCode.F12:
                    return "F12";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "Alt";
                case KeyCode.LeftArrow:
                    return "Left";
                case KeyCode.RightArrow:
                    return "Right";
                case KeyCode.UpArrow:
                    return "Up";
                case KeyCode.DownArrow:
                    return "Down";
                default:
                    return keyCode.ToString();
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryReadMouseButtonPressedFromInputSystem(int button, out bool pressed)
        {
            pressed = false;
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            switch (button)
            {
                case 0:
                    pressed = mouse.leftButton.wasPressedThisFrame;
                    return true;
                case 1:
                    pressed = mouse.rightButton.wasPressedThisFrame;
                    return true;
                case 2:
                    pressed = mouse.middleButton.wasPressedThisFrame;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadPressedFromInputSystem(KeyCode keyCode, out bool pressed)
        {
            pressed = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            KeyControl key = GetKeyboardKey(keyboard, keyCode);
            if (key == null)
            {
                return false;
            }

            pressed = key.wasPressedThisFrame;
            return true;
        }

        private static bool TryReadHeldFromInputSystem(KeyCode keyCode, out bool held)
        {
            held = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            KeyControl key = GetKeyboardKey(keyboard, keyCode);
            if (key == null)
            {
                return false;
            }

            held = key.isPressed;
            return true;
        }

        private static KeyControl GetKeyboardKey(Keyboard keyboard, KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.B: return keyboard.bKey;
                case KeyCode.C: return keyboard.cKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.F: return keyboard.fKey;
                case KeyCode.G: return keyboard.gKey;
                case KeyCode.H: return keyboard.hKey;
                case KeyCode.I: return keyboard.iKey;
                case KeyCode.J: return keyboard.jKey;
                case KeyCode.K: return keyboard.kKey;
                case KeyCode.L: return keyboard.lKey;
                case KeyCode.M: return keyboard.mKey;
                case KeyCode.N: return keyboard.nKey;
                case KeyCode.O: return keyboard.oKey;
                case KeyCode.P: return keyboard.pKey;
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.T: return keyboard.tKey;
                case KeyCode.U: return keyboard.uKey;
                case KeyCode.V: return keyboard.vKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.X: return keyboard.xKey;
                case KeyCode.Y: return keyboard.yKey;
                case KeyCode.Z: return keyboard.zKey;
                case KeyCode.Alpha0: return keyboard.digit0Key;
                case KeyCode.Alpha1: return keyboard.digit1Key;
                case KeyCode.Alpha2: return keyboard.digit2Key;
                case KeyCode.Alpha3: return keyboard.digit3Key;
                case KeyCode.Alpha4: return keyboard.digit4Key;
                case KeyCode.Alpha5: return keyboard.digit5Key;
                case KeyCode.Alpha6: return keyboard.digit6Key;
                case KeyCode.Alpha7: return keyboard.digit7Key;
                case KeyCode.Alpha8: return keyboard.digit8Key;
                case KeyCode.Alpha9: return keyboard.digit9Key;
                case KeyCode.Keypad0: return keyboard.numpad0Key;
                case KeyCode.Keypad1: return keyboard.numpad1Key;
                case KeyCode.Keypad2: return keyboard.numpad2Key;
                case KeyCode.Keypad3: return keyboard.numpad3Key;
                case KeyCode.Keypad4: return keyboard.numpad4Key;
                case KeyCode.Keypad5: return keyboard.numpad5Key;
                case KeyCode.Keypad6: return keyboard.numpad6Key;
                case KeyCode.Keypad7: return keyboard.numpad7Key;
                case KeyCode.Keypad8: return keyboard.numpad8Key;
                case KeyCode.Keypad9: return keyboard.numpad9Key;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                case KeyCode.UpArrow: return keyboard.upArrowKey;
                case KeyCode.DownArrow: return keyboard.downArrowKey;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.Return: return keyboard.enterKey;
                case KeyCode.KeypadEnter: return keyboard.numpadEnterKey;
                case KeyCode.Escape: return keyboard.escapeKey;
                case KeyCode.Tab: return keyboard.tabKey;
                case KeyCode.Backspace: return keyboard.backspaceKey;
                case KeyCode.Delete: return keyboard.deleteKey;
                case KeyCode.F1: return keyboard.f1Key;
                case KeyCode.F2: return keyboard.f2Key;
                case KeyCode.F3: return keyboard.f3Key;
                case KeyCode.F4: return keyboard.f4Key;
                case KeyCode.F5: return keyboard.f5Key;
                case KeyCode.F6: return keyboard.f6Key;
                case KeyCode.F7: return keyboard.f7Key;
                case KeyCode.F8: return keyboard.f8Key;
                case KeyCode.F9: return keyboard.f9Key;
                case KeyCode.F10: return keyboard.f10Key;
                case KeyCode.F11: return keyboard.f11Key;
                case KeyCode.F12: return keyboard.f12Key;
                case KeyCode.LeftShift: return keyboard.leftShiftKey;
                case KeyCode.RightShift: return keyboard.rightShiftKey;
                case KeyCode.LeftControl: return keyboard.leftCtrlKey;
                case KeyCode.RightControl: return keyboard.rightCtrlKey;
                case KeyCode.LeftAlt: return keyboard.leftAltKey;
                case KeyCode.RightAlt: return keyboard.rightAltKey;
                default: return null;
            }
        }
#endif
    }
}
