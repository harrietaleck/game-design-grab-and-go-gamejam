#if (UNITY_EDITOR || UNITY_ANDROID) && (UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM)

using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Scripting;

namespace RogPhoneSdk
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct RogKunaiGamepadAndroidReport : IInputStateTypeInfo
    {
        [InputControl(name = "select", format = "BIT", bit = 4, sizeInBits = 1)]
        [FieldOffset(0)] public byte select;

        [InputControl(name = "profile", layout = "Button", format = "BIT", bit = 1, sizeInBits = 1)]
        [InputControl(name = "function", layout = "Button", format = "BIT", bit = 2, sizeInBits = 1)]
        [InputControl(name = "start", format = "BIT", bit = 3, sizeInBits = 1)]
        [InputControl(name = "M1", layout = "Button", format = "BIT", bit = 4, sizeInBits = 1)]
        [InputControl(name = "M2", layout = "Button", format = "BIT", bit = 5, sizeInBits = 1)]
        [InputControl(name = "M3", layout = "Button", format = "BIT", bit = 6, sizeInBits = 1)]
        [InputControl(name = "M4", layout = "Button", format = "BIT", bit = 7, sizeInBits = 1)]
        [FieldOffset(24)] public byte buttons;

        [InputControl(name = "dpad", format = "VEC2", sizeInBits = 64)]
        [InputControl(name = "dpad/right", offset = 0, bit = 0, format = "FLT", parameters = "clamp=3,clampConstant=0,clampMin=0,clampMax=1")]
        [InputControl(name = "dpad/left", offset = 0, bit = 0, format = "FLT", parameters = "clamp=3,clampConstant=0,clampMin=-1,clampMax=0,invert")]
        [InputControl(name = "dpad/down", offset = 4, bit = 0, format = "FLT",parameters = "clamp=3,clampConstant=0,clampMin=0,clampMax=1")]
        [InputControl(name = "dpad/up", offset = 4, bit = 0, format = "FLT", parameters = "clamp=3,clampConstant=0,clampMin=-1,clampMax=0,invert")]
        [FieldOffset(88)] public byte dpad;
        public FourCC format => new FourCC('A', 'G', 'C', ' ');
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct RogKunaiGamepadKeyboardReport : IInputStateTypeInfo
    {
        [InputControl(name = "select", layout = "Button", format = "BIT", bit = (uint)Key.Escape, sizeInBits = 1)]
        [FieldOffset(0)] public byte select;

        public FourCC format => new FourCC('K', 'E', 'Y', 'S');
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct RogKunai3GamepadAndroidReport : IInputStateTypeInfo
    {
        [InputControl(name = "mode", layout = "Button", format = "BIT", bit = 6, sizeInBits = 1)]
        [FieldOffset(13)] public byte buttons;

        [InputControl(name = "M1", layout = "Button", format = "BIT", bit = 4, sizeInBits = 1)]
        [InputControl(name = "M2", layout = "Button", format = "BIT", bit = 5, sizeInBits = 1)]
        [FieldOffset(24)] public byte multiButtons;

        [InputControl(name = "dpad", format = "VEC2", sizeInBits = 64)]
        [InputControl(name = "dpad/right", offset = 0, bit = 0, format = "FLT", parameters = "clamp=3,clampConstant=0,clampMin=0,clampMax=1")]
        [InputControl(name = "dpad/left", offset = 0, bit = 0, format = "FLT", parameters = "clamp=3,clampConstant=0,clampMin=-1,clampMax=0,invert")]
        [InputControl(name = "dpad/down", offset = 4, bit = 0, format = "FLT", parameters = "clamp=3,clampConstant=0,clampMin=0,clampMax=1")]
        [InputControl(name = "dpad/up", offset = 4, bit = 0, format = "FLT", parameters = "clamp=3,clampConstant=0,clampMin=-1,clampMax=0,invert")]
        [FieldOffset(88)] public byte dpad;
        public FourCC format => new FourCC('A', 'G', 'C', ' ');
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct RogKunai3GamepadKeyboardReport : IInputStateTypeInfo
    {
        [InputControl(name = "M1", layout = "Button", format = "BIT", bit = (uint)Key.None, sizeInBits = 1)]
        [InputControl(name = "M2", layout = "Button", format = "BIT", bit = (uint)Key.None, sizeInBits = 1)]
        [FieldOffset(0)] public byte buttons;

        public FourCC format => new FourCC('K', 'E', 'Y', 'S');
    }

    [InputControlLayout(stateType = typeof(RogKunaiGamepadAndroidReport), hideInUI = true)]
    [Preserve]
    public class RogKunaiGamepadAndroid : RogKunaiGamepad
    {
        protected override void OnRemoved()
        {
            base.OnRemoved();

            // Workaround of removing RogKunaiGamepad without removing RogKunaiGamepadKeyboard & keyboard
            InputDevice device = InputSystem.GetDeviceById(deviceId + 1);
            if (device is RogKunaiGamepadKeyboard || (device is Keyboard && device.description.product.Equals(description.product)))
            {
                Debug.Log(name + " (deviceId = " + deviceId + ") try to remove its bundled keyboard device ("
                        + device.name + ", deviceId = " + device.deviceId + ") manually");
                InputSystem.RemoveDevice(device);
            }
        }
    }

    [InputControlLayout(stateType = typeof(RogKunaiGamepadKeyboardReport), displayName = "ASUS ROG Kunai Gamepad Keyboard")]
    [Preserve]
    public class RogKunaiGamepadKeyboard : InputDevice
    {
        [InputControl(name = "select", displayName = "Select")]
        public ButtonControl selectButton { get; private set; }

        protected override void FinishSetup()
        {
            selectButton = GetChildControl<ButtonControl>("select");
        }
    }

    [InputControlLayout(stateType = typeof(RogKunai3GamepadAndroidReport), hideInUI = true)]
    [Preserve]
    public class RogKunai3GamepadAndroid : RogKunai3Gamepad
    {
        protected override void OnRemoved()
        {
            base.OnRemoved();

            //  Workaround of removing RogKunai3Gamepad without removing keyboard
            InputDevice device = InputSystem.GetDeviceById(deviceId + 1);
            if (device is Keyboard && device.description.product.Equals(description.product))
            {
                Debug.Log(name + " (deviceId = " + deviceId + ") try to remove its bundled keyboard device ("
                        + device.name + ", deviceId = " + device.deviceId + ") manually");
                InputSystem.RemoveDevice(device);
            }
        }
    }

    [InputControlLayout(stateType = typeof(RogKunai3GamepadKeyboardReport), displayName = "ASUS ROG Kunai 3 Gamepad Keyboard")]
    [Preserve]
    public class RogKunai3GamepadKeyboard : InputDevice
    {
        [InputControl(name = "M1", displayName = "M1")]
        public ButtonControl m1Button { get; private set; }
        [InputControl(name = "M2", displayName = "M2")]
        public ButtonControl m2Button { get; private set; }

        protected override void FinishSetup()
        {
            m1Button = GetChildControl<ButtonControl>("M1");
            m2Button = GetChildControl<ButtonControl>("M2");
        }
    }

}
#endif
