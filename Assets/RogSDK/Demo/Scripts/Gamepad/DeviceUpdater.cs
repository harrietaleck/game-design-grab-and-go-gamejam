using RogPhoneSdk;
using System.Collections.Generic;
using UnityEngine;

namespace RogPhoneSdkDemo
{
    class DeviceUpdater : AbsDeviceUpdater, IDeviceConnectionObserver
    {
        private Dictionary<int, string> connectedKunaiGamepad = new Dictionary<int, string>();
        private Dictionary<int, string> connectedKunai3Gamepad = new Dictionary<int, string>();
        private Dictionary<int, string> connectedKunaiKeyboard = new Dictionary<int, string>();
        private Dictionary<int, string> connectedKunai3Keyboard = new Dictionary<int, string>();

        protected override void RegisterInputDeviceListener()
        {
            GameObject.Find("InputEventHelper").GetComponent<InputEventHelper>().AddConnectionObserver(this);
        }

        protected override void UnregisterInputDeviceListener()
        {
            GameObject.Find("InputEventHelper")?.GetComponent<InputEventHelper>().RemoveConnectionObserver(this);
        }

        protected override void UpdateCurrentConnectedDevice()
        {
            string devicesString = Gamepad.GetCurrentConnectedDevice();
            if (devicesString != null)
            {
                string[] args = devicesString.Split(',');
                foreach (string arg in args)
                {
                    AddDeviceToDictionary(new DeviceInfo(arg));
                }
            }
            else
            {
                Debug.Log("No device is connected now");
            }
        }

        private void AddDeviceToDictionary(DeviceInfo deviceInfo)
        {
            Dictionary<int, string> dictionary = GetDictonaryForDevice(deviceInfo);
            if (dictionary != null)
            {
                dictionary.Add(deviceInfo.deviceId, deviceInfo.gamepad.ToString());
                NotifyDeviceChanged();
            }
        }

        private void RemoveDeviceFromDictionary(DeviceInfo deviceInfo)
        {
            Dictionary<int, string> dictionary = GetDictonaryForDevice(deviceInfo);
            if (dictionary != null)
            {
                if (dictionary.Remove(deviceInfo.deviceId))
                {
                    NotifyDeviceChanged();
                }
            }
        }

        private Dictionary<int, string> GetDictonaryForDevice(DeviceInfo deviceInfo)
        {
            if (deviceInfo.gamepad == GamepadEnum.KUNAI_GAMEPAD)
            {
                return connectedKunaiGamepad;
            }
            else if (deviceInfo.gamepad == GamepadEnum.KUNAI_3_GAMEPAD)
            {
                return connectedKunai3Gamepad;
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

        public void NotifyConnected(DeviceInfo deviceInfo)
        {
            AddDeviceToDictionary(deviceInfo);
        }

        public void NotifyDisconnected(DeviceInfo deviceInfo)
        {
            RemoveDeviceFromDictionary(deviceInfo);
        }

    }

}
