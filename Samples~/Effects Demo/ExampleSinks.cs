using System;
using System.Collections;
using System.Collections.Generic;
using TextPipeline.Postprocess;
using TMPro;
using UnityEngine;

namespace TextPipeline.Samples
{
    public static class EffectsDemoTrace
    {
        private static readonly List<string> Lines = new();
        private static float _startedAt;
        public static event Action Changed;

        public static string Text => string.Join("\n", Lines);

        public static void Clear()
        {
            Lines.Clear();
            _startedAt = Time.realtimeSinceStartup;
            Changed?.Invoke();
        }

        public static void Record(string message)
        {
            if (Lines.Count == 48)
                Lines.RemoveAt(0);
            Lines.Add($"+{Time.realtimeSinceStartup - _startedAt:0.00}s  {message}");
            Changed?.Invoke();
            Debug.Log("[Effects Demo] " + message);
        }
    }

    public static class EffectsDemoLifetime
    {
        private struct State
        {
            public int InstanceId;
            public int Run;
            public float Age;
            public bool Running;
        }

        private static readonly Dictionary<string, State> States = new();

        public static string Summary => Format("scope 0") + "\n" + Format("scope 1");

        public static void Clear()
        {
            States.Clear();
        }

        public static void StopAll()
        {
            var labels = new List<string>(States.Keys);
            foreach (var label in labels)
            {
                var state = States[label];
                state.Running = false;
                States[label] = state;
            }
        }

        public static void Report(string label, int instanceId, int run, float age, bool running)
        {
            States[label] = new State
            {
                InstanceId = instanceId,
                Run = run,
                Age = age,
                Running = running
            };
        }

        private static string Format(string label)
        {
            if (!States.TryGetValue(label, out var state))
                return label + "  waiting";
            return $"{label}  #{state.InstanceId}  run {state.Run}  age {state.Age:0.0}s  " +
                (state.Running ? "RUNNING" : "STOPPED");
        }
    }

    [Markup("lifetime")]
    public sealed class LifetimeSink : ITextSinkCoroutine
    {
        private static int _nextInstanceId;
        private readonly int _instanceId = ++_nextInstanceId;
        private int _runCount;
        private float _age;
        private string _lastLabel;

        public LifetimeSink()
        {
            EffectsDemoTrace.Record($"lifetime #{_instanceId} created");
        }

        [MarkupProperty("label", MarkupDataType.String)]
        public string Label { get; set; } = "scope 0";

        [MarkupProperty("speed", MarkupDataType.Float)]
        public float Speed { get; set; } = 0.6f;

        public ITextPipeline TextPipeline { get; set; }

        public IEnumerator PostProcess(TextSegment[] segments)
        {
            var run = ++_runCount;
            EffectsDemoTrace.Record($"lifetime #{_instanceId} run {run} begin (age {_age:0.0}s)");
            try
            {
                while (true)
                {
                    _age += Time.deltaTime;
                    foreach (var segment in segments)
                        segment.DoEffect(this, Paint);
                    TextPipeline.textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                    yield return null;
                }
            }
            finally
            {
                if (_lastLabel != null)
                    EffectsDemoLifetime.Report(_lastLabel, _instanceId, run, _age, false);
                EffectsDemoTrace.Record($"lifetime #{_instanceId} run {run} stopped (age {_age:0.0}s)");
            }
        }

        private void Paint(TextInfoRange range)
        {
            if (_lastLabel != Label)
            {
                _lastLabel = Label;
                EffectsDemoTrace.Record($"lifetime #{_instanceId} bound to {Label}");
            }

            EffectsDemoLifetime.Report(Label, _instanceId, _runCount, _age, true);
            var bright = 0.3f + 0.7f * (0.5f + 0.5f *
                Mathf.Sin(_age * Mathf.Max(0.01f, Speed) * Mathf.PI * 2f));
            var accent = Label == "scope 0"
                ? new Color(0.15f, 0.8f, 1f)
                : new Color(1f, 0.55f, 0.17f);
            var tint = (Color32)Color.Lerp(new Color(0.12f, 0.16f, 0.22f), accent, bright);

            for (var i = 0; i < range.CharacterCount; i++)
            {
                var character = range.CharacterAt(i);
                if (!character.isVisible) continue;
                var colors = TextPipeline.textMesh.textInfo.meshInfo[character.materialReferenceIndex].colors32;
                for (var corner = 0; corner < 4; corner++)
                {
                    tint.a = colors[character.vertexIndex + corner].a;
                    colors[character.vertexIndex + corner] = tint;
                }
            }
        }
    }

