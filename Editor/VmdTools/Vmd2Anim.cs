using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using UnityMMDConverter.Utils;
namespace VMD2Anim
{
    /// <summary>批量流水线中单条 VMD 任务（阶段 1 产出 FbxAbsPath，阶段 2 写入 AnimPath）。</summary>
    public sealed class VmdFbxJob
    {
        public string VmdPath;
        public string PmxPath;
        public string AnimOutputDir;
        public string FbxAbsPath;
        public string ErrorMessage;
        public bool Pmx2FbxSucceeded;
    }

    public class VMDConverter : EditorWindow
    {
        #region 配置参数
        public bool QuickConvertMode = true;

        public bool QuickLoadAnim = true; // 是否快速加载动画
        private static ConversionSettings settings;
        private bool isConverting = false;
        private float progress = 0f;
        private string progressMessage = "";

        // 进度条的取消支持
        private CancellationTokenSource cancellationTokenSource;


        #endregion

        #region 窗口初始化，优先级调到最上面
        [MenuItem("MMD for Unity/VMD To Anim Converter", false, 1)]
        public static void ShowWindow()
        {
            GetWindow<VMDConverter>("VMD To Anim Converter");
        }

        private void OnEnable()
        {
            settings = ConversionSettings.Load();
        }
        #endregion

        #region 界面绘制
        private void OnGUI()
        {
            GUILayout.Label("VMD动画转换工具", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(isConverting);

            DrawFilePathField("VMD文件:", ref settings.VmdFilePath, FileType.VMD);
            DrawModelPathField();
            DrawFilePathField("输出路径:", ref settings.OutputPath, FileType.Directory);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("PMX2FBX工具:", GUILayout.Width(100));
            settings.PMX2FBXPath = EditorGUILayout.TextField(settings.PMX2FBXPath);

            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("选择PMX2FBX工具", "", "exe");
                if (!string.IsNullOrEmpty(path))
                    settings.PMX2FBXPath = path;
            }
            // 提示用户需要添加PMX2FBX工具，默认是选中了MMD4Mecanim的PMX2FBX工具，不过也可以指定已有的PMX2FBX工具路径
            if (string.IsNullOrEmpty(settings.PMX2FBXPath) || !File.Exists(settings.PMX2FBXPath))
            {
                EditorGUILayout.HelpBox("请指定PMX2FBX工具路径，默认是MMD4Mecanim的PMX2FBX工具，如果没有，请安装然后指定", MessageType.Warning);
            }

            EditorGUILayout.EndHorizontal();

            settings.OverwriteExisting = EditorGUILayout.Toggle("覆盖已存在文件", settings.OverwriteExisting);
            settings.TimeoutSeconds = EditorGUILayout.IntField("转换超时 (秒):", settings.TimeoutSeconds);

            if (GUILayout.Button("保存配置", GUILayout.Height(20)))
            {
                settings.Save();
                EditorUtility.DisplayDialog("提示", "配置已保存", "确定");
            }

            GUILayout.Space(20);

            if (GUILayout.Button("开始转换", GUILayout.Height(30)))
            {

                ConvertVMDToAnim();
            }

            EditorGUI.EndDisabledGroup();

            if (isConverting)
            {
                EditorGUILayout.LabelField("转换进度:");
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, progressMessage);
                if (GUILayout.Button("取消"))
                {
                    cancellationTokenSource?.Cancel();
                }
                Repaint();
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField("提示: 确保PMX和VMD文件路径无特殊字符，且PMX2FBX工具正常。");
        }


        private void DrawFilePathField(string label, ref string path, FileType fileType)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            path = EditorGUILayout.TextField(path);

            string buttonText = fileType == FileType.Directory ? "浏览" : "选择";
            string fileExtension = fileType == FileType.VMD ? "vmd" : "pmx";
            string dialogTitle = fileType == FileType.VMD ? "选择VMD文件" : "选择PMX文件";

