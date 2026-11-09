using UnityEngine;

namespace RogPhoneSdk
{
    public class FpsManager : MonoBehaviour
    {
        public FpsEnum fps = FpsEnum.FPS_MAX;

#if UNITY_ANDROID
        private AndroidJavaObject unityActivity;
        private AndroidJavaObject fpsHelper;
#endif
        private int[] availableFpsList = null;

        public enum FpsEnum
        {
            FPS_60 = 60,
            FPS_90 = 90,
            FPS_120 = 120,
            FPS_144 = 144,
            FPS_MAX = int.MaxValue
        };

        void Awake()
        {
#if UNITY_ANDROID
            AndroidJavaClass unityPlayerStatic = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayerStatic.GetStatic<AndroidJavaObject>("currentActivity");
            fpsHelper = new AndroidJavaObject("com.asus.rogplugin.FpsControlHelper");
            availableFpsList = fpsHelper.CallStatic<int[]>("getAvailableFpsList", unityActivity);
#endif
        }

        public void SetFps(FpsEnum fps)
        {
            this.fps = fps;
            SetFps();
        }

        public void SetFps()
        {
            Debug.Log("FpsManager.SetFps: fps (" + fps + ")");
#if UNITY_ANDROID
            fpsHelper?.CallStatic("setFps", unityActivity, (int)fps);
#endif
        }

        public int GetFps()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return fpsHelper?.CallStatic<int>("getFps", unityActivity) ?? -1;
#else
            return Screen.currentResolution.refreshRate;
#endif
        }

        public int[] GetAvailableFpsList()
        {
            return availableFpsList;
        }
    }
}