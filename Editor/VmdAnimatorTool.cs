using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;
using VMDPaser;
using UnityMMDConverter.Utils;
using UnityMMDConverter.Logic; // 引用逻辑层
using UnityMMDConverter.CustomGUI;   // 引用新的GUI层
// CancellationTokenSource
using System.Threading;
// Task
using System.Threading.Tasks;
using UnityEngine.Playables;
using static UnityMMDConverter.LocalizationManager;
using static UnityMMDConverter.L10nKeys;


namespace UnityMMDConverter
{
    public class VmdMorphAnimatorTool : EditorWindow
    {
        private const string DefaultOutputPath = "Assets/UnityMMDConverter/Output/";
        private const string TempBuildFolder = "Assets/Temp/TempPureBuild/";
        private const float DefaultFrameRate = 30f;

        // 核心组件引用
        private AnimationClip sourceClip;
        private GameObject targetModel;
        private SkinnedMeshRenderer bodyRenderer;

        // VMD文件相关（按功能拆分）
        // 1. 动画VMD
        private string animVmdFilePath;
        private VMD parsedAnimVmd;
        private bool animVmdParsed = false;

        private bool isConverting = false;
        private float progress = 0f;
        private string progressMessage = "";
        // 超时时间
        private int timeoutSeconds = 300;

        // 2. 镜头VMD（支持多个）
        private List<string> cameraVmdFilePaths = new List<string>();
        private List<VMD> parsedCameraVmds = new List<VMD>();
        private List<VMDCameraFrame> vmdCameraFrames = new List<VMDCameraFrame>();
        private bool cameraVmdParsed = false;

        private float cameraScale = 1.0f; // 相机缩放比例

        // 3. 表情VMD（支持多个）
        private List<string> morphVmdFilePaths = new List<string>();
        private List<VMD> parsedMorphVmds = new List<VMD>();
        private List<VMDMorphFrame> vmdMorphFrames = new List<VMDMorphFrame>();
        private bool morphVmdParsed = false;

        // 配置选项
        private string newClipName = "NewMorphAnimation";
        private string controllerName = "NewAnimatorController";
        private OutputLocationMode outputLocationMode = OutputLocationMode.SameAsVmd;

        // 形态键管理
        private List<string> availableMorphs = new List<string>();
        private Dictionary<string, bool> selectedMorphs = new Dictionary<string, bool>();
        private Dictionary<string, string> morphMapping = new Dictionary<string, string>(); // 形态键映射表


        // 直接映射模式配置
        private bool directMappingMode = true;
        private string defaultSkinnedMeshPath = "Body";
        private string defaultSkinnedMeshName = "Body";
        private bool showSkinnedMeshOptions = false;

        // 相机动画配置
        private bool showCameraAdvancedOptions = false;
        private bool enableCameraAnimation = false;

        // 相机路径配置
        private string cameraRootPath = "Camera_root";  // 位移接受组件
        private string cameraComponentPath = "Camera_root/Camera_root_1/Camera";// 主相机组件路径
        private string cameraDistancePath = "Camera_root/Camera_root_1"; // Distance变换路径（接收距离动画）

        // 音频配置
        private string audioFilePath = "";

        // 打包配置
        private bool addMorphCurves = true; // 添加表情曲线
        private bool addCameraCurves = false; // 添加镜头曲线

        private string bundleBaseName = "character_animation";
        private BuildAssetBundleOptions bundleOptions = BuildAssetBundleOptions.ChunkBasedCompression;
        private string bundleOutputPath = "";

        // 界面状态
        private bool showSettingsPanel;
        private Vector2 mainScrollPos;
        private Vector2 allMorphsScrollPos;
        private bool showMappingOptions = false;

        // 用于Timeline预览
        private bool showTimelinePreview = false;
        private GameObject characterModel;


        // 4. 在编辑器窗口中添加取消支持
        private CancellationTokenSource cancellationTokenSource;

        // 新增：配置管理器
        private ToolConfigManager configManager;

        // 动画提取模式
        enum AnimExtractionMode { FromExistingClip, FromVmdFile }
        AnimExtractionMode animExtractionMode = AnimExtractionMode.FromVmdFile;

        // 是否使用快速配置
        private bool useQuickConfig = false;

        // PMX辅助文件
        private bool showPmxOptions = false;
        private string pmxFilePath = "";


