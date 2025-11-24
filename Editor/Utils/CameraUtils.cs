using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;


// using MMD.VMD;
using UnityMMDConverter.Utils;

namespace UnityMMDConverter.Utils
{
    public class CameraVmdAgent
    {
        string _vmdFile;
        MMD.VMD.VMDFormat format_;
        string cameraRootPath; // 相机组件完整路径
        string distancePath;   // 距离控制路径

        string cameraComponentPath; //= "Main Camera"; // 默认相机组件路径

        float cameraScale = 1.0f; // 相机缩放比例

        public CameraVmdAgent(string filePath, string cameraRootPath, string distancePath, string cameraComponentPath, float cameraScale)
        {
            _vmdFile = filePath;
            this.cameraRootPath = cameraRootPath;
            this.distancePath = distancePath;
            this.cameraComponentPath = cameraComponentPath;
            this.cameraScale = cameraScale;

            // 验证路径格式
            if (string.IsNullOrEmpty(this.cameraRootPath))
                Debug.LogWarning("相机根路径为空，可能导致曲线生成失败");
            if (string.IsNullOrEmpty(cameraComponentPath))
                Debug.LogWarning("相机组件路径为空，可能导致相机动画失效");
            if (string.IsNullOrEmpty(distancePath))
                Debug.LogWarning("距离控制路径为空，可能导致距离动画失效");
        }

        public AnimationClip CreateAnimationClip()
        {
            if (null == format_)
            {
                format_ = VMDLoaderScript.Import(_vmdFile);
                if (format_ == null)
                {
                    Debug.LogError("VMDLoaderScript.Import 加载VMD文件失败");
                    return null;
                }
                if (format_.camera_list == null || format_.camera_list.camera_count == 0)
                {
                    Debug.LogError("VMD文件中未包含相机数据");
                    return null;
                }
            }

            // 生成相机曲线时明确传递路径参数
            AnimationClip animation_clip = VMDCameraConverter.CreateAnimationClip(
                format_,
                cameraRootPath,  // 确保路径被正确使用
                distancePath,     // 传递距离控制路径
                cameraComponentPath, // 相机组件完整路径
                cameraScale       // 相机缩放比例
            );

            if (animation_clip == null)
            {
                throw new System.Exception("VMDCameraConverter.CreateAnimationClip 返回空剪辑");
            }

            // 验证生成的剪辑是否有曲线
            var bindings = AnimationUtility.GetCurveBindings(animation_clip);
            if (bindings.Length == 0)
            {
                Debug.LogError("生成的相机剪辑不包含任何曲线，可能是路径配置错误");
                return null;
            }
            Debug.Log($"CameraVmdAgent生成相机剪辑，包含 {bindings.Length} 条曲线，路径: {cameraRootPath}");

            // 保存临时剪辑
            string vmd_folder = Path.GetDirectoryName(_vmdFile);
            string anima_file = Path.Combine(vmd_folder, $"{animation_clip.name}_temp_camera.anim");
            if (File.Exists(anima_file))
                AssetDatabase.DeleteAsset(anima_file);
            AssetDatabase.CreateAsset(animation_clip, anima_file);
            AssetDatabase.ImportAsset(anima_file);

            return animation_clip;
        }
    }
}