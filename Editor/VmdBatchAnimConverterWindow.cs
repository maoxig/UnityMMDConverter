using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityMMDConverter.CustomGUI;
using UnityMMDConverter.Utils;
using VMD2Anim;
using static UnityMMDConverter.LocalizationManager;
using static UnityMMDConverter.L10nKeys;

namespace UnityMMDConverter
{
    /// <summary>
    /// 批量将 VMD 转为 .anim：并行 PMX2FBX，每个 FBX 完成后立即导入生成 .anim。
    /// </summary>
    public class VmdBatchAnimConverterWindow : EditorWindow
    {
        private const string DefaultOutputPath = "Assets/UnityMMDConverter/Output/";
        private const string BatchTempRoot = "Assets/UnityMMDConverter/Temp/Batch/";
        private const string EditorPrefsPrefix = "UnityMMDConverter.VmdBatch.";

        private enum BatchItemState
        {
            Pending,
            Running,
            Success,
            Failed,
            Skipped,
            Cancelled
        }

        private sealed class BatchItem
        {
            public string VmdPath;
            public string ResolvedPmxPath;
            public string AnimOutputDir;
            public string FbxAbsPath;
            public string JobWorkspaceDir;
            public BatchItemState State = BatchItemState.Pending;
            public string Message = "";
        }

        private readonly List<BatchItem> batchItems = new List<BatchItem>();

        private Vector2 mainScrollPos;
        private Vector2 listScrollPos;

        private bool showSettings = true;
        private bool showAdvanced;

        private bool quickMode;
        private bool useReferencePmx;
        private bool autoFindPmxBesideVmd = true;
        private string referencePmxPath = "";

        private OutputLocationMode outputLocationMode = OutputLocationMode.SameAsVmd;
        private string customOutputPath = DefaultOutputPath;

        private bool overwriteExisting = true;
        private int timeoutSeconds = 300;
        private bool scanSubfolders = true;
        private int maxParallel = 4;

        private bool isRunning;
        private string batchPhase = "";
        private float overallProgress;
        private string progressMessage = "";
        private int completedCount;
        private int successCount;
        private int failedCount;

        private CancellationTokenSource cancellationTokenSource;
        private int batchFinishedJobs;

        [MenuItem("MMD for Unity/VMD Batch To Anim", false, 2)]
        public static void ShowWindow()
        {
            var window = GetWindow<VmdBatchAnimConverterWindow>("VMD Batch To Anim");
            window.minSize = new Vector2(520, 560);
        }

        private void OnEnable()
        {
            LoadEditorPrefs();
            var settings = ConversionSettings.Load();
            if (string.IsNullOrEmpty(customOutputPath))
                customOutputPath = settings.OutputPath;
        }

        private void OnDisable()
        {
            SaveEditorPrefs();
        }

