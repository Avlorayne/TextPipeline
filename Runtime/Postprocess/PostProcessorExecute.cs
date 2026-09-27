using System;
using System.Collections.Generic;
using System.Reflection;

namespace TextPipeline.Postprocess
{
    public static class PostProcessorExecute
    {
        public static void Execute(ITextSinkBase sink, RootNode root)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (root == null) throw new ArgumentNullException(nameof(root));

            var initialProperties = MarkupInject.GetSinkSnapshot(sink); // 这里有完整的快照作为基础

            // Collect Related Steps
            var steps = new List<TextSegment>();
            foreach (var child in root.Children)
                Visit(sink, child, initialProperties, steps, false);

            if (sink is ITextSink normalSink)
            {
                normalSink.PostProcess(steps.ToArray());
            }
            else if (sink is ITextSinkCoroutine coroutineSink)
            {
                sink.TextPipeline.StartSinkCoroutine(coroutineSink.PostProcess, steps.ToArray());
            }
        }

        private static void Visit(ITextSinkBase sink, Node node,
            Dictionary<PropertyInfo, object> properties, List<TextSegment> steps, bool isActive)
        {
            switch (node)
            {
                case BlockNode block:
                    if (block.PostProcessor == sink)
                    {
                        var inheritedProperties = new Dictionary<PropertyInfo, object>(properties);
                        foreach (var item in block.GetPropertyInfos())
                        {
                            if (!item.property.CanWrite || item.property.GetIndexParameters().Length > 0)
                                continue;
                            inheritedProperties[item.property] = item.dataType.ToValue(item.value); // 属性覆盖，未声明的属性继承自父块
                        }

                        foreach (var child in block.Children)
                            Visit(sink, child, inheritedProperties, steps, true);
                    }
                    else if (isActive || block.SignPost.Contains(sink))
                    {
                        foreach (var child in block.Children)
                            Visit(sink, child, properties, steps, isActive);
                    }

                    break;

                case SingleMarkerNode marker:
                    if (marker.PostProcessor == sink)
                    {
                        var method = marker.GetMethodInfo();
                        var arguments = MarkupInject.ResolveMethodArguments(method, marker.GetParameterInfos());
                        steps.Add(new TextSegment(properties, method, arguments));
                    }

                    break;

                case ContentNode content:
                    if (isActive)
                        steps.Add(new TextSegment(properties, content.Content));    // 所以在这里保存的properties是完整的
                    break;
            }
        }
    }
}
