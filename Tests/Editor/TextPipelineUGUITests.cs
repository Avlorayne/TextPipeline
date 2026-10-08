using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TextPipeline.Postprocess;
using TMPro;
using UnityEngine;

namespace TextPipeline.Editor.Tests
{
    public sealed class TextPipelineUGUITests
    {
        [Markup("ugi")]
        public sealed class ProbeSink : ITextSink
        {
            public static readonly List<(ProbeSink sink, string label, string text)> Runs = new();
            public static Action<ITextPipeline> OnRun;

            [MarkupProperty("label", MarkupDataType.String)]
            public string Label { get; set; } = "default";

            public ITextPipeline TextPipeline { get; set; }

            public void PostProcess(TextSegment[] segments)
            {
                foreach (var segment in segments)
                    segment.DoEffect(this, range =>
                    {
                        var characters = new List<char>();
                        foreach (var character in range)
                            characters.Add(character.character);
                        Runs.Add((this, Label, new string(characters.ToArray())));
                    });
                OnRun?.Invoke(TextPipeline);
            }
        }

        public sealed class PrefixSource : MonoBehaviour, ITextSource
        {
            public ITextPipeline Pipeline { get; private set; }
            public string Prefix;

            ITextPipeline ITextSource.TextPipeline
            {
                set => Pipeline = value;
            }

            public string PreProcess(string input) => Prefix + input;
        }

        private static readonly FieldInfo SettingsField = typeof(TextPipelineSettings)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private TextPipelineSettings _previousSettings;
        private TextPipelineSettings _settings;
        private GameObject _object;

        [SetUp]
        public void SetUp()
        {
            _previousSettings = (TextPipelineSettings)SettingsField.GetValue(null);
            _settings = ScriptableObject.CreateInstance<TextPipelineSettings>();
            ((List<string>)typeof(TextPipelineSettings)
                .GetField("sinkSequenceTypeNames", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_settings)).Add(typeof(ProbeSink).AssemblyQualifiedName);
            ((List<string>)typeof(TextPipelineSettings)
                .GetField("sourceSequenceTypeNames", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_settings)).Add(typeof(PrefixSource).AssemblyQualifiedName);
            SettingsField.SetValue(null, _settings);
            MarkupRegistry.RegisterMarkups(new[] { typeof(ProbeSink).Assembly });
            ProbeSink.Runs.Clear();
            ProbeSink.OnRun = null;
        }

