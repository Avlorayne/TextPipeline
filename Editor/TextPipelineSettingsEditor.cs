#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TextPipeline.Editor
{
    [CustomEditor(typeof(TextPipelineSettings))]
    public class TextPipelineSettingsEditor : UnityEditor.Editor
    {
        private ReorderableList _sinkList;
        private ReorderableList _sourceList;

        private List<Type> _sinkOptions;
        private List<Type> _sourceOptions;

        private SerializedProperty _sinkProp;
        private SerializedProperty _sourceProp;

        private void OnEnable()
        {
            // 1. 查找属性
            _sinkProp = serializedObject.FindProperty("sinkSequenceTypeNames");
            _sourceProp = serializedObject.FindProperty("sourceSequenceTypeNames");

            // 2. 初始化 Sink 列表
            if (_sinkProp != null)
            {
                var baseSinkType = typeof(ITextSinkBase);
                _sinkOptions = TypeFinder.FindDerivedTypes<ITextSinkBase>()
                    .Where(t => t != baseSinkType && t.IsDefined(typeof(MarkupAttribute), inherit: false))
                    .ToList();
                _sinkList = CreateReorderableList(_sinkProp, _sinkOptions, "Sink Sequence (执行顺序: 上→下)");
            }
            else
            {
                Debug.LogError(
                    "[TextPipelineSettingsEditor] 未找到 sinkSequenceTypeNames 字段，请检查 TextPipelineSettings.cs 是否包含该字段且类型为 List<string>。");
            }

            // 3. 初始化 Source 列表
            if (_sourceProp != null)
            {
                var baseSourceType = typeof(ITextSource);
                _sourceOptions = TypeFinder.FindDerivedTypes<ITextSource>()
                    .Where(t => t != baseSourceType) // 过滤：不要接口本身
                    .ToList();
                _sourceList = CreateReorderableList(_sourceProp, _sourceOptions, "Source Sequence (执行顺序: 上→下)");
            }
            else
            {
                Debug.LogError(
                    "[TextPipelineSettingsEditor] 未找到 sourceSequenceTypeNames 字段，请检查 TextPipelineSettings.cs 是否包含该字段且类型为 List<string>。");
            }
        }

        private ReorderableList CreateReorderableList(SerializedProperty prop, List<Type> options, string headerTitle)
        {
            var list = new ReorderableList(serializedObject, prop, draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true)
            {
                drawHeaderCallback = (rect) =>
                {
                    EditorGUI.LabelField(rect, $"{headerTitle}  [{prop.arraySize}]", EditorStyles.boldLabel);
                },

                // 绘制每一个元素
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = prop.GetArrayElementAtIndex(index);
                    var typeName = element.stringValue;
                    var type = Type.GetType(typeName);

                    var label = type != null ? Nicify(type) : $"Missing: {typeName}";

                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    // 【修改处】创建一个新样式并启用 richText
                    var style = new GUIStyle(EditorStyles.popup) { richText = true };

                    if (GUI.Button(rect, label, style))
                    {
                        ShowTypeMenu(options, prop, index);
                    }
                },


                // 点击 + 按钮
                onAddCallback = (list) =>
                {
                    // 弹出菜单选择要添加的类型 (index = -1 表示追加到末尾)
                    ShowTypeMenu(options, prop, -1);
                },

                // 点击 - 按钮
                onRemoveCallback = (list) =>
                {
                    prop.DeleteArrayElementAtIndex(list.index);
                    serializedObject.ApplyModifiedProperties();
                }
            };
            return list;
        }

        // 显示下拉菜单
        private void ShowTypeMenu(List<Type> options, SerializedProperty prop, int index)
        {
            var menu = new GenericMenu();

            // 收集已存在的类型，用于置灰
            var existingNames = new HashSet<string>();
            for (int i = 0; i < prop.arraySize; i++)
            {
                existingNames.Add(prop.GetArrayElementAtIndex(i).stringValue);
            }

            if (options.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("未找到任何实现类"));
                menu.ShowAsContext();
                return;
            }

            // 按 FullName 排序，确保同类都在一块
            foreach (var t in options.OrderBy(t => t.FullName))
            {
                var qualifiedName = t.AssemblyQualifiedName;
                var isAdded = existingNames.Contains(qualifiedName);

                // 使用 Nicify 格式化显示名称 (命名空间作为后缀)
                var niceName = ObjectNames.NicifyVariableName(t.Name);
                // 如果想在菜单中也显示命名空间（不带颜色），可以像下面这样：
                // if (!string.IsNullOrEmpty(t.Namespace)) {
                //     niceName += $" ({t.Namespace})";
                // }
                var content = new GUIContent(isAdded ? $"{niceName} (已添加)" : niceName);
                if (isAdded)
                {
                    menu.AddDisabledItem(content);
                }
                else
                {
                    menu.AddItem(content, false, () => OnTypeSelected(prop, index, qualifiedName));
                }
            }

            menu.ShowAsContext();
        }

        // 菜单选中回调
        private void OnTypeSelected(SerializedProperty prop, int index, string typeName)
        {
            serializedObject.Update(); // 必须先更新

            if (index < 0)
            {
                // 追加模式
                prop.arraySize++;
                prop.GetArrayElementAtIndex(prop.arraySize - 1).stringValue = typeName;
            }
            else
            {
                // 替换模式
                prop.GetArrayElementAtIndex(index).stringValue = typeName;
            }

            serializedObject.ApplyModifiedProperties(); // 应用修改
            EditorUtility.SetDirty(target); // 标记资源脏数据
        }

        // 格式化类型名称显示：命名空间作为后缀
        private string Nicify(Type t)
        {
            var name = ObjectNames.NicifyVariableName(t.Name);
            if (!string.IsNullOrEmpty(t.Namespace))
            {
                // 根据编辑器主题选择合适的灰色
                var color = EditorGUIUtility.isProSkin ? "#888888" : "#555555";
                return $"{name} <color={color}>({t.Namespace})</color>";
            }

            return name;
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox("配置文本管线的处理顺序。上方的组件优先执行。", MessageType.Info);

            if (_sinkList != null) _sinkList.DoLayoutList();
            if (_sourceList != null) _sourceList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
