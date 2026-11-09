using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    public partial class GamepadEditor : Editor
    {
        private void DrawActionButtonLayout()
        {
            DrawButtonProperty(LoadGamepadResource("ButtonAIcon.png"), "BUTTON SOUTH", "hasPressButtonSouth", "hasHoldButtonSouth", "hasReleaseButtonSouth", "onPressButtonSouth", "onHoldButtonSouth", "onReleaseButtonSouth");
            DrawButtonProperty(LoadGamepadResource("ButtonBIcon.png"), "BUTTON EAST", "hasPressButtonEast", "hasHoldButtonEast", "hasReleaseButtonEast", "onPressButtonEast", "onHoldButtonEast", "onReleaseButtonEast");
            DrawButtonProperty(LoadGamepadResource("ButtonXIcon.png"), "BUTTON WEST", "hasPressButtonWest", "hasHoldButtonWest", "hasReleaseButtonWest", "onPressButtonWest", "onHoldButtonWest", "onReleaseButtonWest");
            DrawButtonProperty(LoadGamepadResource("ButtonYIcon.png"), "BUTTON NORTH", "hasPressButtonNorth", "hasHoldButtonNorth", "hasReleaseButtonNorth", "onPressButtonNorth", "onHoldButtonNorth", "onReleaseButtonNorth");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
        }
    }
}
