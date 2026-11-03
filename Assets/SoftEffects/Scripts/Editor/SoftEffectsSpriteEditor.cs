using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Mkey
{
    [CustomEditor(typeof(SoftEffectsSprite))]
    public class SoftEffectsSpriteEditor : SoftEffectsEditor
    {
        private bool drawDefault = false;

        public override void OnInspectorGUI()
        {
            if (drawDefault)
                DrawDefaultInspector();
            SoftEffects myScript = (SoftEffects)target;
            if (!myScript.gameObject.activeSelf || !myScript.enabled) return;
            if (!OnDrawSupportCompute()) return;

            OnDrawTitle();
            if (OndrawTargetMissed(myScript)) return;

            OnDrawCreate(myScript);
            OnDrawTargetProp(myScript);
            OnDrawSpacing(myScript);

            if (OndrawEdiObjectMissed(myScript)) return;

            OnDrawRebuild(myScript);
            OnDrawSoftFolder(myScript);

            if (OndrawEditMaterialMissed(myScript)) return;

            if (OndrawEditTextureMissed(myScript)) return;

            OnDrawSave(myScript);

            EditorGUI.BeginChangeCheck();

            OnDrawOptions(myScript);

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                myScript.AdjustTextureOnLine();
                if (!SceneManager.GetActiveScene().isDirty) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            /*
                    if (UnityEngine.GUILayout.Button("Rebuild Dt Buffers"))
                    {
                       // myScript.RenderDtBuffers();
                    }

                    if (UnityEngine.GUILayout.Button("Create signed Dt Buffer"))
                    {
                       // myScript.RenderSignedDtBuffer();
                    }
                    */
        }
    }
}