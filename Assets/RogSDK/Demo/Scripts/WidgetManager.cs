using System;
using System.Collections;
using System.Collections.Generic;
using RogPhoneSdk;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class WidgetManager : MonoBehaviour
    {
        public static readonly string ROG_VISION_GIF = "rog_vision_demo.gif";

        private static float GET_FPS_ASYNC_TIMEOUT = 5;
        private static Color AuraLightOffColor = new Color(105, 105, 105, 0.2f);
        private static readonly Dictionary<AuraLightManager.AuraRateEnum, float> EffectBreathTable =
            new Dictionary<AuraLightManager.AuraRateEnum, float>()
            {
            { AuraLightManager.AuraRateEnum.RATE_SLOW, 5.64f },
            { AuraLightManager.AuraRateEnum.RATE_MEDIUM, 3.76f },
            { AuraLightManager.AuraRateEnum.RATE_FAST, 1.88f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_1, 1.665f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_2, 1.45f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_3, 1.235f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_4, 1.02f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_5, 0.805f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_6, 0.59f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_7, 0.376f },
            };
        private static readonly Dictionary<AuraLightManager.AuraRateEnum, float> EffectStroblingTable =
            new Dictionary<AuraLightManager.AuraRateEnum, float>()
            {
            { AuraLightManager.AuraRateEnum.RATE_SLOW, 2f },
            { AuraLightManager.AuraRateEnum.RATE_MEDIUM, 1.75f },
            { AuraLightManager.AuraRateEnum.RATE_FAST, 1.5f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_1, 1.315f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_2, 1.13f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_3, 0.945f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_4, 0.76f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_5, 0.575f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_6, 0.39f },
            { AuraLightManager.AuraRateEnum.RATE_FAST_7, 0.2f },
            };
        private static readonly Dictionary<AuraLightManager.AuraRateEnum, float> EffectColorCycleTable =
            new Dictionary<AuraLightManager.AuraRateEnum, float>()
            {
            { AuraLightManager.AuraRateEnum.RATE_SLOW, 8.32f },
            { AuraLightManager.AuraRateEnum.RATE_MEDIUM, 5.12f },
            { AuraLightManager.AuraRateEnum.RATE_FAST, 2.56f }
            };
        private static readonly Dictionary<AuraLightManager.AuraEffectEnum, Dictionary<AuraLightManager.AuraRateEnum, float>> AuraMappingTable =
            new Dictionary<AuraLightManager.AuraEffectEnum, Dictionary<AuraLightManager.AuraRateEnum, float>>()
            {
            { AuraLightManager.AuraEffectEnum.EFFECT_BREATHING, EffectBreathTable },
            { AuraLightManager.AuraEffectEnum.EFFECT_STROBING, EffectStroblingTable },
            { AuraLightManager.AuraEffectEnum.EFFECT_COLOR_CYCLE, EffectColorCycleTable }
            };

        private float animationTimer = 0f;

        void Update()
        {
            Toggle toggle = GameObject.Find("TabAura").GetComponent<Toggle>();
            if (toggle.isOn && GetTargetId() != AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION)
            {
                AuraLightAnimator(GetEffectId(), GetTargetId(), GetEffectRateId());
            }
        }

        void Start()
        {
            InitDisplayFpsText();
        }

        public void ChangeFps()
        {
            FpsManager fpsManager = GameObject.Find("AsusFpsManager").GetComponent<FpsManager>();
            Text text = GameObject.Find("TextDisplayFps").GetComponent<Text>();
            FpsManager.FpsEnum fpsEnum = GetFpsValue();
            fpsManager.SetFps(fpsEnum);
            GetFpsAsync(fpsManager, text, (int)fpsEnum);
        }

        private void InitDisplayFpsText()
        {
            FpsManager fpsManager = GameObject.Find("AsusFpsManager").GetComponent<FpsManager>();
            Text displayFpsText = GameObject.Find("TextDisplayFps").GetComponent<Text>();
            GetFpsAsync(fpsManager, displayFpsText, Screen.currentResolution.refreshRate);
        }

        private void GetFpsAsync(FpsManager fpsManager, Text displayFpsText, int selectedFps)
        {
            object[] args = new object[] { fpsManager, displayFpsText, selectedFps };
            StopCoroutine("GetFpsCoroutine");
            StartCoroutine("GetFpsCoroutine", args);
        }

        private IEnumerator GetFpsCoroutine(object[] args)
        {
            FpsManager fpsManager = ((FpsManager)args[0]);
            if (fpsManager == null)
            {
                Debug.Log("Get Fps failed, reason: FpsManager == null");
                yield break;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            int[] availableFpsList = fpsManager.GetAvailableFpsList();
            if (availableFpsList == null)
            {
                Debug.Log("Get Fps failed, reason: no available Fps list");
                yield break;
            }

            int selectedFps = ((int)args[2]);
            int targetFps = (selectedFps == int.MaxValue) ? availableFpsList[availableFpsList.Length - 1] : selectedFps;
            if (availableFpsList != null && !Array.Exists(availableFpsList, element => element == targetFps))
            {
                Debug.Log("Get Fps failed, reason: unavailable fps (" + targetFps + ")");
                yield break;
            }
#else
            int targetFps = Screen.currentResolution.refreshRate;
#endif

            float timeoutTime = Time.realtimeSinceStartup + GET_FPS_ASYNC_TIMEOUT;
            yield return new WaitUntil(
                () => (targetFps == fpsManager.GetFps()) || (Time.realtimeSinceStartup > timeoutTime));

            int currentFps = fpsManager.GetFps();
            if (currentFps == targetFps)
            {
                Debug.Log("Get Fps successfully");
            }
            else
            {
                Debug.Log("Get Fps timeout");
            }
            Text displayFpsText = ((Text)args[1]);
            if (displayFpsText != null)
            {
                displayFpsText.text = "Display Fps: " + currentFps.ToString();
            }
        }

        public void StartLighting()
        {
            AuraLightManager auraLightManager = GameObject.Find("AsusAuraLightManager").GetComponent<AuraLightManager>();
            if (GetTargetId() != AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION)
            {
                auraLightManager.StartAuraLight(GetTargetId(), GetEffectId(), GetColor(), GetEffectRateId(), GetEffectDuration());
            }
            else
            {
                auraLightManager.StartRogVision(GetDisplayFileName(), GetRepeatCount());
            }
        }

        public static T GetEnumValueByIndex<T>(int enumIndex)
        {
            return GetEnumValueByIndex<T>(enumIndex, null);
        }

        public static T GetEnumValueByIndex<T>(int enumIndex, Comparison<T> comparison)
        {
            T[] options = (T[])Enum.GetValues(typeof(T));
            if (comparison != null)
            {
                Array.Sort(options, comparison);
            }
            return (T)options.GetValue(enumIndex);
        }

        private FpsManager.FpsEnum GetFpsValue()
        {
            int index = GameObject.Find("DropdownFps").GetComponent<Dropdown>().value;
            return GetEnumValueByIndex<FpsManager.FpsEnum>(index);
        }

        private AuraLightManager.AuraEffectEnum GetEffectId()
        {
            int index = GameObject.Find("DropdownEffect").GetComponent<Dropdown>().value;
            return GetEnumValueByIndex<AuraLightManager.AuraEffectEnum>(index);
        }

        private AuraLightManager.AuraTargetEnum GetTargetId()
        {
            int index = GameObject.Find("DropdownTarget").GetComponent<Dropdown>().value;
            return GetEnumValueByIndex<AuraLightManager.AuraTargetEnum>(index);
        }

        private AuraLightManager.AuraRateEnum GetEffectRateId()
        {
            int index = GameObject.Find("DropdownRate").GetComponent<Dropdown>().value;
            return GetEnumValueByIndex(index, new Comparison<AuraLightManager.AuraRateEnum>((i1, i2) => i2.CompareTo(i1)));
        }

        private int GetEffectDuration()
        {
            Slider slider = GameObject.Find("ContentAuraLight").GetComponentInChildren<Slider>();
            return (int)slider.value;
        }

        private String GetDisplayFileName()
        {
            return ROG_VISION_GIF;
        }

        private int GetRepeatCount()
        {
            Slider slider = GameObject.Find("SliderRepeatCount").GetComponent<Slider>();
            return (int)slider.value;
        }

        public void StartColorPicker()
        {
            AuraLightManager auraLightManager = GameObject.Find("AsusAuraLightManager").GetComponent<AuraLightManager>();
            auraLightManager.StartColorPicker();
        }

#if UNITY_ANDROID
        [Preserve /* Invoke from Android */]
        private void OnAuraLightColorUpdated(string color)
        {
            Debug.Log("WidgetManager.OnAuraLightColorUpdated: color (" + color + ")");
            AuraLightManager auraLightManager = GameObject.Find("AsusAuraLightManager").GetComponent<AuraLightManager>();
            if (auraLightManager != null)
            {
                auraLightManager.color = ColorUtil.IntToColor(int.Parse(color));
            }
        }
#endif

        public void Boost()
        {
            PerformanceManager performanceManager = GameObject.Find("AsusPerformanceManager").GetComponent<PerformanceManager>();
            Slider slider = GameObject.Find("ContentPerformance").GetComponentInChildren<Slider>();
            performanceManager.Boost((int)slider.value);
        }

        private void AuraLightAnimator(AuraLightManager.AuraEffectEnum mode, AuraLightManager.AuraTargetEnum target, AuraLightManager.AuraRateEnum rateId)
        {
            float period = GetModePeriod(mode, rateId);

            if (animationTimer >= period)
            {
                animationTimer = 0;
            }
            else
            {
                animationTimer += Time.deltaTime;
                Image auraLight = GameObject.Find("AuraLight").GetComponent<Image>();
                Image rgbLight = GameObject.Find("RgbLight").GetComponent<Image>();
                Color shownColor = (mode != AuraLightManager.AuraEffectEnum.EFFECT_OFF) ?
                    ColorUtil.IntToColor(GetColor(), GetModeAlpha(mode, period, animationTimer)) : AuraLightOffColor;
                switch (target)
                {
                    case AuraLightManager.AuraTargetEnum.TARGET_FOLLOW_SYSTEM:
                    case AuraLightManager.AuraTargetEnum.TARGET_LOGO:
                        auraLight.color = shownColor;
                        rgbLight.color = AuraLightOffColor;
                        break;
                    case AuraLightManager.AuraTargetEnum.TARGET_LOGO_RGB:
                        auraLight.color = shownColor;
                        rgbLight.color = shownColor;
                        break;
                    case AuraLightManager.AuraTargetEnum.TARGET_RGB:
                        auraLight.color = AuraLightOffColor;
                        rgbLight.color = shownColor;
                        break;
                    case AuraLightManager.AuraTargetEnum.TARGET_ROG_VISION:
                        auraLight.color = AuraLightOffColor;
                        rgbLight.color = AuraLightOffColor;
                        break;
                }
            }
        }

        private int GetColor()
        {
            AuraLightManager auraLightManager = GameObject.Find("AsusAuraLightManager").GetComponent<AuraLightManager>();
            return int.Parse(ColorUtility.ToHtmlStringRGB(auraLightManager.color), System.Globalization.NumberStyles.HexNumber);
        }

        private float GetModeAlpha(AuraLightManager.AuraEffectEnum mode, float period, float timer)
        {
            float scaledTimer = timer / period;
            switch (mode)
            {
                case AuraLightManager.AuraEffectEnum.EFFECT_STATIC:
                    return 1f;
                case AuraLightManager.AuraEffectEnum.EFFECT_BREATHING:
                    float alphaMagnification = Mathf.Abs((scaledTimer - 0.5f) * 2.95f - 0.2375f);
                    if (alphaMagnification > 1)
                    {
                        alphaMagnification = 1;
                    }
                    if (alphaMagnification < 0)
                    {
                        alphaMagnification = 0;
                    }
                    return alphaMagnification;
                case AuraLightManager.AuraEffectEnum.EFFECT_STROBING:
                    return (scaledTimer > 0.5) ? 1 : 0;
                case AuraLightManager.AuraEffectEnum.EFFECT_COLOR_CYCLE:
                    return 1f;
                default:
                    return 0f;
            }
        }

        private float GetModePeriod(AuraLightManager.AuraEffectEnum mode, AuraLightManager.AuraRateEnum rate)
        {
            if (AuraMappingTable.ContainsKey(mode))
            {
                if (AuraMappingTable[mode].ContainsKey(rate))
                {
                    return AuraMappingTable[mode][rate];
                }
            }
            return 2f; // default period
        }

        public void SwitchScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}