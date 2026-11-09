# if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM

using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine;

namespace RogPhoneSdk
{
    [InputControlLayout(displayName = "ASUS ROG Kunai Gamepad")]
    public class RogKunaiGamepad :
#if UNITY_ANDROID
        UnityEngine.InputSystem.Android.AndroidGamepad
#else
        UnityEngine.InputSystem.Gamepad
#endif
    {
        [InputControl(name = "buttonSouth", displayName = "A")]
        [InputControl(name = "buttonWest", displayName = "X")]
        [InputControl(name = "buttonNorth", displayName = "Y")]
        [InputControl(name = "buttonEast", displayName = "B")]
        [InputControl(name = "leftShoulder", displayName = "Left Trigger 1")]
        [InputControl(name = "rightShoulder", displayName = "Right Trigger 1")]
        [InputControl(name = "leftTrigger", displayName = "Left Trigger 2")]
        [InputControl(name = "rightTrigger", displayName = "Right Trigger 2")]

        [InputControl(name = "M1", displayName = "M1")]
        public ButtonControl m1Button { get; private set; }
        [InputControl(name = "M2", displayName = "M2")]
        public ButtonControl m2Button { get; private set; }
        [InputControl(name = "M3", displayName = "M3")]
        public ButtonControl m3Button { get; private set; }
        [InputControl(name = "M4", displayName = "M4")]
        public ButtonControl m4Button { get; private set; }
        [InputControl(name = "profile", displayName = "Profile")]
        public ButtonControl profileButton { get; private set; }
        [InputControl(name = "function", displayName = "Function")]
        public ButtonControl functionButton { get; private set; }

        protected override void FinishSetup()
        {
            Debug.Log("FinishSetup, device=" + this.device
                + " / displayName=" + this.displayName
                + " / className=" + this.GetType()
                + " / description=" + this.description);
            base.FinishSetup();
            m1Button = GetChildControl<ButtonControl>("M1");
            m2Button = GetChildControl<ButtonControl>("M2");
            m3Button = GetChildControl<ButtonControl>("M3");
            m4Button = GetChildControl<ButtonControl>("M4");
            profileButton = GetChildControl<ButtonControl>("profile");
            functionButton = GetChildControl<ButtonControl>("function");
        }
    }

    [InputControlLayout(displayName = "ASUS ROG Kunai 3 Gamepad")]
    public class RogKunai3Gamepad :
#if UNITY_ANDROID
        UnityEngine.InputSystem.Android.AndroidGamepad
#else
        UnityEngine.InputSystem.Gamepad
#endif
    {
        [InputControl(name = "buttonSouth", displayName = "A")]
        [InputControl(name = "buttonWest", displayName = "X")]
        [InputControl(name = "buttonNorth", displayName = "Y")]
        [InputControl(name = "buttonEast", displayName = "B")]
        [InputControl(name = "leftShoulder", displayName = "Left Trigger 1")]
        [InputControl(name = "rightShoulder", displayName = "Right Trigger 1")]
        [InputControl(name = "leftTrigger", displayName = "Left Trigger 2")]
        [InputControl(name = "rightTrigger", displayName = "Right Trigger 2")]

        [InputControl(name = "mode", displayName = "Mode")]
        public ButtonControl modeButton { get; private set; }
        [InputControl(name = "M1", displayName = "M1")]
        public ButtonControl m1Button { get; private set; }
        [InputControl(name = "M2", displayName = "M2")]
        public ButtonControl m2Button { get; private set; }

        protected override void FinishSetup()
        {
            Debug.Log("FinishSetup, device=" + this.device
                + " / displayName=" + this.displayName
                + " / className=" + this.GetType()
                + " / description=" + this.description);
            base.FinishSetup();
            modeButton = GetChildControl<ButtonControl>("mode");
            m1Button = GetChildControl<ButtonControl>("M1");
            m2Button = GetChildControl<ButtonControl>("M2");
        }
    }
}
#endif