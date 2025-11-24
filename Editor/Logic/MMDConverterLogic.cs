using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using VMDPaser;
using UnityMMDConverter.Utils;

namespace UnityMMDConverter.Logic
{
    /// <summary>
    /// 处理具体的转换、解析和生成逻辑，与UI分离
    /// </summary>
    public static class MMDConverterLogic
    {
        // 解析相机VMD
        public static bool ParseCameraVmds(List<string> filePaths, out List<VMD> parsedVmds, out List<VMDCameraFrame> frames)
        {
            parsedVmds = new List<VMD>();
            frames = new List<VMDCameraFrame>();

            if (filePaths == null || filePaths.Count == 0) return false;

            try
            {
                foreach (var path in filePaths)
                {
                    if (!File.Exists(path)) continue;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        var vmd = VMDParser.ParseVMD(stream);
                        parsedVmds.Add(vmd);
                        frames.AddRange(vmd.Cameras);
                    }
                }
                frames = frames.OrderBy(f => f.FrameIndex).ToList();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Camera Parse Error: {ex.Message}");
                return false;
            }
        }

        // 解析表情VMD
        public static bool ParseMorphVmds(List<string> filePaths, out List<VMD> parsedVmds, out List<VMDMorphFrame> frames)
        {
            parsedVmds = new List<VMD>();
            frames = new List<VMDMorphFrame>();

            if (filePaths == null || filePaths.Count == 0) return false;

            try
            {
                foreach (var path in filePaths)
                {
                    if (!File.Exists(path)) continue;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        var vmd = VMDParser.ParseVMD(stream);
                        parsedVmds.Add(vmd);
                        frames.AddRange(vmd.Morphs);
                    }
                }
                frames = frames.OrderBy(f => f.FrameIndex).ToList();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Morph Parse Error: {ex.Message}");
                return false;
            }
        }
        // 创建Timeline预览 (重构后的逻辑)
        public static void CreateTimelinePreview(GameObject model, string bundleBaseName, string outputPath, string audioPath)
        {
            if (model == null)
            {
            Debug.LogError("Model is null. Please provide a valid GameObject.");
            return;
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
            animator = model.AddComponent<Animator>();
            Debug.Log("Animator component added to the model.");
            }

            string controllerPath = Path.Combine(outputPath, $"{bundleBaseName}.controller");
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            if (controller == null)
            {
            Debug.LogError($"Controller not found at path: {controllerPath}");
            return;
            }

            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(model);

            string timelinePath = Path.Combine(outputPath, $"{bundleBaseName}_preview.asset");
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timeline == null)
            {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, timelinePath);
            }

            // Clear existing tracks
            foreach (var track in timeline.GetOutputTracks())
            {
            timeline.DeleteTrack(track);
            }

            // Set up PlayableDirector
            var directorObj = GameObject.Find($"{bundleBaseName}_director") ?? new GameObject($"{bundleBaseName}_director");
            var director = directorObj.GetComponent<PlayableDirector>() ?? directorObj.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;

            // Bind animation track
            AnimationTrack animTrack = timeline.CreateTrack<AnimationTrack>(null, "Animation");
            director.SetGenericBinding(animTrack, model);

            // Add animation clips
            foreach (var clip in controller.animationClips)
            {
            if (clip.name.Contains(bundleBaseName))
            {
                var tlClip = animTrack.CreateDefaultClip();
                tlClip.duration = clip.length;
                ((AnimationPlayableAsset)tlClip.asset).clip = clip;
            }
            }

            // Add audio track
            if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
            {
            AudioTrack audioTrack = timeline.CreateTrack<AudioTrack>(null, "Audio");
            var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
            if (audioClip != null)
            {
                var tlClip = audioTrack.CreateDefaultClip();
                tlClip.duration = audioClip.length;
                ((AudioPlayableAsset)tlClip.asset).clip = audioClip;
            }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
        }
    }
}