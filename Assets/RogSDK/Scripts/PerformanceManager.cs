using UnityEngine;

namespace RogPhoneSdk
{
    public class PerformanceManager : MonoBehaviour
    {
        public static readonly int BoostDurationMin = 1; // ms
        public static readonly int BoostDurationMax = 1000; // ms

        [Range(1 /* BoostDurationMin */, 1000 /* BoostDurationMax */)]
        public int durationMillis;

#if UNITY_ANDROID
        private AndroidJavaObject unityActivity;
        private AndroidJavaObject performanceHelper;
#endif

        void Awake()
        {
#if UNITY_ANDROID
            AndroidJavaClass unityPlayerStatic = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayerStatic.GetStatic<AndroidJavaObject>("currentActivity");
            performanceHelper = new AndroidJavaObject("com.asus.rogplugin.PerformanceHelper");
#endif
        }

        public void Boost(int durationMillis)
        {
            this.durationMillis = durationMillis;
            Boost();
        }

        public void Boost()
        {
            Debug.Log("PerformanceManager: duration = " + durationMillis);
            if (!IsValidDuration(durationMillis))
            {
                Debug.Log("duration should be in the range of (" + BoostDurationMin + ", " + BoostDurationMax + ")");
                return;
            }
#if UNITY_ANDROID
            performanceHelper?.CallStatic("boost", unityActivity, durationMillis);
#endif
        }

        private bool IsValidDuration(int durationMillis)
        {
            return (durationMillis <= BoostDurationMax) && (durationMillis >= BoostDurationMin);
        }
    }
}