            if (GUILayout.Button(buttonText, GUILayout.Width(60)))
            {
                if (fileType == FileType.Directory)
                {
                    string selectedPath = EditorUtility.OpenFolderPanel(dialogTitle, "", "");
                    if (!string.IsNullOrEmpty(selectedPath))
                        path = selectedPath + "/";
                }
                else
                {
                    string selectedPath = EditorUtility.OpenFilePanel(dialogTitle, "", fileExtension);
                    if (!string.IsNullOrEmpty(selectedPath))
                        path = selectedPath;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModelPathField()
        {
            settings.UseDefaultPMX = EditorGUILayout.Toggle("使用默认PMX模型", settings.UseDefaultPMX);
            if (!settings.UseDefaultPMX)
            {
                DrawFilePathField("PMX文件:", ref settings.PmxFilePath, FileType.PMX);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("默认PMX路径:", GUILayout.Width(100));
                settings.DefaultPmxPath = EditorGUILayout.TextField(settings.DefaultPmxPath);
                EditorGUILayout.EndHorizontal();
            }
        }
        #endregion
        // 主窗口转换方法
        private async void ConvertVMDToAnim()
        {
            cancellationTokenSource = new CancellationTokenSource();
            isConverting = true;
            progress = 0f;
            progressMessage = "";
            try
            {
                bool success = await ConvertVMDInternalAsync(
                    settings.VmdFilePath,
                    settings.UseDefaultPMX ? settings.DefaultPmxPath : settings.PmxFilePath,
                    settings.OutputPath,
                    (p, msg) => { progress = p; progressMessage = msg; Repaint(); },
                    settings.OverwriteExisting,
                    QuickConvertMode,
                    QuickLoadAnim,
                    settings.TimeoutSeconds * 1000,
                    cancellationTokenSource.Token
                );
                if (success)
                {
                    progress = 1f;
                    progressMessage = "转换完成";
                }
            }
            catch (OperationCanceledException)
            {
                progress = 0f;
                progressMessage = "转换已取消";
                UnityEngine.Debug.Log("VMD转换已取消");
            }
            catch (Exception ex)
            {
                progress = 0f;
                progressMessage = $"转换失败: {ex.Message}";
                UnityEngine.Debug.LogError(ex.Message);
            }
            finally
            {
                isConverting = false;
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }

        // 外部API转换方法
        public static async Task<bool> ConvertVMD(
            string vmdPath,
            string pmxPath = null,
            string outputPath = null,
            Action<float, string> progressCallback = null,
            bool? overwrite = null,
            bool quickMode = true,
            int timeoutMs = 180000, // 添加超时参数，默认180秒
            CancellationToken cancellationToken = default // 添加取消令牌
            )
        {
            var settings = ConversionSettings.Load();
            string actualPmxPath = pmxPath ?? (settings.UseDefaultPMX ? settings.DefaultPmxPath : settings.PmxFilePath);
            string actualOutputPath = outputPath ?? settings.OutputPath;
            bool actualOverwrite = overwrite ?? settings.OverwriteExisting;

            // 调用公共转换方法
            return await ConvertVMDInternalAsync(
                vmdPath,
                actualPmxPath,
                actualOutputPath,
                progressCallback,
                actualOverwrite,
                quickMode: quickMode, // 外部调用默认快速模式
                true,
                timeoutMs, // 传递超时参数
                cancellationToken // 传递取消令牌
            );
        }

        private static async Task<bool> ConvertVMDInternalAsync(
           string vmdPath,
           string pmxPath,
           string outputPath,
           Action<float, string> progressCallback,
           bool overwriteExisting,
           bool quickMode,
           bool quickLoadAnim,
           int timeoutMs,
           CancellationToken cancellationToken
           )
        {
            // 验证参数
            if (!ValidateConversionParams(vmdPath, pmxPath, ConversionSettings.Load().PMX2FBXPath))
            {
                progressCallback?.Invoke(0f, "参数验证失败，转换中止。");
                return false;
            }

            progressCallback?.Invoke(0.2f, "准备转换...");

            // 核心路径变量（完全使用项目内路径）
            string vmdFileName = Path.GetFileNameWithoutExtension(vmdPath);
            string pmxDir = Path.GetDirectoryName(pmxPath); // PMX所在目录（项目内）
            string fbxPath = Path.Combine(pmxDir, $"{vmdFileName}.fbx"); // FBX直接生成在PMX同目录
            string finalAnimPath = Path.Combine(outputPath, $"{vmdFileName}.anim");

            string generatedFbxAbsPath = null;
            try
            {
                // 检查覆盖
                if (File.Exists(finalAnimPath) && !overwriteExisting)
                {
                    progressCallback?.Invoke(0f, "目标文件已存在且未允许覆盖，转换中止。");
                    return false;
                }

                progressCallback?.Invoke(0.3f, "生成FBX文件...");
                generatedFbxAbsPath = await RunPMX2FBXAsync(
                    ConversionSettings.Load().PMX2FBXPath,
                    pmxPath,
                    vmdPath,
                    pmxDir,
                    quickMode,
                    timeoutMs,
                    cancellationToken);

                UnityEngine.Debug.Log($"[VMD转换] FBX生成成功: {generatedFbxAbsPath}");

                progressCallback?.Invoke(0.5f, "导入FBX并提取动画...");
                ImportFbxAndSaveAnim(
                    generatedFbxAbsPath,
                    finalAnimPath,
                    overwriteExisting,
                    quickLoadAnim,
                    progressCallback,
                    scheduleFbxCleanup: true);

                progressCallback?.Invoke(1.0f, "转换完成!");
                return true;
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke(0f, $"转换失败: {ex.Message}");
                UnityEngine.Debug.LogError($"[VMD转换] 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 提取动画剪辑（简化路径依赖）
        /// </summary>
        private static AnimationClip ExtractHumanoidAnimation(string fbxAssetPath, string vmdFileName, bool quickLoadAnim)
        {
            // 强制验证FBX路径在Assets内
            if (!fbxAssetPath.StartsWith("Assets/"))
                throw new Exception($"FBX路径必须在Assets目录内: {fbxAssetPath}");

            AnimationClip sourceClip = null;


            // 常规模式：加载整个FBX并从中提取第一个动画
            sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxAssetPath);
            if (sourceClip == null)
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath);
                if (assets == null || assets.Length == 0)
                    throw new Exception($"FBX无资产: {fbxAssetPath}");

                sourceClip = assets.OfType<AnimationClip>().FirstOrDefault();
                if (sourceClip == null)
                    throw new Exception($"FBX中无AnimationClip");
            }

            return CloneAnimationClip(sourceClip);
        }

        private static AnimationClip CloneAnimationClip(AnimationClip sourceClip)
        {
            AnimationClip newClip = new AnimationClip();
            newClip.name = sourceClip.name;
            newClip.frameRate = sourceClip.frameRate;
            newClip.legacy = false;

            // 设置动画曲线
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(sourceClip);
            foreach (var binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                if (curve != null)
                    newClip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }

            // 设置动画属性
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(newClip);
            clipSettings.loopTime = false;
            clipSettings.loopBlendOrientation = true;
            clipSettings.loopBlendPositionY = true;
            clipSettings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(newClip, clipSettings);

            return newClip;
        }
        private static void EnsureFBXImportSettings(string unityFbxPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(unityFbxPath) as ModelImporter;
            if (importer == null)
                throw new Exception($"无法获取FBX导入器: {unityFbxPath}");

            // 强制设置为Humanoid类型（参考AutoChangeImporter_Vmd2Anim）
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.importAnimation = true;
                // 优化压缩
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.SaveAndReimport();
            }
        }



        #region 辅助方法
        // 验证参数（保持不变）
        private static bool ValidateConversionParams(string vmdPath, string pmxPath, string pmx2FbxPath)
        {
            if (string.IsNullOrEmpty(vmdPath) || !File.Exists(vmdPath))
            {
                EditorUtility.DisplayDialog("错误", "请选择有效的VMD文件", "确定");
                return false;
            }
            if (string.IsNullOrEmpty(pmxPath) || !File.Exists(pmxPath))
            {
                EditorUtility.DisplayDialog("错误", "PMX文件不存在，请检查路径", "确定");
                return false;
            }
            if (string.IsNullOrEmpty(pmx2FbxPath) || !File.Exists(pmx2FbxPath))
            {
                EditorUtility.DisplayDialog("错误", "PMX2FBX工具不存在，请检查路径", "确定");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 阶段 1：并行调用 PMX2FBX（不修改工具目录 pmx2fbx.xml，不触碰 Unity AssetDatabase）。
        /// </summary>
        public static Task<string> RunPMX2FBXAsync(
            string toolPath,
            string pmxPath,
            string vmdPath,
            string workDir,
            bool quickMode = false,
            int timeoutMs = 180000,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(
                () => RunPMX2FBXSync(toolPath, pmxPath, vmdPath, workDir, quickMode, timeoutMs, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// 为并行任务复制 PMX 到独立子目录，避免多个进程写同一 FBX。
        /// </summary>
        public static string PrepareIsolatedPmxWorkspace(string pmxPath, string vmdPath, string batchTempRoot)
        {
            pmxPath = Path.GetFullPath(pmxPath);
            vmdPath = Path.GetFullPath(vmdPath);
            string vmdKey = Path.GetFileNameWithoutExtension(vmdPath);
            string jobDir = Path.Combine(batchTempRoot, $"{SanitizeDirName(vmdKey)}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(jobDir);

            string destPmx = Path.Combine(jobDir, Path.GetFileName(pmxPath));
            File.Copy(pmxPath, destPmx, true);
            return destPmx;
        }

        /// <summary>
        /// 阶段 2：在主线程导入 FBX 并写出 .anim。
        /// </summary>
        public static bool ImportFbxAndSaveAnim(
            string fbxAbsPath,
            string finalAnimPath,
            bool overwriteExisting,
            bool quickLoadAnim,
            Action<float, string> progressCallback = null,
            bool scheduleFbxCleanup = true)
        {
            if (File.Exists(finalAnimPath) && !overwriteExisting)
            {
                progressCallback?.Invoke(0f, "目标 .anim 已存在且未允许覆盖");
                return false;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            string fbxAssetPath = MakeRelativePath(fbxAbsPath);

            EnsureFBXImportSettings(fbxAssetPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string vmdFileName = Path.GetFileNameWithoutExtension(finalAnimPath);
            AnimationClip extractedClip = ExtractHumanoidAnimation(fbxAssetPath, vmdFileName, quickLoadAnim);
            if (extractedClip == null)
                throw new Exception("未从 FBX 中找到有效的 Humanoid 动画");

            progressCallback?.Invoke(0.9f, "保存动画文件...");
            string animDir = Path.GetDirectoryName(finalAnimPath);
            if (!string.IsNullOrEmpty(animDir) && !Directory.Exists(animDir))
                Directory.CreateDirectory(animDir);

            string relativeAnimPath = MakeRelativePath(finalAnimPath);
            if (!relativeAnimPath.StartsWith("Assets/"))
                throw new Exception($"无效的资产路径: {relativeAnimPath}");

            AssetDatabase.CreateAsset(extractedClip, relativeAnimPath);
            AnimUtils.ApplyAnimationClipSettings(extractedClip);
            EditorUtility.SetDirty(extractedClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (scheduleFbxCleanup)
                CleanupGeneratedFbx(fbxAssetPath);

            return true;
        }

        public static string FindGeneratedFbxPath(string pmxPath, string vmdPath)
        {
            pmxPath = Path.GetFullPath(pmxPath);
            vmdPath = Path.GetFullPath(vmdPath);
            string pmxDir = Path.GetDirectoryName(pmxPath);
            string vmdBase = Path.GetFileNameWithoutExtension(vmdPath);

            var candidates = new[]
            {
                Path.Combine(pmxDir, vmdBase + ".fbx"),
                Path.ChangeExtension(pmxPath, ".fbx"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }

            throw new FileNotFoundException(
                $"未找到 PMX2FBX 生成的 FBX（已尝试: {string.Join(", ", candidates)}）");
        }

        private static string SanitizeDirName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrEmpty(name) ? "job" : name;
        }

        private static string RunPMX2FBXSync(
            string toolPath,
            string pmxPath,
            string vmdPath,
            string workDir,
            bool quickMode,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            toolPath = Path.GetFullPath(toolPath);
            pmxPath = Path.GetFullPath(pmxPath);
            vmdPath = Path.GetFullPath(vmdPath);
            workDir = Path.GetFullPath(workDir);

            if (quickMode)
                UnityEngine.Debug.Log("[VMD转换] 快速模式已启用（不再改写工具目录 pmx2fbx.xml，使用工具自带配置）");

            string arguments = $"\"{pmxPath}\" \"{vmdPath}\"";
            UnityEngine.Debug.Log($"[PMX2FBX] 执行命令: {toolPath} {arguments}");
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = toolPath,
                    Arguments = arguments,
                    WorkingDirectory = workDir,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                process.Start();

                var startTime = DateTime.Now;
                while (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try { process.Kill(); }
                        catch { /* ignore */ }
                        throw new OperationCanceledException("PMX2FBX转换被取消", cancellationToken);
                    }

                    if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                    {
                        process.Kill();
                        throw new TimeoutException(
                            $"PMX2FBX工具执行超时（{timeoutMs / 1000}秒），可能是文件错误或工具无响应");
                    }

                    process.WaitForExit(100);
                }

                if (process.ExitCode != 0)
                    throw new Exception($"PMX2FBX执行失败，退出码: {process.ExitCode}");

                return FindGeneratedFbxPath(pmxPath, vmdPath);
            }
        }

        // 清理生成的FBX文件（转换完成后移除，避免项目污染）
        private static async void CleanupGeneratedFbx(string fbxPath)
        {
            if (string.IsNullOrEmpty(fbxPath)) return;

            // 延迟10秒再执行清理
            await Task.Delay(30000);

            // 删除FBX文件及其meta文件
            if (AssetDatabase.DeleteAsset(fbxPath))
            {
                UnityEngine.Debug.Log($"[VMD转换] 清理临时FBX: {fbxPath}");
            }
            // string metaPath = $"{fbxPath}.meta";
            // if (File.Exists(metaPath))
            // {
            //     File.Delete(metaPath);
            // }
        }
        /// <summary>
        /// 修复路径转换，确保生成正确的 Assets 相对路径
        /// </summary>
        public static string MakeRelativePath(string absolutePath)
        {
            try
            {
                string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
                absolutePath = absolutePath.Replace('\\', '/');

                // 强制路径以项目根目录为基准
                if (!absolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    // 若路径不在项目内，尝试直接拼接Assets（处理用户输出路径在Assets外的情况）
                    if (absolutePath.Contains("Assets/"))
                        return absolutePath.Substring(absolutePath.IndexOf("Assets/"));
                    else
                        throw new Exception($"输出路径必须在项目内: {absolutePath}");
                }

                // 生成相对路径
                string relativePath = absolutePath.Substring(projectRoot.Length).TrimStart('/');
                if (!relativePath.StartsWith("Assets/"))
                    relativePath = $"Assets/{relativePath}";

                return relativePath;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"路径转换失败: {ex.Message}，原始路径: {absolutePath}");
                throw; // 直接抛出，避免使用无效路径
            }
        }


        #endregion

    }

    [Serializable]
    public class ConversionSettings : ScriptableObject
    {
        public string VmdFilePath = "";
        public string PmxFilePath = "";
        public string DefaultPmxPath = "Assets/UnityMMDConverter/Model/Default.pmx";
        public string OutputPath = "Assets/UnityMMDConverter/Output/";
        public string PMX2FBXPath = "Assets/MMD4Mecanim/Editor/PMX2FBX/pmx2fbx.exe";
        public bool UseDefaultPMX = true;
        public bool OverwriteExisting = false;
        public int TimeoutSeconds = 180;


        private const string SETTINGS_PATH = "Assets/VMD2AnimSettings.asset";

        public static ConversionSettings Load()
        {
            ConversionSettings settings = AssetDatabase.LoadAssetAtPath<ConversionSettings>(SETTINGS_PATH);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ConversionSettings>();
                AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        public void Save()
        {
            ConversionSettings settings = AssetDatabase.LoadAssetAtPath<ConversionSettings>(SETTINGS_PATH);
            if (settings == null)
            {
                AssetDatabase.CreateAsset(this, SETTINGS_PATH);
            }
            else
            {
                EditorUtility.CopySerialized(this, settings);
            }
            AssetDatabase.SaveAssets();
        }
    }

    public enum FileType
    {
        VMD,
        PMX,
        Directory
    }
}