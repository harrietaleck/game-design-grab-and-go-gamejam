using RogPhoneSdk;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class AuraTargetItemValidator : MonoBehaviour
    {
        private int[] availableTargetList;

        void Start()
        {
            Dropdown dropdown = gameObject.GetComponent<Dropdown>();
            AuraLightManager auraLightManager = GameObject.Find("AsusAuraLightManager").GetComponent<AuraLightManager>();
            availableTargetList = auraLightManager.GetAvailableTargetList();

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                if (!IsAvailableTarget((int)WidgetManager.GetEnumValueByIndex<AuraLightManager.AuraTargetEnum>(i)))
                {
                    dropdown.options[i].text = dropdown.options[i].text + " (Unavailable)";
                }
            }
        }

        private bool IsAvailableTarget(int target)
        {
            return (availableTargetList != null) && Array.Exists(availableTargetList, element => element == target);
        }
    }
}