    [Markup("audit")]
    public sealed class AuditSink : ITextSinkCoroutine
    {
        private static int _nextInstanceId;
        private readonly int _instanceId = ++_nextInstanceId;
        private int _runCount;

        public AuditSink()
        {
            EffectsDemoTrace.Record($"audit #{_instanceId} created");
        }

        [MarkupProperty("label", MarkupDataType.String)]
        public string Label { get; set; } = "default";

        [MarkupProperty("strength", MarkupDataType.Float)]
        public float Strength { get; set; } = 0.5f;

        [MarkupProperty("count", MarkupDataType.Int)]
        public int Count { get; set; } = 1;

        [MarkupProperty("enabled", MarkupDataType.Boolean)]
        public bool Enabled { get; set; } = true;

        public ITextPipeline TextPipeline { get; set; }

        [MarkupMethod("mark")]
        public void Mark([MarkupParam("message", MarkupDataType.String)] string message = "default")
        {
            EffectsDemoTrace.Record($"audit #{_instanceId} mark: {message} (label={Label})");
        }

        public IEnumerator PostProcess(TextSegment[] segments)
        {
            var run = ++_runCount;
            EffectsDemoTrace.Record($"audit #{_instanceId} run {run} begin");
            var completed = false;
            try
            {
                foreach (var segment in segments)
                    yield return segment.DoEffect(this, Pulse);
                completed = true;
            }
            finally
            {
                EffectsDemoTrace.Record($"audit #{_instanceId} run {run} " +
                    (completed ? "end" : "cancelled"));
            }
        }

        private IEnumerator Pulse(TextInfoRange range)
        {
            var content = new char[range.CharacterCount];
            for (var i = 0; i < range.CharacterCount; i++)
                content[i] = range.CharacterAt(i).character;

            EffectsDemoTrace.Record(
                $"audit #{_instanceId} {Label} begin strength={Strength:0.##} count={Count} enabled={Enabled}: {new string(content)}");

            if (!Enabled)
            {
                EffectsDemoTrace.Record($"audit #{_instanceId} {Label} skipped");
                yield break;
            }

            var cool = new Color(0.55f, 0.78f, 1f);
            var warm = new Color(1f, 0.65f, 0.2f);
            var cycles = Mathf.Max(1, Count);
            var duration = 0.55f * cycles;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                var phase = elapsed / duration * cycles * Mathf.PI * 2f;
                var pulse = 0.2f + 0.8f * (0.5f - 0.5f * Mathf.Cos(phase));
                Paint(range, Color.Lerp(cool, warm, Mathf.Clamp01(Strength) * pulse));
                elapsed += Time.deltaTime;
                yield return null;
            }

            Paint(range, Color.Lerp(cool, warm, Mathf.Clamp01(Strength)));
            EffectsDemoTrace.Record($"audit #{_instanceId} {Label} end");
        }

        private void Paint(TextInfoRange range, Color tint)
        {
            var color = (Color32)tint;
            for (var i = 0; i < range.CharacterCount; i++)
            {
                var character = range.CharacterAt(i);
                if (!character.isVisible) continue;
                var colors = TextPipeline.textMesh.textInfo.meshInfo[character.materialReferenceIndex].colors32;
                for (var corner = 0; corner < 4; corner++)
                {
                    color.a = colors[character.vertexIndex + corner].a;
                    colors[character.vertexIndex + corner] = color;
                }
            }

            TextPipeline.textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }

    [Markup("serial")]
    public sealed class SerialRevealSink : ITextSinkCoroutine
    {
        [MarkupProperty("label", MarkupDataType.String)]
        public string Label { get; set; } = "segment";

        [MarkupProperty("frames", MarkupDataType.Int)]
        public int Frames { get; set; } = 30;

        public ITextPipeline TextPipeline { get; set; }

