using SharpCompress.Cli.Formats;
using SharpCompress.Cli.Inspection;

namespace SharpCompress.Cli;

public static class CliApp
{
    public static int Run(string[] args)
    {
        args ??= [];
        var rootCommand = CommandBuilder.CreateRootCommand(
            new ArchiveInspector(),
            new FormatsInspector()
        );
        return rootCommand.Parse(args).Invoke();
    }
}
