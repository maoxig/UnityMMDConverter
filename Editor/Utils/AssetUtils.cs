using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMMDConverter.Utils
{
    public static class AssetUtils
    {
        // 检查后缀确保是合法的vmd
        public static bool IsValidVmdFile(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(".vmd", StringComparison.OrdinalIgnoreCase);
        }

        // 修改：移除meta文件复制，解决GUID冲突
        public static string CopyAssetToTemp(string sourcePath, string tempFolder, string targetFileName)
        {
            var sourceFullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), sourcePath);
            var tempFullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), tempFolder, targetFileName);

            // 确保目标目录存在
            EnsureDirectoryExists(Path.GetDirectoryName(tempFullPath));

            // 复制主文件
            File.Copy(sourceFullPath, tempFullPath, overwrite: true);

            // 不要复制meta文件，让Unity自动生成新的GUID
            AssetDatabase.Refresh();
            return Path.Combine(tempFolder, targetFileName);
        }

        public static void BuildAssetBundle(
            string resourcePath,         // 查找原始资源的路径
            string tempBuildFolder,      // 临时文件夹（必须在项目内）
            string bundleBaseName,       // 资源基础名称
            string audioFilePath,
            string bundleOutputPath,     // 打包输出路径
            BuildAssetBundleOptions bundleOptions
        )
        {
            try
            {
                // 确保输出文件名不包含扩展名
                string safeOutputName = Path.GetFileNameWithoutExtension(bundleBaseName);


                EnsureDirectoryExists(resourcePath);

                // 处理打包输出路径（支持项目内/外）
                string fullOutputPath = Path.GetFullPath(bundleOutputPath);
                EnsureDirectoryExists(fullOutputPath);

                // 清理旧临时文件（必须在项目内）
                if (Directory.Exists(tempBuildFolder))
                {
                    AssetDatabase.DeleteAsset(tempBuildFolder);
                }
                EnsureDirectoryExists(tempBuildFolder);

                var tempAssets = new List<string>();


                // 查找动画剪辑
                string clipPath = $"{resourcePath}{bundleBaseName}.anim";
                if (File.Exists(clipPath) && AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
                {
                    tempAssets.Add(clipPath);
                }
                else
                {
                    Debug.LogWarning($"未找到动画剪辑：{clipPath}");
                }

                // 查找并复制动画控制器
                string controllerPath = $"{resourcePath}{bundleBaseName}.controller";
                if (File.Exists(controllerPath) && AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                {

                    tempAssets.Add(controllerPath);
                }
                else
                {
                    Debug.LogWarning($"未找到动画控制器：{controllerPath}");
                }

                if (!string.IsNullOrEmpty(audioFilePath))
                {
                    string audioAssetPath = GetProjectRelativePath(audioFilePath);

                    // 1. 先复制文件到临时目录（这一步会生成新的临时文件）
                    string audioExtension = Path.GetExtension(audioFilePath);
                    string tempAudioPath = CopyAssetToTemp(audioAssetPath, tempBuildFolder, $"{safeOutputName}{audioExtension}");

                    // 2. 获取【临时文件】的Importer进行设置（这是关键修改点）
                    // 必须修改临时文件的设置，因为CopyAssetToTemp丢弃了原meta文件，导致设置重置为默认。
                    var importer = AssetImporter.GetAtPath(tempAudioPath) as AudioImporter;
                    if (importer != null)
                    {
                        // 【关键设置1】启用后台加载
                        // 这允许Unity在后台线程处理音频的预加载，避免阻塞主线程
                        importer.loadInBackground = true;

                        var settings = importer.defaultSampleSettings;

                        // 【关键设置2】设置为 Streaming (流式加载)
                        // 对于舞蹈音乐这种长音频，Streaming 是绝对的最佳选择。
                        // 它只加载极小的缓冲区到内存，剩下的边播边读。
                        // 这能彻底消除 "DecompressOnLoad" 带来的巨大内存峰值和解压卡顿。
                        settings.loadType = AudioClipLoadType.Streaming;

                        // 推荐使用 Vorbis 压缩，它在流式播放下表现很好
                        settings.compressionFormat = AudioCompressionFormat.Vorbis;
                        settings.quality = 0.9f; // 质量设置

                        importer.defaultSampleSettings = settings;

                        // 应用设置并重新导入临时文件
                        importer.SaveAndReimport();
                    }

                    tempAssets.Add(tempAudioPath);
                }

                // 验证是否有足够的资源进行打包
                if (tempAssets.Count < 2)
                {
                    Debug.LogWarning("未找到足够的资源进行打包。至少需要动画剪辑和控制器。");
                    EditorUtility.DisplayDialog("打包失败", "未找到足够的资源进行打包。至少需要动画剪辑和控制器。", "确定");
                    return;
                }

                // 加载所有临时资源
                UnityEngine.Object[] assetsToBundle = tempAssets
                    .Select(p => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p))
                    .Where(o => o != null)
                    .ToArray();

                // 确保主资产是控制器
                UnityEngine.Object mainAsset = assetsToBundle.FirstOrDefault(a => a is AnimatorController) ?? assetsToBundle[0];

                // 构建AssetBundle（输出到bundleOutputPath）
#if UNITY_2023_1_OR_NEWER
                // 新的 AssetBundle 构建方式
                string outputPath = Path.Combine(fullOutputPath, $"{safeOutputName}");
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);

                // 打包规则：你需要用 AssetBundleBuild 来指定主资源和附属资源
                AssetBundleBuild build = new AssetBundleBuild
                {
                    assetBundleName = $"{safeOutputName}.unity3d",
                    assetNames = tempAssets.ToArray() // 用路径数组，而不是 Object[]
                };

                BuildPipeline.BuildAssetBundles(
                    outputPath,
                    new AssetBundleBuild[] { build },
                    bundleOptions,
                    BuildTarget.StandaloneWindows64
                );
#else
                // 旧的 API
#pragma warning disable CS0618
                BuildPipeline.BuildAssetBundle(
                    mainAsset,
                    assetsToBundle,
                    Path.Combine(fullOutputPath, $"{safeOutputName}.unity3d"),
                    bundleOptions | BuildAssetBundleOptions.CollectDependencies | BuildAssetBundleOptions.CompleteAssets,
                    BuildTarget.StandaloneWindows64
                );
#pragma warning restore CS0618
#endif

                // 清理临时文件
                AssetDatabase.DeleteAsset(tempBuildFolder);
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("打包成功",
                    $"已生成AssetBundle：{Path.Combine(fullOutputPath, $"{safeOutputName}.unity3d")}", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("打包失败", $"打包过程中发生错误: {e.Message}", "确定");
                Debug.LogError($"打包错误: {e}");
            }
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static string GetProjectRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return "";

            if (fullPath.StartsWith(Application.dataPath))
            {
                return $"Assets{fullPath.Substring(Application.dataPath.Length)}";
            }

            if (fullPath.Contains("/Assets/"))
            {
                return $"Assets{fullPath.Substring(fullPath.IndexOf("/Assets/") + 7)}";
            }

            return fullPath;
        }

        public static string GetAnimationPath(string newClipName, string outputPath, AnimationClip sourceClip)
        {
            if (!string.IsNullOrEmpty(newClipName) && !string.IsNullOrEmpty(outputPath))
            {
                return $"{outputPath}{newClipName}.anim";
            }
            return sourceClip != null ? AssetDatabase.GetAssetPath(sourceClip) : "";
        }

        public static string GetControllerPath(string controllerName, string outputPath)
        {
            if (!string.IsNullOrEmpty(controllerName) && !string.IsNullOrEmpty(outputPath))
            {
                return $"{outputPath}{controllerName}.controller";
            }
            return "";
        }

        // 为指定剪辑创建控制器
        public static AnimatorController CreateControllerForClip(
            AnimationClip clip,
            string typeSuffix,
            string outputPath,
            string bundleBaseName)
        {
            if (clip == null) return null;

            AssetUtils.EnsureDirectoryExists(outputPath);
            var controllerNameWithSuffix = $"{bundleBaseName}{typeSuffix}";
            var controllerPath = $"{outputPath}{controllerNameWithSuffix}.controller";

            // 如果控制器已存在，先删除旧的
            if (File.Exists(controllerPath))
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // 添加状态
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState(clip.name);
            state.motion = clip;
            stateMachine.defaultState = state;

            // 设置控制器名称与文件名一致
            controller.name = controllerNameWithSuffix;

            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}