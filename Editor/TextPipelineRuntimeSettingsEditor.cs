using UnityEditor;
using UnityEngine;

namespace TextPipeline.Editor
{
    [CustomEditor(typeof(TextPipelineSettings))]
    public sealed class TextPipelineRuntimeSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("此 SO 是运行时配置。请在 Project Settings > Text Pipeline 编辑；进入 Play Mode 或构建前会自动烘焙。", MessageType.Info);
            using (new EditorGUI.DisabledScope(true)) DrawDefaultInspector();
            if (GUILayout.Button("打开 Project Settings"))
                SettingsService.OpenProjectSettings("Project/Text Pipeline");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("将此配置导入 Project Settings"))
                {
                    TextPipelineProjectSettings.instance.ImportFrom((TextPipelineSettings)target);
                    SettingsService.OpenProjectSettings("Project/Text Pipeline");
                }
            }
        }
    }
}
