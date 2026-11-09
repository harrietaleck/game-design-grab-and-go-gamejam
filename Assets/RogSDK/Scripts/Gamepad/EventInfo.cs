using UnityEngine;

namespace RogPhoneSdk
{
    public enum InputControlEnum
    {
        DPAD_UP,
        DPAD_DOWN,
        DPAD_LEFT,
        DPAD_RIGHT,
        LEFT_SHOULDER,
        RIGHT_SHOULDER,
        LEFT_TRIGGER,
        RIGHT_TRIGGER,
        BUTTON_SOUTH,
        BUTTON_EAST,
        BUTTON_NORTH,
        BUTTON_WEST,
        SELECT,
        PROFILE,
        FUNCTION,
        START,
        MODE,
        M1,
        M2,
        M3,
        M4,
        LEFT_STICK,
        RIGHT_STICK
    }

    public enum GamepadEnum
    {
        UNKNOWN = 0,
        KUNAI_GAMEPAD = 1,
        KUNAI_3_GAMEPAD = 2
    };

    public enum PhaseEnum
    {
        PRESS = 0,
        HOLD = 1,
        RELEASE = 2,
    }

    public abstract class EventInfo
    {
        public GamepadEnum gamepad;
        public int deviceId;
        public InputControlEnum? inputControl;
    }

    public class KeyEventInfo : EventInfo
    {
        public PhaseEnum phaseEnum;

        public KeyEventInfo(string infoString, InputControlEnum control)
        {
            string[] args = infoString.Split('/');
            gamepad = (GamepadEnum)int.Parse(args[0]);
            deviceId = int.Parse(args[1]);
            inputControl = control;
            phaseEnum = (PhaseEnum)int.Parse(args[2]);
        }
    }

    public class MotionEventInfo : EventInfo
    {
        public Vector2 axes;

        public MotionEventInfo(string infoString, InputControlEnum control)
        {
            string[] args = infoString.Split('/');
            gamepad = (GamepadEnum)int.Parse(args[0]);
            deviceId = int.Parse(args[1]);
            inputControl = control;
            axes = new Vector2(float.Parse(args[2]), float.Parse(args[3]));
        }
    }

}