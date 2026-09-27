using System;
using System.Collections.Generic;

namespace TextPipeline.Postprocess
{
    public class InstanceTreeBuilder
    {
        private readonly Dictionary<string, ITextSinkBase> _defaultInstance = new();
        private readonly Dictionary<(string markup, int scope), ITextSinkBase> _instanceCache = new();

        public ITextSinkBase[] Build(RootNode root)
        {
            _instanceCache.Clear();
            
            foreach (var child in root.Children)
                Visit(child);
            
            var instanceList = new List<ITextSinkBase>();
            foreach (var sink in _defaultInstance) instanceList.Add(sink.Value);
            foreach (var sink in _instanceCache) instanceList.Add(sink.Value);
            
            return instanceList.ToArray();
        }

        private ITextSinkBase GetInstance(string markup, int scope)
        {
            if(scope == 0 && _defaultInstance.TryGetValue(markup, out var sink0))
                return sink0;
            if(_instanceCache.TryGetValue((markup, scope), out var sink))
                return sink;
            
            Type type = MarkupRegistry.LookupForType(markup);
            sink = Activator.CreateInstance(type) as ITextSinkBase;
            
            if(scope == 0)
                _defaultInstance.Add(markup, sink);
            else
                _instanceCache.Add((markup, scope), sink);
            
            return sink;
        }
        
        private ITextSinkBase[] Visit(Node node)
        {
            switch (node)
            {
                case BlockNode block:
                {
                    // 进入处理
                    var instance = GetInstance(block.Markup, block.ScopeNum);
                    block.PostProcessor = instance;
                    // 开始递归
                    foreach (var child in block.Children)
                    {
                        var relatedSinks =  Visit(child);
                        foreach (var sink in relatedSinks)
                        {
                            if(block.SignPost.Contains(sink)) continue;
                            block.SignPost.Add(sink);
                        }
                    }
                    // 递归完成，回到本层
                    if(!block.SignPost.Contains(instance)) 
                        block.SignPost.Add(instance);
                    return block.SignPost.ToArray();
                }
                case SingleMarkerNode markup:
                {
                    // 进入处理
                    var instance =  GetInstance(markup.Markup, markup.ScopeNum);
                    markup.PostProcessor = instance;
                    return new[] { instance };
                }
                default:
                    return Array.Empty<ITextSinkBase>();
            }
        }
    }
}
