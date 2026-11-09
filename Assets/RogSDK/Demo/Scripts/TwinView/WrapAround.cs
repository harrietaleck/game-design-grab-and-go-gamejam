using UnityEngine;

namespace RogPhoneSdkDemo
{
    public class WrapAround : MonoBehaviour
    {

        // Update is called once per frame
        void LateUpdate()
        {
            DoWrapAround();
        }

        /// <summary>
        /// Wraps the around the screen
        /// </summary>
        private void DoWrapAround()
        {
            Vector2 wrapPos = this.transform.position;
            if (transform.position.x > LevelBounds.MaxBounds.x) wrapPos.x = -LevelBounds.MaxBounds.x;
            if (transform.position.x < -LevelBounds.MaxBounds.x) wrapPos.x = LevelBounds.MaxBounds.x;
            if (transform.position.y > LevelBounds.MaxBounds.y) wrapPos.y = -LevelBounds.MaxBounds.y;
            if (transform.position.y < -LevelBounds.MaxBounds.y) wrapPos.y = LevelBounds.MaxBounds.y;

            this.transform.position = wrapPos;
        }
    }
}