        [TearDown]
        public void TearDown()
        {
            ProbeSink.OnRun = null;
            if (_object != null) UnityEngine.Object.DestroyImmediate(_object);
            SettingsField.SetValue(null, _previousSettings);
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Setter_ThroughTmpReference_ProcessesRichTextWithoutAnExtraComponent()
        {
            var pipeline = CreatePipeline();
            TMP_Text label = pipeline;
            const string input = "<ugi : label = \"rich\"><b>A</b></ugi>";
            label.text = input;

            Assert.That(pipeline.OriginalText, Is.EqualTo(input));
            Assert.That(label.text, Is.EqualTo("<b>A</b>"));
            Assert.That(label.textInfo.characterCount, Is.EqualTo(1));
            Assert.That(ProbeSink.Runs.Select(run => (run.label, run.text)), Is.EqualTo(new[] { ("rich", "A") }));
            Assert.That(ProbeSink.Runs[0].sink.TextPipeline, Is.SameAs(pipeline));
            Assert.That(_object.GetComponent<TextPipeline>(), Is.Null);
            Assert.That(_object.GetComponents<TMP_Text>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void Setter_PreservesTmpInputStateBeforeActivation_AndProcessesOnlyLatestRequest()
        {
            var pipeline = CreatePipeline(active: false);
            TMP_Text label = pipeline;
            label.SetText(new[] { 'X' });
            label.havePropertiesChanged = false;
            label.text = "<ugi>A</ugi>";
            label.text = "<ugi>B</ugi>";

            Assert.That(label.text, Is.EqualTo("<ugi>B</ugi>"));
            Assert.That(label.havePropertiesChanged, Is.True);
            Assert.That(typeof(TMP_Text).GetField("m_inputSource", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(label).ToString(), Is.EqualTo("TextString"));
            Assert.That(ProbeSink.Runs, Is.Empty);

            _object.SetActive(true);
            Assert.That(label.text, Is.EqualTo("B"));
            Assert.That(ProbeSink.Runs.Select(run => run.text), Is.EqualTo(new[] { "B" }));
            InvokeLateUpdate(pipeline);
            Assert.That(ProbeSink.Runs, Has.Count.EqualTo(1));
        }

        [Test]
        public void Setter_RepeatedInput_ReusesDefaultScopeAndRebuildsEffects()
        {
            var pipeline = CreatePipeline();
            pipeline.text = "<ugi : label = \"first\">A</ugi>";
            var sink = ProbeSink.Runs[0].sink;
            pipeline.text = "<ugi : label = \"second\">A</ugi>";
            pipeline.text = "<ugi : label = \"second\">A</ugi>";
            Assert.That(ProbeSink.Runs.Select(run => run.label), Is.EqualTo(new[] { "first", "second", "second" }));
            Assert.That(ProbeSink.Runs.All(run => ReferenceEquals(run.sink, sink)), Is.True);
        }

        [Test]
        public void SourceAndRefresh_UseMotherText_AndHonourProcessingFlags()
        {
            var pipeline = CreatePipeline(active: false);
            var source = _object.AddComponent<PrefixSource>();
            source.Prefix = "<ugi : label = \"source\">";
            _object.SetActive(true);
            pipeline.text = "S</ugi>";
            Assert.That(source.Pipeline, Is.SameAs(pipeline));
            Assert.That(pipeline.text, Is.EqualTo("S"));
            pipeline.Refresh();
            Assert.That(ProbeSink.Runs.Select(run => run.text), Is.EqualTo(new[] { "S", "S" }));
            pipeline.SetOriginalText("unprocessed", false, false);
            Assert.That(pipeline.text, Is.EqualTo("unprocessed"));
            Assert.That(ProbeSink.Runs, Has.Count.EqualTo(2));
        }

        [Test]
        public void DisabledSetter_IsAppliedOnEnableEvenWhenAutomaticReprocessingIsOff()
        {
            var pipeline = CreatePipeline();
            pipeline.text = "<ugi>A</ugi>";
            pipeline.enabled = false;
            pipeline.text = "<ugi>B</ugi>";
            Assert.That(ProbeSink.Runs, Has.Count.EqualTo(1));
            pipeline.enabled = true;
            Assert.That(pipeline.text, Is.EqualTo("B"));
            Assert.That(ProbeSink.Runs.Select(run => run.text), Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void ReentrantSinkInput_IsDeferredWithoutOverwritingTheCurrentParse()
        {
            var pipeline = CreatePipeline();
            ProbeSink.OnRun = host =>
            {
                ProbeSink.OnRun = null;
                host.textMesh.text = "<ugi>B</ugi>";
            };
            pipeline.text = "<ugi>A</ugi>";
            Assert.That(ProbeSink.Runs.Select(run => run.text), Is.EqualTo(new[] { "A" }));
            Assert.That(pipeline.OriginalText, Is.EqualTo("<ugi>B</ugi>"));
            InvokeLateUpdate(pipeline);
            Assert.That(pipeline.text, Is.EqualTo("B"));
            Assert.That(ProbeSink.Runs.Select(run => run.text), Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void NullInput_ClearsText_AndRefreshDoesNotReportAMotherTextChange()
        {
            var pipeline = CreatePipeline();
            var changes = new List<string>();
            pipeline.OnOriginalTextChange += changes.Add;
            pipeline.text = "A";
            pipeline.Refresh();
            pipeline.text = null;
            Assert.That(pipeline.OriginalText, Is.Empty);
            Assert.That(pipeline.text, Is.Empty);
            Assert.That(pipeline.textInfo.characterCount, Is.Zero);
            Assert.That(changes, Is.EqualTo(new[] { "A", string.Empty }));
        }

        [Test]
        public void Inspector_BindsTextInputToMotherTextAndRestoresPreviewOnUndo()
        {
            var pipeline = CreatePipeline();
            const string original = "<ugi : label = \"original\">A</ugi>";
            pipeline.text = original;
            var editor = UnityEditor.Editor.CreateEditor(pipeline);
            try
            {
                Assert.That(editor.GetType().Name, Is.EqualTo("TextPipelineUGUIEditor"));
                var textProperty = (UnityEditor.SerializedProperty)editor.GetType()
                    .GetField("m_TextProp", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                Assert.That(textProperty.propertyPath, Is.EqualTo("originalText"));
                Assert.That(textProperty.stringValue, Is.EqualTo(original));
                Assert.That(editor.serializedObject.FindProperty("m_text").stringValue, Is.EqualTo("A"));

                UnityEditor.Undo.RecordObject(pipeline, "Change mother text");
                pipeline.text = "<ugi>B</ugi>";
                UnityEditor.Undo.FlushUndoRecordObjects();
                UnityEditor.Undo.PerformUndo();
                Assert.That(pipeline.OriginalText, Is.EqualTo(original));
                Assert.That(pipeline.text, Is.EqualTo("A"));
                Assert.That(pipeline.textInfo.characterInfo[0].character, Is.EqualTo('A'));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
                UnityEditor.Undo.ClearUndo(pipeline);
            }
        }

        private TextPipelineUGUI CreatePipeline(bool active = true)
        {
            _object = new GameObject("TextPipelineUGUI test", typeof(RectTransform), typeof(Canvas));
            _object.SetActive(false);
            _object.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var pipeline = _object.AddComponent<TextPipelineUGUI>();
            pipeline.font = TMP_Settings.defaultFontAsset;
            pipeline.processTextOnEnable = false;
            if (active) _object.SetActive(true);
            return pipeline;
        }

        private static void InvokeLateUpdate(TextPipelineUGUI pipeline)
            => typeof(TextPipelineUGUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pipeline, null);
    }
}
