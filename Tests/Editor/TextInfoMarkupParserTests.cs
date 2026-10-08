using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TextPipeline.Postprocess;

namespace TextPipeline.Editor.Tests
{
    public sealed class TextInfoMarkupParserTests
    {
        [Markup("wave")]
        private sealed class WaveSink : ITextSinkBase
        {
            [MarkupProperty("speed", MarkupDataType.Float)]
            public float Speed { get; set; }

            [MarkupProperty("label", MarkupDataType.String)]
            public string Label { get; set; }

            public ITextPipeline TextPipeline { get; set; }

            [MarkupMethod("pause")]
            public void Pause([MarkupParam("duration", MarkupDataType.Float)] float duration)
            {
            }

            [MarkupMethod("play")]
            public void Play(
                [MarkupParam("clip", MarkupDataType.String)]
                string clip,
                [MarkupParam("loop", MarkupDataType.Boolean)]
                bool loop)
            {
            }

        }

        [Markup("echo")]
        private sealed class EchoSink : ITextSinkBase
        {
            public ITextPipeline TextPipeline { get; set; }
        }

        [Markup("required")]
        private sealed class RequiredSink : ITextSink
        {
            [MarkupProperty("title", MarkupDataType.String, false)]
            public string Title { get; set; }

            [MarkupProperty("count", MarkupDataType.Int)]
            public int Count { get; set; }

            public ITextPipeline TextPipeline { get; set; }

            public readonly List<int> ObservedCounts = new();

            public void PostProcess(TextSegment[] segments)
            {
                foreach (var segment in segments)
                    segment.DoEffect(this, _ => ObservedCounts.Add(Count));
            }

            [MarkupMethod("set")]
            public void Set([MarkupParam("value", MarkupDataType.Float, false)] float value)
            {
            }

            [MarkupMethod("set-int")]
            public void SetInt([MarkupParam("value", MarkupDataType.Int, false)] int value)
            {
            }

            [MarkupMethod("set-bool")]
            public void SetBool([MarkupParam("value", MarkupDataType.Boolean, false)] bool value)
            {
            }

            [MarkupMethod("set-text")]
            public void SetText([MarkupParam("value", MarkupDataType.String, false)] string value)
            {
            }
        }

        [SetUp]
        public void RegisterTestMarkup()
        {
            MarkupRegistry.RegisterMarkups(new[] { typeof(WaveSink).Assembly });
        }

        [Test]
        public void Parse_BuildsTreeAndBindsSingleValueParameter()
        {
            RootNode rootNode = TextInfoMarkupParser.Parse(
                "A<wave : speed = 1.25, label = \"Captain \\\"Nova\\\"\" | scope = 2>B<wave:pause(0.5)>C</wave>D");

            Assert.That(rootNode.StartIndex, Is.EqualTo(0));
            Assert.That(rootNode.EndIndex, Is.EqualTo(4));
            Assert.That(rootNode.Diagnostics, Is.Empty);
            Assert.That(rootNode.Children, Has.Count.EqualTo(3));

            var content = (ContentNode)rootNode.Children[0];
            var wave = (BlockNode)rootNode.Children[1];
            var trailing = (ContentNode)rootNode.Children[2];
            Assert.That(content.StartIndex, Is.EqualTo(0));
            Assert.That(content.EndIndex, Is.EqualTo(1));
            Assert.That(wave.Markup, Is.EqualTo("wave"));
            Assert.That(wave.ScopeNum, Is.EqualTo(2));
            Assert.That(wave.PropertyPairs, Is.EqualTo(new[]
            {
                new KeyValuePair<string, string>("speed", "1.25"),
                new KeyValuePair<string, string>("label", "Captain \"Nova\"")
            }));
            Assert.That(wave.Children, Has.Count.EqualTo(3));

            var marker = (SingleMarkerNode)wave.Children[1];
            Assert.That(marker.Markup, Is.EqualTo("wave"));
            Assert.That(marker.MethodName, Is.EqualTo("pause"));
            Assert.That(marker.ParamPairs, Is.EqualTo(new[]
            {
                new KeyValuePair<string, string>("duration", "0.5")
            }));
            Assert.That(marker.Parent, Is.EqualTo(wave));
            Assert.That(wave.StartIndex, Is.EqualTo(1));
            Assert.That(wave.EndIndex, Is.EqualTo(3));
            Assert.That(marker.StartIndex, Is.EqualTo(2));
            Assert.That(marker.EndIndex, Is.EqualTo(2));
            Assert.That(trailing.StartIndex, Is.EqualTo(wave.EndIndex));
            Assert.That(trailing.EndIndex, Is.EqualTo(4));
        }

