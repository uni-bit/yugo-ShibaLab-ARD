#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Stages.Editor
{
    public static class AutoFixHelper
    {
        [InitializeOnLoadMethod]
        public static void AutoFixAll()
        {
            EditorApplication.delayCall += DoFix;
        }

        [MenuItem("Tools/ShibaLab/Force Fix Stage Settings")]
        public static void DoFix()
        {
            bool modified = false;

            // 1. Fix StageLightCodeDigitAnimator (Hidden gameObjects, broken parameters)
            var animators = Object.FindObjectsOfType<StageLightCodeDigitAnimator>(true);
            foreach (var anim in animators)
            {
                bool animModified = false;
                
                // If it was disabled by old code, re-enable it!
                if (!anim.gameObject.activeSelf)
                {
                    anim.gameObject.SetActive(true);
                    animModified = true;
                    Debug.Log($"[AutoFix] Re-enabled disabled lock digit object: {anim.gameObject.name}");
                }

                // Make sure MeshRenderer is only disabled, but exists
                var mr = anim.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled)
                {
                    mr.enabled = false;
                    animModified = true;
                }

                // Check serialized values via SerializedObject to fix default 0s
                var so = new SerializedObject(anim);
                var spacingProp = so.FindProperty("digitSpacing");
                if (spacingProp != null && spacingProp.floatValue < 0.1f)
                {
                    spacingProp.floatValue = 1.3f;
                    animModified = true;
                }
                var radiusProp = so.FindProperty("radiusMultiplier");
                if (radiusProp != null && radiusProp.floatValue < 0.1f)
                {
                    radiusProp.floatValue = 1.2f;
                    animModified = true;
                }
                if (animModified)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(anim.gameObject);
                    modified = true;
                }
            }

            // 2. Fix StageAudioController
            var audioControllers = Object.FindObjectsOfType<StageAudioController>(true);
            foreach (var controller in audioControllers)
            {
                var so = new SerializedObject(controller);
                bool audioModified = false;

                // Function to safely set clip
                void AssignClip(string propName, string searchName)
                {
                    var prop = so.FindProperty(propName);
                    if (prop != null && prop.objectReferenceValue == null)
                    {
                        string[] guids = AssetDatabase.FindAssets(searchName + " t:AudioClip");
                        if (guids.Length > 0)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                            if (clip != null)
                            {
                                prop.objectReferenceValue = clip;
                                audioModified = true;
                            }
                        }
                    }
                }

                void AssignClipArray(string propName, string[] searchNames)
                {
                    var prop = so.FindProperty(propName);
                    if (prop != null)
                    {
                        // Only auto-fill if empty or all null
                        bool isEmpty = true;
                        for (int i = 0; i < prop.arraySize; i++)
                        {
                            if (prop.GetArrayElementAtIndex(i).objectReferenceValue != null)
                            {
                                isEmpty = false;
                                break;
                            }
                        }
                        if (isEmpty)
                        {
                            prop.ClearArray();
                            for (int i = 0; i < searchNames.Length; i++)
                            {
                                string[] guids = AssetDatabase.FindAssets(searchNames[i] + " t:AudioClip");
                                if (guids.Length > 0)
                                {
                                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                                    if (clip != null)
                                    {
                                        prop.InsertArrayElementAtIndex(prop.arraySize);
                                        prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = clip;
                                        audioModified = true;
                                    }
                                }
                            }
                        }
                    }
                }

                AssignClip("stage1Ambient", "stage1environment");
                AssignClipArray("stage1ExtraAmbients", new[] { "forest-base", "kotori", "river", "suzumusi", "wind" });
                
                AssignClip("stage2Ambient", "stage2environment");
                AssignClipArray("stage2ExtraAmbients", new[] { "doukutu" });
                
                AssignClip("stage3Ambient", "stage3environment");
                AssignClip("commonSuccess", "succes");

                AssignClipArray("stage1AnimalMoves", new[] { "animal-move-gimmick", "doubutuidou" });
                AssignClipArray("stage1LeafMoves", new[] { "tree-move-reaf-gimmick", "kusa-syoudoubutu" });
                AssignClip("stage1SoilMove", "tree-move-soil-gimmick");

                AssignClip("stage2Destroy", "破壊音"); // Note: might match multiple, but finding 1 is fine
                AssignClip("stage2DestroySecondary", "破壊音2");
                AssignClip("stage2Explosion", "爆発");

                AssignClipArray("stage2WaterDrops", new[] { "Water_Drop03-1", "Water_Drop03-2", "Water_Drop03-3", "Water_Drop03-4" });

                AssignClip("stage3RockRise", "岩が浮く瞬間の振動音1");
                AssignClip("stage3StoneFall", "砂や小石が落ちる音2");

                AssignClipArray("stage3GlowClips", new[] { "hikari1", "hikari2", "hikari3" });

                if (audioModified)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(controller.gameObject);
                    Debug.Log($"[AutoFix] StageAudioController on {controller.gameObject.name} auto-assigned missing audio clips.");
                    modified = true;
                }
            }

            if (modified)
            {
                Debug.Log("[AutoFix] Auto Fix complete for scene objects. Proceeding to fix prefabs...");
                AssetDatabase.SaveAssets();
            }

            // 3. Fix Prefabs too
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    var prefabRoot = scope.prefabContentsRoot;
                    bool prefabModified = false;

                    // Fix Animators in Prefab
                    var animatorsPrefab = prefabRoot.GetComponentsInChildren<StageLightCodeDigitAnimator>(true);
                    foreach (var anim in animatorsPrefab)
                    {
                        if (!anim.gameObject.activeSelf)
                        {
                            anim.gameObject.SetActive(true);
                            prefabModified = true;
                        }

                        var so = new SerializedObject(anim);
                        var spacingProp = so.FindProperty("digitSpacing");
                        if (spacingProp != null && spacingProp.floatValue < 0.1f)
                        {
                            spacingProp.floatValue = 1.3f;
                            prefabModified = true;
                        }
                        var radiusProp = so.FindProperty("radiusMultiplier");
                        if (radiusProp != null && radiusProp.floatValue < 0.1f)
                        {
                            radiusProp.floatValue = 1.2f;
                            prefabModified = true;
                        }
                        if (prefabModified) so.ApplyModifiedProperties();
                    }

                    // Fix AudioController in Prefab
                    var audioControllersPrefab = prefabRoot.GetComponentsInChildren<StageAudioController>(true);
                    foreach (var controller in audioControllersPrefab)
                    {
                        var so = new SerializedObject(controller);
                        
                        void AssignClip(string propName, string searchName)
                        {
                            var prop = so.FindProperty(propName);
                            if (prop != null && prop.objectReferenceValue == null)
                            {
                                string[] tguids = AssetDatabase.FindAssets(searchName + " t:AudioClip");
                                if (tguids.Length > 0)
                                {
                                    string tpath = AssetDatabase.GUIDToAssetPath(tguids[0]);
                                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(tpath);
                                    if (clip != null)
                                    {
                                        prop.objectReferenceValue = clip;
                                        prefabModified = true;
                                    }
                                }
                            }
                        }

                        void AssignClipArray(string propName, string[] searchNames)
                        {
                            var prop = so.FindProperty(propName);
                            if (prop != null)
                            {
                                bool isEmpty = true;
                                for (int i = 0; i < prop.arraySize; i++)
                                {
                                    if (prop.GetArrayElementAtIndex(i).objectReferenceValue != null)
                                    {
                                        isEmpty = false; break;
                                    }
                                }
                                if (isEmpty)
                                {
                                    prop.ClearArray();
                                    for (int i = 0; i < searchNames.Length; i++)
                                    {
                                        string[] tguids = AssetDatabase.FindAssets(searchNames[i] + " t:AudioClip");
                                        if (tguids.Length > 0)
                                        {
                                            string tpath = AssetDatabase.GUIDToAssetPath(tguids[0]);
                                            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(tpath);
                                            if (clip != null)
                                            {
                                                prop.InsertArrayElementAtIndex(prop.arraySize);
                                                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = clip;
                                                prefabModified = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        AssignClip("stage1Ambient", "stage1environment");
                        AssignClipArray("stage1ExtraAmbients", new[] { "forest-base", "kotori", "river", "suzumusi", "wind" });
                        AssignClip("stage2Ambient", "stage2environment");
                        AssignClipArray("stage2ExtraAmbients", new[] { "doukutu" });
                        AssignClip("stage3Ambient", "stage3environment");
                        AssignClip("commonSuccess", "succes");
                        AssignClipArray("stage1AnimalMoves", new[] { "animal-move-gimmick", "doubutuidou" });
                        AssignClipArray("stage1LeafMoves", new[] { "tree-move-reaf-gimmick", "kusa-syoudoubutu" });
                        AssignClip("stage1SoilMove", "tree-move-soil-gimmick");
                        AssignClip("stage2Destroy", "破壊音");
                        AssignClip("stage2DestroySecondary", "破壊音2");
                        AssignClip("stage2Explosion", "爆発");
                        AssignClipArray("stage2WaterDrops", new[] { "Water_Drop03-1", "Water_Drop03-2", "Water_Drop03-3", "Water_Drop03-4" });
                        AssignClip("stage3RockRise", "岩が浮く瞬間の振動音1");
                        AssignClip("stage3StoneFall", "砂や小石が落ちる音2");
                        AssignClipArray("stage3GlowClips", new[] { "hikari1", "hikari2", "hikari3" });

                        if (prefabModified)
                        {
                            so.ApplyModifiedProperties();
                        }
                    }

                    if (prefabModified)
                    {
                        Debug.Log($"[AutoFix] Fixed prefab: {path}");
                    }
                }
            }
        }
    }
}
#endif