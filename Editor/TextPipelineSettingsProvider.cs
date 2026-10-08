using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextPipeline.Editor
{
    public sealed class TextPipelineSettingsProvider : SettingsProvider
    {
        private UnityEditor.Editor settingsEditor;

        private TextPipelineSettingsProvider() : base("Project/Text Pipeline", SettingsScope.Project)
        {
            keywords = new HashSet<string> { "Text", "Pipeline", "Sink", "Source", "Sequence", "文本", "执行顺序" };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider() => new TextPipelineSettingsProvider();

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            var settings = TextPipelineProjectSettings.instance;
            settings.EnsureInitialized();
            settingsEditor = UnityEditor.Editor.CreateEditor(settings);
        }

        public override void OnGUI(string searchContext)
        {
            if (settingsEditor == null) return;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                settingsEditor.OnInspectorGUI();
                if (GUILayout.Button("烘焙运行时配置")) TextPipelineSettingsBaker.Bake();
            }
        }

        public override void OnDeactivate()
        {
            Object.DestroyImmediate(settingsEditor);
            settingsEditor = null;
        }
    }
}
