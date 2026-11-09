using RogPhoneSdk;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogPhoneSdkDemo
{
    public class Logger : MonoBehaviour
    {
        private Dictionary<string, Color32> indicatorColorDict = new Dictionary<string, Color32>();
        private Vector2 initLeftPosition;
        private Vector2 initRightPosition;
        private float stickPadSize;

        public void SetUpStick()
        {
            string indicatorLeftStick = GetIndicatorName(EventInfoUtils.InputControlToString(GetShownGamepad(), InputControlEnum.LEFT_STICK));
            string indicatorRightStick = GetIndicatorName(EventInfoUtils.InputControlToString(GetShownGamepad(), InputControlEnum.RIGHT_STICK));
            initLeftPosition = new Vector2(GameObject.Find(indicatorLeftStick).transform.position.x, GameObject.Find(indicatorLeftStick).transform.position.y);
            initRightPosition = new Vector2(GameObject.Find(indicatorRightStick).transform.position.x, GameObject.Find(indicatorRightStick).transform.position.y);
            stickPadSize = GameObject.Find(indicatorLeftStick).GetComponent<RectTransform>().rect.x;
        }

        public void OnButtonClick(KeyEventInfoWrapper info)
        {
            if (!IsCorrectSource(info)) return;
#if DEVELOPMENT_BUILD
            Debug.Log("OnButtonClick, " + info.ToString());
#endif
            SetLogger(EventInfoUtils.InputControlToString(info.gamepad, info.inputControl), info.phase, info.buttonValue);
        }

        public void OnJoystickMove(MotionEventInfoWrapper info)
        {
            if (!IsCorrectSource(info)) return;
#if DEVELOPMENT_BUILD
            Debug.Log("OnJoystickMove, " + info.ToString());
#endif
            MoveJoystickPosition(EventInfoUtils.InputControlToString(info.gamepad, info.inputControl), info.axes, IsLeftStick(info));
        }

        private bool IsCorrectSource(EventInfoWrapper info)
        {
            GamepadEnum shownGamepad = GetShownGamepad();
            if (IsGamepadShown(info, shownGamepad))
            {
                return true;
            }
            else
            {
                Debug.Log("Source incorrect, shownGamepad=" + shownGamepad + " / connectedGamepad=" + info.gamepad);
                return false;
            }
        }

        private GamepadEnum GetShownGamepad()
        {
            int idx = GameObject.Find("GamepadDropdown").GetComponent<Dropdown>().value;
            return WidgetManager.GetEnumValueByIndex<GamepadEnum>(idx);
        }

        private bool IsGamepadShown(EventInfoWrapper info, GamepadEnum shownGamepad)
        {
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
            if (info.IsRogKunaiGamepadKeyboard(shownGamepad)) { return true; }
            if (info.IsRogKunai3GamepadKeyboard(shownGamepad)) { return true; }
#endif
            return shownGamepad == info.gamepad;
        }

        private bool IsLeftStick(EventInfoWrapper info) { return info.inputControl == InputControlEnum.LEFT_STICK; }

        private void SetLogger(string inputControlName, string state, float value)
        {
            string indicatorName = GetIndicatorName(inputControlName);
            GameObject indicator = GameObject.Find(indicatorName);
            if (indicator == null) return; // return if game object is deactive or not found

            // keep defaultColor of indicator in indicatorColorDict
            if (!indicatorColorDict.ContainsKey(indicatorName))
            {
                indicatorColorDict[indicatorName] = indicator.GetComponent<Image>().color;
            }
            string log = Environment.NewLine + inputControlName + ":" + GetStateRichText(indicatorName, state) + ", value=" + value.ToString();
            SetIndicatorColor(indicatorName, state);
            GameObject.Find("LogRecord").GetComponent<Text>().text += log;
            GameObject.Find("Scroll View").GetComponent<ScrollRect>().verticalNormalizedPosition = 0.0f; // move to bottom of scroll view
        }

        private void MoveJoystickPosition(string stickName, Vector2 vec, bool isLeft)
        {
            string axis1;
            string axis2;
            float offsetX = vec.x * stickPadSize;
            float offsetY = vec.y * stickPadSize;
            Vector2 initPos;
            if (isLeft)
            {
                axis1 = "axisX";
                axis2 = "axisY";
                initPos = initLeftPosition;
            }
            else
            {
                axis1 = "axisZ";
                axis2 = "axisRZ";
                initPos = initRightPosition;
            }
            GameObject.Find(axis1).GetComponent<Text>().text = axis1 + ": " + vec.x;
            GameObject.Find(axis2).GetComponent<Text>().text = axis2 + ": " + vec.y;
            GameObject.Find(GetIndicatorName(stickName)).transform.position = new Vector2(initPos.x - offsetX, initPos.y + offsetY);
        }

        private IEnumerator SetDefaultColor(string indicatorName)
        {
            yield return new WaitForSeconds(0.1f);
            SetIndicatorColor(indicatorName, null);
        }

        private string GetIndicatorName(string actionName)
        {
            return "Indicator - " + actionName;
        }

        private string GetStateRichText(string indicator, string state)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGBA(GetIndicatorColor(indicator, state)) + ">" + state + "</color>";
        }

        private Color32 GetIndicatorColor(string indicator, string state)
        {
            if (state != null)
            {
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
                switch (state)
                {
                    case nameof(InputActionPhase.Started):
                        return Color.green;
                    case nameof(InputActionPhase.Performed):
                        return Color.blue;
                    case nameof(InputActionPhase.Canceled):
                        return Color.red;
                    default:
                        return indicatorColorDict[indicator];
                }
#else
                switch (state)
                {
                    case nameof(PhaseEnum.PRESS):
                        return Color.green;
                    case nameof(PhaseEnum.HOLD):
                        return Color.blue;
                    case nameof(PhaseEnum.RELEASE):
                        return Color.red;
                    default:
                        return indicatorColorDict[indicator];
                }
#endif
            }
            return indicatorColorDict[indicator];
        }

        public void OnClickCleanScrollView()
        {
            GameObject.Find("LogRecord").GetComponent<Text>().text = "";
        }

        private void SetIndicatorColor(string indicator, string state)
        {
            var indicatorObject = GameObject.Find(indicator);
            if (indicatorObject)
            {
                indicatorObject.GetComponent<Image>().color = GetIndicatorColor(indicator, state);
            }
            // delay to set indicator to default color to lengthen release effect
#if UNITY_2019_3_OR_NEWER && ENABLE_INPUT_SYSTEM
            if (state == nameof(InputActionPhase.Canceled))
#else
            if (state == nameof(PhaseEnum.RELEASE))
#endif
            {
                StartCoroutine("SetDefaultColor", indicator);
            }
        }

    }
}