using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.VersionControl;
    using UnityEditor.SceneManagement;
#endif

namespace Mkey
{
    [ExecuteInEditMode]
    [AddComponentMenu("UI/Effects/SoftEffectsSprite", 14)]
    [DisallowMultipleComponent]
    //[RequireComponent(typeof(SpriteRenderer))]
    public class SoftEffectsSprite : SoftEffects
    {
        [SerializeField]
        private Sprite SourceSprite;     // Save Text SourceFont Reference

        [SerializeField]
        private Sprite SoftSprite;

        private string SESubFolderSufix = "_SESprite_0";
        private string key = "_se_rep";



#if UNITY_EDITOR
        public override FaceTarget Facetarget
        {
            get { return FaceTarget.Sprite; }
        }

        public override bool HaveSourceObject
        {
            get { return (SourceSprite && SourceSprite != null); }
        }

        public override string SourceObjectName
        {
            get
            {
                if (SourceSprite && SourceSprite != null) return SourceSprite.name;
                return "No saved source";
            }
        }

        private void OnValidate()
        {

        }

        private void OnEnable()
        {

#if UNITY_STANDALONE_WIN
            if (SoftEffects.debuglog) Debug.Log("<<<<<<<<<<<<<<  SoftEffects enable - Unity Stand Alone Windows - >>>>>>>>>>>>");
#elif UNITY_IOS
        if(SoftEffects.debuglog)Debug.Log("<<<<<<<<<<<<<<  SoftEffects enable - Unity IOS - >>>>>>>>>>>>");
#elif UNITY_STANDALONE_OSX
        if(SoftEffects.debuglog)Debug.Log("<<<<<<<<<<<<<<  SoftEffects enable - Unity Stand Alone OSX - >>>>>>>>>>>>");
#elif UNITY_STANDALONE_LINUX
        if(SoftEffects.debuglog)Debug.Log("<<<<<<<<<<<<<<  SoftEffects enable - Unity Stand Alone Linux - >>>>>>>>>>>>");
#elif UNITY_ANDROID
        if(SoftEffects.debuglog)Debug.Log("<<<<<<<<<<<<<<  SoftEffects enable - Unity ANDROID - >>>>>>>>>>>>");
#endif
            cb = new CBuffers();
            if (faceOptions != null) faceOptions.IsCombinedDirty = true;
            AdjustTextureOnLine();
        }

        private void OnDisable()
        {
            if (SoftEffects.debuglog) Debug.Log("<<<<<<<<<<<<<<  SoftEffects disable  >>>>>>>>>>>>");
            ReleaseData();
        }

        void Update()
        {
            UpdateMaterial();
        }

        public override bool IsTargetObjectMissed(ref string missedError)
        {
            missedError = "";
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (!sr)
            {
                missedError = "Object must have <SpriteRender> component.";
                return true;
            }
            if (sr && !sr.sprite)
            {
                missedError = "Component <SpriteRender> must have sprite.";
                return true;
            }
            return false;
        }

        public override bool IsSoftObjectMissed(ref string missedError)
        {
            if (!SoftSprite)
            {
                missedError = "Editable sprite missed. Rebuild or Create new.";
                return true;
            }
            return false;
        }


        public override void ApplyWorkMaterial(Material mat, bool disableComponent)
        {
            GetComponent<SpriteRenderer>().material = null;
            GetComponent<SpriteRenderer>().material = mat;

            EditorUtility.SetDirty(GetComponent<SpriteRenderer>());

            if (faceOptions.mainTexture)
            {
                if (File.Exists(AssetDatabase.GetAssetPath(faceOptions.mainTexture)))
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(faceOptions.mainTexture)); // 11.06.2026FileUtil.DeleteFileOrDirectory(AssetDatabase.GetAssetPath(faceOptions.mainTexture));
                }
            }

