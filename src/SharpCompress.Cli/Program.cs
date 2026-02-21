using SharpCompress.Cli;
using SharpCompress.Cli.Formats;
using SharpCompress.Cli.Inspection;

var rootCommand = CommandBuilder.CreateRootCommand(new ArchiveInspector(), new FormatsInspector());
return rootCommand.Parse(args).Invoke();
