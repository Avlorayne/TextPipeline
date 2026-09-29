using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TextPipeline.Postprocess;

namespace TextPipeline.Editor.Tests
{
    public sealed class TextSegmentSchedulingTests
    {
        [Markup("scheduling-probe")]
        public sealed class SchedulingSink : ITextSinkCoroutine
        {
            public readonly List<string> Events = new();

            public TextPipeline TextPipeline { get; set; }

            [MarkupProperty("label", MarkupDataType.String)]
            public string Label { get; set; } = "default";

            [MarkupMethod("tick")]
            public IEnumerator Tick([MarkupParam("name", MarkupDataType.String)] string name)
            {
                Events.Add($"{Label}:{name}:start");
                yield return null;
                Events.Add($"{Label}:{name}:end");
            }

            public IEnumerator PostProcess(TextSegment[] segments)
            {
                foreach (var step in segments)
                    yield return step.DoEffect(this, _ => Empty());
            }

            private static IEnumerator Empty()
            {
                yield break;
            }
        }

        [SetUp]
        public void SetUp()
        {
            MarkupRegistry.RegisterMarkups(new[] { typeof(SchedulingSink).Assembly });
        }

        [Test]
        public void DoEffect_CanRunSegmentsSerially()
        {
            var sink = new SchedulingSink();
            var first = Segment("first", "A").DoEffect(sink, _ => Empty());
            var second = Segment("second", "B").DoEffect(sink, _ => Empty());

            Drain(first);
            Assert.That(sink.Events, Is.EqualTo(new[] { "first:A:start", "first:A:end" }));
            Drain(second);

            Assert.That(sink.Events, Is.EqualTo(new[]
            {
                "first:A:start", "first:A:end", "second:B:start", "second:B:end"
            }));
            Assert.That(sink.Label, Is.EqualTo("default"));
        }

        [Test]
        public void DoEffect_CanInterleaveSegmentsWithoutLeakingTheirProperties()
        {
            var sink = new SchedulingSink();
            var first = Segment("first", "A").DoEffect(sink, _ => Empty());
            var second = Segment("second", "B").DoEffect(sink, _ => Empty());

            Assert.That(first.MoveNext(), Is.True);
            Assert.That(sink.Label, Is.EqualTo("default"));
            Assert.That(second.MoveNext(), Is.True);
            Assert.That(sink.Label, Is.EqualTo("default"));
            Assert.That(first.MoveNext(), Is.False);
            Assert.That(second.MoveNext(), Is.False);

            Assert.That(sink.Events, Is.EqualTo(new[]
            {
                "first:A:start", "second:B:start", "first:A:end", "second:B:end"
            }));
            Assert.That(sink.Label, Is.EqualTo("default"));
        }

        private static TextSegment Segment(string label, string name)
        {
            return new TextSegment(
                new Dictionary<string, object> { { "label", label } },
                "tick",
                new[] { new KeyValuePair<string, object>("name", name) });
        }

        private static IEnumerator Empty()
        {
            yield break;
        }

        private static void Drain(IEnumerator effect)
        {
            try
            {
                while (effect.MoveNext()) { }
            }
            finally
            {
                (effect as System.IDisposable)?.Dispose();
            }
        }
    }
}
