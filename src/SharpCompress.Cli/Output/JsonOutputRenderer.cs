using System;
using System.Text.Json;
using SharpCompress.Cli.Formats;
using SharpCompress.Cli.Inspection;

namespace SharpCompress.Cli.Output;

internal static class JsonOutputRenderer
{
    public static void RenderInspectionReport(InspectionExecutionResult report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var json = JsonSerializer.Serialize(
            report,
            CliJsonSerializerContext.Default.InspectionExecutionResult
        );
        Console.WriteLine(json);
    }

    public static void RenderFormats(FormatsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var json = JsonSerializer.Serialize(report, CliJsonSerializerContext.Default.FormatsReport);
        Console.WriteLine(json);
    }
}
