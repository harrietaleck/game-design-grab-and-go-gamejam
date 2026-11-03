#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Mkey
{
    [InitializeOnLoad]
    internal static class Promo
    {
        private const int loadsToShow = 3;
        const string showKey = "promo_shown_easytext200";   
        const string loadsCounterKey = "promo_loads_counter_easytext200"; 

        static Promo()
        {
            // EditorApplication.delayCall += ShowMessage; return;  // test    
            if (EditorPrefs.GetBool(showKey)) return;       
            int loads = EditorPrefs.GetInt(loadsCounterKey, 0);
            if (loads < loadsToShow)
            {
                loads++;
                EditorPrefs.SetInt(loadsCounterKey, loads);
                return;
            }
            else if(loads == loadsToShow)
            {
                loads++;
                EditorPrefs.SetInt(loadsCounterKey, loads);
                EditorApplication.delayCall += ShowMessage;    
            }
        } 

        static void ShowMessage()
        {
            if( EditorDialog.DisplayDecisionDialog(
                "Easy Text is part of the MK game development workflow.",
                "If you need complete Unity game templates with ready UI, source code, mobile/WebGL support, and polished graphics, you can explore our portfolio.",
                "Portfolio",
                "Close",
                DialogIconType.Info))
            {
                Application.OpenURL(
                    "https://assetstore.unity.com/publishers/25903"); 
            }
            EditorPrefs.SetBool(showKey, true);
        }
    }
}
#endif
