using System;
using UnityEditor;
using UnityEngine;

namespace RogPhoneSdk
{
    [CustomEditor(typeof(AuraLightManager))]
    public class AuraLightManagerEditor : Editor
    {
        private static readonly GUIContent targetIdContent = new GUIContent("Target Id",
            "The target where the lighting should be displayed.");
        private static readonly GUIContent effectIdContent = new GUIContent("    Effect Id",
            "The type of Aura effect. It should be one of EFFECT_STATIC, EFFECT_BREATHING, EFFECT_STROBING, EFFECT_COLOR_CYCLE.");
        private static readonly GUIContent colorContent = new GUIContent("    Color",
            "The light's color of Aura effect. You can't change the color of EFFECT_COLOR_CYCLE.");
        private static readonly GUIContent durationMillisContent = new GUIContent("    Duration Millis",
            "The number of milliseconds to perform Aura effect. The maximum duration of a effect is 60 seconds.");
        private static readonly GUIContent rateContent = new GUIContent("    Rate Id",
            "The flash rate of this effect. You can't change the rate of EFFECT_STATIC & EFFECT_COLOR_CYCLE.");
        private static readonly GUIContent displayFileNameContent = new GUIContent("    Display File Name",
            "The name of the file should be displayed.");
        private static readonly GUIContent repeatCountContent = new GUIContent("    Repeat Count",
            "The number of times the file will repeat.");

        public override void OnInspectorGUI()
        {
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((AuraLightManager)target), typeof(AuraLightManager), false);
            GUI.enabled = true;

            AuraLightManager auraLightManagerScript = target as AuraLightManager;
            Enum targetId = EditorGUILayout.EnumPopup(targetIdContent, auraLightManagerScript.targetId);

            GUI.enabled = auraLightManagerScript.targetId != AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION;
            EditorGUILayout.LabelField("Aura lighting", EditorStyles.boldLabel);

            GUI.enabled = auraLightManagerScript.targetId != AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION;
            Enum effectId = EditorGUILayout.EnumPopup(effectIdContent, auraLightManagerScript.effectId);

            GUI.enabled = auraLightManagerScript.targetId != AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION
                && auraLightManagerScript.effectId != AuraLightManager.AuraEffectEnum.EFFECT_OFF;
            Color color = EditorGUILayout.ColorField(colorContent, auraLightManagerScript.color, true, false, false);

            GUI.enabled = auraLightManagerScript.targetId != AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION
                && auraLightManagerScript.effectId != AuraLightManager.AuraEffectEnum.EFFECT_OFF;
            int durationMillis = EditorGUILayout.IntSlider(durationMillisContent, auraLightManagerScript.durationMillis,
            AuraLightManager.AuraLightDurationMin, AuraLightManager.AuraLightDurationMax);

            GUI.enabled = auraLightManagerScript.targetId != AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION
                && auraLightManagerScript.effectId != AuraLightManager.AuraEffectEnum.EFFECT_OFF
                && auraLightManagerScript.effectId != AuraLightManager.AuraEffectEnum.EFFECT_STATIC;
            Enum rateId = EditorGUILayout.EnumPopup(
                rateContent, auraLightManagerScript.rateId, IsEnabledRateId, true, EditorStyles.popup);
            rateId = MakeSureRateIsValid((AuraLightManager.AuraRateEnum)rateId);

            GUI.enabled = auraLightManagerScript.targetId == AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION;
            EditorGUILayout.LabelField("ROG Vision", EditorStyles.boldLabel);

            GUI.enabled = auraLightManagerScript.targetId == AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION;
            String displayFileName = EditorGUILayout.TextField(displayFileNameContent,
                auraLightManagerScript.displayFileName.Length == 0 ? "rog_vision_demo.gif" : auraLightManagerScript.displayFileName);
            auraLightManagerScript.displayFileName = displayFileName;

            GUI.enabled = auraLightManagerScript.targetId == AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION;
            int repeatCount = EditorGUILayout.IntSlider(repeatCountContent, auraLightManagerScript.repeatCount, 0, 10);

            SerializedProperty targetIdProp = serializedObject.FindProperty("targetId");
            SerializedProperty effectIdProp = serializedObject.FindProperty("effectId");
            SerializedProperty colorProp = serializedObject.FindProperty("color");
            SerializedProperty durationProp = serializedObject.FindProperty("durationMillis");
            SerializedProperty rateProp = serializedObject.FindProperty("rateId");
            SerializedProperty displayFileNameProp = serializedObject.FindProperty("displayFileName");
            SerializedProperty repeatCountProp = serializedObject.FindProperty("repeatCount");

            targetIdProp.intValue = (int)(AuraLightManager.AuraTargetEnum)targetId;
            effectIdProp.intValue = (int)(AuraLightManager.AuraEffectEnum)effectId;
            colorProp.colorValue = color;
            durationProp.intValue = durationMillis;
            rateProp.intValue = (int)(AuraLightManager.AuraRateEnum)rateId;
            displayFileNameProp.stringValue = displayFileName;
            repeatCountProp.intValue = repeatCount;
            serializedObject.ApplyModifiedProperties();
        }

        private AuraLightManager.AuraRateEnum MakeSureRateIsValid(AuraLightManager.AuraRateEnum auraRateEnum)
        {
            AuraLightManager auraLightManagerScript = target as AuraLightManager;
            return IsEnabledRateId(auraRateEnum) ? auraRateEnum : AuraLightManager.AuraRateEnum.RATE_SLOW;
        }

        private bool IsEnabledRateId(Enum rateId)
        {
            AuraLightManager auraLightManagerScript = target as AuraLightManager;
            if (auraLightManagerScript.effectId == AuraLightManager.AuraEffectEnum.EFFECT_COLOR_CYCLE)
            {
                return rateId.Equals(AuraLightManager.AuraRateEnum.RATE_SLOW)
                    || rateId.Equals(AuraLightManager.AuraRateEnum.RATE_MEDIUM)
                    || rateId.Equals(AuraLightManager.AuraRateEnum.RATE_FAST);
            }
            return true;
        }
    }
}