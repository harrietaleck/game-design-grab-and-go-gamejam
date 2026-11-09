using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace RogPhoneSdk
{
    public class TwinViewManager : MonoBehaviour
    {
        public static readonly int PrimaryDisplayIndex = 0;
        public static readonly int SecondaryDisplayIndex = 1;

        private static readonly Vector2 SecondaryTouchOffset = new Vector2(10000f, 0);

        public List<Camera> secondaryCameras = new List<Camera>();
        public TwinViewGraphicsRaycaster[] primaryCanvasRayCasters;
        public TwinViewGraphicsRaycaster[] secondaryCanvasRayCasters;
        public UnityEvent onDisplay2Attached;
        public UnityEvent onDisplay2Detached;
        public bool ContentIsSwapped { get; set; }

#if UNITY_ANDROID
        private AndroidJavaObject unityActivity;
        private AndroidJavaClass twinViewHelper;
#endif
        private List<Camera> primaryCameras = new List<Camera>();
        private bool isTwinViewDocking = false;

        void Awake()
        {
#if UNITY_ANDROID
            AndroidJavaClass unityPlayerStatic = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayerStatic.GetStatic<AndroidJavaObject>("currentActivity");
#endif

            GraphicsDeviceType graphicsDeviceType = SystemInfo.graphicsDeviceType;
            if (graphicsDeviceType.Equals(GraphicsDeviceType.OpenGLES3)
                || graphicsDeviceType.Equals(GraphicsDeviceType.OpenGLES2))
            {
#if UNITY_ANDROID
                twinViewHelper = new AndroidJavaClass("com.asus.rogplugin.twinview.TwinViewHelper");
                twinViewHelper.CallStatic("init", unityActivity, "mUnityPlayer", SecondaryTouchOffset.x);
#endif
            }
            else
            {
                Debug.LogWarning("TwinViewManager only supports following graphic device type: OpenGLES3, OpenGLES2" +
                    ", current graphic device type = " + graphicsDeviceType);
            }

            Camera[] allCameras = Resources.FindObjectsOfTypeAll<Camera>();
            float minCameraDepth = float.MaxValue;
            float maxCameraDepth = float.MinValue;
            foreach (Camera camera in allCameras)
            {
                if (!secondaryCameras.Contains(camera))
                {
                    primaryCameras.Add(camera);
                    camera.SetTargetBuffers(GetPrimaryDisplay().colorBuffer, GetPrimaryDisplay().depthBuffer);
                    if (camera.depth < minCameraDepth) minCameraDepth = camera.depth;
                    if (camera.depth > maxCameraDepth) maxCameraDepth = camera.depth;
                }
            }
            minCameraDepth -= 1f;
            maxCameraDepth += 1f;
            float cameraDepthRange = Mathf.Abs(maxCameraDepth - minCameraDepth);

            //secondary camera depth has to be lower than primary camera's depth so that GUI will be on primary camera's screen
            foreach (Camera secondaryCamera in secondaryCameras)
            {
                secondaryCamera.depth -= cameraDepthRange;
                secondaryCamera.enabled = false;
            }
            SetCanvasRayCasterOffset();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            Debug.Log("OnApplcationPause: " + pauseStatus);
#if UNITY_ANDROID
            twinViewHelper?.CallStatic("onApplicationPause", unityActivity, pauseStatus);
#endif
        }

        void OnDestroy()
        {
#if UNITY_ANDROID
            twinViewHelper?.CallStatic("destroy", unityActivity);
#endif
        }

#if UNITY_ANDROID
        [Preserve /* Invoke from Android */]
        private void SecondaryDisplayAdded(string displayId)
        {
            Debug.Log("SecondaryDisplayAdded: " + displayId);
            StartCoroutine("SecondaryDisplayAddedCoroutine");
        }

        [Preserve /* Invoke from Android */]
        private void SecondaryDisplayRemoved(string displayId)
        {
            Debug.Log("SecondaryDisplayRemoved: " + displayId);
            RevertDisplayContent();

            foreach (Camera camera in secondaryCameras)
            {
                camera.enabled = false;
            }
            isTwinViewDocking = false;
            onDisplay2Detached.Invoke();
        }
#endif

        public void SwapDisplayContent()
        {
            Debug.Log("SwapDisplayContent");
            if (Display.displays.Length <= 1) return;
            if (!isTwinViewDocking)
            {
                Debug.Log("ignore swap display content request because there has no TwinView Dock");
                return;
            }

            ContentIsSwapped = !ContentIsSwapped;

            foreach (Camera primaryCamera in primaryCameras)
            {
                primaryCamera.SetTargetBuffers(
                    ContentIsSwapped ? GetSecondaryDisplay().colorBuffer : GetPrimaryDisplay().colorBuffer,
                    ContentIsSwapped ? GetSecondaryDisplay().depthBuffer : GetPrimaryDisplay().depthBuffer);
            }
            foreach (Camera secondaryCamera in secondaryCameras)
            {
                secondaryCamera.SetTargetBuffers(
                    ContentIsSwapped ? GetPrimaryDisplay().colorBuffer : GetSecondaryDisplay().colorBuffer,
                    ContentIsSwapped ? GetPrimaryDisplay().depthBuffer : GetSecondaryDisplay().depthBuffer);
            }

            SetCanvasRayCasterOffset(ContentIsSwapped);
        }

        public int GetTouchDisplayIndex(Vector2 input)
        {
            if (Display.displays.Length <= 1) return 0;

            if (input.x >= SecondaryTouchOffset.x) return 1;

            return 0;
        }

        private void RevertDisplayContent()
        {
            Debug.Log("RevertDisplayContent");
            ContentIsSwapped = false;

            foreach (Camera primaryCamera in primaryCameras)
            {
                primaryCamera.SetTargetBuffers(GetPrimaryDisplay().colorBuffer, GetPrimaryDisplay().depthBuffer);
            }

            SetCanvasRayCasterOffset();
        }

        private void SetCanvasRayCasterOffset(bool contentIsSwapped = false)
        {
            foreach (TwinViewGraphicsRaycaster twinViewGraphicsRaycaster in primaryCanvasRayCasters)
            {
                twinViewGraphicsRaycaster.MinInputPosition = contentIsSwapped ? SecondaryTouchOffset : Vector2.zero;
            }
            foreach (TwinViewGraphicsRaycaster twinViewGraphicsRaycaster in secondaryCanvasRayCasters)
            {
                twinViewGraphicsRaycaster.MinInputPosition = contentIsSwapped ? Vector2.zero : SecondaryTouchOffset;
            }
        }

        private Display GetPrimaryDisplay()
        {
            return Display.displays[PrimaryDisplayIndex];
        }

        private Display GetSecondaryDisplay()
        {
            return Display.displays[SecondaryDisplayIndex];
        }

        private IEnumerator SecondaryDisplayAddedCoroutine()
        {
            yield return new WaitForEndOfFrame();

            Debug.Log("SecondaryDisplayAddedCoroutine");
            foreach (Camera secondaryCamera in secondaryCameras)
            {
                secondaryCamera.SetTargetBuffers(GetSecondaryDisplay().colorBuffer, GetSecondaryDisplay().depthBuffer);
                secondaryCamera.enabled = true;
            }
            isTwinViewDocking = true;
            onDisplay2Attached.Invoke();

            yield break;
        }
    }
}