using RogPhoneSdk;
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID && UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogPhoneSdkDemo
{
#if UNITY_ANDROID && UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
    class InputSystemDeviceUpdater : AbsDeviceUpdater
    {
        private Dictionary<int, string> connectedKunaiGamepad = new Dictionary<int, string>();
        private Dictionary<int, string> connectedKunai3Gamepad = new Dictionary<int, string>();
        private Dictionary<int, string> connectedKunaiKeyboard = new Dictionary<int, string>();
        private Dictionary<int, string> connectedKunai3Keyboard = new Dictionary<int, string>();

        protected override void RegisterInputDeviceListener()
        {
            InputSystem.onDeviceChange += OnDeviceChanged;
        }

        protected override void UnregisterInputDeviceListener()
        {
            InputSystem.onDeviceChange -= OnDeviceChanged;
        }

        private void OnDeviceChanged(InputDevice inputDevice, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                    // New Device.
                    Debug.Log("InputDeviceChange.Added: " + inputDevice.name + ", deviceId= " + inputDevice.deviceId);
                    AddDeviceToDictionary(inputDevice);
                    break;
                case InputDeviceChange.Disconnected:
                    // Device got unplugged.
                    Debug.Log("InputDeviceChange.Disconnected: " + inputDevice.name + ", deviceId= " + inputDevice.deviceId);
                    break;
                case InputDeviceChange.Reconnected:
                    // Plugged back in.
                    Debug.Log("InputDeviceChange.Reconnected: " + inputDevice.name + ", deviceId= " + inputDevice.deviceId);
                    break;
                case InputDeviceChange.Removed:
                    // Remove from Input System entirely; by default, Devices stay in the system once discovered.
                    Debug.Log("InputDeviceChange.Removed: " + inputDevice.name + ", deviceId= " + inputDevice.deviceId);
                    RemoveDeviceFromDictionary(inputDevice);
                    break;
                default:
                    // See InputDeviceChange reference for other event types.
                    break;
            };
        }

        protected override void UpdateCurrentConnectedDevice()
        {
            foreach (InputDevice inputDevice in InputSystem.devices)
            {
                AddDeviceToDictionary(inputDevice);
            }
        }

        private void AddDeviceToDictionary(InputDevice inputDevice)
        {
            Dictionary<int, string> dictionary = GetDictonaryForDevice(inputDevice);
            if (dictionary != null)
            {
                dictionary.Add(inputDevice.deviceId, inputDevice.name);
                NotifyDeviceChanged();
            }
        }

        private void RemoveDeviceFromDictionary(InputDevice inputDevice)
        {
            Dictionary<int, string> dictionary = GetDictonaryForDevice(inputDevice);
            if (dictionary != null)
            {
                if (dictionary.Remove(inputDevice.deviceId))
                {
                    NotifyDeviceChanged();
                }
            }
        }

        private Dictionary<int, string> GetDictonaryForDevice(InputDevice inputDevice)
        {
            if (inputDevice is RogKunaiGamepadAndroid)
            {
                return connectedKunaiGamepad;
            }
            else if (inputDevice is RogKunai3GamepadAndroid)
            {
                return connectedKunai3Gamepad;
            }
            else if (inputDevice is RogKunaiGamepadKeyboard)
            {
                return connectedKunaiKeyboard;
            }
            else if (inputDevice is RogKunai3GamepadKeyboard)
            {
                return connectedKunai3Keyboard;
            }
            return null;
        }

        protected override int GetKunaiGamepadCount()
        {
            return connectedKunaiGamepad.Count;
        }

        protected override int GetKunai3GamepadCount()
        {
            return connectedKunai3Gamepad.Count;
        }

        protected override int GetKunaiKeyboardCount()
        {
            return connectedKunaiKeyboard.Count;
        }

        protected override int GetKunai3KeyboardCount()
        {
            return connectedKunai3Keyboard.Count;
        }
    }
#endif
}