        public IEnumerator PostProcess(TextSegment[] segments)
        {
            foreach (var segment in segments)
                yield return segment.DoEffect(this,
                    range => RevealAnimation.Run(TextPipeline, range, "serial", Label, Frames));
        }
    }

    [Markup("parallel")]
    public sealed class ParallelRevealSink : ITextSinkCoroutine
    {
        [MarkupProperty("label", MarkupDataType.String)]
        public string Label { get; set; } = "segment";

        [MarkupProperty("frames", MarkupDataType.Int)]
        public int Frames { get; set; } = 30;

        public ITextPipeline TextPipeline { get; set; }

        public IEnumerator PostProcess(TextSegment[] segments)
        {
            var runs = new IEnumerator[segments.Length];
            for (var i = 0; i < segments.Length; i++)
                runs[i] = segments[i].DoEffect(this, Reveal);

            try
            {
                var active = runs.Length;
                while (active > 0)
                {
                    for (var i = 0; i < runs.Length; i++)
                    {
                        if (runs[i] == null) continue;
                        if (runs[i].MoveNext()) continue;
                        (runs[i] as IDisposable)?.Dispose();
                        runs[i] = null;
                        active--;
                    }

                    TextPipeline.textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                    if (active > 0)
                        yield return null;
                }
            }
            finally
            {
                foreach (var run in runs)
                    (run as IDisposable)?.Dispose();
            }
        }

        private IEnumerator Reveal(TextInfoRange range)
        {
            return RevealAnimation.Run(TextPipeline, range, "parallel", Label, Frames);
        }
    }

    internal static class RevealAnimation
    {
        public static IEnumerator Run(ITextPipeline pipeline, TextInfoRange range,
            string mode, string label, int frames)
        {
            EffectsDemoTrace.Record($"{mode} {label} begin ({frames} frames/char)");
            for (var i = 0; i < range.CharacterCount; i++)
                SetAlpha(pipeline, range.CharacterAt(i), 0);
            pipeline.textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            yield return null;

            for (var i = 0; i < range.CharacterCount; i++)
            {
                for (var frame = 0; frame < Mathf.Max(1, frames); frame++)
                    yield return null;
                SetAlpha(pipeline, range.CharacterAt(i), 255);
                pipeline.textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }

            EffectsDemoTrace.Record($"{mode} {label} end");
        }

        private static void SetAlpha(ITextPipeline pipeline, TMP_CharacterInfo character, byte alpha)
        {
            if (!character.isVisible) return;
            var colors = pipeline.textMesh.textInfo.meshInfo[character.materialReferenceIndex].colors32;
            for (var corner = 0; corner < 4; corner++)
                colors[character.vertexIndex + corner].a = alpha;
        }
    }

    [Markup("typer")]
    public sealed class TyperSink : ITextSinkCoroutine
    {
        [MarkupProperty("speed", MarkupDataType.Float)]
        public float Speed { get; set; } = 18f;

        public ITextPipeline TextPipeline { get; set; }