        [Test]
        public void Parse_AssignsSingleMarkerScopeAndCharacterCounts()
        {
            RootNode rootNode = TextInfoMarkupParser.Parse("ab<wave:pause(0.5) | scope = 7>cd");

            var before = (ContentNode)rootNode.Children[0];
            var marker = (SingleMarkerNode)rootNode.Children[1];
            var after = (ContentNode)rootNode.Children[2];

            Assert.That(marker.ScopeNum, Is.EqualTo(7));
            Assert.That(marker.StartIndex, Is.EqualTo(2));
            Assert.That(marker.EndIndex, Is.EqualTo(2));
            Assert.That(marker.CharacterCount, Is.EqualTo(0));
            Assert.That(before.CharacterCount, Is.EqualTo(2));
            Assert.That(after.CharacterCount, Is.EqualTo(2));
            Assert.That(rootNode.CharacterCount, Is.EqualTo(4));
            Assert.That(GetText(before.Content), Is.EqualTo("ab"));
            Assert.That(GetText(after.Content), Is.EqualTo("cd"));
        }

        [Test]
        public void Parse_InjectsMarkerStrippedTextInfoAndBindsContentRanges()
        {
            TMP_TextInfo strippedTextInfo = CreateTextInfo("abcd");

            RootNode rootNode = TextInfoMarkupParser.Parse("ab<wave:pause(0.5)>cd", strippedTextInfo);

            var before = (ContentNode)rootNode.Children[0];
            var marker = (SingleMarkerNode)rootNode.Children[1];
            var after = (ContentNode)rootNode.Children[2];
            Assert.That(GetText(before.Content), Is.EqualTo("ab"));
            Assert.That(GetText(after.Content), Is.EqualTo("cd"));
            before.Content.CharacterAt(0).character = 'A';
            after.Content.CharacterAt(0).character = 'C';
            Assert.That(GetText(strippedTextInfo), Is.EqualTo("AbCd"));
            Assert.That(marker.StartIndex, Is.EqualTo(2));
            Assert.That(marker.EndIndex, Is.EqualTo(2));
            Assert.That(rootNode.EndIndex, Is.EqualTo(strippedTextInfo.characterCount));
        }

        [Test]
        public void Parse_RejectsInjectedTextInfoThatDoesNotMatchFormalText()
        {
            TMP_TextInfo differentTextInfo = CreateTextInfo("axcd");

            Assert.That(
                () => TextInfoMarkupParser.Parse("ab<wave:pause(0.5)>cd", differentTextInfo),
                Throws.ArgumentException);
        }

        [Test]
        public void Analyse_FormalisesSourceThenBindsTheLiveFormalTextInfo()
        {
            const string authoringText = "ab<wave:pause(0.5)>cd";
            TMP_TextInfo authoringTextInfo = CreateTextInfo(authoringText);

            MarkupParsePlan plan = TextInfoMarkupParser.Analyse(authoringText, authoringTextInfo);
            TMP_TextInfo formalTextInfo = CreateTextInfo("abcd");
            RootNode rootNode = plan.Bind(formalTextInfo);

            Assert.That(plan.FormalisedText, Is.EqualTo("abcd"));
            Assert.That(rootNode.EndIndex, Is.EqualTo(formalTextInfo.characterCount));

            var before = (ContentNode)rootNode.Children[0];
            var after = (ContentNode)rootNode.Children[2];
            before.Content.CharacterAt(0).character = 'A';
            after.Content.CharacterAt(0).character = 'C';

            Assert.That(GetText(formalTextInfo), Is.EqualTo("AbCd"));
        }

        [Test]
        public void TextInfoRange_EnumeratesOnlyItsRangeAndSupportsRefMutation()
        {
            TMP_TextInfo textInfo = CreateTextInfo("abcd");
            var range = new TextInfoRange(textInfo, 1, 3);
            var characters = new char[range.CharacterCount];

            int index = 0;
            foreach (TMP_CharacterInfo characterInfo in range)
                characters[index++] = characterInfo.character;

            foreach (ref TMP_CharacterInfo characterInfo in range)
                characterInfo.character = char.ToUpperInvariant(characterInfo.character);

            Assert.That(new string(characters), Is.EqualTo("bc"));
            Assert.That(GetText(textInfo), Is.EqualTo("aBCd"));
        }

