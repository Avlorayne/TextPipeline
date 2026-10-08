using NUnit.Framework;
using TMPro;
using TextPipeline.Postprocess;

namespace TextPipeline.Editor.Tests
{
    public sealed class InstanceTreeBuilderTests
    {
        [Markup("instancea")]
        public sealed class InstanceASink : ITextSinkBase
        {
            public ITextPipeline TextPipeline { get; set; }
        }

        [Markup("instanceb")]
        public sealed class InstanceBSink : ITextSinkBase
        {
            public ITextPipeline TextPipeline { get; set; }
        }

        [SetUp]
        public void RegisterTestMarkups()
        {
            MarkupRegistry.RegisterMarkups(new[] { typeof(InstanceTreeBuilderTests).Assembly });
        }

        [Test]
        public void Build_BindsPostProcessorsAndAggregatesEveryBlockSignPost()
        {
            var outer = Block("instancea", 1);
            var outerMarker = Marker("instancea", 1);
            var inner = Block("instanceb", 1);
            var innerSharedTypeMarker = Marker("instanceb", 1);
            var innerOuterTypeMarker = Marker("instancea", 1);
            var innerDifferentScopeMarker = Marker("instanceb", 2);
            var sibling = Block("instanceb", 1);
            var siblingMarker = Marker("instanceb", 1);

            outer.Children.Add(outerMarker);
            outer.Children.Add(inner);
            inner.Children.Add(innerSharedTypeMarker);
            inner.Children.Add(innerOuterTypeMarker);
            inner.Children.Add(innerDifferentScopeMarker);
            sibling.Children.Add(siblingMarker);

            var root = new RootNode();
            root.Children.Add(outer);
            root.Children.Add(sibling);

            new InstanceTreeBuilder().Build(root);

            Assert.That(outer.PostProcessor, Is.TypeOf<InstanceASink>());
            Assert.That(inner.PostProcessor, Is.TypeOf<InstanceBSink>());
            Assert.That(innerDifferentScopeMarker.PostProcessor, Is.TypeOf<InstanceBSink>());

            Assert.That(outerMarker.PostProcessor, Is.SameAs(outer.PostProcessor));
            Assert.That(innerOuterTypeMarker.PostProcessor, Is.SameAs(outer.PostProcessor));
            Assert.That(innerSharedTypeMarker.PostProcessor, Is.SameAs(inner.PostProcessor));
            Assert.That(sibling.PostProcessor, Is.SameAs(inner.PostProcessor));
            Assert.That(siblingMarker.PostProcessor, Is.SameAs(inner.PostProcessor));
            Assert.That(innerDifferentScopeMarker.PostProcessor, Is.Not.SameAs(inner.PostProcessor));

            CollectionAssert.AreEqual(new[]
            {
                inner.PostProcessor,
                outer.PostProcessor,
                innerDifferentScopeMarker.PostProcessor
            }, inner.SignPost);
            CollectionAssert.AreEqual(new[]
            {
                outer.PostProcessor,
                inner.PostProcessor,
                innerDifferentScopeMarker.PostProcessor
            }, outer.SignPost);
            CollectionAssert.AreEqual(new[] { sibling.PostProcessor }, sibling.SignPost);
        }

        private static BlockNode Block(string markup, int scope)
        {
            return new BlockNode { Markup = markup, ScopeNum = scope };
        }

        private static SingleMarkerNode Marker(string markup, int scope)
        {
            return new SingleMarkerNode { Markup = markup, ScopeNum = scope };
        }
    }
}
