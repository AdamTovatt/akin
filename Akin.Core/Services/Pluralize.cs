namespace Akin.Core.Services
{
    /// <summary>
    /// Tiny helper for pluralizing English nouns in user-facing output. Keeps
    /// formatting code in commands and MCP tools concise and avoids the
    /// "count == 1 ? "" : "s"" pattern appearing in multiple places.
    /// </summary>
    public static class Pluralize
    {
        public static string Of(int count, string singular, string? plural = null)
        {
            return count == 1 ? singular : (plural ?? singular + "s");
        }
    }
}