        [Test]
        public void Parse_ValidatesContractsAndSkipsOnlyInvalidCandidates()
        {
            RootNode rootNode = TextInfoMarkupParser.Parse(
                "before<wave : speed = true>middle<wave:play(clip:\"alarm\", loop:false)>after");

            Assert.That(rootNode.Children, Has.Count.EqualTo(4));
            Assert.That(rootNode.Children[0], Is.TypeOf<ContentNode>());
            Assert.That(rootNode.Children[1], Is.TypeOf<ContentNode>());
            Assert.That(rootNode.Children[2], Is.TypeOf<SingleMarkerNode>());
            Assert.That(rootNode.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(rootNode.Diagnostics[0].Kind, Is.EqualTo(MarkupParseErrorKind.Contract));

            var marker = (SingleMarkerNode)rootNode.Children[2];
            Assert.That(marker.MethodName, Is.EqualTo("play"));
            Assert.That(marker.ParamPairs, Has.Length.EqualTo(2));
            Assert.That(rootNode.EndIndex, Is.EqualTo(17));
        }

        [Test]
        public void Parse_RequiresDeclaredBlockPropertiesAndKeepsFollowingMarkers()
        {
            RootNode root = TextInfoMarkupParser.Parse(
                "a<required>bad</required><required : title = \"ok\">good</required><required:set(1)>b");

            Assert.That(root.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(root.Diagnostics[0].Kind, Is.EqualTo(MarkupParseErrorKind.Contract));
            Assert.That(root.Diagnostics[0].Message, Does.Contain("title"));
            Assert.That(root.Diagnostics[1].Message, Does.Contain("孤立"));
            Assert.That(root.Children.OfType<BlockNode>().Count(), Is.EqualTo(1));
            Assert.That(root.Children.OfType<SingleMarkerNode>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Parse_RequiresDeclaredMethodParameters()
        {
            RootNode root = TextInfoMarkupParser.Parse("a<required:set>b<required:set(value:2)>c");

            Assert.That(root.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(root.Diagnostics[0].Kind, Is.EqualTo(MarkupParseErrorKind.Contract));
            Assert.That(root.Diagnostics[0].Message, Does.Contain("value"));
            Assert.That(root.Children.OfType<SingleMarkerNode>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Parse_RejectsExplicitDefaultForRequiredProperty()
        {
            RootNode root = TextInfoMarkupParser.Parse(
                "<required : title = \"\">bad</required>" +
                "<required : title = \"ok\", count = 0>good</required>");

            Assert.That(root.Diagnostics[0].Kind, Is.EqualTo(MarkupParseErrorKind.Contract));
            Assert.That(root.Diagnostics[0].Message, Does.Contain("title").And.Contain("默认值"));
            Assert.That(root.Children.OfType<BlockNode>().Count(), Is.EqualTo(1));
        }

        [TestCase("<required:set(0)>")]
        [TestCase("<required:set(value:-0.0)>")]
        [TestCase("<required:set-int(00)>")]
        [TestCase("<required:set-bool(false)>")]
        [TestCase("<required:set-text(\"\")>")]
        public void Parse_RejectsExplicitDefaultForRequiredParameter(string invalidMarker)
        {
            RootNode root = TextInfoMarkupParser.Parse(invalidMarker + "<required:set(1)>");

            Assert.That(root.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(root.Diagnostics[0].Kind, Is.EqualTo(MarkupParseErrorKind.Contract));
            Assert.That(root.Diagnostics[0].Message, Does.Contain("value").And.Contain("默认值"));
            Assert.That(root.Children.OfType<SingleMarkerNode>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Parse_AllowsExplicitDefaultWhenAttributeAllowsIt()
        {
            RootNode root = TextInfoMarkupParser.Parse(
                "<wave : speed = 0, label = \"\">text</wave><wave:play(clip:\"\", loop:false)>");

            Assert.That(root.Diagnostics, Is.Empty);
            Assert.That(root.Children.OfType<BlockNode>().Count(), Is.EqualTo(1));
            Assert.That(root.Children.OfType<SingleMarkerNode>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Parse_RejectsFractionalValuesForIntegerTargets()
        {
            RootNode root = TextInfoMarkupParser.Parse(
                "<required : title = \"ok\", count = 1.5>x</required>" +
                "<required : title = \"ok\", count = 2>y</required>");

            Assert.That(root.Diagnostics[0].Kind, Is.EqualTo(MarkupParseErrorKind.Contract));
            Assert.That(root.Diagnostics[0].Message, Does.Contain("count"));
            Assert.That(root.Children.OfType<BlockNode>().Count(), Is.EqualTo(1));
            var sink = new RequiredSink();
            root.Children.OfType<BlockNode>().Single().PostProcessor = sink;
            PostProcessorExecute.Execute(sink, root);
            Assert.That(sink.ObservedCounts, Is.EqualTo(new[] { 2 }));
            Assert.That(sink.Count, Is.Zero);
            Assert.That(sink.Title, Is.Null);
        }

        [Test]
        public void Registry_ExposesDistinctIntegerAndFloatTypes()
        {
            Assert.That(MarkupRegistry.TryGetProperty("required", "count", out var integerType), Is.True);
            Assert.That(integerType, Is.EqualTo(MarkupDataType.Int));
            Assert.That(MarkupRegistry.TryGetProperty("wave", "speed", out var floatType), Is.True);
            Assert.That(floatType, Is.EqualTo(MarkupDataType.Float));
        }

        [Test]
        public void Execute_PreservesIntegerPropertiesAcrossNestedBlocks()
        {
            RootNode root = TextInfoMarkupParser.Parse(
                "<required : title = \"outer\", count = 2>a" +
                "<required : title = \"inner\", count = 3>b</required>c</required>");
            Assert.That(root.Diagnostics, Is.Empty);

            var outer = root.Children.OfType<BlockNode>().Single();
            var inner = outer.Children.OfType<BlockNode>().Single();
            var sink = new RequiredSink();
            outer.PostProcessor = sink;
            inner.PostProcessor = sink;

            PostProcessorExecute.Execute(sink, root);

            Assert.That(sink.ObservedCounts, Is.EqualTo(new[] { 2, 3, 2 }));
            Assert.That(sink.Count, Is.Zero);
        }

        [Test]
        public void Parse_ClosesUnfinishedBlocksAtEndOfInput()
        {
            RootNode rootNode = TextInfoMarkupParser.Parse("<wave><wave>text</wave>");

            var outer = (BlockNode)rootNode.Children[0];
            var inner = (BlockNode)outer.Children[0];
            Assert.That(outer.StartIndex, Is.EqualTo(0));
            Assert.That(outer.EndIndex, Is.EqualTo(4));
            Assert.That(inner.StartIndex, Is.EqualTo(0));
            Assert.That(inner.EndIndex, Is.EqualTo(4));
            Assert.That(rootNode.Diagnostics, Is.Empty);
        }

        [Test]
        public void Parse_ImplicitlyClosesNestedBlocksAndIgnoresOrphanEndTags()
        {
            RootNode rootNode = TextInfoMarkupParser.Parse("<wave><echo>text</wave></wave>");

            var outer = (BlockNode)rootNode.Children[0];
            var inner = (BlockNode)outer.Children[0];
            Assert.That(inner.EndIndex, Is.EqualTo(4));
            Assert.That(outer.EndIndex, Is.EqualTo(4));
            Assert.That(rootNode.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(rootNode.Diagnostics[0].Message, Does.Contain("孤立"));
        }

        [Test]
        public void Parse_UsesQuotedRecoveryBoundaryAndLeavesNonTagsAsContent()
        {
            RootNode rootNode = TextInfoMarkupParser.Parse("x<Wave : label = \"a > b\">y<  z>&<&&>");

            Assert.That(rootNode.Children, Has.Count.EqualTo(2));
            var content = (ContentNode)rootNode.Children[1];
            Assert.That(content.StartIndex, Is.GreaterThan(0));
            Assert.That(content.EndIndex, Is.EqualTo(12));
            Assert.That(rootNode.EndIndex, Is.EqualTo(12));
            Assert.That(rootNode.Diagnostics, Has.Count.EqualTo(3));
            Assert.That(rootNode.Diagnostics[0].Kind, Is.EqualTo(MarkupParseErrorKind.Lexical));
        }

        private static TMP_TextInfo CreateTextInfo(string text)
        {
            var textInfo = new TMP_TextInfo
            {
                characterCount = text.Length,
                characterInfo = new TMP_CharacterInfo[text.Length]
            };
            for (int index = 0; index < text.Length; index++)
            {
                textInfo.characterInfo[index].character = text[index];
                textInfo.characterInfo[index].index = index;
                textInfo.characterInfo[index].stringLength = 1;
            }

            return textInfo;
        }

        private static string GetText(TMP_TextInfo textInfo)
        {
            var characters = new char[textInfo.characterCount];
            for (int index = 0; index < characters.Length; index++)
                characters[index] = textInfo.characterInfo[index].character;

            return new string(characters);
        }

        private static string GetText(TextInfoRange range)
        {
            var characters = new char[range.CharacterCount];
            int index = 0;
            foreach (TMP_CharacterInfo characterInfo in range)
                characters[index++] = characterInfo.character;

            return new string(characters);
        }
    }
}
