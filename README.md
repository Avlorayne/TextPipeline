# Text Pipeline

Text Pipeline 是 Unity 2022.3+ / TextMeshPro 3.0.7 的文本后处理包。策划在文本里写类 HTML 标签，程序用 Sink
类定义标签效果；管线会去掉自定义标签，并把对应的 TMP 字符范围交给效果代码。

## 安装和运行示例

在 Package Manager 选择 **Add package from Git URL...**，输入：

```text
https://github.com/Avlorayne/TextPipeline.git?path=/Packages/TextPipeline
```

也可以直接将 `Packages/TextPipeline` 作为本地包加入项目。包内 `Samples~/Effects Demo` 是可导入示例：在 Package Manager 的
Text Pipeline 页面导入 **Effects Demo**，打开导入后的 `EffectsDemo.unity` 并按
Play，即可看到持续视觉效果、同标签嵌套、作用域实例生命周期和段调度示例。左侧示例可滚动，右侧是事件时间线。顶部的 **Replay** 使用
Bootstrap
Inspector 中当前的母本文字重新构建， **Replay Scope** 只重建生命周期行， **Refresh** 使用各管线已保存的母本重新处理。导入内容自带
`Resources/Text TextPipeline Settings.asset`。首次打开项目设置或烘焙时，会自动迁移唯一的旧 Resources 配置。
如果项目设置已存在，请在示例配置的 Inspector 点击 **将此配置导入 Project Settings**（会替换当前顺序）；
若项目已有另一个同名 Resources 资产，导入后保留一个即可，多个同名配置会阻止 Play / 构建。

示例效果实现在 [ExampleSinks.cs](Samples~/Effects%20Demo/ExampleSinks.cs)
，场景入口在 [EffectsDemo.unity](Samples~/Effects%20Demo/EffectsDemo.unity)
，启动脚本在 [EffectsDemoBootstrap.cs](Samples~/Effects%20Demo/EffectsDemoBootstrap.cs)。八段示例文本可在场景中选中 **Text
Pipeline Effects Demo**，直接编辑 `Effects Demo Bootstrap` 的对应字段。前四段是可直接复制到 Bootstrap 字段或自己场景中的
TextPipeline 母本的视觉效果：

```xml
<typer : speed = 16>Hello, <typer : pause(0.4)>welcome to Text Pipeline.</typer>
<wave : amplitude = 9, frequency = 0.45, speed = 4>Every letter rides a wave.</wave>
<shake : intensity = 0.5>soft <shake : intensity = 14><color=#FF9F43><b>HARD SHAKE</b></color></shake> soft</shake>
<rainbow : speed = 0.18, spread = 0.08>Color travels through the text.</rainbow>
```

另外四段用于观察管线行为：

```xml
<lifetime : label = "scope 0", speed = 0.6 | scope = 0>KEEP AGE</lifetime>  <lifetime : label = "scope 1", speed = 0.6 | scope = 1>RESET AGE</lifetime>
<audit : label = "serial wrapper", strength = 0.4><serial : label = "A", frames = 30>ALPHA</serial>   <serial : label = "B", frames = 30>BETA</serial></audit>
<audit : label = "parallel wrapper", strength = 0.4><parallel : label = "A", frames = 30>ALPHA</parallel>   <parallel : label = "B", frames = 30>BETA</parallel></audit>
<audit : label = "outer", strength = 0.25, count = 2>outer <audit : label = "inner", strength = 1><b>inner</b></audit> outer<audit : mark("checkpoint")></audit>  <audit : label = "scope one" | scope = 1>scope 1</audit>  <audit : label = "sibling", enabled = false | scope = 0>sibling</audit><audit : mark>
```

### 预处理示例：PREPROCESS

左侧底部有两行 PREPROCESS，分别使用 `TextPipeline` 和 `TextPipelineUGUI`。输入母本为
`Hello, {{player}}! {{effect}}`，`Player Name` 默认为 `Ada`。每行同时显示输入和经过管线处理的文字。

Source Sequence 中 `DemoVariablesSource` 先替换玩家名，并把 `{{effect}}` 展开为
`[[wave]]Sources run before sinks.[[/wave]]`；`DemoMarkupSource` 再将简写展开为 `<wave ...>` 标签。
后处理去除自定义标签，并让 `Sources run before sinks.` 产生波浪动画。若颠倒这两个 Source 的顺序，变量展开后留下的简写将不会被解析为效果。
Execution Trace 会记录两步的输入和输出。两个 Source 都挂在文本对象上，并在管线首次启用前创建。

