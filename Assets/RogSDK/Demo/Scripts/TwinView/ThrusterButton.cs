using UnityEngine;
using UnityEngine.EventSystems;

namespace RogPhoneSdkDemo
{
    public class ThrusterButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] Player player = null;
        private bool buttonHeld = false;

        void Update()
        {
            if (buttonHeld && player) player.AddThrust();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            buttonHeld = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            buttonHeld = false;
        }
    }
}