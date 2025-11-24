using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VMDPaser;

namespace UnityMMDConverter.Utils
{
    public static class MorphUtils
    {
        /// <summary>
        /// 规范化形态名称，去除特殊字符并统一格式。
        /// </summary>
        /// <param name="name">原始形态名称</param>
        /// <returns>规范化后的名称</returns>
        public static string NormalizeMorphName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            return name
                .Replace("【", "").Replace("】", "")
                .Replace("(", "").Replace(")", "")
                .Replace("_", "").Replace(" ", "")
                .Trim().ToLowerInvariant()
                .Replace("っ", "つ")
                .Replace("ゃ", "や").Replace("ゅ", "ゆ").Replace("ょ", "よ")
                .Replace("ぁ", "あ").Replace("ぃ", "い").Replace("ぅ", "う")
                .Replace("ぇ", "え").Replace("ぉ", "お");
        }

        // 工具类重构版本，所有依赖的全局变量都通过参数传递
        public static void InitializeDirectMorphMapping(
            List<VMDMorphFrame> vmdMorphFrames,
            bool directMappingMode,
            Dictionary<string, string> morphMapping,
            List<string> availableMorphs,
            Dictionary<string, bool> selectedMorphs)
        {
            if (vmdMorphFrames == null || vmdMorphFrames.Count == 0)
            {
                Debug.LogWarning("VMD形态键帧数据为空，无法初始化直接映射模式");
                return;
            }
            if (directMappingMode)
            {
                Debug.Log("直接映射模式已启用，初始化形态键映射...");
            }
            morphMapping.Clear();
            availableMorphs.Clear();
            selectedMorphs.Clear();

            var vmdMorphNames = vmdMorphFrames.Select(f => f.MorphName).Distinct().ToList();
            availableMorphs.AddRange(vmdMorphNames);

            foreach (var morphName in vmdMorphNames)
            {
                morphMapping[morphName] = morphName;
                selectedMorphs[morphName] = true;
            }
        }

        /// <summary>
        /// 初始化形态键映射关系。
        /// </summary>
        /// <param name="vmdMorphFrames">VMD帧列表</param>
        /// <param name="availableMorphs">模型可用morph列表</param>
        /// <param name="morphMapping">morph映射字典（会被修改）</param>
        public static void InitializeMorphMapping(
            List<VMDMorphFrame> vmdMorphFrames,
            List<string> availableMorphs,
            Dictionary<string, string> morphMapping,
            Dictionary<string, bool> selectedMorphs
            )
        {
            // logging
            Debug.Log("初始化模型形态键映射...");
            morphMapping.Clear();

            if (vmdMorphFrames == null || vmdMorphFrames.Count == 0)
                return;

            var vmdMorphNames = vmdMorphFrames.Select(f => f.MorphName).Distinct().ToList();

            foreach (var vmdMorph in vmdMorphNames)
            {

                // 尝试直接匹配
                if (availableMorphs.Contains(vmdMorph))
                {
                    morphMapping[vmdMorph] = vmdMorph;
                    selectedMorphs[vmdMorph] = true;
                }
                // 尝试规范化匹配
                else
                {
                    var normalizedVmdName = MorphUtils.NormalizeMorphName(vmdMorph);
                    var matchedModelMorph = availableMorphs.FirstOrDefault(
                    m => MorphUtils.NormalizeMorphName(m) == normalizedVmdName);

                    if (matchedModelMorph != null)
                    {
                        morphMapping[vmdMorph] = matchedModelMorph;
                        selectedMorphs[vmdMorph] = true;
                    }
                    else
                    {
                        //Debug.LogWarning($"未找到匹配的形态键: {vmdMorph}");
                        morphMapping[vmdMorph] = vmdMorph; // 保持原名以便后续处理
                        selectedMorphs[vmdMorph] = false; // 默认不选中
                    }
                }
            }
        }



        /// <summary>
        /// 批量选择/取消选择所有morph。
        /// </summary>
        /// <param name="availableMorphs">可用morph列表</param>
        /// <param name="selectedMorphs">morph选择字典（会被修改）</param>
        /// <param name="select">是否选择</param>
        public static void SelectAllMorphs(List<string> availableMorphs, Dictionary<string, bool> selectedMorphs, bool select)
        {
            if (availableMorphs == null || selectedMorphs == null) return;
            foreach (var morph in availableMorphs)
            {
                selectedMorphs[morph] = select;
            }
        }

        /// <summary>
        /// 批量选择前N个morph。
        /// </summary>
        /// <param name="availableMorphs">可用morph列表</param>
        /// <param name="selectedMorphs">morph选择字典（会被修改）</param>
        /// <param name="count">选择数量</param>
        public static void SelectFirstNMorphs(List<string> availableMorphs, Dictionary<string, bool> selectedMorphs, int count)
        {
            if (availableMorphs == null || selectedMorphs == null) return;
            for (var i = 0; i < availableMorphs.Count; i++)
            {
                selectedMorphs[availableMorphs[i]] = i < count;
            }
        }
    }

}
