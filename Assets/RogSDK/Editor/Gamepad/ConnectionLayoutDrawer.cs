using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    public partial class GamepadEditor : Editor
    {
        private void DrawConnectionLayout()
        {
            DrawConnectionProperty(null, "CONNECTION", "onGamepadConnected", "onGamepadDisconnected");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
        }
    }
}
