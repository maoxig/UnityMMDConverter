// LocalizationManager.cs
using System.Collections.Generic;
using UnityEditor;

namespace UnityMMDConverter
{
    public enum Language
    {
        Chinese,
        English
    }

    public static class LocalizationManager
    {
        private static Language currentLanguage = Language.Chinese;
        private static Dictionary<string, Dictionary<Language, string>> translations;

        public static Language CurrentLanguage
        {
            get => currentLanguage;
            set
            {
                currentLanguage = value;
                EditorPrefs.SetInt("VmdTool_Language", (int)value);
            }
        }

        static LocalizationManager()
        {
            currentLanguage = (Language)EditorPrefs.GetInt("VmdTool_Language", 0);
            InitializeTranslations();
        }

        public static string Get(string key)
        {
            if (translations.TryGetValue(key, out var langDict))
            {
                if (langDict.TryGetValue(currentLanguage, out var text))
                    return text;
            }
            return $"[Missing: {key}]";
        }

        private static void InitializeTranslations()
        {
            translations = new Dictionary<string, Dictionary<Language, string>>
            {
                // ==================== 通用 ====================
                ["language_label"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "Language / 语言:",
                    [Language.English] = "Language / 语言:"
                },

                // ==================== 窗口标题 ====================
                ["window_title"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "VMD Morph Animator Tool",
                    [Language.English] = "VMD Morph Animator Tool"
                },

                // ==================== 第1部分：动画提取 ====================
                ["section_animation"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "1. 动画提取",
                    [Language.English] = "1. Animation Extraction"
                },
                ["anim_source"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "动画来源",
                    [Language.English] = "Animation Source"
                },
                ["anim_from_existing"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "从已有剪辑",
                    [Language.English] = "From Existing Clip"
                },
                ["anim_from_vmd"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "从VMD文件",
                    [Language.English] = "From VMD File"
                },
                ["existing_clip"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "已有动画剪辑",
                    [Language.English] = "Existing Animation Clip"
                },
                ["anim_vmd_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "动画VMD文件",
                    [Language.English] = "Animation VMD File"
                },
                ["timeout_seconds"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "转换超时（秒）",
                    [Language.English] = "Conversion Timeout (Seconds)"
                },
                ["help_conversion_fail"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "如果转换失败，请尝试手动生成anim文件",
                    [Language.English] = "If conversion fails, try generating anim file manually"
                },
                ["quick_config"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "使用快速转换配置文件",
                    [Language.English] = "Use Quick Conversion Config"
                },
                ["pmx_assist"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "使用PMX/PMD模型辅助转换（可选）",
                    [Language.English] = "Use PMX/PMD Model for Assisted Conversion (Optional)"
                },
                ["pmx_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "PMX/PMD文件",
                    [Language.English] = "PMX/PMD File"
                },
                ["pmx_not_selected"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "未选择PMX/PMD文件",
                    [Language.English] = "No PMX/PMD File Selected"
                },
                ["btn_generate_anim"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "从VMD生成动画剪辑",
                    [Language.English] = "Generate Animation Clip from VMD"
                },
                ["converting_progress"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "转换进度:",
                    [Language.English] = "Converting Progress:"
                },
                ["btn_cancel"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "取消",
                    [Language.English] = "Cancel"
                },

                // ==================== 第2部分：镜头提取 ====================
                ["section_camera"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "2. 镜头提取",
                    [Language.English] = "2. Camera Extraction"
                },
                ["enable_camera"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "启用镜头动画",
                    [Language.English] = "Enable Camera Animation"
                },
                ["camera_vmd_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "镜头VMD文件",
                    [Language.English] = "Camera VMD File"
                },
                ["added_camera_files"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "已添加的镜头VMD文件:",
                    [Language.English] = "Added Camera VMD Files:"
                },
                ["btn_remove"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "移除",
                    [Language.English] = "Remove"
                },
                ["camera_scale"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "相机位移缩放",
                    [Language.English] = "Camera Position Scale"
                },
                ["camera_path_config"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "镜头路径配置",
                    [Language.English] = "Camera Path Configuration"
                },
                ["camera_root_path"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "相机位移接收路径",
                    [Language.English] = "Camera Position Receiver Path"
                },
                ["camera_distance_path"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "Distance父对象路径",
                    [Language.English] = "Distance Parent Object Path"
                },
                ["camera_component_path"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "相机组件完整路径",
                    [Language.English] = "Camera Component Full Path"
                },
                ["btn_parse_camera"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "解析所有镜头VMD文件",
                    [Language.English] = "Parse All Camera VMD Files"
                },
                ["camera_parsed_info"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "✓ 已解析 {0} 个镜头帧 (来自 {1} 个文件)",
                    [Language.English] = "✓ Parsed {0} camera frames (from {1} files)"
                },

                // ==================== 第3部分：表情提取 ====================
                ["section_morph"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "3. 表情提取",
                    [Language.English] = "3. Morph Extraction"
                },
                ["morph_vmd_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "表情VMD文件",
                    [Language.English] = "Morph VMD File"
                },
                ["added_morph_files"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "已添加的表情VMD文件:",
                    [Language.English] = "Added Morph VMD Files:"
                },
                ["btn_parse_morph"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "解析所有表情VMD文件",
                    [Language.English] = "Parse All Morph VMD Files"
                },
                ["morph_parsed_info"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "✓ 已解析 {0} 个表情帧，包含 {1} 种表情 (来自 {2} 个文件)",
                    [Language.English] = "✓ Parsed {0} morph frames, containing {1} morphs (from {2} files)"
                },

                // ==================== 模型设置 ====================
                ["section_model"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "模型表情设置",
                    [Language.English] = "Model Morph Settings"
                },
                ["direct_mapping"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "直接映射",
                    [Language.English] = "Direct Mapping"
                },
                ["help_direct_mapping"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "直接映射模式将直接使用VMD中的表情写入到对应路径的动画里，无需关联模型",
                    [Language.English] = "Direct mapping mode writes VMD morphs directly to animation paths without requiring model association"
                },
                ["skinned_mesh_path_settings"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "SkinnedMeshRenderer 路径设置",
                    [Language.English] = "SkinnedMeshRenderer Path Settings"
                },
                ["skinned_mesh_path"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "SkinnedMeshRenderer路径",
                    [Language.English] = "SkinnedMeshRenderer Path"
                },
                ["component_name"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "组件名称",
                    [Language.English] = "Component Name"
                },
                ["help_non_direct_mapping"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "非直接映射模式需要关联目标模型",
                    [Language.English] = "Non-direct mapping mode requires target model association"
                },
                ["target_model"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "目标模型",
                    [Language.English] = "Target Model"
                },
                ["btn_reset"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "重置",
                    [Language.English] = "Reset"
                },
                ["vmd_morph_count"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "VMD表情总数: {0} 个",
                    [Language.English] = "Total VMD Morphs: {0}"
                },
                ["matched_morph_count"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "匹配到模型的表情: {0} 个",
                    [Language.English] = "Matched Model Morphs: {0}"
                },
                ["match_rate"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "匹配率: {0:F1}%",
                    [Language.English] = "Match Rate: {0:F1}%"
                },
                ["help_no_match"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "未找到匹配的表情数据，请检查形态键映射设置",
                    [Language.English] = "No matched morph data found, please check morph mapping settings"
                },

                // ==================== 形态键映射 ====================
                ["morph_mapping_settings"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "形态键选择与映射设置",
                    [Language.English] = "Morph Selection and Mapping Settings"
                },
                ["morph_mapping_instruction1"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择需要使用的形态键并设置映射关系",
                    [Language.English] = "Select morphs to use and set mapping relationships"
                },
                ["morph_mapping_instruction2"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "（勾选启用，文本框填写映射目标名称）",
                    [Language.English] = "(Check to enable, enter target mapping name in text box)"
                },
                ["btn_select_all"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "全选",
                    [Language.English] = "Select All"
                },
                ["btn_select_first_20"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择前20个",
                    [Language.English] = "Select First 20"
                },
                ["btn_deselect_all"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "取消全选",
                    [Language.English] = "Deselect All"
                },
                ["help_no_morph_data"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "未找到可用的形态键数据，请先解析表情VMD文件或关联模型",
                    [Language.English] = "No available morph data found, please parse morph VMD files or associate model first"
                },

                // ==================== 输出设置 ====================
                ["section_output"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "输出设置",
                    [Language.English] = "Output Settings"
                },
                ["animation_curve_options"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "动画曲线添加选项",
                    [Language.English] = "Animation Curve Addition Options"
                },
                ["add_morph_curves"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "添加表情曲线",
                    [Language.English] = "Add Morph Curves"
                },
                ["add_camera_curves"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "添加镜头曲线",
                    [Language.English] = "Add Camera Curves"
                },
                ["help_merge_morph_camera"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "将原有动画与表情动画、镜头动画合并输出",
                    [Language.English] = "Merge original animation with morph and camera animations"
                },
                ["help_merge_morph"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "将原有动画与表情动画合并输出",
                    [Language.English] = "Merge original animation with morph animations"
                },
                ["help_merge_camera"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "将原有动画与镜头动画合并输出",
                    [Language.English] = "Merge original animation with camera animations"
                },
                ["help_select_curve_type"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "请至少选择一种曲线类型添加",
                    [Language.English] = "Please select at least one curve type to add"
                },

                // ==================== 命名设置 ====================
                ["section_naming"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "统一资源命名设置",
                    [Language.English] = "Unified Resource Naming Settings"
                },
                ["base_name"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "基础名称",
                    [Language.English] = "Base Name"
                },
                ["btn_auto_name"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "自动命名",
                    [Language.English] = "Auto Name"
                },

                // ==================== 操作按钮 ====================
                ["btn_process"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "添加到动画并创建控制器",
                    [Language.English] = "Add to Animation and Create Controller"
                },

                // ==================== 音频设置 ====================
                ["section_audio"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "音频设置",
                    [Language.English] = "Audio Settings"
                },
                ["audio_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "音频文件",
                    [Language.English] = "Audio File"
                },
                ["audio_not_selected"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "未选择音频文件",
                    [Language.English] = "No Audio File Selected"
                },
                ["btn_browse"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "浏览...",
                    [Language.English] = "Browse..."
                },

                // ==================== Timeline预览 ====================
                ["section_timeline"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "Timeline 预览",
                    [Language.English] = "Timeline Preview"
                },
                ["character_model"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "角色模型",
                    [Language.English] = "Character Model"
                },
                ["drag_model_here"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "拖放模型到此处",
                    [Language.English] = "Drag Model Here"
                },
                ["btn_create_timeline"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "创建预览Timeline",
                    [Language.English] = "Create Preview Timeline"
                },
                ["help_specify_model"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "请先指定角色模型",
                    [Language.English] = "Please specify character model first"
                },
                ["help_set_base_name"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "请设置有效的基础名称",
                    [Language.English] = "Please set a valid base name"
                },
                ["help_generate_resources"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "输出目录不存在，请先生成动画资源",
                    [Language.English] = "Output directory does not exist, please generate animation resources first"
                },

                // ==================== AssetBundle打包 ====================
                ["section_bundle"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "资源打包设置",
                    [Language.English] = "Asset Bundle Settings"
                },
                ["help_preview_first"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "打包前请先在Unity内预览，确保一切正常，并且确保音频轴对上动作轴",
                    [Language.English] = "Please preview in Unity before building, ensure everything is correct and audio is synced with motion"
                },
                ["help_adjust_pose"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "如果预览时人物朝向、初始位置不对，请在动画Inspector中调整",
                    [Language.English] = "If character orientation or initial position is incorrect during preview, adjust in Animation Inspector"
                },
                ["auto_build_advanced"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "自动打包（高级）",
                    [Language.English] = "Auto Build (Advanced)"
                },
                ["bundle_advanced_options"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "打包高级选项",
                    [Language.English] = "Bundle Advanced Options"
                },
                ["bundle_options"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "打包选项",
                    [Language.English] = "Bundle Options"
                },
                ["help_bundle_options"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "None: 基本打包\nChunkBasedCompression: 分块压缩\nDeterministicAssetBundle: 确定性打包",
                    [Language.English] = "None: Basic build\nChunkBasedCompression: Chunk-based compression\nDeterministicAssetBundle: Deterministic build"
                },
                ["auto_build_output_path"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "自动打包输出路径",
                    [Language.English] = "Auto Build Output Path"
                },
                ["btn_select_output_path"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择输出路径",
                    [Language.English] = "Select Output Path"
                },
                ["help_path_in_project"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "输出路径在项目内: {0}",
                    [Language.English] = "Output path inside project: {0}"
                },
                ["help_path_outside_project"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "输出路径在项目外: {0}",
                    [Language.English] = "Output path outside project: {0}"
                },
                ["btn_auto_build"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "📦 自动打包",
                    [Language.English] = "📦 Auto Build"
                },
                ["help_manual_build"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "如果自动打包失败, 请手动构建文件",
                    [Language.English] = "If auto build fails, please build files manually"
                },
                ["assets_to_pack"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "将打包的资源:",
                    [Language.English] = "Assets to Pack:"
                },
                ["asset_animation"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "- 动画: {0}",
                    [Language.English] = "- Animation: {0}"
                },
                ["asset_controller"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "- 控制器: {0}",
                    [Language.English] = "- Controller: {0}"
                },
                ["asset_audio"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "- 音频: {0}",
                    [Language.English] = "- Audio: {0}"
                },
                ["asset_audio_none"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "- 音频: 未选择",
                    [Language.English] = "- Audio: Not Selected"
                },
                ["bundle_output_info"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "资源将统一命名并被打包输出为: {0}.unity3d",
                    [Language.English] = "Resources will be named and packed as: {0}.unity3d"
                },
                ["help_anim_not_exist"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "动画文件 {0} 不存在于输出: {1}",
                    [Language.English] = "Animation file {0} does not exist in output: {1}"
                },
                ["help_controller_not_exist"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "控制器文件 {0} 不存在于输出: {1}",
                    [Language.English] = "Controller file {0} does not exist in output: {1}"
                },

                // ==================== 通用按钮和文本 ====================
                ["btn_clear"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "清空",
                    [Language.English] = "Clear"
                },
                ["btn_clear_all"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "清空所有",
                    [Language.English] = "Clear All"
                },
                ["btn_add"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "添加",
                    [Language.English] = "Add"
                },
                ["btn_add_camera_vmd"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "添加镜头VMD",
                    [Language.English] = "Add Camera VMD"
                },
                ["btn_add_morph_vmd"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "添加表情VMD",
                    [Language.English] = "Add Morph VMD"
                },
                ["file_not_selected"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "未选择{0} (可拖拽)",
                    [Language.English] = "{0} Not Selected (Drag & Drop Supported)"
                },
                ["file_not_selected_multi"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "未选择{0} (可拖拽多个)",
                    [Language.English] = "{0} Not Selected (Drag & Drop Multiple Files)"
                },
                ["file_count"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "已选择 {0} 个文件",
                    [Language.English] = "{0} Files Selected"
                },

                // ==================== 对话框消息 ====================
                ["dialog_success"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "成功",
                    [Language.English] = "Success"
                },
                ["dialog_error"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "错误",
                    [Language.English] = "Error"
                },
                ["dialog_warning"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "警告",
                    [Language.English] = "Warning"
                },
                ["dialog_info"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "提示",
                    [Language.English] = "Info"
                },
                ["dialog_confirm"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "确定",
                    [Language.English] = "OK"
                },
                ["dialog_cancel"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "取消",
                    [Language.English] = "Cancel"
                },
                ["msg_anim_generated"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "已生成动画剪辑: {0}",
                    [Language.English] = "Animation clip generated: {0}"
                },
                ["msg_conversion_failed"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "VMD转换为动画失败",
                    [Language.English] = "VMD to animation conversion failed"
                },
                ["msg_conversion_cancelled"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "VMD转换已取消",
                    [Language.English] = "VMD conversion cancelled"
                },
                ["msg_conversion_error"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "VMD转换失败: {0}",
                    [Language.English] = "VMD conversion failed: {0}"
                },
                ["msg_file_not_exist"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "{0}文件不存在",
                    [Language.English] = "{0} file does not exist"
                },
                ["msg_files_not_exist"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "部分{0}文件不存在",
                    [Language.English] = "Some {0} files do not exist"
                },
                ["msg_parse_success"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "成功解析{0}文件: {1}",
                    [Language.English] = "Successfully parsed {0} file: {1}"
                },
                ["msg_parse_error"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "解析{0}文件时出错: {1}",
                    [Language.English] = "Error parsing {0} file: {1}"
                },
                ["msg_anim_created"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "已生成动画: {0}",
                    [Language.English] = "Animation created: {0}"
                },
                ["msg_controller_created"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "已生成控制器: {0}",
                    [Language.English] = "Controller created: {0}"
                },
                ["msg_process_error"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "处理动画时出错: {0}",
                    [Language.English] = "Error processing animation: {0}"
                },
                ["msg_no_original_clip"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "未找到原动画剪辑",
                    [Language.English] = "Original animation clip not found"
                },
                ["msg_select_model"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "请先在 Inspector 中选择角色模型！",
                    [Language.English] = "Please select character model in Inspector first!"
                },
                ["msg_animator_added"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "已为模型自动添加Animator组件",
                    [Language.English] = "Animator component automatically added to model"
                },
                ["msg_controller_not_found"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "未找到目标动画控制器：{0}\n请先生成动画资源！",
                    [Language.English] = "Target animation controller not found: {0}\nPlease generate animation resources first!"
                },
                ["msg_timeline_created"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "Timeline创建成功",
                    [Language.English] = "Timeline Created Successfully"
                },
                ["msg_timeline_success_details"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "✅ 已完成以下操作：\n- 模型：{0}\n- 控制器：已绑定 {1}\n- Timeline路径：{2}\n\n操作提示：\n1. 在Window > Sequencing > Timeline打开编辑器\n2. 点击场景播放按钮预览动画",
                    [Language.English] = "✅ Completed operations:\n- Model: {0}\n- Controller: Bound {1}\n- Timeline path: {2}\n\nInstructions:\n1. Open editor in Window > Sequencing > Timeline\n2. Click scene play button to preview animation"
                },
                ["msg_no_animator_controller"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "角色模型的Animator没有关联控制器",
                    [Language.English] = "Character model's Animator has no associated controller"
                },

                // ==================== 文件选择对话框 ====================
                ["select_anim_vmd"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择动画VMD文件",
                    [Language.English] = "Select Animation VMD File"
                },
                ["select_camera_vmd"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择镜头VMD文件",
                    [Language.English] = "Select Camera VMD File"
                },
                ["select_morph_vmd"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择表情VMD文件",
                    [Language.English] = "Select Morph VMD File"
                },
                ["select_pmx_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择PMX/PMD文件",
                    [Language.English] = "Select PMX/PMD File"
                },
                ["select_audio_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择音频文件",
                    [Language.English] = "Select Audio File"
                },
                ["select_output_folder"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "选择输出文件夹",
                    [Language.English] = "Select Output Folder"
                },
                ["add_more_files_title"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "添加更多文件?",
                    [Language.English] = "Add More Files?"
                },
                ["add_more_camera_files"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "是否继续添加镜头VMD文件？",
                    [Language.English] = "Continue adding camera VMD files?"
                },
                ["add_more_morph_files"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "是否继续添加表情VMD文件？",
                    [Language.English] = "Continue adding morph VMD files?"
                },
                ["btn_continue_add"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "继续添加",
                    [Language.English] = "Continue Adding"
                },
                ["btn_done"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "完成",
                    [Language.English] = "Done"
                },

                // ==================== 进度条信息 ====================
                ["progress_parsing_camera"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "解析镜头VMD",
                    [Language.English] = "Parsing Camera VMD"
                },
                ["progress_parsing_morph"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "解析表情VMD",
                    [Language.English] = "Parsing Morph VMD"
                },
                ["progress_parsing_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "正在解析 {0} ({1}/{2})",
                    [Language.English] = "Parsing {0} ({1}/{2})"
                },

                // ==================== 调试和日志信息 ====================
                ["log_vmd_found"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "自动找到动画VMD文件: {0}",
                    [Language.English] = "Auto-found animation VMD file: {0}"
                },
                ["log_parse_success_frames"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "成功解析 {0} 个{1}文件，共 {2} 个{3}帧",
                    [Language.English] = "Successfully parsed {0} {1} files, total {2} {3} frames"
                },
                ["log_parse_success_morphs"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "成功解析 {0} 个表情VMD文件，共 {1} 个表情帧，{2} 种表情",
                    [Language.English] = "Successfully parsed {0} morph VMD files, {1} morph frames, {2} morph types"
                },
                ["log_controller_updated"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "控制器已更新: 模型原有控制器：{0}，已替换为：{1}（用于匹配当前Timeline动画）",
                    [Language.English] = "Controller updated: Original controller: {0}, replaced with: {1} (to match current Timeline animation)"
                },
                ["log_conversion_failed"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "[VMD转换] 失败: {0}",
                    [Language.English] = "[VMD Conversion] Failed: {0}"
                },
                ["log_parse_error"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "{0}VMD解析错误: {1}",
                    [Language.English] = "{0} VMD parse error: {1}"
                },
                ["log_auto_search_error"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "自动查找VMD文件时出错: {0}",
                    [Language.English] = "Error auto-searching VMD file: {0}"
                },
                ["log_anim_process_error"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "动画处理错误: {0}",
                    [Language.English] = "Animation processing error: {0}"
                },

                // ==================== 枚举值翻译 ====================
                ["enum_from_existing_clip"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "从已有剪辑",
                    [Language.English] = "From Existing Clip"
                },
                ["enum_from_vmd_file"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "从VMD文件",
                    [Language.English] = "From VMD File"
                },
                // ==================== 新增：输出位置配置 ====================
                ["output_location"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "中间文件输出位置",
                    [Language.English] = "Output Location for Intermediate Files"
                },
                ["output_location_default"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "文件将保存到: {0}",
                    [Language.English] = "Files will be saved to: {0}"
                },
                ["output_location_same_folder"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "文件将保存在.vmd文件所在的文件夹（如果在项目内）。",
                    [Language.English] = "Files will be saved in the same folder as the .vmd file (if inside project)."
                },
                ["settings_panel"] = new Dictionary<Language, string>
                {
                    [Language.Chinese] = "设置面板/Settings Panel",
                    [Language.English] = "设置面板/Settings Panel"
                },
            };
        }
    }
}
