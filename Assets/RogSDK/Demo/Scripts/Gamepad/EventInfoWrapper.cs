using RogPhoneSdk;
using UnityEngine;
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace RogPhoneSdkDemo
{
    public abstract class EventInfoWrapper
    {
        public GamepadEnum gamepad;
        public int deviceId;
        public InputControlEnum? inputControl;

#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
        protected InputDevice device;

        public bool IsRogKunaiGamepadKeyboard(GamepadEnum shownGamepad)
        {
            return device is RogKunaiGamepadKeyboard && shownGamepad == GamepadEnum.KUNAI_GAMEPAD;
        }

        public bool IsRogKunai3GamepadKeyboard(GamepadEnum shownGamepad)
        {
            return device is RogKunai3GamepadKeyboard && shownGamepad == GamepadEnum.KUNAI_3_GAMEPAD;
        }
#endif

    }

    public class KeyEventInfoWrapper : EventInfoWrapper
    {
        public string phase;
        public float buttonValue;

        public KeyEventInfoWrapper(KeyEventInfo info)
        {
            gamepad = info.gamepad;
            deviceId = info.deviceId;
            inputControl = info.inputControl;
            buttonValue = (info.phaseEnum == PhaseEnum.PRESS || info.phaseEnum == PhaseEnum.HOLD) ? 1f : 0f;
            phase = info.phaseEnum.ToString();
        }

#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
        private InputAction inputAction;

        public KeyEventInfoWrapper(InputAction.CallbackContext context)
        {
            gamepad = EventInfoUtils.GetGamepadEnum(context.control.device);
            deviceId = context.control.device.deviceId;
            inputAction = context.action;
            inputControl = EventInfoUtils.GetInputControlEnum(context.action.activeControl);
            device = context.control.device;
            buttonValue = ((ButtonControl)context.control).ReadValue();
            phase = context.phase.ToString();
        }
#endif

        override public string ToString()
        {
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
            return "gamepad = " + gamepad + " / deviceId = " + deviceId + " / inputAction = " + inputAction + " / phase=" + phase;
#else
            return "gamepad=" + gamepad + " / deviceId=" + deviceId + " / inputControl=" + inputControl.ToString() + " / phase=" + phase;
#endif

        }
    }

    public class MotionEventInfoWrapper : EventInfoWrapper
    {
        public Vector2 axes;

        public MotionEventInfoWrapper(MotionEventInfo info)
        {
            gamepad = info.gamepad;
            deviceId = info.deviceId;
            inputControl = info.inputControl;
            axes = info.axes;
        }

#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
        private InputAction inputAction;

        public MotionEventInfoWrapper(InputAction.CallbackContext context)
        {
            gamepad = EventInfoUtils.GetGamepadEnum(context.control.device);
            deviceId = context.control.device.deviceId;
            inputAction = context.action;
            inputControl = EventInfoUtils.GetInputControlEnum(context.action.activeControl);
            device = context.control.device;
            axes = new Vector2(context.ReadValue<Vector2>().x, context.ReadValue<Vector2>().y * -1);
        }
#endif

        override public string ToString()
        {
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
            return "gamepad = " + gamepad + " / deviceId = " + deviceId + " / inputAction = " + inputAction + " / axes = " + axes;
#else
            return "gamepad = " + gamepad + " / deviceId = " + deviceId + " / inputControl = " + inputControl.ToString() + " / axes = " + axes;
#endif
        }
    }
}