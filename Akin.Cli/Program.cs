using Akin.Core.Commands;
using Akin.Core.Interfaces;
using Akin.Core.Models;
using Akin.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akin.Cli
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("--version", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(AssemblyVersion());
                return 0;
            }

            bool isMcpMode = args.Length > 0 &&
                (args[0].Equals("--mcp", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("mcp", StringComparison.OrdinalIgnoreCase));

            string? repoRoot = RepoLocator.FindRepoRoot(Directory.GetCurrentDirectory());
            if (repoRoot == null)
            {
                Console.Error.WriteLine("No git repository found. Run akin from inside a git repository.");
                return 1;
            }

            RepoContext context;
            try
            {
                context = await RepoContext.OpenAsync(repoRoot);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to open repo context: {ex.Message}");
                return 1;
            }

            try
            {
                if (isMcpMode)
                {
                    // MCP: no progress printer — a long-running background index
                    // shouldn't spam the MCP host's stderr log.
                    ICommandFactory mcpFactory = new CommandFactory(context);
                    return await RunMcpServerAsync(context, mcpFactory, args);
                }

                // CLI: print progress to stderr so reindex and initial-build-triggered
                // searches don't look frozen. Suppress the live overwrite behaviour
                // when stderr is redirected (pipes, log files).
                ConsoleProgressPrinter? printer = Console.IsErrorRedirected ? null : new ConsoleProgressPrinter();
                ICommandFactory factory = new CommandFactory(context, printer);
                int exitCode = await RunCliAsync(factory, args);
                printer?.Finish();
                return exitCode;
            }
            finally
            {
                await context.DisposeAsync();
            }
        }

        private static string AssemblyVersion()
        {
            Version? version = typeof(Program).Assembly.GetName().Version;
            return version?.ToString() ?? "unknown";
        }

        private static async Task<int> RunCliAsync(ICommandFactory factory, string[] args)
        {
            try
            {
                ICommand command = factory.CreateFromArgs(args);
                CommandResult result = await command.ExecuteAsync(CancellationToken.None);

                Console.WriteLine(result.Message);
                if (!string.IsNullOrEmpty(result.Details))
                    Console.WriteLine(result.Details);

                return result.Success ? 0 : 1;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error: {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> RunMcpServerAsync(RepoContext context, ICommandFactory factory, string[] args)
        {
            try
            {
                // Do not block here on EnsureIndexReadyAsync — for a large repo that
                // is a minute or more of work, which blocks the MCP stdio handshake
                // long enough that the client times out and kills the process. The
                // IndexBackgroundService handles initial indexing in the background
                // after the MCP host starts, so searches arriving before the index
                // is built just return empty results until it's ready.
                HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });

                builder.Services.AddSingleton(context);
                builder.Services.AddSingleton(factory);
                builder.Services.AddHostedService<IndexBackgroundService>();

                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly();

                builder.Services.AddSingleton<McpTools>();

                IHost host = builder.Build();
                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"MCP server error: {ex.Message}");
                return 1;
            }
        }
    }
}
