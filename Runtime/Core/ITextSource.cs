namespace TextPipeline
{
    public interface ITextSource
    {
        /// <summary>
        /// 希望在特殊情况下可以反向请求开启管线操作
        /// </summary>
        TextPipeline TextPipeline { set; }

        /// <summary>
        /// 由管线注入预处理文本
        /// </summary>
        /// <param name="text">待处理文本</param>
        /// <returns>预处理后的文本</returns>
        string PreProcess(string text);
    }
}