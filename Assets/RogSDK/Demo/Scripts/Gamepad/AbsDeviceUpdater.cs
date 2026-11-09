using RogPhoneSdk;
using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    abstract class AbsDeviceUpdater
    {
        public virtual void Init()
        {
            RegisterInputDeviceListener();
            UpdateCurrentConnectedDevice();
            NotifyDeviceChanged();
        }

        public virtual void Destroy()
        {
            UnregisterInputDeviceListener();
        }

        protected abstract void RegisterInputDeviceListener();
        protected abstract void UnregisterInputDeviceListener();
        protected abstract void UpdateCurrentConnectedDevice();
        protected abstract int GetKunaiGamepadCount();
        protected abstract int GetKunai3GamepadCount();
        protected abstract int GetKunaiKeyboardCount();
        protected abstract int GetKunai3KeyboardCount();

        protected void NotifyDeviceChanged()
        {
            Dropdown gamepadDropdown = GameObject.Find("GamepadDropdown").GetComponent<Dropdown>();
            for (int i = 0; i < gamepadDropdown.options.Count; i++)
            {
                if (i == (int)GamepadEnum.KUNAI_GAMEPAD)
                {
                    gamepadDropdown.options[i].text = "Kunai Gamepad" + "\n("
                        + GetKunaiGamepadCount() + " Gamepad & "
                        + GetKunaiKeyboardCount() + " Keyboard connected)";
                }
                else if (i == (int)GamepadEnum.KUNAI_3_GAMEPAD)
                {
                    gamepadDropdown.options[i].text = "Kunai 3 Gamepad" + "\n("
                        + GetKunai3GamepadCount() + " Gamepad & "
                        + GetKunai3KeyboardCount() + " Keyboard connected)";
                }
            }
        }
    }
}
