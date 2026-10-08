using System;
using System.Linq;
using TextPipeline.Postprocess;
using TMPro;

namespace TextPipeline
{
    /// <summary>两种宿主共用的解析、实例生命周期和效果执行流程。</summary>
    internal sealed class TextPipelinePostprocessor
    {
        private readonly InstanceTreeBuilder _instanceTreeBuilder = new();

        public void Process(ITextPipeline host, string authoringText, Action<string> setTextAndUpdate)
        {
            setTextAndUpdate(authoringText);
            MarkupParsePlan plan = TextInfoMarkupParser.Analyse(authoringText, host.textMesh.textInfo);
            setTextAndUpdate(plan.FormalisedText);
            RootNode tree = plan.Bind(host.textMesh.textInfo);

            var sinks = SortSinks(_instanceTreeBuilder.Build(tree));
            foreach (var sink in sinks)
                sink.TextPipeline = host;
            foreach (var sink in sinks)
                PostProcessorExecute.Execute(sink, tree);

            host.textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        internal static ITextSinkBase[] SortSinks(ITextSinkBase[] sinks)
        {
            if (sinks == null || sinks.Length == 0) return Array.Empty<ITextSinkBase>();
            var sinkOrder = TextPipelineSettings.Instance.TextSinks;
            var unconfiguredTypes = sinks
                .Where(sink => sink == null || !sinkOrder.ContainsKey(sink.GetType()))
                .Select(sink => sink?.GetType().FullName ?? "<null>")
                .Distinct()
                .ToArray();
            if (unconfiguredTypes.Length > 0)
                throw new InvalidOperationException(
                    "TextPipeline 解析到了未在 TextPipelineSettings 的 Sink Sequence 中配置的 Sink 类型：" +
                    string.Join(", ", unconfiguredTypes) +
                    "。请在 Project Settings > Text Pipeline 中添加这些类型并设置执行顺序。");
            return sinks.OrderBy(sink => sinkOrder[sink.GetType()]).ToArray();
        }
    }
}
