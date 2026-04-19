# Akin

[![Tests](https://github.com/AdamTovatt/akin/actions/workflows/dotnet.yml/badge.svg)](https://github.com/AdamTovatt/akin/actions/workflows/dotnet.yml)
[![NuGet Version](https://img.shields.io/nuget/v/Akin.svg)](https://www.nuget.org/packages/Akin)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

Semantic code search for AI agents and CLI users. Finds code by meaning rather than exact text match. Works as both a CLI tool and an MCP (Model Context Protocol) server. Runs entirely locally — no API keys, no external services.

Built on the [VectorSharp](https://github.com/AdamTovatt/vector-sharp) libraries, using the Nomic Embed Text v1.5 model via ONNX for embeddings and VectorSharp.Chunking for structure-aware chunking of Markdown, C#, JavaScript/TypeScript/JSX/TSX, HTML, CSS, Python, and plain text.

## Installation

### Install script (macOS / Linux)

```bash
curl -fsSL https://raw.githubusercontent.com/AdamTovatt/akin/master/install-akin.sh | bash
```

This detects your platform, downloads the latest release, verifies its SHA256 checksum, and installs the `akin` binary to `/usr/local/bin`. Requires only `curl`, `unzip`, and `sha256sum`/`shasum` — all standard on macOS and Linux. Run the same command again to update.

### Updaemon (Linux)

If you use [updaemon](https://github.com/AdamTovatt/updaemon) for managing tools:

```bash
updaemon new akin --from github --remote AdamTovatt/akin/akin-linux-arm64.zip --type cli
updaemon set-exec-name akin Akin.Cli
updaemon init akin
```

Replace `akin-linux-arm64.zip` with `akin-linux-x64.zip` on x86_64 systems. Future updates via `updaemon update`.

### .NET tool

```bash
dotnet tool install --global Akin
```

After installation, the `akin` command is available globally.

Update: `dotnet tool update --global Akin`. Uninstall: `dotnet tool uninstall --global Akin`.

### MCP registration

To register as an MCP server in Claude Code:

```bash
claude mcp add akin -- akin --mcp
```

For Cursor or other MCP clients, add to your MCP configuration:

```json
{
  "mcpServers": {
    "akin": {
      "command": "akin",
      "args": ["--mcp"]
    }
  }
}
```

## Usage

```bash
akin search <query> [--max N] [--no-snippets] [--min-score S]   # Semantic search
akin status                                                     # Show index status
akin reindex                                                    # Force a full reindex
akin --mcp                                                      # Run as MCP server
akin --version                                                  # Show version
akin help                                                       # Show help
```

### Examples

```bash
# Natural-language query across the current repository
akin search "where do we handle authentication failures"

# Cap the number of files returned
akin search "database connection pooling" --max 5

# Skip snippet text for a broad survey
akin search "error handling" --no-snippets

# Only surface strong matches
akin search "cosine similarity" --min-score 0.75

# Check whether the index is built and current
akin status
```

### Search output

Results are grouped by file. Each file carries an aggregate score (the maximum score across its matching chunks) and one or more matching regions with their own line ranges and scores. Pass `--no-snippets` to omit the chunk text, useful when you want file names only for a broad survey.

## How it works

Akin stores its index in a `.akin/` folder at the repository root. Add `.akin/` to your `.gitignore` to keep it out of version control. The folder contains three files:

- `vectors.bin` — the vector store in VectorSharp's binary format
- `chunks.json` — chunk metadata (file path, line range, content hash, text)
- `manifest.json` — embedding model identifier, dimension, chunker fingerprint, schema version

On startup, Akin compares the stored manifest against its current configuration. If the embedding model or chunker has changed, the index is rebuilt automatically. If copied to a new worktree (e.g. via [worktree-initializer](https://github.com/AdamTovatt/worktree-initializer)), the index is reused and only the changed files are re-embedded.

### Files indexed

Akin runs `git ls-files` to enumerate tracked files, so the set of indexed files exactly matches what you have chosen to version. Binary files are detected by scanning the first 8KB for null bytes and skipped. Files larger than 2MB are skipped.

### Chunking

Files are chunked using the predefined VectorSharp.Chunking format for their extension:

| Extension | Format |
|-----------|--------|
| `.md`, `.markdown` | Markdown |
| `.cs` | C# |
| `.js`, `.jsx`, `.ts`, `.tsx`, `.mjs`, `.cjs` | JavaScript |
| `.html`, `.htm` | HTML |
| `.css`, `.scss` | CSS |
| `.py`, `.pyi` | Python |
| anything else | plain text |

### Incremental updates (MCP mode)

When running as an MCP server, Akin watches the repository for file changes via `FileSystemWatcher` and debounces events into batched incremental reindexes. A periodic reconciliation every five minutes diffs the current tracked set against the index to catch events the watcher may have dropped, as well as files that have become gitignored or newly tracked.

In CLI mode (short-lived invocations), no watcher is used — each invocation opens the existing index, runs the command, and exits.

## As MCP server

```bash
akin --mcp
```

When running as an MCP server, the following tools are available:

- `akin_search(query, maxResults?, includeSnippets?, minimumScore?)` — Semantic code search
- `akin_status()` — Report index state, file and chunk counts, compatibility
- `akin_reindex()` — Force a full rebuild of the index

## Development

```bash
git clone <repository-url>
cd akin
dotnet build Akin.slnx
dotnet test Akin.slnx
```

Run as MCP server during development:

```bash
dotnet run --project Akin.Cli/Akin.Cli.csproj -- --mcp
```

Package locally:

```bash
dotnet pack Akin.Cli/Akin.Cli.csproj --configuration Release
```

## License

MIT License
