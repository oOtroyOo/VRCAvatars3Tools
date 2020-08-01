﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using YamlDotNet.RepresentationModel;

// ver 1.0.0.1
// Copyright (c) 2020 gatosyocora
// MIT License. See LICENSE.txt

namespace Gatosyocora.VRCAvatars3Tools
{
    public class VRCAvatarConverterTo3 : EditorWindow
    {
        private GameObject avatarPrefab;

        private VRCAvatarDescripterDeserializedObject avatar2Info;

        private const string LEFT_EYE_PATH = "Armature/Hips/Spine/Chest/Neck/Head/LeftEye";
        private const string RIGHT_EYE_PATH = "Armature/Hips/Spine/Chest/Neck/Head/RightEye";
        private const string EYELIDS_MESH_PATH = "Body";

        // 2.0のAnimatorOverrideControllerと3.0のHandLayerControllerの
        // AnimationClip設定位置を対応させる
        // 配列Indexが2.0に対し, 各Valueが3.0
        private static string[] clipIndexPair = new string[]
        {
            "Fist", "Point", "RockNRoll", "Open", "Thumbs up", "Peace", "Gun"
        };

        private bool showViewInfo = true;
        private bool showLipSyncInfo = true;
        private bool showEyeLookInfo = true;
        private bool showAnimationLayersInfo = true;
        private Vector2 scrollPos = Vector2.zero;

        [MenuItem("VRCAvatars3Tools/VRCAvatarConverterTo3")]
        public static void Open()
        {
            GetWindow<VRCAvatarConverterTo3>(nameof(VRCAvatarConverterTo3));
        }

        private void OnGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                avatarPrefab = EditorGUILayout.ObjectField("2.0 Avatar Prefab", avatarPrefab, typeof(GameObject), false) as GameObject;
                if (ObjectSelectorWrapper.isVisible)
                {
                    ObjectSelectorWrapper.SetFilterString("t:prefab");
                }

                if (check.changed && avatarPrefab != null)
                {
                    avatar2Info = GetAvatar2Info(avatarPrefab);
                }
            }

