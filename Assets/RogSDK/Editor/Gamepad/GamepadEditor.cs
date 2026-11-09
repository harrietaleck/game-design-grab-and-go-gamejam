using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    [CustomEditor((typeof(Gamepad)))]
    public partial class GamepadEditor : Editor
    {
        private static readonly string GAMEPAD_RESOURCE_FOLDER = "Assets/RogSDK/Resources/Gamepad/";
        private static bool sEnableGamepadEvent = false;

        void OnEnable()
        {
            sEnableGamepadEvent = Gamepad.ShouldEnableGamepadEvent();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (!sEnableGamepadEvent)
            {
                // draw unsupported dialog on inspector
                EditorGUILayout.HelpBox("The GameObject (" + target.name + ") is unnecessary when Input System is enabled", MessageType.Warning);
                return;
            }
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Draw Title
            GUIStyle titleStyle = new GUIStyle() { fontSize = 24, fontStyle = FontStyle.Bold };
            titleStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField("<color=white>ASUS GAMEPAD</color>", titleStyle, GUILayout.Height(24));

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Draw Control Type Selection
            SerializedProperty inputTypeProp = serializedObject.FindProperty("inputType");
            inputTypeProp.enumValueIndex = (int)(GamepadInputType)EditorGUILayout.EnumPopup((GamepadInputType)inputTypeProp.enumValueIndex);

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Draw selected input type UI
            switch ((GamepadInputType)inputTypeProp.enumValueIndex)
            {
                case GamepadInputType.DirectionalButtons: DrawDirectionalButtonLayout(); break;
                case GamepadInputType.TriggerButtons: DrawTriggerButtonLayout(); break;
                case GamepadInputType.ActionButtons: DrawActionButtonLayout(); break;
                case GamepadInputType.CustomButtons: DrawCustomButtonLayout(); break;
                case GamepadInputType.Joysticks: DrawJoystickLayout(); break;
                case GamepadInputType.Connections: DrawConnectionLayout(); break;
                default: break;
            }
            if (serializedObject.hasModifiedProperties) serializedObject.ApplyModifiedProperties();
        }

        private void DrawCustomHeaderLabel(string content)
        {
            // Define Style
            GUIStyle headerStyle = new GUIStyle()
            {
                normal = EditorStyles.textField.normal,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 18,
            };

            GUI.backgroundColor = Color.red;
            EditorGUILayout.LabelField(content, headerStyle, GUILayout.Height(25));
            GUI.backgroundColor = Color.white;
        }

        public Texture LoadGamepadResource(string fileName)
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(GAMEPAD_RESOURCE_FOLDER + fileName, typeof(Texture));
        }
    }
}