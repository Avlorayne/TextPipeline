using UnityEngine;

namespace TextPipeline.Samples
{
    /// <summary>First preprocessing step: expand authored variables, including effect shorthand.</summary>
    public sealed class DemoVariablesSource : MonoBehaviour, ITextSource
    {
        public string playerName = "Ada";
        private ITextPipeline _pipeline;

        public ITextPipeline TextPipeline { set => _pipeline = value; }

        public string PreProcess(string text)
        {
            text ??= string.Empty;
            var result = text.Replace("{{player}}", playerName)
                .Replace("{{effect}}", "[[wave]]Sources run before sinks.[[/wave]]");
            EffectsDemoTrace.Record($"{_pipeline.GetType().Name} SOURCE 1 / variables: {text} -> {result}");
            return result;
        }
    }
}
