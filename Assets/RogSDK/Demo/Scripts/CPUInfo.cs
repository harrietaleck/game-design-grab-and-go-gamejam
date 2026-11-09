using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class CPUInfo : MonoBehaviour
    {
#if UNITY_ANDROID
        private AndroidJavaObject unityActivity;
        private AndroidJavaObject cpuInfoHelper;
#endif
        private Text textLabel;
        private int nextUpdate = 1;

        void Awake()
        {
#if UNITY_ANDROID
            AndroidJavaClass unityPlayerStatic = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayerStatic.GetStatic<AndroidJavaObject>("currentActivity");
            cpuInfoHelper = new AndroidJavaObject("com.asus.rogplugin.CpuInfoHelper");
#endif
            textLabel = GameObject.Find("TextCpuFrequency").GetComponent<Text>();
        }

        // Update is called once per frame
        void Update()
        {
            if (Time.time >= nextUpdate)
            {
                nextUpdate = Mathf.FloorToInt(Time.time) + 1;
#if UNITY_ANDROID
                textLabel.text = cpuInfoHelper.CallStatic<string>("getBoostedCpuUsage", unityActivity);
#endif
            }

        }
    }
}
