
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using VMDPaser;
using System.Linq;
using System;
using System.IO;

namespace UnityMMDConverter.Utils
{
    public static class AnimUtils
    {
        /// <summary>
        /// 设置动画曲线所有关键帧的切线模式为线性
        /// </summary>
        /// <param name="curve">要设置的动画曲线</param>
        public static void SetCurveTangentMode(AnimationCurve curve)
        {
            if (curve == null) return;
            for (var i = 0; i < curve.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
        }

        public static void AdjustAnimationLength(AnimationClip clip, Dictionary<string, List<VMDMorphFrame>> morphFramesGrouped)
        {
            // TODO
        }


        /// <summary>
        /// 在动画剪辑的开头和结尾添加Camera激活/关闭的关键帧
        /// </summary>
        /// <param name="clip">目标动画剪辑</param>
        /// <param name="cameraPath">相机对象的路径</param>
        /// <param name="startTime">开始时间（秒）</param>
        /// <param name="endTime">结束时间（秒），默认使用动画剪辑的长度</param>
        public static AnimationClip AddCameraActivationKeyframes(AnimationClip clip, string cameraPath, float startTime = 0f, float endTime = -1f)
        {
            // 如果未指定结束时间，则使用动画剪辑的长度
            if (endTime < 0f)
            {
                endTime = clip.length;
            }


            AnimationCurve activeCurve = new AnimationCurve();

            // 在开始时间添加关键帧，值为1（激活GameObject）
            activeCurve.AddKey(startTime, 1f);

            // 在结束时间添加关键帧，值为0（关闭GameObject）
            activeCurve.AddKey(endTime, 0f);

            // 设置曲线在关键帧之间保持常数值（不插值）
            foreach (Keyframe keyframe in activeCurve.keys)
            {
                int index = System.Array.IndexOf(activeCurve.keys, keyframe);
                activeCurve.SmoothTangents(index, 0f);
            }

            // 将曲线添加到动画剪辑中
            // 使用m_IsActive属性控制GameObject的激活状态
            clip.SetCurve(cameraPath, typeof(GameObject), "m_IsActive", activeCurve);

            return clip;
        }
        public static AnimationClip AddMorphCurveToClip(AnimationClip clip, string path, string morphName, List<VMDMorphFrame> frames, float frameRate = 30f)
        {

            if (frames == null || frames.Count == 0)
            {
                Debug.LogWarning($"跳过空形态键帧: {morphName}");
                return null;
            }

            var keyframes = new Keyframe[frames.Count];
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var time = frame.FrameIndex / frameRate;
                var weight = Mathf.Clamp(frame.Weight * 100.0f, 0f, 100.0f);

                keyframes[i] = new Keyframe(time, weight);
                keyframes[i].inTangent = float.PositiveInfinity;
                keyframes[i].outTangent = float.PositiveInfinity;
            }

            var curve = new AnimationCurve(keyframes);
            AnimUtils.SetCurveTangentMode(curve);

            var binding = new EditorCurveBinding
            {
                path = path,
                type = typeof(SkinnedMeshRenderer),
                propertyName = $"blendShape.{morphName}"
            };

            AnimationUtility.SetEditorCurve(clip, binding, curve);
            return clip;

        }

        /// <summary>
        /// 直接映射模式下为动画剪辑添加morph曲线。
        /// </summary>
        /// <param name="clip">目标动画剪辑</param>
        /// <param name="vmdMorphFrames">VMD解析得到的morph帧列表</param>
        /// <param name="selectedMorphs">用户选择的morph字典</param>
        /// <param name="morphMapping">morph映射表</param>
        /// <param name="defaultSkinnedMeshPath">SkinnedMeshRenderer路径</param>
        /// <returns>处理后的AnimationClip</returns>
        public static AnimationClip AddMorphCurvesDirectMode(
            AnimationClip clip,
            List<VMDMorphFrame> vmdMorphFrames,
            Dictionary<string, bool> selectedMorphs,
            Dictionary<string, string> morphMapping,
            string defaultSkinnedMeshPath)
        {
            if (vmdMorphFrames == null || vmdMorphFrames.Count == 0 || clip == null)
                return clip;

            var skinnedMeshPath = defaultSkinnedMeshPath;

            // 清空现有曲线以避免重复
            AnimationUtility.SetEditorCurves(clip, new EditorCurveBinding[0], new AnimationCurve[0]);

            // 分组处理VMD中的每个表情帧，使用用户设置的映射关系
            var morphFramesGrouped = vmdMorphFrames
            .Where(f =>
                selectedMorphs != null &&
                selectedMorphs.TryGetValue(f.MorphName, out bool isSelected) && isSelected &&
                morphMapping != null &&
                morphMapping.TryGetValue(f.MorphName, out string targetMorph) &&
                !string.IsNullOrEmpty(targetMorph))
            .GroupBy(f => morphMapping[f.MorphName]) // 按目标形态键名称分组
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.FrameIndex).ToList());

