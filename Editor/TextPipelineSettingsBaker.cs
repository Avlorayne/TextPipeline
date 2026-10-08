using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TextPipeline.Editor
{
    [InitializeOnLoad]
    public sealed class TextPipelineSettingsBaker : IPreprocessBuildWithReport
    {
        public const string DefaultAssetPath = "Assets/Resources/" + TextPipelineSettings.ResourceName + ".asset";

        static TextPipelineSettingsBaker()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report) => Bake();

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try
            {
                Bake();
            }
            catch (Exception exception)
            {
                EditorApplication.isPlaying = false;
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Text Pipeline/Bake Runtime Settings")]
        public static void BakeFromMenu() => Bake();

        public static TextPipelineSettings Bake()
        {
            var settings = TextPipelineProjectSettings.instance;
            settings.EnsureInitialized();
            settings.SaveSettings();

            var path = FindRuntimeAssetPath() ?? DefaultAssetPath;
            var baked = AssetDatabase.LoadAssetAtPath<TextPipelineSettings>(path);
            if (baked == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new BuildFailedException($"Text Pipeline 烘焙路径已被其他资产占用：{path}");
                EnsureFolder("Assets/Resources");
                baked = ScriptableObject.CreateInstance<TextPipelineSettings>();
                AssetDatabase.CreateAsset(baked, path);
            }

            settings.CopyTo(baked);
            EditorUtility.SetDirty(baked);
            AssetDatabase.SaveAssetIfDirty(baked);
            return baked;
        }

        internal static string FindRuntimeAssetPath()
        {
            var suffix = "/Resources/" + TextPipelineSettings.ResourceName + ".asset";
            var paths = AssetDatabase.FindAssets("t:TextPipelineSettings")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(suffix, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length > 1)
                throw new BuildFailedException("Text Pipeline 存在多个同名 Resources 配置，请保留一个后重试：\n" +
                    string.Join("\n", paths));
            // Reuse legacy assets in place, preserving references and avoiding duplicate Resources keys.
            return paths.FirstOrDefault();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var separator = path.LastIndexOf('/');
            var parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
