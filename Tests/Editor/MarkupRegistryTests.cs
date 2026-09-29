using NUnit.Framework;
using System.Collections.Generic;
using TextPipeline.Postprocess;

namespace TextPipeline.Editor.Tests
{
    public sealed class MarkupRegistryTests
    {
        [Markup("type")]
        private sealed class TextTyper : ITextSinkBase
        {
            [MarkupProperty("speed", MarkupDataType.Float)]
            public float Speed { get; set; } = 0.15f;

            public float LastDelay { get; private set; }

            public TextPipeline TextPipeline { get; set; }

            [MarkupMethod("delay")]
            public void Delay([MarkupParam("seconds", MarkupDataType.Float, false)] float seconds)
            {
                LastDelay = seconds;
            }

        }

        [SetUp]
        public void RegisterTemplateMarkup()
        {
            MarkupRegistry.RegisterMarkups(new[] { typeof(TextTyper).Assembly });
        }

        [Test]
        public void RegisterMarkups_TemplateClass_ExposesRegisteredMetadataThroughLookupApi()
        {
            Assert.That(MarkupRegistry.LookupForType("type"), Is.EqualTo(typeof(TextTyper)));
            Assert.That(MarkupRegistry.TryGetProperty("type", "speed", out var propertyType), Is.True);
            Assert.That(propertyType, Is.EqualTo(MarkupDataType.Float));
            Assert.That(MarkupRegistry.LookupForDeclaredProperties("type"), Has.Length.EqualTo(1));
            Assert.That(MarkupRegistry.LookupForDeclaredProperties("type")[0].name, Is.EqualTo("speed"));
            Assert.That(MarkupRegistry.LookupForDeclaredProperties("type")[0].allowDefault, Is.True);

            Assert.That(MarkupRegistry.HasMethod("type", "delay"), Is.True);

            Assert.That(MarkupRegistry.TryGetParameter("type", "delay", "seconds", out var parameterType), Is.True);
            Assert.That(parameterType, Is.EqualTo(MarkupDataType.Float));
            Assert.That(MarkupRegistry.LookupForDeclaredParams("type", "delay"), Has.Length.EqualTo(1));
            Assert.That(MarkupRegistry.LookupForDeclaredParams("type", "delay")[0].name,
                Is.EqualTo("seconds"));
            Assert.That(MarkupRegistry.LookupForDeclaredParams("type", "delay")[0].allowDefault, Is.False);
            Assert.That(MarkupRegistry.GetSingleParameterName("type", "delay"), Is.EqualTo("seconds"));
        }

        [Test]
        public void CachedDelegates_GetSetAndInvokeByName()
        {
            var sink = new TextTyper();
            Assert.That(MarkupRegistry.Get(sink, "speed"), Is.EqualTo(0.15f));
            MarkupRegistry.Set(sink, "speed", 0.5f);
            Assert.That(sink.Speed, Is.EqualTo(0.5f));

            MarkupRegistry.Invoke(sink, "delay", new[]
            {
                new KeyValuePair<string, object>("seconds", 1.25f)
            });
            Assert.That(sink.LastDelay, Is.EqualTo(1.25f));
        }

        [Test]
        public void RegisterMarkups_RepeatedRegistrationOfTheSameAssembly_ReplacesAllLookupTables()
        {
            Assert.DoesNotThrow(() =>
                MarkupRegistry.RegisterMarkups(new[] { typeof(TextTyper).Assembly }));

            Assert.That(MarkupRegistry.LookupForProperties(typeof(TextTyper)), Has.Length.EqualTo(1));
        }
    }
}
