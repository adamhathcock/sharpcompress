using System.CommandLine;
using SharpCompress.Cli.Formats;
using SharpCompress.Cli.Inspection;
using SharpCompress.Cli.Renders;

namespace SharpCompress.Cli;

internal static class CommandBuilder
{
    public static RootCommand CreateRootCommand(
        ArchiveInspector archiveInspector,
        FormatsInspector formatsInspector
    )
    {
        var rootCommand = new RootCommand("Inspect archive files with SharpCompress.");

        var rootSymbols = CreateInspectionSymbols();
        AddInspectionSymbols(rootCommand, rootSymbols);
        rootCommand.SetAction(parseResult =>
            ExecuteInspectionCommand(
                parseResult,
                rootSymbols,
                archiveInspector,
                InspectionRenderMode.Inspect
            )
        );

        rootCommand.Subcommands.Add(CreateInspectCommand(archiveInspector));
        rootCommand.Subcommands.Add(CreateListCommand(archiveInspector));
        rootCommand.Subcommands.Add(CreateFormatsCommand(formatsInspector));

        return rootCommand;
    }

    private static Command CreateInspectCommand(ArchiveInspector archiveInspector)
    {
        var command = new Command("inspect", "Inspect archives and print summary plus entries.");
        var symbols = CreateInspectionSymbols();
        AddInspectionSymbols(command, symbols);

        command.SetAction(parseResult =>
            ExecuteInspectionCommand(
                parseResult,
                symbols,
                archiveInspector,
                InspectionRenderMode.Inspect
            )
        );

        return command;
    }

    private static Command CreateListCommand(ArchiveInspector archiveInspector)
    {
        var command = new Command("list", "List archive entries.");
        var symbols = CreateInspectionSymbols();
        AddInspectionSymbols(command, symbols);

        command.SetAction(parseResult =>
            ExecuteInspectionCommand(
                parseResult,
                symbols,
                archiveInspector,
                InspectionRenderMode.List
            )
        );

        return command;
    }

    private static Command CreateFormatsCommand(FormatsInspector formatsInspector)
    {
        var command = new Command("formats", "List supported formats and access capabilities.");
        var formatOption = CreateFormatOption();
        command.Options.Add(formatOption);

        command.SetAction(parseResult =>
        {
            var outputFormat = ParseOutputFormat(parseResult.GetValue(formatOption));
            var formats = formatsInspector.GetFormats();
            if (outputFormat == OutputFormat.Json)
            {
                JsonOutputRenderer.RenderFormats(new FormatsReport(formats));
            }
            else
            {
                TableOutputRenderer.RenderFormats(formats);
            }

            return 0;
        });

        return command;
    }

    private static int ExecuteInspectionCommand(
        ParseResult parseResult,
        InspectionSymbols symbols,
        ArchiveInspector archiveInspector,
        InspectionRenderMode renderMode
    )
    {
        var archives = parseResult.GetValue(symbols.ArchiveArgument) ?? [];
        var request = new InspectionRequest
        {
            AccessMode = ParseAccessMode(parseResult.GetValue(symbols.AccessOption)),
            Password = parseResult.GetValue(symbols.PasswordOption),
            LookForHeader = parseResult.GetValue(symbols.LookForHeaderOption),
            ExtensionHint = NormalizeExtensionHint(
                parseResult.GetValue(symbols.ExtensionHintOption)
            ),
            RewindableBufferSize = parseResult.GetValue(symbols.RewindableBufferSizeOption),
            IncludeDirectories = parseResult.GetValue(symbols.IncludeDirectoriesOption),
            Limit = parseResult.GetValue(symbols.LimitOption),
        };

        var outputFormat = ParseOutputFormat(parseResult.GetValue(symbols.FormatOption));
        var longOutput = parseResult.GetValue(symbols.LongOption);
        var report = archiveInspector.InspectArchives(archives, request);

        if (outputFormat == OutputFormat.Json)
        {
            JsonOutputRenderer.RenderInspectionReport(report);
        }
        else if (renderMode == InspectionRenderMode.List)
        {
            TableOutputRenderer.RenderList(report, longOutput);
        }
        else
        {
            TableOutputRenderer.RenderInspect(report, longOutput);
        }

        return report.Errors.Count == 0 ? 0 : 1;
    }

