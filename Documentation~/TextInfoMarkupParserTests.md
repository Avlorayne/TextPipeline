# TextInfoMarkupParser 测试说明

## 目标与范围

本文档说明 `TextInfoMarkupParserTests` 对 `TextInfoMarkupParser` 的 EditMode 覆盖范围。
测试关注从标记文本到 `RootNode`、`BlockNode`、`SingleMarkerNode` 与 `ContentNode` 的树形结果；不覆盖后续 `TextPipeline` 的实际渲染、处理实例执行或 TMP 网格修改。

## 测试夹具

测试在每个用例开始前调用 `MarkupRegistry.RegisterMarkups()`，注册当前测试程序集中的两个模拟 Sink：

- `wave`：属性 `speed:number`、`label:string`；方法 `pause(duration:number)` 与 `play(clip:string, loop:boolean)`。
- `echo`：无属性、无方法，用于验证不同块标签间的栈式恢复。

这样每个用例只依赖 Attribute/Registry 契约，不依赖场景对象或 Inspector 配置。

## 用例清单

| 用例 | 输入/条件 | 关键断言 |
| --- | --- | --- |
| `Parse_BuildsTreeAndBindsSingleValueParameter` | 嵌套 `wave` 块和 `<wave:pause(0.5)>` | 树的父子关系、块属性、scope、单值参数绑定为 `duration`，以及正式文本 `ABCD` 中的节点范围。 |
| `Parse_AssignsSingleMarkerScopeAndCharacterCounts` | `ab<wave:pause(0.5) | scope = 7>cd` | `SingleMarkerNode.ScopeNum == 7`；Marker 位于 `[2, 2)`、`CharacterCount == 0`；前后正文和 Root 均以 `abcd` 的四个字符计数。该用例同时保护 `)` 与 `| scope` 间允许空白。 |
| `Parse_InjectsMarkerStrippedTextInfoAndBindsContentRanges` | 原始标记文本与 `abcd` 的 `TMP_TextInfo` | 每个 `ContentNode.Content` 是注入数据的受限区间；通过该区间写入会修改原始 `TMP_TextInfo`。 |
| `Parse_RejectsInjectedTextInfoThatDoesNotMatchFormalText` | 原始文本与字符序列不一致的 `TMP_TextInfo` | 解析器拒绝不对应正式文本的注入快照，避免节点下标指向错误字符。 |
| `Parse_ValidatesContractsAndSkipsOnlyInvalidCandidates` | 属性 `speed` 使用布尔值，后接合法 `play` 指令 | 无效标签产生 `Contract` 诊断并被跳过；后续合法单标记仍被保留。 |
| `Parse_ClosesUnfinishedBlocksAtEndOfInput` | 外层 `wave` 缺少结束标签 | 输入结束时由内至外隐式闭合，两个块的 `EndIndex` 均为正式文本 `text` 的末尾（4）。 |
| `Parse_ImplicitlyClosesNestedBlocksAndIgnoresOrphanEndTags` | `wave` 内嵌 `echo`，由 `</wave>` 触发恢复 | `echo` 在匹配结束标签前隐式闭合；外层关闭；两个块范围均结束于正式文本位置 4，后续孤立结束标签产生诊断。 |
| `Parse_UsesQuotedRecoveryBoundaryAndLeavesNonTagsAsContent` | 非法大写标签的字符串值含 `>`，并包含非标签 `<` | 恢复边界忽略引号中的 `>`；非标签文本保留为内容；词法诊断位置稳定。 |

## 重要约定

- `StartIndex` 与 `EndIndex` 是左闭右开的**去标记正式文本**索引；`CharacterCount` 恒等于 `EndIndex - StartIndex`。成功标签和按 E4 跳过的候选标签均不占用该坐标系。
- `SingleMarkerNode` 没有正文，因而总是零长度区间 `[p, p)`；`BlockNode` 的范围仅覆盖其内部正式文本。
- `ContentNode.Content` 直接保存去标记 `TMP_TextInfo` 的受限区间。可用 `TextInfoMarkupParser.Parse(原始标记文本, 去标记TextInfo)` 注入真实渲染快照；通过 `Content` 处理的字符会写回同一份 TMP 数据。仅传字符串时，解析器生成一个轻量字符快照，适合编辑器工具和测试，不提供重新排版后的 TMP 网格数据。
- `RootNode.Diagnostics` 不属于节点范围；其 `StartIndex`/`EndIndex` 仍是原始输入坐标，方便直接指出出错的标记文本。
- 省略 `scope` 时，解析器会保留节点字段的默认值 `0`；显式 scope 必须为 `Int32` 范围内的非负整数。
- 候选标签不符合词法、语法、契约或上下文约束时，解析器记录 `Root.Diagnostics` 并执行标签级恢复，而不会中断后续文本的解析。

## 执行方式

1. 在 Unity 中打开 **Window > General > Test Runner**。
2. 选择 **EditMode**。
3. 过滤或选择 `TextPipeline.Editor.Tests.TextInfoMarkupParserTests`，点击 **Run Selected**。

测试程序集由 `TextPipeline.Editor.Tests.asmdef` 声明，仅在 Editor 平台且定义 `UNITY_INCLUDE_TESTS` 时参与编译。
