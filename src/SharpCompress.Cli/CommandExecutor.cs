using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Readers;

namespace SharpCompress.Cli;

internal static class CommandExecutor
{
    internal static async Task<int> ExecuteAsync(
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
                return await FormatsCommand
                    .ExecuteAsync(path, output, json, ndjson)
                    .ConfigureAwait(false);
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
                await OutputRenderer
                    .RenderAsync(
                        new DetectResult(
                            path,
                            file.Length,
                            detection is null ? "unrecognized" : "recognized",
                            detection
                        ),
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
                await OutputRenderer
                    .RenderAsync(
                        new InspectResult(
                            path,
                            file.Length,
                            detection,
                            information,
                            information is null
                                ? "Standalone stream metadata is unavailable without payload processing."
                                : null
                        ),
                        output,
                        json,
                        ndjson
                    )
                    .ConfigureAwait(false);
                return 0;
            }
            return await ListCommand
                .ExecuteAsync(
                    path,
                    detection,
                    options,
                    output,
                    json,
                    ndjson,
                    longListing,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await OutputRenderer
                .WriteErrorAsync(error, json || ndjson, "Cancelled.", 130)
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
            await OutputRenderer
                .WriteErrorAsync(error, json || ndjson, message, code)
                .ConfigureAwait(false);
            return code;
        }
    }
}
