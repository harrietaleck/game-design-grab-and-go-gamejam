using UnityEngine;

namespace RogPhoneSdkDemo
{
    public class LevelBounds : MonoBehaviour
    {
        public static Vector2 MaxBounds { get; private set; } = Vector2.zero;

        void Start()
        {
            MaxBounds = Camera.main.ScreenToWorldPoint(new Vector2(Screen.width, Screen.height));
        }
    }
}