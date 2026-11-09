using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    public partial class GamepadEditor : Editor
    {
        private void DrawTriggerButtonLayout()
        {
            DrawButtonProperty(LoadGamepadResource("LTIcon.png"), "LEFT SHOULDER (FRONT)", "hasPressLeftShoulder", "hasHoldLeftShoulder", "hasReleaseLeftShoulder", "onPressLeftShoulder", "onHoldLeftShoulder", "onReleaseLeftShoulder");
            DrawButtonProperty(LoadGamepadResource("RTIcon.png"), "RIGHT SHOULDER (FRONT)", "hasPressRightShoulder", "hasHoldRightShoulder", "hasReleaseRightShoulder", "onPressRightShoulder", "onHoldRightShoulder", "onReleaseRightShoulder");
            DrawButtonProperty(LoadGamepadResource("LTIcon.png"), "LEFT TRIGGER (REAR)", "hasPressLeftTrigger", "hasHoldLeftTrigger", "hasReleaseLeftTrigger", "onPressLeftTrigger", "onHoldLeftTrigger", "onReleaseLeftTrigger");
            DrawButtonProperty(LoadGamepadResource("RTIcon.png"), "RIGHT TRIGGER (REAR)", "hasPressRightTrigger", "hasHoldRightTrigger", "hasReleaseRightTrigger", "onPressRightTrigger", "onHoldRightTrigger", "onReleaseRightTrigger");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
        }
    }
}