            foreach (var group in morphFramesGrouped)
            {
                // 获取目标形态键名称
                string targetMorphName = group.Key;

                // 为每个目标形态键添加动画曲线
                AnimUtils.AddMorphCurveToClip(clip, skinnedMeshPath, targetMorphName, group.Value);
            }

            AnimUtils.AdjustAnimationLength(clip, morphFramesGrouped);
            Debug.Log($"直接映射模式: 已添加 {morphFramesGrouped.Count} 个形态键曲线");
            return clip;
        }


        /// <summary>
        /// 向动画剪辑添加morph曲线（非直接映射模式）。
        /// </summary>
        /// <param name="clip">目标动画剪辑</param>
        /// <param name="vmdMorphFrames">VMD解析得到的morph帧列表</param>
        /// <param name="selectedMorphs">用户选择的morph字典</param>
        /// <param name="morphMapping">morph映射表</param>
        /// <param name="targetModel">目标模型</param>
        /// <param name="bodyRenderer">目标模型的SkinnedMeshRenderer</param>
        /// <returns>添加曲线后的动画剪辑</returns>
        public static AnimationClip AddMorphCurvesToAnimation(
            AnimationClip clip,
            List<VMDMorphFrame> vmdMorphFrames,
            Dictionary<string, bool> selectedMorphs,
            Dictionary<string, string> morphMapping,
            GameObject targetModel,
            SkinnedMeshRenderer bodyRenderer)
        {
            if (clip == null) return null;

            // AnimationUtility.SetEditorCurves(clip, new EditorCurveBinding[0], new AnimationCurve[0]);
            if (vmdMorphFrames == null || vmdMorphFrames.Count == 0)
                return clip;

            var bodyRendererPath = ModelUtils.GetBodyRendererPath(targetModel, bodyRenderer);

            // 分组处理VMD中的每个表情帧，使用用户设置的映射关系
            var morphFramesGrouped = vmdMorphFrames
            .Where(f =>
                selectedMorphs != null &&
                selectedMorphs.TryGetValue(f.MorphName, out bool isSelected) && isSelected &&
                morphMapping != null &&
                morphMapping.TryGetValue(f.MorphName, out string targetMorph) &&
                !string.IsNullOrEmpty(targetMorph))
            .GroupBy(f => morphMapping[f.MorphName]) // 按目标形态键名称分组
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.FrameIndex).ToList());

            foreach (var group in morphFramesGrouped)
            {
                // 获取目标形态键名称
                string targetMorphName = group.Key;

                // 为每个目标形态键添加动画曲线
                AnimUtils.AddMorphCurveToClip(clip, bodyRendererPath, targetMorphName, group.Value);
            }

            AnimUtils.AdjustAnimationLength(clip, morphFramesGrouped);
            Debug.Log($"成功添加 {morphFramesGrouped.Count} 个morph曲线到动画剪辑: {clip.name}");

            return clip;
        }

        /// <summary>
        /// 将相机VMD帧数据的动画曲线添加到目标AnimationClip。
        /// </summary>
        /// <param name="targetClip">目标动画剪辑</param>
        /// <param name="vmdCameraFrames">已解析的相机帧数据</param>
        /// <param name="cameraRootPath">相机根路径</param>
        /// <param name="cameraDistancePath">相机距离路径</param>
        /// <param name="cameraComponentPath">相机组件路径</param>
        /// <param name="enableCameraAnimation">是否启用相机动画</param>

        public static AnimationClip AddCameraCurvesToClip(
            AnimationClip targetClip,
            string cameraVmdPath,
            string cameraRootPath,
            string cameraDistancePath,
            string cameraComponentPath,
            float cameraScale)
        {
            try
            {
                // 生成临时相机剪辑
                Debug.Log($"开始生成相机曲线，VMD路径: {cameraVmdPath}");
                var tempAgent = new CameraVmdAgent(cameraVmdPath, cameraRootPath, cameraDistancePath, cameraComponentPath, cameraScale);
                AnimationClip cameraClip = tempAgent.CreateAnimationClip();

                if (cameraClip == null)
                {
                    Debug.LogError("CameraVmdAgent生成的相机剪辑为空");
                    return null;
                }

                // 验证临时相机剪辑是否包含曲线
                var cameraBindings = AnimationUtility.GetCurveBindings(cameraClip);
                if (cameraBindings.Length == 0)
                {
                    Debug.LogError("生成的相机剪辑中未包含任何曲线");
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(cameraClip));
                    return null;
                }
                Debug.Log($"成功获取相机剪辑，包含 {cameraBindings.Length} 条曲线");

                // 将相机曲线复制到目标剪辑
                foreach (var binding in cameraBindings)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(cameraClip, binding);
                    if (curve == null || curve.keys.Length == 0)
                    {
                        Debug.LogWarning($"跳过无效曲线: {binding.path}.{binding.propertyName}");
                        continue;
                    }
                    AnimationUtility.SetEditorCurve(targetClip, binding, curve);
                }

                // 添加相机激活/关闭关键帧
                targetClip = AnimUtils.AddCameraActivationKeyframes(targetClip, cameraComponentPath);

                // 清理临时资源
                string tempClipPath = AssetDatabase.GetAssetPath(cameraClip);
                if (!string.IsNullOrEmpty(tempClipPath))
                {
                    AssetDatabase.DeleteAsset(tempClipPath);
                    Debug.Log("清理临时相机剪辑资源");
                }

                return targetClip;
            }
            catch (Exception e)
            {
                Debug.LogError($"添加相机曲线失败: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        public static AnimationClip CreateOriginalAnimationClip(
            AnimationClip sourceClip,
            string bundleBaseName,
            float frameRate
        )
        {
            if (sourceClip == null)
            {
                Debug.LogError("CreateOriginalAnimationClip: sourceClip is null.");
                return null;
            }

            // 复制原动画曲线
            AnimationClip originalClip = new AnimationClip
            {
                name = bundleBaseName,
                frameRate = frameRate,
                wrapMode = sourceClip.wrapMode
            };

            // 复制源动画所有曲线
            var sourceBindings = AnimationUtility.GetCurveBindings(sourceClip);
            foreach (var binding in sourceBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                AnimationUtility.SetEditorCurve(originalClip, binding, curve);
            }

            return originalClip;
        }

        /// <summary>
        /// 强制应用动画片段的循环和烘焙设置
        /// </summary>
        public static void ApplyAnimationClipSettings(AnimationClip clip)
        {
            // 获取当前的设置（基于默认值）
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);

            // 设置你的目标属性
            settings.loopTime = false; // 如果是舞蹈动作，通常不需要 LoopTime，除非你确定它是循环的

            //即使 loopTime 为 false，为了防止根骨骼位移导致的“飘移”，
            // 通常也建议把 Bake Into Pose 打开。
            // 但是请注意：在 Unity Inspector 中，如果 loopTime 为 false，
            // 这些选项可能会变灰不可选，但在代码中设置是生效的。
            settings.loopBlendOrientation = true; // Bake Into Pose: Root Transform Rotation
            settings.loopBlendPositionY = true;   // Bake Into Pose: Root Transform Position (Y)
            settings.loopBlendPositionXZ = true;  // Bake Into Pose: Root Transform Position (XZ)

            // 保持原本的 OrientationOffsetY 等设置默认即可，或者根据需要设置
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;

            // 应用设置到资源
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }


    }

}