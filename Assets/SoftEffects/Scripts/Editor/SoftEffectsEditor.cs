using UnityEngine;
using UnityEditor;

namespace Mkey
{
    [CustomEditor(typeof(SoftEffects))]
    public class SoftEffectsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
        }

        internal bool OnDrawSupportCompute()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Warning.Your GPU not support Compute Shaders. Effect not work.");
                EditorGUILayout.EndHorizontal();
                return false;
            }
            return true;
        }

        internal bool OndrawTargetMissed(SoftEffects myScript)
        {
            string missedError = "";
            if (myScript.IsTargetObjectMissed(ref missedError))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(missedError);
                EditorGUILayout.EndHorizontal();
                return true;
            }
            return false;
        }

        internal bool OndrawEdiObjectMissed(SoftEffects myScript)
        {
            string missedError = "";
            if (myScript.IsSoftObjectMissed(ref missedError))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(missedError);
                EditorGUILayout.EndHorizontal();
                return true;
            }
            return false;
        }

        internal bool OndrawEditMaterialMissed(SoftEffects myScript)
        {
            string missedError = "";
            if (myScript.IsEditMaterialMissed(ref missedError))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(missedError);
                EditorGUILayout.EndHorizontal();
                return true;
            }
            return false;
        }

        internal bool OndrawEditTextureMissed(SoftEffects myScript)
        {
            string missedError = "";
            if (myScript.IsEditTextureMissed(ref missedError))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(missedError);
                EditorGUILayout.EndHorizontal();
                return true;
            }
            return false;
        }

        internal void OnDrawTitle()
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(GetType().ToString(), EditorUIUtil.guiTitleStyle);

            EditorGUILayout.Space();
        }

        internal void OnDrawCreate(SoftEffects myScript)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("For using effects create please edit object.");
            if (GUILayout.Button("Create"))
            {
                myScript.ApplySoftEffect(true);
            }
            EditorGUILayout.EndHorizontal();
        }

        internal void OnDrawSave(SoftEffects myScript)
        {    /*
        EditorGUILayout.BeginHorizontal();
    
        if (GUILayout.Button("Save Face Dialog"))
        {
            myScript.SaveFaceTexturePanel();
        }
        if (GUILayout.Button("Save Face to edit folder"))
        {
            myScript.SaveFaceTexture();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Shadow Dialog"))
        {
            myScript.SaveShadowTexturePanel();
        }

        if (GUILayout.Button("Save Shadow to edit folder"))
        {
            myScript.SaveShadowTexture();
        }

        EditorGUILayout.EndHorizontal();
        */
            if (GUILayout.Button("Save working material to edit folder"))
            {
                myScript.CreateWorkMaterial();
            }
        }

        internal void OnDrawSoftFolder(SoftEffects myScript)
        {
            if (myScript.HaveEditFolder)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("box");
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                string[] folder = myScript.EditObjectFolder.Split('/');
                EditorGUILayout.LabelField("Working Folder :  " + folder[folder.Length - 1]);
                if (GUILayout.Button("Ping"))
                {
                    myScript.PingEditFolder();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                if (myScript.HaveSourceObject)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Force Rebuild : " + myScript.SourceObjectName);
                    if (GUILayout.Button("Rebuild"))
                    {
                        myScript.ApplySoftEffect(false);
                        return;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();

            }
        }

        internal void OnDrawRebuild(SoftEffects myScript)
        {

        }

        internal void OnDrawSpacing(SoftEffects myScript)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            myScript.faceOptions.extPixels = (int)EditorGUILayout.Slider("Spacing, px", myScript.faceOptions.extPixels, 0, 100);
            myScript.faceOptions.extPixels = Mathf.Clamp(myScript.faceOptions.extPixels, 0, 100);
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        internal void OnDrawTargetProp(SoftEffects myScript)
        {
            if (myScript.Facetarget != FaceTarget.Sprite)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("box");
                EditorGUI.indentLevel++;
                if (myScript.Facetarget == FaceTarget.Font)
                {
                    myScript.SoftFontTextureCase = (FontTextureCase)EditorGUILayout.EnumPopup("FontTextureCase", myScript.SoftFontTextureCase);
                    if (myScript.SoftFontTextureCase == FontTextureCase.CustomSet)
                    {
                        myScript.customCharacters = EditorGUILayout.TextField("Custom characters", myScript.customCharacters);
                    }
                    else if (myScript.SoftFontTextureCase == FontTextureCase.Dynamic)
                    {
                        EditorGUILayout.LabelField("Can't use Dynamic. Default set to ASCII.");
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        internal void OnDrawOptions(SoftEffects myScript)
        {
            // DrawDefaultInspector();
            #region bevel
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            bevelOptionsVisible = EditorGUILayout.Foldout(bevelOptionsVisible, "Bevel");
            myScript.bevelOptions.use = EditorGUILayout.Toggle("Use Bevel: ", myScript.bevelOptions.use);
            EditorGUILayout.EndHorizontal();
            if (bevelOptionsVisible) DrawBevelOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            #endregion bevel

            #region stroke
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            strokeOptionsVisible = EditorGUILayout.Foldout(strokeOptionsVisible, "Stroke");
            myScript.strokeOptions.use = EditorGUILayout.Toggle("Use Stroke: ", myScript.strokeOptions.use);
            EditorGUILayout.EndHorizontal();
            if (strokeOptionsVisible) DrawStrokeOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            #endregion stroke

            #region innerShadow
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            innerShadowOptionsVisible = EditorGUILayout.Foldout(innerShadowOptionsVisible, "Inner Shadow");
            myScript.innerShadowOptions.use = EditorGUILayout.Toggle("Use Inner Shadow: ", myScript.innerShadowOptions.use);
            EditorGUILayout.EndHorizontal();
            if (innerShadowOptionsVisible) DrawInnerShadowOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            #endregion innerShadow

            #region innerGlow
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            innerGlowOptionsVisible = EditorGUILayout.Foldout(innerGlowOptionsVisible, "Inner Glow");
            myScript.innerGlowOptions.use = EditorGUILayout.Toggle("Use Inner Glow: ", myScript.innerGlowOptions.use);
            EditorGUILayout.EndHorizontal();
            if (innerGlowOptionsVisible) DrawInnerGlowOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            #endregion innerGlow

            #region face color
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            faceColorOptionsVisible = EditorGUILayout.Foldout(faceColorOptionsVisible, "Color Overlay");
            myScript.faceOptions.useColor = EditorGUILayout.Toggle("Use Color Overlay: ", myScript.faceOptions.useColor);
            EditorGUILayout.EndHorizontal();
            if (faceColorOptionsVisible) DrawFaceColorOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            #endregion face color

            #region face gradient
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            faceGradientOptionsVisible = EditorGUILayout.Foldout(faceGradientOptionsVisible, "Gradient Overlay");
            myScript.faceGradientOptions.use = EditorGUILayout.Toggle("Use Gradient Overlay: ", myScript.faceGradientOptions.use);
            EditorGUILayout.EndHorizontal();
            if (faceGradientOptionsVisible) DrawFaceGradientOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            #endregion face gradient

            #region face pattern
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            facePatternOptionsVisible = EditorGUILayout.Foldout(facePatternOptionsVisible, "Pattern Overlay");
            myScript.faceOptions.usePattern = EditorGUILayout.Toggle("Pattern Overlay: ", myScript.faceOptions.usePattern);
            EditorGUILayout.EndHorizontal();
            if (facePatternOptionsVisible) DrawFacePatternOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            #endregion face pattern

            #region outerGlow
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            outerGlowOptionsVisible = EditorGUILayout.Foldout(outerGlowOptionsVisible, "Outer Glow");
            myScript.outerGlowOptions.use = EditorGUILayout.Toggle("Use Outer Glow: ", myScript.outerGlowOptions.use);
            EditorGUILayout.EndHorizontal();
            if (outerGlowOptionsVisible) DrawOuterGlowOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            #endregion outerGlow

            #region close shadow
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            closeShadowOptionsVisible = EditorGUILayout.Foldout(closeShadowOptionsVisible, "Drop CloseShadow");
            myScript.closeShadowOptions.use = EditorGUILayout.Toggle("Use Drop CloseShadow: ", myScript.closeShadowOptions.use);
            EditorGUILayout.EndHorizontal();
            if (closeShadowOptionsVisible) DrawCloseShadowOptions(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            #endregion close shadow

            #region close shadow_1
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            closeShadowOptionsVisible_1 = EditorGUILayout.Foldout(closeShadowOptionsVisible_1, "Drop CloseShadow(1)");
            myScript.closeShadowOptions_1.use = EditorGUILayout.Toggle("Use Drop CloseShadow(1): ", myScript.closeShadowOptions_1.use);
            EditorGUILayout.EndHorizontal();
            if (closeShadowOptionsVisible_1) DrawCloseShadowOptions_1(myScript);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            #endregion close shadow_1

#region shadow
/* // 10.06.2026
EditorGUILayout.Space();
EditorGUILayout.BeginVertical("box");
EditorGUI.indentLevel++;
EditorGUILayout.BeginHorizontal();
shadowOptionsVisible = EditorGUILayout.Foldout(shadowOptionsVisible, "Drop Shadow");
myScript.shadowOptions.use = EditorGUILayout.Toggle("Use Drop Shadow: ", myScript.shadowOptions.use);
EditorGUILayout.EndHorizontal();
if (shadowOptionsVisible) DrawShadowOptions(myScript);
EditorGUI.indentLevel--;
EditorGUILayout.EndVertical();
EditorGUILayout.Space();
*/
#endregion shadow
}

private bool strokeOptionsVisible;
private void DrawStrokeOptions(SoftEffects script)
{
SerializedProperty strokeSP = serializedObject.FindProperty("strokeOptions");

script.strokeOptions.size = (int)EditorGUILayout.Slider("Size, px", script.strokeOptions.size, 0, 250);

script.strokeOptions.pos = (StrokeOptions.Position)EditorGUILayout.EnumPopup("Position", script.strokeOptions.pos);
script.strokeOptions.fillType = (StrokeOptions.FillType)EditorGUILayout.EnumPopup("FillType", script.strokeOptions.fillType);

EditorGUI.indentLevel++;
switch (script.strokeOptions.fillType)
{
    case StrokeOptions.FillType.Color:
        SerializedProperty coll = strokeSP.FindPropertyRelative("color");
        EditorGUILayout.PropertyField(coll, new GUIContent("Color"));
        break;
    case StrokeOptions.FillType.Gradient:
        SerializedProperty gradient = strokeSP.FindPropertyRelative("gradient");
        EditorGUILayout.PropertyField(gradient, new GUIContent("Gradient"));
        script.strokeOptions.gradType = (StrokeOptions.GradientType)EditorGUILayout.EnumPopup("Gradient Type", script.strokeOptions.gradType);
        script.strokeOptions.angle = (int)EditorGUILayout.Slider("Angle", script.strokeOptions.angle, 0, 360);
        break;

        // case StrokeOptions.FillType.Pattern:
        //     SerializedProperty texture = strokeSP.FindPropertyRelative("pattern");
        //     EditorGUILayout.PropertyField(texture, new GUIContent("Pattern"));
        //    break;
}
EditorGUI.indentLevel--;
EditorGUI.indentLevel--;
}

private bool outerGlowOptionsVisible;
private void DrawOuterGlowOptions(SoftEffects script)
{
SerializedProperty outerGlowSP = serializedObject.FindProperty("outerGlowOptions");

EditorGUI.indentLevel++;
EditorGUILayout.Space();

script.outerGlowOptions.fillType = (OuterGlowOptions.FillType)EditorGUILayout.EnumPopup("FillType", script.outerGlowOptions.fillType);

EditorGUI.indentLevel++;
switch (script.outerGlowOptions.fillType)
{
    case OuterGlowOptions.FillType.Color:
        SerializedProperty coll = outerGlowSP.FindPropertyRelative("color");
        EditorGUILayout.PropertyField(coll, new GUIContent("Color"));
        break;
    case OuterGlowOptions.FillType.Gradient:
        SerializedProperty gradient = outerGlowSP.FindPropertyRelative("gradient");
        EditorGUILayout.PropertyField(gradient, new GUIContent("Gradient"));
        break;
}
EditorGUI.indentLevel--;

EditorGUILayout.Space();
script.outerGlowOptions.method = (OuterGlowOptions.Method)EditorGUILayout.EnumPopup("Method", script.outerGlowOptions.method);
EditorGUI.indentLevel++;
script.outerGlowOptions.spread = (int)EditorGUILayout.Slider("Spread", script.outerGlowOptions.spread, 0, 100);
script.outerGlowOptions.size = (int)EditorGUILayout.Slider("Size, px", script.outerGlowOptions.size, 0, 250);
EditorGUI.indentLevel--;

EditorGUILayout.Space();
SerializedProperty contour = outerGlowSP.FindPropertyRelative("contour");
EditorGUILayout.PropertyField(contour, new GUIContent("Contour"));
script.outerGlowOptions.range = (int)EditorGUILayout.Slider("Range", script.outerGlowOptions.range, 0, 100);
EditorGUI.indentLevel--;
}

private bool closeShadowOptionsVisible;
private void DrawCloseShadowOptions(SoftEffects script)
{
SerializedProperty optionSP = serializedObject.FindProperty("closeShadowOptions");
CloseShadowOptions opt = script.closeShadowOptions;
EditorGUI.indentLevel++;
EditorGUILayout.Space();
opt.blur = (int)EditorGUILayout.Slider("Size, px", opt.blur, 0, 250);
opt.spread = (int)EditorGUILayout.Slider("Spread", opt.spread, 0, 100);
opt.angle = (int)EditorGUILayout.Slider("Light Angle", opt.angle, 0, 360);
opt.offset = (int)EditorGUILayout.Slider("Offset, px", opt.offset, 0, 100);
EditorGUILayout.PropertyField(optionSP.FindPropertyRelative("color"), new GUIContent("Color"));
EditorGUI.indentLevel--;

EditorGUILayout.Space();
EditorGUILayout.PropertyField(optionSP.FindPropertyRelative("contour"), new GUIContent("Contour"));
// opt.noise = (int)EditorGUILayout.Slider("Noise", opt.noise, 0, 100);
}

private bool closeShadowOptionsVisible_1;
private void DrawCloseShadowOptions_1(SoftEffects script)
{
SerializedProperty optionSP = serializedObject.FindProperty("closeShadowOptions_1");
CloseShadowOptions opt = script.closeShadowOptions_1;
EditorGUI.indentLevel++;
EditorGUILayout.Space();
opt.blur = (int)EditorGUILayout.Slider("Size, px", opt.blur, 0, 250);
opt.spread = (int)EditorGUILayout.Slider("Spread", opt.spread, 0, 100);
opt.angle = (int)EditorGUILayout.Slider("Light Angle", opt.angle, 0, 360);
opt.offset = (int)EditorGUILayout.Slider("Offset, px", opt.offset, 0, 100);
EditorGUILayout.PropertyField(optionSP.FindPropertyRelative("color"), new GUIContent("Color"));
EditorGUI.indentLevel--;

EditorGUILayout.Space();
EditorGUILayout.PropertyField(optionSP.FindPropertyRelative("contour"), new GUIContent("Contour"));
//  opt.noise = (int)EditorGUILayout.Slider("Noise", opt.noise, 0, 100);
}

private bool shadowOptionsVisible;
private void DrawShadowOptions(SoftEffects script)
{
SerializedProperty shadowSP = serializedObject.FindProperty("shadowOptions");
EditorGUI.indentLevel++;
EditorGUILayout.Space();
script.shadowOptions.blur = (int)EditorGUILayout.Slider("Size, px", script.shadowOptions.blur, 0, 250);
script.shadowOptions.spread = (int)EditorGUILayout.Slider("Spread", script.shadowOptions.spread, 0, 100);
script.shadowOptions.angle = (int)EditorGUILayout.Slider("Light Angle", script.shadowOptions.angle, 0, 360);
script.shadowOptions.offset = (int)EditorGUILayout.Slider("Offset, px", script.shadowOptions.offset, 0, 100);
SerializedProperty coll = shadowSP.FindPropertyRelative("color");
EditorGUILayout.PropertyField(coll, new GUIContent("Color"));
EditorGUI.indentLevel--;

EditorGUILayout.Space();
SerializedProperty contour = shadowSP.FindPropertyRelative("contour");
EditorGUILayout.PropertyField(contour, new GUIContent("Contour"));
script.shadowOptions.noise = (int)EditorGUILayout.Slider("Noise", script.shadowOptions.noise, 0, 100);
}

private bool bevelOptionsVisible;
private void DrawBevelOptions(SoftEffects script)
{
SerializedProperty bevelSP = serializedObject.FindProperty("bevelOptions");

EditorGUI.indentLevel++;
script.bevelOptions.bStyle = (BevelOptions.Style)EditorGUILayout.EnumPopup("Bevel Style", script.bevelOptions.bStyle);
EditorGUILayout.Space();
script.bevelOptions.bTechnique = (BevelOptions.BevelTechnique)EditorGUILayout.EnumPopup("Bevel Type", script.bevelOptions.bTechnique);

script.bevelOptions.size = (int)EditorGUILayout.Slider("Size, px", script.bevelOptions.size, 0, 250);
script.bevelOptions.depth = (int)EditorGUILayout.Slider("Depth", script.bevelOptions.depth, 1, 1000);
script.bevelOptions.smoothing = (int)EditorGUILayout.Slider("Smoothing", script.bevelOptions.smoothing, 0, 16);

EditorGUILayout.LabelField("Shading");

script.bevelOptions.angle = (int)EditorGUILayout.Slider("Light Angle ", script.bevelOptions.angle, -180, 180);
script.bevelOptions.lightAltitude = (int)EditorGUILayout.Slider("Light Altitude ", script.bevelOptions.lightAltitude, 0, 90);
script.bevelOptions.lightBlendMode = (BevelOptions.BLightMode)EditorGUILayout.EnumPopup("Light blend mode", script.bevelOptions.lightBlendMode);
script.bevelOptions.shadowBlendMode = (BevelOptions.BShadowMode)EditorGUILayout.EnumPopup("Shadow blend mode", script.bevelOptions.shadowBlendMode);
SerializedProperty coll = bevelSP.FindPropertyRelative("lightColor");
EditorGUILayout.PropertyField(coll, new GUIContent("Light Color"));

SerializedProperty cols = bevelSP.FindPropertyRelative("shadowColor");
EditorGUILayout.PropertyField(cols, new GUIContent("Shadow Color"));

EditorGUI.indentLevel++;

EditorGUI.indentLevel--;
EditorGUI.indentLevel--;

EditorGUILayout.Space();
SerializedProperty contour = bevelSP.FindPropertyRelative("contour");
EditorGUILayout.PropertyField(contour, new GUIContent("Contour"));
}

private bool innerGlowOptionsVisible;
private void DrawInnerGlowOptions(SoftEffects script)
{
SerializedProperty innerGlowSP = serializedObject.FindProperty("innerGlowOptions");

EditorGUI.indentLevel++;
script.innerGlowOptions.blendMode = (InnerGlowOptions.IGBlendMode)EditorGUILayout.EnumPopup("BlendMode", script.innerGlowOptions.blendMode);
EditorGUILayout.Space();

script.innerGlowOptions.fillType = (InnerGlowOptions.FillType)EditorGUILayout.EnumPopup("FillType", script.innerGlowOptions.fillType);

EditorGUI.indentLevel++;
switch (script.innerGlowOptions.fillType)
{
    case InnerGlowOptions.FillType.Color:
        SerializedProperty coll = innerGlowSP.FindPropertyRelative("color");
        EditorGUILayout.PropertyField(coll, new GUIContent("Color"));
        break;
    case InnerGlowOptions.FillType.Gradient:
        SerializedProperty gradient = innerGlowSP.FindPropertyRelative("gradient");
        EditorGUILayout.PropertyField(gradient, new GUIContent("Gradient"));
        break;
}
EditorGUI.indentLevel--;

EditorGUILayout.Space();
script.innerGlowOptions.position = (InnerGlowOptions.Position)EditorGUILayout.EnumPopup("Position", script.innerGlowOptions.position);

EditorGUILayout.Space();
script.innerGlowOptions.method = (InnerGlowOptions.Method)EditorGUILayout.EnumPopup("Method", script.innerGlowOptions.method);
EditorGUI.indentLevel++;
script.innerGlowOptions.spread = (int)EditorGUILayout.Slider("Spread", script.innerGlowOptions.spread, 0, 100);
script.innerGlowOptions.size = (int)EditorGUILayout.Slider("Size, px", script.innerGlowOptions.size, 0, 250);
EditorGUI.indentLevel--;

EditorGUILayout.Space();
SerializedProperty contour = innerGlowSP.FindPropertyRelative("contour");
EditorGUILayout.PropertyField(contour, new GUIContent("Contour"));
script.innerGlowOptions.range = (int)EditorGUILayout.Slider("Range", script.innerGlowOptions.range, 0, 100);
EditorGUI.indentLevel--;
}

private bool innerShadowOptionsVisible;
private void DrawInnerShadowOptions(SoftEffects script)
{
SerializedProperty innerShadowSP = serializedObject.FindProperty("innerShadowOptions");

EditorGUI.indentLevel++;
script.innerShadowOptions.blendMode = (InnerShadowOptions.ISBlendMode)EditorGUILayout.EnumPopup("BlendMode", script.innerShadowOptions.blendMode);
EditorGUILayout.Space();

SerializedProperty coll = innerShadowSP.FindPropertyRelative("color");
EditorGUILayout.PropertyField(coll, new GUIContent("Color"));

EditorGUILayout.Space();
script.innerShadowOptions.angle = (int)EditorGUILayout.Slider("Angle", script.innerShadowOptions.angle, -180, 180);
script.innerShadowOptions.distance = (int)EditorGUILayout.Slider("Distance", script.innerShadowOptions.distance, 0, 250);
EditorGUILayout.Space();

script.innerShadowOptions.choke = (int)EditorGUILayout.Slider("Choke", script.innerShadowOptions.choke, 0, 100);
script.innerShadowOptions.size = (int)EditorGUILayout.Slider("Size, px", script.innerShadowOptions.size, 0, 250);

EditorGUILayout.Space();
SerializedProperty contour = innerShadowSP.FindPropertyRelative("contour");
EditorGUILayout.PropertyField(contour, new GUIContent("Contour"));
EditorGUI.indentLevel--;
}

private bool faceColorOptionsVisible;
private void DrawFaceColorOptions(SoftEffects script)
{
SerializedProperty faceSP = serializedObject.FindProperty("faceOptions");
EditorGUI.indentLevel++;
script.faceOptions.cBlendMode = (FaceOptions.CBlendMode)EditorGUILayout.EnumPopup("BlendMode", script.faceOptions.cBlendMode);
EditorGUILayout.Space();
EditorGUI.indentLevel++;
SerializedProperty coll = faceSP.FindPropertyRelative("fColor");
EditorGUILayout.PropertyField(coll, new GUIContent("Face Color"));
EditorGUI.indentLevel--;
EditorGUI.indentLevel--;
}

private bool faceGradientOptionsVisible;
private void DrawFaceGradientOptions(SoftEffects script)
{
SerializedProperty faceSP = serializedObject.FindProperty("faceGradientOptions");
EditorGUI.indentLevel++;
script.faceGradientOptions.gBlendMode = (FaceGradientOptions.GBlendMode)EditorGUILayout.EnumPopup("BlendMode", script.faceGradientOptions.gBlendMode);
EditorGUILayout.Space();
EditorGUI.indentLevel++;
SerializedProperty gradient = faceSP.FindPropertyRelative("gradient");
EditorGUILayout.PropertyField(gradient, new GUIContent("Gradient"));
script.faceGradientOptions.gradType = (FaceGradientOptions.GradientType)EditorGUILayout.EnumPopup("Gradient Type", script.faceGradientOptions.gradType);
script.faceGradientOptions.angle = (int)EditorGUILayout.Slider("Angle", script.faceGradientOptions.angle, 0, 360);
script.faceGradientOptions.scale = (int)EditorGUILayout.Slider("Scale, %", script.faceGradientOptions.scale, 10, 150);
EditorGUI.indentLevel--;
EditorGUI.indentLevel--;

}

private bool facePatternOptionsVisible;
private void DrawFacePatternOptions(SoftEffects script)
{
SerializedProperty faceSP = serializedObject.FindProperty("faceOptions");
EditorGUI.indentLevel++;
script.faceOptions.pBlendMode = (FaceOptions.PBlendMode)EditorGUILayout.EnumPopup("BlendMode", script.faceOptions.pBlendMode);
EditorGUILayout.Space();
EditorGUI.indentLevel++;
SerializedProperty fpText = faceSP.FindPropertyRelative("patternText");
EditorGUILayout.PropertyField(fpText, new GUIContent("Pattern"));
script.faceOptions.pOpacity = (int)EditorGUILayout.Slider("Opacity", script.faceOptions.pOpacity, 0, 100);
script.faceOptions.pScale = (int)EditorGUILayout.Slider("Scale, %", script.faceOptions.pScale, 10, 150);
EditorGUI.indentLevel--;
EditorGUI.indentLevel--;
}
}

public class EditorUIUtil
{

public static GUIStyle guiTitleStyle
{
get
{
    var guiTitleStyle = new GUIStyle(GUI.skin.label);
    guiTitleStyle.normal.textColor = Color.black;
    guiTitleStyle.fontSize = 16;
    guiTitleStyle.fixedHeight = 30;

    return guiTitleStyle;
}
}

public static GUIStyle guiMessageStyle
{
get
{
    var messageStyle = new GUIStyle(GUI.skin.label);
    messageStyle.wordWrap = true;

    return messageStyle;
}
}

}
}
