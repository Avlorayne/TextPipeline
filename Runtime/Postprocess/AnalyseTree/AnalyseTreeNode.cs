using System.Collections.Generic;
using System.Reflection;

namespace TextPipeline.Postprocess
{
    /// <summary>
    /// TMP 渲染结果的语法树节点。
    /// <para><see cref="StartIndex"/> 与 <see cref="EndIndex"/> 是去除已收束标记后
    /// 正式文本中的零基左闭右开区间。对于 <see cref="ContentNode"/>，该区间同时
    /// 用于构造它的 <see cref="ContentNode.Content"/>。</para>
    /// <para><see cref="BlockNode"/> 覆盖其内部正式文本；
    /// <see cref="SingleMarkerNode"/> 没有正文，起止位置相等，表示正式文本中的
    /// 一个插入点；<see cref="ContentNode"/> 覆盖正文字符。可见与不可见字符一并计入。</para>
    /// </summary>
    public abstract class Node
    {
        public int StartIndex;
        public int EndIndex;
        public BlockNode Parent;

        /// <summary>该节点覆盖的无标记 TMP 字符数。</summary>
        public int CharacterCount => EndIndex - StartIndex;

        /// <summary>此节点是否覆盖无标记 TMP characterInfo 中的指定下标。</summary>
        public bool ContainsCharacterIndex(int characterIndex)
            => characterIndex >= StartIndex && characterIndex < EndIndex;
    }

    public class BlockNode : Node
    {
        public string Markup;
        public KeyValuePair<string, string>[] PropertyPairs;
        public int ScopeNum = 0;

        public readonly List<Node> Children = new();
        public ITextSinkBase PostProcessor;

        public readonly List<ITextSinkBase> SignPost = new();
    }

    public sealed class RootNode : BlockNode
    {
        private readonly List<MarkupParseDiagnostic> diagnostics = new();

        /// <summary>解析器在局部容错时记录的原始输入坐标诊断。</summary>
        public IReadOnlyList<MarkupParseDiagnostic> Diagnostics => diagnostics;

        internal void AddDiagnostic(MarkupParseErrorKind kind, int startIndex, int endIndex, string message)
        {
            diagnostics.Add(new MarkupParseDiagnostic(kind, startIndex, endIndex, message));
        }
    }

    public class SingleMarkerNode : Node
    {
        public string Markup;
        public string MethodName;
        public KeyValuePair<string, string>[] ParamPairs;
        public int ScopeNum = 0;
        
        public ITextSinkBase PostProcessor;
    }

    /// <summary>
    /// 纯文本叶子。<see cref="Content"/> 是原始 TMP 文本信息在
    /// <c>[StartIndex, EndIndex)</c> 内的受限视图，不复制字符或网格数据。
    /// </summary>
    public class ContentNode : Node
    {
        public TextInfoRange Content { get; internal set; }
    }
}
