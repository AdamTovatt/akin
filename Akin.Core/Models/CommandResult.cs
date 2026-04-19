namespace Akin.Core.Models
{
    /// <summary>
    /// The outcome of a CLI or MCP command execution.
    /// </summary>
    /// <param name="Success">Whether the command completed successfully.</param>
    /// <param name="Message">The primary message to surface to the caller.</param>
    /// <param name="Details">Optional longer-form output (multi-line text).</param>
    public record CommandResult(bool Success, string Message, string? Details = null);
}