    private static InspectionSymbols CreateInspectionSymbols()
    {
        var archiveArgument = new Argument<string[]>("archive")
        {
            Arity = ArgumentArity.OneOrMore,
            Description = "Archive file paths to inspect.",
        };

        var accessOption = new Option<string>("--access", "-a")
        {
            Description = "Access mode: forward or seekable.",
            DefaultValueFactory = _ => "forward",
        };
        accessOption.Validators.Add(result =>
        {
            var access = result.GetValueOrDefault<string>();
            if (
                !string.Equals(access, "forward", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(access, "seekable", StringComparison.OrdinalIgnoreCase)
            )
            {
                result.AddError("--access must be one of: forward, seekable.");
            }
        });

        var formatOption = CreateFormatOption();

        var longOption = new Option<bool>("--long", "-l")
        {
            Description = "Show additional entry metadata columns.",
        };

        var includeDirectoriesOption = new Option<bool>("--include-directories")
        {
            Description = "Include directory entries in output.",
        };

        var limitOption = new Option<int?>("--limit")
        {
            Description = "Maximum number of entries to print per archive.",
        };
        limitOption.Validators.Add(result =>
        {
            var limit = result.GetValueOrDefault<int?>();
            if (limit is <= 0)
            {
                result.AddError("--limit must be greater than zero.");
            }
        });

        var passwordOption = new Option<string?>("--password")
        {
            Description = "Password for encrypted archives.",
        };

        var lookForHeaderOption = new Option<bool>("--look-for-header")
        {
            Description = "Enable header scanning for self-extracting archives.",
        };

        var extensionHintOption = new Option<string?>("--extension-hint")
        {
            Description = "Optional extension hint (for example: zip, tar.gz, 7z).",
        };

        var rewindableBufferSizeOption = new Option<int?>("--rewindable-buffer-size")
        {
            Description = "Rewindable buffer size for non-seekable stream detection.",
        };
        rewindableBufferSizeOption.Validators.Add(result =>
        {
            var size = result.GetValueOrDefault<int?>();
            if (size is <= 0)
            {
                result.AddError("--rewindable-buffer-size must be greater than zero.");
            }
        });

        return new(
            archiveArgument,
            accessOption,
            formatOption,
            longOption,
            includeDirectoriesOption,
            limitOption,
            passwordOption,
            lookForHeaderOption,
            extensionHintOption,
            rewindableBufferSizeOption
        );
    }

    private static Option<string> CreateFormatOption()
    {
        var formatOption = new Option<string>("--format", "-f")
        {
            Description = "Output format: table or json.",
            DefaultValueFactory = _ => "table",
        };
        formatOption.Validators.Add(result =>
        {
            var output = result.GetValueOrDefault<string>();
            if (
                !string.Equals(output, "table", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(output, "json", StringComparison.OrdinalIgnoreCase)
            )
            {
                result.AddError("--format must be one of: table, json.");
            }
        });
        return formatOption;
    }

    private static void AddInspectionSymbols(Command command, InspectionSymbols symbols)
    {
        command.Arguments.Add(symbols.ArchiveArgument);
        command.Options.Add(symbols.AccessOption);
        command.Options.Add(symbols.FormatOption);
        command.Options.Add(symbols.LongOption);
        command.Options.Add(symbols.IncludeDirectoriesOption);
        command.Options.Add(symbols.LimitOption);
        command.Options.Add(symbols.PasswordOption);
        command.Options.Add(symbols.LookForHeaderOption);
        command.Options.Add(symbols.ExtensionHintOption);
        command.Options.Add(symbols.RewindableBufferSizeOption);
    }

    private static AccessMode ParseAccessMode(string? value) =>
        string.Equals(value, "seekable", StringComparison.OrdinalIgnoreCase)
            ? AccessMode.Seekable
            : AccessMode.Forward;

    private static OutputFormat ParseOutputFormat(string? value) =>
        string.Equals(value, "json", StringComparison.OrdinalIgnoreCase)
            ? OutputFormat.Json
            : OutputFormat.Table;

    private static string? NormalizeExtensionHint(string? extensionHint)
    {
        if (string.IsNullOrWhiteSpace(extensionHint))
        {
            return null;
        }

        return extensionHint.Trim().TrimStart('.');
    }

    private sealed record InspectionSymbols(
        Argument<string[]> ArchiveArgument,
        Option<string> AccessOption,
        Option<string> FormatOption,
        Option<bool> LongOption,
        Option<bool> IncludeDirectoriesOption,
        Option<int?> LimitOption,
        Option<string?> PasswordOption,
        Option<bool> LookForHeaderOption,
        Option<string?> ExtensionHintOption,
        Option<int?> RewindableBufferSizeOption
    );
}
