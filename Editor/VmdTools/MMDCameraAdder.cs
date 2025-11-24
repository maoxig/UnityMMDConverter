using UnityEditor;
using UnityEngine;

public class MMDCameraAdder : EditorWindow
{
    // 添加菜单项
    [MenuItem("MMD for Unity/Add MMD Camera Structure for Selected GameObject", false, 10)]
    static void AddMMDCameraStructure()
    {
        // 获取当前选中的GameObject
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个GameObject", "确定");
            return;
        }

        // 创建相机层级结构
        CreateCameraHierarchy(selectedObject);
    }

    // 创建相机层级结构
    static void CreateCameraHierarchy(GameObject parentObject)
    {
        // 创建Camera_root GameObject并设置旋转
        GameObject cameraRoot = new GameObject("Camera_root");
        cameraRoot.transform.SetParent(parentObject.transform);
        cameraRoot.transform.localPosition = Vector3.zero;
        cameraRoot.transform.localRotation = Quaternion.Euler(0, 180, 0); // Y轴旋转180度

        // 创建Camera_root_1 GameObject
        GameObject cameraRoot1 = new GameObject("Camera_root_1");
        cameraRoot1.transform.SetParent(cameraRoot.transform);
        cameraRoot1.transform.localPosition = Vector3.zero;
        cameraRoot1.transform.localRotation = Quaternion.identity;

        // 创建Camera GameObject并添加Camera组件
        GameObject cameraObj = new GameObject("Camera");
        cameraObj.transform.SetParent(cameraRoot1.transform);
        cameraObj.transform.localPosition = Vector3.zero;
        cameraObj.transform.localRotation = Quaternion.identity;

        // 添加Camera组件
        Camera cameraComponent = cameraObj.AddComponent<Camera>();

        // 关闭AudioListener（如果存在）
        AudioListener audioListener = cameraObj.GetComponent<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
        // 设置相机GameObject为非激活状态
        cameraObj.SetActive(false);

        // 选中并激活Camera_root对象（因为相机对象已禁用，无法直接选中）
        Selection.activeGameObject = cameraRoot;
        EditorGUIUtility.PingObject(cameraRoot);

        Debug.Log("已成功创建MMD相机结构，相机对象已设置为非激活状态");
    }
}