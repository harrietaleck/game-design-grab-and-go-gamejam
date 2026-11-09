using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    public partial class GamepadEditor : Editor
    {
        private void DrawDirectionalButtonLayout()
        {
            DrawButtonProperty(LoadGamepadResource("UpIcon.png"), "UP ⇧", "hasPressDpadUp", "hasHoldDpadUp", "hasReleaseDpadUp", "onPressDpadUp", "onHoldDpadUp", "onReleaseDpadUp");
            DrawButtonProperty(LoadGamepadResource("DownIcon.png"), "DOWN ⇩", "hasPressDpadDown", "hasHoldDpadDown", "hasReleaseDpadDown", "onPressDpadDown", "onHoldDpadDown", "onReleaseDpadDown");
            DrawButtonProperty(LoadGamepadResource("LeftIcon.png"), "LEFT ⇦", "hasPressDpadLeft", "hasHoldDpadLeft", "hasReleaseDpadLeft", "onPressDpadLeft", "onHoldDpadLeft", "onReleaseDpadLeft");
            DrawButtonProperty(LoadGamepadResource("RightIcon.png"), "RIGHT ⇨", "hasPressDpadRight", "hasHoldDpadRight", "hasReleaseDpadRight", "onPressDpadRight", "onHoldDpadRight", "onReleaseDpadRight");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
        }
    }
}