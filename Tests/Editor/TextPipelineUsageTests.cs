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
    /// <summary>
    /// Exercises the public markup path as a consumer would: parse text, allocate
    /// sinks, and execute them against the resulting TMP character ranges.
    /// </summary>
    public sealed class TextPipelineUsageTests
    {
        [Markup("audit")]
        public sealed class AuditSink : ITextSink
        {
            public static readonly List<(AuditSink sink, string tone, string text)> ProcessedContent = new();
            public static readonly List<(AuditSink sink, string message)> MarkerCalls = new();

            [MarkupProperty("tone", MarkupDataType.String)]
            public string Tone { get; set; } = "default";

            public ITextPipeline TextPipeline { get; set; }

            [MarkupMethod("mark")]
            public void Mark([MarkupParam("message", MarkupDataType.String)] string message)
            {
                MarkerCalls.Add((this, message));
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

                        ProcessedContent.Add((this, Tone, new string(characters)));
                    });
                }
            }

            public static void Reset()
            {
                ProcessedContent.Clear();
                MarkerCalls.Clear();
            }
        }

        public sealed class AuthoringSource : MonoBehaviour, ITextSource
        {
            public string ReplacementText { get; set; }
            public ITextPipeline Pipeline { get; private set; }

            ITextPipeline ITextSource.TextPipeline
            {
                set => Pipeline = value;
            }

            public string PreProcess(string text)
            {
                return ReplacementText;
            }
        }

        private TextPipelineSettings _previousSettings;
        private TextPipelineSettings _testSettings;
        private GameObject _pipelineObject;

        [SetUp]
        public void SetUp()
        {
            MarkupRegistry.RegisterMarkups(new[] { typeof(AuditSink).Assembly });
            AuditSink.Reset();
            _previousSettings = GetSettingsInstance();
        }

        [TearDown]
        public void TearDown()
        {
            SetSettingsInstance(_previousSettings);
            if (_testSettings != null)
                UnityEngine.Object.DestroyImmediate(_testSettings);
            if (_pipelineObject != null)
                UnityEngine.Object.DestroyImmediate(_pipelineObject);
        }

        [Test]
        public void Execute_ReusesScopes_RestoresNestedProperties_AndPreventsSiblingWriteLeaks()
        {
            var root = TextInfoMarkupParser.Parse(
                "<audit : tone = \"outer\"><audit : tone = \"inner\">A</audit>B</audit>" +
                "<audit : tone = \"sibling\">C</audit>" +
                "<audit:mark(message:\"scope-one\") | scope = 1>" +
                "<audit : tone = \"scope-one\" | scope = 1>D</audit>");

            var sinks = new InstanceTreeBuilder().Build(root).Cast<AuditSink>().ToArray();

            Assert.That(sinks, Has.Length.EqualTo(2),
                "The default scope and scope=1 must receive separate instances.");

            foreach (var sink in sinks)
                PostProcessorExecute.Execute(sink, root);

            var defaultScopeSink = AuditSink.ProcessedContent.Single(item => item.text == "B").sink;
            var scopeOneSink = AuditSink.ProcessedContent.Single(item => item.text == "D").sink;

            Assert.That(AuditSink.ProcessedContent.Select(item => (item.tone, item.text)), Is.EqualTo(new[]
            {
                ("inner", "A"),
                ("outer", "B"),
                ("sibling", "C"),
                ("scope-one", "D")
            }));
            Assert.That(defaultScopeSink, Is.Not.SameAs(scopeOneSink));
            Assert.That(AuditSink.MarkerCalls, Is.EqualTo(new[] { (scopeOneSink, "scope-one") }));
            Assert.That(defaultScopeSink.Tone, Is.EqualTo("default"),
                "The outer and sibling writes must be restored after their blocks finish.");
            Assert.That(scopeOneSink.Tone, Is.EqualTo("default"),
                "Each scoped instance must be restored after its block finishes.");
        }

        [Test]
        public void SetOriginalText_UsesConfiguredSinksThroughThePublicPipelineEntryPoint()
        {
            ConfigureSinkSequence(typeof(AuditSink));

            _pipelineObject = new GameObject("TextPipeline public-use test", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasRenderer));
            _pipelineObject.SetActive(false);
            _pipelineObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var textMesh = _pipelineObject.AddComponent<TMPro.TextMeshProUGUI>();
            textMesh.font = TMPro.TMP_Settings.defaultFontAsset;
            var pipeline = _pipelineObject.AddComponent<TextPipeline>();
            pipeline.textMesh = textMesh;
            pipeline.processTextOnEnable = false;
            _pipelineObject.SetActive(true);

            const string authoringText = "<audit : tone = \"public\">A</audit>";
            var result = pipeline.SetOriginalText(authoringText);

            Assert.That(result, Is.EqualTo(authoringText));
            Assert.That(textMesh.text, Is.EqualTo("A"),
                "The public API must display formal text, with successful markup compacted away.");
            Assert.That(AuditSink.ProcessedContent, Has.Count.EqualTo(1));
            Assert.That(AuditSink.ProcessedContent[0].tone, Is.EqualTo("public"));
            Assert.That(AuditSink.ProcessedContent[0].text, Is.EqualTo("A"));
            Assert.That(AuditSink.ProcessedContent[0].sink.TextPipeline, Is.SameAs(pipeline));

            var firstInstance = AuditSink.ProcessedContent[0].sink;
            AuditSink.Reset();
            pipeline.SetOriginalText("<audit : tone = \"second\">B</audit>");

            Assert.That(AuditSink.ProcessedContent, Has.Count.EqualTo(1));
            Assert.That(AuditSink.ProcessedContent[0].sink, Is.SameAs(firstInstance),
                "A pipeline owns a sink instance for its full component lifetime.");
            Assert.That(AuditSink.ProcessedContent[0].tone, Is.EqualTo("second"));
            Assert.That(AuditSink.ProcessedContent[0].text, Is.EqualTo("B"));
        }

        [Test]
        public void SetOriginalText_PreservesTmpRichTextWhileRemovingPipelineMarkup()
        {
            ConfigureSinkSequence(typeof(AuditSink));

            _pipelineObject = new GameObject("TextPipeline rich-text test", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasRenderer));
            _pipelineObject.SetActive(false);
            _pipelineObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var textMesh = _pipelineObject.AddComponent<TextMeshProUGUI>();
            textMesh.font = TMPro.TMP_Settings.defaultFontAsset;
            var pipeline = _pipelineObject.AddComponent<TextPipeline>();
            pipeline.textMesh = textMesh;
            pipeline.processTextOnEnable = false;
            _pipelineObject.SetActive(true);

            const string authoringText = "<audit : tone = \"rich\"><b>A</b></audit>";
            pipeline.SetOriginalText(authoringText);

            Assert.That(textMesh.text, Is.EqualTo("<b>A</b>"));
            Assert.That(textMesh.textInfo.characterCount, Is.EqualTo(1));
            Assert.That(textMesh.textInfo.characterInfo[0].character, Is.EqualTo('A'));
            Assert.That(AuditSink.ProcessedContent.Select(item => (item.tone, item.text)),
                Is.EqualTo(new[] { ("rich", "A") }));
        }

        [Test]
        public void SetOriginalText_RunsConfiguredSourcesBeforeMarkupParsing()
        {
            ConfigureSinkSequence(typeof(AuditSink));
            ConfigureSourceSequence(typeof(AuthoringSource));

            _pipelineObject = new GameObject("TextPipeline source test", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasRenderer));
            _pipelineObject.SetActive(false);
            _pipelineObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var textMesh = _pipelineObject.AddComponent<TMPro.TextMeshProUGUI>();
            textMesh.font = TMPro.TMP_Settings.defaultFontAsset;
            var source = _pipelineObject.AddComponent<AuthoringSource>();
            source.ReplacementText = "<audit : tone = \"source\">S</audit>";
            var pipeline = _pipelineObject.AddComponent<TextPipeline>();
            pipeline.textMesh = textMesh;
            pipeline.processTextOnEnable = false;
            _pipelineObject.SetActive(true);

            var result = pipeline.SetOriginalText("unprocessed input");

            Assert.That(result, Is.EqualTo(source.ReplacementText));
            Assert.That(source.Pipeline, Is.SameAs(pipeline));
            Assert.That(textMesh.text, Is.EqualTo("S"));
            Assert.That(AuditSink.ProcessedContent.Select(item => (item.tone, item.text)),
                Is.EqualTo(new[] { ("source", "S") }));
        }

        [Test]
        public void Lifecycle_OnEnableThenStart_ProcessesInitialTextOnlyOnce()
        {
            ConfigureSinkSequence(typeof(AuditSink));

            _pipelineObject = new GameObject("TextPipeline lifecycle test", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasRenderer));
            _pipelineObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var textMesh = _pipelineObject.AddComponent<TMPro.TextMeshProUGUI>();
            textMesh.font = TMPro.TMP_Settings.defaultFontAsset;
            var pipeline = _pipelineObject.AddComponent<TextPipeline>();
            pipeline.textMesh = textMesh;
            pipeline.processTextOnEnable = false;

            typeof(TextPipeline).GetField("originalText", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(pipeline, "<audit : tone = \"lifecycle\">A</audit>");
            AuditSink.Reset();
            pipeline.processTextOnEnable = true;

            InvokeLifecycleMethod(pipeline, "OnEnable");
            InvokeLifecycleMethod(pipeline, "Start");

            Assert.That(AuditSink.ProcessedContent.Select(item => (item.tone, item.text)),
                Is.EqualTo(new[] { ("lifecycle", "A") }));
        }

        [Test]
        public void Build_CreatesOneInstancePerScopeWithinAParsedDocument()
        {
            var root = TextInfoMarkupParser.Parse(
                "<audit>A</audit><audit | scope = 0>B</audit><audit | scope = 2>C</audit>");

            var sinks = new InstanceTreeBuilder().Build(root).Cast<AuditSink>().ToArray();

            Assert.That(sinks, Has.Length.EqualTo(2));
            Assert.That(((BlockNode)root.Children[0]).PostProcessor,
                Is.SameAs(((BlockNode)root.Children[1]).PostProcessor));
            Assert.That(((BlockNode)root.Children[2]).PostProcessor,
                Is.Not.SameAs(((BlockNode)root.Children[0]).PostProcessor));
        }

        [Test]
        public void SortSinks_ReportsEveryParsedSinkTypeMissingFromSettings()
        {
            ConfigureSinkSequence();

            _pipelineObject = new GameObject("TextPipeline sort test");
            _pipelineObject.SetActive(false);
            var pipeline = _pipelineObject.AddComponent<TextPipeline>();
            pipeline.processTextOnEnable = false;

            var sortSinks = typeof(TextPipeline).GetMethod("SortSinks",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var thrown = Assert.Throws<TargetInvocationException>(() =>
                sortSinks.Invoke(pipeline, new object[] { new ITextSinkBase[] { new AuditSink() } }));

            Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(thrown.InnerException.Message, Does.Contain(typeof(AuditSink).FullName));
            Assert.That(thrown.InnerException.Message, Does.Contain("Sink Sequence"));
        }

        private void ConfigureSinkSequence(params Type[] sinkTypes)
        {
            _testSettings = ScriptableObject.CreateInstance<TextPipelineSettings>();
            var names = (List<string>)typeof(TextPipelineSettings)
                .GetField("sinkSequenceTypeNames", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_testSettings);
            names.AddRange(sinkTypes.Select(type => type.AssemblyQualifiedName));
            SetSettingsInstance(_testSettings);
        }

        private void ConfigureSourceSequence(params Type[] sourceTypes)
        {
            var names = (List<string>)typeof(TextPipelineSettings)
                .GetField("sourceSequenceTypeNames", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_testSettings);
            names.AddRange(sourceTypes.Select(type => type.AssemblyQualifiedName));
        }

        private static void InvokeLifecycleMethod(TextPipeline pipeline, string methodName)
        {
            typeof(TextPipeline).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pipeline, null);
        }

        private static TextPipelineSettings GetSettingsInstance()
        {
            return (TextPipelineSettings)typeof(TextPipelineSettings)
                .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
        }

        private static void SetSettingsInstance(TextPipelineSettings settings)
        {
            typeof(TextPipelineSettings)
                .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, settings);
        }
    }
}