        private void OnGUI()
        {
            mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

            EditorGUILayout.LabelField("VMD 批量转 .anim", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "并行调用 PMX2FBX；每个 VMD 的 FBX 生成成功后，会立刻导入并写出 .anim（默认与 VMD 同目录）。\n" +
                "请将所有 VMD 放在当前项目 Assets 下；可拖拽添加，也可用文件夹批量导入。",
                MessageType.Info);

            DrawFileListSection();
            EditorGUILayout.Space();
            DrawSettingsSection();
            EditorGUILayout.Space();
            DrawActionSection();

            if (isRunning)
                DrawProgressSection();

            DrawSummarySection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawFileListSection()
        {
            EditorGUILayout.LabelField("待转换 VMD 列表", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("拖拽 .vmd 到下方槽位，或点击右侧按钮选择文件", EditorStyles.miniLabel);

            var pathList = batchItems.Select(i => i.VmdPath).ToList();
            MMDCustomGUI.DrawFileSelectorList(pathList, Get(ANIM_VMD_FILE), "vmd");
            SyncPathListToBatchItems(pathList);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("从文件夹添加…", GUILayout.Height(22)))
                AddVmdsFromFolder();
            if (GUILayout.Button("重置状态", GUILayout.Width(72), GUILayout.Height(22)))
            {
                foreach (var item in batchItems)
                {
                    item.State = BatchItemState.Pending;
                    item.Message = "";
                    item.FbxAbsPath = null;
                    item.JobWorkspaceDir = null;
                }
            }
            if (GUILayout.Button("清空列表", GUILayout.Width(72), GUILayout.Height(22)))
            {
                if (EditorUtility.DisplayDialog("确认", "确定清空全部 VMD？", "确定", "取消"))
                    batchItems.Clear();
            }
            EditorGUILayout.EndHorizontal();

            int pending = batchItems.Count(i => i.State == BatchItemState.Pending);
            EditorGUILayout.LabelField($"共 {batchItems.Count} 个（待处理 {pending}）", EditorStyles.miniLabel);

            if (batchItems.Count > 0)
            {
                listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, GUILayout.MaxHeight(140));
                foreach (var item in batchItems)
                {
                    DrawBatchItemRow(item);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawBatchItemRow(BatchItem item)
        {
            EditorGUILayout.BeginHorizontal();
            var color = GetStateColor(item.State);
            var prev = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(GetStateLabel(item.State), GUILayout.Width(52));
            GUI.color = prev;

            EditorGUILayout.LabelField(Path.GetFileName(item.VmdPath), EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(item.Message))
                EditorGUILayout.LabelField(item.Message, EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettingsSection()
        {
            showSettings = EditorGUILayout.Foldout(showSettings, "转换设置", true);
            if (!showSettings)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");

            quickMode = EditorGUILayout.Toggle(Get(QUICK_CONFIG), quickMode);

            useReferencePmx = EditorGUILayout.Toggle("使用参考 PMX/PMD 模型", useReferencePmx);
            if (useReferencePmx)
            {
                referencePmxPath = MMDCustomGUI.DrawSingleFileSelector(
                    referencePmxPath, Get(PMX_FILE), "pmx", "pmd");
                autoFindPmxBesideVmd = EditorGUILayout.Toggle(
                    "未指定时自动查找 VMD 同目录 PMX/PMD",
                    autoFindPmxBesideVmd);
            }
            else
            {
                var settings = ConversionSettings.Load();
                EditorGUILayout.HelpBox(
                    $"将使用 VMD To Anim 配置中的默认模型：\n{(settings.UseDefaultPMX ? settings.DefaultPmxPath : settings.PmxFilePath)}",
                    MessageType.None);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(Get("output_location"), EditorStyles.miniBoldLabel);
            outputLocationMode = (OutputLocationMode)EditorGUILayout.EnumPopup(outputLocationMode);

            switch (outputLocationMode)
            {
                case OutputLocationMode.DefaultFolder:
                    EditorGUILayout.HelpBox(
                        string.Format(Get("output_location_default"), DefaultOutputPath),
                        MessageType.None);
                    break;
                case OutputLocationMode.SameAsVmd:
                    EditorGUILayout.HelpBox(Get("output_location_same_folder"), MessageType.None);
                    break;
            }

            if (outputLocationMode == OutputLocationMode.DefaultFolder)
            {
                EditorGUILayout.BeginHorizontal();
                customOutputPath = EditorGUILayout.TextField("输出目录", customOutputPath);
                if (GUILayout.Button(Get(BTN_BROWSE), GUILayout.Width(60)))
                {
                    var picked = EditorUtility.OpenFolderPanel("选择输出目录", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(picked))
                        customOutputPath = NormalizeOutputFolder(picked);
                }
                EditorGUILayout.EndHorizontal();
            }

            overwriteExisting = EditorGUILayout.Toggle("覆盖已存在的 .anim", overwriteExisting);
            timeoutSeconds = EditorGUILayout.IntField(Get(TIMEOUT_SECONDS), timeoutSeconds);
            maxParallel = EditorGUILayout.IntSlider("PMX2FBX 并行数", maxParallel, 1, 16);
            EditorGUILayout.HelpBox(
                "并行仅作用于 PMX2FBX 进程；共享同一 PMX 时会自动复制到临时目录，避免 FBX 互相覆盖。",
                MessageType.None);

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "高级 / 与 VMD To Anim 同步", true);
            if (showAdvanced)
            {
                var settings = ConversionSettings.Load();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("PMX2FBX 工具");
                EditorGUILayout.SelectableLabel(settings.PMX2FBXPath, EditorStyles.textField, GUILayout.Height(18));
                if (GUILayout.Button("…", GUILayout.Width(24)))
                {
                    var path = EditorUtility.OpenFilePanel("选择 PMX2FBX", "", "exe");
                    if (!string.IsNullOrEmpty(path))
                    {
                        settings.PMX2FBXPath = path;
                        settings.Save();
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (string.IsNullOrEmpty(settings.PMX2FBXPath) || !File.Exists(settings.PMX2FBXPath))
                    EditorGUILayout.HelpBox("请在 VMD To Anim 窗口或此处指定 PMX2FBX 路径。", MessageType.Warning);

                scanSubfolders = EditorGUILayout.Toggle("添加文件夹时包含子目录", scanSubfolders);

                if (GUILayout.Button("保存到 VMD To Anim 配置"))
                {
                    settings.OutputPath = outputLocationMode == OutputLocationMode.DefaultFolder
                        ? customOutputPath
                        : settings.OutputPath;
                    settings.OverwriteExisting = overwriteExisting;
                    settings.TimeoutSeconds = timeoutSeconds;
                    settings.Save();
                    EditorUtility.DisplayDialog("提示", "已写入 VMD2AnimSettings", "确定");
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void DrawActionSection()
        {
            EditorGUI.BeginDisabledGroup(isRunning || batchItems.Count == 0);

            if (GUILayout.Button("开始批量转换", GUILayout.Height(32)))
                StartBatchConversion();

            EditorGUI.EndDisabledGroup();

            if (isRunning && GUILayout.Button(Get(BTN_CANCEL), GUILayout.Height(24)))
                cancellationTokenSource?.Cancel();
        }

        private void DrawProgressSection()
        {
            EditorGUILayout.Space(4);
            if (!string.IsNullOrEmpty(batchPhase))
                EditorGUILayout.LabelField(batchPhase, EditorStyles.miniBoldLabel);
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20f), overallProgress, progressMessage);
            EditorGUILayout.LabelField(
                $"进度 {completedCount}/{batchItems.Count}（成功 {successCount}，失败 {failedCount}）",
                EditorStyles.miniLabel);
        }

        private void DrawSummarySection()
        {
            if (batchItems.Count == 0 || isRunning)
                return;

            var done = batchItems.All(i =>
                i.State != BatchItemState.Pending && i.State != BatchItemState.Running);
            if (!done)
                return;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"批量完成：成功 {successCount}，失败 {failedCount}，跳过/取消 {batchItems.Count(i => i.State == BatchItemState.Skipped || i.State == BatchItemState.Cancelled)}",
                failedCount > 0 ? MessageType.Warning : MessageType.Info);
        }

        private async void StartBatchConversion()
        {
            var pending = batchItems.Where(i => i.State == BatchItemState.Pending).ToList();
            if (pending.Count == 0)
            {
                foreach (var item in batchItems)
                {
                    item.State = BatchItemState.Pending;
                    item.Message = "";
                    item.FbxAbsPath = null;
                }
                pending = batchItems.ToList();
            }

            if (!ValidateBatchItems(pending, out string validationError))
            {
                EditorUtility.DisplayDialog(Get(DIALOG_ERROR), validationError, Get(DIALOG_CONFIRM));
                return;
            }

            var settings = ConversionSettings.Load();
            if (string.IsNullOrEmpty(settings.PMX2FBXPath) || !File.Exists(settings.PMX2FBXPath))
            {
                EditorUtility.DisplayDialog(Get(DIALOG_ERROR), "请先配置有效的 PMX2FBX 工具路径。", Get(DIALOG_CONFIRM));
                return;
            }

            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            isRunning = true;
            completedCount = 0;
            successCount = 0;
            failedCount = 0;
            overallProgress = 0f;
            batchPhase = "";

            string batchTempAbs = Path.GetFullPath(BatchTempRoot);
            AssetUtils.EnsureDirectoryExists(batchTempAbs);

            batchFinishedJobs = 0;
            var pmxUsageCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var item in pending)
                {
                    item.VmdPath = ToAbsolutePath(item.VmdPath);
                    item.ResolvedPmxPath = ToAbsolutePath(ResolvePmxPath(item.VmdPath));
                    item.AnimOutputDir = GetOutputPathForVmd(item.VmdPath);
                    AssetUtils.EnsureDirectoryExists(item.AnimOutputDir);

                    string animFullPath = Path.Combine(
                        item.AnimOutputDir,
                        Path.GetFileNameWithoutExtension(item.VmdPath) + ".anim");

                    if (File.Exists(animFullPath) && !overwriteExisting)
                    {
                        item.State = BatchItemState.Skipped;
                        item.Message = "已存在";
                        completedCount++;
                        continue;
                    }

                    if (!pmxUsageCount.ContainsKey(item.ResolvedPmxPath))
                        pmxUsageCount[item.ResolvedPmxPath] = 0;
                    pmxUsageCount[item.ResolvedPmxPath]++;
                }

                var workItems = pending
                    .Where(i => i.State != BatchItemState.Skipped)
                    .ToList();
                int workTotal = workItems.Count;

                foreach (var item in workItems)
                {
                    if (!File.Exists(item.ResolvedPmxPath))
                    {
                        item.State = BatchItemState.Failed;
                        item.Message = "PMX 无效";
                        failedCount++;
                    }
                }

                workItems = workItems.Where(i => i.State != BatchItemState.Failed).ToList();
                workTotal = workItems.Count;

                batchPhase = $"批量转换（PMX2FBX ×{maxParallel}，完成即导入 .anim）";
                var pmxSemaphore = new SemaphoreSlim(maxParallel, maxParallel);
                var importLock = new SemaphoreSlim(1, 1);
                string toolPath = settings.PMX2FBXPath;

                var pipelineTasks = workItems.Select(async item =>
                {
                    await pmxSemaphore.WaitAsync(token);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        item.State = BatchItemState.Running;
                        item.Message = "PMX2FBX…";
                        RepaintOnMainThread();

                        string pmxForJob = item.ResolvedPmxPath;
                        bool needsIsolation = maxParallel > 1 || pmxUsageCount[item.ResolvedPmxPath] > 1;
                        if (needsIsolation)
                        {
                            pmxForJob = VMDConverter.PrepareIsolatedPmxWorkspace(
                                item.ResolvedPmxPath, item.VmdPath, batchTempAbs);
                            item.JobWorkspaceDir = Path.GetDirectoryName(pmxForJob);
                        }

                        string workDir = Path.GetDirectoryName(pmxForJob);
                        item.FbxAbsPath = await VMDConverter.RunPMX2FBXAsync(
                            toolPath,
                            pmxForJob,
                            ToAbsolutePath(item.VmdPath),
                            workDir,
                            quickMode,
                            timeoutSeconds * 1000,
                            token);

                        item.Message = "导入 .anim…";
                        RepaintOnMainThread();

                        await importLock.WaitAsync(token);
                        try
                        {
                            await RunOnMainThreadAsync(
                                () => ImportBatchItemAnim(item, workTotal),
                                token);
                        }
                        finally
                        {
                            importLock.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        item.State = BatchItemState.Cancelled;
                        item.Message = "已取消";
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (item.State != BatchItemState.Success)
                        {
                            item.State = BatchItemState.Failed;
                            item.Message = TruncateMessage(ex.Message);
                            Interlocked.Increment(ref failedCount);
                        }
                        Debug.LogError($"[VMD Batch] {item.VmdPath}: {ex}");
                        int done = Interlocked.Increment(ref batchFinishedJobs);
                        UpdateBatchProgress(done, workTotal, item);
                    }
                    finally
                    {
                        pmxSemaphore.Release();
                    }
                });

                await Task.WhenAll(pipelineTasks);

                completedCount = batchItems.Count(i =>
                    i.State != BatchItemState.Pending && i.State != BatchItemState.Running);
                CleanupBatchTempWorkspaces(batchTempAbs, workItems);

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    Get(DIALOG_SUCCESS),
                    $"批量转换结束。\n成功：{successCount}\n失败：{failedCount}",
                    Get(DIALOG_CONFIRM));
            }
            catch (OperationCanceledException)
            {
                foreach (var item in batchItems.Where(x => x.State == BatchItemState.Running))
                {
                    item.State = BatchItemState.Cancelled;
                    item.Message = "已取消";
                }
                EditorUtility.DisplayDialog(Get(DIALOG_CANCEL), Get("msg_conversion_cancelled"), Get(DIALOG_CONFIRM));
            }
            finally
            {
                isRunning = false;
                batchPhase = "";
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
                overallProgress = 1f;
                Repaint();
            }
        }

        private void ImportBatchItemAnim(BatchItem item, int workTotal)
        {
            if (string.IsNullOrEmpty(item.FbxAbsPath) || !File.Exists(item.FbxAbsPath))
            {
                item.State = BatchItemState.Failed;
                item.Message = "无 FBX";
                Interlocked.Increment(ref failedCount);
                int doneFail = Interlocked.Increment(ref batchFinishedJobs);
                UpdateBatchProgress(doneFail, workTotal, item);
                return;
            }

            string animFullPath = Path.Combine(
                item.AnimOutputDir,
                Path.GetFileNameWithoutExtension(item.VmdPath) + ".anim");

            bool ok = VMDConverter.ImportFbxAndSaveAnim(
                item.FbxAbsPath,
                animFullPath,
                overwriteExisting,
                quickLoadAnim: true,
                null,
                scheduleFbxCleanup: true);

            if (ok && File.Exists(animFullPath))
            {
                item.State = BatchItemState.Success;
                item.Message = "完成";
                Interlocked.Increment(ref successCount);
            }
            else
            {
                item.State = BatchItemState.Failed;
                item.Message = "导入失败";
                Interlocked.Increment(ref failedCount);
            }

            int done = Interlocked.Increment(ref batchFinishedJobs);
            UpdateBatchProgress(done, workTotal, item);
            Repaint();
        }

        private void UpdateBatchProgress(int finished, int total, BatchItem item)
        {
            overallProgress = total > 0 ? (float)finished / total : 0f;
            progressMessage = $"{finished}/{total} — {Path.GetFileName(item.VmdPath)} — {item.Message}";
            RepaintOnMainThread();
        }

        private static Task RunOnMainThreadAsync(Action action, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (token.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            CancellationTokenRegistration reg = default;
            EditorApplication.delayCall += () =>
            {
                if (token.IsCancellationRequested)
                {
                    tcs.TrySetCanceled();
                    return;
                }

                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };

            if (token.CanBeCanceled)
            {
                reg = token.Register(() => tcs.TrySetCanceled());
                tcs.Task.ContinueWith(_ => reg.Dispose(), TaskScheduler.Default);
            }

            return tcs.Task;
        }

        private static void RepaintOnMainThread()
        {
            EditorApplication.delayCall += () =>
            {
                var w = GetWindow<VmdBatchAnimConverterWindow>(false, null, false);
                w?.Repaint();
            };
        }

        private static void CleanupBatchTempWorkspaces(string batchTempAbs, List<BatchItem> items)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.JobWorkspaceDir))
                    continue;
                if (!item.JobWorkspaceDir.StartsWith(batchTempAbs, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    if (Directory.Exists(item.JobWorkspaceDir))
                        Directory.Delete(item.JobWorkspaceDir, true);
                    string meta = item.JobWorkspaceDir + ".meta";
                    if (File.Exists(meta))
                        File.Delete(meta);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[VMD Batch] 清理临时目录失败: {item.JobWorkspaceDir} — {ex.Message}");
                }
            }
            AssetDatabase.Refresh();
        }

        private bool ValidateBatchItems(List<BatchItem> items, out string error)
        {
            var invalidVmd = items.Where(i => !File.Exists(ToAbsolutePath(i.VmdPath))).ToList();
            if (invalidVmd.Count > 0)
            {
                error = $"有 {invalidVmd.Count} 个 VMD 路径无效。";
                return false;
            }

            foreach (var item in items)
            {
                string abs = ToAbsolutePath(item.VmdPath);
                if (!IsUnderAssets(abs))
                {
                    error = $"VMD 须在项目 Assets 内：\n{Path.GetFileName(abs)}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool IsUnderAssets(string absolutePath)
        {
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            absolutePath = Path.GetFullPath(absolutePath).Replace('\\', '/');
            return absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            path = path.Replace('\\', '/');
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
                return Path.GetFullPath(Path.Combine(projectRoot, path));
            }

            return Path.GetFullPath(path);
        }

        private string ResolvePmxPath(string vmdPath)
        {
            if (!useReferencePmx)
                return GetConfiguredPmxPath();

            if (!string.IsNullOrEmpty(referencePmxPath) && File.Exists(referencePmxPath))
                return referencePmxPath;

            if (autoFindPmxBesideVmd)
            {
                string dir = Path.GetDirectoryName(vmdPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    foreach (var ext in new[] { ".pmx", ".pmd" })
                    {
                        var files = Directory.GetFiles(dir, "*" + ext);
                        if (files.Length > 0)
                            return files[0];
                    }
                }
            }

            return GetConfiguredPmxPath();
        }

        private static string GetConfiguredPmxPath()
        {
            var conversionSettings = ConversionSettings.Load();
            return conversionSettings.UseDefaultPMX
                ? conversionSettings.DefaultPmxPath
                : conversionSettings.PmxFilePath;
        }

        private string GetOutputPathForVmd(string vmdPath)
        {
            switch (outputLocationMode)
            {
                case OutputLocationMode.SameAsVmd:
                    var dir = Path.GetDirectoryName(vmdPath);
                    return string.IsNullOrEmpty(dir)
                        ? DefaultOutputPath
                        : NormalizeOutputFolder(dir);
                case OutputLocationMode.DefaultFolder:
                default:
                    return NormalizeOutputFolder(
                        string.IsNullOrEmpty(customOutputPath) ? DefaultOutputPath : customOutputPath);
            }
        }

        private static string NormalizeOutputFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (!path.EndsWith("/"))
                path += "/";
            return path;
        }

        private void AddVmdsFromFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("选择包含 VMD 的文件夹", Application.dataPath, "");
            if (string.IsNullOrEmpty(folder))
                return;

            var option = scanSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(folder, "*.vmd", option);
            int added = 0;
            int skippedOutside = 0;
            foreach (var file in files)
            {
                string normalized = file.Replace('\\', '/');
                if (!IsUnderAssets(normalized))
                {
                    skippedOutside++;
                    continue;
                }

                if (batchItems.Any(i => string.Equals(i.VmdPath, normalized, StringComparison.OrdinalIgnoreCase)))
                    continue;

                batchItems.Add(new BatchItem { VmdPath = ToProjectRelativePath(normalized) });
                added++;
            }

            if (skippedOutside > 0)
                Debug.LogWarning($"[VMD Batch] 跳过 {skippedOutside} 个不在 Assets 内的 VMD");

            Debug.Log($"[VMD Batch] 从文件夹添加 {added} 个 VMD（共扫描 {files.Length} 个）");
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            absolutePath = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return "Assets" + absolutePath.Substring(dataPath.Length);

            return absolutePath;
        }

        private void SyncPathListToBatchItems(List<string> pathList)
        {
            var existing = batchItems.ToDictionary(
                i => i.VmdPath,
                i => i,
                StringComparer.OrdinalIgnoreCase);

            batchItems.Clear();
            foreach (var path in pathList)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                if (existing.TryGetValue(path, out var item))
                    batchItems.Add(item);
                else
                    batchItems.Add(new BatchItem { VmdPath = path });
            }
        }

        private void UpdateOverallProgress(int total)
        {
            overallProgress = total > 0 ? (float)completedCount / total : 0f;
        }

        private static string TruncateMessage(string msg, int max = 40)
        {
            if (string.IsNullOrEmpty(msg))
                return "";
            return msg.Length <= max ? msg : msg.Substring(0, max) + "…";
        }

        private static string GetStateLabel(BatchItemState state)
        {
            switch (state)
            {
                case BatchItemState.Pending: return "等待";
                case BatchItemState.Running: return "进行";
                case BatchItemState.Success: return "成功";
                case BatchItemState.Failed: return "失败";
                case BatchItemState.Skipped: return "跳过";
                case BatchItemState.Cancelled: return "取消";
                default: return "?";
            }
        }

        private static Color GetStateColor(BatchItemState state)
        {
            switch (state)
            {
                case BatchItemState.Success: return new Color(0.4f, 0.85f, 0.4f);
                case BatchItemState.Failed: return new Color(1f, 0.45f, 0.45f);
                case BatchItemState.Running: return new Color(0.5f, 0.75f, 1f);
                case BatchItemState.Skipped: return new Color(0.85f, 0.85f, 0.5f);
                case BatchItemState.Cancelled: return Color.gray;
                default: return Color.white;
            }
        }

        private void LoadEditorPrefs()
        {
            quickMode = EditorPrefs.GetBool(EditorPrefsPrefix + "quickMode", false);
            useReferencePmx = EditorPrefs.GetBool(EditorPrefsPrefix + "useReferencePmx", false);
            autoFindPmxBesideVmd = EditorPrefs.GetBool(EditorPrefsPrefix + "autoFindPmx", true);
            referencePmxPath = EditorPrefs.GetString(EditorPrefsPrefix + "referencePmx", "");
            outputLocationMode = (OutputLocationMode)EditorPrefs.GetInt(
                EditorPrefsPrefix + "outputMode", (int)OutputLocationMode.SameAsVmd);
            customOutputPath = EditorPrefs.GetString(EditorPrefsPrefix + "customOutput", DefaultOutputPath);
            overwriteExisting = EditorPrefs.GetBool(EditorPrefsPrefix + "overwrite", true);
            timeoutSeconds = EditorPrefs.GetInt(EditorPrefsPrefix + "timeout", 300);
            scanSubfolders = EditorPrefs.GetBool(EditorPrefsPrefix + "scanSub", true);
            maxParallel = EditorPrefs.GetInt(
                EditorPrefsPrefix + "maxParallel",
                Math.Min(4, Math.Max(1, Environment.ProcessorCount)));
        }

        private void SaveEditorPrefs()
        {
            EditorPrefs.SetBool(EditorPrefsPrefix + "quickMode", quickMode);
            EditorPrefs.SetBool(EditorPrefsPrefix + "useReferencePmx", useReferencePmx);
            EditorPrefs.SetBool(EditorPrefsPrefix + "autoFindPmx", autoFindPmxBesideVmd);
            EditorPrefs.SetString(EditorPrefsPrefix + "referencePmx", referencePmxPath ?? "");
            EditorPrefs.SetInt(EditorPrefsPrefix + "outputMode", (int)outputLocationMode);
            EditorPrefs.SetString(EditorPrefsPrefix + "customOutput", customOutputPath ?? DefaultOutputPath);
            EditorPrefs.SetBool(EditorPrefsPrefix + "overwrite", overwriteExisting);
            EditorPrefs.SetInt(EditorPrefsPrefix + "timeout", timeoutSeconds);
            EditorPrefs.SetBool(EditorPrefsPrefix + "scanSub", scanSubfolders);
            EditorPrefs.SetInt(EditorPrefsPrefix + "maxParallel", maxParallel);
        }
    }
}
