using System.CommandLine;
using System.CommandLine.Help;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Cli;

/// <summary>Command execution with injectable output for embedding and tests.</summary>
public static class CliApplication
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default
    )
    {
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write versioned JSON output.",
            Recursive = true,
        };
        var ndjsonOption = new Option<bool>("--ndjson")
        {
            Description = "Write newline-delimited JSON records.",
            Recursive = true,
        };
        var passwordOption = new Option<string?>("--password-env")
        {
            Description = "Read the password from an environment variable.",
        };
        var encodingOption = new Option<string?>("--encoding")
        {
            Description = "Entry name encoding.",
        };
        var headerOption = new Option<bool>("--look-for-header")
        {
            Description = "Search for a self-extracting RAR header.",
        };
        var longOption = new Option<bool>("--long")
        {
            Description = "Show detailed entry metadata.",
        };
        var root = new RootCommand(
            "Detect and inspect archives. These commands do not verify payload integrity."
        );
        root.Options.Add(jsonOption);
        root.Options.Add(ndjsonOption);
        var formats = new Command("formats", "List supported archive and compression formats.");
        var formatName = new Argument<string?>("name")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Optional format name.",
        };
        formats.Arguments.Add(formatName);
        formats.SetAction(
            (parse, token) =>
                CommandExecutor.ExecuteAsync(
                    "formats",
                    parse.GetValue(formatName),
                    parse.GetValue(jsonOption),
                    parse.GetValue(ndjsonOption),
                    false,
                    null,
                    null,
                    false,
                    output,
                    error,
                    token
                )
        );
        root.Subcommands.Add(formats);
        foreach (var name in new[] { "detect", "inspect", "list" })
        {
            var command = new Command(
                name,
                name switch
                {
                    "detect" => "Identify an input file.",
                    "inspect" => "Inspect archive metadata.",
                    _ => "List archive entries.",
                }
            );
            var fileArgument = new Argument<string>("file")
            {
                Description = "Path to the input file.",
            };
            command.Arguments.Add(fileArgument);
            command.Options.Add(passwordOption);
            command.Options.Add(encodingOption);
            command.Options.Add(headerOption);
            if (name == "list")
            {
                command.Options.Add(longOption);
            }
            command.SetAction(
                (parse, token) =>
                    CommandExecutor.ExecuteAsync(
                        name,
                        parse.GetRequiredValue(fileArgument),
                        parse.GetValue(jsonOption),
                        parse.GetValue(ndjsonOption),
                        name == "list" && parse.GetValue(longOption),
                        parse.GetValue(passwordOption),
                        parse.GetValue(encodingOption),
                        parse.GetValue(headerOption),
                        output,
                        error,
                        token
                    )
            );
            root.Subcommands.Add(command);
        }
        var result = root.Parse(
            args.Length == 0 ? ["--help"] : args,
            new ParserConfiguration { ResponseFileTokenReplacer = null }
        );
        if (result.Errors.Count > 0 && result.Action is not HelpAction)
        {
            var machine = result.GetValue(jsonOption) || result.GetValue(ndjsonOption);
            var message = string.Join(" ", result.Errors.Select(e => e.Message));
            await OutputRenderer.WriteErrorAsync(error, machine, message, 2).ConfigureAwait(false);
            return 2;
        }
        return await result
            .InvokeAsync(
                new InvocationConfiguration
                {
                    Output = output,
                    Error = error,
                    EnableDefaultExceptionHandler = false,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
