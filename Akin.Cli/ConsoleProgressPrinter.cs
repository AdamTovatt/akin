using Akin.Core.Models;

namespace Akin.Cli
{
    /// <summary>
    /// Prints index progress to <see cref="Console.Error"/> as a single
    /// transient line that overwrites itself, so the user sees live status
    /// while <c>akin reindex</c> or the initial search-triggered build is
    /// running. Only used in CLI mode; the MCP path passes a null progress
    /// reporter so background indexing doesn't spam the MCP logs.
    /// </summary>
    internal sealed class ConsoleProgressPrinter : IProgress<IndexProgress>
    {
        private readonly object _lock = new object();
        private int _lastLineLength;

        public void Report(IndexProgress value)
        {
            lock (_lock)
            {
                string line = Format(value);
                WriteLine(line);
            }
        }

        public void Finish()
        {
            lock (_lock)
            {
                if (_lastLineLength > 0)
                {
                    Console.Error.WriteLine();
                    _lastLineLength = 0;
                }
            }
        }

        private static string Format(IndexProgress progress)
        {
            return progress.Phase switch
            {
                "scanning" => "Scanning files...",
                "persisting" => $"Persisting {progress.ChunksDone} chunks...",
                _ => FormatIndexing(progress),
            };
        }

        private static string FormatIndexing(IndexProgress progress)
        {
            string counter = progress.FilesTotal > 0
                ? $"[{progress.FilesDone}/{progress.FilesTotal}]"
                : $"[{progress.FilesDone}]";

            string file = progress.CurrentFile ?? "";
            return string.IsNullOrEmpty(file)
                ? $"Indexing {counter}"
                : $"Indexing {counter} {Truncate(file, 80)}";
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength) return value;
            return "…" + value.Substring(value.Length - (maxLength - 1));
        }

        private void WriteLine(string line)
        {
            int padding = Math.Max(0, _lastLineLength - line.Length);
            Console.Error.Write('\r');
            Console.Error.Write(line);
            if (padding > 0) Console.Error.Write(new string(' ', padding));
            _lastLineLength = line.Length;
        }
    }
}
