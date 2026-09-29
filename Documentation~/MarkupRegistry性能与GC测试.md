# MarkupRegistry 性能与 GC 测试

测试代码：[MarkupRegistryPerformanceTests.cs](../Tests/Editor/MarkupRegistryPerformanceTests.cs)。它比较直接调用、`PropertyInfo` 反射和 `MarkupRegistry` 热路径，并单独比较预装箱与每次装箱；注册表重建另测。

## 如何运行

在 Unity 中打开 **Window > General > Test Runner**，选择 **EditMode**，搜索 `TextPipeline.Editor.Tests.MarkupRegistryPerformanceTests` 并运行该测试类。选中每个用例查看输出；每行包含 `ns/op`、`GC.Alloc B/op` 和 `allocations/op`。测试只在 Editor 且定义 `UNITY_INCLUDE_TESTS` 时编译。

也可以在 PowerShell 中用同版本 Unity 批处理运行。先关闭正在使用该项目的 Editor，或像本次一样在项目副本上运行，避免项目锁冲突。路径按本机实际位置替换：

```powershell
$unityExe = 'C:\Program Files TMP\Unity Editor\2022.3.62f3c1\Editor\Unity.exe'
$project = 'F:\Project\Unity\TextPipeline'
$result = Join-Path $project 'MarkupRegistryPerformanceResults.xml'
$log = Join-Path $project 'MarkupRegistryPerformance.log'

& $unityExe -batchmode -nographics -projectPath $project `
    -runTests -testPlatform EditMode `
    -testFilter 'TextPipeline.Editor.Tests.MarkupRegistryPerformanceTests' `
    -testResults $result -logFile $log
```

运行完毕后，查看 XML 中各 `<test-case>` 的 `<output>`。**不要在这条测试命令后加 `-quit`**：本次首次批处理在完成导入后直接退出，没有生成测试结果；去掉 `-quit` 后才实际执行了测试。

## 指标怎样测

每个热路径先预热 1000 次。计时使用 `Stopwatch`：每组连续调用 100000 次，重复 5 组，输出每次调用耗时的中位数。GC 分配另开采样窗口：每组调用 1000 次，重复 5 组，使用 `ProfilerRecorder` 记录当前线程的 `GC.Alloc`；`ProfilerRecorderSample.Value` 除以调用次数得到 `B/op`，`Count` 除以调用次数得到 `allocations/op`。注册表重建每组只调用 1 次。预热、构造测试对象和复用参数数组均在测量窗口外；`GC.Collect()` 也在每组采样前执行。

关键计数器配置如下；完整实现见测试代码：

```csharp
using var recorder = ProfilerRecorder.StartNew(
    ProfilerCategory.Internal, "GC.Alloc", 1,
    ProfilerRecorderOptions.SumAllSamplesInFrame |
    ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
// 在这里循环调用被测操作
recorder.Stop();
if (recorder.Count > 0)
{
    var sample = recorder.GetSample(0);
    // sample.Value 是累计字节数，sample.Count 是分配事件数
}
```

计时与 GC 采样分开，是为了避免 Profiler 记录开销混入 `ns/op`。`GC.Alloc` 表示托管对象的**分配**，不是实际 GC 暂停时间；`0 B/op` 也不代表这条路径没有 CPU 开销。测试中的“每次装箱再拆箱”必须测到正的分配量，用来验证计数器没有静默失效。[Unity 的 `ProfilerRecorder` 用法](https://docs.unity3d.com/cn/2022.3/ScriptReference/Unity.Profiling.ProfilerRecorder.StartNew.html)和[`GC.Alloc` 求和与当前线程选项](https://docs.unity3d.com/cn/2021.3/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.SumAllSamplesInFrame.html)说明了这个口径。

## 这次为什么测得曲折

1. 最初使用 `GC.GetAllocatedBytesForCurrentThread()`。在本项目的 Unity 2022.3 Editor Mono 中，它连显式装箱都报告 `0 B`。因此不能看到零就判断没有分配。
2. 随后试了 `Profiler.GetMonoUsedSizeLong()` 的前后差值。长窗口中装箱只显示约 `9 B/次`，短窗口又显示 `0 B`；堆的净增长不等于期间累计分配量，不能据此计算每次调用的 GC 压力。
3. UnitySkills 本地测试服务当时没有监听端口，于是改用 Unity 批处理。原项目已在 Editor 中打开，使用临时项目副本；第一次命令额外带了 `-quit`，导致导入成功却没有执行测试。去掉后运行正常。
4. 最终改用 `ProfilerRecorder` 的 `GC.Alloc`。显式装箱测得约 `28 B/次、1 次分配`，直接传值与预装箱拆箱均为 `0 B/次`，校验通过。5 个性能用例全部通过。

## 本次结果示例

Unity 2022.3.62f3c1、Windows、EditMode Mono；数值是 5 组采样的中位数，仅用于比较本次环境中的路径。

| 操作 | ns/op | GC.Alloc B/op | 分配次数/op |
| --- | ---: | ---: | ---: |
| `float` 直接传参 | 5.9 | 0 | 0 |
| 每次装箱再拆箱 | 33.5 | 28.0 | 1 |
| `PropertyInfo.GetValue` 读取 `float` | 44.6 | 26.7 | 1 |
| `MarkupRegistry.Get` 读取 `float` | 292.4 | 27.5 | 1 |
| `PropertyInfo.SetValue`，参数已装箱 | 643.5 | 29.0 | 1 |
| `PropertyInfo.SetValue`，每次装箱 | 706.5 | 57.0 | 2 |
| `MarkupRegistry.Set`，参数已装箱 | 248.4 | 0 | 0 |
| `MarkupRegistry.Set`，每次装箱 | 308.3 | 27.4 | 1 |
| `MethodInfo.Invoke`，复用参数数组 | 505.7 | 0 | 0 |
| `MarkupRegistry.Invoke`，复用参数数组 | 443.4 | 0 | 0 |
| `MarkupRegistry.Invoke`，每次新建并装箱参数 | 574.5 | 55.2 | 2 |
| `MarkupRegistry.Invoke`，命名参数 | 963.9 | 54.8 | 2 |

重建**测试程序集**的注册表约为 `6.4 ms`、`138.5 KB`、`5070` 次分配。它不等于扫描整个游戏项目的启动成本。Editor 与 Player、Mono 与 IL2CPP 的结果可能不同；要判断目标平台表现，应在目标设备的构建中另测。[Unity 2022.3 内存分析文档](https://docs.unity3d.com/cn/2022.3/Manual/ProfilerMemory.html)也说明 Editor 的内存数据可能高于目标设备构建。

本次结果显示，`MarkupRegistry.Get` 虽然通过缓存委托访问属性，但 `object` 返回值仍使 `float` 装箱；`Set` 的预装箱路径没有逐次分配，直接传入 `float` 则每次装箱。命名参数版 `Invoke` 即使复用输入数组，也会在内部解析参数时产生逐次分配。注册阶段的扫描和委托编译应与热路径分开评估。
