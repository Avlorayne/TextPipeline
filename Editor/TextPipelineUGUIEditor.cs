using System;
using System.Linq;
using TextPipeline.Postprocess;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

namespace TextPipeline.Editor
{
    [CustomEditor(typeof(TextPipelineUGUI), true), CanEditMultipleObjects]
    public class TextPipelineUGUIEditor : TMP_EditorPanelUI
    {
        [InitializeOnLoadMethod]
        private static void RegisterEditorMarkups()
        {
            if (Application.isPlaying) return;
            string runtimeAssembly = typeof(ITextSinkBase).Assembly.GetName().Name;
            MarkupRegistry.RegisterMarkups(AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
                assembly.GetName().Name == "Assembly-CSharp" || assembly.GetName().Name == runtimeAssembly));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Reuse TMP's text input UI, but author the original rather than the rendered string.
            m_TextProp = serializedObject.FindProperty("originalText");
            Undo.undoRedoPerformed += RefreshPreview;
        }

        protected override void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshPreview;
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            var pipelines = targets.Cast<TextPipelineUGUI>().ToArray();
            var previousTexts = pipelines.Select(pipeline => pipeline.OriginalText).ToArray();
            base.OnInspectorGUI();

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("processTextOnEnable"));
            serializedObject.ApplyModifiedProperties();

            for (int index = 0; index < pipelines.Length; index++)
            {
                if (pipelines[index].OriginalText != previousTexts[index])
                    UpdatePreview(pipelines[index]);
            }

            using (new EditorGUI.DisabledScope(true))
            {
                if (pipelines.Length == 1)
                {
                    EditorGUILayout.LabelField("显示文本预览");
                    EditorGUILayout.TextArea(pipelines[0].text ?? string.Empty, GUILayout.MinHeight(40));
                }

                EditorGUILayout.PropertyField(serializedObject.FindProperty("temporaryTexts"), true);
            }

            if (GUILayout.Button("Refresh"))
                RefreshPreview();
        }

        private void RefreshPreview()
        {
            foreach (var targetObject in targets)
            {
                if (targetObject is TextPipelineUGUI pipeline && pipeline != null)
                    UpdatePreview(pipeline);
            }
        }

        private static void UpdatePreview(TextPipelineUGUI pipeline)
        {
            pipeline.text = pipeline.OriginalText;
            EditorUtility.SetDirty(pipeline);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pipeline);
        }
    }
}
