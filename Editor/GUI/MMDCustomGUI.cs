using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMMDConverter.CustomGUI
{
    public static class MMDCustomGUI
    {
        /// <summary>
        /// 绘制文件列表选择器（仿Unity ObjectField风格）
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        /// <param name="extension">允许的文件后缀 (不带点，例如 "vmd")</param>
        /// <param name="label">标题</param>
        public static void DrawFileSelectorList(List<string> filePaths, string extension, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            // 1. 绘制已存在的文件列表
            for (int i = 0; i < filePaths.Count; i++)
            {
                string path = filePaths[i];
                string newPath = DrawSmartObjectField(path, extension, false);

                // 如果路径变了（且非空），更新；如果被清空，视为删除请求
                if (path != newPath)
                {
                    if (string.IsNullOrEmpty(newPath))
                    {
                        filePaths.RemoveAt(i);
                        i--; // 调整索引
                    }
                    else
                    {
                        filePaths[i] = newPath;
                    }
                }
            }

            // 2. 绘制末尾的一个空槽位（用于添加新文件）
            string addedPath = DrawSmartObjectField(null, extension, true);
            if (!string.IsNullOrEmpty(addedPath))
            {
                // 只有当路径不在列表中时才添加（防止重复）
                if (!filePaths.Contains(addedPath))
                {
                    filePaths.Add(addedPath);
                }
            }
        }

        /// <summary>
        /// 绘制单个文件选择器（也能用于单选模式）
        /// </summary>
        public static string DrawSingleFileSelector(string currentPath, string extension, string label)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            string result = DrawSmartObjectField(currentPath, extension, true);
            EditorGUILayout.EndHorizontal();
            return result;
        }

        /// <summary>
        /// 核心绘制逻辑：模拟 ObjectField
        /// </summary>
        /// <param name="path">当前路径</param>
        /// <param name="extension">后缀限制</param>
        /// <param name="allowEmpty">是否允许为空（用于空槽位样式）</param>
        /// <returns>新的路径</returns>
        private static string DrawSmartObjectField(string path, string extension, bool allowEmpty)
        {
            string resultPath = path;

            EditorGUILayout.BeginHorizontal();

            // 计算控件矩形
            Rect r = EditorGUILayout.GetControlRect(false, 18, GUILayout.ExpandWidth(true));
            // 右边留出 Browse (圆点) 和 Clear (X) 的位置
            float pickerWidth = 20f;
            float clearWidth = 20f;
            Rect fieldRect = new Rect(r.x, r.y, r.width - pickerWidth - (string.IsNullOrEmpty(path) ? 0 : clearWidth), r.height);
            Rect pickerRect = new Rect(r.x + r.width - pickerWidth - (string.IsNullOrEmpty(path) ? 0 : clearWidth), r.y, pickerWidth, r.height);
            Rect clearRect = new Rect(r.x + r.width - clearWidth, r.y, clearWidth, r.height);

            // --- 1. 绘制主体样式 (仿 ObjectField) ---
            GUIContent content;
            Texture icon = EditorGUIUtility.IconContent("TextAsset Icon").image; // 使用文本资源图标，或者根据后缀判断

            if (string.IsNullOrEmpty(path))
            {
                content = new GUIContent($"None ({extension})");
            }
            else
            {
                content = new GUIContent(Path.GetFileName(path), icon);
            }

            // 使用 ObjectField 样式绘制背景
            UnityEngine.GUI.Box(fieldRect, GUIContent.none, EditorStyles.objectField);

            // 绘制图标和文字
            Rect iconRect = new Rect(fieldRect.x + 2, fieldRect.y + 1, 16, 16);
            Rect textRect = new Rect(fieldRect.x + 20, fieldRect.y, fieldRect.width - 20, fieldRect.height);

            if (icon != null) UnityEngine.GUI.DrawTexture(iconRect, icon);
            UnityEngine.GUI.Label(textRect, content, EditorStyles.label);

            // --- 2. 处理拖拽逻辑 ---
            Event evt = Event.current;
            if (fieldRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (string draggedPath in DragAndDrop.paths)
                        {
                            if (draggedPath.EndsWith($".{extension}", System.StringComparison.OrdinalIgnoreCase))
                            {
                                resultPath = draggedPath;
                                break; // 只取第一个匹配的
                            }
                        }
                        evt.Use();
                    }
                }
            }

            if (UnityEngine.GUI.Button(pickerRect, EditorGUIUtility.IconContent("Folder Icon"), GUIStyle.none))
            {
                string selected = EditorUtility.OpenFilePanel($"Select {extension} file", "Assets", extension);
                if (!string.IsNullOrEmpty(selected))
                {
                    // 转换为相对路径（如果是项目内）
                    if (selected.StartsWith(Application.dataPath))
                    {
                        selected = Path.Combine("Assets", selected[(Application.dataPath.Length)..]);
                    }
                    resultPath = selected;
                }
            }

            // --- 4. 绘制清除/删除按钮 ---
            if (!string.IsNullOrEmpty(path))
            {
                // 使用 X 号或者 "-" 号
                if (UnityEngine.GUI.Button(clearRect, "×", EditorStyles.miniButton))
                {
                    resultPath = ""; // 返回空字符串，上层逻辑会处理移除
                }
            }

            EditorGUILayout.EndHorizontal();

            // 加一点间距
            GUILayout.Space(2);

            return resultPath;
        }
    }
}