        // 优先级放到最上面
        [MenuItem("MMD for Unity/Unity MMD Converter", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<VmdMorphAnimatorTool>("Unity MMD Converter");
            window.minSize = new Vector2(600, 800);
        }

        private void OnGUI()
        {
            mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);
            EditorGUILayout.Space();

            DrawSettingsPanel();
            // 1. 动画部分
            DrawAnimationSection();
            DrawSeparator();

            // 2. 镜头部分
            DrawCameraSection();
            DrawSeparator();

            // 3. 表情部分
            DrawMorphSection();
            DrawSeparator();

            DrawModelSettings();
            DrawMorphMappingSettings();
            DrawSeparator();

            DrawOutputSettings();
            DrawNamingSettings();
            DrawActionButtons();
            DrawAudioSettings();
            // 使用timeline预览
            DrawTimelinePreview();
            DrawSeparator();


            DrawAssetBundleSettings();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAnimationSection()
        {
            EditorGUILayout.LabelField(Get(SECTION_ANIMATION), EditorStyles.boldLabel);

            animExtractionMode = (AnimExtractionMode)EditorGUILayout.EnumPopup(Get(ANIM_SOURCE), animExtractionMode);

            if (animExtractionMode == AnimExtractionMode.FromExistingClip)
            {
                EditorGUILayout.BeginHorizontal();
                sourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                    Get(EXISTING_CLIP), sourceClip, typeof(AnimationClip), false);

                if (GUILayout.Button(Get(BTN_CLEAR), GUILayout.Width(60)))
                {
                    sourceClip = null;
                    ResetAnimVmdState();
                }
                EditorGUILayout.EndHorizontal();
                // 如果选择了已有动画剪辑，也要设置animVmdFilePath
                if (sourceClip != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(sourceClip);
                    // 这里获取sourceclip的路径作为vmd路径的参考（仅用于显示）
                    animVmdFilePath = Path.ChangeExtension(assetPath, "vmd");
                }
            }

            else
            {

                // 使用新工具绘制单个文件选择
                animVmdFilePath = MMDCustomGUI.DrawSingleFileSelector(animVmdFilePath, "vmd", Get(ANIM_VMD_FILE));

                // 辅助选项
                useQuickConfig = EditorGUILayout.Toggle(Get(QUICK_CONFIG), useQuickConfig);
                showPmxOptions = EditorGUILayout.Foldout(showPmxOptions, Get(PMX_ASSIST));
                if (showPmxOptions)
                {
                    pmxFilePath = MMDCustomGUI.DrawSingleFileSelector(pmxFilePath, "pmx", "PMX Reference");
                }

                // 设置超时时间
                timeoutSeconds = EditorGUILayout.IntField(Get(TIMEOUT_SECONDS), timeoutSeconds);
                // 生成按钮逻辑 (保持原有的异步逻辑，或封装到 Logic 层)
                if (!string.IsNullOrEmpty(animVmdFilePath) && File.Exists(animVmdFilePath))
                {
                    if (GUILayout.Button(Get(BTN_GENERATE_ANIM)))
                    {
                        RunAnimConversionTask(); // 将具体的 Task 启动逻辑封装到方法里
                    }
                }

                if (isConverting)
                {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, progressMessage);
                    if (GUILayout.Button(Get(BTN_CANCEL))) cancellationTokenSource?.Cancel();
                }
            }
        }
        private void DrawCameraSection()
        {
            EditorGUILayout.LabelField(Get(SECTION_CAMERA), EditorStyles.boldLabel);
            enableCameraAnimation = EditorGUILayout.Toggle(Get(ENABLE_CAMERA), enableCameraAnimation);

            if (enableCameraAnimation)
            {
                // 1. 文件选择列表 (修复多语言)
                MMDCustomGUI.DrawFileSelectorList(
                    cameraVmdFilePaths,
                    "vmd",
                    Get(CAMERA_VMD_FILE) // 使用多语言 Key
                );

                EditorGUILayout.Space();

                // 2. 解析按钮
                // 只有当有文件且未完全解析，或者你想允许重新解析时显示
                if (cameraVmdFilePaths.Count > 0)
                {
                    if (GUILayout.Button(Get(BTN_PARSE_CAMERA)))
                    {
                        // 调用 Logic 层
                        cameraVmdParsed = UnityMMDConverter.Logic.MMDConverterLogic.ParseCameraVmds(
                            cameraVmdFilePaths,
                            out parsedCameraVmds, // 注意：你需要确保类里定义了这个变量，或者直接用 _ 丢弃
                            out vmdCameraFrames
                        );

                        if (cameraVmdParsed)
                            Debug.Log($"Parsed {vmdCameraFrames.Count} camera frames.");
                    }
                }

                // 3. 显示解析结果状态
                if (cameraVmdParsed)
                {
                    EditorGUILayout.HelpBox(
                        string.Format(Get(CAMERA_PARSED_INFO), vmdCameraFrames.Count, cameraVmdFilePaths.Count),
                        MessageType.Info
                    );
                }

                // 4. 恢复：清空所有解析数据按钮
                // 原先逻辑：ResetCameraVmdState();
                if (GUILayout.Button(Get(BTN_CLEAR_ALL)))
                {
                    ResetCameraVmdState(); // 这个函数是你原代码里有的，用来清空 vmdCameraFrames 等
                    Repaint();
                }

                // 5. 其他参数 (保持原样)
                cameraScale = EditorGUILayout.Slider(Get(CAMERA_SCALE), cameraScale, 0.1f, 2.0f);

                // 高级选项折叠页 (保留你原有的逻辑)
                showCameraAdvancedOptions = EditorGUILayout.Foldout(showCameraAdvancedOptions, Get(CAMERA_PATH_CONFIG));
                if (showCameraAdvancedOptions)
                {
                    cameraRootPath = EditorGUILayout.TextField(Get(CAMERA_ROOT_PATH), cameraRootPath);
                    cameraDistancePath = EditorGUILayout.TextField(Get(CAMERA_DISTANCE_PATH), cameraDistancePath);
                    cameraComponentPath = EditorGUILayout.TextField(Get(CAMERA_COMPONENT_PATH), cameraComponentPath);
                }
            }
        }
        private void DrawSettingsPanel()
        {

            EditorGUILayout.LabelField("0. " + Get("settings_panel"), EditorStyles.boldLabel);
            showSettingsPanel = EditorGUILayout.Foldout(showSettingsPanel, Get("settings_panel"), true);

            if (showSettingsPanel)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Get(LANGUAGE_LABEL), GUILayout.Width(120));

                var newLang = (Language)EditorGUILayout.EnumPopup(CurrentLanguage, GUILayout.Width(100));
                if (newLang != CurrentLanguage)
                {
                    CurrentLanguage = newLang;
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();

                // --- 新增：输出位置配置 ---
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Get("output_location"), GUILayout.Width(EditorGUIUtility.labelWidth));
                outputLocationMode = (OutputLocationMode)EditorGUILayout.EnumPopup(outputLocationMode);
                EditorGUILayout.EndHorizontal();

