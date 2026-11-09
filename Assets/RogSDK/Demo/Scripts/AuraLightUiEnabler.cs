using RogPhoneSdk;
using RogPhoneSdkDemo;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class AuraLightUiEnabler : MonoBehaviour
    {
        public Dropdown dropdownTarget;
        public Dropdown dropdownEffect;
        public Dropdown dropdownRate;
        public Slider sliderDuration;
        public Text warningText;
        public GameObject gameObjectColorPicker;
        public GameObject gameObjectRogVisionHintText;
        public GameObject gameObjectRogVisionPreview;
        public GameObject gameObjectSliderRepeatCount;

        private AuraLightManager.AuraTargetEnum auraTargetEnum;
        private AuraLightManager.AuraEffectEnum auraEffectEnum;

        // Start is called before the first frame update
        void Start()
        {
            OnTargetChanged(dropdownTarget.value);
            OnEffectChanged(dropdownEffect.value);
        }

        public void OnTargetChanged(int index)
        {
            auraTargetEnum = WidgetManager.GetEnumValueByIndex<AuraLightManager.AuraTargetEnum>(index);
            UpdateUiState();
        }

        public void OnEffectChanged(int index)
        {
            auraEffectEnum = WidgetManager.GetEnumValueByIndex<AuraLightManager.AuraEffectEnum>(index);
            UpdateUiState();
        }

        private void UpdateUiState()
        {
            if (dropdownEffect != null)
            {
                dropdownEffect.interactable = !auraTargetEnum.Equals(AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION);
            }
            if (dropdownRate != null)
            {
                dropdownRate.interactable = !auraTargetEnum.Equals(AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION)
                    && !auraEffectEnum.Equals(AuraLightManager.AuraEffectEnum.EFFECT_OFF)
                    && !auraEffectEnum.Equals(AuraLightManager.AuraEffectEnum.EFFECT_STATIC);
            }
            if (sliderDuration != null)
            {
                sliderDuration.interactable = !auraTargetEnum.Equals(AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION)
                    && !auraEffectEnum.Equals(AuraLightManager.AuraEffectEnum.EFFECT_OFF);
            }
            if (warningText != null)
            {
                warningText.gameObject.SetActive(auraEffectEnum.Equals(AuraLightManager.AuraEffectEnum.EFFECT_COLOR_CYCLE)
                    && dropdownEffect != null && dropdownEffect.interactable);
            }
            if (gameObjectColorPicker != null)
            {
                gameObjectColorPicker.SetActive(!auraTargetEnum.Equals(AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION));
            }
            if (gameObjectRogVisionHintText != null)
            {
                gameObjectRogVisionHintText.SetActive(
                    auraTargetEnum.Equals(AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION));
            }
            if (gameObjectRogVisionPreview != null)
            {
                gameObjectRogVisionPreview.SetActive(auraTargetEnum.Equals(AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION));
            }
            if (gameObjectSliderRepeatCount != null)
            {
                gameObjectSliderRepeatCount.SetActive(auraTargetEnum.Equals(AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION));
            }
        }
    }
}
