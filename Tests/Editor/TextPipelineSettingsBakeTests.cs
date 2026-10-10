using System;
using System.IO;
using NUnit.Framework;
using TextPipeline.Postprocess;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TextPipeline.Editor.Tests
{
    public sealed class TextPipelineSettingsBakeTests
    {
        [Markup("settings-first")]
        public sealed class FirstSink : ITextSink
        {
            public ITextPipeline TextPipeline { get; set; }
            public void PostProcess(TextSegment[] segments) { }
        }

        [Markup("settings-second")]
        public sealed class SecondSink : ITextSink
        {
            public ITextPipeline TextPipeline { get; set; }
            public void PostProcess(TextSegment[] segments) { }
        }

        public sealed class FirstSource : ITextSource
        {
            public ITextPipeline TextPipeline { private get; set; }
            public string PreProcess(string text) => text;
        }

        public sealed class SecondSource : ITextSource
        {
            public ITextPipeline TextPipeline { private get; set; }
            public string PreProcess(string text) => text;
        }

        private TextPipelineProjectSettings settings;
        private string settingsJson;
        private byte[] settingsFile;
        private TextPipelineSettings originalAsset;
        private string originalAssetJson;
        private string bakedPath;
        private const string DuplicateFolder = "Assets/TextPipelineSettingsBakeTests";

        [SetUp]
        public void SetUp()
        {
            settings = TextPipelineProjectSettings.instance;
            settingsJson = EditorJsonUtility.ToJson(settings);
            settingsFile = File.Exists(TextPipelineProjectSettings.SettingsPath)
                ? File.ReadAllBytes(TextPipelineProjectSettings.SettingsPath) : null;
            originalAsset = Resources.Load<TextPipelineSettings>(TextPipelineSettings.ResourceName);
            if (originalAsset != null) originalAssetJson = EditorJsonUtility.ToJson(originalAsset);
            bakedPath = AssetDatabase.GetAssetPath(TextPipelineSettingsBaker.Bake());
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(DuplicateFolder);
            if (originalAsset == null) AssetDatabase.DeleteAsset(bakedPath);
            else
            {
                EditorJsonUtility.FromJsonOverwrite(originalAssetJson, originalAsset);
                EditorUtility.SetDirty(originalAsset);
                AssetDatabase.SaveAssetIfDirty(originalAsset);
            }
            EditorJsonUtility.FromJsonOverwrite(settingsJson, settings);
            if (settingsFile == null) File.Delete(TextPipelineProjectSettings.SettingsPath);
            else File.WriteAllBytes(TextPipelineProjectSettings.SettingsPath, settingsFile);
        }

        [Test]
        public void Bake_ResourceContainsConfiguredOrder_AndPreservesGuid()
        {
            Configure(typeof(SecondSink), typeof(FirstSink));
            ConfigureSources(typeof(SecondSource), typeof(FirstSource));
            var guid = AssetDatabase.AssetPathToGUID(bakedPath);
            var baked = TextPipelineSettingsBaker.Bake();
            Assert.That(Resources.Load<TextPipelineSettings>(TextPipelineSettings.ResourceName), Is.SameAs(baked));
            Assert.That(baked.TextSinks[typeof(SecondSink)], Is.EqualTo(0));
            Assert.That(baked.TextSinks[typeof(FirstSink)], Is.EqualTo(1));
            Assert.That(baked.TextSources[typeof(SecondSource)], Is.EqualTo(0));
            Assert.That(baked.TextSources[typeof(FirstSource)], Is.EqualTo(1));
            TextPipelineSettingsBaker.Bake();
            Assert.That(AssetDatabase.AssetPathToGUID(bakedPath), Is.EqualTo(guid));
        }

        [Test]
        public void Bake_AfterOrderChange_InvalidatesPreviouslyReadCache()
        {
            Configure(typeof(FirstSink), typeof(SecondSink));
            ConfigureSources(typeof(FirstSource), typeof(SecondSource));
            var baked = TextPipelineSettingsBaker.Bake();
            Assert.That(baked.TextSinks[typeof(FirstSink)], Is.EqualTo(0));
            Assert.That(baked.TextSources[typeof(FirstSource)], Is.EqualTo(0));
            Configure(typeof(SecondSink), typeof(FirstSink));
            ConfigureSources(typeof(SecondSource), typeof(FirstSource));
            TextPipelineSettingsBaker.Bake();
            Assert.That(baked.TextSinks[typeof(FirstSink)], Is.EqualTo(1));
            Assert.That(baked.TextSinks[typeof(SecondSink)], Is.EqualTo(0));
            Assert.That(baked.TextSources[typeof(FirstSource)], Is.EqualTo(1));
        }

        [Test]
        public void BuildPreprocessor_BakesLatestProjectSettings()
        {
            Configure(typeof(SecondSink));
            new TextPipelineSettingsBaker().OnPreprocessBuild(null);
            var baked = Resources.Load<TextPipelineSettings>(TextPipelineSettings.ResourceName);
            Assert.That(baked.TextSinks.Count, Is.EqualTo(1));
            Assert.That(baked.TextSinks[typeof(SecondSink)], Is.EqualTo(0));
            StringAssert.Contains(typeof(SecondSink).FullName,
                File.ReadAllText(TextPipelineProjectSettings.SettingsPath));
        }

        [Test]
        public void FirstBake_MigratesLegacyResourceOrder()
        {
            Configure(typeof(SecondSink), typeof(FirstSink));
            TextPipelineSettingsBaker.Bake();
            Configure();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("initialized").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var baked = TextPipelineSettingsBaker.Bake();
            Assert.That(baked.TextSinks[typeof(SecondSink)], Is.EqualTo(0));
            Assert.That(baked.TextSinks[typeof(FirstSink)], Is.EqualTo(1));
        }

        [Test]
        public void Import_UndoAndRedoPersistWithoutAnOpenSettingsEditor()
        {
            Configure(typeof(FirstSink));
            var imported = ScriptableObject.CreateInstance<TextPipelineSettings>();
            try
            {
                var serialized = new SerializedObject(imported);
                var names = serialized.FindProperty("sinkSequenceTypeNames");
                names.arraySize = 1;
                names.GetArrayElementAtIndex(0).stringValue = typeof(SecondSink).AssemblyQualifiedName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                settings.ImportFrom(imported);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                StringAssert.Contains(typeof(FirstSink).FullName,
                    File.ReadAllText(TextPipelineProjectSettings.SettingsPath));
                Undo.PerformRedo();
                StringAssert.Contains(typeof(SecondSink).FullName,
                    File.ReadAllText(TextPipelineProjectSettings.SettingsPath));
            }
            finally { UnityEngine.Object.DestroyImmediate(imported); }
        }

        [Test]
        public void Bake_DuplicateResourceKeys_FailsBeforeOverwriting()
        {
            Configure(typeof(FirstSink));
            var baked = TextPipelineSettingsBaker.Bake();
            var before = EditorJsonUtility.ToJson(baked);
            AssetDatabase.CreateFolder("Assets", "TextPipelineSettingsBakeTests");
            AssetDatabase.CreateFolder(DuplicateFolder, "Resources");
            var duplicate = ScriptableObject.CreateInstance<TextPipelineSettings>();
            AssetDatabase.CreateAsset(duplicate, DuplicateFolder + "/Resources/" + TextPipelineSettings.ResourceName + ".asset");
            Configure(typeof(SecondSink));
            Assert.Throws<BuildFailedException>(() => TextPipelineSettingsBaker.Bake());
            Assert.That(EditorJsonUtility.ToJson(baked), Is.EqualTo(before));
        }

        private void Configure(params Type[] sinks)
        {
            var serialized = new SerializedObject(settings);
            var names = serialized.FindProperty("sinkSequenceTypeNames");
            names.arraySize = sinks.Length;
            for (var i = 0; i < sinks.Length; i++)
                names.GetArrayElementAtIndex(i).stringValue = sinks[i].AssemblyQualifiedName;
            serialized.FindProperty("sourceSequenceTypeNames").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            settings.SaveSettings();
        }

        private void ConfigureSources(params Type[] sources)
        {
            var serialized = new SerializedObject(settings);
            var names = serialized.FindProperty("sourceSequenceTypeNames");
            names.arraySize = sources.Length;
            for (var i = 0; i < sources.Length; i++)
                names.GetArrayElementAtIndex(i).stringValue = sources[i].AssemblyQualifiedName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            settings.SaveSettings();
        }
    }
}
