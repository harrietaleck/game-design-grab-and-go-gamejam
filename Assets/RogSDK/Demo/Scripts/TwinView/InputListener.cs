using RogPhoneSdk;
using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class InputListener : MonoBehaviour
    {
        public TwinViewManager twinViewManager;
        public Text primaryScreenLabel;
        public Text secondaryScreenLabel;


#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
        void OnEnable()
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        }

        void OnDisable()
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
        }
#endif

        // Update is called once per frame
        void Update()
        {
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                UnityEngine.InputSystem.EnhancedTouch.Touch t =
                    UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];

                if (twinViewManager.GetTouchDisplayIndex(t.screenPosition) <= 0)
                {
                    if (twinViewManager.ContentIsSwapped)
                        secondaryScreenLabel.text = t.screenPosition + " : " + t.startScreenPosition;
                    else
                        primaryScreenLabel.text = t.screenPosition + " : " + t.startScreenPosition;
                }
                else
                {
                    if (twinViewManager.ContentIsSwapped)
                        primaryScreenLabel.text = t.screenPosition + " : " + t.startScreenPosition;
                    else
                        secondaryScreenLabel.text = t.screenPosition + " : " + t.startScreenPosition;
                }
            }
#else
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);

                if (twinViewManager.GetTouchDisplayIndex(t.position) <= 0)
                {
                    if (twinViewManager.ContentIsSwapped)
                        secondaryScreenLabel.text = t.position + " : " + t.rawPosition;
                    else
                        primaryScreenLabel.text = t.position + " : " + t.rawPosition;
                }
                else
                {
                    if (twinViewManager.ContentIsSwapped)
                        primaryScreenLabel.text = t.position + " : " + t.rawPosition;
                    else
                        secondaryScreenLabel.text = t.position + " : " + t.rawPosition;
                }
            }
#endif
        }
    }
}