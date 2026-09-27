using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using TextPipeline;
using PipelineComponent = TextPipeline.TextPipeline;
using TextPipeline.Postprocess;

namespace TextPipeline.Editor.Tests
{
    /// <summary>
    /// Consumer-side contract tests for the embedded TextPipeline package.
    /// These types deliberately live outside Packages/TextPipeline so that the
    /// tests exercise the same extension surface available to a game project.
    /// </summary>
    public sealed class TextPipelineConsumerContractTests
    {
        [Markup("probe")]
        private sealed class ProbeSink : ITextSink
        {
            public static readonly List<string> Processed = new();
            public static readonly List<string> Markers = new();

            public PipelineComponent TextPipeline { get; set; }

            [MarkupProperty("label", MarkupDataType.String)]
            public string Label { get; set; } = "default";

            [MarkupProperty("strength", MarkupDataType.Number)]
            public float Strength { get; set; }

            [MarkupProperty("enabled", MarkupDataType.Boolean)]
            public bool Enabled { get; set; }

            [MarkupMethod("ping")]
            public void Ping([MarkupParam("message", MarkupDataType.String)] string message = "default")
            {
                Markers.Add(message);
            }

            public void PostProcess(TextSegment[] segments)
            {
                foreach (var segment in segments)
                {
                    segment.DoEffect(this, content =>
                    {
                        var characters = new char[content.CharacterCount];
                        var index = 0;
                        foreach (var character in content)
                            characters[index++] = character.character;

                        Processed.Add($"{Label}|{Strength:0.0}|{Enabled}|{new string(characters)}");
                    });
                }
            }

            public static void Reset()
            {
                Processed.Clear();
                Markers.Clear();
            }
        }

        private sealed class PrefixSource : MonoBehaviour, ITextSource
        {
            public string Prefix { get; set; }
            public PipelineComponent Pipeline { get; private set; }

            PipelineComponent ITextSource.TextPipeline
            {
                set => Pipeline = value;
            }

            public string PreProcess(string text) => Prefix + text;
        }

