namespace Akin.Core.Services
{
    /// <summary>
    /// Simple token counting for chunker budgeting. Uses a character-based approximation
    /// that roughly matches BERT-style subword tokenization (~4 characters per token).
    /// Exact counts aren't needed since the chunker only uses this to enforce a soft
    /// maximum — a slight over- or undercount just shifts chunk boundaries by a line
    /// or two.
    /// </summary>
    public static class TokenCounter
    {
        public static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return Math.Max(1, text.Length / 4);
        }
    }
}
