# 词法结构与语法

## 1. 概述

本语言用于在 Unity 游戏文本中声明局部文本效果与即时指令。文本由以下元素组成：

- **块标签**：为其内部游戏文本声明持续生效的效果范围，例如抖动、颜色、打字速度；
- **单标记**：在文本流当前位置触发一次无内容的游戏文本指令，例如停顿、音效或事件；
- **内容**：角色对白、系统提示、任务描述等普通游戏文本。

块标签与单标记的运行时意义由标签实现定义；属性契约见“Contextual Constraints.md”，实例、scope 与文本收束语义见“Semantics.md”。

## 2. 词法规则

### 2.1 标识符

标签类型 type、函数名 func、属性名与具名函数参数名 param 共用 name 规则：

1. 必须以 ASCII 小写字母 a–z 开头；
2. 后续字符只能是 ASCII 小写字母、数字 0–9、下划线 _ 或连字符 -；
3. 标识符按字符完全相等比较，不进行大小写转换。

例如：shake、play_sound、voice-line-2 合法；Shake、2shake、playSound 非法。

### 2.2 值

属性与参数的取值只能是以下三种字面量：

| 类别 | 形式 | Unity 游戏业务示例 |
|---|---|---|
| 字符串 | 由双引号界定；反斜杠加双引号表示双引号，两个反斜杠表示反斜杠 | "ui_warning"、"Captain \"Nova\"" |
| 数值 | 可选负号、整数部分，以及可选的小数部分 | 42、-3、0.35、1.0 |
| 布尔 | 字面量 true 或 false | true |

数值统一作为 number 解析；是否允许某个范围、是否必须为整数等，属于标签契约与实现校验，不属于词法规则。

### 2.3 空白处理

- 内容中的空白原样保留，并进入正式游戏文本。
- 标签内部的词法单元之间允许空白；空白只用于书写排版，不属于属性名、值或 scope。
- EBNF 中产生式之间的排版空白不参与匹配；content 内的空白则参与匹配。

## 3. 语法结构

### 3.1 块标签

块标签由开始标签、零或多个子元素、结束标签构成。显式书写的开始标签有四种形式：

```html
<type>
<type : attr = value>
<type | scope = integer>
<type : attr = value, attr = value | scope = integer>
```

属性列表以 : 引导；属性之间以 , 分隔；scope 以 | 引导且必须位于开始标签最后。

```html
<shake>护盾受损</shake>

<shake : intensity = 0.8, frequency = 12>
    警告：反应堆温度异常！
</shake>

<voice | scope = 3>
    指挥官通讯已接入。
</voice>

<shake : intensity = 1.2, frequency = 16 | scope = 1>
    敌方主炮充能中！
</shake>
```

结束标签不携带属性、函数参数或 scope：

```html
</shake>
```

显式结束标签的匹配与缺失结束标签的容错恢复，见“Contextual Constraints.md”和“Error Handling.md”。

### 3.2 单标记

单标记表示一次无内容的游戏文本指令：

```html
<type:func>
<type:func(value)>
<type:func(param:value, param:value)>
<type:func(param:value, param:value) | scope = integer>
```

`func` 必填。参数列表整体可省略；出现时以圆括号包围，并且只能采用下列一种形式：

- **单值形式**：仅写一个 `value`，不写参数名；
- **具名形式**：一个或多个 `param:value`，以 `,` 分隔。

两种形式不得混用；单值形式不能写多个值。单值实参如何绑定到函数参数见“Contextual Constraints.md”的 C4。单标记不含结束标签，也不接受内容。

```html
<pause:wait(duration:0.35)>
<pause:wait(0.35)>
<audio:play(clip:"ui_warning", loop:false)>
<event:raise(id:"reactor_overheat") | scope = 1>
```

函数与参数是否被允许、参数的默认值和数据类型，见“Contextual Constraints.md”。

### 3.3 内容与转义

正文中的 <、> 与 & 是语法保留字符，若要作为普通游戏文本显示，必须转义：

| 原字符 | 转义序列 | 游戏文本示例 |
|---|---|---|
| < | &< | 温度 &< 0°C |
| > | &> | 能量 &> 80% |
| & | && | R&&D 终端已连接 |

除上述三种保留字符外，任意 Unicode 字符都可直接作为内容。

## 4. 完整游戏文本示例

```html
<shake : intensity = 0.8, frequency = 12 | scope = 0>
    警告：反应堆温度异常！
    <pause:wait(0.35)>
    <color : tint = "#ff5b5b">立即撤离。</color>
</shake>

<audio:play(clip:"ui_warning", loop:false)>
```

该示例只说明语言的书写形式：shake、pause、color 与 audio 的实际效果、参数契约和处理顺序由 Unity 项目中的标签实现及 TextPipeline 配置决定。

## 5. EBNF

~~~ebnf
(* ---------- 基础字符 ---------- *)
any-character   ::= ? any Unicode scalar value ? ;
lowerletter     ::= "a" | "b" | "c" | "d" | "e" | "f" | "g" | "h" | "i" | "j"
                  | "k" | "l" | "m" | "n" | "o" | "p" | "q" | "r" | "s" | "t"
                  | "u" | "v" | "w" | "x" | "y" | "z" ;
digit           ::= "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" ;
space           ::= " " | "\t" | "\n" | "\r" ;

(* ---------- 词法单元 ---------- *)
name            ::= lowerletter { lowerletter | digit | "_" | "-" } ;
type            ::= name ;
func            ::= name ;
param           ::= name ;

integer         ::= digit { digit } ;
number          ::= [ "-" ] integer [ "." integer ] ;
string-char     ::= any-character - '"' - "\" | "\" ( '"' | "\" ) ;
quoted-string   ::= '"' { string-char } '"' ;

(* ---------- 公共片段 ---------- *)
value                ::= quoted-string | number | "true" | "false" ;
scope-part           ::= "|" "scope" "=" integer ;
prop-list            ::= param "=" value { "," param "=" value } ;
single-markup-params ::= value | ( param ":" value { "," param ":" value }) ;

(* ---------- 块标签 ---------- *)
block-start     ::= "<" type [ ":" prop-list ] [ scope-part ] ">" ;
block-end       ::= "<" "/" type ">" ;
block           ::= block-start { element } block-end ;

(* ---------- 单标记 ---------- *)
single-markup   ::= "<" type ":" func [ "(" single-markup-params ")" ] [ scope-part ] ">" ;

(* ---------- 内容与转义 ---------- *)
escape          ::= "&" ( "<" | ">" | "&" ) ;
text-char       ::= any-character - "<" - ">" - "&" ;
content-item    ::= escape | text-char ;
content         ::= content-item { content-item } ;

(* ---------- 层次 ---------- *)
element         ::= block | single-markup | content ;
root            ::= { element } ;
~~~