            if (SoftMaterial)
            {
                if (File.Exists(AssetDatabase.GetAssetPath(SoftMaterial)))
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(SoftMaterial));
                }
            }

            enabled = !disableComponent;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public bool IsSoftObjectCreated
        {
            get
            {
                return (SoftSprite && SoftMaterial);
            }
        }

        private bool IsSourceTexture(Sprite sprite)
        {
            if (!sprite) return false;
            bool isSource = true;
            if (sprite.texture.name.Contains(key))
            {
                isSource = false;
            }
            return isSource;
        }

        /// <summary>
        /// Check source Text component, create SoftEffects Font and set new Font to Text 
        /// </summary>
        public override void ApplySoftEffect(bool createNewFolder)
        {
            SpriteRenderer sR = GetComponent<SpriteRenderer>();
            Sprite sprite = (sR) ? sR.sprite : null;
            if (!sprite) return;

#if UNITY_EDITOR

            if (!(EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (IsSourceTexture(sprite))
                {
                    SourceSprite = sprite;// SourceMaterial = sR.material;
                }
                else
                {
                    if (!SourceSprite) return;
                }

                CreateSEFolder(SourceSprite.name, createNewFolder);

                // load compute shader
                FindComputeShader();

                if (!eShader) return;

                //create softsprite and set it to Spriterenderer
                CreateSoftObject(SourceSprite, createNewFolder);
            }

#endif
            if (SoftSprite)
            {
                //  sR.sprite = SoftSprite;
                sR.material = SoftMaterial;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Create Soft Sprite, Soft Sprite Material
        /// </summary>
        /// <param name="font"></param>
        private void CreateSoftObject(Sprite sourceSprite, bool createNewFolder) // http://answers.unity3d.com/questions/485695/truetypefontimportergenerateeditablefont-does-not.html
        {
            faceOptions.pixelsPerUnit = sourceSprite.pixelsPerUnit;

            gpuWorker = new GPUWorker(eShader);
            //  string sourceFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sourceSprite));
            string targetFolder = AssetDatabase.GUIDToAssetPath(FolderGUID);
            string sourcePath = AssetDatabase.GetAssetPath(sourceSprite);

            if (SoftEffects.debuglog) Debug.Log("Path to Source sprite" + sourcePath);

            //1) remove old editable data
            if (SoftSprite && !createNewFolder)
            {
                if (SoftEffects.debuglog) Debug.Log("EditableSprite folder: " + Path.GetDirectoryName(AssetDatabase.GetAssetPath(SoftSprite)));
                if (AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(SoftSprite)))
                {
                    if (SoftEffects.debuglog) Debug.Log("Soft sprite deleted");
                }
                else
                {
                    if (SoftEffects.debuglog) Debug.Log("Soft sprite not deleted");
                }
            }
            if (SoftMaterial && !createNewFolder)
            {
                if (AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(SoftMaterial)))
                {
                    if (SoftEffects.debuglog) Debug.Log("Soft material deleted");
                }
                else
                {
                    if (SoftEffects.debuglog) Debug.Log("Soft material not deleted");
                }
            }


            //3) reimport sprite texture as readable and save new texture

            bool rwEnabled = false;
            ClassExtensions.ReimportTexture(sourceSprite.texture, true, ref rwEnabled);

            ClassExtensions.ReimportTexture(sourceSprite.texture, true);

            faceOptions.RenderSpriteTexture(gpuWorker, sourceSprite.texture, cb);
            faceOptions.CreateTextureFromRender_ARGB32(true, targetFolder + "/" + sourceSprite.name + key + "_edit" + ".png");    // sourceSprite.texture.SaveTextureToPng(targetFolder + "/" + sourceSprite.name + key + "_face" + ".png");

            Texture2D t = (Texture2D)AssetDatabase.LoadMainAssetAtPath(targetFolder + "/" + sourceSprite.name + key + "_edit" + ".png");
            ClassExtensions.ReimportTextureAsSprite_1(targetFolder + "/" + sourceSprite.name + key + "_edit" + ".png", faceOptions.pixelsPerUnit, true);

            if (!rwEnabled) ClassExtensions.ReimportTexture(sourceSprite.texture, false); // reimport souce texture to non readable
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Sprite s = (Sprite)AssetDatabase.LoadAssetAtPath(targetFolder + "/" + sourceSprite.name + key + "_edit" + ".png", typeof(Sprite));
            SoftSprite = s;


            // create new material
            Shader softShader = Shader.Find("SoftEffects/SoftEditShader");
            SoftMaterial = new Material(softShader);
            AssetDatabase.CreateAsset(SoftMaterial, targetFolder + "/" + sourceSprite.name + key + "_material" + ".mat");
            AssetDatabase.Refresh();

            faceOptions.mainTexture = t;
            GetComponent<SpriteRenderer>().sprite = SoftSprite;
            GetComponent<SpriteRenderer>().material = SoftMaterial;
            AssetDatabase.Refresh();

            faceOptions.IsCombinedDirty = true;
            RenderNewTextures(gpuWorker, true);

            //  gpuWorker = null;

            EditorGUIUtility.PingObject(SoftSprite);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// Create Soft Font, Soft Font Material, Soft Font Texture from SoftEffect Settings,
        /// </summary>
        /// <param name="font"></param>
        public override void AdjustTextureOnLine()
        {
            if (!eShader || eShader == null) FindComputeShader();
            if (!eShader || eShader == null) return;


            if (!SoftSprite || !SoftMaterial)
            {
                if (SoftEffects.debuglog) Debug.Log("SoftObject is missing. Create SoftObject bevore.");
                return;
            }

            if (!faceOptions.mainTexture || faceOptions.mainTexture == null)
            {
                Debug.LogError("Edit texture is missing. Create or rebuild.");
                return;
            }

            if (cb == null) cb = new CBuffers();

            if (gpuWorker == null) gpuWorker = new GPUWorker(eShader);

            RenderNewTextures(gpuWorker, false);

            //2 Set new gpu rendered textures to material
            // shadowOptions.SetMaterialPoperty(SoftMaterial); // 10.06.2026
            faceOptions.SetMaterialPoperty(SoftMaterial);

            GetComponent<SpriteRenderer>().material = null;
            GetComponent<SpriteRenderer>().material = SoftMaterial;
        }

        /// <summary>
        /// Create unique font folder for SoftEffects Instance.
        /// </summary>
        private void CreateSEFolder(string objectName, bool createNew)
        {
            if (!createNew)
            {
                // check for folder existing for SoftEffects Font
                if (AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(FolderGUID)))
                {
                    // delete all assets from folder
                    if (SoftEffects.debuglog) Debug.Log("Folder for SeObject also exist: " + AssetDatabase.GUIDToAssetPath(FolderGUID));
                    if (SoftEffects.debuglog) Debug.Log("Delete files from existing folder: " + AssetDatabase.GUIDToAssetPath(FolderGUID));

                    if (SoftEffects.debuglog) Debug.Log(Application.dataPath);
                    ClassExtensions.DeleteFilesFromDir(AssetDatabase.GUIDToAssetPath(FolderGUID), key);

                    if (SoftMaterial)
                    {
                        if (File.Exists(AssetDatabase.GetAssetPath(SoftMaterial)))
                        {
                            if (SoftEffects.debuglog) Debug.Log("Material File: " + AssetDatabase.GetAssetPath(SoftMaterial) + " - delete");
                            FileUtil.DeleteFileOrDirectory(AssetDatabase.GetAssetPath(SoftMaterial));
                        }
                    }

                    if (SoftSprite)
                    {
                        if (File.Exists(AssetDatabase.GetAssetPath(SoftSprite)))
                        {
                            if (SoftEffects.debuglog) Debug.Log("Font File: " + AssetDatabase.GetAssetPath(SoftSprite) + " - delete");
                            FileUtil.DeleteFileOrDirectory(AssetDatabase.GetAssetPath(SoftSprite));
                        }
                    }
                    return;
                }

            }

            // if has no existing folder - create new unique folder  
            string seFolder = GetSEEditFolder();

            if (seFolder != "")
            {
                FolderGUID = AssetDatabase.CreateFolder(seFolder, objectName + SESubFolderSufix);
                if (SoftEffects.debuglog) Debug.Log("Create new folder : " + AssetDatabase.GUIDToAssetPath(FolderGUID));
            }
        }
#endif

    }
}