using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RogPhoneSdk
{
    public class SetupChecker : AssetPostprocessor
    {
        private static readonly string ASSETS_NAME = "RogSDK";
        private static readonly int ACCEPTABLE_MAJOR_VERSION = 2018;
        private static readonly int ACCEPTABLE_MINOR_VERSION = 4;

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string importedAsset in importedAssets)
            {
                if (importedAsset.Equals("Assets/" + ASSETS_NAME))
                {
                    if (GraphicsSettings.defaultRenderPipeline != null)
                    {
                        string message1 = "ROG Phone SDK was successfully initialized.\n\n";
                        string message2 = "We noticed you have enabled a Scriptable Render Pipeline."
                            + "\nPlease note that ROG Phone SDK's TwinView (multi-display) feature is not supported with any Scriptable Render Pipeline "
                            + "(Universal Render Pipeline (URP), Light Weight Render Pipeline (LWRP) or High Definition Render Pipeline (HDRP))."
                            + "\nTo ensure a smooth experience, please switch to Unity's Built-in Standard Render Pipeline.";
                        EditorUtility.DisplayDialog("Notice", message1 + message2, "OK");
                        Debug.LogWarning(message2);
                    }
                    else
                    {
                        string unityVersion = Application.unityVersion;
                        if (!IsRunningOnAcceptableUnityVersion(unityVersion))
                        {
                            string message1 = "ROG Phone SDK was successfully initialized.\n\n";
                            string message2 = "Please note: ROG Phone SDK is not supported with your current version of Unity"
                                + " (" + unityVersion + ")." + "\nTo ensure a smooth experience, please switch to Unity version "
                                + ACCEPTABLE_MAJOR_VERSION + "." + ACCEPTABLE_MINOR_VERSION
                                + " or above and make sure to utilize Unity's Built-in Standard Render Pipeline.";
                            EditorUtility.DisplayDialog("Notice", message1 + message2, "OK");
                            Debug.LogWarning(message2);
                        }
                    }
                }
            }
        }

        private static bool IsRunningOnAcceptableUnityVersion(string unityVersion)
        {
            string[] splitUnityVersion = unityVersion.Split('.');
            return splitUnityVersion.Length >= 2 && IsRunningOnAcceptableUnityVersion(int.Parse(splitUnityVersion[0]));
        }

        private static bool IsRunningOnAcceptableUnityVersion(int majorVersion)
        {
            return majorVersion >= ACCEPTABLE_MAJOR_VERSION;
        }
    }
}