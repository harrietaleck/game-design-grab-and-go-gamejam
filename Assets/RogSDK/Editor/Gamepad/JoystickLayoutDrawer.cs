using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    public partial class GamepadEditor : Editor
    {
        private void DrawJoystickLayout()
        {
            // Left Joystick
            DrawMotionProperty(LoadGamepadResource("LeftJoystickIcon.png"), "LEFT JOYSTICK", "hasPressLeftStick", "hasHoldLeftStick", "hasReleaseLeftStick", "onPressLeftStick", "onHoldLeftStick", "onReleaseLeftStick", "hasMoveLeftStick", "onMoveLeftStick");

            // Right Joystick
            DrawMotionProperty(LoadGamepadResource("RightJoystickIcon.png"), "RIGHT JOYSTICK", "hasPressRightStick", "hasHoldRightStick", "hasReleaseRightStick", "onPressRightStick", "onHoldRightStick", "onReleaseRightStick", "hasMoveRightStick", "onMoveRightStick");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
        }
    }
}