修改 Bootstrap 的 `Player Name` 或 `Preprocessing` 后点击 **Replay** 可提交新值； **Refresh**
从保存的母本重新跑预处理和后处理，母本中的占位符仍会保留。
示例的 `Editor/EffectsDemoSettingsSetup.cs` 会在导入后和进入 Play 时，通过编辑器设置 API 补充缺失的 Source 并重新烘焙；已有 Source/Sink 顺序会保留。
也可使用 **Tools > Text Pipeline > Configure Effects Demo Sources** 手动执行。不要只在 Unity 打开时修改 ProjectSettings 文件；编辑器中已加载的设置单例可能在下次烘焙时覆盖磁盘内容。

### 先看同标签嵌套：SHAKE

画面文字是 `soft HARD SHAKE soft`。外层 `<shake : intensity = 0.5>` 包住整句，左右两处 `soft` 都轻微抖动。中间再套同一个
`shake` 标签，
把 `intensity` 覆盖为 `14`，因此橙色加粗的 `HARD SHAKE` 会剧烈抖动。内层结束后，右侧 `soft` 恢复外层的 `0.5`；它不会继续使用
`14`。
橙色和加粗来自 TMP 的 `<color>`、`<b>`，抖动幅度来自 Text Pipeline 的属性注入。三个片段在同一个 Sink 实例中持续运行，方便直接比较属性覆盖与恢复。

### 再看实例生命周期：LIFETIME

`KEEP AGE` 指定 `scope = 0`，`RESET AGE` 指定 `scope = 1`，两段由 **同一标签类型的两个 Sink 实例**分别驱动。它们持续按自身累计
`age`
明暗脉冲。每段下方实时显示 `scope`、实例编号 `#`、运行次数 `run`、累计年龄 `age` 和运行状态。
等两边的 `age` 增长几秒后，点击 **Replay Scope**：两段文字冻结约 1.2 秒，状态显示 `STOPPED`，然后重新运行；左侧 `scope = 0`
复用原实例，`#` 不变、`run` 加一、
`age` 继续累计；
右侧 `scope = 1` 得到新实例，`#` 改变、`run` 从一开始、`age` 回到零。右侧 Execution Trace 记录创建、绑定、运行开始与停止的时间。
这里的“生命周期”指 Sink 实例的复用或重建，以及其协程的启动和停止；它不表示 Unity 对象的销毁回调。

`serial` 和 `parallel` 两行使用相同的 A/B 文字及每字等待帧数；前者完成 A 才开始 B，后者让两段交错推进。两行还把 `audit`
与显现效果跨类型嵌套。
Trace 中 `serial A end` 应早于 `serial B begin`，而 `parallel A begin` 与 `parallel B begin` 应都早于任一 end。`typer`
也按段串行运行，其 `pause` 单标记按文字顺序等待。 **串行或交错由 Sink 的 `PostProcess` 调度决定，标签文字只提供段和参数。**

最后的 `AUDIT` 行是诊断示例：`label`、`strength`、`count`、`enabled` 覆盖 String、Float、Int、Boolean 四种属性；内层覆盖
`strength` 后外层恢复，
`mark("checkpoint")` 与无参数 `mark` 写入时间线。它用脉冲着色辅助观察属性，但嵌套效果的主要视觉对比请看上方 SHAKE
行，实例生命周期请看 LIFETIME 行。

每行都使用独立 TMP 对象；`audit` 与 `lifetime` 修改顶点颜色，`serial` 和 `parallel` 只改变透明度。示例字体用英文，便于使用
TMP
默认字体直接运行；写中文时请给 TMP
配置包含中文字形的字体资产。Replay 和 Refresh 会取消并重启管线持有的协程；修改 Bootstrap 字段后使用 Replay，Refresh
不读取尚未提交到管线的新字段值。

## 给策划：怎样写效果文本

1. 找到带 `TextPipeline` 组件的 TMP 对象，改 `TextPipeline` 的 **Original Text**（文本母本）；运行时由程序调用
   `SetOriginalTextSimply` 更换对白。
2. 用 `<标签 : 属性 = 值>正文</标签>` 包住需要作用的文字。未写的属性使用程序给出的默认值。
3. 单次指令写成 `<标签 : 方法(值)>`，如 `<typer : pause(0.4)>`。它没有结束标签，通常放在同类块内。
4. 进入 Play 查看效果。编辑母本后，程序调用 `Refresh()` 可以重新处理；编辑器内直接改 TMP 的显示文本不等同于更新母本。

