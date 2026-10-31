using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Factories;
using SharpCompress.Providers;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        return await CliApplication
            .RunAsync(args, Console.Out, Console.Error, cancellation.Token)
            .ConfigureAwait(false);
    }
}

/// <summary>Command execution with injectable output for embedding and tests.</summary>
public static class CliApplication
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly JsonSerializerOptions PrettyJson = new(Json) { WriteIndented = true };

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
                ExecuteAsync(
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
                    ExecuteAsync(
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
        var result = root.Parse(args.Length == 0 ? ["--help"] : args);
        if (result.Errors.Count > 0 && result.Action is not HelpAction)
        {
            var machine = result.GetValue(jsonOption) || result.GetValue(ndjsonOption);
            var message = string.Join(" ", result.Errors.Select(e => e.Message));
            await error
                .WriteLineAsync(
                    machine
                        ? JsonSerializer.Serialize(
                            new
                            {
                                SchemaVersion = 1,
                                Error = message,
                                ExitCode = 2,
                            },
                            Json
                        )
                        : message
                )
                .ConfigureAwait(false);
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

    private static async Task<int> ExecuteAsync(
        string command,
        string? path,
        bool json,
        bool ndjson,
        bool longListing,
        string? passwordEnvironment,
        string? encoding,
        bool lookForHeader,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (json && ndjson)
            {
                throw new ArgumentException("Choose --json or --ndjson, not both.");
            }

            var options = lookForHeader
                ? ReaderOptions.ForSelfExtractingArchive()
                : ReaderOptions.ForFilePath;
            if (passwordEnvironment is not null)
            {
                options = options with
                {
                    Password =
                        Environment.GetEnvironmentVariable(passwordEnvironment)
                        ?? throw new ArgumentException("Password environment variable is not set."),
                };
            }
            if (encoding is not null)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                options = options with
                {
                    ArchiveEncoding = new ArchiveEncoding
                    {
                        Default = Encoding.GetEncoding(encoding),
                    },
                };
            }

            if (command == "formats")
            {
                var rows = Factory
                    .Factories.OrderBy(f => f.Name, StringComparer.Ordinal)
                    .Where(f =>
                        path is null || f.Name.Equals(path, StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(f =>
                        (object)
                            new
                            {
                                Kind = "archive",
                                f.Name,
                                Extensions = f.GetSupportedExtensions().ToArray(),
                                Archive = f is IArchiveFactory,
                                Reader = f is IReaderFactory,
                                Writer = f is IWriterFactory,
                            }
                    )
                    .ToList();
                foreach (
                    var wrapper in TarWrapper.Wrappers.Where(w =>
                        w.CompressionType != CompressionType.None
                    )
                )
                {
                    var provider = CompressionProviderRegistry.Default.GetProvider(
                        wrapper.CompressionType
                    );
                    if (
                        path is null
                        || wrapper
                            .CompressionType.ToString()
                            .Equals(path, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        rows.Add(
                            new
                            {
                                Kind = "compressionStream",
                                Name = wrapper.CompressionType.ToString(),
                                Read = provider?.SupportsDecompression == true,
                                Write = provider?.SupportsCompression == true,
                            }
                        );
                    }
                }
                await RenderAsync(rows, output, json, ndjson).ConfigureAwait(false);
                return rows.Count == 0 ? 1 : 0;
            }
            if (path is null)
            {
                throw new ArgumentException("A file path is required.");
            }

            var file = new FileInfo(path);
            var detection = await FileDetector
                .DetectFileAsync(path, options, cancellationToken)
                .ConfigureAwait(false);
            if (command == "detect" || detection is null)
            {
                await RenderAsync(
                        new
                        {
                            Path = path,
                            PhysicalSize = file.Length,
                            Status = detection is null ? "unrecognized" : "recognized",
                            Detection = detection,
                        },
                        output,
                        json,
                        ndjson
                    )
                    .ConfigureAwait(false);
                return detection is null ? 1 : 0;
            }
            if (command == "inspect")
            {
                var information = detection.Archive is null
                    ? null
                    : await ArchiveInspector
                        .InspectArchiveAsync(path, options, cancellationToken)
                        .ConfigureAwait(false);
                await RenderAsync(
                        new
                        {
                            Path = path,
                            PhysicalSize = file.Length,
                            Detection = detection,
                            Information = information,
                            Integrity = "notTested",
                            Limitations = information is null
                                ? "Standalone stream metadata is unavailable without payload processing."
                                : null,
                        },
                        output,
                        json,
                        ndjson
                    )
                    .ConfigureAwait(false);
                return 0;
            }
            var entries = new List<object>();
            long index = 0;
            async Task AddAsync(object entry)
            {
                if (ndjson)
                {
                    await RenderAsync(entry, output, false, true).ConfigureAwait(false);
                }
                else if (json)
                {
                    entries.Add(entry);
                }
                else if (entry is string text)
                {
                    await output.WriteLineAsync(text).ConfigureAwait(false);
                }
                else
                {
                    await RenderAsync(entry, output, false, false).ConfigureAwait(false);
                }
            }
            async Task AddEntryAsync(IEntry entry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = EntryInformation.FromEntry(entry);
                var ordinal = index++;
                await AddAsync(
                        longListing || json || ndjson
                            ? new { Index = ordinal, Metadata = snapshot }
                            : snapshot.Key ?? "(unnamed)"
                    )
                    .ConfigureAwait(false);
            }
            if (detection.Archive is null)
            {
                await AddAsync(
                        new
                        {
                            Index = index++,
                            Kind = "logicalPayload",
                            Name = System.IO.Path.GetFileNameWithoutExtension(path),
                            NameSource = "inferred",
                            Size = (long?)null,
                            Compression = detection.Compression,
                        }
                    )
                    .ConfigureAwait(false);
            }
            else if (detection.Archive.SupportedApis.HasFlag(ArchiveAccessMode.Archive))
            {
                await using var archive = await ArchiveFactory
                    .OpenAsyncArchive(path, options, cancellationToken)
                    .ConfigureAwait(false);
                await foreach (
                    var entry in archive
                        .EntriesAsync.WithCancellation(cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    await AddEntryAsync(entry).ConfigureAwait(false);
                }
            }
            else
            {
                await using var reader = await ReaderFactory
                    .OpenAsyncReader(path, options, cancellationToken)
                    .ConfigureAwait(false);
                while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
                {
                    await AddEntryAsync(reader.Entry).ConfigureAwait(false);
                }
            }
            if (json)
            {
                await RenderAsync(
                        new { Entries = entries, Integrity = "notTested" },
                        output,
                        true,
                        false
                    )
                    .ConfigureAwait(false);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            await error
                .WriteLineAsync(
                    json || ndjson
                        ? JsonSerializer.Serialize(
                            new
                            {
                                SchemaVersion = 1,
                                Error = "Cancelled.",
                                ExitCode = 130,
                            },
                            Json
                        )
                        : "Cancelled."
                )
                .ConfigureAwait(false);
            return 130;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or IOException
                        or UnauthorizedAccessException
                        or SharpCompressException
                        or NotSupportedException
            )
        {
            var code =
                exception is ArgumentException ? 2
                : exception is IOException or UnauthorizedAccessException ? 3
                : 4;
            // Library exception messages can include input data; never echo password sources.
            var message =
                exception is ArgumentException
                    ? exception.Message
                    : "Unable to process input (" + exception.GetType().Name + ").";
            await error
                .WriteLineAsync(
                    json || ndjson
                        ? JsonSerializer.Serialize(
                            new
                            {
                                SchemaVersion = 1,
                                Error = message,
                                ExitCode = code,
                            },
                            Json
                        )
                        : message
                )
                .ConfigureAwait(false);
            return code;
        }
    }

    private static Task RenderAsync(object value, TextWriter output, bool json, bool ndjson)
    {
        if (!json && !ndjson)
        {
            return WriteTextAsync(JsonSerializer.SerializeToElement(value, Json), output, "");
        }
        if (ndjson && value is IEnumerable<object> rows)
        {
            return WriteRowsAsync(rows, output);
        }

        return output.WriteLineAsync(
            JsonSerializer.Serialize(
                new { SchemaVersion = 1, Result = value },
                ndjson ? Json : PrettyJson
            )
        );
    }

    private static async Task WriteTextAsync(JsonElement value, TextWriter output, string prefix)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                await WriteTextAsync(property.Value, output, prefix + property.Name + ".")
                    .ConfigureAwait(false);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                await WriteTextAsync(item, output, prefix + index++ + ".").ConfigureAwait(false);
            }
        }
        else
        {
            await output
                .WriteLineAsync(
                    prefix.TrimEnd('.')
                        + ": "
                        + (
                            value.ValueKind == JsonValueKind.Null
                                ? "unavailable"
                                : value.GetRawText()
                        )
                )
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteRowsAsync(IEnumerable<object> rows, TextWriter output)
    {
        foreach (var row in rows)
        {
            await RenderAsync(row, output, false, true).ConfigureAwait(false);
        }
    }
}
