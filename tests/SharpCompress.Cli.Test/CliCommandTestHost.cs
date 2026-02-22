using System;
using System.IO;

namespace SharpCompress.Cli.Test;

internal static class CliCommandTestHost
{
    public static CliCommandResult Invoke(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var outputWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        Console.SetOut(outputWriter);
        Console.SetError(errorWriter);

        try
        {
            var exitCode = global::SharpCompress.Cli.CliApp.Run(args);
            return new CliCommandResult(exitCode, outputWriter.ToString(), errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    internal sealed record CliCommandResult(int ExitCode, string StdOut, string StdErr);
}
