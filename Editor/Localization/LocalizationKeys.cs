// LocalizationKeys.cs
namespace UnityMMDConverter
{
    /// <summary>
    /// 本地化键值常量，避免硬编码字符串
    /// </summary>
    public static class L10nKeys
    {
        // 通用
        public const string LANGUAGE_LABEL = "language_label";
        public const string WINDOW_TITLE = "window_title";
        public const string BTN_CLEAR = "btn_clear";
        public const string BTN_CLEAR_ALL = "btn_clear_all";
        public const string BTN_BROWSE = "btn_browse";
        public const string BTN_RESET = "btn_reset";
        public const string BTN_CANCEL = "btn_cancel";
        public const string BTN_AUTO_NAME = "btn_auto_name";
        public const string BTN_REMOVE = "btn_remove";
        
        // 第1部分：动画提取
        public const string SECTION_ANIMATION = "section_animation";
        public const string ANIM_SOURCE = "anim_source";
        public const string EXISTING_CLIP = "existing_clip";
        public const string ANIM_VMD_FILE = "anim_vmd_file";
        public const string TIMEOUT_SECONDS = "timeout_seconds";
        public const string HELP_CONVERSION_FAIL = "help_conversion_fail";
        public const string QUICK_CONFIG = "quick_config";
        public const string PMX_ASSIST = "pmx_assist";
        public const string PMX_FILE = "pmx_file";
        public const string PMX_NOT_SELECTED = "pmx_not_selected";
        public const string BTN_GENERATE_ANIM = "btn_generate_anim";
        public const string CONVERTING_PROGRESS = "converting_progress";
        
        // 第2部分：镜头提取
        public const string SECTION_CAMERA = "section_camera";
        public const string ENABLE_CAMERA = "enable_camera";
        public const string CAMERA_VMD_FILE = "camera_vmd_file";
        public const string ADDED_CAMERA_FILES = "added_camera_files";
        public const string CAMERA_SCALE = "camera_scale";
        public const string CAMERA_PATH_CONFIG = "camera_path_config";
        public const string CAMERA_ROOT_PATH = "camera_root_path";
        public const string CAMERA_DISTANCE_PATH = "camera_distance_path";
        public const string CAMERA_COMPONENT_PATH = "camera_component_path";
        public const string BTN_PARSE_CAMERA = "btn_parse_camera";
        public const string CAMERA_PARSED_INFO = "camera_parsed_info";
        
        // 第3部分：表情提取
        public const string SECTION_MORPH = "section_morph";
        public const string MORPH_VMD_FILE = "morph_vmd_file";
        public const string ADDED_MORPH_FILES = "added_morph_files";
        public const string BTN_PARSE_MORPH = "btn_parse_morph";
        public const string MORPH_PARSED_INFO = "morph_parsed_info";
        
        // 模型设置
        public const string SECTION_MODEL = "section_model";
        public const string DIRECT_MAPPING = "direct_mapping";
        public const string HELP_DIRECT_MAPPING = "help_direct_mapping";
        public const string SKINNED_MESH_PATH_SETTINGS = "skinned_mesh_path_settings";
        public const string SKINNED_MESH_PATH = "skinned_mesh_path";
        public const string COMPONENT_NAME = "component_name";
        public const string HELP_NON_DIRECT_MAPPING = "help_non_direct_mapping";
        public const string TARGET_MODEL = "target_model";
        public const string VMD_MORPH_COUNT = "vmd_morph_count";
        public const string MATCHED_MORPH_COUNT = "matched_morph_count";
        public const string MATCH_RATE = "match_rate";
        public const string HELP_NO_MATCH = "help_no_match";
        
        // 形态键映射
        public const string MORPH_MAPPING_SETTINGS = "morph_mapping_settings";
        public const string MORPH_MAPPING_INSTRUCTION1 = "morph_mapping_instruction1";
        public const string MORPH_MAPPING_INSTRUCTION2 = "morph_mapping_instruction2";
        public const string BTN_SELECT_ALL = "btn_select_all";
        public const string BTN_SELECT_FIRST_20 = "btn_select_first_20";
        public const string BTN_DESELECT_ALL = "btn_deselect_all";
        public const string HELP_NO_MORPH_DATA = "help_no_morph_data";
        
        // 输出设置
        public const string SECTION_OUTPUT = "section_output";
        public const string ANIMATION_CURVE_OPTIONS = "animation_curve_options";
        public const string ADD_MORPH_CURVES = "add_morph_curves";
        public const string ADD_CAMERA_CURVES = "add_camera_curves";
        public const string HELP_MERGE_MORPH_CAMERA = "help_merge_morph_camera";
        public const string HELP_MERGE_MORPH = "help_merge_morph";
        public const string HELP_MERGE_CAMERA = "help_merge_camera";
        public const string HELP_SELECT_CURVE_TYPE = "help_select_curve_type";
        
        // 命名设置
        public const string SECTION_NAMING = "section_naming";
        public const string BASE_NAME = "base_name";
        
        // 音频设置
        public const string SECTION_AUDIO = "section_audio";
        public const string AUDIO_FILE = "audio_file";
        public const string AUDIO_NOT_SELECTED = "audio_not_selected";
        
        // Timeline预览
        public const string SECTION_TIMELINE = "section_timeline";
        public const string CHARACTER_MODEL = "character_model";
        public const string DRAG_MODEL_HERE = "drag_model_here";
        public const string BTN_CREATE_TIMELINE = "btn_create_timeline";
        public const string HELP_SPECIFY_MODEL = "help_specify_model";
        public const string HELP_SET_BASE_NAME = "help_set_base_name";
        public const string HELP_GENERATE_RESOURCES = "help_generate_resources";
        
        // AssetBundle打包
        public const string SECTION_BUNDLE = "section_bundle";
        public const string HELP_PREVIEW_FIRST = "help_preview_first";
        public const string HELP_ADJUST_POSE = "help_adjust_pose";
        public const string AUTO_BUILD_ADVANCED = "auto_build_advanced";
        public const string BUNDLE_ADVANCED_OPTIONS = "bundle_advanced_options";
        public const string BUNDLE_OPTIONS = "bundle_options";
        public const string HELP_BUNDLE_OPTIONS = "help_bundle_options";
        public const string AUTO_BUILD_OUTPUT_PATH = "auto_build_output_path";
        public const string BTN_SELECT_OUTPUT_PATH = "btn_select_output_path";
        public const string BTN_AUTO_BUILD = "btn_auto_build";
        public const string HELP_MANUAL_BUILD = "help_manual_build";
        public const string ASSETS_TO_PACK = "assets_to_pack";
        public const string ASSET_ANIMATION = "asset_animation";
        public const string ASSET_CONTROLLER = "asset_controller";
        public const string ASSET_AUDIO = "asset_audio";
        public const string ASSET_AUDIO_NONE = "asset_audio_none";
        public const string BUNDLE_OUTPUT_INFO = "bundle_output_info";
        
        // 对话框
        public const string DIALOG_SUCCESS = "dialog_success";
        public const string DIALOG_ERROR = "dialog_error";
        public const string DIALOG_WARNING = "dialog_warning";
        public const string DIALOG_INFO = "dialog_info";
        public const string DIALOG_CONFIRM = "dialog_confirm";
        public const string DIALOG_CANCEL = "dialog_cancel";
        
        // 操作按钮
        public const string BTN_PROCESS = "btn_process";
        public const string BTN_ADD_CAMERA_VMD = "btn_add_camera_vmd";
        public const string BTN_ADD_MORPH_VMD = "btn_add_morph_vmd";
    }
}