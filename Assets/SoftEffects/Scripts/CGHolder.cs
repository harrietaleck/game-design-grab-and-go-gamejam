using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace Mkey
{
    [CreateAssetMenu]
    public class CGHolder : ScriptableObject
    {

        [Space(8, order = 0)]
        [Header("Curves", order = 1)]
        public AnimationCurve[] curves;

        [Space(8, order = 0)]
        [Header("Gradients", order = 1)]
        public Gradient[] gradients;

        //public string TOSTRING() // 09.08.2026 comment
        //{
        //    return name + "; id: " + GetInstanceID();
        //}

        public void Save()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }

    }
}

