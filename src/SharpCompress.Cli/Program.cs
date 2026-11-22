using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Preserve Unicode archive names in redirected output on Windows as well.
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
