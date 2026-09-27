using UnityEditor;
using UnityEngine;

namespace TextPipeline.Editor
{
    [CustomPropertyDrawer(typeof(InspectorReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 返回该属性在 Inspector 中应有的高度
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 核心：禁用 GUI 的交互功能，使其变为只读
            GUI.enabled = false;
            // 绘制该属性字段
            EditorGUI.PropertyField(position, property, label, true);
            // 恢复 GUI 状态，避免影响后续绘制的其他属性
            GUI.enabled = true;
        }
    }
}
