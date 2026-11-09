using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace RogPhoneSdk
{
    [Serializable] public class KeyEvent : UnityEvent<KeyEventInfo> { }

    [Serializable] public class MotionEvent : UnityEvent<MotionEventInfo> { }

    [Serializable] public class ConnectionEvent : UnityEvent<DeviceInfo> { }


    public class Gamepad : MonoBehaviour
    {
        private static bool sInitialized = false;

        public bool hasPressDpadUp = false;
        public bool hasHoldDpadUp = false;
        public bool hasReleaseDpadUp = false;
        public KeyEvent onPressDpadUp;
        public KeyEvent onHoldDpadUp;
        public KeyEvent onReleaseDpadUp;

        public bool hasPressDpadDown = false;
        public bool hasHoldDpadDown = false;
        public bool hasReleaseDpadDown = false;
        public KeyEvent onPressDpadDown;
        public KeyEvent onHoldDpadDown;
        public KeyEvent onReleaseDpadDown;

        public bool hasPressDpadLeft = false;
        public bool hasHoldDpadLeft = false;
        public bool hasReleaseDpadLeft = false;
        public KeyEvent onPressDpadLeft;
        public KeyEvent onHoldDpadLeft;
        public KeyEvent onReleaseDpadLeft;

        public bool hasPressDpadRight = false;
        public bool hasHoldDpadRight = false;
        public bool hasReleaseDpadRight = false;
        public KeyEvent onPressDpadRight;
        public KeyEvent onHoldDpadRight;
        public KeyEvent onReleaseDpadRight;

        // Trigger
        public bool hasPressLeftShoulder = false;
        public bool hasHoldLeftShoulder = false;
        public bool hasReleaseLeftShoulder = false;
        public KeyEvent onPressLeftShoulder;
        public KeyEvent onHoldLeftShoulder;
        public KeyEvent onReleaseLeftShoulder;

        public bool hasPressRightShoulder = false;
        public bool hasHoldRightShoulder = false;
        public bool hasReleaseRightShoulder = false;
        public KeyEvent onPressRightShoulder;
        public KeyEvent onHoldRightShoulder;
        public KeyEvent onReleaseRightShoulder;

        public bool hasPressLeftTrigger = false;
        public bool hasHoldLeftTrigger = false;
        public bool hasReleaseLeftTrigger = false;
        public KeyEvent onPressLeftTrigger;
        public KeyEvent onHoldLeftTrigger;
        public KeyEvent onReleaseLeftTrigger;

        public bool hasPressRightTrigger = false;
        public bool hasHoldRightTrigger = false;
        public bool hasReleaseRightTrigger = false;
        public KeyEvent onPressRightTrigger;
        public KeyEvent onHoldRightTrigger;
        public KeyEvent onReleaseRightTrigger;

        // Action Button
        public bool hasPressButtonSouth = false;
        public bool hasHoldButtonSouth = false;
        public bool hasReleaseButtonSouth = false;
        public KeyEvent onPressButtonSouth;
        public KeyEvent onHoldButtonSouth;
        public KeyEvent onReleaseButtonSouth;

        public bool hasPressButtonEast = false;
        public bool hasHoldButtonEast = false;
        public bool hasReleaseButtonEast = false;
        public KeyEvent onPressButtonEast;
        public KeyEvent onHoldButtonEast;
        public KeyEvent onReleaseButtonEast;

        public bool hasPressButtonNorth = false;
        public bool hasHoldButtonNorth = false;
        public bool hasReleaseButtonNorth = false;
        public KeyEvent onPressButtonNorth;
        public KeyEvent onHoldButtonNorth;
        public KeyEvent onReleaseButtonNorth;

        public bool hasPressButtonWest = false;
        public bool hasHoldButtonWest = false;
        public bool hasReleaseButtonWest = false;
        public KeyEvent onPressButtonWest;
        public KeyEvent onHoldButtonWest;
        public KeyEvent onReleaseButtonWest;

        // Custom Button
        public bool hasPressSelect = false;
        public bool hasHoldSelect = false;
        public bool hasReleaseSelect = false;
        public KeyEvent onPressSelect;
        public KeyEvent onHoldSelect;
        public KeyEvent onReleaseSelect;

        public bool hasPressProfile = false;
        public bool hasHoldProfile = false;
        public bool hasReleaseProfile = false;
        public KeyEvent onPressProfile;
        public KeyEvent onHoldProfile;
        public KeyEvent onReleaseProfile;

        public bool hasPressFunction = false;
        public bool hasHoldFunction = false;
        public bool hasReleaseFunction = false;
        public KeyEvent onPressFunction;
        public KeyEvent onHoldFunction;
        public KeyEvent onReleaseFunction;

        public bool hasPressStart = false;
        public bool hasHoldStart = false;
        public bool hasReleaseStart = false;
        public KeyEvent onPressStart;
        public KeyEvent onHoldStart;
        public KeyEvent onReleaseStart;

        public bool hasPressMode = false;
        public bool hasHoldMode = false;
        public bool hasReleaseMode = false;
        public KeyEvent onPressMode;
        public KeyEvent onHoldMode;
        public KeyEvent onReleaseMode;

        public bool hasPressM1 = false;
        public bool hasHoldM1 = false;
        public bool hasReleaseM1 = false;
        public KeyEvent onPressM1;
        public KeyEvent onHoldM1;
        public KeyEvent onReleaseM1;

        public bool hasPressM2 = false;
        public bool hasHoldM2 = false;
        public bool hasReleaseM2 = false;
        public KeyEvent onPressM2;
        public KeyEvent onHoldM2;
        public KeyEvent onReleaseM2;

        public bool hasPressM3 = false;
        public bool hasHoldM3 = false;
        public bool hasReleaseM3 = false;
        public KeyEvent onPressM3;
        public KeyEvent onHoldM3;
        public KeyEvent onReleaseM3;

        public bool hasPressM4 = false;
        public bool hasHoldM4 = false;
        public bool hasReleaseM4 = false;
        public KeyEvent onPressM4;
        public KeyEvent onHoldM4;
        public KeyEvent onReleaseM4;

        // Joystick
        public bool hasPressLeftStick = false;
        public bool hasHoldLeftStick = false;
        public bool hasReleaseLeftStick = false;
        public KeyEvent onPressLeftStick;
        public KeyEvent onHoldLeftStick;
        public KeyEvent onReleaseLeftStick;

        public bool hasPressRightStick = false;
        public bool hasHoldRightStick = false;
        public bool hasReleaseRightStick = false;
        public KeyEvent onPressRightStick;
        public KeyEvent onHoldRightStick;
        public KeyEvent onReleaseRightStick;

        public bool hasMoveLeftStick = false;
        public bool hasMoveRightStick = false;
        public MotionEvent onMoveLeftStick;
        public MotionEvent onMoveRightStick;

        public ConnectionEvent onGamepadConnected;
        public ConnectionEvent onGamepadDisconnected;

        [SerializeField] private GamepadInputType inputType = GamepadInputType.ChooseControlType;

        void Awake()
        {
            if (!sInitialized)
            {
                bool enableGamepadEvent = ShouldEnableGamepadEvent();
                Debug.Log("Gamepad.SetGamepadEventEnabled: enabled (" + enableGamepadEvent + ")");
#if UNITY_ANDROID
                AndroidJavaClass unityPlayerStatic = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject unityActivity = unityPlayerStatic.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject gamepadHelper = new AndroidJavaObject("com.asus.rogplugin.gamepad.GamepadHelper");
                gamepadHelper.CallStatic("setGamepadEventEnabled", enableGamepadEvent);
#endif
                sInitialized = true;
            }
        }

        void OnEnable()
        {
            gameObject.name = $"Gamepad - {inputType}";
        }

        public static bool ShouldEnableGamepadEvent()
        {
#if ENABLE_INPUT_SYSTEM && UNITY_2019_3_OR_NEWER
            return false;
#else
            return true;
#endif
        }

        public static string GetCurrentConnectedDevice()
        {
#if UNITY_ANDROID
            AndroidJavaClass unityPlayerStatic = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject gamepadHelper = new AndroidJavaObject("com.asus.rogplugin.gamepad.GamepadHelper");
            return gamepadHelper?.CallStatic<string>("getCurrentConnectedDevice", unityPlayerStatic.GetStatic<AndroidJavaObject>("currentActivity"));
#else
            return null;
#endif
        }

#if UNITY_ANDROID
        [Preserve /* Invoke from Android */]
        private void OnDpadUpEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.DPAD_UP, hasPressDpadUp, hasHoldDpadUp, hasReleaseDpadUp, onPressDpadUp, onHoldDpadUp, onReleaseDpadUp);
        }

        [Preserve /* Invoke from Android */]
        private void OnDpadDownEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.DPAD_DOWN, hasPressDpadDown, hasHoldDpadDown, hasReleaseDpadDown, onPressDpadDown, onHoldDpadDown, onReleaseDpadDown);
        }

        [Preserve /* Invoke from Android */]
        private void OnDpadLeftEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.DPAD_LEFT, hasPressDpadLeft, hasHoldDpadLeft, hasReleaseDpadLeft, onPressDpadLeft, onHoldDpadLeft, onReleaseDpadLeft);
        }

        [Preserve /* Invoke from Android */]
        private void OnDpadRightEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.DPAD_RIGHT, hasPressDpadRight, hasHoldDpadRight, hasReleaseDpadRight, onPressDpadRight, onHoldDpadRight, onReleaseDpadRight);
        }

        [Preserve /* Invoke from Android */]
        private void OnLeftShoulderEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.LEFT_SHOULDER, hasPressLeftShoulder, hasHoldLeftShoulder, hasReleaseLeftShoulder, onPressLeftShoulder, onHoldLeftShoulder, onReleaseLeftShoulder);
        }

        [Preserve /* Invoke from Android */]
        private void OnRightShoulderEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.RIGHT_SHOULDER, hasPressRightShoulder, hasHoldRightShoulder, hasReleaseRightShoulder, onPressRightShoulder, onHoldRightShoulder, onReleaseRightShoulder);
        }

        [Preserve /* Invoke from Android */]
        private void OnLeftTriggerEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.LEFT_TRIGGER, hasPressLeftTrigger, hasHoldLeftTrigger, hasReleaseLeftTrigger, onPressLeftTrigger, onHoldLeftTrigger, onReleaseLeftTrigger);
        }

        [Preserve /* Invoke from Android */]
        private void OnRightTriggerEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.RIGHT_TRIGGER, hasPressRightTrigger, hasHoldRightTrigger, hasReleaseRightTrigger, onPressRightTrigger, onHoldRightTrigger, onReleaseRightTrigger);
        }

        [Preserve /* Invoke from Android */]
        private void OnButtonSouthEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.BUTTON_SOUTH, hasPressButtonSouth, hasHoldButtonSouth, hasReleaseButtonSouth, onPressButtonSouth, onHoldButtonSouth, onReleaseButtonSouth);
        }

        [Preserve /* Invoke from Android */]
        private void OnButtonEastEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.BUTTON_EAST, hasPressButtonEast, hasHoldButtonEast, hasReleaseButtonEast, onPressButtonEast, onHoldButtonEast, onReleaseButtonEast);
        }

        [Preserve /* Invoke from Android */]
        private void OnButtonWestEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.BUTTON_WEST, hasPressButtonWest, hasHoldButtonWest, hasReleaseButtonWest, onPressButtonWest, onHoldButtonWest, onReleaseButtonWest);
        }

        [Preserve /* Invoke from Android */]
        private void OnButtonNorthEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.BUTTON_NORTH, hasPressButtonNorth, hasHoldButtonNorth, hasReleaseButtonNorth, onPressButtonNorth, onHoldButtonNorth, onReleaseButtonNorth);
        }

        [Preserve /* Invoke from Android */]
        private void OnSelectEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.SELECT, hasPressSelect, hasHoldSelect, hasReleaseSelect, onPressSelect, onHoldSelect, onReleaseSelect);
        }

        [Preserve /* Invoke from Android */]
        private void OnProfileEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.PROFILE, hasPressProfile, hasHoldProfile, hasReleaseProfile, onPressProfile, onHoldProfile, onReleaseProfile);
        }

        [Preserve /* Invoke from Android */]
        private void OnFunctionEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.FUNCTION, hasPressFunction, hasHoldFunction, hasReleaseFunction, onPressFunction, onHoldFunction, onReleaseFunction);
        }

        [Preserve /* Invoke from Android */]
        private void OnStartEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.START, hasPressStart, hasHoldStart, hasReleaseStart, onPressStart, onHoldStart, onReleaseStart);
        }

        [Preserve /* Invoke from Android */]
        private void OnModeEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.MODE, hasPressMode, hasHoldMode, hasReleaseMode, onPressMode, onHoldMode, onReleaseMode);
        }

        [Preserve /* Invoke from Android */]
        private void OnM1Event(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.M1, hasPressM1, hasHoldM1, hasReleaseM1, onPressM1, onHoldM1, onReleaseM1);
        }

        [Preserve /* Invoke from Android */]
        private void OnM2Event(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.M2, hasPressM2, hasHoldM2, hasReleaseM2, onPressM2, onHoldM2, onReleaseM2);
        }

        [Preserve /* Invoke from Android */]
        private void OnM3Event(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.M3, hasPressM3, hasHoldM3, hasReleaseM3, onPressM3, onHoldM3, onReleaseM3);
        }

        [Preserve /* Invoke from Android */]
        private void OnM4Event(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.M4, hasPressM4, hasHoldM4, hasReleaseM4, onPressM4, onHoldM4, onReleaseM4);
        }

        [Preserve /* Invoke from Android */]
        private void OnLeftStickButtonEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.LEFT_STICK, hasPressLeftStick, hasHoldLeftStick, hasReleaseLeftStick, onPressLeftStick, onHoldLeftStick, onReleaseLeftStick);
        }

        [Preserve /* Invoke from Android */]
        private void OnRightStickButtonEvent(string keyEventInfo)
        {
            InvokeKeyEvent(keyEventInfo, InputControlEnum.RIGHT_STICK, hasPressRightStick, hasHoldRightStick, hasReleaseRightStick, onPressRightStick, onHoldRightStick, onReleaseRightStick);
        }

        [Preserve /* Invoke from Android */]
        private void OnLeftStickMoveEvent(string motionEventInfo)
        {
            InvokeMotionEvent(motionEventInfo, InputControlEnum.LEFT_STICK, hasMoveLeftStick, onMoveLeftStick);
        }

        [Preserve /* Invoke from Android */]
        private void OnRightStickMoveEvent(string motionEventInfo)
        {
            InvokeMotionEvent(motionEventInfo, InputControlEnum.RIGHT_STICK, hasMoveRightStick, onMoveRightStick);
        }

        [Preserve /* Invoke from Android */]
        private void OnGamepadConnected(string deviceInfo)
        {
            InvokeConnectionEvent(deviceInfo, onGamepadConnected);
        }

        [Preserve /* Invoke from Android */]
        private void OnGamepadDisconnected(string deviceInfo)
        {
            InvokeConnectionEvent(deviceInfo, onGamepadDisconnected);
        }
#endif

        private void InvokeKeyEvent(string keyEventInfo, InputControlEnum inputControl, bool hasPress, bool hasHold, bool hasRelease, params KeyEvent[] events)
        {
            KeyEventInfo info = new KeyEventInfo(keyEventInfo, inputControl);
            if (info.phaseEnum == PhaseEnum.PRESS && hasPress) events[0].Invoke(info);
            else if (info.phaseEnum == PhaseEnum.HOLD && hasHold) events[1].Invoke(info);
            else if (info.phaseEnum == PhaseEnum.RELEASE && hasRelease) events[2].Invoke(info);
        }

        private void InvokeMotionEvent(string motionEventInfo, InputControlEnum inputControl, bool hasEnable, MotionEvent motionEvent)
        {
            MotionEventInfo info = new MotionEventInfo(motionEventInfo, inputControl);
            if (hasEnable) motionEvent.Invoke(info);
        }

        private void InvokeConnectionEvent(string deviceInfo, ConnectionEvent connectionEvent)
        {
            connectionEvent.Invoke(new DeviceInfo(deviceInfo));
        }

    }
}