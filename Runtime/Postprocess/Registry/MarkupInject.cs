using System.Collections;
using System.Collections.Generic;

namespace TextPipeline.Postprocess
{
    public static class MarkupInject
    {
        public static Dictionary<string, object> GetSinkSnapshot(ITextSinkBase sink)
        {
            string[] parameters = MarkupRegistry.LookupForProperties(sink.GetType());
            Dictionary<string, object> snapshot = new();
            foreach (var param in parameters)
            {
                var value = MarkupRegistry.Get(sink, param);
                snapshot.Add(param, value);
            }

            return snapshot;
        }

        public static void InjectBySnapShot(ITextSinkBase sink, IReadOnlyDictionary<string, object> snapshot)
        {
            foreach (var pair in snapshot)
            {
                MarkupRegistry.Set(sink, pair.Key, pair.Value);
            }
        }

        public static object MethodInjectWithInvoke(ITextSinkBase sink, string method,
            KeyValuePair<string, object>[] arguments)
        {
            return MarkupRegistry.Invoke(sink, method, arguments);
        }
    }
}