            if (avatarPrefab != null && avatar2Info != null)
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
                {
                    scrollPos = scroll.scrollPosition;

                    EditorGUILayout.LabelField("Prefab Name", avatarPrefab.name);

                    showViewInfo = EditorGUILayout.Foldout(showViewInfo, "View");
                    if (showViewInfo)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.LabelField("ViewPosition", avatar2Info.ViewPosition.ToString());
                            EditorGUILayout.LabelField("ScaleIPD", avatar2Info.ScaleIPD.ToString());
                        }
                    }

                    showLipSyncInfo = EditorGUILayout.Foldout(showLipSyncInfo, "LipSync");
                    if (showLipSyncInfo)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.LabelField("FaceMeshPath", avatar2Info.faceMeshRendererPath);
                        }
                    }

                    showEyeLookInfo = EditorGUILayout.Foldout(showEyeLookInfo, "EyeLook");
                    if (showEyeLookInfo)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.LabelField("Eyes.LeftEyeBone", LEFT_EYE_PATH);
                            EditorGUILayout.LabelField("Eyes.RightEyeBone", RIGHT_EYE_PATH);
                            EditorGUILayout.LabelField("EyelidType", "None");
                            EditorGUILayout.HelpBox("If use this, change type after convert", MessageType.Info);
                            EditorGUILayout.LabelField("Eyelids.FyelidsMesh", EYELIDS_MESH_PATH);
                            EditorGUILayout.LabelField("Eyelids.BlendShapeStates", "<Unimplemented>");
                            EditorGUILayout.HelpBox("Set LeftEyeBone, RightEyeBone and EyelidsMesh if found them", MessageType.Warning);
                        }
                    }

                    showAnimationLayersInfo = EditorGUILayout.Foldout(showAnimationLayersInfo, "AnimationLayers");
                    if (showAnimationLayersInfo)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.LabelField("StandingOverrideController", avatar2Info.standingOverrideControllerPath);
                            EditorGUILayout.LabelField("SittingOverrideController", "<Unimplemented>");
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledGroupScope(avatarPrefab is null || avatar2Info is null))
            {
                if (GUILayout.Button("Convert Avatar To 3.0"))
                {
                    var avatar3Obj = ConvertAvatarTo3(avatarPrefab, avatar2Info);
                    Selection.activeObject = avatar3Obj;
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Remove missing component after convert", MessageType.Warning);
            EditorGUILayout.Space();
        }

        private GameObject ConvertAvatarTo3(GameObject avatarPrefab2, VRCAvatarDescripterDeserializedObject avatar2Info)
        {
            var avatarObj3 = PrefabUtility.InstantiatePrefab(avatarPrefab2) as GameObject;
            avatarObj3.name = GameObjectUtility.GetUniqueNameForSibling(avatarObj3.transform.parent, $"{ avatarObj3.name}_3.0");
            var avatar = avatarObj3.AddComponent<VRCAvatarDescriptor>();
            avatar.Name = avatar2Info.Name;
            avatar.ViewPosition = avatar2Info.ViewPosition;
            avatar.ScaleIPD = avatar2Info.ScaleIPD;
            avatar.lipSync = avatar2Info.lipSync;
            avatar.VisemeSkinnedMesh = avatarObj3.transform.Find(avatar2Info.faceMeshRendererPath)?.GetComponent<SkinnedMeshRenderer>() ?? null;
            avatar.VisemeBlendShapes = avatar2Info.VisemeBlendShapes;
            avatar.enableEyeLook = true;
            avatar.customEyeLookSettings = new VRCAvatarDescriptor.CustomEyeLookSettings
            {
                leftEye = avatarObj3.transform.Find(LEFT_EYE_PATH),
                rightEye = avatarObj3.transform.Find(RIGHT_EYE_PATH),
                // TODO: 設定が未完了なのでアバターが鏡に写らなくなってしまう
                //eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes,
                eyelidsSkinnedMesh = avatarObj3.transform.Find(EYELIDS_MESH_PATH)?.GetComponent<SkinnedMeshRenderer>() ?? null
            };

            if (avatar.customEyeLookSettings.eyelidsSkinnedMesh is null)
            {
                avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;
            }

            if (avatar.customEyeLookSettings.leftEye is null && avatar.customEyeLookSettings.rightEye is null &&
                avatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.None)
            {
                avatar.enableEyeLook = false;
            }

            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[]
            {
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Base,
                    isDefault = true
                },
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Additive,
                    isDefault = true
                },
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Gesture,
                    isDefault = true
                },
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Action,
                    isDefault = true
                },
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    isDefault = true
                }
            };

            avatar.specialAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[]
            {
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.Sitting,
                    isDefault = true
                },
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.TPose,
                    isDefault = true
                },
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.IKPose,
                    isDefault = true
                }
            };

            var originalHandLayerControllerPath = GetAssetPathForSearch("vrc_AvatarV3HandsLayer t:AnimatorController");
            var fxControllerName = $"{Path.GetFileNameWithoutExtension(originalHandLayerControllerPath)}_{avatarPrefab2.name}.controller";
            var fxControllerPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(Path.GetDirectoryName(avatar2Info.standingOverrideControllerPath), fxControllerName));
            AssetDatabase.CopyAsset(originalHandLayerControllerPath, fxControllerPath);
            var fxController = AssetDatabase.LoadAssetAtPath(fxControllerPath, typeof(AnimatorController)) as AnimatorController;

            avatar.baseAnimationLayers[4].isDefault = false;
            avatar.baseAnimationLayers[4].isEnabled = true;
            avatar.baseAnimationLayers[4].animatorController = fxController;
            avatar.baseAnimationLayers[4].mask = null;

            foreach (var layer in fxController.layers)
            {
                if (layer.name != "Left Hand" && layer.name != "Right Hand") continue;

                for (int i = 0; i < avatar2Info.OverrideAnimationClips.Length; i++)
                {
                    var animPath = avatar2Info.OverrideAnimationClips[i];
                    if (string.IsNullOrEmpty(animPath)) continue;

                    var animClip = AssetDatabase.LoadAssetAtPath(animPath, typeof(AnimationClip)) as AnimationClip;
                    var state = GetAnimatorStateFromStateName(layer.stateMachine, clipIndexPair[i]);
                    state.motion = animClip;
                }
            }

            return avatarObj3;
        }

        private VRCAvatarDescripterDeserializedObject GetAvatar2Info(GameObject avatarPrefab2)
        {
            var avatar2Info = new VRCAvatarDescripterDeserializedObject();
            var filePath = AssetDatabase.GetAssetPath(avatarPrefab2);
            var yaml = new YamlStream();
            using (var sr = new StreamReader(filePath, System.Text.Encoding.UTF8))
            {
                yaml.Load(sr);
            }

            // コンポーネントレベルでDocumentが存在する
            foreach (var document in yaml.Documents)
            {
                var node = document.RootNode;
                // MonoBehaiviour以外は処理しない
                if (node.Tag != "tag:unity3d.com,2011:114") continue;

                var mapping = (YamlMappingNode)node;
                var vrcAvatarDescripter = (YamlMappingNode)mapping.Children["MonoBehaviour"];

                // VRCAvatarDescripter以外は処理しない
                if (((YamlScalarNode)((YamlMappingNode)vrcAvatarDescripter["m_Script"]).Children["guid"]).Value != "f78c4655b33cb5741983dc02e08899cf") continue;

                avatar2Info.Name = ((YamlScalarNode)vrcAvatarDescripter["Name"]).Value;

                // [View]
                // ViewPosition
                var viewPosition = (YamlMappingNode)vrcAvatarDescripter["ViewPosition"];
                avatar2Info.ViewPosition = new Vector3(
                                                float.Parse(((YamlScalarNode)viewPosition["x"]).Value),
                                                float.Parse(((YamlScalarNode)viewPosition["y"]).Value),
                                                float.Parse(((YamlScalarNode)viewPosition["z"]).Value)
                                            );
                // ScaleIPD
                avatar2Info.ScaleIPD = ((YamlScalarNode)vrcAvatarDescripter["ScaleIPD"]).Value == "1";

                // [LipSync]
                // Mode
                var lipSyncTypeIndex = int.Parse(((YamlScalarNode)vrcAvatarDescripter["lipSync"]).Value);
                avatar2Info.lipSync = (VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle)Enum.ToObject(typeof(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle), lipSyncTypeIndex);
                // FaceMesh
                var faceMeshRendererGuid = ((YamlScalarNode)((YamlMappingNode)vrcAvatarDescripter["VisemeSkinnedMesh"]).Children["fileID"]).Value;
                var path = GetSkinnedMeshRendererPathFromGUID(yaml.Documents, faceMeshRendererGuid);
                avatar2Info.faceMeshRendererPath = path;
                // VisemeBlendShapes
                avatar2Info.VisemeBlendShapes = new string[15];
                var visemeBlendShapes = ((YamlSequenceNode)vrcAvatarDescripter["VisemeBlendShapes"]);
                for (int i = 0; i < 15; i++)
                {
                    avatar2Info.VisemeBlendShapes[i] = ((YamlScalarNode)visemeBlendShapes[i]).Value;
                }

                // [AnimationLayers]
                // CustomStaindingAnims
                var standingOverrideControllerGuid = ((YamlScalarNode)((YamlMappingNode)vrcAvatarDescripter["CustomStandingAnims"]).Children["guid"]).Value;
                avatar2Info.standingOverrideControllerPath = AssetDatabase.GUIDToAssetPath(standingOverrideControllerGuid);

                var yamlController = new YamlStream();
                using (var sr = new StreamReader(avatar2Info.standingOverrideControllerPath, System.Text.Encoding.UTF8))
                {
                    yaml.Load(sr);
                }
                var controllerNode = (YamlMappingNode)yaml.Documents[0].RootNode;
                var overrideController = (YamlMappingNode)controllerNode.Children["AnimatorOverrideController"];
                var clips = (YamlSequenceNode)overrideController.Children["m_Clips"];
                avatar2Info.OverrideAnimationClips = new string[clips.Count()];
                for (int i = 0; i < clips.Count(); i++)
                {
                    var clip = clips[i];
                    var clipPair = (YamlMappingNode)clip;
                    var overrideClip = (YamlMappingNode)clipPair.Children["m_OverrideClip"];

                    if (!overrideClip.Children.TryGetValue("guid", out YamlNode overrideClipGuidNode))
                    {
                        continue;
                    }
                    var overrideClipGuid = ((YamlScalarNode)overrideClipGuidNode).Value;
                    avatar2Info.OverrideAnimationClips[i] = AssetDatabase.GUIDToAssetPath(overrideClipGuid);
                }

                break;
            }

            return avatar2Info;
        }

        private YamlNode GetNodeFromGUID(IList<YamlDocument> components, string guid)
        {
            foreach (var component in components)
            {
                var node = component.RootNode;
                if (node.Anchor != guid) continue;
                return node;
            }
            return null;
        }

        private string GetSkinnedMeshRendererPathFromGUID(IList<YamlDocument> components, string rendererGuid)
        {
            string path = string.Empty;
            var node = GetNodeFromGUID(components, rendererGuid);
            var skinnedMeshRenderer = (YamlMappingNode)((YamlMappingNode)node).Children["SkinnedMeshRenderer"];
            
            var gameObjectGuid = ((YamlScalarNode)((YamlMappingNode)skinnedMeshRenderer["m_GameObject"]).Children["fileID"]).Value;
            node = GetNodeFromGUID(components, gameObjectGuid);
            var gameObjectNode = (YamlMappingNode)((YamlMappingNode)node).Children["GameObject"];

            string gameObjectName = ((YamlScalarNode)gameObjectNode["m_Name"]).Value;
            while (true)
            {
                string parentGuid = string.Empty;
                var componentInGameObject = (YamlSequenceNode)gameObjectNode["m_Component"];
                foreach (YamlMappingNode component in componentInGameObject)
                {
                    var componentGuid = ((YamlScalarNode)((YamlMappingNode)component["component"]).Children["fileID"]).Value;
                    node = GetNodeFromGUID(components, componentGuid);
                    // Transform以外処理しない
                    if (node.Tag != "tag:unity3d.com,2011:4") continue;

                    var transform = (YamlMappingNode)((YamlMappingNode)node).Children["Transform"];
                    parentGuid = ((YamlScalarNode)((YamlMappingNode)transform["m_Father"]).Children["fileID"]).Value;
                    break;
                }

                if (string.IsNullOrEmpty(parentGuid)) break;

                node = GetNodeFromGUID(components, parentGuid);

                if (node is null) break;

                var parentTransform = (YamlMappingNode)((YamlMappingNode)node).Children["Transform"];
                gameObjectGuid = ((YamlScalarNode)((YamlMappingNode)parentTransform["m_GameObject"]).Children["fileID"]).Value;
                node = GetNodeFromGUID(components, gameObjectGuid);
                gameObjectNode = (YamlMappingNode)((YamlMappingNode)node).Children["GameObject"];
                path = $"{gameObjectName}/{path}";
                gameObjectName = ((YamlScalarNode)gameObjectNode["m_Name"]).Value;
            }

            path = path.Substring(0, path.Length - 1);
            return path;
        }

        private AnimatorState GetAnimatorStateFromStateName(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.name != stateName) continue;
                return state.state;
            }
            return null;
        }

        private static string GetAssetPathForSearch(string filter) =>
            AssetDatabase.FindAssets(filter)
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .OrderBy(p => Path.GetFileName(p).Count())
                .First();
    }
}