| 标签            | 用途                         | 可调参数                                                          | 示例                                                       |
|-----------------|------------------------------|-------------------------------------------------------------------|------------------------------------------------------------|
| `typer`         | 逐字出现                     | `speed`：每秒字符数                                               | `<typer : speed = 20>你好！</typer>`                       |
| `typer : pause` | 在当前位置等待               | `seconds`：秒数                                                   | `<typer : pause(0.5)>`                                     |
| `wave`          | 字符上下摆动                 | `amplitude`：像素位移；`frequency`：字符间相位；`speed`：时间速度 | `<wave : amplitude = 6>波浪</wave>`                        |
| `shake`         | 字符随机抖动                 | `intensity`：最大像素位移                                         | `<shake : intensity = 2>警告</shake>`                      |
| `rainbow`       | 字符颜色循环                 | `speed`：时间速度；`spread`：字符间色相差                         | `<rainbow : speed = 0.2>彩虹</rainbow>`                    |
| `lifetime`      | 持续明暗脉冲并展示实例状态   | `label`：状态行；`speed`：脉冲速度；另用通用 `scope` 指定实例     | `<lifetime : speed = 0.6>AGE</lifetime>`                   |
| `audit`         | 脉冲着色并记录属性与标记事件 | `label`、`strength`、`count`、`enabled`；`mark` 方法              | `<audit : label = "outer">A<audit : mark("here")></audit>` |
| `serial`        | 文本段依次逐字显现           | `label`：轨迹名称；`frames`：每字等待帧数                         | `<serial : label = "A", frames = 30>ABC</serial>`          |
| `parallel`      | 文本段交错逐字显现           | `label`：轨迹名称；`frames`：每字等待帧数                         | `<parallel : label = "A", frames = 30>ABC</parallel>`      |

这八个标签由示例代码提供， **只安装包而不导入示例时不会出现**。项目可以用相同方式增加自己的标签。普通正文和 TMP 原生富文本（例如
`<b>加粗</b>`、`<color=#ffcc00>金色</color>`）可以与自定义标签共用。标签名、属性名、方法名均使用小写英文字母开头，之后可含数字、下划线和连字符；大小写敏感。字符串用双引号，数字用
`0.5` 这样的形式，布尔值用 `true`/`false`。块标签要配对闭合；单标记没有闭合标签。

不同效果可以嵌套，例如 `<wave><rainbow>彩色波浪</rainbow></wave>`，但同时修改同一 TMP
网格的效果需要程序协调顶点基准和执行顺序。示例为每种持续效果使用单独文本对象。

## 项目配置与运行时烘焙

配置入口为 **Edit > Project Settings > Text Pipeline**，支持 Sink / Source 类型选择、拖动排序和 Undo / Redo。
编辑数据保存于 `ProjectSettings/TextPipelineSettings.asset`；运行时继续使用 `TextPipelineSettings.Instance`。

进入 Play Mode 前和 Player 构建前（包括脚本调用 `BuildPipeline.BuildPlayer`）会自动将当前配置烘焙为 SO。
新项目默认输出到 `Assets/Resources/Text TextPipeline Settings.asset`；有唯一旧 Resources 配置时原位更新，保留 GUID。
也可通过项目设置页的 **烘焙运行时配置** 或 **Tools > Text Pipeline > Bake Runtime Settings** 手动烘焙。
运行时 SO 的 Inspector 只读，后续配置变更请在 Project Settings 完成。
关闭 Domain Reload 后再次进入 Play 也会重置运行时单例和顺序缓存。

## 给程序：接入已有 UI

1. 在 `TMP_Text` 所在对象或其父对象上加 `TextPipeline`，让 `textMesh` 指向目标 TMP。一个管线管理一份 TMP 文本。
2. 打开 **Edit > Project Settings > Text Pipeline**，在 **Sink Sequence** 加入该 UI 会用到的 Sink 类型并排序；若使用
   `ITextSource`，也在 **Source Sequence** 配置。解析出未配置的
   Sink 时，管线会抛错。
3. 在 `Original Text` 写带标签的母本，或用代码设置：

```csharp
using PipelineComponent = TextPipeline.TextPipeline;

public sealed class DialoguePresenter : UnityEngine.MonoBehaviour
{
    [UnityEngine.SerializeField] private PipelineComponent pipeline;

    public void Show(string authoredText)
    {
        pipeline.SetOriginalTextSimply(authoredText);
    }
}
```

`processTextOnEnable` 默认开启，组件启用时处理已保存的母本。`SetOriginalText(text, withPreProcessing, withPostProcessing)`
可单独选择两个阶段；`Refresh()` 使用当前母本重跑。每次设置新文本及组件禁用时，管线会停止它持有的 Sink 协程。

### 使用自带管线的 TMP 派生组件

