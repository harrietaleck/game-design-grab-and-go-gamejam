using RogPhoneSdk;
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogPhoneSdkDemo
{
    public static class EventInfoUtils
    {
        public static string InputControlToString(GamepadEnum gamepad, InputControlEnum? action)
        {
            switch (action)
            {
                case InputControlEnum.DPAD_UP:
                    return "Dpad Up";
                case InputControlEnum.DPAD_DOWN:
                    return "Dpad Down";
                case InputControlEnum.DPAD_LEFT:
                    return "Dpad Left";
                case InputControlEnum.DPAD_RIGHT:
                    return "Dpad Right";
                case InputControlEnum.LEFT_SHOULDER:
                    return gamepad == GamepadEnum.KUNAI_GAMEPAD ? "LT1" : "LB";
                case InputControlEnum.RIGHT_SHOULDER:
                    return gamepad == GamepadEnum.KUNAI_GAMEPAD ? "RT1" : "RB";
                case InputControlEnum.LEFT_TRIGGER:
                    return gamepad == GamepadEnum.KUNAI_GAMEPAD ? "LT2" : "LT";
                case InputControlEnum.RIGHT_TRIGGER:
                    return gamepad == GamepadEnum.KUNAI_GAMEPAD ? "RT2" : "RT";
                case InputControlEnum.BUTTON_SOUTH:
                    return "Button A";
                case InputControlEnum.BUTTON_EAST:
                    return "Button B";
                case InputControlEnum.BUTTON_WEST:
                    return "Button X";
                case InputControlEnum.BUTTON_NORTH:
                    return "Button Y";
                case InputControlEnum.SELECT:
                    return "Select";
                case InputControlEnum.PROFILE:
                    return "Profile";
                case InputControlEnum.FUNCTION:
                    return "Function";
                case InputControlEnum.START:
                    return "Start";
                case InputControlEnum.MODE:
                    return "Mode";
                case InputControlEnum.M1:
                    return "M1";
                case InputControlEnum.M2:
                    return "M2";
                case InputControlEnum.M3:
                    return "M3";
                case InputControlEnum.M4:
                    return "M4";
                case InputControlEnum.LEFT_STICK:
                    return "LeftStick";
                case InputControlEnum.RIGHT_STICK:
                    return "RightStick";
                default:
                    return "Unknown";
            }
        }

#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
        public static GamepadEnum GetGamepadEnum(InputDevice device)
        {
            if (device is RogKunaiGamepadAndroid)
                return GamepadEnum.KUNAI_GAMEPAD;
            else if (device is RogKunai3GamepadAndroid)
                return GamepadEnum.KUNAI_3_GAMEPAD;
            else
                return GamepadEnum.UNKNOWN;
        }

        public static InputControlEnum? GetInputControlEnum(InputControl control)
        {
            if (control.device is UnityEngine.InputSystem.Gamepad)
            {
                UnityEngine.InputSystem.Gamepad gamepad = (UnityEngine.InputSystem.Gamepad)control.device;
                if (control == gamepad.leftShoulder) return InputControlEnum.LEFT_SHOULDER;
                if (control == gamepad.rightShoulder) return InputControlEnum.RIGHT_SHOULDER;
                if (control == gamepad.leftTrigger) return InputControlEnum.LEFT_TRIGGER;
                if (control == gamepad.rightTrigger) return InputControlEnum.RIGHT_TRIGGER;
                if (control == gamepad.dpad.up) return InputControlEnum.DPAD_UP;
                if (control == gamepad.dpad.down) return InputControlEnum.DPAD_DOWN;
                if (control == gamepad.dpad.left) return InputControlEnum.DPAD_LEFT;
                if (control == gamepad.dpad.right) return InputControlEnum.DPAD_RIGHT;
                if (control == gamepad.buttonSouth) return InputControlEnum.BUTTON_SOUTH;
                if (control == gamepad.buttonEast) return InputControlEnum.BUTTON_EAST;
                if (control == gamepad.buttonNorth) return InputControlEnum.BUTTON_NORTH;
                if (control == gamepad.buttonWest) return InputControlEnum.BUTTON_WEST;
                if (control == gamepad.leftStick || control == gamepad.leftStickButton) return InputControlEnum.LEFT_STICK;
                if (control == gamepad.rightStick || control == gamepad.rightStickButton) return InputControlEnum.RIGHT_STICK;
                if (control == gamepad.startButton) return InputControlEnum.START;
                if (control == gamepad.selectButton) return InputControlEnum.SELECT;
            }
            if (control.device is RogKunaiGamepad)
            {
                RogKunaiGamepad kunaiGamepad = (RogKunaiGamepad)control.device;
                if (control == kunaiGamepad.m1Button) return InputControlEnum.M1;
                if (control == kunaiGamepad.m2Button) return InputControlEnum.M2;
                if (control == kunaiGamepad.m3Button) return InputControlEnum.M3;
                if (control == kunaiGamepad.m4Button) return InputControlEnum.M4;
                if (control == kunaiGamepad.profileButton) return InputControlEnum.PROFILE;
                if (control == kunaiGamepad.functionButton) return InputControlEnum.FUNCTION;
            }
            if (control.device is RogKunai3Gamepad)
            {
                RogKunai3Gamepad kunai3Gamepad = (RogKunai3Gamepad)control.device;
                if (control == kunai3Gamepad.m1Button) return InputControlEnum.M1;
                if (control == kunai3Gamepad.m2Button) return InputControlEnum.M2;
                if (control == kunai3Gamepad.modeButton) return InputControlEnum.MODE;
            }
            if (control.device is RogKunaiGamepadKeyboard)
            {
                RogKunaiGamepadKeyboard kunaiGamepadKeyboard = (RogKunaiGamepadKeyboard)control.device;
                if (control == kunaiGamepadKeyboard.selectButton) return InputControlEnum.SELECT;
            }
            if (control.device is RogKunai3GamepadKeyboard)
            {
                RogKunai3GamepadKeyboard kunaiGamepad3Keyboard = (RogKunai3GamepadKeyboard)control.device;
                if (control == kunaiGamepad3Keyboard.m1Button) return InputControlEnum.M1;
                if (control == kunaiGamepad3Keyboard.m2Button) return InputControlEnum.M2;
            }
            return null;
        }
#endif
    }
}