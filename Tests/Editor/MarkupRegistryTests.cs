using NUnit.Framework;
using TMPro;
using TextPipeline.Postprocess;

namespace TextPipeline.Editor.Tests
{
    public sealed class MarkupRegistryTests
    {
        [Markup("type")]
        private sealed class TextTyper : ITextSinkBase
        {
            [MarkupProperty("speed", MarkupDataType.Number)]
            public float Speed { get; set; } = 0.15f;

            public TextPipeline TextPipeline { get; set; }

            [MarkupMethod("delay")]
            public void Delay([MarkupParam("seconds", MarkupDataType.Number, false)] float seconds)
            {
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

            var property = MarkupRegistry.LookupForProperty("type", "speed");
            Assert.That(property.property.Name, Is.EqualTo(nameof(TextTyper.Speed)));
            Assert.That(property.dataType, Is.EqualTo(MarkupDataType.Number));
            Assert.That(property.allowDefault, Is.True);
            Assert.That(MarkupRegistry.LookupForDeclaredProperties("type"), Has.Length.EqualTo(1));
            Assert.That(MarkupRegistry.LookupForDeclaredProperties("type")[0].name, Is.EqualTo("speed"));

            Assert.That(MarkupRegistry.LookupForMethod("type", "delay").Name,
                Is.EqualTo(nameof(TextTyper.Delay)));

            var parameter = MarkupRegistry.LookupForParam("type", "delay", "seconds");
            Assert.That(parameter.param.Name, Is.EqualTo("seconds"));
            Assert.That(parameter.dataType, Is.EqualTo(MarkupDataType.Number));
            Assert.That(parameter.allowDefault, Is.False);
            Assert.That(MarkupRegistry.LookupForDeclaredParams("type", "delay"), Has.Length.EqualTo(1));
            Assert.That(MarkupRegistry.LookupForDeclaredParams("type", "delay")[0].name,
                Is.EqualTo("seconds"));

            var singleParameter = MarkupRegistry.LookupForParam("type", "delay");
            Assert.That(singleParameter.param, Is.EqualTo(parameter.param));
            Assert.That(singleParameter.paramName, Is.EqualTo("seconds"));
            Assert.That(singleParameter.dataType, Is.EqualTo(MarkupDataType.Number));
            Assert.That(singleParameter.allowDefault, Is.False);
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
