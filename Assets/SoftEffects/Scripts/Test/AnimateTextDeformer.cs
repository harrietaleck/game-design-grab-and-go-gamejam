using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mkey
{
    [RequireComponent(typeof(EasyTextDeformer))]
    public class AnimateTextDeformer : MonoBehaviour
    {
        private EasyTextDeformer deformer;
        Vector3 handlePos_1;
        Vector3 handlePos_2;
        Vector3 animPos_1;
        Vector3 animPos_2;

        public float amplitude;

        private void Start()
        {
            if (!deformer) deformer = GetComponent<EasyTextDeformer>();
            if (!deformer) return;
            handlePos_1 = deformer.handlesPositions[1];
            handlePos_2 = deformer.handlesPositions[2];
            animPos_1 = handlePos_1;
            animPos_2 = handlePos_2;
        }

        private void Update()
        {
            if (!deformer) deformer = GetComponent<EasyTextDeformer>();
            if (!deformer) return;


            deformer.handlesPositions[1] = animPos_1;
            deformer.handlesPositions[2] = animPos_2;
            TestAnimate();
        }

        private float i = 0;

        private void TestAnimate()
        {
            float dPos = amplitude * Mathf.Sin((i++) * 0.01f * 2 * Mathf.PI );
      
            if (i > 100) i = 0;
            animPos_1 = handlePos_1 + new Vector3(0, dPos,0);
            animPos_2 =  handlePos_2 + new Vector3(0, -dPos, 0);
            deformer.OnChangeSpline();
        }
    }

}