`TextPipeline` 仍是可独立挂载的 `MonoBehaviour`，已有场景和 `textMesh` 绑定方式继续使用。另一条入口是
`TextPipelineUGUI : TextMeshProUGUI`：新建 UI 时可以直接添加 **UI > Text Pipeline (TMP)** 组件，它本身就是 TMP 渲染组件，
不需要额外挂载 `TextPipeline`。两条路径共用后处理核心、Settings、Sources 和 Sinks；同一个派生文本不要再绑定独立管线。

```csharp
TMPro.TMP_Text label = GetComponent<TextPipeline.TextPipelineUGUI>();
label.text = "<wave>你好</wave>";
```

派生组件的 `text` setter 先调用 `base.text = value`，完整保留 TMP 原有输入状态和网格、布局 dirty 标记，
再把输入提交为母本。管线内部输出也通过 `base.text` 写入，避免递归。getter 返回最终显示字符串，
`OriginalText` 返回带管线标签的母本；`SetOriginalText` 的阶段开关和 `Refresh` 同样可用。

未激活或 TMP 尚未就绪时保存最新请求，准备好后处理；处理中再次提交的输入在之后的更新中处理。
重复赋值会重建效果。`StopEffects()` 停止管线效果协程并保持文字可见，`Refresh()` 可重新启动；禁用派生组件会同时禁用 TMP 显示。
派生组件 Inspector 的 Text Input 编辑母本并显示只读预览，编辑状态不会启动协程效果。
TMP 的 `SetText(...)` / `SetCharArray(...)` 不经过虚拟属性 setter，需要管线处理时使用 `.text = ...` 或
`SetOriginalText(...)`。

Source/Sink 的宿主属性统一为 `ITextPipeline`，以支持两种入口。已有自定义实现需要把
`TextPipeline TextPipeline { get; set; }` 改为 `ITextPipeline TextPipeline { get; set; }`；Source 的显式接口属性及相关辅助方法也使用该类型。
通过 `TextPipeline.textMesh` 访问渲染数据的代码继续使用。

### 自定义标签的最小实现

Sink 是无参构造的普通类，不要依赖挂在 GameObject 上的实例。同步效果实现 `ITextSink`；跨帧效果实现 `ITextSinkCoroutine`。在
`PostProcess` 内调用每个 `TextSegment` 的 `DoEffect`，框架才会注入该段属性、执行单标记并恢复状态。

```csharp
using TextPipeline;
using TextPipeline.Postprocess;

[Markup("highlight")]
public sealed class HighlightSink : ITextSink
{
    [MarkupProperty("strength", MarkupDataType.Float)]
    public float Strength { get; set; } = 1f;

    public ITextPipeline TextPipeline { get; set; }

    public void PostProcess(TextSegment[] segments)
    {
        foreach (var segment in segments)
            segment.DoEffect(this, range =>
            {
                foreach (var character in range)
                {
                    if (!character.isVisible) continue;
                    // 使用 character.materialReferenceIndex、vertexIndex
                    // 访问 textMesh.textInfo.meshInfo 中对应的四个顶点。
                }
                // 若更改顶点或颜色，调用 textMesh.UpdateVertexData(...)
            });
    }
}
```

`[MarkupProperty]` 只能标注可读可写属性，支持 `Float`、`Int`、`String`、`Boolean` 四种 C# 类型。`[MarkupMethod]` 的方法可返回
`void` 或 `IEnumerator`，参数用 `[MarkupParam]` 标注。默认情况下方法未传的参数取 C# 默认值或类型默认值；必要参数可将
`allowDefault` 设为 `false`。示例中的 `TyperSink.Pause` 展示了协程单标记。

运行时默认只自动扫描 `Assembly-CSharp` 和包运行时程序集。如果 Sink 放在自定义 asmdef，需在场景处理前调用
`MarkupRegistry.RegisterMarkups` 并传入包含所有要用的 Sink 的程序集；该方法会先清空再重建注册表。不要只注册一个新增程序集而漏掉其他业务
Sink。注册时通过反射发现成员、表达式树生成并缓存 getter、setter
与方法委托；文本执行时走缓存委托。详见 [后处理技术](Documentation~/后处理技术.md)。

## 当前边界

- 示例持续效果按各自独立 TMP 网格编写；需要多个效果叠加同一范围时，应在业务代码里共同管理基础顶点与更新顺序。
- `typer` 示例以单个从首字符开始的块展示逐字效果；复杂对白、跨多个块的连续显隐应自行扩展游标管理。
- 动画 Sink 使用 TMP 网格快照；字体、字号、文本布局变化后应调用 `SetOriginalTextSimply` 或 `Refresh()`，让管线重新建立字符范围与网格基准。
