#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace RogPhoneSdk
{
#if UNITY_EDITOR
    [InitializeOnLoad] // Make sure static constructor is called during startup.
#endif
    public static class RogKunaiGamepadSupport
    {
#if UNITY_EDITOR || UNITY_ANDROID
        private static readonly int ASUS_VID = 0xB05;
        private static readonly int GP_V1_LEFT_HANDLE_USB_PID = 0x7900;
        private static readonly int GP_V1_HOLDER_USB_PID = 0x7901;
        private static readonly int GP_V1_HOLDER_BT_PID = 0x7902;
        private static readonly int GP_V1_WIRELESS_DONGLE_USB_PID = 0x7903;

        private static readonly int GP_V2_LEFT_HANDLE_USB_PID = 0x7904;
        private static readonly int GP_V2_HOLDER_USB_PID = 0x7905;
        private static readonly int GP_V2_BT_PID = 0x7906;
        private static readonly int GP_V2_BT_PID_LEGACY = 0x7907;

        private static readonly string ProductRogKunaiGamepad = "ASUS ROG Kunai Gamepad";
        private static readonly string ProductRogKunaiGamepadConsumerControl = "ROG Kunai Gamepad Consumer Control";
        private static readonly string ProductRogKunai3GamepadConsumerControl = "ROG Kunai 3 Gamepad Consumer Control";
        private static readonly string InterfaceAndroid = "Android";
        private static readonly string CapabilityVendorId = "vendorId";
        private static readonly string CapabilityProductId = "productId";
        private static readonly string DeviceClassKeyboard = "Keyboard";
#endif

        static RogKunaiGamepadSupport()
        {
            InputSystem.RegisterLayout<RogKunaiGamepad>();
            InputSystem.RegisterLayout<RogKunai3Gamepad>();

#if UNITY_EDITOR || UNITY_ANDROID
            InputSystem.RegisterLayout<RogKunaiGamepadAndroid>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithCapability(CapabilityVendorId, ASUS_VID)
                    .WithCapability(CapabilityProductId, GP_V1_LEFT_HANDLE_USB_PID)); // ROG Kunai bumper

            InputSystem.RegisterLayout<RogKunaiGamepadAndroid>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithCapability(CapabilityVendorId, ASUS_VID)
                    .WithCapability(CapabilityProductId, GP_V1_HOLDER_USB_PID)); // ROG Kunai holder (USB)

            InputSystem.RegisterLayout<RogKunaiGamepadAndroid>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithCapability(CapabilityVendorId, ASUS_VID)
                    .WithCapability(CapabilityProductId, GP_V1_HOLDER_BT_PID)); // ROG Kunai holder (Bluetooth)

            InputSystem.RegisterLayout<RogKunaiGamepadAndroid>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithCapability(CapabilityVendorId, ASUS_VID)
                    .WithCapability(CapabilityProductId, GP_V1_WIRELESS_DONGLE_USB_PID)); // ROG Kunai holder (RF)

            InputSystem.RegisterLayout<RogKunaiGamepadKeyboard>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithDeviceClass(DeviceClassKeyboard)
                    .WithProduct(ProductRogKunaiGamepad)); // ROG Kunai holder (USB / RF) - select

            InputSystem.RegisterLayout<RogKunaiGamepadKeyboard>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithDeviceClass(DeviceClassKeyboard)
                    .WithProduct(ProductRogKunaiGamepadConsumerControl));

            InputSystem.RegisterLayout<RogKunai3GamepadAndroid>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithCapability(CapabilityVendorId, ASUS_VID)
                    .WithCapability(CapabilityProductId, GP_V2_LEFT_HANDLE_USB_PID)); // ROG Kunai 3 bumper

            InputSystem.RegisterLayout<RogKunai3GamepadAndroid>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithCapability(CapabilityVendorId, ASUS_VID)
                    .WithCapability(CapabilityProductId, GP_V2_HOLDER_USB_PID)); // ROG Kunai 3 holder (USB)

            InputSystem.RegisterLayout<RogKunai3GamepadAndroid>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithCapability(CapabilityVendorId, ASUS_VID)
                    .WithCapability(CapabilityProductId, GP_V2_BT_PID)); // ROG Kunai 3 holder (Bluetooth)

            InputSystem.RegisterLayout<RogKunai3GamepadAndroid>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithCapability(CapabilityVendorId, ASUS_VID)
                    .WithCapability(CapabilityProductId, GP_V2_BT_PID_LEGACY)); // ROG Kunai 3 holder (Bluetooth)

            InputSystem.RegisterLayout<RogKunai3GamepadKeyboard>(
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceAndroid)
                    .WithDeviceClass(DeviceClassKeyboard)
                    .WithProduct(ProductRogKunai3GamepadConsumerControl)); // ROG Kunai 3 holder (Bluetooth) - M1, M2
#endif
        }

        [RuntimeInitializeOnLoadMethod]
        static void Init() { }

    }
}
#endif