                // 提示信息
                if (outputLocationMode == OutputLocationMode.DefaultFolder)
                {
                    EditorGUILayout.HelpBox(string.Format(Get("output_location_default"), DefaultOutputPath), MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox(Get("output_location_same_folder"), MessageType.None);
                }
                EditorGUILayout.Space();
                // -----------------------

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }
        private void DrawMorphSection()
        {


            EditorGUILayout.LabelField(Get(SECTION_MORPH), EditorStyles.boldLabel);

            // 1. 文件选择列表 (修复多语言)
            MMDCustomGUI.DrawFileSelectorList(
                morphVmdFilePaths,
                "vmd",
                Get(MORPH_VMD_FILE) // 使用多语言 Key
            );

            EditorGUILayout.Space();

            // 2. 解析按钮
            if (morphVmdFilePaths.Count > 0)
            {
                if (GUILayout.Button(Get(BTN_PARSE_MORPH)))
                {
                    // 调用 Logic 层
                    morphVmdParsed = UnityMMDConverter.Logic.MMDConverterLogic.ParseMorphVmds(
                        morphVmdFilePaths,
                        out parsedMorphVmds,
                        out vmdMorphFrames
                    );

                    // 自动初始化映射
                    if (morphVmdParsed && directMappingMode)
                    {
                        MorphUtils.InitializeDirectMorphMapping(
                            vmdMorphFrames,
                            directMappingMode,
                            morphMapping,
                            availableMorphs,
                            selectedMorphs
                        );
                    }
                }
            }

            // 3. 显示解析结果状态
            if (morphVmdParsed)
            {
                var uniqueMorphs = vmdMorphFrames.Select(f => f.MorphName).Distinct().Count();
                EditorGUILayout.HelpBox(
                    string.Format(Get("morph_parsed_info"), vmdMorphFrames.Count, uniqueMorphs, morphVmdFilePaths.Count),
                    MessageType.Info
                );
            }

            // 4. 恢复：清空所有解析数据按钮
            if (GUILayout.Button(Get(BTN_CLEAR_ALL)))
            {
                ResetMorphVmdState(); // 这个函数是你原代码里有的
                Repaint();
            }
        }


        #region UI方法

        private void DrawSeparator()
        {
            EditorGUILayout.Space();
            Rect rect = GUILayoutUtility.GetRect(1f, 1.5f);
            rect.width = EditorGUIUtility.currentViewWidth - 20f;
            rect.x += 10f;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space();
        }

