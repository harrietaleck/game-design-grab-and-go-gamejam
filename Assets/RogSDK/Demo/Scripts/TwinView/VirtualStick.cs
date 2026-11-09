using UnityEngine;
using UnityEngine.EventSystems;

namespace RogPhoneSdkDemo
{
    public class VirtualStick : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] Player player = null;
        public Vector2 Direction { get; private set; } = Vector2.zero;

        private Vector2 initPosition;
        private Vector2 dragOffset;

        #region Initialization

        void Start()
        {
            initPosition = transform.position;
        }

        #endregion

        #region Input Events

        /// <summary>
        /// Dragging
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            transform.position = (eventData.enterEventCamera != null ?
                (Vector2)eventData.enterEventCamera.ScreenToWorldPoint(eventData.position) :
                eventData.position) - dragOffset; // Set virtual stick position
            Direction = (eventData.position - (eventData.enterEventCamera != null ?
                (Vector2)eventData.enterEventCamera.WorldToScreenPoint(initPosition) : initPosition)
                - dragOffset).normalized; // Calculate direction
            if (player) player.Aim(Direction);
        }

        /// <summary>
        /// First frame of dragging : Get an offset of finger position
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            dragOffset = (eventData.enterEventCamera != null ?
                (Vector2)eventData.enterEventCamera.ScreenToWorldPoint(eventData.position) :
                eventData.position) - initPosition; // Gets the offset of the finger position
        }

        /// <summary>
        /// Last frame of dragging : Reset position and values
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            this.transform.position = initPosition; // Snaps the controller back to origin
        }

        #endregion
    }
}