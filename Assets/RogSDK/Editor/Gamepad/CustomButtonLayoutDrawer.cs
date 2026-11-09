using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    public partial class GamepadEditor : Editor
    {
        private void DrawCustomButtonLayout()
        {
            DrawButtonProperty(null, "SELECT", "hasPressSelect", "hasHoldSelect", "hasReleaseSelect", "onPressSelect", "onHoldSelect", "onReleaseSelect");
            DrawButtonProperty(null, "PROFILE", "hasPressProfile", "hasHoldProfile", "hasReleaseProfile", "onPressProfile", "onHoldProfile", "onReleaseProfile");
            DrawButtonProperty(null, "FUNCTION", "hasPressFunction", "hasHoldFunction", "hasReleaseFunction", "onPressFunction", "onHoldFunction", "onReleaseFunction");
            DrawButtonProperty(null, "START", "hasPressStart", "hasHoldStart", "hasReleaseStart", "onPressStart", "onHoldStart", "onReleaseStart");
            DrawButtonProperty(null, "MODE", "hasPressMode", "hasHoldMode", "hasReleaseMode", "onPressMode", "onHoldMode", "onReleaseMode");
            DrawButtonProperty(null, "M1", "hasPressM1", "hasHoldM1", "hasReleaseM1", "onPressM1", "onHoldM1", "onReleaseM1");
            DrawButtonProperty(null, "M2", "hasPressM2", "hasHoldM2", "hasReleaseM2", "onPressM2", "onHoldM2", "onReleaseM2");
            DrawButtonProperty(null, "M3", "hasPressM3", "hasHoldM3", "hasReleaseM3", "onPressM3", "onHoldM3", "onReleaseM3");
            DrawButtonProperty(null, "M4", "hasPressM4", "hasHoldM4", "hasReleaseM4", "onPressM4", "onHoldM4", "onReleaseM4");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
        }
    }
}