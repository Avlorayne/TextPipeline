using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using TextPipeline.Postprocess;
using Unity.Profiling;

namespace TextPipeline.Editor.Tests
{
    [Category("Performance")]
    public sealed class MarkupRegistryPerformanceTests
    {
        private const int HotPathIterations = 100000;
        private const int AllocationIterations = 1000;
        private const int WarmupIterations = 1000;
        private const int SampleCount = 5;
        private static float _observed;

        [Markup("performanceprobe")]
        private sealed class BenchmarkSink : ITextSinkBase
        {
            [MarkupProperty("speed", MarkupDataType.Float)]
            public float Speed { get; set; } = 0.25f;

            public float LastDelay { get; private set; }

            public TextPipeline TextPipeline { get; set; }

            [MarkupMethod("delay")]
            public void Delay([MarkupParam("seconds", MarkupDataType.Float)] float seconds)
            {
                LastDelay = seconds;
            }
        }

        [SetUp]
        public void RegisterBenchmarkSink()
        {
            MarkupRegistry.RegisterMarkups(new[] { typeof(BenchmarkSink).Assembly });
        }

        [Test]
        public void FloatBoxingAndUnboxing_ReportsTimeAndGc()
        {
            const float value = 0.75f;
            object boxedValue = value;

            Report("float / typed call", () => _observed = ConsumeFloat(value));
            Report("float / unbox preboxed object", () => _observed = UnboxFloat(boxedValue));
            var boxedBytes = Report("float / box each call then unbox", () => _observed = UnboxFloat(value));
            Assert.That(boxedBytes, Is.GreaterThan(0), "GC.Alloc must detect the explicit boxing allocation.");
            Assert.That(_observed, Is.EqualTo(value));
        }

        [Test]
        public void PropertyGet_ReportsTimeAndGcForDirectReflectionAndRegistry()
        {
            var sink = new BenchmarkSink();
            var property = typeof(BenchmarkSink).GetProperty(nameof(BenchmarkSink.Speed));
            Assert.That(property, Is.Not.Null);
            Assert.That((float)MarkupRegistry.Get(sink, "speed"), Is.EqualTo(sink.Speed));

            Report("property get / direct", () => _observed = sink.Speed);
            Report("property get / PropertyInfo.GetValue", () => _observed = (float)property.GetValue(sink));
            Report("property get / MarkupRegistry.Get", () => _observed = (float)MarkupRegistry.Get(sink, "speed"));
            Assert.That(_observed, Is.EqualTo(sink.Speed));
        }

        [Test]
        public void PropertySet_ReportsBoxingCostSeparately()
        {
            var sink = new BenchmarkSink();
            var property = typeof(BenchmarkSink).GetProperty(nameof(BenchmarkSink.Speed));
            Assert.That(property, Is.Not.Null);
            const float value = 0.75f;
            object boxedValue = value;

            Report("property set / direct", () => sink.Speed = value);
            Report("property set / PropertyInfo.SetValue, preboxed", () => property.SetValue(sink, boxedValue));
            Report("property set / PropertyInfo.SetValue, box each call", () => property.SetValue(sink, value));
            Report("property set / MarkupRegistry.Set, preboxed", () => MarkupRegistry.Set(sink, "speed", boxedValue));
            Report("property set / MarkupRegistry.Set, box each call", () => MarkupRegistry.Set(sink, "speed", value));
            Assert.That(sink.Speed, Is.EqualTo(value));
        }

        [Test]
        public void MethodInvoke_ReportsArgumentAndNamedLookupAllocations()
        {
            var sink = new BenchmarkSink();
            var method = typeof(BenchmarkSink).GetMethod(nameof(BenchmarkSink.Delay));
            Assert.That(method, Is.Not.Null);
            const float value = 0.75f;
            object boxedValue = value;
            var arguments = new[] { boxedValue };
            var namedArguments = new[]
                { new System.Collections.Generic.KeyValuePair<string, object>("seconds", boxedValue) };

            Report("method invoke / direct", () => sink.Delay(value));
            Report("method invoke / MethodInfo.Invoke, reused arguments", () => method.Invoke(sink, arguments));
            Report("method invoke / MarkupRegistry.Invoke, reused arguments",
                () => MarkupRegistry.Invoke(sink, "delay", arguments));
            Report("method invoke / MarkupRegistry.Invoke, new boxed arguments", () =>
                MarkupRegistry.Invoke(sink, "delay", new object[] { value }));
            Report("method invoke / MarkupRegistry.Invoke, named arguments", () =>
                MarkupRegistry.Invoke(sink, "delay", namedArguments));
            Assert.That(sink.LastDelay, Is.EqualTo(value));
        }

        [Test]
        public void RegisterMarkups_ReportsRebuildTimeAndGc()
        {
            var assemblies = new[] { typeof(BenchmarkSink).Assembly };
            Report("registration / rebuild test assembly", () => MarkupRegistry.RegisterMarkups(assemblies), 1, 1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ConsumeFloat(float value) => value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float UnboxFloat(object value) => (float)value;

        // Results are diagnostic: machine load, Mono/JIT and Editor version affect elapsed time.
        // Time and GC.Alloc are sampled separately so profiling does not skew timings.
        private static double Report(string label, Action operation, int iterations = HotPathIterations,
            int warmup = WarmupIterations)
        {
            for (var i = 0; i < warmup; i++) operation();

            var nanoseconds = new double[SampleCount];
            var allocatedBytes = new double[SampleCount];
            var allocationCounts = new double[SampleCount];
            var stopwatch = new Stopwatch();
            for (var sample = 0; sample < SampleCount; sample++)
            {
                GC.Collect();
                stopwatch.Reset();
                stopwatch.Start();
                for (var i = 0; i < iterations; i++) operation();
                stopwatch.Stop();
                nanoseconds[sample] = stopwatch.Elapsed.TotalMilliseconds * 1000000.0 / iterations;
            }

            var allocationIterations = Math.Min(iterations, AllocationIterations);
            for (var sample = 0; sample < SampleCount; sample++)
            {
                GC.Collect();
                using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                           ProfilerRecorderOptions.SumAllSamplesInFrame |
                           ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    for (var i = 0; i < allocationIterations; i++) operation();
                    recorder.Stop();
                    if (recorder.Count == 0) continue;
                    var allocationSample = recorder.GetSample(0);
                    allocatedBytes[sample] = (double)allocationSample.Value / allocationIterations;
                    allocationCounts[sample] = (double)allocationSample.Count / allocationIterations;
                }
            }

            Array.Sort(nanoseconds);
            Array.Sort(allocatedBytes);
            Array.Sort(allocationCounts);
            TestContext.WriteLine($"{label}: median {nanoseconds[SampleCount / 2]:F1} ns/op, " +
                                  $"GC.Alloc {allocatedBytes[SampleCount / 2]:F2} B/op, " +
                                  $"{allocationCounts[SampleCount / 2]:F2} allocations/op " +
                                  $"({iterations} timed and {allocationIterations} allocation operations x " +
                                  $"{SampleCount} samples)");
            return allocatedBytes[SampleCount / 2];
        }
    }
}