        private void DrawNamingSettings()
        {
            EditorGUILayout.LabelField(Get(SECTION_NAMING), EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            var oldBaseName = bundleBaseName;
            bundleBaseName = EditorGUILayout.TextField(Get(BASE_NAME), bundleBaseName);

            if (!string.IsNullOrEmpty(bundleBaseName) && oldBaseName != bundleBaseName)
            {
                newClipName = bundleBaseName;
                controllerName = bundleBaseName;
            }

            if (GUILayout.Button(Get(BTN_AUTO_NAME), GUILayout.Width(100)))
            {
                AutoNameResources();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawModelSettings()
        {
            EditorGUILayout.LabelField(Get(SECTION_MODEL), EditorStyles.miniBoldLabel);
            directMappingMode = EditorGUILayout.Toggle(Get(DIRECT_MAPPING), directMappingMode);

            if (directMappingMode)
            {
                EditorGUILayout.HelpBox(Get(HELP_DIRECT_MAPPING), MessageType.Info);

                showSkinnedMeshOptions = EditorGUILayout.Foldout(
                    showSkinnedMeshOptions,
                    Get(SKINNED_MESH_PATH_SETTINGS)
                );
                if (showSkinnedMeshOptions)
                {
                    EditorGUILayout.BeginVertical();
                    defaultSkinnedMeshPath = EditorGUILayout.TextField(
                        Get(SKINNED_MESH_PATH),
                        defaultSkinnedMeshPath
                    );
                    defaultSkinnedMeshName = EditorGUILayout.TextField(
                        Get(COMPONENT_NAME),
                        defaultSkinnedMeshName
                    );
                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                EditorGUILayout.HelpBox(Get(HELP_NON_DIRECT_MAPPING), MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            if (!directMappingMode)
            {
                targetModel = (GameObject)EditorGUILayout.ObjectField(
                    Get(TARGET_MODEL),
                    targetModel,
                    typeof(GameObject),
                    true
                );

                if (targetModel != null)
                {
                    bodyRenderer = ModelUtils.UpdateModelComponents(
                        targetModel,
                        bodyRenderer,
                        availableMorphs,
                        selectedMorphs,
                        directMappingMode,
                        vmdMorphFrames,
                        morphMapping,
                        IsMorphVmdDataReady
                    );
                }
            }

            if (GUILayout.Button(Get(BTN_RESET), GUILayout.Width(60)))
            {
                targetModel = null;
                ResetModelState();
                if (directMappingMode && IsMorphVmdDataReady())
                {
                    MorphUtils.InitializeDirectMorphMapping(
                        vmdMorphFrames,
                        directMappingMode,
                        morphMapping,
                        availableMorphs,
                        selectedMorphs
                    );
                }
            }

            EditorGUILayout.EndHorizontal();
            ShowMorphStatistics();
            EditorGUILayout.Space();
        }

        private void DrawOutputSettings()
        {
            EditorGUILayout.LabelField(Get(SECTION_OUTPUT), EditorStyles.miniBoldLabel);


            EditorGUILayout.LabelField(Get(ANIMATION_CURVE_OPTIONS), EditorStyles.miniBoldLabel);

            addMorphCurves = EditorGUILayout.Toggle(Get(ADD_MORPH_CURVES), addMorphCurves);
            addCameraCurves = EditorGUILayout.Toggle(Get(ADD_CAMERA_CURVES), addCameraCurves);

            EditorGUILayout.BeginHorizontal();
            if (addMorphCurves && addCameraCurves)
            {
                EditorGUILayout.HelpBox(Get(HELP_MERGE_MORPH_CAMERA), MessageType.Info);
            }
            else if (addMorphCurves)
            {
                EditorGUILayout.HelpBox(Get(HELP_MERGE_MORPH), MessageType.Info);
            }
            else if (addCameraCurves)
            {
                EditorGUILayout.HelpBox(Get(HELP_MERGE_CAMERA), MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(Get(HELP_SELECT_CURVE_TYPE), MessageType.Warning);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private void DrawMorphMappingSettings()
        {
            showMappingOptions = EditorGUILayout.Foldout(
                showMappingOptions,
                Get(MORPH_MAPPING_SETTINGS)
            );

            if (showMappingOptions && availableMorphs.Count > 0 && IsMorphVmdDataReady())
            {
                allMorphsScrollPos = EditorGUILayout.BeginScrollView(allMorphsScrollPos, GUILayout.Height(300));
                EditorGUILayout.LabelField(Get(MORPH_MAPPING_INSTRUCTION1), EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(Get(MORPH_MAPPING_INSTRUCTION2), EditorStyles.miniLabel);

                // 批量操作按钮
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(Get(BTN_SELECT_ALL)))
                    MorphUtils.SelectAllMorphs(availableMorphs, selectedMorphs, true);
                if (GUILayout.Button(Get(BTN_SELECT_FIRST_20)))
                    MorphUtils.SelectFirstNMorphs(availableMorphs, selectedMorphs, 20);
                if (GUILayout.Button(Get(BTN_DESELECT_ALL)))
                    MorphUtils.SelectAllMorphs(availableMorphs, selectedMorphs, false);
                EditorGUILayout.EndHorizontal();

                // 获取VMD中的所有唯一形态键名称
                var vmdMorphNames = vmdMorphFrames.Select(f => f.MorphName).Distinct().ToList();

                foreach (var vmdMorph in vmdMorphNames)
                {
                    EditorGUILayout.BeginHorizontal();

                    bool isSelected = selectedMorphs.TryGetValue(vmdMorph, out bool selectedValue)
                        ? selectedValue
                        : false;

                    EditorGUI.BeginChangeCheck();
                    isSelected = EditorGUILayout.ToggleLeft("", isSelected, GUILayout.Width(20));
                    if (EditorGUI.EndChangeCheck())
                    {
                        selectedMorphs[vmdMorph] = isSelected;
                    }

                    EditorGUILayout.LabelField(vmdMorph, GUILayout.Width(150));

                    string currentMapping = morphMapping.TryGetValue(vmdMorph, out string mapValue)
                        ? mapValue
                        : ModelUtils.GetMappedMorphName(
                            vmdMorph,
                            morphMapping,
                            availableMorphs
                        );

                    EditorGUI.BeginChangeCheck();
                    currentMapping = EditorGUILayout.TextField(currentMapping);
                    if (EditorGUI.EndChangeCheck())
                    {
                        morphMapping[vmdMorph] = currentMapping;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            else if (availableMorphs.Count == 0 && (targetModel != null || IsMorphVmdDataReady()))
            {
                EditorGUILayout.HelpBox(Get(HELP_NO_MORPH_DATA), MessageType.Info);
            }
            EditorGUILayout.Space();
        }

        private void DrawActionButtons()
        {
            UnityEngine.GUI.enabled = CanProcessAnimation();
            if (GUILayout.Button(Get(BTN_PROCESS), GUILayout.Height(30)))
            {
                ProcessAnimationAndController();
            }
            UnityEngine.GUI.enabled = true;

            EditorGUILayout.Space();
        }

        private void DrawAudioSettings()
        {
            EditorGUILayout.LabelField(Get(SECTION_AUDIO), EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Get(AUDIO_FILE), GUILayout.Width(EditorGUIUtility.labelWidth));

            EditorGUI.BeginChangeCheck();
            var projectRelativeAudioPath = AssetUtils.GetProjectRelativePath(audioFilePath);
            var audioObj = !string.IsNullOrEmpty(projectRelativeAudioPath)
                ? AssetDatabase.LoadAssetAtPath<Object>(projectRelativeAudioPath)
                : null;

            audioObj = EditorGUILayout.ObjectField(audioObj, typeof(AudioClip), false);

            if (EditorGUI.EndChangeCheck())
            {
                audioFilePath = audioObj != null ? AssetDatabase.GetAssetPath(audioObj) : "";
            }

            if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
            {
                EditorGUILayout.LabelField(Path.GetFileName(audioFilePath), EditorStyles.objectFieldThumb);
            }
            else
            {
                EditorGUILayout.LabelField(Get(AUDIO_NOT_SELECTED), EditorStyles.objectFieldThumb);
            }

            if (GUILayout.Button(Get(BTN_BROWSE), GUILayout.Width(80)))
            {
                BrowseAudioFile();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        private void DrawTimelinePreview()
        {
            showTimelinePreview = EditorGUILayout.Foldout(showTimelinePreview, Get(SECTION_TIMELINE), true);

            if (showTimelinePreview)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField(Get(CHARACTER_MODEL), EditorStyles.boldLabel);
                characterModel = EditorGUILayout.ObjectField(
                    Get(DRAG_MODEL_HERE),
                    characterModel,
                    typeof(GameObject),
                    true) as GameObject;

                EditorGUILayout.Space();

                UnityEngine.GUI.enabled = characterModel != null &&
                             !string.IsNullOrEmpty(bundleBaseName);

                if (GUILayout.Button(Get(BTN_CREATE_TIMELINE)))
                {
                    CreateTimelinePreview();
                }

                if (!UnityEngine.GUI.enabled)
                {
                    string disabledReason = "";
                    if (characterModel == null)
                        disabledReason = Get(HELP_SPECIFY_MODEL);
                    else if (string.IsNullOrEmpty(bundleBaseName))
                        disabledReason = Get(HELP_SET_BASE_NAME);

                    EditorGUILayout.HelpBox(disabledReason, MessageType.Info);
                }

                UnityEngine.GUI.enabled = true;
                EditorGUI.indentLevel--;
            }
        }

        private void CreateTimelinePreview()
        {
            // --------------- 核心修改1：强制覆写模型的Animator控制器 ---------------
            if (characterModel == null)
            {
                EditorUtility.DisplayDialog("错误", "请先在 Inspector 中选择角色模型！", "确定");
                return;
            }

            // 1. 获取/添加模型的Animator组件（确保模型具备动画播放能力）
            Animator characterAnimator = characterModel.GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = characterModel.AddComponent<Animator>();
                EditorUtility.DisplayDialog("提示", "已为模型自动添加Animator组件", "确定");
            }

            // 2. 加载当前工具生成的目标Controller（必须是包含Timeline所需动画的Controller）
            string outputDir = GetOutputPath();
            string targetControllerPath = $"{outputDir}{bundleBaseName}.controller";
            RuntimeAnimatorController targetController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(targetControllerPath);

            if (targetController == null)
            {
                EditorUtility.DisplayDialog("错误", $"未找到目标动画控制器：{targetControllerPath}\n请先生成动画资源！", "确定");
                return;
            }

            // 3. 强制覆写Animator的Controller（不管之前有没有，直接替换为目标Controller）
            if (characterAnimator.runtimeAnimatorController != targetController)
            {
                // 记录旧Controller名称，用于用户提示
                string oldControllerName = characterAnimator.runtimeAnimatorController?.name ?? "空控制器";
                // 强制赋值目标Controller
                characterAnimator.runtimeAnimatorController = targetController;
                // 标记模型为已修改，确保Controller变更被保存
                EditorUtility.SetDirty(characterModel);
                // 提示用户“控制器已被更新”（避免用户困惑）
                Debug.Log($"控制器已更新: 模型原有控制器：{oldControllerName}，已替换为：{targetController.name}（用于匹配当前Timeline动画）");
            }
            // ----------------------------------------------------------------------

            // 创建Timeline资产路径
            string outputPath = GetOutputPath();
            string timelinePath = $"{outputPath}{bundleBaseName}_preview.asset";
            string sceneDirectorName = $"{bundleBaseName}_director";

            // 确保输出目录存在
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // 创建或获取Timeline资产（显式指定类型参数）
            TimelineAsset timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (timelineAsset == null)
            {
                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timelineAsset, timelinePath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                // 清除现有轨道（避免旧轨道干扰）
                foreach (var track in timelineAsset.GetOutputTracks())
                {
                    timelineAsset.DeleteTrack(track);
                }
                EditorUtility.SetDirty(timelineAsset);
            }

            // 在当前场景中创建或获取PlayableDirector
            PlayableDirector director = GameObject.FindObjectOfType<PlayableDirector>();
            GameObject directorObj = null;

            if (director == null || director.gameObject.name != sceneDirectorName)
            {
                // 清除场景中同名旧导演对象（避免冲突）
                var oldDirectors = GameObject.FindObjectsOfType<PlayableDirector>();
                foreach (var oldDir in oldDirectors)
                {
                    if (oldDir.gameObject.name == sceneDirectorName)
                        DestroyImmediate(oldDir.gameObject);
                }

                // 创建新的导演对象并关联Timeline
                directorObj = new GameObject(sceneDirectorName);
                director = directorObj.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.extrapolationMode = DirectorWrapMode.Hold;
            }
            else
            {
                director.playableAsset = timelineAsset;
                EditorUtility.SetDirty(director);
            }

            // 添加动画轨道并绑定到模型（关键：确保轨道控制目标模型）
            AnimationTrack animTrack = timelineAsset.CreateTrack<AnimationTrack>("动画轨道");
            director.SetGenericBinding(animTrack, characterModel);

            // --------------- 核心修改2：直接使用已覆写的目标Controller ---------------
            // 此时模型的Animator已被强制设置为targetController，直接获取即可
            RuntimeAnimatorController modelController = characterAnimator.runtimeAnimatorController;
            // ----------------------------------------------------------------------

            if (modelController != null)
            {
                // 查找并添加当前Controller中匹配的动画剪辑（避免无关动画混入）
                foreach (var clip in modelController.animationClips)
                {
                    if (clip.name.Contains(bundleBaseName))
                    {
                        TimelineClip animTimelineClip = animTrack.CreateDefaultClip();
                        animTimelineClip.displayName = clip.name;
                        animTimelineClip.start = 0;
                        animTimelineClip.duration = clip.length;

                        // 赋值动画剪辑（修复属性名大小写问题，统一用小写clip，兼容不同Unity版本）
                        AnimationPlayableAsset animationAsset = animTimelineClip.asset as AnimationPlayableAsset;
                        if (animationAsset != null)
                        {
                            animationAsset.clip = clip; // 部分Unity版本中属性为小写clip，根据实际版本调整
                                                        // 若报错“不存在clip属性”，则改为：animationAsset.AnimationClip = clip;
                        }
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "角色模型的Animator没有关联控制器", "确定");
                return;
            }

            // 添加音频轨道（可选）
            if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
            {
                AudioTrack audioTrack = timelineAsset.CreateTrack<AudioTrack>("音频轨道");
                var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioFilePath);

                if (audioClip != null)
                {
                    TimelineClip audioTimelineClip = audioTrack.CreateDefaultClip();
                    audioTimelineClip.displayName = audioClip.name;
                    audioTimelineClip.start = 0;
                    audioTimelineClip.duration = audioClip.length;

                    AudioPlayableAsset audioAsset = audioTimelineClip.asset as AudioPlayableAsset;
                    if (audioAsset != null)
                    {
                        audioAsset.clip = audioClip;
                    }
                }
            }

            // 保存所有修改（确保Controller覆写、Timeline轨道变更生效）
            EditorUtility.SetDirty(characterModel);   // 保存模型的Controller变更
            EditorUtility.SetDirty(timelineAsset);    // 保存Timeline轨道变更
            EditorUtility.SetDirty(director.gameObject); // 保存导演对象变更
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 提示用户操作指引
            EditorUtility.DisplayDialog(
                "Timeline创建成功",
                $"✅ 已完成以下操作：\n" +
                $"- 模型：{characterModel.name}\n" +
                $"- 控制器：已绑定 {targetController.name}\n" +
                $"- Timeline路径：{timelinePath}\n\n" +
                $"操作提示：\n" +
                $"1. 在Window > Sequencing > Timeline打开编辑器\n" +
                $"2. 点击场景播放按钮预览动画",
                "确定");

            // 自动打开Timeline窗口（提升用户体验）
            EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
        }


        private void DrawAssetBundleSettings()
        {
            EditorGUILayout.LabelField(Get(SECTION_BUNDLE), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Get(HELP_PREVIEW_FIRST), MessageType.Info);
            EditorGUILayout.HelpBox(Get(HELP_ADJUST_POSE), MessageType.Info);
            EditorGUILayout.LabelField(Get(AUTO_BUILD_ADVANCED), EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            if (string.IsNullOrEmpty(bundleOutputPath))
            {
                bundleOutputPath = DefaultOutputPath;
            }
            bundleOutputPath = EditorGUILayout.TextField(Get(AUTO_BUILD_OUTPUT_PATH), bundleOutputPath);
            if (GUILayout.Button(Get(BTN_SELECT_OUTPUT_PATH), GUILayout.Width(120)))
            {
                bundleOutputPath = SelectBundleOutputPath(bundleOutputPath);
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(bundleOutputPath))
            {
                bool isInProject = bundleOutputPath.StartsWith(Application.dataPath) ||
                                  bundleOutputPath.StartsWith("Assets/");
                EditorGUILayout.HelpBox(
                    isInProject
                        ? string.Format(Get("help_path_in_project"), bundleOutputPath)
                        : string.Format(Get("help_path_outside_project"), bundleOutputPath),
                    MessageType.Info
                );
            }

            // 获取中间anim, controller文件的路径
            string outputPath = GetOutputPath();
            DrawBundleAssetsPreview(outputPath);

            UnityEngine.GUI.enabled = CanBuildBundle(outputPath);
            if (GUILayout.Button(Get(BTN_AUTO_BUILD), GUILayout.Height(30)))
            {
                AssetUtils.BuildAssetBundle(
                    outputPath,
                    TempBuildFolder,
                    bundleBaseName,
                    audioFilePath,
                    bundleOutputPath,
                    bundleOptions
                );
            }
            UnityEngine.GUI.enabled = true;

            EditorGUILayout.HelpBox(Get(HELP_MANUAL_BUILD), MessageType.Info);
            EditorGUILayout.Space();
        }


        private void ProcessAnimationAndController()
        {
            if (!addMorphCurves && !addCameraCurves)
            {
                EditorUtility.DisplayDialog(
                    Get(DIALOG_INFO),
                    Get(HELP_SELECT_CURVE_TYPE),
                    Get(DIALOG_CONFIRM)
                );
                return;
            }

            try
            {
                var baseClip = AnimUtils.CreateOriginalAnimationClip(sourceClip, bundleBaseName, DefaultFrameRate);
                if (baseClip == null)
                {
                    EditorUtility.DisplayDialog(
                        Get(DIALOG_ERROR),
                        Get("msg_no_original_clip"),
                        Get(DIALOG_CONFIRM)
                    );
                    return;
                }

                if (addMorphCurves && IsMorphVmdDataReady())
                {
                    baseClip = directMappingMode
                        ? AnimUtils.AddMorphCurvesDirectMode(
                            baseClip,
                            vmdMorphFrames,
                            selectedMorphs,
                            morphMapping,
                            defaultSkinnedMeshPath
                        )
                        : AnimUtils.AddMorphCurvesToAnimation(
                            baseClip,
                            vmdMorphFrames,
                            selectedMorphs,
                            morphMapping,
                            targetModel,
                            bodyRenderer
                        );
                }

                if (addCameraCurves && cameraVmdParsed)
                {
                    foreach (var cameraVmdFilePath in cameraVmdFilePaths)
                    {
                        baseClip = AnimUtils.AddCameraCurvesToClip(
                            baseClip,
                            cameraVmdFilePath,
                            cameraRootPath,
                            cameraDistancePath,
                            cameraComponentPath,
                            cameraScale
                        );
                    }
                }


                string outputPath = GetOutputPath();
                string clipPath = $"{outputPath}{bundleBaseName}.anim";
                AssetDatabase.CreateAsset(baseClip, clipPath);
                // 设置动画剪辑的相关属性
                AnimUtils.ApplyAnimationClipSettings(baseClip);

                AnimatorController controller = AssetUtils.CreateControllerForClip(
                    baseClip,
                    "",
                    outputPath,
                    bundleBaseName
                );

                string successMessage = string.Format(Get("msg_anim_created"), baseClip.name);
                if (controller != null)
                {
                    successMessage += "\n" + string.Format(Get("msg_controller_created"), controller.name);
                }

                EditorUtility.DisplayDialog(
                    Get(DIALOG_SUCCESS),
                    successMessage,
                    Get(DIALOG_CONFIRM)
                );
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    Get(DIALOG_ERROR),
                    string.Format(Get("msg_process_error"), e.Message),
                    Get(DIALOG_CONFIRM)
                );
                Debug.LogError(string.Format(Get("log_anim_process_error"), e));
            }
        }

        #endregion

        #region 辅助方法和状态管理

        private void BrowseAudioFile()
        {
            var path = EditorUtility.OpenFilePanel(Get("select_audio_file"), Application.dataPath, "wav,mp3,ogg");
            if (!string.IsNullOrEmpty(path))
            {
                audioFilePath = AssetUtils.GetProjectRelativePath(path);
            }
        }

        private string SelectBundleOutputPath(string currentPath)
        {
            var path = EditorUtility.OpenFolderPanel(Get("select_output_folder"),
                string.IsNullOrEmpty(currentPath) ? Application.dataPath : currentPath,
                "");

            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            return currentPath;
        }

        private void DrawBundleAssetsPreview(string outputPath)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Get(ASSETS_TO_PACK), EditorStyles.miniBoldLabel);

            string clipName = $"{bundleBaseName}.anim";
            EditorGUILayout.LabelField(string.Format(Get(ASSET_ANIMATION), clipName), EditorStyles.miniLabel);
            string clipFullPath = Path.Combine(outputPath, clipName);
            if (!File.Exists(clipFullPath))
            {
                EditorGUILayout.HelpBox(
                    string.Format(Get("help_anim_not_exist"), clipName, outputPath),
                    MessageType.Warning
                );
            }

            string controllerName = $"{bundleBaseName}.controller";
            EditorGUILayout.LabelField(
                string.Format(Get(ASSET_CONTROLLER), controllerName),
                EditorStyles.miniLabel
            );
            string controllerFullPath = Path.Combine(outputPath, controllerName);
            if (!File.Exists(controllerFullPath))
            {
                EditorGUILayout.HelpBox(
                    string.Format(Get("help_controller_not_exist"), controllerName, outputPath),
                    MessageType.Warning
                );
            }

            if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
            {
                string audioName = Path.GetFileName(audioFilePath);
                EditorGUILayout.LabelField(
                    string.Format(Get(ASSET_AUDIO), audioName),
                    EditorStyles.miniLabel
                );
            }
            else
            {
                EditorGUILayout.LabelField(Get(ASSET_AUDIO_NONE), EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField(
                string.Format(Get(BUNDLE_OUTPUT_INFO), bundleBaseName),
                EditorStyles.miniBoldLabel
            );
        }

        private void ShowMorphStatistics()
        {
            if (!IsMorphVmdDataReady()) return;

            var totalMorphs = vmdMorphFrames.Select(f => f.MorphName).Distinct().Count();
            var matchedMorphs = vmdMorphFrames
                .Select(f => ModelUtils.GetMappedMorphName(
                    f.MorphName,
                    morphMapping,
                    availableMorphs
                ))
                .Distinct()
                .Count(n => availableMorphs.Contains(n));

            EditorGUILayout.LabelField(
                string.Format(Get(VMD_MORPH_COUNT), totalMorphs),
                EditorStyles.miniLabel
            );
            EditorGUILayout.LabelField(
                string.Format(Get(MATCHED_MORPH_COUNT), matchedMorphs),
                EditorStyles.miniLabel
            );

            var matchRate = totalMorphs > 0 ? (float)matchedMorphs / totalMorphs * 100 : 0;
            EditorGUILayout.LabelField(
                string.Format(Get(MATCH_RATE), matchRate),
                EditorStyles.miniLabel
            );

            if (matchedMorphs == 0 && !directMappingMode)
            {
                EditorGUILayout.HelpBox(Get(HELP_NO_MATCH), MessageType.Warning);
            }
        }

        private void AutoNameResources()
        {
            string baseName = "";

            if (morphVmdFilePaths != null && morphVmdFilePaths.Count > 0)
            {
                baseName = Path.GetFileNameWithoutExtension(morphVmdFilePaths[0]);
            }
            else if (!string.IsNullOrEmpty(animVmdFilePath))
            {
                baseName = Path.GetFileNameWithoutExtension(animVmdFilePath);
            }
            else if (sourceClip != null)
            {
                baseName = sourceClip.name;
            }

            if (!string.IsNullOrEmpty(baseName))
            {
                bundleBaseName = baseName;
                newClipName = baseName;
                controllerName = baseName;
            }
        }
        private async void RunAnimConversionTask()
        {
            // 1. 基础检查
            if (string.IsNullOrEmpty(animVmdFilePath) || !File.Exists(animVmdFilePath))
            {
                EditorUtility.DisplayDialog(Get(DIALOG_ERROR), "VMD path is invalid.", Get(DIALOG_CONFIRM));
                return;
            }

            // 2. 准备路径
            string animOutputDir = GetOutputPath();

            AssetUtils.EnsureDirectoryExists(animOutputDir);
            string animFileName = Path.GetFileNameWithoutExtension(animVmdFilePath) + ".anim";
            string animFullPath = Path.Combine(animOutputDir, animFileName);

            // 3. 处理取消令牌
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
            cancellationTokenSource = new CancellationTokenSource();

            isConverting = true;

            try
            {
                // 4. 调用转换逻辑
                bool result = await VMD2Anim.VMDConverter.ConvertVMD(
                    animVmdFilePath,
                    showPmxOptions && !string.IsNullOrEmpty(pmxFilePath) ? pmxFilePath : null,
                    animOutputDir,
                    (p, msg) =>
                    {
                        progress = p;
                        progressMessage = msg;
                        Repaint(); // 强制刷新界面以更新进度条
                    },
                    overwrite: true,
                    quickMode: useQuickConfig,
                    timeoutMs: timeoutSeconds * 1000,
                    cancellationToken: cancellationTokenSource.Token
                );

                // 5. 处理结果
                if (result && File.Exists(animFullPath))
                {
                    // 重新加载资源
                    AssetDatabase.Refresh();
                    sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetUtils.GetProjectRelativePath(animFullPath));
                    AnimUtils.ApplyAnimationClipSettings(sourceClip);
                    EditorUtility.DisplayDialog(Get(DIALOG_SUCCESS), string.Format(Get("msg_anim_generated"), animFileName), Get(DIALOG_CONFIRM));

                    // 自动命名资源（复用你原有的逻辑）
                    AutoNameResources();
                    animVmdParsed = true;
                }
                else
                {
                    EditorUtility.DisplayDialog(Get(DIALOG_ERROR), Get("msg_conversion_failed"), Get(DIALOG_CONFIRM));
                }
            }
            catch (OperationCanceledException)
            {
                EditorUtility.DisplayDialog(Get(DIALOG_CANCEL), Get("msg_conversion_cancelled"), Get(DIALOG_CONFIRM));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(Get(DIALOG_ERROR), string.Format(Get("msg_conversion_error"), ex.Message), Get(DIALOG_CONFIRM));
                UnityEngine.Debug.LogError(string.Format(Get("log_conversion_failed"), ex.Message));
            }
            finally
            {
                // 6. 清理状态
                isConverting = false;
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }
                Repaint();
            }
        }



        #endregion

        #region 状态检查和重置

        private string GetOutputPath()
        {
            if (outputLocationMode == OutputLocationMode.DefaultFolder)
            {
                return DefaultOutputPath;
            }

            return !string.IsNullOrEmpty(animVmdFilePath)
                ? Path.GetDirectoryName(animVmdFilePath) + "/"
                : DefaultOutputPath;
        }

        private bool CanProcessAnimation()
        {
            bool hasValidSource = sourceClip != null;
            bool hasValidMorphData = !addMorphCurves || (IsMorphVmdDataReady() && selectedMorphs.Any(m => m.Value));
            bool hasValidCameraData = !addCameraCurves || cameraVmdParsed;
            bool hasValidModel = directMappingMode || (targetModel != null && bodyRenderer != null);

            return hasValidSource && hasValidCameraData && hasValidModel;
        }

        private bool CanBuildBundle(string outputPath)
        {
            return !string.IsNullOrEmpty(AssetUtils.GetAnimationPath(newClipName, outputPath, sourceClip)) &&
                   !string.IsNullOrEmpty(AssetUtils.GetControllerPath(controllerName, outputPath)) &&
                   !string.IsNullOrEmpty(outputPath) &&
                   !string.IsNullOrEmpty(bundleBaseName);
        }

        private bool IsMorphVmdDataReady() => morphVmdParsed && vmdMorphFrames != null && vmdMorphFrames.Count > 0;
        private bool IsCameraVmdDataReady() => cameraVmdParsed && vmdCameraFrames != null && vmdCameraFrames.Count > 0;
        private bool IsAnimVmdDataReady() => animVmdParsed;

        private bool IsModelDataReady() => !directMappingMode && targetModel != null && bodyRenderer != null;

        private void ResetAnimVmdState()
        {
            animVmdFilePath = "";
            animVmdParsed = false;
            parsedAnimVmd = null;
        }

        private void ResetCameraVmdState()
        {
            cameraVmdFilePaths.Clear();
            parsedCameraVmds.Clear();
            vmdCameraFrames.Clear();
            cameraVmdParsed = false;
        }

        private void ResetMorphVmdState()
        {
            morphVmdFilePaths.Clear();
            parsedMorphVmds.Clear();
            vmdMorphFrames.Clear();
            morphVmdParsed = false;
        }

        private void ResetModelState()
        {
            bodyRenderer = null;
            availableMorphs.Clear();
            selectedMorphs.Clear();
            morphMapping.Clear();
            Repaint();
        }

        private void OnEnable()
        {
            configManager = new ToolConfigManager();
            ApplyConfigToTool();
        }

        private void OnDisable()
        {
            SyncToolToConfig();
            configManager.SaveConfig();
        }

        private void ApplyConfigToTool()
        {
            var config = configManager.Config;

            defaultSkinnedMeshPath = config.defaultSkinnedMeshPath;
            defaultSkinnedMeshName = config.defaultSkinnedMeshName;
            directMappingMode = config.directMappingMode;
            enableCameraAnimation = config.enableCameraAnimation;
            cameraRootPath = config.cameraRootPath;
            cameraComponentPath = config.cameraComponentPath;
            cameraDistancePath = config.cameraDistancePath;
            bundleOutputPath = config.bundleOutputPath;
            outputLocationMode = config.OutputLocationMode;
        }

        private void SyncToolToConfig()
        {
            var config = configManager.Config;

            config.defaultSkinnedMeshPath = defaultSkinnedMeshPath;
            config.defaultSkinnedMeshName = defaultSkinnedMeshName;
            config.directMappingMode = directMappingMode;
            config.enableCameraAnimation = enableCameraAnimation;
            config.cameraRootPath = cameraRootPath;
            config.cameraComponentPath = cameraComponentPath;
            config.cameraDistancePath = cameraDistancePath;
            config.bundleOutputPath = bundleOutputPath;
            config.OutputLocationMode = outputLocationMode;
        }

        #endregion
    }
}