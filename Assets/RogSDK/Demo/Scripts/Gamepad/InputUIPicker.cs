using RogPhoneSdk;
using UnityEngine;

namespace RogPhoneSdkDemo
{
    public class InputUIPicker : MonoBehaviour
    {
        public GameObject kunaiGamepad;
        public GameObject kunai3Gamepad;
        public GameObject logger;

        // Current displayed diagram
        private GameObject currentDisplay = null;

        private AbsDeviceUpdater deviceUpdater =
#if UNITY_ANDROID && UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
            new InputSystemDeviceUpdater();
#else
            new DeviceUpdater();
#endif

        void Awake()
        {
            deviceUpdater.Init();
        }

        void OnDestroy()
        {
            deviceUpdater.Destroy();
        }

        public void OnDropdownValueChanged(int idx)
        {
            switch (WidgetManager.GetEnumValueByIndex<GamepadEnum>(idx))
            {
                case GamepadEnum.KUNAI_GAMEPAD:
                    SwitchGamepadImage(kunaiGamepad);
                    logger.GetComponent<Logger>().SetUpStick();
                    break;
                case GamepadEnum.KUNAI_3_GAMEPAD:
                    SwitchGamepadImage(kunai3Gamepad);
                    logger.GetComponent<Logger>().SetUpStick();
                    break;
                default:
                    currentDisplay = null;
                    kunaiGamepad.SetActive(false);
                    kunai3Gamepad.SetActive(false);
                    break;
            }
        }

        private void SwitchGamepadImage(GameObject newDiagram)
        {
            Debug.Log("currentDisplay=" + currentDisplay + " / newDiagram=" + newDiagram);
            if (currentDisplay != newDiagram)
            {
                currentDisplay?.SetActive(false);
                currentDisplay = newDiagram;
                currentDisplay.SetActive(true);
            }
        }
    }
}
