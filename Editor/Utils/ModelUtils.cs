using System;
using System.Collections.Generic;

using System.Linq;

using UnityEngine;

using VMDPaser;

namespace UnityMMDConverter.Utils
{
    public static class ModelUtils
    {
        public static string GetBodyRendererPath(GameObject targetModel, SkinnedMeshRenderer bodyRenderer)
        {
            if (bodyRenderer == null || targetModel == null)
                return "";

            var current = bodyRenderer.transform;
            var path = "";

            while (current != null && current != targetModel.transform)
            {
                path = string.IsNullOrEmpty(path) ? current.name : $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
        public static Transform FindTransformByName(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;

            for (var i = 0; i < parent.childCount; i++)
            {
                var result = FindTransformByName(parent.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }
        /// <summary>
        /// 收集SkinnedMeshRenderer的所有BlendShape名称到availableMorphs，并初始化selectedMorphs。
        /// </summary>
        /// <param name="bodyRenderer">目标SkinnedMeshRenderer</param>
        /// <param name="availableMorphs">输出：收集到的morph名称列表</param>
        /// <param name="selectedMorphs">输出：morph名称到选择状态的字典</param>
        /// <param name="directMappingMode">是否为直接映射模式（为true时跳过收集）</param>
        public static void CollectAvailableMorphs(
            SkinnedMeshRenderer bodyRenderer,
            List<string> availableMorphs,
            Dictionary<string, bool> selectedMorphs,
            bool directMappingMode)
        {
            // 直接模式下不收集模型形态键
            if (directMappingMode) return;
            availableMorphs.Clear();
            selectedMorphs.Clear();

            if (bodyRenderer == null || bodyRenderer.sharedMesh == null)
                return;

            for (var i = 0; i < bodyRenderer.sharedMesh.blendShapeCount; i++)
            {
                var morphName = bodyRenderer.sharedMesh.GetBlendShapeName(i);
                availableMorphs.Add(morphName);
                selectedMorphs[morphName] = false;
            }
        }

        /// <summary>
        /// 获取VMD形态键在模型中的映射名称。
        /// </summary>
        /// <param name="vmdMorphName">VMD中的morph名称</param>
        /// <param name="morphMapping">形态键映射表</param>
        /// <param name="availableMorphs">模型可用的morph列表</param>
        /// <returns>映射后的morph名称</returns>
        public static string GetMappedMorphName(
            string vmdMorphName,
            Dictionary<string, string> morphMapping,
            List<string> availableMorphs)
        {
            if (morphMapping != null && morphMapping.TryGetValue(vmdMorphName, out var mappedName) && !string.IsNullOrEmpty(mappedName))
            {
                return mappedName;
            }

            // 尝试直接匹配
            if (availableMorphs != null && availableMorphs.Contains(vmdMorphName))
            {
                return vmdMorphName;
            }

            // 尝试规范化名称匹配
            var normalizedName = MorphUtils.NormalizeMorphName(vmdMorphName);
            if (availableMorphs != null)
            {
                var match = availableMorphs.FirstOrDefault(m => MorphUtils.NormalizeMorphName(m) == normalizedName);
                if (!string.IsNullOrEmpty(match))
                    return match;
            }
            return vmdMorphName;
        }

        /// <summary>
        /// 查找模型的SkinnedMeshRenderer组件
        /// </summary>
        /// <param name="targetModel">目标模型GameObject</param>
        /// <returns>找到的SkinnedMeshRenderer，找不到则返回null</returns>
        public static SkinnedMeshRenderer FindBodyRenderer(GameObject targetModel)
        {
            if (targetModel == null) return null;

            // 先尝试查找名为"Body"的SkinnedMeshRenderer
            var bodyTransform = ModelUtils.FindTransformByName(targetModel.transform, "Body");
            if (bodyTransform != null)
            {
                var renderer = bodyTransform.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null)
                    return renderer;
            }

            // 如果找不到，则查找第一个SkinnedMeshRenderer
            var renderers = targetModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length > 0)
            {
                // 尝试智能选择主要的渲染器
                if (renderers.Length > 1)
                {
                    foreach (var renderer in renderers)
                    {
                        if (renderer.name.Contains("Body") || renderer.name.Contains("Main"))
                        {
                            return renderer;
                        }
                    }
                }
                return renderers[0];
            }

            return null;
        }

        /// <summary>
        /// 更新模型组件，收集morphs并初始化映射。
        /// </summary>
        /// <param name="targetModel">目标模型GameObject</param>
        /// <param name="bodyRenderer">SkinnedMeshRenderer组件（输入/输出）</param>
        /// <param name="availableMorphs">可用morph列表（输入/输出）</param>
        /// <param name="selectedMorphs">已选morph字典（输入/输出）</param>
        /// <param name="directMappingMode">是否为直接映射模式</param>
        /// <param name="vmdMorphFrames">VMD的morph帧</param>
        /// <param name="morphMapping">morph映射表（输入/输出）</param>
        /// <param name="isVmdDataReady">VMD数据是否准备好</param>
        /// <returns>更新后的SkinnedMeshRenderer组件</returns>
        public static SkinnedMeshRenderer UpdateModelComponents(
            GameObject targetModel,
            SkinnedMeshRenderer bodyRenderer,
            List<string> availableMorphs,
            Dictionary<string, bool> selectedMorphs,
            bool directMappingMode,
            List<VMDMorphFrame> vmdMorphFrames,
            Dictionary<string, string> morphMapping,
            Func<bool> isVmdDataReady)
        {
            // 非直接模式下才需要加载模型的SkinnedMeshRenderer
            if (!directMappingMode && bodyRenderer == null)
            {
                bodyRenderer = ModelUtils.FindBodyRenderer(targetModel);
                if (bodyRenderer != null)
                {
                    ModelUtils.CollectAvailableMorphs(bodyRenderer, availableMorphs, selectedMorphs, directMappingMode); // 收集模型形态键
                    if (isVmdDataReady())
                        MorphUtils.InitializeMorphMapping(
                            vmdMorphFrames,
                            availableMorphs,
                            morphMapping,
                            selectedMorphs
                        ); // 基于模型初始化映射
                }
                else
                {
                    // 这里不能用EditorGUILayout.HelpBox，因为这是纯逻辑方法
                    Debug.LogWarning("找不到模型的SkinnedMeshRenderer组件");
                }
            }
            return bodyRenderer;
        }


    }

}