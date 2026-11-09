using UnityEngine;
using UnityEngine.UI;

namespace RogPhoneSdkDemo
{
    public class FPSCounter : MonoBehaviour
    {
        // Start is called before the first frame update
        private void Start()
        {
            Initialize();
        }

        public void Reset()
        {
            ResetProbingData();

            m_LastUpdateTime = Time.realtimeSinceStartup;
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void ResetProbingData()
        {
            m_MinFrameTime = float.MaxValue;
            m_MaxFrameTime = float.MinValue;
            m_AccumulatedTime = 0.0f;
            m_AccumulatedFrames = 0;
        }

        private float m_FrameTime = 0.0f;
        private float m_MinFrameTime = 0.0f;
        private float m_MaxFrameTime = 0.0f;
        private float m_FrameTimeFlutuation = 0.0f;
        private float m_FrameRate = 0.0f;
        private float m_MinFrameRate = 0.0f;
        private float m_MaxFrameRate = 0.0f;
        private float m_FrameRateFlutuation = 0.0f;

        private float m_AccumulatedTime;
        private int m_AccumulatedFrames;
        private float m_LastUpdateTime;

        public static float UpdateInterval = 0.5f;
        public static float MinTime = 0.000000001f; // equivalent to 1B fps
        private Text textLabel;

        /// <summary>
        /// Initializes (and resets) the component.
        /// <para></para>
        /// <para></para>
        /// <remarks>
        /// The initialization only targets the component's internal data.
        /// </remarks>
        /// </summary>
        public void Initialize()
        {
            textLabel = GetComponent<Text>();
            Reset();
        }

        private void UpdateFPS()
        {
            float deltaTime = Time.unscaledDeltaTime;

            m_AccumulatedTime += deltaTime;
            m_AccumulatedFrames++;

            if (deltaTime < MinTime)
            {
                deltaTime = MinTime;
            }

            if (deltaTime < m_MinFrameTime)
            {
                m_MinFrameTime = deltaTime;
            }

            if (deltaTime > m_MaxFrameTime)
            {
                m_MaxFrameTime = deltaTime;
            }

            float nowTime = Time.realtimeSinceStartup;
            if (nowTime - m_LastUpdateTime < UpdateInterval)
            {
                return;
            }

            if (m_AccumulatedTime < MinTime)
            {
                m_AccumulatedTime = MinTime;
            }

            if (m_AccumulatedFrames < 1)
            {
                m_AccumulatedFrames = 1;
            }

            m_FrameTime = m_AccumulatedTime / m_AccumulatedFrames;
            m_FrameRate = 1.0f / m_FrameTime;

            m_MinFrameRate = 1.0f / m_MaxFrameTime;
            m_MaxFrameRate = 1.0f / m_MinFrameTime;

            m_FrameTimeFlutuation = Mathf.Abs(m_MaxFrameTime - m_MinFrameTime) / 2.0f;
            m_FrameRateFlutuation = Mathf.Abs(m_MaxFrameRate - m_MinFrameRate) / 2.0f;

            string fpsStr = m_FrameRate.ToString("F1");
            textLabel.text = "Render Fps: " + fpsStr;

            ResetProbingData();
            m_LastUpdateTime = nowTime;
        }

        // Update is called once per frame
        private void Update()
        {
            UpdateFPS();
        }
    }
}