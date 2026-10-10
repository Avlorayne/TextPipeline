using System;
using TextPipeline.Editor;
using UnityEditor;

namespace TextPipeline.Samples.Editor
{
    /// <summary>Register demo sources on the live settings singleton before Play.</summary>
    [InitializeOnLoad]
    public static class EffectsDemoSettingsSetup
    {
        static EffectsDemoSettingsSetup()
        {
            EditorApplication.delayCall += ConfigureAfterImport;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void ConfigureAfterImport()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Configure();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                Configure();
        }

        [MenuItem("Tools/Text Pipeline/Configure Effects Demo Sources")]
        public static void Configure()
        {
            // Migrate legacy resources before editing the project singleton.
            TextPipelineSettingsBaker.Bake();
            var settings = TextPipelineProjectSettings.instance;
            var serialized = new SerializedObject(settings);
            var sources = serialized.FindProperty("sourceSequenceTypeNames");
            var changed = false;
            if (FindSource(sources, typeof(DemoVariablesSource)) < 0)
            {
                var markupIndex = FindSource(sources, typeof(DemoMarkupSource));
                InsertSource(sources, markupIndex < 0 ? sources.arraySize : markupIndex,
                    typeof(DemoVariablesSource));
                changed = true;
            }
            if (FindSource(sources, typeof(DemoMarkupSource)) < 0)
            {
                InsertSource(sources, sources.arraySize, typeof(DemoMarkupSource));
                changed = true;
            }
            if (!changed) return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            settings.SaveSettings();
            TextPipelineSettingsBaker.Bake();
        }

        private static int FindSource(SerializedProperty sources, Type type)
        {
            for (var i = 0; i < sources.arraySize; i++)
                if (Type.GetType(sources.GetArrayElementAtIndex(i).stringValue) == type)
                    return i;
            return -1;
        }

        private static void InsertSource(SerializedProperty sources, int index, Type type)
        {
            sources.arraySize++;
            for (var i = sources.arraySize - 1; i > index; i--)
                sources.GetArrayElementAtIndex(i).stringValue = sources.GetArrayElementAtIndex(i - 1).stringValue;
            sources.GetArrayElementAtIndex(index).stringValue = type.AssemblyQualifiedName;
        }
    }
}
