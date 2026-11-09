using UnityEngine;
using UnityEngine.EventSystems;

namespace RogPhoneSdkDemo
{


    public class AttackButton : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] Player player = null;

        public void OnPointerDown(PointerEventData eventData)
        {
            player?.Fire();
        }
    }
}