using UnityEngine;

namespace TextPipeline.Samples
{
    /// <summary>Second preprocessing step: turn the first step's shorthand into pipeline markup.</summary>
    public sealed class DemoMarkupSource : MonoBehaviour, ITextSource
    {
        private ITextPipeline _pipeline;

        public ITextPipeline TextPipeline { set => _pipeline = value; }

        public string PreProcess(string text)
        {
            text ??= string.Empty;
            var result = text.Replace("[[wave]]", "<wave : amplitude = 6, frequency = 0.45, speed = 4>")
                .Replace("[[/wave]]", "</wave>");
            EffectsDemoTrace.Record($"{_pipeline.GetType().Name} SOURCE 2 / markup: {text} -> {result}");
            return result;
        }
    }
}
