using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    public partial class GamepadEditor : Editor
    {
        private const float MARGIN = 42;
        private void DrawConnectionProperty(Texture icon, string headerLabel, string connectedKey, string disconnectedKey)
        {
            EditorGUILayout.BeginHorizontal();
            DrawIcon(icon);

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawCustomHeaderLabel(headerLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(connectedKey));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(disconnectedKey));

            EditorGUILayout.EndVertical();
            GUILayout.Space(MARGIN);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMotionProperty(Texture icon, string headerLabel, string hasPressKey, string hasHoldKey, string hasReleaseKey, string pressEventKey, string holdEventKey, string releaseEventKey, string hasMotionKey, string motionEventKey)
        {
            EditorGUILayout.BeginHorizontal();
            DrawIcon(icon);

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawCustomHeaderLabel(headerLabel);

            EditorGUILayout.BeginHorizontal();

            SerializedProperty hasPressProp = serializedObject.FindProperty(hasPressKey);
            SerializedProperty hasHoldProp = serializedObject.FindProperty(hasHoldKey);
            SerializedProperty hasReleaseProp = serializedObject.FindProperty(hasReleaseKey);
            SerializedProperty hasMotionProp = serializedObject.FindProperty(hasMotionKey);
            DrawJoystickEnableButton(hasPressProp, hasHoldProp, hasReleaseProp, hasMotionProp);

            EditorGUILayout.EndHorizontal();

            DrawJoystickEventList(hasPressProp, pressEventKey, hasHoldProp, holdEventKey, hasReleaseProp, releaseEventKey, hasMotionProp, motionEventKey);

            EditorGUILayout.EndVertical();
            GUILayout.Space(MARGIN);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawButtonProperty(Texture icon, string headerLabel, string hasPressKey, string hasHoldKey, string hasReleaseKey, string pressEventKey, string holdEventKey, string releaseEventKey)
        {
            EditorGUILayout.BeginHorizontal();
            DrawIcon(icon);

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawCustomHeaderLabel(headerLabel);

            EditorGUILayout.BeginHorizontal();

            SerializedProperty hasPressProp = serializedObject.FindProperty(hasPressKey);
            SerializedProperty hasHoldProp = serializedObject.FindProperty(hasHoldKey);
            SerializedProperty hasReleaseProp = serializedObject.FindProperty(hasReleaseKey);
            DrawKeyEnableButton(hasPressProp, hasHoldProp, hasReleaseProp);
            EditorGUILayout.EndHorizontal();

            if (hasPressProp.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty(pressEventKey));
            if (hasHoldProp.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty(holdEventKey));
            if (hasReleaseProp.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty(releaseEventKey));

            EditorGUILayout.EndVertical();

            GUILayout.Space(MARGIN);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawIcon(Texture icon)
        {
            if (icon != null)
            {
                GUILayoutOption[] opt = new GUILayoutOption[]
                {
                    GUILayout.Width(MARGIN),
                    GUILayout.Height(MARGIN),
                    GUILayout.ExpandWidth(false),
                    GUILayout.ExpandHeight(false),
                    GUILayout.MaxWidth(MARGIN)
                };
                EditorGUILayout.BeginVertical(opt);
                GUILayout.Box(icon, opt);
                EditorGUILayout.EndVertical();
            }
            else
            {
                GUILayoutOption[] opt = new GUILayoutOption[]
                {
                    GUILayout.Width(50),
                    GUILayout.Height(50),
                    GUILayout.ExpandWidth(false),
                    GUILayout.ExpandHeight(false),
                    GUILayout.MaxWidth(50)
                };
                EditorGUILayout.BeginVertical(opt);
                GUILayout.Space(50);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawJoystickEnableButton(SerializedProperty hasPressProp, SerializedProperty hasHoldProp, SerializedProperty hasReleaseProp, SerializedProperty hasMotionProp)
        {
            DrawKeyEnableButton(hasPressProp, hasHoldProp, hasReleaseProp);
            GUI.backgroundColor = (hasMotionProp.boolValue) ? Color.green : Color.white;
            if (GUILayout.Button("Motion", EditorStyles.miniButtonRight)) hasMotionProp.boolValue = !hasMotionProp.boolValue;
            GUI.backgroundColor = Color.white;
        }

        private void DrawKeyEnableButton(SerializedProperty hasPressProp, SerializedProperty hasHoldProp, SerializedProperty hasReleaseProp)
        {
            GUI.backgroundColor = (hasPressProp.boolValue) ? Color.green : Color.white;
            if (GUILayout.Button("Press", EditorStyles.miniButtonLeft)) hasPressProp.boolValue = !hasPressProp.boolValue;

            GUI.backgroundColor = (hasHoldProp.boolValue) ? Color.green : Color.white;
            if (GUILayout.Button("Hold", EditorStyles.miniButtonMid)) hasHoldProp.boolValue = !hasHoldProp.boolValue;

            GUI.backgroundColor = (hasReleaseProp.boolValue) ? Color.green : Color.white;
            if (GUILayout.Button("Release", EditorStyles.miniButtonRight)) hasReleaseProp.boolValue = !hasReleaseProp.boolValue;
            GUI.backgroundColor = Color.white;
        }

        private void DrawJoystickEventList(SerializedProperty hasPressProp, string pressEventKey, SerializedProperty hasHoldProp, string holdEventKey, SerializedProperty hasReleaseProp, string releaseEventKey, SerializedProperty hasMotionProp, string motionEventKey)
        {
            DrawKeyEventList(hasPressProp, pressEventKey, hasHoldProp, holdEventKey, hasReleaseProp, releaseEventKey);
            if (hasMotionProp.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty(motionEventKey));
        }
        private void DrawKeyEventList(SerializedProperty hasPressProp, string pressEventKey, SerializedProperty hasHoldProp, string holdEventKey, SerializedProperty hasReleaseProp, string releaseEventKey)
        {
            if (hasPressProp.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty(pressEventKey));
            if (hasHoldProp.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty(holdEventKey));
            if (hasReleaseProp.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty(releaseEventKey));
        }
    }
}