using System;
using RogPhoneSdk;
using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class FpsItemValidator : MonoBehaviour
    {
        private int[] availableFpsList;

        void Start()
        {
            Dropdown dropdown = gameObject.GetComponent<Dropdown>();
            FpsManager fpsManager = GameObject.Find("AsusFpsManager").GetComponent<FpsManager>();
            availableFpsList = fpsManager.GetAvailableFpsList();

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                if (i == dropdown.options.Count - 1)
                {
                    continue;
                }
                if (!IsAvailableFps((int)WidgetManager.GetEnumValueByIndex<FpsManager.FpsEnum>(i)))
                {
                    dropdown.options[i].text = dropdown.options[i].text + "   (Unavailable)";
                }
            }
        }

        private bool IsAvailableFps(int fps)
        {
            return (availableFpsList != null) && Array.Exists(availableFpsList, element => element == fps); ;
        }
    }
}