        private static readonly FieldInfo SettingsInstanceField = typeof(TextPipelineSettings)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo SinkSequenceField = typeof(TextPipelineSettings)
            .GetField("sinkSequenceTypeNames", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SourceSequenceField = typeof(TextPipelineSettings)
            .GetField("sourceSequenceTypeNames", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo OriginalTextField = typeof(PipelineComponent)
            .GetField("originalText", BindingFlags.Instance | BindingFlags.NonPublic);

        private TextPipelineSettings _previousSettings;
        private TextPipelineSettings _testSettings;
        private GameObject _pipelineObject;
        private PipelineComponent _pipeline;
        private TMP_Text _text;

        [SetUp]
        public void SetUp()
        {
            MarkupRegistry.RegisterMarkups(new[] { typeof(ProbeSink).Assembly, typeof(PipelineComponent).Assembly });
            ProbeSink.Reset();
            _previousSettings = (TextPipelineSettings)SettingsInstanceField.GetValue(null);
            _testSettings = ScriptableObject.CreateInstance<TextPipelineSettings>();
            SettingsInstanceField.SetValue(null, _testSettings);
        }

        [TearDown]
        public void TearDown()
        {
            SettingsInstanceField.SetValue(null, _previousSettings);
            if (_pipelineObject != null)
                UnityEngine.Object.DestroyImmediate(_pipelineObject);
            if (_testSettings != null)
                UnityEngine.Object.DestroyImmediate(_testSettings);
        }

        [Test]
        public void ConsumerSink_ProcessesBlockProperties_Markers_DefaultArguments_AndEscapedText()
        {
            ConfigureSinks(typeof(ProbeSink));
            CreatePipeline();

            var output = _pipeline.SetOriginalText(
                "<probe : label = \"Captain \\\"Nova\\\"\", strength = -1.5, enabled = true>" +
                "AB<probe:ping(\"now\")>C<probe:ping></probe>");

            Assert.That(output, Does.Contain("<probe"));
            Assert.That(_text.text, Is.EqualTo("ABC"));
            Assert.That(ProbeSink.Processed, Is.EqualTo(new[]
            {
                "Captain \"Nova\"|-1.5|True|AB",
                "Captain \"Nova\"|-1.5|True|C"
            }));
            Assert.That(ProbeSink.Markers, Is.EqualTo(new[] { "now", "default" }));
        }

        [Test]
        public void Pipeline_OrdersSources_AndHonoursPreAndPostProcessingFlags()
        {
            ConfigureSinks(typeof(ProbeSink));
            ConfigureSources(typeof(PrefixSource));
            var source = CreatePipelineWithPrefixSource("<probe : label = \"source\">");

            _pipeline.SetOriginalText("X</probe>", withPreProcessing: false, withPostProcessing: false);
            Assert.That(_text.text, Is.EqualTo("X</probe>"));
            Assert.That(ProbeSink.Processed, Is.Empty);

            _pipeline.SetOriginalTextSimply("Y</probe>");

            Assert.That(source.Pipeline, Is.SameAs(_pipeline));
            Assert.That(_text.text, Is.EqualTo("Y"));
            Assert.That(ProbeSink.Processed, Is.EqualTo(new[] { "source|0.0|False|Y" }));

            ProbeSink.Reset();
            _pipeline.Refresh();
            Assert.That(ProbeSink.Processed, Is.EqualTo(new[] { "source|0.0|False|Y" }),
                "Refresh must reuse OriginalText and rerun the configured source chain.");
        }

        [Test]
        public void Lifecycle_OnEnable_RebuildsSerializedOriginalText_WhenEnabled()
        {
            ConfigureSinks(typeof(ProbeSink));
            _pipelineObject = new GameObject("TextPipeline lifecycle consumer test", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasRenderer));
            _pipelineObject.SetActive(false);
            _pipelineObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _text = _pipelineObject.AddComponent<TextMeshProUGUI>();
            _text.font = TMP_Settings.defaultFontAsset;
            _pipeline = _pipelineObject.AddComponent<PipelineComponent>();
            _pipeline.textMesh = _text;
            _pipeline.processTextOnEnable = true;
            OriginalTextField.SetValue(_pipeline, "<probe : label = \"lifecycle\">L</probe>");

            _pipelineObject.SetActive(true);
            InvokeLifecycleMethod(_pipeline, "OnEnable");

            Assert.That(_text.text, Is.EqualTo("L"));
            Assert.That(ProbeSink.Processed, Is.EqualTo(new[] { "lifecycle|0.0|False|L" }));
        }

        private void CreatePipeline()
        {
            _pipelineObject = new GameObject("TextPipeline consumer test", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasRenderer));
            _pipelineObject.SetActive(false);
            _pipelineObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _text = _pipelineObject.AddComponent<TextMeshProUGUI>();
            _text.font = TMP_Settings.defaultFontAsset;
            _pipeline = _pipelineObject.AddComponent<PipelineComponent>();
            _pipeline.textMesh = _text;
            _pipeline.processTextOnEnable = false;
            _pipelineObject.SetActive(true);
        }

        private PrefixSource CreatePipelineWithPrefixSource(string prefix)
        {
            _pipelineObject = new GameObject("TextPipeline source consumer test", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasRenderer));
            _pipelineObject.SetActive(false);
            _pipelineObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var source = _pipelineObject.AddComponent<PrefixSource>();
            source.Prefix = prefix;
            _text = _pipelineObject.AddComponent<TextMeshProUGUI>();
            _text.font = TMP_Settings.defaultFontAsset;
            _pipeline = _pipelineObject.AddComponent<PipelineComponent>();
            _pipeline.textMesh = _text;
            _pipeline.processTextOnEnable = false;
            _pipelineObject.SetActive(true);
            return source;
        }

        private void ConfigureSinks(params Type[] types)
        {
            ((List<string>)SinkSequenceField.GetValue(_testSettings)).AddRange(
                types.Select(type => type.AssemblyQualifiedName));
        }

        private void ConfigureSources(params Type[] types)
        {
            ((List<string>)SourceSequenceField.GetValue(_testSettings)).AddRange(
                types.Select(type => type.AssemblyQualifiedName));
        }

        private static void InvokeLifecycleMethod(PipelineComponent pipeline, string methodName)
        {
            typeof(PipelineComponent).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pipeline, null);
        }
    }
}
