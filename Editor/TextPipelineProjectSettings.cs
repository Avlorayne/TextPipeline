using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextPipeline.Editor
{
    [FilePath(SettingsPath, FilePathAttribute.Location.ProjectFolder)]
    public sealed class TextPipelineProjectSettings : ScriptableSingleton<TextPipelineProjectSettings>
    {
        public const string SettingsPath = "ProjectSettings/TextPipelineSettings.asset";

        [SerializeField] private List<string> sinkSequenceTypeNames = new();
        [SerializeField] private List<string> sourceSequenceTypeNames = new();
        [SerializeField, HideInInspector] private bool initialized;

        static TextPipelineProjectSettings()
        {
            // Persist Undo/Redo even after the Project Settings page has been closed.
            Undo.undoRedoPerformed += () =>
            {
                if (instance.initialized) instance.SaveSettings();
            };
        }

        public void SaveSettings() => Save(true);

        internal void EnsureInitialized()
        {
            if (initialized) return;

            // Only migrate on first use; subsequent bakes always use Project Settings.
            var legacyPath = TextPipelineSettingsBaker.FindRuntimeAssetPath();
            if (legacyPath != null)
                CopySequences(new SerializedObject(AssetDatabase.LoadAssetAtPath<TextPipelineSettings>(legacyPath)),
                    new SerializedObject(this));

            initialized = true;
            SaveSettings();
        }

        public void ImportFrom(TextPipelineSettings settings)
        {
            if (settings == null) throw new System.ArgumentNullException(nameof(settings));
            Undo.RecordObject(this, "Import Text Pipeline Settings");
            CopySequences(new SerializedObject(settings), new SerializedObject(this));
            initialized = true;
            SaveSettings();
        }

        internal void CopyTo(TextPipelineSettings settings)
        {
            CopySequences(new SerializedObject(this), new SerializedObject(settings));
        }

        private static void CopySequences(SerializedObject source, SerializedObject destination)
        {
            foreach (var field in new[] { "sinkSequenceTypeNames", "sourceSequenceTypeNames" })
            {
                var from = source.FindProperty(field);
                var to = destination.FindProperty(field);
                to.arraySize = from.arraySize;
                for (var i = 0; i < from.arraySize; i++)
                    to.GetArrayElementAtIndex(i).stringValue = from.GetArrayElementAtIndex(i).stringValue;
            }
            destination.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
