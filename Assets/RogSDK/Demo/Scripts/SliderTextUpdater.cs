using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class SliderTextUpdater : MonoBehaviour
    {
        private Text text;
        private string title;
        private string unit;

        void Awake()
        {
            text = gameObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                string[] splitText = text.text.Split(':');
                if (splitText.Length == 2)
                {
                    title = splitText[0];
                    unit = splitText[1];
                }
            }

            Slider slider = gameObject.GetComponent<Slider>();
            if (slider != null)
            {
                slider.onValueChanged.AddListener(UpdateText);
                UpdateText(slider.value);
            }
        }

        private void UpdateText(float value)
        {
            if (text != null)
            {
                text.text = title + ": " + value.ToString() + unit;
            }
        }
    }
}