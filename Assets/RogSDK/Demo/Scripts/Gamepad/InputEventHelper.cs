using RogPhoneSdk;
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogPhoneSdkDemo
{
    public class InputEventHelper : MonoBehaviour, IDeviceConnectionObservable
    {
        public Logger logger;
        public List<IDeviceConnectionObserver> observers = new List<IDeviceConnectionObserver>();
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
        private DemoInputMap deviceInputMap;

        void Awake()
        {
            deviceInputMap = new DemoInputMap();
            foreach (InputActionMap intputActionMap in deviceInputMap.asset.actionMaps)
            {
                foreach (InputAction inputAction in intputActionMap)
                {
                    if (inputAction.type == InputActionType.Button) // Button
                    {
                        inputAction.started += OnButtonClick;
                        inputAction.performed += OnButtonClick;
                        inputAction.canceled += OnButtonClick;
                    }
                    if (inputAction.type == InputActionType.Value) // Joystick
                    {
                        inputAction.started += OnJoystickMove;
                        inputAction.performed += OnJoystickMove;
                        inputAction.canceled += OnJoystickMove;
                    }
                }
            }
        }

        void OnEnable()
        {
            foreach (InputActionMap intputActionMap in deviceInputMap.asset.actionMaps)
            {
                intputActionMap.Enable();
            }
        }

        void OnDisable()
        {
            foreach (InputActionMap intputActionMap in deviceInputMap.asset.actionMaps)
            {
                intputActionMap.Disable();
            }
        }

        // Input System callback
        public void OnButtonClick(InputAction.CallbackContext context)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(context));
        }

        public void OnJoystickMove(InputAction.CallbackContext context)
        {
            logger?.OnJoystickMove(new MotionEventInfoWrapper(context));
        }
# endif

        public void OnPressDpadUp(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldDpadUp(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseDpadUp(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressDpadDown(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldDpadDown(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseDpadDown(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressDpadLeft(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldDpadLeft(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseDpadLeft(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressDpadRight(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldDpadRight(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseDpadRight(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressLeftShoulder(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldLeftShoulder(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseLeftShoulder(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressRightShoulder(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldRightShoulder(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseRightShoulder(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressLeftTrigger(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldLeftTrigger(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseLeftTrigger(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressRightTrigger(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldRightTrigger(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseRightTrigger(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressButtonSouth(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldButtonSouth(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseButtonSouth(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressButtonEast(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldButtonEast(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseButtonEast(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressButtonWest(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldButtonWest(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseButtonWest(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressButtonNorth(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldButtonNorth(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseButtonNorth(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressSelect(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldSelect(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseSelect(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressProfile(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldProfile(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseProfile(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressFunction(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldFunction(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseFunction(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressStart(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldStart(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseStart(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressMode(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldMode(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseMode(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressM1(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldM1(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseM1(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressM2(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldM2(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseM2(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressM3(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldM3(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseM3(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressM4(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldM4(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseM4(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressLeftStick(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldLeftStick(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseLeftStick(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnPressRightStick(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnHoldRightStick(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }
        public void OnReleaseRightStick(KeyEventInfo info)
        {
            logger?.OnButtonClick(new KeyEventInfoWrapper(info));
        }

        public void OnMoveLeftStick(MotionEventInfo info)
        {
            logger?.OnJoystickMove(new MotionEventInfoWrapper(info));
        }

        public void OnMoveRightStick(MotionEventInfo info)
        {
            logger?.OnJoystickMove(new MotionEventInfoWrapper(info));
        }

        public void AddConnectionObserver(IDeviceConnectionObserver observer)
        {
            observers.Add(observer);
        }

        public void RemoveConnectionObserver(IDeviceConnectionObserver observer)
        {
            observers.Remove(observer);
        }

        public void OnGamepadConnected(DeviceInfo deviceInfo)
        {
            observers.ForEach(observer => observer.NotifyConnected(deviceInfo));
        }

        public void OnGamepadDisconnected(DeviceInfo deviceInfo)
        {
            observers.ForEach(observer => observer.NotifyDisconnected(deviceInfo));
        }

    }

}
