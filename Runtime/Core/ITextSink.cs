using System.Collections;
using TextPipeline.Postprocess;

namespace TextPipeline
{
    public interface ITextSinkBase
    {
        TextPipeline TextPipeline { get; set; }
    }

    public interface ITextSink : ITextSinkBase
    {
        void PostProcess(TextSegment[] segments);
    }

    public interface ITextSinkCoroutine : ITextSinkBase
    {
        /// <summary>
        /// 对整个文本处理流程进行后处理，由 <see cref="TextPipeline"/> 启动，
        /// 并在文本重建或组件被禁用时由 <see cref="TextPipeline"/> 取消。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 职责划分：
        /// <list type="bullet">
        /// <item><description>
        /// <c>PostProcess</c> 负责所有相关文字段的调度；
        /// </description></item>
        /// <item><description>
        /// <see cref="TextSegment.DoEffect(ITextSinkCoroutine, System.Func{TextInfoRange, System.Collections.IEnumerator}, TextSegment.CoroutineType)"> DoEffect </see>
        /// 负责对某一段文字进行实际的处理。
        /// </description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 对不同文字段的调度方式有两种：
        /// <list type="bullet">
        /// <item><description>
        /// 并行：<c>foreach (var seg in segments) seg.DoEffect(this, ProcessOne);</c>
        /// </description></item>
        /// <item><description>
        /// 串行：<c>foreach (var seg in segments) yield return seg.DoEffect(this, ProcessOne);</c>
        /// </description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="segments">需要参与后处理的文字段数组。</param>
        /// <returns>供协程调度使用的迭代器。</returns>
        IEnumerator PostProcess(TextSegment[] segments);
    }
}
