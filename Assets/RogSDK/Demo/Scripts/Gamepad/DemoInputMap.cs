// GENERATED AUTOMATICALLY FROM 'Assets/RogSDK/Demo/Scripts/Gamepad/DemoInputMap.inputactions'
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace RogPhoneSdkDemo
{
    public class @DemoInputMap : IInputActionCollection, IDisposable
    {
        public InputActionAsset asset { get; }
        public @DemoInputMap()
        {
            asset = InputActionAsset.FromJson(@"{
    ""name"": ""DemoInputMap"",
    ""maps"": [
        {
            ""name"": ""DemoActionMap"",
            ""id"": ""6b16ad30-066a-43e4-8e77-93b115615de8"",
            ""actions"": [
                {
                    ""name"": ""LT1"",
                    ""type"": ""Button"",
                    ""id"": ""fd33b9e6-1956-43c1-842f-821dd15d5f5d"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""RT1"",
                    ""type"": ""Button"",
                    ""id"": ""26a2ad56-8b12-441a-bf6d-9083851a158d"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""LT2"",
                    ""type"": ""Button"",
                    ""id"": ""10ba5004-7950-4ca7-89d5-c3965d84f62e"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""RT2"",
                    ""type"": ""Button"",
                    ""id"": ""94c2999b-01d6-44da-9c56-7ba277b08efd"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Button X"",
                    ""type"": ""Button"",
                    ""id"": ""1d6ea50b-9328-4f44-8f1b-0fa662a340e4"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Button Y"",
                    ""type"": ""Button"",
                    ""id"": ""c31f6bec-ef23-4c32-88c4-7640f2909da5"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Button A"",
                    ""type"": ""Button"",
                    ""id"": ""85189450-c25e-4e0a-a1b0-9097f2dcaa33"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Button B"",
                    ""type"": ""Button"",
                    ""id"": ""eaf04b9a-0954-4104-8f0c-ec9303031762"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Dpad Up"",
                    ""type"": ""Button"",
                    ""id"": ""87454940-2c95-4ee1-a049-7b52f5403ff3"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Dpad Down"",
                    ""type"": ""Button"",
                    ""id"": ""8340461b-0ed2-4261-8083-eb2bd3860a76"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Dpad Left"",
                    ""type"": ""Button"",
                    ""id"": ""53bf2d7e-6cf8-4f81-b853-55b642739447"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Dpad Right"",
                    ""type"": ""Button"",
                    ""id"": ""ccdfef27-e2a8-4de0-a3b7-dd3660023581"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""LeftStick Press"",
                    ""type"": ""Button"",
                    ""id"": ""d261c5c2-60e2-42c4-ae7d-570ecd264f5d"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""RightStick Press"",
                    ""type"": ""Button"",
                    ""id"": ""aa1a7925-bfbd-4d7f-93e8-22350cdf2469"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""LeftStick Move"",
                    ""type"": ""Value"",
                    ""id"": ""33195217-8af2-4737-9b44-fcc37cf1b135"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""RightStick Move"",
                    ""type"": ""Value"",
                    ""id"": ""dcce69a8-8c0d-495d-9b9a-674a180ee8aa"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""M1"",
                    ""type"": ""Button"",
                    ""id"": ""f713a423-ad25-4e0d-b13a-91559557843b"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""M2"",
                    ""type"": ""Button"",
                    ""id"": ""56b2f440-202c-4d54-9760-0f33236d0e78"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""M3"",
                    ""type"": ""Button"",
                    ""id"": ""e63185e5-c233-4edd-97db-1cd927f5f2f6"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""M4"",
                    ""type"": ""Button"",
                    ""id"": ""9ecfd551-d81a-461d-842a-77bfc506b136"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Select"",
                    ""type"": ""Button"",
                    ""id"": ""986e6dc0-a96b-42a3-a14c-ec35a79ef02e"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Start"",
                    ""type"": ""Button"",
                    ""id"": ""2edb8d8a-f8a9-4b68-bd6a-694d1ae44c17"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Profile"",
                    ""type"": ""Button"",
                    ""id"": ""5b3eea0b-a8c0-4121-bdcb-8abb614d9748"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Function"",
                    ""type"": ""Button"",
                    ""id"": ""c5b0a369-bcc0-49df-bb84-bbeab7e9622d"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Mode"",
                    ""type"": ""Button"",
                    ""id"": ""e7950404-1b8f-4489-85c1-031b95afe881"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""db6a60c5-c8f1-4450-93cb-1ef4b5d14a3d"",
                    ""path"": ""<Gamepad>/leftShoulder"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LT1"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""3aa7148f-e905-41fd-b2c6-7eab07626f8e"",
                    ""path"": ""<Gamepad>/rightShoulder"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""RT1"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""17e7944c-75f2-43d7-9585-c461b9dfe492"",
                    ""path"": ""<Gamepad>/leftTrigger"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LT2"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""8824bfcd-d55d-4dad-bc9d-709fd166cbc8"",
                    ""path"": ""<Gamepad>/rightTrigger"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""RT2"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""987dab31-e068-4bd0-9819-5045a04720b5"",
                    ""path"": ""<Gamepad>/buttonWest"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Button X"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""7c4ca61c-d799-4097-9797-55d6843a4189"",
                    ""path"": ""<Gamepad>/buttonNorth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Button Y"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""df352444-d260-47c9-a3ca-c0a156d58b15"",
                    ""path"": ""<Gamepad>/buttonSouth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Button A"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""5c011eb1-f641-421c-acc8-bd84331d7cfd"",
                    ""path"": ""<Gamepad>/buttonEast"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Button B"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""cd3faef6-1ee4-455b-a7aa-a8e00e9f67aa"",
                    ""path"": ""<Gamepad>/dpad/up"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Dpad Up"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c240501d-4d3e-4959-a944-213d4499bf28"",
                    ""path"": ""<Gamepad>/dpad/down"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Dpad Down"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""97781321-00f7-426f-a951-532e22bad3c0"",
                    ""path"": ""<Gamepad>/dpad/left"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Dpad Left"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""108a817a-83b1-46ae-b8bb-05e75178d00b"",
                    ""path"": ""<Gamepad>/dpad/right"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Dpad Right"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""f6d96dd7-4a89-4bff-9585-ead156b674c2"",
                    ""path"": ""<Gamepad>/leftStickPress"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LeftStick Press"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""8b0f9a8e-4bc4-4d2c-b5b4-5c2543fe7057"",
                    ""path"": ""<Gamepad>/rightStickPress"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""RightStick Press"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""292bd78c-975e-4097-b750-ecc9a6c25960"",
                    ""path"": ""<Gamepad>/leftStick"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LeftStick Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""64f7341c-cc87-43a7-a711-54c0c836e123"",
                    ""path"": ""<Gamepad>/rightStick"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""RightStick Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""44b8c61d-ab37-461b-8a69-fb56d0ac8521"",
                    ""path"": ""<RogKunaiGamepad>/M1"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""M1"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""31e30f6d-85b3-47db-8a52-0ee99c591860"",
                    ""path"": ""<RogKunai3Gamepad>/M1"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""M1"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""29eb1f78-dfc7-4374-8013-908db2d3b86c"",
                    ""path"": ""<RogKunai3GamepadKeyboard>/M1"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""M1"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""23b7a051-efa0-4365-8148-396eeb42fb68"",
                    ""path"": ""<RogKunaiGamepad>/M2"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""M2"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""993c1de6-dbb0-4122-9a1f-3043bee2ea1e"",
                    ""path"": ""<RogKunai3Gamepad>/M2"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""M2"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""4c76a062-5422-4c71-b226-c9443b874661"",
                    ""path"": ""<RogKunai3GamepadKeyboard>/M2"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""M2"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""ebccb1f8-198f-432a-88e6-1d6e295ed701"",
                    ""path"": ""<RogKunaiGamepad>/M3"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""M3"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""41534d0a-cf14-47f1-a254-de3d7147189f"",
                    ""path"": ""<RogKunaiGamepad>/M4"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""M4"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b16441ce-7f3b-4139-a72b-4e5802fb6b85"",
                    ""path"": ""<RogKunaiGamepad>/select"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Select"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""5b8835e4-1cb9-493c-9bac-0ed6b0ae45c7"",
                    ""path"": ""<RogKunaiGamepadKeyboard>/select"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Select"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""6009f9d6-0d9d-4300-b1e0-86f006be65ef"",
                    ""path"": ""<RogKunai3Gamepad>/select"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Select"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""2f2d3ff5-7279-4c7a-a47a-9e993e844d5e"",
                    ""path"": ""<RogKunaiGamepad>/start"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Start"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""42d1c79b-8d3d-4918-8b2f-92ad7db9a080"",
                    ""path"": ""<RogKunai3Gamepad>/start"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Start"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""17cbd156-ce85-4d14-bc7a-c71d294a09f4"",
                    ""path"": ""<RogKunaiGamepad>/Profile"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Profile"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e27f0402-91c7-480c-9b69-d0aa391c19c9"",
                    ""path"": ""<RogKunaiGamepad>/Function"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Function"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""067abfa7-1c9a-4777-9ced-1ff8afb98a05"",
                    ""path"": ""<RogKunai3Gamepad>/Mode"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Mode"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
            // DemoActionMap
            m_DemoActionMap = asset.FindActionMap("DemoActionMap", throwIfNotFound: true);
            m_DemoActionMap_LT1 = m_DemoActionMap.FindAction("LT1", throwIfNotFound: true);
            m_DemoActionMap_RT1 = m_DemoActionMap.FindAction("RT1", throwIfNotFound: true);
            m_DemoActionMap_LT2 = m_DemoActionMap.FindAction("LT2", throwIfNotFound: true);
            m_DemoActionMap_RT2 = m_DemoActionMap.FindAction("RT2", throwIfNotFound: true);
            m_DemoActionMap_ButtonX = m_DemoActionMap.FindAction("Button X", throwIfNotFound: true);
            m_DemoActionMap_ButtonY = m_DemoActionMap.FindAction("Button Y", throwIfNotFound: true);
            m_DemoActionMap_ButtonA = m_DemoActionMap.FindAction("Button A", throwIfNotFound: true);
            m_DemoActionMap_ButtonB = m_DemoActionMap.FindAction("Button B", throwIfNotFound: true);
            m_DemoActionMap_DpadUp = m_DemoActionMap.FindAction("Dpad Up", throwIfNotFound: true);
            m_DemoActionMap_DpadDown = m_DemoActionMap.FindAction("Dpad Down", throwIfNotFound: true);
            m_DemoActionMap_DpadLeft = m_DemoActionMap.FindAction("Dpad Left", throwIfNotFound: true);
            m_DemoActionMap_DpadRight = m_DemoActionMap.FindAction("Dpad Right", throwIfNotFound: true);
            m_DemoActionMap_LeftStickPress = m_DemoActionMap.FindAction("LeftStick Press", throwIfNotFound: true);
            m_DemoActionMap_RightStickPress = m_DemoActionMap.FindAction("RightStick Press", throwIfNotFound: true);
            m_DemoActionMap_LeftStickMove = m_DemoActionMap.FindAction("LeftStick Move", throwIfNotFound: true);
            m_DemoActionMap_RightStickMove = m_DemoActionMap.FindAction("RightStick Move", throwIfNotFound: true);
            m_DemoActionMap_M1 = m_DemoActionMap.FindAction("M1", throwIfNotFound: true);
            m_DemoActionMap_M2 = m_DemoActionMap.FindAction("M2", throwIfNotFound: true);
            m_DemoActionMap_M3 = m_DemoActionMap.FindAction("M3", throwIfNotFound: true);
            m_DemoActionMap_M4 = m_DemoActionMap.FindAction("M4", throwIfNotFound: true);
            m_DemoActionMap_Select = m_DemoActionMap.FindAction("Select", throwIfNotFound: true);
            m_DemoActionMap_Start = m_DemoActionMap.FindAction("Start", throwIfNotFound: true);
            m_DemoActionMap_Profile = m_DemoActionMap.FindAction("Profile", throwIfNotFound: true);
            m_DemoActionMap_Function = m_DemoActionMap.FindAction("Function", throwIfNotFound: true);
            m_DemoActionMap_Mode = m_DemoActionMap.FindAction("Mode", throwIfNotFound: true);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(asset);
        }

        public InputBinding? bindingMask
        {
            get => asset.bindingMask;
            set => asset.bindingMask = value;
        }

        public ReadOnlyArray<InputDevice>? devices
        {
            get => asset.devices;
            set => asset.devices = value;
        }

        public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

        public bool Contains(InputAction action)
        {
            return asset.Contains(action);
        }

        public IEnumerator<InputAction> GetEnumerator()
        {
            return asset.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Enable()
        {
            asset.Enable();
        }

        public void Disable()
        {
            asset.Disable();
        }

        // DemoActionMap
        private readonly InputActionMap m_DemoActionMap;
        private IDemoActionMapActions m_DemoActionMapActionsCallbackInterface;
        private readonly InputAction m_DemoActionMap_LT1;
        private readonly InputAction m_DemoActionMap_RT1;
        private readonly InputAction m_DemoActionMap_LT2;
        private readonly InputAction m_DemoActionMap_RT2;
        private readonly InputAction m_DemoActionMap_ButtonX;
        private readonly InputAction m_DemoActionMap_ButtonY;
        private readonly InputAction m_DemoActionMap_ButtonA;
        private readonly InputAction m_DemoActionMap_ButtonB;
        private readonly InputAction m_DemoActionMap_DpadUp;
        private readonly InputAction m_DemoActionMap_DpadDown;
        private readonly InputAction m_DemoActionMap_DpadLeft;
        private readonly InputAction m_DemoActionMap_DpadRight;
        private readonly InputAction m_DemoActionMap_LeftStickPress;
        private readonly InputAction m_DemoActionMap_RightStickPress;
        private readonly InputAction m_DemoActionMap_LeftStickMove;
        private readonly InputAction m_DemoActionMap_RightStickMove;
        private readonly InputAction m_DemoActionMap_M1;
        private readonly InputAction m_DemoActionMap_M2;
        private readonly InputAction m_DemoActionMap_M3;
        private readonly InputAction m_DemoActionMap_M4;
        private readonly InputAction m_DemoActionMap_Select;
        private readonly InputAction m_DemoActionMap_Start;
        private readonly InputAction m_DemoActionMap_Profile;
        private readonly InputAction m_DemoActionMap_Function;
        private readonly InputAction m_DemoActionMap_Mode;
        public struct DemoActionMapActions
        {
            private @DemoInputMap m_Wrapper;
            public DemoActionMapActions(@DemoInputMap wrapper) { m_Wrapper = wrapper; }
            public InputAction @LT1 => m_Wrapper.m_DemoActionMap_LT1;
            public InputAction @RT1 => m_Wrapper.m_DemoActionMap_RT1;
            public InputAction @LT2 => m_Wrapper.m_DemoActionMap_LT2;
            public InputAction @RT2 => m_Wrapper.m_DemoActionMap_RT2;
            public InputAction @ButtonX => m_Wrapper.m_DemoActionMap_ButtonX;
            public InputAction @ButtonY => m_Wrapper.m_DemoActionMap_ButtonY;
            public InputAction @ButtonA => m_Wrapper.m_DemoActionMap_ButtonA;
            public InputAction @ButtonB => m_Wrapper.m_DemoActionMap_ButtonB;
            public InputAction @DpadUp => m_Wrapper.m_DemoActionMap_DpadUp;
            public InputAction @DpadDown => m_Wrapper.m_DemoActionMap_DpadDown;
            public InputAction @DpadLeft => m_Wrapper.m_DemoActionMap_DpadLeft;
            public InputAction @DpadRight => m_Wrapper.m_DemoActionMap_DpadRight;
            public InputAction @LeftStickPress => m_Wrapper.m_DemoActionMap_LeftStickPress;
            public InputAction @RightStickPress => m_Wrapper.m_DemoActionMap_RightStickPress;
            public InputAction @LeftStickMove => m_Wrapper.m_DemoActionMap_LeftStickMove;
            public InputAction @RightStickMove => m_Wrapper.m_DemoActionMap_RightStickMove;
            public InputAction @M1 => m_Wrapper.m_DemoActionMap_M1;
            public InputAction @M2 => m_Wrapper.m_DemoActionMap_M2;
            public InputAction @M3 => m_Wrapper.m_DemoActionMap_M3;
            public InputAction @M4 => m_Wrapper.m_DemoActionMap_M4;
            public InputAction @Select => m_Wrapper.m_DemoActionMap_Select;
            public InputAction @Start => m_Wrapper.m_DemoActionMap_Start;
            public InputAction @Profile => m_Wrapper.m_DemoActionMap_Profile;
            public InputAction @Function => m_Wrapper.m_DemoActionMap_Function;
            public InputAction @Mode => m_Wrapper.m_DemoActionMap_Mode;
            public InputActionMap Get() { return m_Wrapper.m_DemoActionMap; }
            public void Enable() { Get().Enable(); }
            public void Disable() { Get().Disable(); }
            public bool enabled => Get().enabled;
            public static implicit operator InputActionMap(DemoActionMapActions set) { return set.Get(); }
            public void SetCallbacks(IDemoActionMapActions instance)
            {
                if (m_Wrapper.m_DemoActionMapActionsCallbackInterface != null)
                {
                    @LT1.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLT1;
                    @LT1.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLT1;
                    @LT1.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLT1;
                    @RT1.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRT1;
                    @RT1.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRT1;
                    @RT1.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRT1;
                    @LT2.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLT2;
                    @LT2.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLT2;
                    @LT2.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLT2;
                    @RT2.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRT2;
                    @RT2.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRT2;
                    @RT2.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRT2;
                    @ButtonX.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonX;
                    @ButtonX.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonX;
                    @ButtonX.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonX;
                    @ButtonY.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonY;
                    @ButtonY.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonY;
                    @ButtonY.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonY;
                    @ButtonA.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonA;
                    @ButtonA.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonA;
                    @ButtonA.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonA;
                    @ButtonB.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonB;
                    @ButtonB.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonB;
                    @ButtonB.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnButtonB;
                    @DpadUp.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadUp;
                    @DpadUp.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadUp;
                    @DpadUp.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadUp;
                    @DpadDown.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadDown;
                    @DpadDown.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadDown;
                    @DpadDown.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadDown;
                    @DpadLeft.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadLeft;
                    @DpadLeft.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadLeft;
                    @DpadLeft.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadLeft;
                    @DpadRight.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadRight;
                    @DpadRight.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadRight;
                    @DpadRight.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnDpadRight;
                    @LeftStickPress.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLeftStickPress;
                    @LeftStickPress.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLeftStickPress;
                    @LeftStickPress.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLeftStickPress;
                    @RightStickPress.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRightStickPress;
                    @RightStickPress.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRightStickPress;
                    @RightStickPress.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRightStickPress;
                    @LeftStickMove.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLeftStickMove;
                    @LeftStickMove.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLeftStickMove;
                    @LeftStickMove.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnLeftStickMove;
                    @RightStickMove.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRightStickMove;
                    @RightStickMove.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRightStickMove;
                    @RightStickMove.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnRightStickMove;
                    @M1.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM1;
                    @M1.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM1;
                    @M1.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM1;
                    @M2.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM2;
                    @M2.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM2;
                    @M2.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM2;
                    @M3.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM3;
                    @M3.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM3;
                    @M3.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM3;
                    @M4.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM4;
                    @M4.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM4;
                    @M4.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnM4;
                    @Select.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnSelect;
                    @Select.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnSelect;
                    @Select.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnSelect;
                    @Start.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnStart;
                    @Start.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnStart;
                    @Start.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnStart;
                    @Profile.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnProfile;
                    @Profile.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnProfile;
                    @Profile.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnProfile;
                    @Function.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnFunction;
                    @Function.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnFunction;
                    @Function.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnFunction;
                    @Mode.started -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnMode;
                    @Mode.performed -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnMode;
                    @Mode.canceled -= m_Wrapper.m_DemoActionMapActionsCallbackInterface.OnMode;
                }
                m_Wrapper.m_DemoActionMapActionsCallbackInterface = instance;
                if (instance != null)
                {
                    @LT1.started += instance.OnLT1;
                    @LT1.performed += instance.OnLT1;
                    @LT1.canceled += instance.OnLT1;
                    @RT1.started += instance.OnRT1;
                    @RT1.performed += instance.OnRT1;
                    @RT1.canceled += instance.OnRT1;
                    @LT2.started += instance.OnLT2;
                    @LT2.performed += instance.OnLT2;
                    @LT2.canceled += instance.OnLT2;
                    @RT2.started += instance.OnRT2;
                    @RT2.performed += instance.OnRT2;
                    @RT2.canceled += instance.OnRT2;
                    @ButtonX.started += instance.OnButtonX;
                    @ButtonX.performed += instance.OnButtonX;
                    @ButtonX.canceled += instance.OnButtonX;
                    @ButtonY.started += instance.OnButtonY;
                    @ButtonY.performed += instance.OnButtonY;
                    @ButtonY.canceled += instance.OnButtonY;
                    @ButtonA.started += instance.OnButtonA;
                    @ButtonA.performed += instance.OnButtonA;
                    @ButtonA.canceled += instance.OnButtonA;
                    @ButtonB.started += instance.OnButtonB;
                    @ButtonB.performed += instance.OnButtonB;
                    @ButtonB.canceled += instance.OnButtonB;
                    @DpadUp.started += instance.OnDpadUp;
                    @DpadUp.performed += instance.OnDpadUp;
                    @DpadUp.canceled += instance.OnDpadUp;
                    @DpadDown.started += instance.OnDpadDown;
                    @DpadDown.performed += instance.OnDpadDown;
                    @DpadDown.canceled += instance.OnDpadDown;
                    @DpadLeft.started += instance.OnDpadLeft;
                    @DpadLeft.performed += instance.OnDpadLeft;
                    @DpadLeft.canceled += instance.OnDpadLeft;
                    @DpadRight.started += instance.OnDpadRight;
                    @DpadRight.performed += instance.OnDpadRight;
                    @DpadRight.canceled += instance.OnDpadRight;
                    @LeftStickPress.started += instance.OnLeftStickPress;
                    @LeftStickPress.performed += instance.OnLeftStickPress;
                    @LeftStickPress.canceled += instance.OnLeftStickPress;
                    @RightStickPress.started += instance.OnRightStickPress;
                    @RightStickPress.performed += instance.OnRightStickPress;
                    @RightStickPress.canceled += instance.OnRightStickPress;
                    @LeftStickMove.started += instance.OnLeftStickMove;
                    @LeftStickMove.performed += instance.OnLeftStickMove;
                    @LeftStickMove.canceled += instance.OnLeftStickMove;
                    @RightStickMove.started += instance.OnRightStickMove;
                    @RightStickMove.performed += instance.OnRightStickMove;
                    @RightStickMove.canceled += instance.OnRightStickMove;
                    @M1.started += instance.OnM1;
                    @M1.performed += instance.OnM1;
                    @M1.canceled += instance.OnM1;
                    @M2.started += instance.OnM2;
                    @M2.performed += instance.OnM2;
                    @M2.canceled += instance.OnM2;
                    @M3.started += instance.OnM3;
                    @M3.performed += instance.OnM3;
                    @M3.canceled += instance.OnM3;
                    @M4.started += instance.OnM4;
                    @M4.performed += instance.OnM4;
                    @M4.canceled += instance.OnM4;
                    @Select.started += instance.OnSelect;
                    @Select.performed += instance.OnSelect;
                    @Select.canceled += instance.OnSelect;
                    @Start.started += instance.OnStart;
                    @Start.performed += instance.OnStart;
                    @Start.canceled += instance.OnStart;
                    @Profile.started += instance.OnProfile;
                    @Profile.performed += instance.OnProfile;
                    @Profile.canceled += instance.OnProfile;
                    @Function.started += instance.OnFunction;
                    @Function.performed += instance.OnFunction;
                    @Function.canceled += instance.OnFunction;
                    @Mode.started += instance.OnMode;
                    @Mode.performed += instance.OnMode;
                    @Mode.canceled += instance.OnMode;
                }
            }
        }
        public DemoActionMapActions @DemoActionMap => new DemoActionMapActions(this);
        public interface IDemoActionMapActions
        {
            void OnLT1(InputAction.CallbackContext context);
            void OnRT1(InputAction.CallbackContext context);
            void OnLT2(InputAction.CallbackContext context);
            void OnRT2(InputAction.CallbackContext context);
            void OnButtonX(InputAction.CallbackContext context);
            void OnButtonY(InputAction.CallbackContext context);
            void OnButtonA(InputAction.CallbackContext context);
            void OnButtonB(InputAction.CallbackContext context);
            void OnDpadUp(InputAction.CallbackContext context);
            void OnDpadDown(InputAction.CallbackContext context);
            void OnDpadLeft(InputAction.CallbackContext context);
            void OnDpadRight(InputAction.CallbackContext context);
            void OnLeftStickPress(InputAction.CallbackContext context);
            void OnRightStickPress(InputAction.CallbackContext context);
            void OnLeftStickMove(InputAction.CallbackContext context);
            void OnRightStickMove(InputAction.CallbackContext context);
            void OnM1(InputAction.CallbackContext context);
            void OnM2(InputAction.CallbackContext context);
            void OnM3(InputAction.CallbackContext context);
            void OnM4(InputAction.CallbackContext context);
            void OnSelect(InputAction.CallbackContext context);
            void OnStart(InputAction.CallbackContext context);
            void OnProfile(InputAction.CallbackContext context);
            void OnFunction(InputAction.CallbackContext context);
            void OnMode(InputAction.CallbackContext context);
        }
    }
}
#endif