        [MarkupMethod("pause")]
        public IEnumerator Pause([MarkupParam("seconds", MarkupDataType.Float)] float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, seconds));
        }

        public IEnumerator PostProcess(TextSegment[] segments)
        {
            var text = TextPipeline.textMesh;
            text.maxVisibleCharacters = 0;
            foreach (var segment in segments)
                yield return segment.DoEffect(this, Reveal);
        }

        private IEnumerator Reveal(TextInfoRange range)
        {
            for (var i = 0; i < range.CharacterCount; i++)
            {
                TextPipeline.textMesh.maxVisibleCharacters++;
                yield return new WaitForSeconds(1f / Mathf.Max(1f, Speed));
            }
        }
    }

    [Markup("wave")]
    public sealed class WaveSink : ITextSinkCoroutine
    {
        [MarkupProperty("amplitude", MarkupDataType.Float)]
        public float Amplitude { get; set; } = 8f;

        [MarkupProperty("frequency", MarkupDataType.Float)]
        public float Frequency { get; set; } = 0.4f;

        [MarkupProperty("speed", MarkupDataType.Float)]
        public float Speed { get; set; } = 4f;

        public ITextPipeline TextPipeline { get; set; }
        private TMP_MeshInfo[] _original;

        public IEnumerator PostProcess(TextSegment[] segments)
        {
            var text = TextPipeline.textMesh;
            _original = text.textInfo.CopyMeshInfoVertexData();
            while (true)
            {
                for (var m = 0; m < _original.Length; m++)
                    Array.Copy(_original[m].vertices, text.textInfo.meshInfo[m].vertices, _original[m].vertices.Length);
                foreach (var segment in segments)
                    yield return segment.DoEffect(this, Apply);
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                yield return null;
            }
        }

        private IEnumerator Apply(TextInfoRange range)
        {
            var time = Time.time * Speed;
            for (var i = 0; i < range.CharacterCount; i++)
            {
                var character = range.CharacterAt(i);
                if (!character.isVisible) continue;
                var offset = Vector3.up * (Mathf.Sin(time + i * Frequency) * Amplitude);
                var vertices = TextPipeline.textMesh.textInfo.meshInfo[character.materialReferenceIndex].vertices;
                for (var corner = 0; corner < 4; corner++)
                    vertices[character.vertexIndex + corner] += offset;
            }
            yield break;
        }
    }

    [Markup("shake")]
    public sealed class ShakeSink : ITextSinkCoroutine
    {
        [MarkupProperty("intensity", MarkupDataType.Float)]
        public float Intensity { get; set; } = 2f;

        public ITextPipeline TextPipeline { get; set; }
        private TMP_MeshInfo[] _original;

        public IEnumerator PostProcess(TextSegment[] segments)
        {
            var text = TextPipeline.textMesh;
            _original = text.textInfo.CopyMeshInfoVertexData();
            while (true)
            {
                for (var m = 0; m < _original.Length; m++)
                    Array.Copy(_original[m].vertices, text.textInfo.meshInfo[m].vertices, _original[m].vertices.Length);
                foreach (var segment in segments)
                    segment.DoEffect(this, Apply);
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                yield return null;
            }
        }

        private void Apply(TextInfoRange range)
        {
            for (var i = 0; i < range.CharacterCount; i++)
            {
                var character = range.CharacterAt(i);
                if (!character.isVisible) continue;
                var offset = new Vector3(UnityEngine.Random.Range(-Intensity, Intensity),
                    UnityEngine.Random.Range(-Intensity, Intensity));
                var vertices = TextPipeline.textMesh.textInfo.meshInfo[character.materialReferenceIndex].vertices;
                for (var corner = 0; corner < 4; corner++)
                    vertices[character.vertexIndex + corner] += offset;
            }
        }
    }

    [Markup("rainbow")]
    public sealed class RainbowSink : ITextSinkCoroutine
    {
        [MarkupProperty("speed", MarkupDataType.Float)]
        public float Speed { get; set; } = 0.2f;

        [MarkupProperty("spread", MarkupDataType.Float)]
        public float Spread { get; set; } = 0.08f;

        public ITextPipeline TextPipeline { get; set; }
        private TMP_MeshInfo[] _original;

        public IEnumerator PostProcess(TextSegment[] segments)
        {
            var text = TextPipeline.textMesh;
            _original = text.textInfo.CopyMeshInfoVertexData();
            while (true)
            {
                for (var m = 0; m < _original.Length; m++)
                    Array.Copy(_original[m].colors32, text.textInfo.meshInfo[m].colors32, _original[m].colors32.Length);
                foreach (var segment in segments)
                    yield return segment.DoEffect(this, Apply);
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                yield return null;
            }
        }

        private IEnumerator Apply(TextInfoRange range)
        {
            for (var i = 0; i < range.CharacterCount; i++)
            {
                var character = range.CharacterAt(i);
                if (!character.isVisible) continue;
                var color = (Color32)Color.HSVToRGB(Mathf.Repeat(Time.time * Speed + i * Spread, 1f), 0.85f, 1f);
                var colors = TextPipeline.textMesh.textInfo.meshInfo[character.materialReferenceIndex].colors32;
                for (var corner = 0; corner < 4; corner++)
                {
                    color.a = colors[character.vertexIndex + corner].a;
                    colors[character.vertexIndex + corner] = color;
                }
            }
            yield break;
        }
    }
}
