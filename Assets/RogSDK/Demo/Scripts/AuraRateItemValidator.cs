using System;
using RogPhoneSdk;
using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class AuraRateItemValidator : MonoBehaviour
    {
        public void OnEffectChanged(int index)
        {
            Dropdown dropdown = gameObject.GetComponent<Dropdown>();
            if (dropdown != null)
            {
                AuraLightManager.AuraEffectEnum auraEffectEnum =
                    WidgetManager.GetEnumValueByIndex<AuraLightManager.AuraEffectEnum>(index);
                if (auraEffectEnum.Equals(AuraLightManager.AuraEffectEnum.EFFECT_COLOR_CYCLE))
                {
                    for (int i = 0; i < dropdown.options.Count; i++)
                    {
                        AuraLightManager.AuraRateEnum auraRateEnum = WidgetManager.GetEnumValueByIndex<AuraLightManager.AuraRateEnum>(
                            i, new Comparison<AuraLightManager.AuraRateEnum>((i1, i2) => i2.CompareTo(i1)));
                        if (!IsAvailableAuraRateForColorCycle(auraRateEnum))
                        {
                            dropdown.options[i].text = dropdown.options[i].text + "   (Unavailable)";
                        }
                    }
                }
                else
                {
                    foreach (Dropdown.OptionData optionData in dropdown.options)
                    {
                        optionData.text = optionData.text.Replace("   (Unavailable)", "");
                    }
                }
            }
        }

        private bool IsAvailableAuraRateForColorCycle(AuraLightManager.AuraRateEnum auraRateEnum)
        {
            return auraRateEnum.Equals(AuraLightManager.AuraRateEnum.RATE_SLOW)
                || auraRateEnum.Equals(AuraLightManager.AuraRateEnum.RATE_MEDIUM)
                || auraRateEnum.Equals(AuraLightManager.AuraRateEnum.RATE_FAST);
        }
    }
}