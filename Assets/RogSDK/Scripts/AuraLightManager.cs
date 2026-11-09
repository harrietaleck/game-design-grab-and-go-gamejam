using UnityEngine;

namespace RogPhoneSdk
{
    public class AuraLightManager : MonoBehaviour
    {
        public static readonly int AuraLightDurationMin = 1; // ms
        public static readonly int AuraLightDurationMax = 60000; // ms

        public Color color;
        [Range(1 /* AuraLightDurationMin */, 60000 /* AuraLightDurationMax */)]
        public int durationMillis;
        public AuraEffectEnum effectId = AuraEffectEnum.EFFECT_OFF;
        public AuraTargetEnum targetId = AuraTargetEnum.TARGET_FOLLOW_SYSTEM;
        public AuraRateEnum rateId = AuraRateEnum.RATE_SLOW;
        public string displayFileName;
        public int repeatCount;

#if UNITY_ANDROID
        private AndroidJavaObject unityActivity;
        private AndroidJavaObject auraLightHelper;
#endif
        private int[] availableTargetList = null;

        public enum AuraEffectEnum
        {
            EFFECT_OFF = 0,
            EFFECT_STATIC = 1,
            EFFECT_BREATHING = 2,
            EFFECT_STROBING = 3,
            EFFECT_COLOR_CYCLE = 4
        };

        public enum AuraTargetEnum
        {
            TARGET_FOLLOW_SYSTEM = 0x00,
            TARGET_LOGO = 0x01,
            TARGET_RGB = 0x04,
            TARGET_LOGO_RGB = TARGET_LOGO | TARGET_RGB,
            TARGET_ROG_VISION = -1
        }

        public enum AuraRateEnum
        {
            RATE_SLOW = 0,
            RATE_MEDIUM = -1,
            RATE_FAST = -2,
            RATE_FAST_1 = -3,
            RATE_FAST_2 = -4,
            RATE_FAST_3 = -5,
            RATE_FAST_4 = -6,
            RATE_FAST_5 = -7,
            RATE_FAST_6 = -8,
            RATE_FAST_7 = -9
        };

        void Awake()
        {
#if UNITY_ANDROID
            AndroidJavaClass unityPlayerStatic = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayerStatic.GetStatic<AndroidJavaObject>("currentActivity");
            auraLightHelper = new AndroidJavaObject("com.asus.rogplugin.AuraLightHelper");
            availableTargetList = auraLightHelper.CallStatic<int[]>("getAvailableTargetList", unityActivity);
            auraLightHelper.CallStatic("init", unityActivity);
#endif
        }

        void OnDestroy()
        {
#if UNITY_ANDROID
            auraLightHelper?.CallStatic("destroy", unityActivity);
#endif
        }

        public void StartAuraLight(AuraTargetEnum targetId, AuraEffectEnum effectId, int color, AuraRateEnum rateId, int durationMillis)
        {
            this.targetId = targetId;
            this.effectId = effectId;
            this.color = ColorUtil.IntToColor(color);
            this.rateId = rateId;
            this.durationMillis = durationMillis;
            StartAuraLight();
        }

        public void StartAuraLight()
        {
            Debug.Log("AuraLightManager.StartAuraLight: targetId (" + targetId + "), effectId (" + effectId + "), color(" + color + "), rateId (" + rateId + "), durationMillis (" + durationMillis + ")");
            if (IsValidDuration(durationMillis))
            {
#if UNITY_ANDROID
                auraLightHelper?.CallStatic("startAuraLight", unityActivity, (int)targetId, (int)effectId,
                    int.Parse(ColorUtility.ToHtmlStringRGB(color), System.Globalization.NumberStyles.HexNumber), (int)rateId, durationMillis);
#endif
            }
        }

        public void StartRogVision(string displayFileName, int repeatCount)
        {
            this.targetId = AuraTargetEnum.TARGET_ROG_VISION;
            this.displayFileName = displayFileName;
            this.repeatCount = repeatCount;
            StartRogVision();
        }

        public void StartRogVision()
        {
            Debug.Log("AuraLightManager.StartRogVision: targetId (" + targetId + "), displayFileName (" + displayFileName + ")" +
                ", repeatCount (" + repeatCount + ")");
#if UNITY_ANDROID
                auraLightHelper?.CallStatic("startRogVision", unityActivity, displayFileName, repeatCount);
#endif
        }

        public void StartColorPicker()
        {
            Debug.Log("AuraLightManager StartColorPicker");
#if UNITY_ANDROID
            auraLightHelper?.CallStatic("startColorPicker", unityActivity);
#endif
        }

        private bool IsValidDuration(int durationMillis)
        {
            return (durationMillis <= AuraLightDurationMax) && (durationMillis >= AuraLightDurationMin);
        }

        public int[] GetAvailableTargetList()
        {
            return availableTargetList;
        }
    }
}