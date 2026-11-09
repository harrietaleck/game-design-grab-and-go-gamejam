using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RogPhoneSdk
{
    public class TwinViewGraphicsRaycaster : GraphicRaycaster
    {
        public Vector2 MinInputPosition
        {
            set { minInputPosition = value; }
        }

        private Vector2 minInputPosition;

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (eventData.position.x < minInputPosition.x) return;
            if (eventData.position.y < minInputPosition.y) return;

            eventData.position -= minInputPosition;

            base.Raycast(eventData, resultAppendList);
        }
    }
}