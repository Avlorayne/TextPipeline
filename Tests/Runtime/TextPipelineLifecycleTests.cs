using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TextPipeline.Postprocess;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace TextPipeline.Runtime.Tests
{
    public sealed class TextPipelineLifecycleTests
    {
        [Markup("runprobe")]
        public sealed class RunningSink : ITextSinkCoroutine
        {
            public static readonly Dictionary<int, int> Ticks = new();
            public static int RunCount;
            public ITextPipeline TextPipeline { get; set; }

            public IEnumerator PostProcess(TextSegment[] segments)
            {
                int run = ++RunCount;
                Ticks[run] = 0;
                while (true)
                {
                    Ticks[run]++;
                    yield return null;
                }
            }
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
                .GetValue(_settings)).Add(typeof(RunningSink).AssemblyQualifiedName);
            SettingsField.SetValue(null, _settings);
            MarkupRegistry.RegisterMarkups(new[] { typeof(RunningSink).Assembly });
            RunningSink.Ticks.Clear();
            RunningSink.RunCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_object != null) UnityEngine.Object.DestroyImmediate(_object);
            SettingsField.SetValue(null, _previousSettings);
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [UnityTest]
        public IEnumerator AttachedComponent_CancelsOldEffectsOnReplacementAndDisable()
            => ExerciseLifecycle(false);

        [UnityTest]
        public IEnumerator InheritedComponent_CancelsOldEffectsAndStopsWithoutHidingText()
            => ExerciseLifecycle(true);

        private IEnumerator ExerciseLifecycle(bool inherited)
        {
            _object = new GameObject("Pipeline lifecycle", typeof(RectTransform), typeof(Canvas));
            _object.SetActive(false);
            _object.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ITextPipeline pipeline;
            MonoBehaviour owner;
            if (inherited)
            {
                var derived = _object.AddComponent<TextPipelineUGUI>();
                derived.processTextOnEnable = false;
                pipeline = derived;
                owner = derived;
            }
            else
            {
                var label = _object.AddComponent<TextMeshProUGUI>();
                var attached = _object.AddComponent<TextPipeline>();
                attached.textMesh = label;
                attached.processTextOnEnable = false;
                pipeline = attached;
                owner = attached;
            }
            pipeline.textMesh.font = TMP_Settings.defaultFontAsset;
            _object.SetActive(true);
            if (inherited)
                pipeline.textMesh.text = "<runprobe>A</runprobe>";
            else
                pipeline.SetOriginalText("<runprobe>A</runprobe>");
            yield return null;
            yield return null;
            Assert.That(RunningSink.Ticks[1], Is.GreaterThan(1));

            pipeline.SetOriginalText("<runprobe>B</runprobe>");
            int stoppedTicks = RunningSink.Ticks[1];
            yield return null;
            yield return null;
            Assert.That(RunningSink.Ticks[1], Is.EqualTo(stoppedTicks));
            Assert.That(RunningSink.Ticks[2], Is.GreaterThan(1));
            Assert.That(pipeline.textMesh.text, Is.EqualTo("B"));

            if (inherited)
                ((TextPipelineUGUI)owner).StopEffects();
            else
                owner.enabled = false;
            stoppedTicks = RunningSink.Ticks[2];
            yield return null;
            yield return null;
            Assert.That(RunningSink.Ticks[2], Is.EqualTo(stoppedTicks));
            Assert.That(pipeline.textMesh.enabled, Is.True);
            Assert.That(pipeline.textMesh.text, Is.EqualTo("B"));

            owner.enabled = true;
            pipeline.Refresh();
            yield return null;
            Assert.That(RunningSink.Ticks[3], Is.GreaterThan(1));
            owner.enabled = false;
            stoppedTicks = RunningSink.Ticks[3];
            yield return null;
            yield return null;
            Assert.That(RunningSink.Ticks[3], Is.EqualTo(stoppedTicks));
        }
    }
}
