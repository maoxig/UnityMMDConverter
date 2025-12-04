using System.Collections.Generic;
using System.IO;
using System.Linq; // 引入 Linq 以便简便地检查多个后缀
using UnityEditor;
using UnityEngine;

namespace UnityMMDConverter.CustomGUI
{
    public static class MMDCustomGUI
    {
        /// <summary>
        /// 绘制文件列表选择器（统一写法：Label 在前，后缀在后，支持无限个后缀）
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        /// <param name="label">标题</param>
        /// <param name="extensions">允许的文件后缀 (例如 "pmx", "pmd")</param>
        public static void DrawFileSelectorList(List<string> filePaths, string label, params string[] extensions)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            // 1. 绘制已存在的文件列表
            for (int i = 0; i < filePaths.Count; i++)
            {
                string path = filePaths[i];
                // 调用核心逻辑，传入后缀数组
                string newPath = DrawSmartObjectField(path, false, extensions);

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
            string addedPath = DrawSmartObjectField(null, true, extensions);
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
        /// 绘制单个文件选择器（统一写法：Label 在前，后缀在后，支持无限个后缀）
        /// </summary>
        /// <param name="currentPath">当前路径</param>
        /// <param name="label">标题</param>
        /// <param name="extensions">允许的文件后缀 (例如 "vmd")</param>
        public static string DrawSingleFileSelector(string currentPath, string label, params string[] extensions)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            string result = DrawSmartObjectField(currentPath, true, extensions);
            EditorGUILayout.EndHorizontal();
            return result;
        }

        /// <summary>
        /// 核心绘制逻辑：模拟 ObjectField (已升级支持多后缀)
        /// </summary>
        /// <param name="path">当前路径</param>
        /// <param name="allowEmpty">是否允许为空（用于空槽位样式）</param>
        /// <param name="extensions">后缀限制列表</param>
        /// <returns>新的路径</returns>
        private static string DrawSmartObjectField(string path, bool allowEmpty, params string[] extensions)
        {
            string resultPath = path;

            // 默认安全检查：如果没有传后缀，视为允许所有文件
            if (extensions == null || extensions.Length == 0) extensions = new string[] { "*" };

            EditorGUILayout.BeginHorizontal();

            // 计算控件矩形 (逻辑保持原样)
            Rect r = EditorGUILayout.GetControlRect(false, 18, GUILayout.ExpandWidth(true));
            float pickerWidth = 20f;
            float clearWidth = 20f;

            // 根据是否有内容决定是否留出"X"按钮的宽度
            float rightSpace = pickerWidth + (string.IsNullOrEmpty(path) ? 0 : clearWidth);

            Rect fieldRect = new Rect(r.x, r.y, r.width - rightSpace, r.height);
            Rect pickerRect = new Rect(r.x + r.width - rightSpace, r.y, pickerWidth, r.height);
            Rect clearRect = new Rect(r.x + r.width - clearWidth, r.y, clearWidth, r.height);

            // --- 1. 绘制主体样式 (仿 ObjectField) ---
            GUIContent content;
            Texture icon = EditorGUIUtility.IconContent("TextAsset Icon").image;

            if (string.IsNullOrEmpty(path))
            {
                // 空状态显示支持的格式，例如: "None (pmx, pmd)"
                content = new GUIContent($"None ({string.Join(", ", extensions)})");
            }
            else
            {
                content = new GUIContent(Path.GetFileName(path), icon);
            }

            UnityEngine.GUI.Box(fieldRect, GUIContent.none, EditorStyles.objectField);

            Rect iconRect = new Rect(fieldRect.x + 2, fieldRect.y + 1, 16, 16);
            Rect textRect = new Rect(fieldRect.x + 20, fieldRect.y, fieldRect.width - 20, fieldRect.height);

            if (icon != null) UnityEngine.GUI.DrawTexture(iconRect, icon);
            UnityEngine.GUI.Label(textRect, content, EditorStyles.label);

            // --- 2. 处理拖拽逻辑 (支持多后缀检测) ---
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
                            // 只要满足任意一个后缀即可
                            bool isMatch = extensions.Any(ext => draggedPath.EndsWith($".{ext}", System.StringComparison.OrdinalIgnoreCase));

                            if (isMatch)
                            {
                                resultPath = draggedPath;
                                break; // 只取第一个匹配的
                            }
                        }
                        evt.Use();
                    }
                }
            }

            // --- 3. 文件选择弹窗 (支持多后缀过滤) ---
            if (UnityEngine.GUI.Button(pickerRect, EditorGUIUtility.IconContent("Folder Icon"), GUIStyle.none))
            {
                // OpenFilePanel 支持逗号分隔的扩展名，例如 "pmx,pmd"
                string filter = string.Join(",", extensions);
                string selected = EditorUtility.OpenFilePanel("Select file", "Assets", filter);

                if (!string.IsNullOrEmpty(selected))
                {
                    // 转换为相对路径逻辑 (保持原样)
                    if (selected.StartsWith(Application.dataPath))
                    {
                        string relative = selected.Substring(Application.dataPath.Length);
                        // 处理斜杠兼容性
                        if (relative.StartsWith("/") || relative.StartsWith("\\"))
                            relative = relative.Substring(1);

                        resultPath = Path.Combine("Assets", relative);
                    }
                    else
                    {
                        resultPath = selected;
                    }
                }
            }

            // --- 4. 绘制清除/删除按钮 ---
            if (!string.IsNullOrEmpty(path))
            {
                if (UnityEngine.GUI.Button(clearRect, "×", EditorStyles.miniButton))
                {
                    resultPath = "";
                }
            }

            EditorGUILayout.EndHorizontal();

            // 保持原有的间距
            GUILayout.Space(2);

            return resultPath;
        }
    }
}